using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using HarmonyLib;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.Dynamics;

namespace EnhancedDynamics.Editor
{
    [InitializeOnLoad]
    public static class PhysBoneColliderInspectorPatcher
    {
        private static Harmony _harmony;
        private const string HarmonyId = "com.enhanceddynamics.physbone.inspector";
        
        // Gizmo state management
        private static Dictionary<int, GizmoState> _gizmoStates = new Dictionary<int, GizmoState>();
        private static VRCPhysBoneCollider _currentCollider;
        private static bool _isDrawingPhysBoneInspector = false;
        private static string _lastPropertyName = "";
        
        // GUI content
        private static GUIStyle _miniButtonStyle;
        
        private class GizmoState
        {
            public bool radiusGizmo;
            public bool heightGizmo;
            public bool positionGizmo;
            public bool rotationGizmo;
        }
        
        static PhysBoneColliderInspectorPatcher()
        {
            Debug.Log("[EnhancedDynamics] Initializing PhysBoneColliderInspectorPatcher...");
            
            // Delay initialization to ensure VRChat assemblies are loaded
            EditorApplication.delayCall += DelayedInitialize;
        }
        
        private static void DelayedInitialize()
        {
            Debug.Log("[EnhancedDynamics] Delayed initialization starting...");
            
            _harmony = new Harmony(HarmonyId);
            
            // Patch the inspector
            PatchInspector();
            
            // Patch PropertyField with correct signature
            PatchPropertyField();
            
            // Patch the rotation field method
            PatchRotationField();
            
            // Try patching EditorGUI float/vector fields directly
            PatchEditorGUIFields();
            
            // Subscribe to scene GUI
            SceneView.duringSceneGui += OnSceneGUI;
            
            Debug.Log("[EnhancedDynamics] Initialization complete!");
        }
        
