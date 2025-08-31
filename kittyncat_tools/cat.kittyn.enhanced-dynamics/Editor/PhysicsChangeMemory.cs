using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.SDK3.Dynamics.Contact.Components;

namespace EnhancedDynamics.Editor
{
    /// <summary>
    /// Stores physics changes in memory during preview mode to be applied after exiting play mode.
    /// This avoids issues with modifying hidden GameObjects during play mode.
    /// </summary>
    [InitializeOnLoad]
    public static class PhysicsChangeMemory
    {
        [Serializable]
        public class ComponentChange
        {
            public string componentPath; // Relative path from avatar root
            public string componentTypeName; // Full type name
            public int componentIndex; // Index if multiple components of same type
            public List<PropertyChange> propertyChanges = new List<PropertyChange>();
        }
        
        [Serializable]
        public class PropertyChange
        {
            public string propertyPath;
            public string serializedValue; // JSON serialized value
            public SerializedPropertyType propertyType;
        }
        
        // Static storage that survives play mode transitions
        private static string _pendingChangesJson = "";
        private static string _originalAvatarPath = "";
        private static bool _hasPendingChanges = false;
        
        // Store original component values captured before hiding avatar
        private static Dictionary<string, ComponentSnapshot> _originalComponentSnapshots = new Dictionary<string, ComponentSnapshot>();
        
        [Serializable]
        private class ComponentSnapshot
        {
            public string componentPath;
            public string componentTypeName;
            public Dictionary<string, string> propertyValues = new Dictionary<string, string>();
        }
        
        static PhysicsChangeMemory()
        {
            // Subscribe to play mode changes to apply pending changes
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }
        
        /// <summary>
        /// Capture original component values before hiding avatar
        /// </summary>
        public static void CaptureOriginalValues(GameObject avatar)
        {
            if (avatar == null)
            {
                Debug.LogWarning("[EnhancedDynamics] Cannot capture original values - avatar is null");
                return;
            }
            
            try
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log("[EnhancedDynamics] Capturing original component values before hiding avatar...");
                    }
                }
                _originalComponentSnapshots.Clear();
                
                // Capture PhysBone values
                CaptureOriginalComponentValues<VRCPhysBone>(avatar);
                
                // Capture PhysBoneCollider values
                CaptureOriginalComponentValues<VRCPhysBoneCollider>(avatar);
                
                // Capture ContactSender values
                CaptureOriginalComponentValues<VRCContactSender>(avatar);
                
                // Capture ContactReceiver values
                CaptureOriginalComponentValues<VRCContactReceiver>(avatar);
                
