using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using HarmonyLib;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.Dynamics;
using Kittyn.Tools.EnhancedDynamics;

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
        
        
        private class GizmoState
        {
            public bool radiusGizmo;
            public bool heightGizmo;
            public bool positionGizmo;
            public bool rotationGizmo;
        }
        
        static PhysBoneColliderInspectorPatcher()
        {
            try
            {
                
                // Clean up any existing harmony instance
                if (_harmony != null)
                {
                    _harmony.UnpatchAll(HarmonyId);
                    _harmony = null;
                }
                
                // Try immediate initialization
                Initialize();
                
                // Also delay initialization to ensure VRChat assemblies are loaded
                EditorApplication.delayCall += DelayedInitialize;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error in static constructor: {e}");
            }
        }
        
        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/🐟 Reinitialize Patches", false, 1500)]
        private static void ReinitializePatches()
        {
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log("[EnhancedDynamics] Manual reinitialization requested");
            }
            
            // Clean up existing patches
            if (_harmony != null)
            {
                _harmony.UnpatchAll(HarmonyId);
                _harmony = null;
            }
            
            // Reinitialize
            Initialize();
        }
        
        private static void DelayedInitialize()
        {
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log("[EnhancedDynamics] Delayed initialization starting...");
            }
            Initialize();
        }
        
        private static void Initialize()
        {
            try
            {
                // Check if already initialized
                if (_harmony != null)
                {
                    if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Already initialized, cleaning up first...");
                }
                    _harmony.UnpatchAll(HarmonyId);
                    SceneView.duringSceneGui -= OnSceneGUI;
                }
                
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Starting initialization...");
                }
                
                _harmony = new Harmony(HarmonyId);
                
                // Log all VRC assemblies (only in debug mode)
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Available VRC assemblies:");
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (assembly.FullName.Contains("VRC"))
                        {
                            Debug.Log($"[EnhancedDynamics]   - {assembly.FullName}");
                        }
                    }
                }
                
                // Patch the inspector
                PatchInspector();
                
                // We no longer need these patches since we're using the button approach
                // All the inline button attempts have been removed
                
                // Subscribe to scene GUI
                SceneView.duringSceneGui -= OnSceneGUI; // Remove first to avoid duplicates
                SceneView.duringSceneGui += OnSceneGUI;
                
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Initialization complete!");
                }
                
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error during initialization: {e}");
            }
        }
        
        private static void PatchInspector()
        {
            try
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Searching for VRC PhysBone inspectors...");
                }
                
                Type colliderInspectorType = null;
                Type physBoneInspectorType = null;
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
                                            if (EnhancedDynamicsSettings.DebugMode)
                                            {
                                                Debug.Log($"[EnhancedDynamics] Found VRCPhysBoneCollider editor: {type.FullName}");
                                            }
                                            colliderInspectorType = type;
                                        }
                                        else if (inspectedType == typeof(VRCPhysBone))
                                        {
                                            if (EnhancedDynamicsSettings.DebugMode)
                                            {
                                                Debug.Log($"[EnhancedDynamics] Found VRCPhysBone editor: {type.FullName}");
                                            }
                                            physBoneInspectorType = type;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
                
                // Patch collider inspector
                if (colliderInspectorType != null)
                {
                    var onInspectorGUI = colliderInspectorType.GetMethod("OnInspectorGUI", BindingFlags.Public | BindingFlags.Instance);
                    if (onInspectorGUI != null)
                    {
                        var prefix = typeof(PhysBoneColliderInspectorPatcher).GetMethod(nameof(OnInspectorGUI_Prefix),
                            BindingFlags.Static | BindingFlags.NonPublic);
                        var postfix = typeof(PhysBoneColliderInspectorPatcher).GetMethod(nameof(OnInspectorGUI_Postfix),
                            BindingFlags.Static | BindingFlags.NonPublic);
                        
                        _harmony.Patch(onInspectorGUI, 
                            prefix: new HarmonyMethod(prefix),
                            postfix: new HarmonyMethod(postfix));
                        
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log("[EnhancedDynamics] Successfully patched VRCPhysBoneCollider inspector!");
                        }
                    }
                }
                
                // Patch PhysBone inspector
                if (physBoneInspectorType != null)
                {
                    var onInspectorGUI = physBoneInspectorType.GetMethod("OnInspectorGUI", BindingFlags.Public | BindingFlags.Instance);
                    if (onInspectorGUI != null)
                    {
                        var prefix = typeof(PhysBoneColliderInspectorPatcher).GetMethod(nameof(PhysBone_OnInspectorGUI_Prefix),
                            BindingFlags.Static | BindingFlags.NonPublic);
                        var postfix = typeof(PhysBoneColliderInspectorPatcher).GetMethod(nameof(PhysBone_OnInspectorGUI_Postfix),
                            BindingFlags.Static | BindingFlags.NonPublic);
                        
                        _harmony.Patch(onInspectorGUI, 
                            prefix: new HarmonyMethod(prefix),
                            postfix: new HarmonyMethod(postfix));
                        
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log("[EnhancedDynamics] Successfully patched VRCPhysBone inspector!");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to patch inspector: {e}");
            }
        }
        
        
        
        
        private static void OnInspectorGUI_Prefix(UnityEditor.Editor __instance)
        {
            _currentCollider = __instance.target as VRCPhysBoneCollider;
            _isDrawingPhysBoneInspector = _currentCollider != null;
            
            if (!_isDrawingPhysBoneInspector || _currentCollider == null) return;
            
            // Add Preview Physics button at the top
            EditorGUILayout.Space(5);
            
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = PhysicsPreviewManager.IsPreviewActive ? new Color(0.5f, 1f, 0.5f) : Color.green;
            
            if (GUILayout.Button(PhysicsPreviewManager.IsPreviewActive ? KittynLocalization.Get("enhanced_dynamics.preview_physics_active") : KittynLocalization.Get("enhanced_dynamics.preview_physics"), 
                GUILayout.Height(30)))
            {
                if (PhysicsPreviewManager.IsPreviewActive)
                {
                    PhysicsPreviewManager.StopPreview();
                }
                else
                {
                    PhysicsPreviewManager.StartPreview();
                }
            }
            
            GUI.backgroundColor = originalColor;
            
            EditorGUILayout.Space(5);
        }
        
        private static VRCPhysBone _currentPhysBone;
        
        private static void PhysBone_OnInspectorGUI_Prefix(UnityEditor.Editor __instance)
        {
            _currentPhysBone = __instance.target as VRCPhysBone;
            
            if (_currentPhysBone == null)
            {
                return;
            }
            
            // Add Preview Physics button at the top
            EditorGUILayout.Space(5);
            
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = PhysicsPreviewManager.IsPreviewActive ? new Color(0.5f, 1f, 0.5f) : Color.green;
            
            if (GUILayout.Button(PhysicsPreviewManager.IsPreviewActive ? KittynLocalization.Get("enhanced_dynamics.preview_physics_active") : KittynLocalization.Get("enhanced_dynamics.preview_physics"), 
                GUILayout.Height(30)))
            {
                if (PhysicsPreviewManager.IsPreviewActive)
                {
                    PhysicsPreviewManager.StopPreview();
                }
                else
                {
                    PhysicsPreviewManager.StartPreview();
                }
            }
            
            GUI.backgroundColor = originalColor;
            
            EditorGUILayout.Space(5);
        }
        
        private static void PhysBone_OnInspectorGUI_Postfix(UnityEditor.Editor __instance)
        {
            _currentPhysBone = null;
        }
        
        private static void OnInspectorGUI_Postfix(UnityEditor.Editor __instance)
        {
            if (!_isDrawingPhysBoneInspector || _currentCollider == null)
            {
                _isDrawingPhysBoneInspector = false;
                _currentCollider = null;
                return;
            }
            
            // Get or create gizmo state
            var instanceId = _currentCollider.GetInstanceID();
            if (!_gizmoStates.ContainsKey(instanceId))
            {
                _gizmoStates[instanceId] = new GizmoState();
            }
            var state = _gizmoStates[instanceId];
            
            // Keep the fallback section at the bottom
            if (_currentCollider != null)
            {
                // Add a section for all gizmo buttons as fallback
                EditorGUILayout.Space(10);
                
                // Create a box for better visual separation
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(KittynLocalization.Get("enhanced_dynamics.viewport_gizmos"), EditorStyles.miniLabel);
                
                EditorGUILayout.BeginHorizontal();
                
                // Store original background color
                var originalColor = GUI.backgroundColor;
                
                // Radius button (not for Plane)
                if (_currentCollider.shapeType != VRCPhysBoneColliderBase.ShapeType.Plane)
                {
                    GUI.backgroundColor = state.radiusGizmo ? Color.green : Color.red;
                    if (GUILayout.Button(state.radiusGizmo ? KittynLocalization.Get("enhanced_dynamics.gizmo_radius_active") : KittynLocalization.Get("enhanced_dynamics.gizmo_radius_inactive"), EditorStyles.miniButton))
                    {
                        state.radiusGizmo = !state.radiusGizmo;
                        SceneView.RepaintAll();
                    }
                }
                
                // Height button (only for Capsule)
                if (_currentCollider.shapeType == VRCPhysBoneColliderBase.ShapeType.Capsule)
                {
                    GUI.backgroundColor = state.heightGizmo ? Color.green : Color.red;
                    if (GUILayout.Button(state.heightGizmo ? KittynLocalization.Get("enhanced_dynamics.gizmo_height_active") : KittynLocalization.Get("enhanced_dynamics.gizmo_height_inactive"), EditorStyles.miniButton))
                    {
                        state.heightGizmo = !state.heightGizmo;
                        SceneView.RepaintAll();
                    }
                }
                
                // Position button
                GUI.backgroundColor = state.positionGizmo ? Color.green : Color.red;
                if (GUILayout.Button(state.positionGizmo ? KittynLocalization.Get("enhanced_dynamics.gizmo_position_active") : KittynLocalization.Get("enhanced_dynamics.gizmo_position_inactive"), EditorStyles.miniButton))
                {
                    state.positionGizmo = !state.positionGizmo;
                    SceneView.RepaintAll();
                }
                
                // Rotation button
                GUI.backgroundColor = state.rotationGizmo ? Color.green : Color.red;
                if (GUILayout.Button(state.rotationGizmo ? KittynLocalization.Get("enhanced_dynamics.gizmo_rotation_active") : KittynLocalization.Get("enhanced_dynamics.gizmo_rotation_inactive"), EditorStyles.miniButton))
                {
                    state.rotationGizmo = !state.rotationGizmo;
                    SceneView.RepaintAll();
                }
                
                // Restore original background color
                GUI.backgroundColor = originalColor;
                
                EditorGUILayout.EndHorizontal();
            }
            
            _isDrawingPhysBoneInspector = false;
            _currentCollider = null;
        }
        
        
        private static void PrefixLabel_Postfix(object[] __args)
        {
            if (!_isDrawingPhysBoneInspector || _currentCollider == null)
                return;
            
            // Extract label from args
            string label = null;
            if (__args.Length > 0)
            {
                if (__args[0] is string str)
                    label = str;
                else if (__args[0] is GUIContent content)
                    label = content.text;
            }
            
            if (!string.IsNullOrEmpty(label))
            {
            }
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
            
            // Use rootTransform if available, otherwise use the collider's transform
            var transform = activeCollider.rootTransform != null ? activeCollider.rootTransform : activeCollider.transform;
            
            // Draw radius gizmo
            if (activeState.radiusGizmo && activeCollider.shapeType != VRCPhysBoneColliderBase.ShapeType.Plane)
            {
                EditorGUI.BeginChangeCheck();
                Handles.color = new Color(1f, 0.5f, 0f, 0.8f);
                
                var worldPos = transform.TransformPoint(activeCollider.position);
                var worldRotation = transform.rotation * activeCollider.rotation;
                
                var newRadius = Handles.RadiusHandle(
                    worldRotation,
                    worldPos,
                    activeCollider.radius
                );
                
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(activeCollider, KittynLocalization.Get("enhanced_dynamics.undo_change_physbone_radius"));
                    activeCollider.radius = newRadius;
                    EditorUtility.SetDirty(activeCollider);
                }
            }
            
            // Draw height gizmo for capsule
            if (activeState.heightGizmo && activeCollider.shapeType == VRCPhysBoneColliderBase.ShapeType.Capsule)
            {
                // Use yellow color for better contrast
                Handles.color = new Color(1f, 0.92f, 0.016f, 0.8f); // Yellow
                
                // Calculate capsule endpoints in world space
                var worldPos = transform.TransformPoint(activeCollider.position);
                var halfHeight = activeCollider.height * 0.5f;
                var upVector = transform.rotation * activeCollider.rotation * Vector3.up;
                var topPos = worldPos + upVector * halfHeight;
                var bottomPos = worldPos - upVector * halfHeight;
                
                // Draw top arrow handle (bigger size)
                EditorGUI.BeginChangeCheck();
                var newTopPos = Handles.Slider(topPos, upVector, HandleUtility.GetHandleSize(topPos) * 1.0f, Handles.ArrowHandleCap, 0.1f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(activeCollider, KittynLocalization.Get("enhanced_dynamics.undo_change_physbone_height"));
                    var delta = Vector3.Project(newTopPos - topPos, upVector);
                    activeCollider.height += delta.magnitude * 2f * (Vector3.Dot(delta, upVector) > 0 ? 1 : -1);
                    activeCollider.height = Mathf.Max(0.01f, activeCollider.height);
                    EditorUtility.SetDirty(activeCollider);
                }
                
                // Draw bottom arrow handle (bigger size)
                EditorGUI.BeginChangeCheck();
                var newBottomPos = Handles.Slider(bottomPos, -upVector, HandleUtility.GetHandleSize(bottomPos) * 1.0f, Handles.ArrowHandleCap, 0.1f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(activeCollider, KittynLocalization.Get("enhanced_dynamics.undo_change_physbone_height"));
                    var delta = Vector3.Project(newBottomPos - bottomPos, -upVector);
                    activeCollider.height += delta.magnitude * 2f * (Vector3.Dot(delta, -upVector) > 0 ? 1 : -1);
                    activeCollider.height = Mathf.Max(0.01f, activeCollider.height);
                    EditorUtility.SetDirty(activeCollider);
                }
                
                // Draw connecting line with increased thickness using AA poly line
                Handles.DrawAAPolyLine(3.0f, topPos, bottomPos);
            }
            
            // Draw position gizmo
            if (activeState.positionGizmo)
            {
                EditorGUI.BeginChangeCheck();
                
                var worldPos = transform.TransformPoint(activeCollider.position);
                var newWorldPos = Handles.PositionHandle(
                    worldPos,
                    transform.rotation * activeCollider.rotation
                );
                
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(activeCollider, KittynLocalization.Get("enhanced_dynamics.undo_move_physbone_collider"));
                    activeCollider.position = transform.InverseTransformPoint(newWorldPos);
                    EditorUtility.SetDirty(activeCollider);
                }
            }
            
            // Draw rotation gizmo
            if (activeState.rotationGizmo)
            {
                EditorGUI.BeginChangeCheck();
                
                var worldPos = transform.TransformPoint(activeCollider.position);
                var worldRotation = transform.rotation * activeCollider.rotation;
                var newWorldRotation = Handles.RotationHandle(
                    worldRotation,
                    worldPos
                );
                
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(activeCollider, KittynLocalization.Get("enhanced_dynamics.undo_rotate_physbone_collider"));
                    activeCollider.rotation = Quaternion.Inverse(transform.rotation) * newWorldRotation;
                    EditorUtility.SetDirty(activeCollider);
                }
            }
        }
    }
}