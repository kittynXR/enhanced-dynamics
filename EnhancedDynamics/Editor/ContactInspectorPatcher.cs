using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using HarmonyLib;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.Dynamics;

namespace EnhancedDynamics.Editor
{
    [InitializeOnLoad]
    public static class ContactInspectorPatcher
    {
        private static Harmony _harmony;
        private const string HarmonyId = "com.enhanceddynamics.contact.inspector";
        
        // Gizmo state management
        private static Dictionary<int, ContactGizmoState> _gizmoStates = new Dictionary<int, ContactGizmoState>();
        private static ContactBase _currentContact;
        private static bool _isDrawingContactInspector = false;
        private static string _currentPropertyPath = "";
        
        // Button rendering state
        private static Dictionary<string, Rect> _pendingButtonRects = new Dictionary<string, Rect>();
        private static string _clickedButton = null;
        
        private class ContactGizmoState
        {
            public bool radiusGizmo;
            public bool heightGizmo;
            public bool positionGizmo;
            public bool rotationGizmo;
        }
        
        static ContactInspectorPatcher()
        {
            try
            {
                Debug.Log("[EnhancedDynamics] Initializing ContactInspectorPatcher...");
                
                // Clean up any existing harmony instance
                if (_harmony != null)
                {
                    _harmony.UnpatchAll(HarmonyId);
                    _harmony = null;
                }
                
                // Initialize immediately
                Initialize();
                
                // Also delay initialization to ensure VRChat assemblies are loaded
                EditorApplication.delayCall += DelayedInitialize;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error in ContactInspectorPatcher static constructor: {e}");
            }
        }
        
        private static void DelayedInitialize()
        {
            Debug.Log("[EnhancedDynamics] ContactInspectorPatcher delayed initialization...");
            Initialize();
        }
        
        private static void Initialize()
        {
            try
            {
                if (_harmony != null)
                {
                    _harmony.UnpatchAll(HarmonyId);
                }
                
                _harmony = new Harmony(HarmonyId);
                
                // Find VRCContactBaseEditor type
                Assembly contactEditorAssembly = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "VRC.SDK3.Dynamics.Contact.Editor")
                    {
                        contactEditorAssembly = assembly;
                        break;
                    }
                }
                
                if (contactEditorAssembly == null)
                {
                    Debug.LogWarning("[EnhancedDynamics] VRC.SDK3.Dynamics.Contact.Editor assembly not found");
                    return;
                }
                
                // Get the VRCContactBaseEditor type
                var contactBaseEditorType = contactEditorAssembly.GetType("VRC.SDK3.Dynamics.Contact.VRCContactBaseEditor");
                if (contactBaseEditorType == null)
                {
                    Debug.LogError("[EnhancedDynamics] VRCContactBaseEditor type not found");
                    return;
                }
                
                // Patch OnInspectorGUI
                var onInspectorGUIMethod = contactBaseEditorType.GetMethod("OnInspectorGUI", BindingFlags.Public | BindingFlags.Instance);
                if (onInspectorGUIMethod != null)
                {
                    var onInspectorGUIPrefix = typeof(ContactInspectorPatcher).GetMethod(nameof(OnInspectorGUI_Prefix), BindingFlags.Static | BindingFlags.NonPublic);
                    var onInspectorGUIPostfix = typeof(ContactInspectorPatcher).GetMethod(nameof(OnInspectorGUI_Postfix), BindingFlags.Static | BindingFlags.NonPublic);
                    _harmony.Patch(onInspectorGUIMethod, new HarmonyMethod(onInspectorGUIPrefix), new HarmonyMethod(onInspectorGUIPostfix));
                    Debug.Log("[EnhancedDynamics] Patched VRCContactBaseEditor.OnInspectorGUI");
                }
                
                // Patch DrawInspector_Shape
                var drawShapeMethod = contactBaseEditorType.GetMethod("DrawInspector_Shape", BindingFlags.Public | BindingFlags.Instance);
                if (drawShapeMethod != null)
                {
                    var drawShapePostfix = typeof(ContactInspectorPatcher).GetMethod(nameof(DrawInspector_Shape_Postfix), BindingFlags.Static | BindingFlags.NonPublic);
                    _harmony.Patch(drawShapeMethod, null, new HarmonyMethod(drawShapePostfix));
                    Debug.Log("[EnhancedDynamics] Patched VRCContactBaseEditor.DrawInspector_Shape");
                }
                