                // Store the avatar path for later reference
                _originalAvatarPath = GetScenePath(avatar);
                
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log($"[EnhancedDynamics] ✓ Captured original values for {_originalComponentSnapshots.Count} components");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error capturing original values: {e}");
            }
        }
        
        private static void CaptureOriginalComponentValues<T>(GameObject avatar) where T : Component
        {
            var components = avatar.GetComponentsInChildren<T>(true);
            if (EnhancedDynamicsSettings.DebugMode)
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log($"[EnhancedDynamics] Found {components.Length} {typeof(T).Name} components to capture");
                }
            }
            
            foreach (var component in components)
            {
                if (component == null) continue;
                
                var snapshot = new ComponentSnapshot
                {
                    componentPath = GetRelativePath(component.transform, avatar.transform),
                    componentTypeName = component.GetType().AssemblyQualifiedName // Use AssemblyQualifiedName for cross-assembly type resolution
                };
                
                // Capture all properties
                var so = new SerializedObject(component);
                so.Update();
                
                var prop = so.GetIterator();
                if (prop.NextVisible(true))
                {
                    do
                    {
                        if (!ShouldSkipProperty(prop.propertyPath))
                        {
                            var serializedValue = SerializePropertyValue(prop);
                            if (!string.IsNullOrEmpty(serializedValue))
                            {
                                snapshot.propertyValues[prop.propertyPath] = serializedValue;
                                
                                // Log array properties specially (only for debugging)
                                // if (prop.isArray || prop.propertyPath.Contains(".Array."))
                                // {
                                //     Debug.Log($"[EnhancedDynamics] Captured array property: {prop.propertyPath} = {serializedValue}");
                                // }
                            }
                        }
                    }
                    while (prop.NextVisible(false));
                }
                
                so.Dispose();
                
                // Create unique key for this component
                var key = $"{snapshot.componentPath}#{snapshot.componentTypeName}";
                _originalComponentSnapshots[key] = snapshot;
                
                // Debug.Log($"[EnhancedDynamics] Captured {snapshot.propertyValues.Count} properties for {typeof(T).Name} at {snapshot.componentPath}");
                // Debug.Log($"[EnhancedDynamics] Snapshot key: {key}");
                
                // Log first few properties for debugging (commented out to reduce spam)
                // int count = 0;
                // foreach (var kvp in snapshot.propertyValues)
                // {
                //     if (count++ < 5) // Only log first 5 properties
                //     {
                //         Debug.Log($"  Property: {kvp.Key} = {kvp.Value}");
                //     }
                // }
            }
        }
        
        /// <summary>
        /// Capture changes from clone components and store in memory
        /// </summary>
        public static bool CaptureChangesToMemory(GameObject originalAvatar, GameObject physicsClone)
        {
            if (physicsClone == null)
            {
                Debug.LogWarning("[EnhancedDynamics] Cannot capture changes - physics clone is null");
                return false;
            }
            
            // Lazily capture originals if not captured yet (improves preview start time)
            if (_originalComponentSnapshots.Count == 0)
            {
                if (originalAvatar == null)
                {
                    Debug.LogWarning("[EnhancedDynamics] No original avatar provided for change comparison");
                    return false;
                }
                CaptureOriginalValues(originalAvatar);
                if (_originalComponentSnapshots.Count == 0)
                {
                    Debug.LogWarning("[EnhancedDynamics] Failed to capture original snapshots; aborting save");
                    return false;
                }
            }
            
            try
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log("[EnhancedDynamics] Capturing physics changes to memory...");
                    }
                }
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log($"[EnhancedDynamics] Comparing against {_originalComponentSnapshots.Count} original component snapshots");
                    }
                }
                
                var changes = new List<ComponentChange>();
                
                // Compare PhysBone changes against snapshots
                CompareComponentsWithSnapshots<VRCPhysBone>(physicsClone, changes);
                
                // Compare PhysBoneCollider changes against snapshots
                CompareComponentsWithSnapshots<VRCPhysBoneCollider>(physicsClone, changes);
                
                // Compare ContactSender changes against snapshots
                CompareComponentsWithSnapshots<VRCContactSender>(physicsClone, changes);
                
                // Compare ContactReceiver changes against snapshots
                CompareComponentsWithSnapshots<VRCContactReceiver>(physicsClone, changes);
                
                if (changes.Count > 0)
                {
                    // Serialize changes to JSON for persistence
                    _pendingChangesJson = JsonUtility.ToJson(new SerializableChangeList { changes = changes });
                    _hasPendingChanges = true;
                    
                    int totalProperties = 0;
                    foreach (var change in changes)
                    {
                        totalProperties += change.propertyChanges.Count;
                    }
                    
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log($"[EnhancedDynamics] ✓ Captured {totalProperties} property changes from {changes.Count} components to memory");
                        }
                    }
                    return true;
                }
                else
                {
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log("[EnhancedDynamics] No changes detected to capture");
                    }
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error capturing changes to memory: {e}");
                return false;
            }
        }
        
        /// <summary>
        /// Apply pending changes from memory to the original avatar
        /// </summary>
        public static bool ApplyPendingChanges()
        {
            if (!_hasPendingChanges || string.IsNullOrEmpty(_pendingChangesJson))
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] No pending changes to apply");
                }
                return false;
            }
            
            try
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Applying pending physics changes from memory...");
                }
                
                // Find the original avatar
                var originalAvatar = FindGameObjectByPath(_originalAvatarPath);
                if (originalAvatar == null)
                {
                    Debug.LogError($"[EnhancedDynamics] Could not find original avatar at path: {_originalAvatarPath}");
                    return false;
                }
                
                // Deserialize changes
                var changeList = JsonUtility.FromJson<SerializableChangeList>(_pendingChangesJson);
                if (changeList == null || changeList.changes == null)
                {
                    Debug.LogError("[EnhancedDynamics] Failed to deserialize pending changes");
                    return false;
                }
                
                int appliedCount = 0;
                
                foreach (var componentChange in changeList.changes)
                {
                    if (ApplyComponentChanges(originalAvatar, componentChange))
                    {
                        appliedCount++;
                    }
                }
                
                // Clear pending changes
                _pendingChangesJson = "";
                _originalAvatarPath = "";
                _hasPendingChanges = false;
                
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log($"[EnhancedDynamics] ✓ Applied changes to {appliedCount} components from memory");
                }
                
                // Log details of what was applied
                foreach (var componentChange in changeList.changes)
                {
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log($"[EnhancedDynamics] Applied {componentChange.propertyChanges.Count} changes to {componentChange.componentTypeName} at {componentChange.componentPath}");
                    }
                }
                
                // Show notification
                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.ShowNotification(new GUIContent("Physics changes applied!"), 2.0);
                }
                
                return appliedCount > 0;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error applying pending changes: {e}");
                return false;
            }
        }
        
        private static void CompareComponentsWithSnapshots<T>(GameObject physicsClone, List<ComponentChange> changes) where T : Component
        {
            try
            {
                var cloneComponents = physicsClone.GetComponentsInChildren<T>(true);
                
                // Debug.Log($"[EnhancedDynamics] Checking {cloneComponents.Length} {typeof(T).Name} clone components for changes");
                // Debug.Log($"[EnhancedDynamics] Available snapshot keys: {string.Join(", ", _originalComponentSnapshots.Keys)}");
                
                foreach (var cloneComponent in cloneComponents)
                {
                    if (cloneComponent == null) continue;
                    
                    var clonePath = GetRelativePath(cloneComponent.transform, physicsClone.transform);
                    var componentType = cloneComponent.GetType().AssemblyQualifiedName;
                    var snapshotKey = $"{clonePath}#{componentType}";
                    
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log($"[EnhancedDynamics] Looking for snapshot with key: {snapshotKey}");
                    }
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log($"[EnhancedDynamics] Clone component: {cloneComponent.name} (Type: {componentType})");
                    }
                    
                    if (_originalComponentSnapshots.TryGetValue(snapshotKey, out var snapshot))
                    {
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log($"[EnhancedDynamics] Found matching snapshot for {componentType} at {clonePath}");
                        }
                        
                        var componentChange = CompareWithSnapshot(cloneComponent, snapshot, clonePath);
                        if (componentChange != null && componentChange.propertyChanges.Count > 0)
                        {
                            changes.Add(componentChange);
                            if (EnhancedDynamicsSettings.DebugMode)
                            {
                                Debug.Log($"[EnhancedDynamics] Found {componentChange.propertyChanges.Count} changes in {typeof(T).Name} at {clonePath}");
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[EnhancedDynamics] No snapshot found for {componentType} at {clonePath}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error comparing {typeof(T).Name} with snapshots: {e}");
            }
        }
        
        private static ComponentChange CompareWithSnapshot(Component cloneComponent, ComponentSnapshot snapshot, string relativePath)
        {
            var componentChange = new ComponentChange
            {
                componentPath = relativePath,
                componentTypeName = cloneComponent.GetType().AssemblyQualifiedName, // Use AssemblyQualifiedName for cross-assembly type resolution
                componentIndex = 0 // We'll use path-based matching instead of index
            };
            
            var cloneSO = new SerializedObject(cloneComponent);
            cloneSO.Update();
            
            var cloneProp = cloneSO.GetIterator();
            
            if (cloneProp.NextVisible(true))
            {
                do
                {
                    if (!ShouldSkipProperty(cloneProp.propertyPath))
                    {
                        var currentValue = SerializePropertyValue(cloneProp);
                        
                        // Log property checks for debugging
                        if (cloneProp.isArray || cloneProp.propertyPath.Contains(".Array."))
                        {
                            Debug.Log($"[EnhancedDynamics] Checking array property: {cloneProp.propertyPath} (Size: {(cloneProp.isArray ? cloneProp.arraySize.ToString() : "N/A")})");
                        }
                        
                        // Check if this property exists in the snapshot
                        if (snapshot.propertyValues.TryGetValue(cloneProp.propertyPath, out var originalValue))
                        {
                            // Compare current value with original
                            bool hasChanged = false;
                            
                            // Special comparison for floats with tolerance
                            if (cloneProp.propertyType == SerializedPropertyType.Float)
                            {
                                if (float.TryParse(currentValue, out float current) && 
                                    float.TryParse(originalValue, out float original))
                                {
                                    hasChanged = Mathf.Abs(current - original) > 0.0001f;
                                    // if (hasChanged)
                                    // {
                                    //     Debug.Log($"[EnhancedDynamics] Float property changed: {cloneProp.propertyPath}");
                                    //     Debug.Log($"  Original: {original:F6}");
                                    //     Debug.Log($"  Current: {current:F6}");
                                    //     Debug.Log($"  Difference: {Mathf.Abs(current - original):F6}");
                                    // }
                                }
                                else
                                {
                                    hasChanged = currentValue != originalValue;
                                }
                            }
                            // Special handling for array size properties
                            else if (cloneProp.propertyPath.EndsWith(".Array.size"))
                            {
                                hasChanged = currentValue != originalValue;
                                // if (hasChanged)
                                // {
                                //     Debug.Log($"[EnhancedDynamics] Array size changed: {cloneProp.propertyPath}");
                                //     Debug.Log($"  Original size: {originalValue}");
                                //     Debug.Log($"  Current size: {currentValue}");
                                // }
                            }
                            else
                            {
                                hasChanged = currentValue != originalValue;
                                // if (hasChanged && cloneProp.propertyPath.Contains(".Array."))
                                // {
                                //     Debug.Log($"[EnhancedDynamics] Array element changed: {cloneProp.propertyPath}");
                                // }
                            }
                            
                            if (hasChanged)
                            {
                                var propertyChange = new PropertyChange
                                {
                                    propertyPath = cloneProp.propertyPath,
                                    propertyType = cloneProp.propertyType,
                                    serializedValue = currentValue
                                };
                                
                                componentChange.propertyChanges.Add(propertyChange);
                                // Debug.Log($"[EnhancedDynamics] Property changed: {cloneProp.propertyPath} (Type: {cloneProp.propertyType})");
                                // Debug.Log($"  Original: {originalValue}");
                                // Debug.Log($"  Current: {currentValue}");
                            }
                        }
                        else if (!string.IsNullOrEmpty(currentValue))
                        {
                            // This is a new property that wasn't in the original
                            Debug.LogWarning($"[EnhancedDynamics] Property {cloneProp.propertyPath} not found in original snapshot");
                            Debug.LogWarning($"  Available keys in snapshot: {string.Join(", ", snapshot.propertyValues.Keys)}");
                        }
                    }
                }
                while (cloneProp.NextVisible(false));
            }
            
            cloneSO.Dispose();
            
            return componentChange;
        }
        
        private static bool ApplyComponentChanges(GameObject avatar, ComponentChange componentChange)
        {
            try
            {
                // Find the target GameObject
                var targetTransform = avatar.transform;
                if (!string.IsNullOrEmpty(componentChange.componentPath))
                {
                    // Use recursive search for nested paths
                    targetTransform = FindTransformByPath(avatar.transform, componentChange.componentPath);
                    if (targetTransform == null)
                    {
                        Debug.LogWarning($"[EnhancedDynamics] Could not find GameObject at path: {componentChange.componentPath}");
                        return false;
                    }
                }
                
                // Find the component
                var componentType = Type.GetType(componentChange.componentTypeName);
                
                // Fallback: Try to find type by searching assemblies if direct resolution fails
                if (componentType == null)
                {
                    componentType = FindTypeInAssemblies(componentChange.componentTypeName);
                }
                
                if (componentType == null)
                {
                    Debug.LogError($"[EnhancedDynamics] Could not find component type: {componentChange.componentTypeName}");
                    Debug.LogError($"[EnhancedDynamics] Make sure VRChat SDK is properly imported");
                    return false;
                }
                
                // Since we're using path-based matching, just get the first component of this type
                // In the future, we could store additional metadata to identify specific components
                var targetComponent = targetTransform.GetComponent(componentType);
                if (targetComponent == null)
                {
                    Debug.LogWarning($"[EnhancedDynamics] Could not find component of type {componentType.Name} on {targetTransform.name}");
                    return false;
                }
                
                // Record undo
                Undo.RecordObject(targetComponent, "Apply Physics Preview Changes");
                
                // Apply changes
                var so = new SerializedObject(targetComponent);
                so.Update();
                
                foreach (var propertyChange in componentChange.propertyChanges)
                {
                    var prop = so.FindProperty(propertyChange.propertyPath);
                    if (prop != null)
                    {
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log($"[EnhancedDynamics] Applying change to property: {propertyChange.propertyPath}");
                            Debug.Log($"  New value: {propertyChange.serializedValue}");
                        }
                        DeserializePropertyValue(prop, propertyChange.serializedValue, propertyChange.propertyType);
                    }
                    else
                    {
                        Debug.LogWarning($"[EnhancedDynamics] Could not find property {propertyChange.propertyPath} on component");
                    }
                }
                
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(targetComponent);
                
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log($"[EnhancedDynamics] Applied {componentChange.propertyChanges.Count} changes to {componentType.Name} on {targetTransform.name}");
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error applying component changes: {e}");
                return false;
            }
        }
        
        private static bool ShouldSkipProperty(string propertyPath)
        {
            var skipProperties = new[]
            {
                "m_ObjectHideFlags",
                "m_CorrespondingSourceObject", 
                "m_PrefabInstance",
                "m_PrefabAsset",
                "m_GameObject",
                "m_Enabled",
                "m_EditorHideFlags",
                "m_Script"
            };
            
            // Skip if it's one of the blacklisted properties
            if (Array.Exists(skipProperties, p => propertyPath.Equals(p)))
                return true;
            
            // We now handle array properties - don't skip them
            return false;
        }
        
        private static string SerializePropertyValue(SerializedProperty property)
        {
            try
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Float:
                        return property.floatValue.ToString("G9"); // Use G9 for better float precision
                    case SerializedPropertyType.Integer:
                        return property.intValue.ToString();
                    case SerializedPropertyType.Boolean:
                        return property.boolValue.ToString();
                    case SerializedPropertyType.String:
                        return property.stringValue ?? "";
                    case SerializedPropertyType.Vector3:
                        return JsonUtility.ToJson(property.vector3Value);
                    case SerializedPropertyType.Quaternion:
                        return JsonUtility.ToJson(property.quaternionValue);
                    case SerializedPropertyType.Enum:
                        return property.enumValueIndex.ToString();
                    case SerializedPropertyType.Color:
                        return JsonUtility.ToJson(property.colorValue);
                    case SerializedPropertyType.Vector2:
                        return JsonUtility.ToJson(property.vector2Value);
                    case SerializedPropertyType.Vector4:
                        return JsonUtility.ToJson(property.vector4Value);
                    case SerializedPropertyType.Rect:
                        return JsonUtility.ToJson(property.rectValue);
                    case SerializedPropertyType.Bounds:
                        return JsonUtility.ToJson(property.boundsValue);
                    case SerializedPropertyType.AnimationCurve:
                        return JsonUtility.ToJson(property.animationCurveValue);
                    case SerializedPropertyType.ObjectReference:
                        // For object references, store the instance ID
                        var objRef = property.objectReferenceValue;
                        return objRef != null ? objRef.GetInstanceID().ToString() : "null";
                    case SerializedPropertyType.LayerMask:
                        return property.intValue.ToString(); // LayerMask is stored as int
                    case SerializedPropertyType.ArraySize:
                        return property.arraySize.ToString();
                    case SerializedPropertyType.Character:
                        return ((int)property.intValue).ToString(); // Character stored as int
                    case SerializedPropertyType.Generic:
                        // Generic usually means it's a complex type like an array or custom serializable class
                        // For arrays, we'll just store a marker and handle the elements separately
                        if (property.isArray && property.propertyPath.EndsWith(".Array.size"))
                        {
                            return property.arraySize.ToString();
                        }
                        // For other generic types, we'll need special handling
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log($"[EnhancedDynamics] Generic property type at {property.propertyPath} - may need special handling");
                        }
                        return "generic";
                    default:
                        Debug.LogWarning($"[EnhancedDynamics] Unsupported property type for serialization: {property.propertyType} at {property.propertyPath}");
                        return "";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error serializing property {property.propertyPath} of type {property.propertyType}: {e}");
                return "";
            }
        }
        
        private static void DeserializePropertyValue(SerializedProperty property, string value, SerializedPropertyType type)
        {
            try
            {
                switch (type)
                {
                    case SerializedPropertyType.Float:
                        property.floatValue = float.Parse(value);
                        break;
                    case SerializedPropertyType.Integer:
                        property.intValue = int.Parse(value);
                        break;
                    case SerializedPropertyType.Boolean:
                        property.boolValue = bool.Parse(value);
                        break;
                    case SerializedPropertyType.String:
                        property.stringValue = value;
                        break;
                    case SerializedPropertyType.Vector3:
                        property.vector3Value = JsonUtility.FromJson<Vector3>(value);
                        break;
                    case SerializedPropertyType.Quaternion:
                        property.quaternionValue = JsonUtility.FromJson<Quaternion>(value);
                        break;
                    case SerializedPropertyType.Enum:
                        property.enumValueIndex = int.Parse(value);
                        break;
                    case SerializedPropertyType.Color:
                        property.colorValue = JsonUtility.FromJson<Color>(value);
                        break;
                    case SerializedPropertyType.Vector2:
                        property.vector2Value = JsonUtility.FromJson<Vector2>(value);
                        break;
                    case SerializedPropertyType.Vector4:
                        property.vector4Value = JsonUtility.FromJson<Vector4>(value);
                        break;
                    case SerializedPropertyType.Rect:
                        property.rectValue = JsonUtility.FromJson<Rect>(value);
                        break;
                    case SerializedPropertyType.Bounds:
                        property.boundsValue = JsonUtility.FromJson<Bounds>(value);
                        break;
                    case SerializedPropertyType.AnimationCurve:
                        property.animationCurveValue = JsonUtility.FromJson<AnimationCurve>(value);
                        break;
                    case SerializedPropertyType.ObjectReference:
                        // Object references can't be restored from instance ID in a reliable way
                        // Skip for now, but log it
                        Debug.LogWarning($"[EnhancedDynamics] Cannot restore object reference for property {property.propertyPath}");
                        break;
                    case SerializedPropertyType.LayerMask:
                        property.intValue = int.Parse(value);
                        break;
                    case SerializedPropertyType.ArraySize:
                        property.arraySize = int.Parse(value);
                        break;
                    case SerializedPropertyType.Character:
                        property.intValue = int.Parse(value);
                        break;
                    case SerializedPropertyType.Generic:
                        // For generic types, we need special handling
                        if (property.isArray && property.propertyPath.EndsWith(".Array.size"))
                        {
                            property.arraySize = int.Parse(value);
                        }
                        else
                        {
                            Debug.LogWarning($"[EnhancedDynamics] Cannot deserialize generic property at {property.propertyPath}");
                        }
                        break;
                    default:
                        Debug.LogWarning($"[EnhancedDynamics] Unsupported property type for deserialization: {type} at {property.propertyPath}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error deserializing property {property.propertyPath}: {e}");
            }
        }
        
        private static string GetRelativePath(Transform transform, Transform root)
        {
            if (transform == root) return "";
            
            var path = transform.name;
            var parent = transform.parent;
            
            while (parent != null && parent != root)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            
            return path;
        }
        
        private static string GetScenePath(GameObject obj)
        {
            // Build full hierarchy path including scene name
            var path = GetFullHierarchyPath(obj.transform);
            var scene = obj.scene;
            if (scene.IsValid())
            {
                return scene.name + "/" + path;
            }
            return path;
        }
        
        private static GameObject FindGameObjectByPath(string path)
        {
            // Split scene name and hierarchy path
            var parts = path.Split(new[] { '/' }, 2);
            string sceneName = parts.Length > 1 ? parts[0] : "";
            string hierarchyPath = parts.Length > 1 ? parts[1] : parts[0];
            
            var allObjects = GameObject.FindObjectsOfType<GameObject>();
            foreach (var obj in allObjects)
            {
                // Check if scene matches (if specified)
                if (!string.IsNullOrEmpty(sceneName) && obj.scene.name != sceneName)
                    continue;
                
                // Check if hierarchy path matches
                if (GetFullHierarchyPath(obj.transform) == hierarchyPath)
                {
                    return obj;
                }
            }
            return null;
        }
        
        private static string GetFullHierarchyPath(Transform transform)
        {
            if (transform == null) return "";
            
            var path = transform.name;
            var parent = transform.parent;
            
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            
            return path;
        }
        
        private static Transform FindTransformByPath(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path)) return root;
            
            var segments = path.Split('/');
            var current = root;
            
            foreach (var segment in segments)
            {
                if (string.IsNullOrEmpty(segment)) continue;
                
                Transform found = null;
                // Search immediate children first
                foreach (Transform child in current)
                {
                    if (child.name == segment)
                    {
                        found = child;
                        break;
                    }
                }
                
                if (found == null)
                {
                    Debug.LogWarning($"[EnhancedDynamics] Could not find child '{segment}' in '{current.name}'");
                    return null;
                }
                
                current = found;
            }
            
            return current;
        }
        
        private static Type FindTypeInAssemblies(string typeName)
        {
            // First try to extract just the type name from AssemblyQualifiedName
            var parts = typeName.Split(',');
            var simpleTypeName = parts[0].Trim();
            
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log($"[EnhancedDynamics] Searching for type: {simpleTypeName}");
            }
            
            // Check all loaded assemblies
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    // Skip system assemblies for performance
                    var assemblyName = assembly.GetName().Name;
                    if (assemblyName.StartsWith("System") || assemblyName.StartsWith("mscorlib"))
                        continue;
                    
                    var type = assembly.GetType(simpleTypeName);
                    if (type != null)
                    {
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log($"[EnhancedDynamics] Found type {simpleTypeName} in assembly {assemblyName}");
                        }
                        return type;
                    }
                }
                catch (Exception e)
                {
                    // Some assemblies might throw on GetType, ignore them
                    Debug.LogWarning($"[EnhancedDynamics] Error searching assembly {assembly.GetName().Name}: {e.Message}");
                }
            }
            
            // Special handling for VRChat types
            if (simpleTypeName.Contains("VRCPhysBone") && !simpleTypeName.Contains("Collider"))
            {
                return typeof(VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone);
            }
            else if (simpleTypeName.Contains("VRCPhysBoneCollider"))
            {
                return typeof(VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneCollider);
            }
            else if (simpleTypeName.Contains("VRCContactSender"))
            {
                return typeof(VRC.SDK3.Dynamics.Contact.Components.VRCContactSender);
            }
            else if (simpleTypeName.Contains("VRCContactReceiver"))
            {
                return typeof(VRC.SDK3.Dynamics.Contact.Components.VRCContactReceiver);
            }
            
            Debug.LogError($"[EnhancedDynamics] Could not find type {simpleTypeName} in any loaded assembly");
            return null;
        }
        
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && _hasPendingChanges)
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Entered edit mode with pending physics changes");
                }
                
                // Apply pending changes after a small delay to ensure everything is initialized
                EditorApplication.delayCall += () =>
                {
                    ApplyPendingChanges();
                };
            }
        }
        
        /// <summary>
        /// Check if there are pending changes in memory
        /// </summary>
        public static bool HasPendingChanges => _hasPendingChanges;
        
        /// <summary>
        /// Clear any pending changes without applying them
        /// </summary>
        public static void ClearPendingChanges()
        {
            _pendingChangesJson = "";
            _originalAvatarPath = "";
            _hasPendingChanges = false;
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log("[EnhancedDynamics] Cleared pending physics changes");
            }
        }
        
        /// <summary>
        /// Clear the original component snapshots (called when exiting preview without saving)
        /// </summary>
        public static void ClearOriginalSnapshots()
        {
            _originalComponentSnapshots.Clear();
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log("[EnhancedDynamics] Cleared original component snapshots");
            }
        }
        
        [Serializable]
        private class SerializableChangeList
        {
            public List<ComponentChange> changes;
        }
    }
}
