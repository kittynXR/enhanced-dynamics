using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace EnhancedDynamics.Runtime.Physics
{
    /// <summary>
    /// Component that simulates VRCPhysBone physics in editor mode
    /// </summary>
    [ExecuteAlways]
    public class PhysBonePreviewComponent : MonoBehaviour
    {
        private VRCPhysBone physBone;
        private List<BoneInfo> bones = new List<BoneInfo>();
        private bool initialized = false;
        private float fixedTimeStep = 1f / 90f; // 90Hz like VRChat
        private float timeAccumulator = 0f;
        
        private class BoneInfo
        {
            public Transform transform;
            public Vector3 worldRestPosition;
            public Quaternion worldRestRotation;
            public Vector3 currentPosition;
            public Vector3 previousPosition;
            public float length;
            public BoneInfo parent;
            public int depth;
            
            // Cached transform data
            public Transform rootTransform;
            public Vector3 localRestPosition;
            public Quaternion localRestRotation;
        }
        
        void Awake()
        {
            physBone = GetComponent<VRCPhysBone>();
            if (physBone == null)
            {
                enabled = false;
                return;
            }
        }
        
        void OnEnable()
        {
            if (!Application.isPlaying && physBone != null)
            {
                Initialize();
                // Subscribe to editor update
                EditorApplication.update += EditorUpdate;
            }
        }
        
        void OnDisable()
        {
            if (!Application.isPlaying)
            {
                EditorApplication.update -= EditorUpdate;
                RestoreBones();
            }
        }
        
        void Initialize()
        {
            bones.Clear();
            
            var rootTransform = physBone.GetRootTransform();
            if (rootTransform == null)
            {
                rootTransform = physBone.transform;
            }
            
            // Build bone chain
            BuildBoneChain(rootTransform, null, 0);
            
            // Initialize positions
            foreach (var bone in bones)
            {
                bone.currentPosition = bone.transform.position;
                bone.previousPosition = bone.transform.position;
                bone.worldRestPosition = bone.transform.position;
                bone.worldRestRotation = bone.transform.rotation;
            }
            
            initialized = true;
            Debug.Log($"[PhysBonePreview] Initialized {bones.Count} bones for {physBone.name}");
        }
        
        void BuildBoneChain(Transform current, BoneInfo parent, int depth)
        {
            // VRChat limits depth
            if (depth > 256) return;
            
            var bone = new BoneInfo
            {
                transform = current,
                parent = parent,
                depth = depth,
                rootTransform = physBone.GetRootTransform() ?? physBone.transform,
                localRestPosition = current.localPosition,
                localRestRotation = current.localRotation
            };
            
            // Calculate bone length from parent
            if (parent != null)
            {
                bone.length = Vector3.Distance(current.position, parent.transform.position);
            }
            
            bones.Add(bone);
            
            // Add children
            foreach (Transform child in current)
            {
                // Skip if child has its own PhysBone
                if (child.GetComponent<VRCPhysBone>() != null) continue;
                BuildBoneChain(child, bone, depth + 1);
            }
        }
        
        void EditorUpdate()
        {
            if (!initialized || bones.Count == 0 || Application.isPlaying) return;
            
            // Accumulate time for fixed timestep
            timeAccumulator += Time.deltaTime;
            
            // Force scene view to update
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.Repaint();
            }
            
            // Run physics at fixed timestep
            while (timeAccumulator >= fixedTimeStep)
            {
                UpdatePhysics(fixedTimeStep);
                timeAccumulator -= fixedTimeStep;
            }
        }
        
        void UpdatePhysics(float deltaTime)
        {
            // Update root transform movement
            UpdateRootMovement();
            
            // Apply forces and integrate
            foreach (var bone in bones)
            {
                if (bone.parent == null) continue; // Skip root
                
                // Get current world space rest position based on parent
                Vector3 parentPos = bone.parent.currentPosition;
                Quaternion parentRot = bone.parent.transform.rotation;
                Vector3 targetRestPos = parentPos + (parentRot * bone.localRestPosition);
                
                // Apply gravity to rest position (not as a force)
                float gravity = physBone.gravity;
                if (gravity != 0)
                {
                    // Gravity affects the rest position, with falloff
                    float gravityFalloff = Mathf.Max(0, physBone.gravityFalloff);
                    float gravityStrength = gravity * (1f - gravityFalloff * bone.depth / 10f);
                    targetRestPos += Vector3.down * gravityStrength * bone.length * 0.1f;
                }
                
                // Calculate forces
                Vector3 restoreForce = Vector3.zero;
                
                // Pull - main restoring force
                float pull = Mathf.Clamp01(physBone.pull);
                if (pull > 0)
                {
                    Vector3 toRest = targetRestPos - bone.currentPosition;
                    restoreForce += toRest * pull;
                }
                
                // Spring/Stiffness - resistance to bending
                float spring = Mathf.Clamp01(physBone.spring);
                float stiffness = Mathf.Clamp01(physBone.stiffness);
                
                // Verlet integration with damping
                Vector3 velocity = bone.currentPosition - bone.previousPosition;
                float damping = 1f - (spring * 0.95f); // Spring affects damping
                velocity *= (1f - damping);
                
                // Store previous position
                bone.previousPosition = bone.currentPosition;
                
                // Update position
                bone.currentPosition += velocity + (restoreForce * deltaTime * deltaTime);
                
                // Apply stiffness as rotation constraint
                if (stiffness > 0 && bone.parent != null)
                {
                    Vector3 parentForward = bone.parent.transform.rotation * Vector3.forward;
                    Vector3 currentDir = (bone.currentPosition - parentPos).normalized;
                    Vector3 stiffDir = Vector3.Slerp(currentDir, parentForward, stiffness * 0.5f);
                    bone.currentPosition = parentPos + stiffDir * bone.length;
                }
                
                // Constrain to bone length
                if (bone.length > 0)
                {
                    Vector3 toParent = bone.currentPosition - parentPos;
                    float currentLength = toParent.magnitude;
                    
                    // Apply stretch limits (VRChat default: can't stretch)
                    if (currentLength > bone.length)
                    {
                        bone.currentPosition = parentPos + (toParent.normalized * bone.length);
                    }
                    // Apply squish limits (VRChat default: can squish to 0)
                    else if (currentLength < bone.length * 0.0f)
                    {
                        bone.currentPosition = parentPos + (toParent.normalized * bone.length * 0.0f);
                    }
                }
                
                // Apply position to transform
                bone.transform.position = bone.currentPosition;
                
                // Update rotation to look away from parent
                if (bone.parent != null)
                {
                    Vector3 dir = (bone.currentPosition - parentPos).normalized;
                    if (dir.magnitude > 0.001f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(dir, bone.parent.transform.up);
                        bone.transform.rotation = targetRot;
                    }
                }
            }
        }
        
        void UpdateRootMovement()
        {
            // Update bone positions if root has moved
            var rootBone = bones.Count > 0 ? bones[0] : null;
            if (rootBone != null && rootBone.transform != null)
            {
                Vector3 rootDelta = rootBone.transform.position - rootBone.currentPosition;
                if (rootDelta.magnitude > 0.001f)
                {
                    // Move all bones with root
                    foreach (var bone in bones)
                    {
                        bone.currentPosition += rootDelta;
                        bone.previousPosition += rootDelta;
                    }
                }
            }
        }
        
        void RestoreBones()
        {
            foreach (var bone in bones)
            {
                if (bone.transform != null)
                {
                    bone.transform.localPosition = bone.localRestPosition;
                    bone.transform.localRotation = bone.localRestRotation;
                }
            }
            bones.Clear();
            initialized = false;
        }
        
        void OnDestroy()
        {
            if (!Application.isPlaying)
            {
                EditorApplication.update -= EditorUpdate;
                RestoreBones();
            }
        }
    }
}