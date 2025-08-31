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
        private static bool _manualAnchorSet = false;
        private static Vector3 _manualAnchor = Vector3.zero;
        
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
            if (!_gizmoEnabled || !PlayModeHook.IsInAnyPreview)
            {
                return;
            }
            
            var physicsClone = AvatarHiding.PhysicsClone;
            if (physicsClone == null)
            {
                // In fast preview there is no clone; target the selected avatar if available
                var root = PhysicsContextTracker.OriginalAvatar ?? (Selection.activeGameObject != null ? FindAvatarRoot(Selection.activeGameObject) : null);
                if (root == null) return;
                physicsClone = root;
            }
            
            try
            {
                // Always show avatar-level gizmo so it can be grabbed at any time
                
                // Calculate avatar gizmo anchor
                if (_manualAnchorSet)
                {
                    _avatarCenter = _manualAnchor;
                }
                else
                {
                    if (!_avatarCenterCached)
                    {
                        _avatarCenter = CalculateAvatarCenter(physicsClone);
                        _avatarCenterCached = true;
                        if (!_avatarCenterLogged && EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log($"[EnhancedDynamics] Avatar center calculated: {_avatarCenter}");
                        }
                        _avatarCenterLogged = true;
                    }
                }
                
                // Draw both translation and rotation handles so users can always grab either
                EditorGUI.BeginChangeCheck();
                
                // Use a good handle size for visibility
                var handleSize = HandleUtility.GetHandleSize(_avatarCenter) * 0.4f;
                
                var avatarRotation = physicsClone.transform.rotation;
                Vector3 newCenter = Handles.PositionHandle(_avatarCenter, avatarRotation);
                var newRotation = Handles.RotationHandle(avatarRotation, _avatarCenter);
                
                if (EditorGUI.EndChangeCheck())
                {
                    // Apply translation if changed
                    if (newCenter != _avatarCenter)
                    {
                        var offset = newCenter - _avatarCenter;
                        ApplyAvatarTranslation(physicsClone, offset);
                        _avatarCenter = newCenter;
                        _manualAnchorSet = true;
                        _manualAnchor = _avatarCenter;
                    }
                    
                    // Apply rotation if changed
                    if (newRotation != avatarRotation)
                    {
                        var delta = newRotation * Quaternion.Inverse(avatarRotation);
                        var t = physicsClone.transform;
                        var newPos = _avatarCenter + delta * (t.position - _avatarCenter);
                        var newRot = delta * avatarRotation;
                        t.SetPositionAndRotation(newPos, newRot);
                        _manualAnchorSet = true;
                        _manualAnchor = _avatarCenter;
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
            if (!_manualAnchorSet)
            {
                _avatarCenter = Vector3.zero;
            }
        }

        // Set the gizmo anchor to the avatar center offset to the avatar's right by the given meters
        public static void SetAnchorToRightOfCenter(float offsetMeters = 0.1f)
        {
            try
            {
                var root = AvatarHiding.PhysicsClone ?? (Selection.activeGameObject != null ? FindAvatarRoot(Selection.activeGameObject) : null);
                if (root == null) return;
                var center = CalculateAvatarCenter(root);
                var pos = center + (root.transform.right * offsetMeters);
                SetManualAnchor(pos);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EnhancedDynamics] Failed to set default anchor offset: {e}");
            }
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

        private static Vector3 CalculateAutoPlacedAnchor(GameObject avatar)
        {
            // Place a handle near the avatar center but offset toward the SceneView camera
            try
            {
                var center = CalculateAvatarCenter(avatar);
                var renderers = avatar.GetComponentsInChildren<Renderer>();
                var extents = Vector3.one * 0.5f;
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    extents = bounds.extents;
                    center = bounds.center;
                }

                var sv = SceneView.lastActiveSceneView;
                if (sv != null && sv.camera != null)
                {
                    var cam = sv.camera;
                    var forward = cam.transform.forward;
                    var up = cam.transform.up;
                    // Offset in front of the avatar and slightly upward for visibility
                    float r = extents.magnitude;
                    var pos = center + forward * (r * 0.6f) + up * (r * 0.2f);
                    return pos;
                }

                return center;
            }
            catch
            {
                return avatar.transform.position;
            }
        }

        private static GameObject FindAvatarRoot(GameObject obj)
        {
            var t = obj.transform;
            while (t != null)
            {
                var desc = t.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
                if (desc != null) return t.gameObject;
                t = t.parent;
            }
            return null;
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

        private static void ApplyAvatarRotationAroundPivot(GameObject avatar, Quaternion newRotation, Vector3 pivot)
        {
            try
            {
                var oldRotation = avatar.transform.rotation;
                var delta = newRotation * Quaternion.Inverse(oldRotation);
                var pos = avatar.transform.position;
                var newPos = pivot + delta * (pos - pivot);
                avatar.transform.SetPositionAndRotation(newPos, newRotation);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error rotating avatar around pivot: {e}");
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

        public static void ClearManualAnchor()
        {
            _manualAnchorSet = false;
            _avatarCenterCached = false;
        }

        public static void RecenterToCamera(SceneView sv)
        {
            try
            {
                var root = AvatarHiding.PhysicsClone ?? (Selection.activeGameObject != null ? FindAvatarRoot(Selection.activeGameObject) : null);
                if (root == null) return;
                var anchor = CalculateAutoPlacedAnchor(root);
                SetManualAnchor(anchor);
                
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EnhancedDynamics] Failed to recenter to camera: {e}");
            }
        }

        public static void DropAnchorUnderMouse(SceneView sv, Vector2 guiMousePosition)
        {
            try
            {
                var root = AvatarHiding.PhysicsClone ?? (Selection.activeGameObject != null ? FindAvatarRoot(Selection.activeGameObject) : null);
                if (root == null || sv == null) return;
                var ray = HandleUtility.GUIPointToWorldRay(guiMousePosition);

                // Always place pivot on a plane through avatar center (free space), not on colliders
                var center = CalculateAvatarCenter(root);
                var renderers = root.GetComponentsInChildren<Renderer>();
                var bounds = new Bounds(center, Vector3.one);
                if (renderers.Length > 0)
                {
                    bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    center = bounds.center;
                }
                var plane = new Plane(sv.camera.transform.forward, center);
                Vector3 pos = center;
                if (plane.Raycast(ray, out float d))
                {
                    pos = ray.origin + ray.direction * d;
                }
                // No forward offset; keep pivot exactly on plane
                SetManualAnchor(pos);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EnhancedDynamics] Failed dropping anchor under mouse: {e}");
            }
        }

        public static void SetManualAnchor(Vector3 pos)
        {
            _manualAnchorSet = true;
            _manualAnchor = pos;
            _avatarCenter = pos;
        }

        // Shortcut integration: request a drop under mouse to be executed on next Scene GUI event
        private static bool _requestDropUnderMouse;
        public static void RequestDropUnderMouse()
        {
            _requestDropUnderMouse = true;
        }
        public static bool ConsumeDropUnderMouseRequest()
        {
            if (_requestDropUnderMouse)
            {
                _requestDropUnderMouse = false;
                return true;
            }
            return false;
        }
    }
}