                // Patch PropertyField to intercept property rendering
                var propertyFieldMethods = typeof(EditorGUILayout).GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .Where(m => m.Name == "PropertyField");
                
                foreach (var method in propertyFieldMethods)
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length >= 1 && parameters[0].ParameterType == typeof(SerializedProperty))
                    {
                        try
                        {
                            var prefix = typeof(ContactInspectorPatcher).GetMethod(nameof(PropertyField_Prefix), BindingFlags.Static | BindingFlags.NonPublic);
                            var postfix = typeof(ContactInspectorPatcher).GetMethod(nameof(PropertyField_Postfix), BindingFlags.Static | BindingFlags.NonPublic);
                            _harmony.Patch(method, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
                            Debug.Log($"[EnhancedDynamics] Patched PropertyField method with {parameters.Length} parameters");
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[EnhancedDynamics] Failed to patch PropertyField variant: {e.Message}");
                        }
                    }
                }
                Debug.Log("[EnhancedDynamics] Patched EditorGUILayout.PropertyField methods");
                
                // Patch QuaternionAsEulerField for rotation
                var inspectorUtilType = contactEditorAssembly.GetType("VRC.Dynamics.InspectorUtil");
                if (inspectorUtilType != null)
                {
                    var quaternionMethod = inspectorUtilType.GetMethod("QuaternionAsEulerField", BindingFlags.Static | BindingFlags.Public);
                    if (quaternionMethod != null)
                    {
                        var quaternionPostfix = typeof(ContactInspectorPatcher).GetMethod(nameof(QuaternionAsEulerField_Postfix), BindingFlags.Static | BindingFlags.NonPublic);
                        _harmony.Patch(quaternionMethod, null, new HarmonyMethod(quaternionPostfix));
                        Debug.Log("[EnhancedDynamics] Patched InspectorUtil.QuaternionAsEulerField");
                    }
                }
                
                // Hook into scene view
                SceneView.duringSceneGui -= OnSceneGUI;
                SceneView.duringSceneGui += OnSceneGUI;
                
