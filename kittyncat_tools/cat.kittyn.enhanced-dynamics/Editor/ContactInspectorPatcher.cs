using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using HarmonyLib;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.Dynamics;
using Kittyn.Tools;

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
                }
                
                // Patch DrawInspector_Shape
                var drawShapeMethod = contactBaseEditorType.GetMethod("DrawInspector_Shape", BindingFlags.Public | BindingFlags.Instance);
                if (drawShapeMethod != null)
                {
                    var drawShapePostfix = typeof(ContactInspectorPatcher).GetMethod(nameof(DrawInspector_Shape_Postfix), BindingFlags.Static | BindingFlags.NonPublic);
                    _harmony.Patch(drawShapeMethod, null, new HarmonyMethod(drawShapePostfix));
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
            }
        }
        
        private static void OnInspectorGUI_Postfix()
        {
            _isDrawingContactInspector = false;
            _currentContact = null;
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
            // Find the active contact component (like PhysBone does)
            ContactBase activeContact = null;
            ContactGizmoState activeState = null;
            Color gizmoColor = Color.white;
            
            if (Selection.activeGameObject != null)
            {
                // Check for ContactReceiver
                var receiver = Selection.activeGameObject.GetComponent<VRCContactReceiver>();
                if (receiver != null && _gizmoStates.ContainsKey(receiver.GetInstanceID()))
                {
                    activeContact = receiver;
                    activeState = _gizmoStates[receiver.GetInstanceID()];
                    gizmoColor = new Color(0, 1, 1, 0.8f); // Cyan for receivers
                }
                else
                {
                    // Check for ContactSender
                    var sender = Selection.activeGameObject.GetComponent<VRCContactSender>();
                    if (sender != null && _gizmoStates.ContainsKey(sender.GetInstanceID()))
                    {
                        activeContact = sender;
                        activeState = _gizmoStates[sender.GetInstanceID()];
                        gizmoColor = new Color(1, 0.92f, 0.016f, 0.8f); // Yellow for senders
                    }
                }
            }
            
            // Only draw gizmos for the selected contact
            if (activeContact != null && activeState != null)
            {
                DrawContactGizmos(activeContact, gizmoColor);
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
                // Use orange/red color for radius handles to match PhysBone
                Handles.color = new Color(1f, 0.5f, 0f, 0.8f);
                
                var worldPos = transform.TransformPoint(contact.position);
                var worldRotation = transform.rotation * contact.rotation;
                
                // Always show radius handle when gizmo is active (like PhysBone)
                EditorGUI.BeginChangeCheck();
                var newRadius = Handles.RadiusHandle(worldRotation, worldPos, contact.radius);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(contact, "Change Contact Radius");
                    contact.radius = newRadius;
                    EditorUtility.SetDirty(contact);
                }
            }
            
            if (state.heightGizmo && contact.shapeType == ContactBase.ShapeType.Capsule)
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
            
            if (state.positionGizmo)
            {
                Handles.color = color;
                
                var worldPos = transform.TransformPoint(contact.position);
                
                EditorGUI.BeginChangeCheck();
                var newWorldPos = Handles.PositionHandle(worldPos, transform.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(contact, "Change Contact Position");
                    contact.position = transform.InverseTransformPoint(newWorldPos);
                    EditorUtility.SetDirty(contact);
                }
            }
            
            if (state.rotationGizmo)
            {
                Handles.color = color;
                
                var worldPos = transform.TransformPoint(contact.position);
                var worldRot = transform.rotation * contact.rotation;
                
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