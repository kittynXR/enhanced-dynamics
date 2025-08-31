using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace EnhancedDynamics.Editor
{
    /// <summary>
    /// Simple avatar hiding system to prevent build pipeline triggers during physics preview.
    /// Hidden avatars don't run their components, preventing VRCFury/NDMF from triggering.
    /// </summary>
    public static class AvatarHiding
    {
        private static Dictionary<GameObject, bool> _originalAvatarStates = new Dictionary<GameObject, bool>();
        private static GameObject _physicsClone = null;
        private static GameObject _originalAvatarForClone = null; // Store reference to the original avatar
        private static bool _isHiding = false;
        
        /// <summary>
        /// Hide all VRC avatars and create a physics-only clone of the selected avatar
        /// </summary>
        public static bool HideAvatarsAndCreatePhysicsClone()
        {
            if (_isHiding)
            {
                Debug.LogWarning("[EnhancedDynamics] Avatars already hidden");
                return false;
            }
            
            try
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Hiding all VRC avatars for physics preview...");
                }
                
                // Find all VRC avatars in the scene
                var avatars = GameObject.FindObjectsOfType<VRCAvatarDescriptor>();
                
                if (avatars.Length == 0)
                {
                    Debug.LogWarning("[EnhancedDynamics] No VRC avatars found in scene");
                    return false;
                }
                
                // Determine the avatar and component that should be focused
                GameObject selectedAvatar = null;
                Component triggeringComponent = null;
                
                // Check if current selection has a physics component
                if (Selection.activeGameObject != null)
                {
                    triggeringComponent = Selection.activeGameObject.GetComponent<VRCPhysBone>();
                    if (triggeringComponent == null)
                        triggeringComponent = Selection.activeGameObject.GetComponent<VRCPhysBoneCollider>();
                    
                    if (triggeringComponent != null)
                    {
                        // Find the avatar this component belongs to
                        selectedAvatar = GetAvatarFromComponent(triggeringComponent);
                        
                        // Set the physics context for component tracking
                        PhysicsContextTracker.SetPreviewContext(triggeringComponent);
                    }
                }
                
                // Store original states (but don't hide yet)
                _originalAvatarStates.Clear();
                foreach (var avatar in avatars)
                {
                    var rootObj = avatar.gameObject;
                    _originalAvatarStates[rootObj] = rootObj.activeSelf;
                    
                    // Check if this avatar is selected (fallback if no component was found)
                    if (selectedAvatar == null && Selection.activeGameObject != null && 
                        (Selection.activeGameObject == rootObj || Selection.activeGameObject.transform.IsChildOf(rootObj.transform)))
                    {
                        selectedAvatar = rootObj;
                    }
                }
                
                // If no specific avatar selected, use the first one with PhysBones
                if (selectedAvatar == null)
                {
                    foreach (var avatar in avatars)
                    {
                        if (avatar.gameObject.GetComponentsInChildren<VRCPhysBone>(true).Length > 0)
                        {
                            selectedAvatar = avatar.gameObject;
                            if (EnhancedDynamicsSettings.DebugMode)
                            {
                                if (EnhancedDynamicsSettings.DebugMode)
                            {
                                Debug.Log($"[EnhancedDynamics] Auto-selected avatar with PhysBones: {selectedAvatar.name}");
                            }
                            }
                            break;
                        }
                    }
                }
                
                // Store reference to original; capture snapshots lazily on save to speed up preview start
                if (selectedAvatar != null)
                {
                    _originalAvatarForClone = selectedAvatar;
                }
                
                // Now hide all avatars
                int hiddenCount = 0;
                foreach (var avatar in avatars)
                {
                    var rootObj = avatar.gameObject;
                    
                    // Hide the avatar - this is CRITICAL for preventing build pipeline
                    bool wasActive = rootObj.activeSelf;
                    rootObj.SetActive(false);
                    
                    if (wasActive)
                    {
                        hiddenCount++;
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log($"[EnhancedDynamics] ✓ Hidden active avatar: {rootObj.name}");
                        }
                        }
                    }
                    else
                    {
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log($"[EnhancedDynamics] • Avatar already inactive: {rootObj.name}");
                        }
                    }
                    
                    // Verify the avatar is actually hidden
                    if (rootObj.activeSelf)
                    {
                        Debug.LogError($"[EnhancedDynamics] ❌ FAILED to hide avatar: {rootObj.name} - BUILD PIPELINE MAY TRIGGER!");
                    }
                }
                
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log($"[EnhancedDynamics] Avatar hiding complete: {hiddenCount} active avatars hidden out of {avatars.Length} total");
                }
                
                // Create physics clone if we have a selected avatar
                if (selectedAvatar != null)
                {
                    CreatePhysicsClone(selectedAvatar);
                    
                    if (_physicsClone != null)
                    {
                        
                        // Apply context to the clone and select the corresponding component
                        if (PhysicsContextTracker.HasActiveContext)
                        {
                            var cloneComponent = PhysicsContextTracker.GetCorrespondingCloneComponent(_physicsClone);
                            if (cloneComponent != null)
                            {
                                PhysicsContextTracker.ApplyContextToClone(cloneComponent);
                                
                                // Select the corresponding component in the clone instead of just the root
                                Selection.activeGameObject = cloneComponent.gameObject;
                                if (EnhancedDynamicsSettings.DebugMode)
                                {
                                    Debug.Log($"[EnhancedDynamics] Selected clone component: {cloneComponent.GetType().Name} on {cloneComponent.gameObject.name}");
                                }
                                
                                // Ping the object to expand hierarchy and highlight it
                                EditorApplication.delayCall += () =>
                                {
                                    EditorGUIUtility.PingObject(cloneComponent.gameObject);
                                    if (EnhancedDynamicsSettings.DebugMode)
                                    {
                                        Debug.Log($"[EnhancedDynamics] Pinged clone component to expand hierarchy: {cloneComponent.gameObject.name}");
                                    }
                                };
                            }
                            else
                            {
                                // Fallback to selecting the clone root
                                Selection.activeGameObject = _physicsClone;
                                if (EnhancedDynamicsSettings.DebugMode)
                                {
                                    Debug.Log("[EnhancedDynamics] No corresponding component found, selected clone root");
                                }
                                
                                // Ping the clone root to expand hierarchy
                                EditorApplication.delayCall += () =>
                                {
                                    EditorGUIUtility.PingObject(_physicsClone);
                                    if (EnhancedDynamicsSettings.DebugMode)
                                    {
                                        if (EnhancedDynamicsSettings.DebugMode)
                                {
                                    Debug.Log($"[EnhancedDynamics] Pinged clone root to expand hierarchy: {_physicsClone.name}");
                                }
                                    }
                                };
                            }
                        }
                        else
                        {
                            // No context, just select the clone root
                            Selection.activeGameObject = _physicsClone;
                            if (EnhancedDynamicsSettings.DebugMode)
                            {
                                Debug.Log("[EnhancedDynamics] No active context, selected clone root");
                            }
                            
                            // Ping the clone root to expand hierarchy
                            EditorApplication.delayCall += () =>
                            {
                                EditorGUIUtility.PingObject(_physicsClone);
                                if (EnhancedDynamicsSettings.DebugMode)
                                {
                                    Debug.Log($"[EnhancedDynamics] Pinged clone root to expand hierarchy: {_physicsClone.name}");
                                }
                            };
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[EnhancedDynamics] No avatar selected and no avatars with PhysBones found");
                }
                
                _isHiding = true;
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log($"[EnhancedDynamics] Hidden {avatars.Length} avatars, created physics clone: {_physicsClone?.name}");
                }
                
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error hiding avatars: {e}");
                RestoreAvatars(); // Cleanup on error
                return false;
            }
        }
        
        /// <summary>
        /// Restore all hidden avatars and cleanup physics clone
        /// </summary>
        public static void RestoreAvatars()
        {
            if (!_isHiding)
            {
                return;
            }
            
            try
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Restoring hidden avatars...");
                }
                
                // Clear physics context
                PhysicsContextTracker.ClearContext();
                
                // Clear original snapshots if no changes were saved
                if (!PhysicsChangeMemory.HasPendingChanges)
                {
                    PhysicsChangeMemory.ClearOriginalSnapshots();
                }
                
                // Restore original avatar states
                foreach (var kvp in _originalAvatarStates)
                {
                    var avatar = kvp.Key;
                    var wasActive = kvp.Value;
                    
                    if (avatar != null)
                    {
                        avatar.SetActive(wasActive);
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log($"[EnhancedDynamics] Restored avatar: {avatar.name} (active: {wasActive})");
                        }
                    }
                }
                
                // Cleanup physics clone
                if (_physicsClone != null)
                {
                    GameObject.DestroyImmediate(_physicsClone);
                    _physicsClone = null;
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log("[EnhancedDynamics] Destroyed physics clone");
                    }
                }
                
                
                _originalAvatarStates.Clear();
                _originalAvatarForClone = null; // Clear reference
                _isHiding = false;
                
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Avatar restoration complete");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error restoring avatars: {e}");
            }
        }
        
        public static bool IsHiding => _isHiding;
        
        private static void CreatePhysicsClone(GameObject originalAvatar)
        {
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log($"[EnhancedDynamics] Creating physics clone of: {originalAvatar.name}");
            }
            
            // Create a copy of the avatar
            _physicsClone = GameObject.Instantiate(originalAvatar);
            // IMPORTANT: Keep the exact same name to ensure path matching works
            _physicsClone.name = originalAvatar.name;
            
            // Clean the clone of build-triggering components and AvatarDescriptor to prevent pipelines
            CleanPhysicsClone(_physicsClone);

            // Ensure physics components have distinct IDs to avoid conflicts
            ResetPhysicsComponentIds(_physicsClone);
            
            // Make sure the clone is active
            _physicsClone.SetActive(true);
            
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log($"[EnhancedDynamics] Physics clone created: {_physicsClone.name}");
            }
        }
        
        private static void CleanPhysicsClone(GameObject clone)
        {
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log($"[EnhancedDynamics] Cleaning non-physics components from clone...");
            }
            
            // Get all components in the clone hierarchy
            var allComponents = clone.GetComponentsInChildren<Component>(true);
            int removedCount = 0;
            
            foreach (var component in allComponents)
            {
                if (component == null) continue;
                
                var componentType = component.GetType();
                
                // Keep essential components
                if (IsEssentialComponent(componentType))
                {
                    continue;
                }
                
                // Remove problematic components that trigger builds
                if (IsBuildTriggeringComponent(componentType))
                {
                    GameObject.DestroyImmediate(component);
                    removedCount++;
                }
                else if (componentType == typeof(VRCAvatarDescriptor) && EnhancedDynamicsSettings.PreventModularAvatarInPreview)
                {
                    // Explicitly remove AvatarDescriptor so VRChat/NDMF/VRCF build hooks ignore the clone
                    GameObject.DestroyImmediate(component);
                    removedCount++;
                }
            }
            
            // Fix VRChat physics system duplicate shape errors
            // Reset physics component IDs to prevent conflicts
            ResetPhysicsComponentIds(clone);
            
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log($"[EnhancedDynamics] Removed {removedCount} build-triggering components from clone");
            }
        }
        
        private static bool IsEssentialComponent(Type componentType)
        {
            // Keep these component types
            return componentType == typeof(Transform) ||
                   componentType == typeof(VRCPhysBone) ||
                   componentType == typeof(VRCPhysBoneCollider) ||
                   componentType == typeof(SkinnedMeshRenderer) ||
                   componentType == typeof(MeshRenderer) ||
                   componentType == typeof(MeshFilter) ||
                   componentType == typeof(Animator);
        }
        
        /// <summary>
        /// Reset physics component IDs to prevent VRChat's "Shape with id X is already in execution group" error
        /// </summary>
        private static void ResetPhysicsComponentIds(GameObject clone)
        {
            try
            {
                // Get all PhysBone components in the clone
                var physBones = clone.GetComponentsInChildren<VRCPhysBone>(true);
                foreach (var physBone in physBones)
                {
                    // Force Unity to regenerate the component's internal ID
                    physBone.enabled = false;
                    physBone.enabled = true;
                }
                
                // Get all PhysBoneCollider components in the clone
                var physBoneColliders = clone.GetComponentsInChildren<VRCPhysBoneCollider>(true);
                foreach (var collider in physBoneColliders)
                {
                    // Force Unity to regenerate the component's internal ID
                    collider.enabled = false;
                    collider.enabled = true;
                }
                
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log($"[EnhancedDynamics] Reset physics component IDs: {physBones.Length} PhysBones, {physBoneColliders.Length} PhysBoneColliders");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error resetting physics component IDs: {e}");
            }
        }

        private static bool IsBuildTriggeringComponent(Type componentType)
        {
            var typeName = componentType.FullName ?? "";
            var namespaceName = componentType.Namespace ?? "";
            
            // Remove VRCFury components
            if (EnhancedDynamicsSettings.PreventVRCFuryInPreview && (namespaceName.StartsWith("VF.") || typeName.Contains("VRCFury")))
            {
                return true;
            }
            
            // Remove Modular Avatar components  
            if (EnhancedDynamicsSettings.PreventModularAvatarInPreview && (
                namespaceName.StartsWith("nadena.dev.modular_avatar") || 
                namespaceName.StartsWith("nadena.dev.ndmf") ||
                typeName.Contains("ModularAvatar")))
            {
                return true;
            }
            
            // Remove VRChat pipeline manager and related build pipeline components
            if (EnhancedDynamicsSettings.PreventModularAvatarInPreview && (typeName.Contains("PipelineManager") || namespaceName.StartsWith("VRC.Core")))
            {
                return true;
            }

            // Remove other known build-triggering components
            if (typeName.Contains("Prefabulous") ||
                typeName.Contains("Vixen") ||
                typeName.Contains("BuildPipeline"))
            {
                return true;
            }
            
            return false;
        }
        
        private static GameObject GetAvatarFromComponent(Component component)
        {
            if (component == null) return null;
            
            // Walk up the hierarchy to find the VRCAvatarDescriptor
            var current = component.transform;
            while (current != null)
            {
                var avatarDescriptor = current.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
                if (avatarDescriptor != null)
                {
                    return current.gameObject;
                }
                current = current.parent;
            }
            
            // If no avatar descriptor found, return the root of the hierarchy
            current = component.transform;
            while (current.parent != null)
            {
                current = current.parent;
            }
            
            return current.gameObject;
        }
        
        /// <summary>
        /// Get the current physics clone if available
        /// </summary>
        public static GameObject PhysicsClone => _physicsClone;
        
        /// <summary>
        /// Get the original avatar that the clone was created from
        /// </summary>
        public static GameObject OriginalAvatarForClone => _originalAvatarForClone;
    }
}