                Debug.Log("[EnhancedDynamics] ContactInspectorPatcher initialization complete");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error initializing ContactInspectorPatcher: {e}");
            }
        }
        
        private static void OnInspectorGUI_Prefix(UnityEditor.Editor __instance)
        {
            if (__instance.target is ContactBase contact)
            {
                _currentContact = contact;
                _isDrawingContactInspector = true;
                _pendingButtonRects.Clear(); // Clear button rects from previous frame
            }
        }
        
        private static void OnInspectorGUI_Postfix()
        {
            _isDrawingContactInspector = false;
            _currentContact = null;
        }
        
        private static bool PropertyField_Prefix(object[] __args, ref bool __result)
        {
            if (!_isDrawingContactInspector || _currentContact == null || __args.Length == 0)
                return true; // Run original method
            
            // Get the SerializedProperty from the first argument
            var property = __args[0] as SerializedProperty;
            if (property == null)
                return true;
            
            // Check if this is one of our target properties
            if (property.propertyPath == "radius" || property.propertyPath == "height" || 
                property.propertyPath == "position" || property.propertyPath == "rotation")
            {
                var instanceId = _currentContact.GetInstanceID();
                if (!_gizmoStates.ContainsKey(instanceId))
                    _gizmoStates[instanceId] = new ContactGizmoState();
                
                var state = _gizmoStates[instanceId];
                
                // Draw our custom property field with inline button
                DrawPropertyFieldWithButton(property, state);
                
                __result = true; // Tell Unity the property was drawn
                return false; // Skip the original method
            }
            
            return true; // Run original method for other properties
        }
        
        private static void PropertyField_Postfix(SerializedProperty property)
        {
            // No longer needed - everything is handled in the prefix
        }
        
        private static void DrawPropertyFieldWithButton(SerializedProperty property, ContactGizmoState state)
        {
            EditorGUILayout.BeginHorizontal();
            
            // Draw the property field in a controlled width
            var fieldRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            var buttonWidth = 25f;
            var spacing = 2f;
            
            // Adjust field rect to make room for button
            fieldRect.width -= (buttonWidth + spacing);
            
            // Draw the property field
            EditorGUI.PropertyField(fieldRect, property, new GUIContent(property.displayName));
            
            // Draw the button
            var buttonRect = new Rect(fieldRect.x + fieldRect.width + spacing, fieldRect.y, buttonWidth, fieldRect.height);
            
            string buttonLabel = "";
            bool currentState = false;
            
            switch (property.propertyPath)
            {
                case "radius":
                    buttonLabel = state.radiusGizmo ? "●" : "○";
                    currentState = state.radiusGizmo;
                    break;
                case "height":
                    buttonLabel = state.heightGizmo ? "↕" : "│";
                    currentState = state.heightGizmo;
                    break;
                case "position":
                    buttonLabel = state.positionGizmo ? "⊕" : "⊙";
                    currentState = state.positionGizmo;
                    break;
                case "rotation":
                    buttonLabel = state.rotationGizmo ? "↻" : "○";
                    currentState = state.rotationGizmo;
                    break;
            }
            
            if (GUI.Button(buttonRect, buttonLabel, EditorStyles.miniButton))
            {
                switch (property.propertyPath)
                {
                    case "radius":
                        state.radiusGizmo = !state.radiusGizmo;
                        break;
                    case "height":
                        state.heightGizmo = !state.heightGizmo;
                        break;
                    case "position":
                        state.positionGizmo = !state.positionGizmo;
                        break;
                    case "rotation":
                        state.rotationGizmo = !state.rotationGizmo;
                        break;
                }
                SceneView.RepaintAll();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private static void QuaternionAsEulerField_Postfix(SerializedProperty quaternionProperty)
        {
            // No longer needed - rotation is handled in PropertyField
        }
        
        private static void DrawInspector_Shape_Postfix()
        {
            if (!_isDrawingContactInspector || _currentContact == null)
                return;
            
            // Add viewport gizmos section at the bottom of the Shape section
            var instanceId = _currentContact.GetInstanceID();
            if (!_gizmoStates.ContainsKey(instanceId))
                _gizmoStates[instanceId] = new ContactGizmoState();
            
            var state = _gizmoStates[instanceId];
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Viewport Gizmos", EditorStyles.miniLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            // Store original background color
            var originalColor = GUI.backgroundColor;
            
            // Radius button
            GUI.backgroundColor = state.radiusGizmo ? Color.green : Color.red;
            if (GUILayout.Button(state.radiusGizmo ? "● Radius" : "○ Radius", EditorStyles.miniButton))
            {
                state.radiusGizmo = !state.radiusGizmo;
                SceneView.RepaintAll();
            }
            
            // Height button (only for capsules)
            if (_currentContact.shapeType == ContactBase.ShapeType.Capsule)
            {
                GUI.backgroundColor = state.heightGizmo ? Color.green : Color.red;
                if (GUILayout.Button(state.heightGizmo ? "● Height" : "○ Height", EditorStyles.miniButton))
                {
                    state.heightGizmo = !state.heightGizmo;
                    SceneView.RepaintAll();
                }
            }
            
            // Position button
            GUI.backgroundColor = state.positionGizmo ? Color.green : Color.red;
            if (GUILayout.Button(state.positionGizmo ? "● Position" : "○ Position", EditorStyles.miniButton))
            {
                state.positionGizmo = !state.positionGizmo;
                SceneView.RepaintAll();
            }
            
            // Rotation button
            GUI.backgroundColor = state.rotationGizmo ? Color.green : Color.red;
            if (GUILayout.Button(state.rotationGizmo ? "● Rotation" : "○ Rotation", EditorStyles.miniButton))
            {
                state.rotationGizmo = !state.rotationGizmo;
                SceneView.RepaintAll();
            }
            
            // Restore original background color
            GUI.backgroundColor = originalColor;
            
            EditorGUILayout.EndHorizontal();
        }
        
        private static void OnSceneGUI(SceneView sceneView)
        {
            // Find all contact components in the scene
            var receivers = GameObject.FindObjectsOfType<VRCContactReceiver>();
            var senders = GameObject.FindObjectsOfType<VRCContactSender>();
            
            foreach (var receiver in receivers)
            {
                DrawContactGizmos(receiver, new Color(0, 1, 1, 0.8f)); // Cyan for receivers
            }
            
            foreach (var sender in senders)
            {
                DrawContactGizmos(sender, new Color(1, 0.92f, 0.016f, 0.8f)); // Yellow for senders
            }
        }
        
        private static void DrawContactGizmos(ContactBase contact, Color color)
        {
            if (contact == null || !_gizmoStates.ContainsKey(contact.GetInstanceID()))
                return;
            
            var state = _gizmoStates[contact.GetInstanceID()];
            var transform = contact.GetRootTransform();
            if (transform == null)
                return;
            
            // Check if this contact is selected
            bool isSelected = Selection.Contains(contact.gameObject);
            
            if (state.radiusGizmo)
            {
                Handles.color = color;
                
                var worldPos = transform.TransformPoint(contact.position);
                var worldScale = transform.lossyScale;
                var scaledRadius = contact.radius * Mathf.Max(worldScale.x, worldScale.y, worldScale.z);
                
                if (isSelected)
                {
                    // Show interactive radius handle when selected
                    EditorGUI.BeginChangeCheck();
                    var worldRotation = transform.rotation * contact.rotation;
                    var newRadius = Handles.RadiusHandle(worldRotation, worldPos, scaledRadius);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(contact, "Change Contact Radius");
                        contact.radius = newRadius / Mathf.Max(worldScale.x, worldScale.y, worldScale.z);
                        EditorUtility.SetDirty(contact);
                    }
                }
            }
            
            if (state.heightGizmo && contact.shapeType == ContactBase.ShapeType.Capsule)
            {
                if (isSelected)
                {
                    Handles.color = new Color(1f, 0.92f, 0.016f, 0.8f); // Yellow for height
                    
                    var worldPos = transform.TransformPoint(contact.position);
                    var halfHeight = contact.height * 0.5f;
                    var upVector = transform.rotation * contact.rotation * Vector3.up;
                    var topPos = worldPos + upVector * halfHeight * transform.lossyScale.y;
                    var bottomPos = worldPos - upVector * halfHeight * transform.lossyScale.y;
                    
                    EditorGUI.BeginChangeCheck();
                    var newTopPos = Handles.Slider(topPos, upVector, HandleUtility.GetHandleSize(topPos) * 1.0f, Handles.ArrowHandleCap, 0.1f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(contact, "Change Contact Height");
                        var delta = Vector3.Project(newTopPos - topPos, upVector);
                        contact.height += delta.magnitude * 2f / transform.lossyScale.y * (Vector3.Dot(delta, upVector) > 0 ? 1 : -1);
                        contact.height = Mathf.Max(0.01f, contact.height);
                        EditorUtility.SetDirty(contact);
                    }
                    
                    EditorGUI.BeginChangeCheck();
                    var newBottomPos = Handles.Slider(bottomPos, -upVector, HandleUtility.GetHandleSize(bottomPos) * 1.0f, Handles.ArrowHandleCap, 0.1f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(contact, "Change Contact Height");
                        var delta = Vector3.Project(newBottomPos - bottomPos, -upVector);
                        contact.height += delta.magnitude * 2f / transform.lossyScale.y * (Vector3.Dot(delta, -upVector) > 0 ? 1 : -1);
                        contact.height = Mathf.Max(0.01f, contact.height);
                        EditorUtility.SetDirty(contact);
                    }
                    
                    // Draw capsule line
                    Handles.DrawAAPolyLine(3.0f, topPos, bottomPos);
                }
            }
            
            if (state.positionGizmo)
            {
                Handles.color = color;
                
                var worldPos = transform.TransformPoint(contact.position);
                
                if (isSelected)
                {
                    EditorGUI.BeginChangeCheck();
                    var newWorldPos = Handles.PositionHandle(worldPos, transform.rotation);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(contact, "Change Contact Position");
                        contact.position = transform.InverseTransformPoint(newWorldPos);
                        EditorUtility.SetDirty(contact);
                    }
                }
            }
            
            if (state.rotationGizmo)
            {
                Handles.color = color;
                
                var worldPos = transform.TransformPoint(contact.position);
                var worldRot = transform.rotation * contact.rotation;
                
                if (isSelected)
                {
                    EditorGUI.BeginChangeCheck();
                    var newWorldRot = Handles.RotationHandle(worldRot, worldPos);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(contact, "Change Contact Rotation");
                        contact.rotation = Quaternion.Inverse(transform.rotation) * newWorldRot;
                        EditorUtility.SetDirty(contact);
                    }
                }
            }
        }
    }
}