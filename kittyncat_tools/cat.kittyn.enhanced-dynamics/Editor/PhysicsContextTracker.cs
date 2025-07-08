using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace EnhancedDynamics.Editor
{
    /// <summary>
    /// Tracks the context of physics component editing to maintain state across preview sessions.
    /// Remembers which component triggered the preview and preserves editing context.
    /// </summary>
    public static class PhysicsContextTracker
    {
        private static Component _triggeringComponent = null;
        private static GameObject _originalAvatar = null;
        private static string _componentPath = null;
        private static Dictionary<string, object> _componentProperties = new Dictionary<string, object>();
        private static bool _hasActiveContext = false;
        
        /// <summary>
        /// Track the component that triggered the physics preview
        /// </summary>
        public static void SetPreviewContext(Component triggeringComponent)
        {
            if (triggeringComponent == null)
            {
                Debug.LogWarning("[EnhancedDynamics] Cannot set preview context with null component");
                return;
            }
            
            try
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log($"[EnhancedDynamics] Setting preview context for component: {triggeringComponent.GetType().Name} on {triggeringComponent.gameObject.name}");
                }
                
                _triggeringComponent = triggeringComponent;
                _originalAvatar = GetAvatarRoot(triggeringComponent.gameObject);
                _componentPath = GetComponentPath(triggeringComponent);
                
                // Capture current properties for restoration
                CaptureComponentProperties(triggeringComponent);
                
                _hasActiveContext = true;
                
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log($"[EnhancedDynamics] Preview context set successfully:");
                    Debug.Log($"  - Component: {triggeringComponent.GetType().Name}");
                    Debug.Log($"  - GameObject: {triggeringComponent.gameObject.name}");
                    Debug.Log($"  - Avatar: {_originalAvatar?.name}");
                    Debug.Log($"  - Component Path: {_componentPath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error setting preview context: {e}");
                ClearContext();
            }
        }
        
        /// <summary>
        /// Get the component in the clone that corresponds to the triggering component
        /// </summary>
        public static Component GetCorrespondingCloneComponent(GameObject physicsClone)
        {
            if (!_hasActiveContext || physicsClone == null || string.IsNullOrEmpty(_componentPath))
            {
                Debug.LogWarning($"[EnhancedDynamics] Cannot find corresponding clone component:");
                Debug.LogWarning($"  - Has context: {_hasActiveContext}");
                Debug.LogWarning($"  - Physics clone: {physicsClone?.name ?? "null"}");
                Debug.LogWarning($"  - Component path: {_componentPath ?? "null"}");
                return null;
            }
            
            try
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log($"[EnhancedDynamics] Searching for corresponding clone component:");
                }
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log($"  - Component path: {_componentPath}");
                }
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log($"  - Component type: {_triggeringComponent?.GetType().Name}");
                }
                
                var cloneComponent = FindComponentByPath(physicsClone, _componentPath, _triggeringComponent.GetType());
                
                if (cloneComponent != null)
                {
                    Debug.Log($"[EnhancedDynamics] ✓ Found corresponding clone component: {cloneComponent.GetType().Name} on {cloneComponent.gameObject.name}");
                }
                else
                {
                    Debug.LogWarning($"[EnhancedDynamics] ❌ Could not find corresponding component in clone at path: {_componentPath}");
                    // Log available hierarchy for debugging
                    Debug.LogWarning("[EnhancedDynamics] Available paths in clone hierarchy:");
                    LogCloneHierarchy(physicsClone, "");
                }
                
                return cloneComponent;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error finding corresponding clone component: {e}");
                return null;
            }
        }
        
        /// <summary>
        /// Apply any specific context setup to the clone component
        /// </summary>
        public static void ApplyContextToClone(Component cloneComponent)
        {
            if (cloneComponent == null || !_hasActiveContext)
            {
                return;
            }
            
            try
            {
                Debug.Log($"[EnhancedDynamics] Applying context to clone component: {cloneComponent.GetType().Name}");
                
                // For physics components, we might want to ensure they're in optimal preview state
                if (cloneComponent is VRCPhysBone physBone)
                {
                    SetupPhysBoneForPreview(physBone);
                }
                else if (cloneComponent is VRCPhysBoneCollider collider)
                {
                    SetupColliderForPreview(collider);
                }
                
                // Mark the component as dirty to ensure inspector updates
                EditorUtility.SetDirty(cloneComponent);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error applying context to clone: {e}");
            }
        }
        
        private static void SetupPhysBoneForPreview(VRCPhysBone physBone)
        {
            // Ensure PhysBone is enabled for preview
            if (!physBone.enabled)
            {
                Debug.Log("[EnhancedDynamics] Enabling PhysBone for preview");
                physBone.enabled = true;
            }
            
            // Could add other preview-specific optimizations here
            // For example, temporarily increasing simulation rate for better responsiveness
        }
        
        private static void SetupColliderForPreview(VRCPhysBoneCollider collider)
        {
            // Ensure collider is enabled for preview
            if (!collider.enabled)
            {
                Debug.Log("[EnhancedDynamics] Enabling PhysBoneCollider for preview");
                collider.enabled = true;
            }
        }
        
        private static GameObject GetAvatarRoot(GameObject obj)
        {
            // Find the VRCAvatarDescriptor to determine the avatar root
            var current = obj.transform;
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
            current = obj.transform;
            while (current.parent != null)
            {
                current = current.parent;
            }
            
            return current.gameObject;
        }
        
        private static string GetComponentPath(Component component)
        {
            if (component == null) return null;
            
            // Create a unique path for this component
            var transform = component.transform;
            var pathParts = new List<string>();
            
            // Build path from component to avatar root
            var avatarRoot = GetAvatarRoot(component.gameObject);
            while (transform != null && transform.gameObject != avatarRoot)
            {
                pathParts.Insert(0, transform.name);
                transform = transform.parent;
            }
            
            // Add component type and index
            var componentType = component.GetType().Name;
            var components = component.gameObject.GetComponents(component.GetType());
            var componentIndex = Array.IndexOf(components, component);
            
            var path = pathParts.Count > 0 ? string.Join("/", pathParts) : "";
            return $"{path}#{componentType}[{componentIndex}]";
        }
        
        private static Component FindComponentByPath(GameObject root, string componentPath, Type componentType)
        {
            if (string.IsNullOrEmpty(componentPath) || root == null) return null;
            
            try
            {
                var parts = componentPath.Split('#');
                if (parts.Length != 2) return null;
                
                var transformPath = parts[0];
                var componentInfo = parts[1];
                
                // Parse component info
                var componentParts = componentInfo.Split('[');
                if (componentParts.Length != 2) return null;
                
                var indexStr = componentParts[1].TrimEnd(']');
                if (!int.TryParse(indexStr, out int componentIndex)) return null;
                
                // Find the target transform
                Transform targetTransform = root.transform;
                
                if (!string.IsNullOrEmpty(transformPath))
                {
                    var pathSegments = transformPath.Split('/');
                    foreach (var segment in pathSegments)
                    {
                        targetTransform = targetTransform.Find(segment);
                        if (targetTransform == null)
                        {
                            Debug.LogWarning($"[EnhancedDynamics] Could not find transform segment: {segment}");
                            return null;
                        }
                    }
                }
                
                // Get the component at the specified index
                var components = targetTransform.GetComponents(componentType);
                if (componentIndex >= 0 && componentIndex < components.Length)
                {
                    return components[componentIndex];
                }
                
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error finding component by path: {e}");
                return null;
            }
        }
        
        private static void CaptureComponentProperties(Component component)
        {
            _componentProperties.Clear();
            
            try
            {
                // Use SerializedObject to capture current property values
                var serializedObject = new SerializedObject(component);
                var property = serializedObject.GetIterator();
                
                // Iterate through all serialized properties
                if (property.NextVisible(true))
                {
                    do
                    {
                        CapturePropertyValue(property);
                    }
                    while (property.NextVisible(false));
                }
                
                Debug.Log($"[EnhancedDynamics] Captured {_componentProperties.Count} properties for {component.GetType().Name}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error capturing component properties: {e}");
            }
        }
        
        private static void CapturePropertyValue(SerializedProperty property)
        {
            try
            {
                var propertyPath = property.propertyPath;
                
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Float:
                        _componentProperties[propertyPath] = property.floatValue;
                        break;
                    case SerializedPropertyType.Integer:
                        _componentProperties[propertyPath] = property.intValue;
                        break;
                    case SerializedPropertyType.Boolean:
                        _componentProperties[propertyPath] = property.boolValue;
                        break;
                    case SerializedPropertyType.String:
                        _componentProperties[propertyPath] = property.stringValue;
                        break;
                    case SerializedPropertyType.Vector3:
                        _componentProperties[propertyPath] = property.vector3Value;
                        break;
                    case SerializedPropertyType.Quaternion:
                        _componentProperties[propertyPath] = property.quaternionValue;
                        break;
                    case SerializedPropertyType.Enum:
                        _componentProperties[propertyPath] = property.enumValueIndex;
                        break;
                    // Add more types as needed
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error capturing property {property.propertyPath}: {e}");
            }
        }
        
        /// <summary>
        /// Clear the current preview context
        /// </summary>
        public static void ClearContext()
        {
            _triggeringComponent = null;
            _originalAvatar = null;
            _componentPath = null;
            _componentProperties.Clear();
            _hasActiveContext = false;
            
            Debug.Log("[EnhancedDynamics] Preview context cleared");
        }
        
        /// <summary>
        /// Check if we have an active preview context
        /// </summary>
        public static bool HasActiveContext => _hasActiveContext;
        
        /// <summary>
        /// Get the original triggering component
        /// </summary>
        public static Component TriggeringComponent => _triggeringComponent;
        
        /// <summary>
        /// Get the original avatar root
        /// </summary>
        public static GameObject OriginalAvatar => _originalAvatar;
        
        /// <summary>
        /// Get the component path for debugging
        /// </summary>
        public static string ComponentPath => _componentPath;
        
        /// <summary>
        /// Log the clone hierarchy for debugging purposes
        /// </summary>
        private static void LogCloneHierarchy(GameObject root, string indent)
        {
            if (root == null) return;
            
            try
            {
                var components = root.GetComponents<Component>();
                var componentList = string.Join(", ", System.Array.ConvertAll(components, c => c.GetType().Name));
                Debug.LogWarning($"{indent}{root.name} [{componentList}]");
                
                // Recursively log children (limit depth to avoid spam)
                if (indent.Length < 20) // Max 10 levels deep
                {
                    for (int i = 0; i < root.transform.childCount; i++)
                    {
                        LogCloneHierarchy(root.transform.GetChild(i).gameObject, indent + "  ");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error logging hierarchy: {e}");
            }
        }
    }
}