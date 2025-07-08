using System;
using UnityEngine;
using UnityEditor;

namespace EnhancedDynamics.Editor
{
    /// <summary>
    /// Handles avatar-level translation gizmo during physics preview.
    /// Provides a central translation handle for moving the entire avatar.
    /// </summary>
    public static class AvatarGizmoHandler
    {
        private static bool _gizmoEnabled = true;
        private static Vector3 _avatarCenter = Vector3.zero;
        private static bool _avatarCenterCached = false;
        private static bool _avatarCenterLogged = false;
        
        /// <summary>
        /// Enable or disable the avatar translation gizmo
        /// </summary>
        public static bool GizmoEnabled
        {
            get => _gizmoEnabled;
            set => _gizmoEnabled = value;
        }
        
        /// <summary>
        /// Draw the avatar translation and rotation gizmo if physics preview is active
        /// </summary>
        public static void DrawAvatarGizmo()
        {
            if (!_gizmoEnabled || !PlayModeHook.IsInPhysicsPreview)
            {
                return;
            }
            
            var physicsClone = AvatarHiding.PhysicsClone;
            if (physicsClone == null)
            {
                return;
            }
            
            try
            {
                
                // Calculate avatar center if not cached
                if (!_avatarCenterCached)
                {
                    _avatarCenter = CalculateAvatarCenter(physicsClone);
                    _avatarCenterCached = true;
                    
                    // Only log once per session, and only in debug mode
                    if (!_avatarCenterLogged)
                    {
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log($"[EnhancedDynamics] Avatar center calculated: {_avatarCenter}");
                        }
                        _avatarCenterLogged = true;
                    }
                }
                
                // Get current avatar rotation
                var avatarRotation = physicsClone.transform.rotation;
                
                // Draw combined translation and rotation handles
                EditorGUI.BeginChangeCheck();
                
                // Use a good handle size for visibility
                var handleSize = HandleUtility.GetHandleSize(_avatarCenter) * 0.4f;
                
                // Draw translation handle
                var newCenter = Handles.PositionHandle(_avatarCenter, avatarRotation);
                
                // Draw rotation handle at the same position
                var newRotation = Handles.RotationHandle(avatarRotation, _avatarCenter);
                
                if (EditorGUI.EndChangeCheck())
                {
                    // Apply translation if changed
                    if (newCenter != _avatarCenter)
                    {
                        var offset = newCenter - _avatarCenter;
                        ApplyAvatarTranslation(physicsClone, offset);
                        _avatarCenter = newCenter;
                    }
                    
                    // Apply rotation if changed
                    if (newRotation != avatarRotation)
                    {
                        ApplyAvatarRotation(physicsClone, newRotation);
                    }
                    
                    // Mark scene as dirty
                    EditorUtility.SetDirty(physicsClone);
                }
                
                // Draw a visual indicator around the avatar center
                DrawAvatarCenterIndicator(_avatarCenter, handleSize);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error drawing avatar gizmo: {e}");
            }
        }
        
        /// <summary>
        /// Reset the cached avatar center when physics preview starts/stops
        /// </summary>
        public static void ResetCache()
        {
            _avatarCenterCached = false;
            _avatarCenterLogged = false;
            _avatarCenter = Vector3.zero;
        }
        
