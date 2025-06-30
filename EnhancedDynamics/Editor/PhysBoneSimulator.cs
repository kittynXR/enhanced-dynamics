using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace EnhancedDynamics.Editor
{
    /// <summary>
    /// Handles the actual physics simulation for PhysBones in edit mode
    /// </summary>
    public class PhysBoneSimulator
    {
        private VRCPhysBone _physBone;
        private List<BoneState> _bones = new List<BoneState>();
        private Transform _rootTransform;
        private bool _initialized = false;
        
        // Reflection cache
        private static bool _reflectionCached = false;
        private static FieldInfo _bonesField;
        private static FieldInfo _rootTransformField;
        private static MethodInfo _initMethod;
        private static MethodInfo _updateMethod;
        private static Type _boneType;
        
        private class BoneState
        {
            public Transform transform;
            public Vector3 restPosition;
            public Quaternion restRotation;
            public Vector3 currentPosition;
            public Vector3 velocity;
            public float length;
            public BoneState parent;
            public List<BoneState> children = new List<BoneState>();
        }
        
        public PhysBoneSimulator(VRCPhysBone physBone)
        {
            _physBone = physBone;
            CacheReflection();
            Initialize();
        }
        
        private static void CacheReflection()
        {
            if (_reflectionCached) return;
            
            try
            {
                var physBoneType = typeof(VRCPhysBone);
                
                // Try to find internal fields and methods
                _bonesField = physBoneType.GetField("bones", BindingFlags.NonPublic | BindingFlags.Instance) ??
                             physBoneType.GetField("_bones", BindingFlags.NonPublic | BindingFlags.Instance) ??
                             physBoneType.GetField("m_bones", BindingFlags.NonPublic | BindingFlags.Instance);
                
                _rootTransformField = physBoneType.GetField("rootTransform", BindingFlags.Public | BindingFlags.Instance) ??
                                     physBoneType.GetField("_rootTransform", BindingFlags.NonPublic | BindingFlags.Instance);
                
                _initMethod = physBoneType.GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Instance) ??
                             physBoneType.GetMethod("Init", BindingFlags.NonPublic | BindingFlags.Instance) ??
                             physBoneType.GetMethod("Setup", BindingFlags.NonPublic | BindingFlags.Instance);
                
                _updateMethod = physBoneType.GetMethod("UpdatePhysBone", BindingFlags.NonPublic | BindingFlags.Instance) ??
                               physBoneType.GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance) ??
                               physBoneType.GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
                
                // Look for internal bone type
                var nestedTypes = physBoneType.GetNestedTypes(BindingFlags.NonPublic);
                foreach (var type in nestedTypes)
                {
                    if (type.Name.Contains("Bone") || type.Name.Contains("Particle"))
                    {
                        _boneType = type;
                        break;
                    }
                }
                
                _reflectionCached = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to cache PhysBone reflection: {e}");
            }
        }
        
        private void Initialize()
        {
            try
            {
                // Get root transform
                _rootTransform = _physBone.GetRootTransform();
                if (_rootTransform == null)
                {
                    _rootTransform = _physBone.transform;
                }
                
                // Try to call init method
                if (_initMethod != null)
                {
                    _initMethod.Invoke(_physBone, null);
                }
                
                // Build bone chain manually if reflection fails
                BuildBoneChain();
                
                _initialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to initialize PhysBone simulator: {e}");
            }
        }
        
        private void BuildBoneChain()
        {
            _bones.Clear();
            
            // Try to get affected transforms via reflection
            List<Transform> transforms = null;
            try
            {
                var getAffectedMethod = typeof(VRCPhysBone).GetMethod("GetAffectedTransforms", BindingFlags.Public | BindingFlags.Instance);
                if (getAffectedMethod != null)
                {
                    transforms = getAffectedMethod.Invoke(_physBone, null) as List<Transform>;
                }
            }
            catch { }
            
            if (transforms == null || transforms.Count == 0)
            {
                // Fallback: build chain from root
                BuildBoneChainRecursive(_rootTransform, null);
            }
            else
            {
                // Build from affected transforms
                Dictionary<Transform, BoneState> transformToBone = new Dictionary<Transform, BoneState>();
                
                foreach (var transform in transforms)
                {
                    if (transform == null) continue;
                    
                    var bone = new BoneState
                    {
                        transform = transform,
                        restPosition = transform.localPosition,
                        restRotation = transform.localRotation,
                        currentPosition = transform.position,
                        velocity = Vector3.zero
                    };
                    
                    _bones.Add(bone);
                    transformToBone[transform] = bone;
                }
                
                // Set up parent-child relationships
                foreach (var bone in _bones)
                {
                    if (bone.transform.parent != null && transformToBone.ContainsKey(bone.transform.parent))
                    {
                        bone.parent = transformToBone[bone.transform.parent];
                        bone.parent.children.Add(bone);
                        bone.length = Vector3.Distance(bone.transform.position, bone.parent.transform.position);
                    }
                }
            }
        }
        
        private void BuildBoneChainRecursive(Transform current, BoneState parent)
        {
            var bone = new BoneState
            {
                transform = current,
                restPosition = current.localPosition,
                restRotation = current.localRotation,
                currentPosition = current.position,
                velocity = Vector3.zero,
                parent = parent
            };
            
            if (parent != null)
            {
                parent.children.Add(bone);
                bone.length = Vector3.Distance(current.position, parent.transform.position);
            }
            
            _bones.Add(bone);
            
            // Add children up to max chain length
            int maxChainLength = GetMaxChainLength();
            if (_bones.Count < maxChainLength)
            {
                foreach (Transform child in current)
                {
                    if (ShouldIncludeTransform(child))
                    {
                        BuildBoneChainRecursive(child, bone);
                    }
                }
            }
        }
        
        private int GetMaxChainLength()
        {
            try
            {
                var prop = typeof(VRCPhysBone).GetProperty("maxChainLength", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    return (int)prop.GetValue(_physBone);
                }
                
                var field = typeof(VRCPhysBone).GetField("maxChainLength", BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    return (int)field.GetValue(_physBone);
                }
            }
            catch { }
            
            return 10; // Default fallback
        }
        
        private float GetImmobilize()
        {
            try
            {
                var prop = typeof(VRCPhysBone).GetProperty("immobilize", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    return (float)prop.GetValue(_physBone);
                }
                
                var field = typeof(VRCPhysBone).GetField("immobilize", BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    return (float)field.GetValue(_physBone);
                }
            }
            catch { }
            
            return 0f; // Default fallback
        }
        
        private List<Transform> GetExclusions()
        {
            try
            {
                var prop = typeof(VRCPhysBone).GetProperty("exclusions", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    return prop.GetValue(_physBone) as List<Transform>;
                }
                
                var field = typeof(VRCPhysBone).GetField("exclusions", BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    return field.GetValue(_physBone) as List<Transform>;
                }
            }
            catch { }
            
            return null;
        }
        
        private List<Transform> GetIgnoreTransforms()
        {
            try
            {
                var prop = typeof(VRCPhysBone).GetProperty("ignoreTransforms", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    return prop.GetValue(_physBone) as List<Transform>;
                }
                
                var field = typeof(VRCPhysBone).GetField("ignoreTransforms", BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    return field.GetValue(_physBone) as List<Transform>;
                }
            }
            catch { }
            
            return null;
        }
        
        private bool ShouldIncludeTransform(Transform transform)
        {
            // Skip if excluded
            var exclusions = GetExclusions();
            if (exclusions != null && exclusions.Contains(transform))
                return false;
            
            // Skip if it has its own PhysBone
            if (transform.GetComponent<VRCPhysBone>() != null)
                return false;
            
            // Include if in ignore transforms list (they should still be simulated)
            var ignoreTransforms = GetIgnoreTransforms();
            if (ignoreTransforms != null && ignoreTransforms.Contains(transform))
                return true;
            
            return true;
        }
        
        public void Simulate(float deltaTime)
        {
            if (!_initialized) return;
            
            // Try reflection first
            if (_updateMethod != null)
            {
                try
                {
                    _updateMethod.Invoke(_physBone, null);
                    return;
                }
                catch { }
            }
            
            // Fallback to manual simulation
            ManualSimulate(deltaTime);
        }
        
        private void ManualSimulate(float deltaTime)
        {
            // Apply forces to each bone
            foreach (var bone in _bones)
            {
                if (bone.parent == null) continue; // Skip root
                
                // Gravity
                var gravity = Vector3.down * _physBone.gravity * deltaTime;
                bone.velocity += gravity;
                
                // Pull (return to rest force)
                var restWorldPos = bone.parent.transform.TransformPoint(bone.restPosition);
                var pullForce = (restWorldPos - bone.currentPosition) * _physBone.pull * deltaTime;
                bone.velocity += pullForce;
                
                // Spring (angular return force)
                var currentDir = (bone.currentPosition - bone.parent.transform.position).normalized;
                var restDir = bone.parent.transform.TransformDirection(bone.restRotation * Vector3.forward);
                var springForce = Vector3.Cross(currentDir, restDir) * _physBone.spring * deltaTime;
                bone.velocity += springForce;
                
                // Damping
                bone.velocity *= 1f - (_physBone.stiffness * deltaTime);
                
                // Immobilize
                bone.velocity *= 1f - GetImmobilize();
                
                // Update position
                bone.currentPosition += bone.velocity * deltaTime;
                
                // Constraint to length
                if (bone.parent != null && bone.length > 0)
                {
                    var dir = (bone.currentPosition - bone.parent.transform.position).normalized;
                    bone.currentPosition = bone.parent.transform.position + dir * bone.length;
                }
                
                // Apply to transform
                bone.transform.position = bone.currentPosition;
                
                // Look at parent
                if (bone.parent != null)
                {
                    var lookDir = bone.parent.transform.position - bone.transform.position;
                    if (lookDir != Vector3.zero)
                    {
                        bone.transform.rotation = Quaternion.LookRotation(-lookDir, bone.transform.up);
                    }
                }
            }
        }
        
        public void Reset()
        {
            foreach (var bone in _bones)
            {
                bone.transform.localPosition = bone.restPosition;
                bone.transform.localRotation = bone.restRotation;
                bone.currentPosition = bone.transform.position;
                bone.velocity = Vector3.zero;
            }
        }
    }
}