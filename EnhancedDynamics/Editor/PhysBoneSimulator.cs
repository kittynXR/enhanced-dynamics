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
        
        // Integration type
        private enum IntegrationType
        {
            Simplified,
            Advanced
        }
        private IntegrationType _integrationType = IntegrationType.Simplified;
        
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
            public Quaternion currentRotation;
            public Vector3 velocity;
            public float length;
            public float boneRadius; // For collision
            public BoneState parent;
            public List<BoneState> children = new List<BoneState>();
            
            // Gravity-modified rest position
            public Vector3 gravityRestPosition;
            public Quaternion gravityRestRotation;
            
            // Previous frame data for momentum
            public Vector3 previousPosition;
            public Quaternion previousRotation;
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
                        currentRotation = transform.rotation,
                        velocity = Vector3.zero,
                        previousPosition = transform.position,
                        previousRotation = transform.rotation
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
            
            // Calculate gravity rest positions
            CalculateGravityRestPositions();
        }
        
        private void BuildBoneChainRecursive(Transform current, BoneState parent)
        {
            var bone = new BoneState
            {
                transform = current,
                restPosition = current.localPosition,
                restRotation = current.localRotation,
                currentPosition = current.position,
                currentRotation = current.rotation,
                velocity = Vector3.zero,
                parent = parent,
                previousPosition = current.position,
                previousRotation = current.rotation
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
        
        private void CalculateGravityRestPositions()
        {
            // Calculate gravity-modified rest positions for each bone
            foreach (var bone in _bones)
            {
                if (bone.parent == null)
                {
                    // Root bone doesn't move
                    bone.gravityRestPosition = bone.transform.position;
                    bone.gravityRestRotation = bone.transform.rotation;
                }
                else
                {
                    // Calculate how much this bone should move due to gravity
                    float gravityRatio = _physBone.gravity;
                    
                    // Apply gravity falloff based on chain depth
                    if (_physBone.gravityFalloff > 0)
                    {
                        int depth = GetBoneDepth(bone);
                        gravityRatio *= Mathf.Pow(1f - _physBone.gravityFalloff, depth);
                    }
                    
                    // Gravity modifies the rest position between current rest and straight down
                    Vector3 restWorldPos = bone.parent.transform.TransformPoint(bone.restPosition);
                    Vector3 downPos = bone.parent.transform.position + Vector3.down * bone.length;
                    
                    // Interpolate between rest position and down position based on gravity
                    bone.gravityRestPosition = Vector3.Lerp(restWorldPos, downPos, gravityRatio);
                    
                    // Calculate rotation to point from parent to gravity rest position
                    Vector3 gravityDir = (bone.gravityRestPosition - bone.parent.transform.position).normalized;
                    bone.gravityRestRotation = Quaternion.LookRotation(gravityDir, bone.parent.transform.up);
                }
            }
        }
        
        private int GetBoneDepth(BoneState bone)
        {
            int depth = 0;
            BoneState current = bone;
            while (current.parent != null)
            {
                depth++;
                current = current.parent;
            }
            return depth;
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
            // First, update gravity rest positions (they can change if root moves)
            UpdateGravityRestPositions();
            
            // Apply physics to each bone
            foreach (var bone in _bones)
            {
                if (bone.parent == null) 
                {
                    // Root follows the transform
                    bone.currentPosition = bone.transform.position;
                    bone.currentRotation = bone.transform.rotation;
                    continue;
                }
                
                // Store previous state for momentum
                bone.previousPosition = bone.currentPosition;
                bone.previousRotation = bone.currentRotation;
                
                // Calculate target based on gravity-modified rest position
                Vector3 targetPosition = bone.gravityRestPosition;
                
                // Pull is the ONLY force that moves bones toward their gravity-modified rest position
                float pullStrength = _physBone.pull;
                
                // Calculate pull force
                Vector3 pullDirection = targetPosition - bone.currentPosition;
                Vector3 pullForce = pullDirection * pullStrength;
                
                // Apply pull to velocity
                bone.velocity += pullForce * deltaTime;
                
                // Spring/Momentum affects oscillation
                if (_integrationType == IntegrationType.Simplified)
                {
                    // Spring in simplified mode controls oscillation
                    float springDamping = 1f - (_physBone.spring * 0.5f); // Spring reduces oscillation
                    bone.velocity *= springDamping;
                }
                else
                {
                    // Advanced mode would use momentum instead
                    // TODO: Implement advanced mode with momentum
                }
                
                // Stiffness competes with pull to keep bones in previous orientation
                float stiffnessStrength = _physBone.stiffness;
                if (stiffnessStrength > 0)
                {
                    // Stiffness tries to maintain the previous frame's position
                    Vector3 stiffnessForce = (bone.previousPosition - bone.currentPosition) * stiffnessStrength;
                    bone.velocity += stiffnessForce * deltaTime;
                }
                
                // Immobilize reduces all motion
                float immobilize = GetImmobilize();
                if (immobilize > 0)
                {
                    bone.velocity *= 1f - immobilize;
                }
                
                // Apply velocity
                bone.currentPosition += bone.velocity * deltaTime;
                
                // Constrain to parent distance (maintain bone length)
                if (bone.parent != null && bone.length > 0)
                {
                    Vector3 toParent = bone.currentPosition - bone.parent.currentPosition;
                    if (toParent.magnitude > 0.001f)
                    {
                        toParent = toParent.normalized * bone.length;
                        bone.currentPosition = bone.parent.currentPosition + toParent;
                    }
                }
                
                // Apply damping to velocity
                bone.velocity *= 0.95f; // General damping to prevent infinite motion
                
                // Update transform
                bone.transform.position = bone.currentPosition;
                
                // Calculate rotation to point away from parent
                if (bone.parent != null)
                {
                    Vector3 boneDirection = (bone.currentPosition - bone.parent.currentPosition).normalized;
                    if (boneDirection.magnitude > 0.001f)
                    {
                        // Maintain up vector relative to parent
                        Vector3 parentUp = bone.parent.currentRotation * Vector3.up;
                        bone.currentRotation = Quaternion.LookRotation(boneDirection, parentUp);
                        bone.transform.rotation = bone.currentRotation;
                    }
                }
            }
        }
        
        private void UpdateGravityRestPositions()
        {
            // Update gravity rest positions based on current parent positions
            foreach (var bone in _bones)
            {
                if (bone.parent != null)
                {
                    // Recalculate gravity position based on current parent
                    float gravityRatio = _physBone.gravity;
                    
                    // Apply gravity falloff
                    if (_physBone.gravityFalloff > 0)
                    {
                        int depth = GetBoneDepth(bone);
                        gravityRatio *= Mathf.Pow(1f - _physBone.gravityFalloff, depth);
                    }
                    
                    // Calculate positions
                    Vector3 restWorldPos = bone.parent.currentPosition + (bone.parent.currentRotation * bone.restPosition);
                    Vector3 downPos = bone.parent.currentPosition + Vector3.down * bone.length;
                    
                    // Interpolate based on gravity
                    bone.gravityRestPosition = Vector3.Lerp(restWorldPos, downPos, gravityRatio);
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
                bone.currentRotation = bone.transform.rotation;
                bone.previousPosition = bone.currentPosition;
                bone.previousRotation = bone.currentRotation;
                bone.velocity = Vector3.zero;
            }
            
            // Recalculate gravity rest positions
            CalculateGravityRestPositions();
        }
    }
}