        private static Vector3 CalculateAvatarCenter(GameObject avatar)
        {
            try
            {
                // First priority: Use Unity's Humanoid Avatar system for hip bone
                var animator = avatar.GetComponent<Animator>();
                if (animator != null && animator.isHuman)
                {
                    var hipTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
                    if (hipTransform != null)
                    {
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log($"[EnhancedDynamics] Using Unity Humanoid hip bone: {hipTransform.name} at {hipTransform.position}");
                        }
                        return hipTransform.position;
                    }
                    
                    // Try spine if hips not available
                    var spineTransform = animator.GetBoneTransform(HumanBodyBones.Spine);
                    if (spineTransform != null)
                    {
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log($"[EnhancedDynamics] Using Unity Humanoid spine bone: {spineTransform.name} at {spineTransform.position}");
                        }
                        return spineTransform.position;
                    }
                    
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log("[EnhancedDynamics] Humanoid avatar found but no hip/spine bones available");
                    }
                }
                else
                {
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log("[EnhancedDynamics] No Animator component or not a humanoid avatar, falling back to manual search");
                    }
                }
                
                // Second priority: Manual bone search for non-humanoid avatars
                var hipBone = FindBoneByName(avatar, new[] { "Hips", "Hip", "Pelvis", "mixamorig:Hips" });
                if (hipBone != null)
                {
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log($"[EnhancedDynamics] Using manual search hip bone: {hipBone.name} at {hipBone.transform.position}");
                    }
                    return hipBone.transform.position;
                }
                
                // Third priority: Look for spine bone
                var spineBone = FindBoneByName(avatar, new[] { "Spine", "Spine1", "mixamorig:Spine", "mixamorig:Spine1" });
                if (spineBone != null)
                {
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log($"[EnhancedDynamics] Using manual search spine bone: {spineBone.name} at {spineBone.transform.position}");
                    }
                    return spineBone.transform.position;
                }
                
                // Fourth priority: Use VRCAvatarDescriptor ViewPosition (head area) and adjust down to center
                var avatarDescriptor = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
                if (avatarDescriptor != null && avatarDescriptor.ViewPosition != Vector3.zero)
                {
                    var viewPosition = avatar.transform.TransformPoint(avatarDescriptor.ViewPosition);
                    // Estimate center as about 60% down from head to feet
                    var estimatedCenter = new Vector3(viewPosition.x, viewPosition.y - (viewPosition.y - avatar.transform.position.y) * 0.6f, viewPosition.z);
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log($"[EnhancedDynamics] Using estimated center from ViewPosition: {estimatedCenter}");
                    }
                    return estimatedCenter;
                }
                
                // Fifth priority: Calculate bounds center from all renderers
                var renderers = avatar.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                    {
                        bounds.Encapsulate(renderers[i].bounds);
                    }
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log($"[EnhancedDynamics] Using renderer bounds center: {bounds.center}");
                    }
                    return bounds.center;
                }
                
                // Final fallback: Use avatar root position
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log($"[EnhancedDynamics] Using avatar root position: {avatar.transform.position}");
                }
                return avatar.transform.position;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EnhancedDynamics] Error calculating avatar center: {e}");
                return avatar.transform.position;
            }
        }
        
        private static GameObject FindBoneByName(GameObject avatar, string[] boneNames)
        {
            try
            {
                // Search all transforms in the avatar hierarchy
                var allTransforms = avatar.GetComponentsInChildren<Transform>();
                
                foreach (var boneName in boneNames)
                {
                    foreach (var transform in allTransforms)
                    {
                        if (transform.name.Equals(boneName, StringComparison.OrdinalIgnoreCase) ||
                            transform.name.Contains(boneName))
                        {
                            return transform.gameObject;
                        }
                    }
                }
                
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EnhancedDynamics] Error finding bone: {e}");
                return null;
            }
        }
        
        private static void ApplyAvatarTranslation(GameObject avatar, Vector3 offset)
        {
            try
            {
                // Skip undo recording during physics preview to avoid conflicts with physics system
                // Changes are temporary and will be discarded when exiting preview mode
                
                // Apply translation to avatar root
                avatar.transform.position += offset;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error applying avatar translation: {e}");
            }
        }
        
        private static void ApplyAvatarRotation(GameObject avatar, Quaternion newRotation)
        {
            try
            {
                // Skip undo recording during physics preview to avoid conflicts with physics system
                // Changes are temporary and will be discarded when exiting preview mode
                
                // Apply rotation to avatar root
                avatar.transform.rotation = newRotation;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error applying avatar rotation: {e}");
            }
        }
        
        private static void DrawAvatarCenterIndicator(Vector3 center, float handleSize)
        {
            try
            {
                // Draw a subtle circle around the avatar center
                var originalColor = Handles.color;
                Handles.color = new Color(0.3f, 0.8f, 1f, 0.6f); // Light blue with transparency
                
                // Draw circular indicator
                Handles.DrawWireDisc(center, Vector3.up, handleSize * 2f);
                Handles.DrawWireDisc(center, Vector3.right, handleSize * 2f);
                Handles.DrawWireDisc(center, Vector3.forward, handleSize * 2f);
                
                // Draw a small solid sphere at the center
                Handles.color = new Color(0.3f, 0.8f, 1f, 0.8f);
                Handles.SphereHandleCap(0, center, Quaternion.identity, handleSize * 0.2f, EventType.Repaint);
                
                Handles.color = originalColor;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EnhancedDynamics] Error drawing avatar center indicator: {e}");
            }
        }
    }
}