        private static void PatchInspector()
        {
            try
            {
                Debug.Log("[EnhancedDynamics] Searching for VRCPhysBoneCollider inspector...");
                
                Type inspectorType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (assembly.FullName.Contains("VRC") || assembly.FullName.Contains("PhysBone"))
                        {
                            foreach (var type in assembly.GetTypes())
                            {
                                if (type.IsSubclassOf(typeof(UnityEditor.Editor)))
                                {
                                    var customEditorAttr = type.GetCustomAttribute<CustomEditor>();
                                    if (customEditorAttr != null)
                                    {
                                        var inspectedType = customEditorAttr.GetType().GetField("m_InspectedType", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(customEditorAttr) as Type;
                                        if (inspectedType == typeof(VRCPhysBoneCollider))
                                        {
                                            Debug.Log($"[EnhancedDynamics] Found VRCPhysBoneCollider editor: {type.FullName}");
                                            inspectorType = type;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
                
                if (inspectorType != null)
                {
                    var onInspectorGUI = inspectorType.GetMethod("OnInspectorGUI", BindingFlags.Public | BindingFlags.Instance);
                    if (onInspectorGUI != null)
                    {
                        var prefix = typeof(PhysBoneColliderInspectorPatcher).GetMethod(nameof(OnInspectorGUI_Prefix),
                            BindingFlags.Static | BindingFlags.NonPublic);
                        var postfix = typeof(PhysBoneColliderInspectorPatcher).GetMethod(nameof(OnInspectorGUI_Postfix),
                            BindingFlags.Static | BindingFlags.NonPublic);
                        
                        _harmony.Patch(onInspectorGUI, 
                            prefix: new HarmonyMethod(prefix),
                            postfix: new HarmonyMethod(postfix));
                        
                        Debug.Log("[EnhancedDynamics] Successfully patched VRCPhysBoneCollider inspector!");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to patch inspector: {e}");
            }
        }
        
        private static void PatchPropertyField()
        {
            try
            {
                Debug.Log("[EnhancedDynamics] Searching for VRC custom field methods...");
                
                // Search for VRC-specific field drawing methods similar to QuaternionAsEulerField
                Type vrcEditorType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.FullName.Contains("VRC") && assembly.FullName.Contains("Editor"))
                    {
                        // Look for the VRCPhysBoneColliderEditor type
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.Name.Contains("VRCPhysBoneColliderEditor") || 
                                (type.IsSubclassOf(typeof(UnityEditor.Editor)) && 
                                 type.GetCustomAttribute<CustomEditor>()?.GetType().GetField("m_InspectedType", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(type.GetCustomAttribute<CustomEditor>()) as Type == typeof(VRCPhysBoneCollider)))
                            {
                                vrcEditorType = type;
                                Debug.Log($"[EnhancedDynamics] Found VRC editor type: {type.FullName}");
                                
                                // Look for all methods in this type
                                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                                foreach (var method in methods)
                                {
                                    Debug.Log($"[EnhancedDynamics] VRC Editor method: {method.Name} (Parameters: {method.GetParameters().Length})");
                                    
                                    // Try to patch any method that might be drawing our fields
                                    if (method.Name.Contains("Field") || method.Name.Contains("Draw") || 
                                        method.Name.Contains("Radius") || method.Name.Contains("Height") || 
                                        method.Name.Contains("Position") || method.Name.Contains("Shape"))
                                    {
                                        try
                                        {
                                            var postfix = typeof(PhysBoneColliderInspectorPatcher).GetMethod(nameof(GenericField_Postfix),
                                                BindingFlags.Static | BindingFlags.NonPublic);
                                            _harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                                            Debug.Log($"[EnhancedDynamics] Patched method: {method.Name}");
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.Log($"[EnhancedDynamics] Could not patch {method.Name}: {ex.Message}");
                                        }
                                    }
                                }
                                break;
                            }
                        }
                    }
                }
                
                // Also search InspectorUtil for more custom field methods
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.FullName.Contains("VRC"))
                    {
                        var inspectorUtil = assembly.GetType("InspectorUtil");
                        if (inspectorUtil != null)
                        {
                            var methods = inspectorUtil.GetMethods(BindingFlags.Public | BindingFlags.Static);
                            foreach (var method in methods)
                            {
                                if (method.Name.Contains("Field") && !method.Name.Contains("QuaternionAsEuler"))
                                {
                                    Debug.Log($"[EnhancedDynamics] Found InspectorUtil method: {method.Name}");
                                    try
                                    {
                                        var postfix = typeof(PhysBoneColliderInspectorPatcher).GetMethod(nameof(InspectorUtil_Field_Postfix),
                                            BindingFlags.Static | BindingFlags.NonPublic);
                                        _harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                                        Debug.Log($"[EnhancedDynamics] Patched InspectorUtil.{method.Name}");
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to patch custom field methods: {e}");
            }
        }
        
        private static void PatchRotationField()
        {
            try
            {
                Debug.Log("[EnhancedDynamics] Searching for InspectorUtil.QuaternionAsEulerField...");
                
                // Find InspectorUtil type in VRC assemblies
                Type inspectorUtilType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.FullName.Contains("VRC"))
                    {
                        var type = assembly.GetType("InspectorUtil");
                        if (type != null)
                        {
                            inspectorUtilType = type;
                            break;
                        }
                    }
                }
                
                if (inspectorUtilType != null)
                {
                    var quaternionMethod = inspectorUtilType.GetMethod("QuaternionAsEulerField", 
                        BindingFlags.Public | BindingFlags.Static);
                    
                    if (quaternionMethod != null)
                    {
                        var postfix = typeof(PhysBoneColliderInspectorPatcher).GetMethod(nameof(QuaternionAsEulerField_Postfix),
                            BindingFlags.Static | BindingFlags.NonPublic);
                        
                        _harmony.Patch(quaternionMethod, postfix: new HarmonyMethod(postfix));
                        Debug.Log("[EnhancedDynamics] Successfully patched QuaternionAsEulerField!");
                    }
                }
                else
                {
                    Debug.LogWarning("[EnhancedDynamics] Could not find InspectorUtil type!");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to patch rotation field: {e}");
            }
        }
        
        private static void PatchEditorGUIFields()
        {
            try
            {
                Debug.Log("[EnhancedDynamics] Patching EditorGUI field methods...");
                
                // Patch FloatField
                var floatFieldMethods = typeof(EditorGUILayout).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "FloatField").ToList();
                
                foreach (var method in floatFieldMethods)
                {
                    try
                    {
                        var postfix = typeof(PhysBoneColliderInspectorPatcher).GetMethod(nameof(FloatField_Postfix),
                            BindingFlags.Static | BindingFlags.NonPublic);
                        _harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                        Debug.Log($"[EnhancedDynamics] Patched FloatField overload with {method.GetParameters().Length} parameters");
                    }
                    catch { }
                }
                
                // Patch Vector3Field
                var vector3FieldMethods = typeof(EditorGUILayout).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "Vector3Field").ToList();
                
                foreach (var method in vector3FieldMethods)
                {
                    try
                    {
                        var postfix = typeof(PhysBoneColliderInspectorPatcher).GetMethod(nameof(Vector3Field_Postfix),
                            BindingFlags.Static | BindingFlags.NonPublic);
                        _harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                        Debug.Log($"[EnhancedDynamics] Patched Vector3Field overload with {method.GetParameters().Length} parameters");
                    }
                    catch { }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to patch EditorGUI fields: {e}");
            }
        }
        
        private static SerializedProperty _lastFoundProperty;
        
        private static void FindProperty_Postfix(SerializedProperty __result, string propertyPath)
        {
            if (_isDrawingPhysBoneInspector && __result != null)
            {
                Debug.Log($"[EnhancedDynamics] FindProperty called for: {propertyPath}");
                if (propertyPath == "radius" || propertyPath == "height" || propertyPath == "position" || propertyPath == "rotation")
                {
                    _lastFoundProperty = __result;
                }
            }
        }
        
        private static void EditorGUI_PropertyField_Postfix(Rect position, SerializedProperty property)
        {
            if (_isDrawingPhysBoneInspector && property != null)
            {
                Debug.Log($"[EnhancedDynamics] EditorGUI.PropertyField called for: {property.name}");
            }
        }
        
        private static void OnInspectorGUI_Prefix(UnityEditor.Editor __instance)
        {
            _currentCollider = __instance.target as VRCPhysBoneCollider;
            _isDrawingPhysBoneInspector = _currentCollider != null;
        }
        
        private static void OnInspectorGUI_Postfix(UnityEditor.Editor __instance)
        {
            // Try a different approach - add buttons for all fields at once
            var collider = __instance.target as VRCPhysBoneCollider;
            if (collider != null)
            {
                var instanceId = collider.GetInstanceID();
                if (!_gizmoStates.ContainsKey(instanceId))
                {
                    _gizmoStates[instanceId] = new GizmoState();
                }
                var state = _gizmoStates[instanceId];
                
                // Add a section for all gizmo buttons
                EditorGUILayout.Space(10);
                
                // Create a box for better visual separation
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Viewport Gizmos", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                
                // Radius button (not for Plane)
                if (collider.shapeType != VRCPhysBoneColliderBase.ShapeType.Plane)
                {
                    GUI.backgroundColor = state.radiusGizmo ? Color.green : Color.red;
                    if (GUILayout.Button("Radius (R)", GUILayout.Width(80)))
                    {
                        state.radiusGizmo = !state.radiusGizmo;
                        SceneView.RepaintAll();
                    }
                }
                
                // Height button (only for Capsule)
                if (collider.shapeType == VRCPhysBoneColliderBase.ShapeType.Capsule)
                {
                    GUI.backgroundColor = state.heightGizmo ? Color.green : Color.red;
                    if (GUILayout.Button("Height (H)", GUILayout.Width(80)))
                    {
                        state.heightGizmo = !state.heightGizmo;
                        SceneView.RepaintAll();
                    }
                }
                
                // Position button
                GUI.backgroundColor = state.positionGizmo ? Color.green : Color.red;
                if (GUILayout.Button("Position (P)", GUILayout.Width(80)))
                {
                    state.positionGizmo = !state.positionGizmo;
                    SceneView.RepaintAll();
                }
                
                // Rotation button
                GUI.backgroundColor = state.rotationGizmo ? Color.green : Color.red;
                if (GUILayout.Button("Rotation (↻)", GUILayout.Width(80)))
                {
                    state.rotationGizmo = !state.rotationGizmo;
                    SceneView.RepaintAll();
                }
                
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            
            _isDrawingPhysBoneInspector = false;
            _currentCollider = null;
        }
        
        private static void PropertyField_Prefix(SerializedProperty property, GUILayoutOption[] options)
        {
            if (_isDrawingPhysBoneInspector && property != null)
            {
                Debug.Log($"[EnhancedDynamics] PropertyField_Prefix - Property: {property.name}, Options: {options?.Length ?? 0}");
            }
        }
        
        private static void PropertyField_Postfix(bool __result, object[] __args)
        {
            // Extract SerializedProperty from first argument
            if (!_isDrawingPhysBoneInspector || !__result || _currentCollider == null || 
                __args == null || __args.Length == 0 || !(__args[0] is SerializedProperty property))
                return;
            
            var propertyName = property.name;
            Debug.Log($"[EnhancedDynamics] PropertyField_Postfix called for: {propertyName} (args: {__args.Length})");
            
            // Check if this is one of our target properties
            if (propertyName == "radius" || propertyName == "height" || propertyName == "position")
            {
                _lastPropertyName = propertyName;
                DrawInlineButton(propertyName);
            }
        }
        
        private static void QuaternionAsEulerField_Postfix(SerializedProperty property)
        {
            if (!_isDrawingPhysBoneInspector || _currentCollider == null || property == null)
                return;
            
            Debug.Log($"[EnhancedDynamics] QuaternionAsEulerField_Postfix called for: {property?.name}");
            
            // This is called for rotation field
            if (property.name == "rotation")
            {
                _lastPropertyName = "rotation";
                DrawInlineButton("rotation");
            }
        }
        
        private static void GenericField_Postfix(object __instance, MethodBase __originalMethod, object[] __args)
        {
            if (!_isDrawingPhysBoneInspector || _currentCollider == null)
                return;
            
            Debug.Log($"[EnhancedDynamics] GenericField_Postfix called for method: {__originalMethod.Name}");
            
            // Try to extract property information from the arguments
            SerializedProperty property = null;
            foreach (var arg in __args)
            {
                if (arg is SerializedProperty prop)
                {
                    property = prop;
                    break;
                }
            }
            
            if (property != null && (property.name == "radius" || property.name == "height" || property.name == "position"))
            {
                Debug.Log($"[EnhancedDynamics] Found property {property.name} in {__originalMethod.Name}");
                DrawInlineButton(property.name);
            }
        }
        
        private static void InspectorUtil_Field_Postfix(object[] __args, MethodBase __originalMethod)
        {
            if (!_isDrawingPhysBoneInspector || _currentCollider == null)
                return;
            
            Debug.Log($"[EnhancedDynamics] InspectorUtil_Field_Postfix called for: {__originalMethod.Name}");
            
            // InspectorUtil methods might have different signatures, try to find SerializedProperty
            SerializedProperty property = null;
            foreach (var arg in __args)
            {
                if (arg is SerializedProperty prop)
                {
                    property = prop;
                    break;
                }
            }
            
            if (property != null && (property.name == "radius" || property.name == "height" || property.name == "position"))
            {
                Debug.Log($"[EnhancedDynamics] Found property {property.name} in InspectorUtil.{__originalMethod.Name}");
                DrawInlineButton(property.name);
            }
        }
        
        private static void FloatField_Postfix(float __result, object[] __args)
        {
            if (!_isDrawingPhysBoneInspector || _currentCollider == null)
                return;
            
            // Check if this is a float field with a label that matches our properties
            string label = null;
            if (__args.Length > 0 && __args[0] is string str)
                label = str;
            else if (__args.Length > 0 && __args[0] is GUIContent content)
                label = content.text;
            
            Debug.Log($"[EnhancedDynamics] FloatField_Postfix - Label: {label ?? "null"}");
            
            if (label == "Radius" || label == "Height")
            {
                DrawInlineButton(label.ToLower());
            }
        }
        
        private static void Vector3Field_Postfix(Vector3 __result, object[] __args)
        {
            if (!_isDrawingPhysBoneInspector || _currentCollider == null)
                return;
            
            // Check if this is a vector field with a label that matches our properties
            string label = null;
            if (__args.Length > 0 && __args[0] is string str)
                label = str;
            else if (__args.Length > 0 && __args[0] is GUIContent content)
                label = content.text;
            
            Debug.Log($"[EnhancedDynamics] Vector3Field_Postfix - Label: {label ?? "null"}");
            
            if (label == "Position")
            {
                DrawInlineButton("position");
            }
        }
        
        private static void DrawInlineButton(string propertyName)
        {
            Debug.Log($"[EnhancedDynamics] DrawInlineButton called for: {propertyName}");
            
            // Get or create gizmo state
            var instanceId = _currentCollider.GetInstanceID();
            if (!_gizmoStates.ContainsKey(instanceId))
            {
                _gizmoStates[instanceId] = new GizmoState();
            }
            var state = _gizmoStates[instanceId];
            
            // Determine which button to draw
            bool isActive = false;
            string buttonText = "";
            bool shouldDraw = true;
            
            switch (propertyName)
            {
                case "radius":
                    if (_currentCollider.shapeType == VRCPhysBoneColliderBase.ShapeType.Plane)
                        shouldDraw = false;
                    isActive = state.radiusGizmo;
                    buttonText = "Toggle Radius Gizmo";
                    break;
                case "height":
                    if (_currentCollider.shapeType != VRCPhysBoneColliderBase.ShapeType.Capsule)
                        shouldDraw = false;
                    isActive = state.heightGizmo;
                    buttonText = "Toggle Height Gizmo";
                    break;
                case "position":
                    isActive = state.positionGizmo;
                    buttonText = "Toggle Position Gizmo";
                    break;
                case "rotation":
                    isActive = state.rotationGizmo;
                    buttonText = "Toggle Rotation Gizmo";
                    break;
                default:
                    shouldDraw = false;
                    break;
            }
            
            if (!shouldDraw)
                return;
            
            // Draw button similar to how rotation button works
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = isActive ? Color.green : Color.red;
            
            if (GUILayout.Button(buttonText, GUILayout.Height(18)))
            {
                Debug.Log($"[EnhancedDynamics] Button clicked for: {propertyName}");
                // Toggle the appropriate gizmo
                switch (propertyName)
                {
                    case "radius": state.radiusGizmo = !state.radiusGizmo; break;
                    case "height": state.heightGizmo = !state.heightGizmo; break;
                    case "position": state.positionGizmo = !state.positionGizmo; break;
                    case "rotation": state.rotationGizmo = !state.rotationGizmo; break;
                }
                SceneView.RepaintAll();
            }
            
            GUI.backgroundColor = oldColor;
        }
        
        private static void OnSceneGUI(SceneView sceneView)
        {
            // Find the active collider
            VRCPhysBoneCollider activeCollider = null;
            GizmoState activeState = null;
            
            if (Selection.activeGameObject != null)
            {
                var collider = Selection.activeGameObject.GetComponent<VRCPhysBoneCollider>();
                if (collider != null && _gizmoStates.ContainsKey(collider.GetInstanceID()))
                {
                    activeCollider = collider;
                    activeState = _gizmoStates[collider.GetInstanceID()];
                }
            }
            
            if (activeCollider == null || activeState == null)
                return;
            
            var transform = activeCollider.transform;
            
            // Set up handle matrix
            var oldMatrix = Handles.matrix;
            Handles.matrix = transform.localToWorldMatrix;
            
            // Draw radius gizmo
            if (activeState.radiusGizmo && activeCollider.shapeType != VRCPhysBoneColliderBase.ShapeType.Plane)
            {
                EditorGUI.BeginChangeCheck();
                Handles.color = new Color(1f, 0.5f, 0f, 0.8f);
                
                var newRadius = Handles.RadiusHandle(
                    activeCollider.rotation,
                    activeCollider.position,
                    activeCollider.radius
                );
                
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(activeCollider, "Change PhysBone Radius");
                    activeCollider.radius = newRadius;
                    EditorUtility.SetDirty(activeCollider);
                }
            }
            
            // Draw height gizmo for capsule
            if (activeState.heightGizmo && activeCollider.shapeType == VRCPhysBoneColliderBase.ShapeType.Capsule)
            {
                EditorGUI.BeginChangeCheck();
                Handles.color = new Color(0f, 1f, 0.5f, 0.8f);
                
                // Draw height handle as a line
                var heightVector = activeCollider.rotation * Vector3.up * activeCollider.height;
                var endPos = activeCollider.position + heightVector;
                
                var newEndPos = Handles.PositionHandle(endPos, activeCollider.rotation);
                
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(activeCollider, "Change PhysBone Height");
                    var localEnd = Quaternion.Inverse(activeCollider.rotation) * (newEndPos - activeCollider.position);
                    activeCollider.height = localEnd.y;
                    EditorUtility.SetDirty(activeCollider);
                }
            }
            
            // Draw position gizmo
            if (activeState.positionGizmo)
            {
                EditorGUI.BeginChangeCheck();
                
                var newPosition = Handles.PositionHandle(
                    activeCollider.position,
                    activeCollider.rotation
                );
                
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(activeCollider, "Move PhysBone Collider");
                    activeCollider.position = newPosition;
                    EditorUtility.SetDirty(activeCollider);
                }
            }
            
            // Draw rotation gizmo
            if (activeState.rotationGizmo)
            {
                EditorGUI.BeginChangeCheck();
                
                var newRotation = Handles.RotationHandle(
                    activeCollider.rotation,
                    activeCollider.position
                );
                
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(activeCollider, "Rotate PhysBone Collider");
                    activeCollider.rotation = newRotation;
                    EditorUtility.SetDirty(activeCollider);
                }
            }
            
            Handles.matrix = oldMatrix;
        }
    }
}