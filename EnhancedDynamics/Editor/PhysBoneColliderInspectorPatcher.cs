using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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
        // Force reinitialization on next compile
        private const int INIT_VERSION = 2;
        private static Harmony _harmony;
        private const string HarmonyId = "com.enhanceddynamics.physbone.inspector";
        
        // Gizmo state management
        private static Dictionary<int, GizmoState> _gizmoStates = new Dictionary<int, GizmoState>();
        private static VRCPhysBoneCollider _currentCollider;
        private static bool _isDrawingPhysBoneInspector = false;
        private static string _lastPropertyName = "";
        
        // GUI content
        private static GUIStyle _miniButtonStyle;
        private static Rect _lastPropertyRect;
        private static bool _shouldDrawButtonAfterProperty;
        private static string _pendingButtonProperty;
        private static Dictionary<string, Rect> _lastFieldRects = new Dictionary<string, Rect>();
        private static int _lastControlID;
        
        
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
                Debug.Log("[EnhancedDynamics] Static constructor called - Initializing PhysBoneColliderInspectorPatcher...");
                
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
        
        [MenuItem("Tools/Enhanced Dynamics/Reinitialize Patches")]
        private static void ReinitializePatches()
        {
            Debug.Log("[EnhancedDynamics] Manual reinitialization requested");
            
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
            Debug.Log("[EnhancedDynamics] Delayed initialization starting...");
            Initialize();
        }
        
        private static void Initialize()
        {
            try
            {
                // Check if already initialized
                if (_harmony != null)
                {
                    Debug.Log("[EnhancedDynamics] Already initialized, cleaning up first...");
                    _harmony.UnpatchAll(HarmonyId);
                    SceneView.duringSceneGui -= OnSceneGUI;
                }
                
                Debug.Log("[EnhancedDynamics] Starting initialization...");
                
                _harmony = new Harmony(HarmonyId);
                
                // Log all VRC assemblies
                Debug.Log("[EnhancedDynamics] Available VRC assemblies:");
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.FullName.Contains("VRC"))
                    {
                        Debug.Log($"[EnhancedDynamics]   - {assembly.FullName}");
                    }
                }
                
                // Patch the inspector
                PatchInspector();
                
                // Patch PropertyField with correct signature
                PatchPropertyField();
                
                // Patch the rotation field method
                PatchRotationField();
                
                // Try patching EditorGUI float/vector fields directly
                PatchEditorGUIFields();
                
                // Patch property wrapper methods
                PatchPropertyWrappers();
                
                // Try patching label methods
                PatchLabelMethods();
                
                // Try to find VRC's internal drawing methods
                FindAndPatchVRCInternalMethods();
                
                // Patch GUILayout rect methods
                PatchGUILayoutRectMethods();
                
                // Subscribe to scene GUI
                SceneView.duringSceneGui -= OnSceneGUI; // Remove first to avoid duplicates
                SceneView.duringSceneGui += OnSceneGUI;
                
                Debug.Log("[EnhancedDynamics] Initialization complete!");
                
                // Log all patched methods
                Debug.Log("[EnhancedDynamics] Successfully patched methods:");
                var patchedMethods = Harmony.GetAllPatchedMethods();
                foreach (var method in patchedMethods)
                {
                    var patchInfo = Harmony.GetPatchInfo(method);
                    if (patchInfo != null && patchInfo.Owners.Contains(HarmonyId))
                    {
                        Debug.Log($"[EnhancedDynamics]   - {method.DeclaringType?.FullName}.{method.Name}");
                    }
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
                
                // Patch FloatField (both Layout and non-Layout versions)
                var floatFieldMethods = typeof(EditorGUILayout).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "FloatField").ToList();
                
                foreach (var method in floatFieldMethods)
                {
                    try
                    {
                        var postfix = typeof(PhysBoneColliderInspectorPatcher).GetMethod(nameof(FloatField_Postfix),
                            BindingFlags.Static | BindingFlags.NonPublic);
                        _harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                        Debug.Log($"[EnhancedDynamics] Patched EditorGUILayout.FloatField with {method.GetParameters().Length} parameters");
                    }
                    catch { }
                }
                
                // Also patch non-Layout FloatField
                var editorGUIFloatFields = typeof(EditorGUI).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "FloatField").ToList();
                
                foreach (var method in editorGUIFloatFields)
                {
                    try
                    {
                        var postfix = typeof(PhysBoneColliderInspectorPatcher).GetMethod(nameof(EditorGUI_FloatField_Postfix),
                            BindingFlags.Static | BindingFlags.NonPublic);
                        _harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                        Debug.Log($"[EnhancedDynamics] Patched EditorGUI.FloatField with {method.GetParameters().Length} parameters");
                    }
                    catch { }
                }
                
                // Patch Vector3Field (both versions)
                var vector3FieldMethods = typeof(EditorGUILayout).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "Vector3Field").ToList();
                
                foreach (var method in vector3FieldMethods)
                {
                    try
                    {
                        var postfix = typeof(PhysBoneColliderInspectorPatcher).GetMethod(nameof(Vector3Field_Postfix),
                            BindingFlags.Static | BindingFlags.NonPublic);
                        _harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                        Debug.Log($"[EnhancedDynamics] Patched EditorGUILayout.Vector3Field with {method.GetParameters().Length} parameters");
                    }
                    catch { }
                }
                
                var editorGUIVector3Fields = typeof(EditorGUI).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "Vector3Field").ToList();
                
                foreach (var method in editorGUIVector3Fields)
                {
                    try
                    {
                        var postfix = typeof(PhysBoneColliderInspectorPatcher).GetMethod(nameof(EditorGUI_Vector3Field_Postfix),
                            BindingFlags.Static | BindingFlags.NonPublic);
                        _harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                        Debug.Log($"[EnhancedDynamics] Patched EditorGUI.Vector3Field with {method.GetParameters().Length} parameters");
                    }
                    catch { }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to patch EditorGUI fields: {e}");
            }
        }
        
        private static void PatchPropertyWrappers()
        {
            try
            {
                Debug.Log("[EnhancedDynamics] Patching property wrapper methods...");
                
                // Patch BeginProperty
                var beginPropertyMethod = typeof(EditorGUI).GetMethod("BeginProperty",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(Rect), typeof(GUIContent), typeof(SerializedProperty) },
                    null);
                
                if (beginPropertyMethod != null)
                {
                    var postfix = typeof(PhysBoneColliderInspectorPatcher).GetMethod(nameof(BeginProperty_Postfix),
                        BindingFlags.Static | BindingFlags.NonPublic);
                    _harmony.Patch(beginPropertyMethod, postfix: new HarmonyMethod(postfix));
                    Debug.Log("[EnhancedDynamics] Patched EditorGUI.BeginProperty");
                }
                
                // Patch EndProperty
                var endPropertyMethod = typeof(EditorGUI).GetMethod("EndProperty",
                    BindingFlags.Public | BindingFlags.Static);
                
                if (endPropertyMethod != null)
                {
                    var postfix = typeof(PhysBoneColliderInspectorPatcher).GetMethod(nameof(EndProperty_Postfix),
                        BindingFlags.Static | BindingFlags.NonPublic);
                    _harmony.Patch(endPropertyMethod, postfix: new HarmonyMethod(postfix));
                    Debug.Log("[EnhancedDynamics] Patched EditorGUI.EndProperty");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to patch property wrappers: {e}");
            }
        }
        
        private static void PatchLabelMethods()
        {
            try
            {
                Debug.Log("[EnhancedDynamics] Patching label methods...");
                
                // Patch PrefixLabel
                var prefixLabelMethods = typeof(EditorGUILayout).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "PrefixLabel").ToList();
                
                foreach (var method in prefixLabelMethods)
                {
                    try
                    {
                        var postfix = typeof(PhysBoneColliderInspectorPatcher).GetMethod(nameof(PrefixLabel_Postfix),
                            BindingFlags.Static | BindingFlags.NonPublic);
                        _harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                        Debug.Log($"[EnhancedDynamics] Patched PrefixLabel with {method.GetParameters().Length} parameters");
                    }
                    catch { }
                }
                
                // Also patch LabelField
                var labelFieldMethods = typeof(EditorGUILayout).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "LabelField").ToList();
                
                foreach (var method in labelFieldMethods)
                {
                    try
                    {
                        var postfix = typeof(PhysBoneColliderInspectorPatcher).GetMethod(nameof(LabelField_Postfix),
                            BindingFlags.Static | BindingFlags.NonPublic);
                        _harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                        Debug.Log($"[EnhancedDynamics] Patched LabelField with {method.GetParameters().Length} parameters");
                    }
                    catch { }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to patch label methods: {e}");
            }
        }
        
        private static void FindAndPatchVRCInternalMethods()
        {
            try
            {
                Debug.Log("[EnhancedDynamics] Searching for VRC internal drawing methods...");
                
                // Find the VRCPhysBoneColliderEditor type
                Type vrcEditorType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.FullName.Contains("VRC.SDK3.Dynamics.PhysBone.Editor"))
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.Name == "VRCPhysBoneColliderEditor")
                            {
                                vrcEditorType = type;
                                break;
                            }
                        }
                    }
                }
                
                if (vrcEditorType != null)
                {
                    Debug.Log($"[EnhancedDynamics] Found VRCPhysBoneColliderEditor type: {vrcEditorType.FullName}");
                    
                    // Get ALL methods, including private ones
                    var allMethods = vrcEditorType.GetMethods(
                        BindingFlags.Public | BindingFlags.NonPublic | 
                        BindingFlags.Instance | BindingFlags.Static | 
                        BindingFlags.DeclaredOnly);
                    
                    Debug.Log($"[EnhancedDynamics] Found {allMethods.Length} methods in VRCPhysBoneColliderEditor:");
                    
                    foreach (var method in allMethods)
                    {
                        Debug.Log($"[EnhancedDynamics]   - {method.Name} ({(method.IsPrivate ? "private" : "public")}, {(method.IsStatic ? "static" : "instance")})");
                        
                        // Try to patch any method that might be drawing our fields
                        if (method.Name.ToLower().Contains("draw") || 
                            method.Name.ToLower().Contains("radius") || 
                            method.Name.ToLower().Contains("height") || 
                            method.Name.ToLower().Contains("position") ||
                            method.Name.ToLower().Contains("shape") ||
                            method.Name.ToLower().Contains("field"))
                        {
                            try
                            {
                                var transpiler = typeof(PhysBoneColliderInspectorPatcher).GetMethod(
                                    nameof(GenericTranspiler),
                                    BindingFlags.Static | BindingFlags.NonPublic);
                                
                                _harmony.Patch(method, transpiler: new HarmonyMethod(transpiler));
                                Debug.Log($"[EnhancedDynamics] Successfully patched: {method.Name}");
                            }
                            catch (Exception e)
                            {
                                Debug.Log($"[EnhancedDynamics] Could not patch {method.Name}: {e.Message}");
                            }
                        }
                    }
                    
                    // Also check the base class methods
                    var baseType = vrcEditorType.BaseType;
                    if (baseType != null)
                    {
                        Debug.Log($"[EnhancedDynamics] Base type: {baseType.FullName}");
                    }
                }
                
                // Also check InspectorUtil for any field drawing methods
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
                    var utilMethods = inspectorUtilType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                    Debug.Log($"[EnhancedDynamics] InspectorUtil methods:");
                    foreach (var method in utilMethods)
                    {
                        Debug.Log($"[EnhancedDynamics]   - {method.Name}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error in FindAndPatchVRCInternalMethods: {e}");
            }
        }
        
        private static IEnumerable<CodeInstruction> GenericTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        {
            Debug.Log($"[EnhancedDynamics] Transpiling method: {method.Name}");
            return instructions;
        }
        
        private static void PatchGUILayoutRectMethods()
        {
            try
            {
                Debug.Log("[EnhancedDynamics] Patching GUILayout rect methods...");
                
                // Patch GetRect method
                var getRectMethod = typeof(GUILayoutUtility).GetMethod("GetRect",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(float), typeof(float), typeof(GUILayoutOption[]) },
                    null);
                
                if (getRectMethod != null)
                {
                    var postfix = typeof(PhysBoneColliderInspectorPatcher).GetMethod(nameof(GetRect_Postfix),
                        BindingFlags.Static | BindingFlags.NonPublic);
                    _harmony.Patch(getRectMethod, postfix: new HarmonyMethod(postfix));
                    Debug.Log("[EnhancedDynamics] Patched GUILayoutUtility.GetRect");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to patch GUILayout rect methods: {e}");
            }
        }
        
        private static SerializedProperty _lastFoundProperty;
        private static SerializedProperty _currentProperty;
        private static string _currentPropertyLabel;
        private static string _lastLabel;
        
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
        
        private static void BeginProperty_Postfix(Rect position, GUIContent label, SerializedProperty property)
        {
            if (_isDrawingPhysBoneInspector && property != null)
            {
                _currentProperty = property;
                _currentPropertyLabel = label?.text;
                Debug.Log($"[EnhancedDynamics] BeginProperty: {property.name} (Label: {label?.text})");
            }
        }
        
        private static void EndProperty_Postfix()
        {
            // Disabled - we use the buttons at the bottom instead
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
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Viewport Gizmos", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                
                // Radius button (not for Plane)
                if (_currentCollider.shapeType != VRCPhysBoneColliderBase.ShapeType.Plane)
                {
                    GUI.backgroundColor = state.radiusGizmo ? Color.green : Color.red;
                    if (GUILayout.Button("Radius (R)", GUILayout.ExpandWidth(true)))
                    {
                        state.radiusGizmo = !state.radiusGizmo;
                        SceneView.RepaintAll();
                    }
                    GUILayout.FlexibleSpace();
                }
                
                // Height button (only for Capsule)
                if (_currentCollider.shapeType == VRCPhysBoneColliderBase.ShapeType.Capsule)
                {
                    GUI.backgroundColor = state.heightGizmo ? Color.green : Color.red;
                    if (GUILayout.Button("Height (H)", GUILayout.ExpandWidth(true)))
                    {
                        state.heightGizmo = !state.heightGizmo;
                        SceneView.RepaintAll();
                    }
                    GUILayout.FlexibleSpace();
                }
                
                // Position button
                GUI.backgroundColor = state.positionGizmo ? Color.green : Color.red;
                if (GUILayout.Button("Position (P)", GUILayout.ExpandWidth(true)))
                {
                    state.positionGizmo = !state.positionGizmo;
                    SceneView.RepaintAll();
                }
                GUILayout.FlexibleSpace();
                
                // Rotation button
                GUI.backgroundColor = state.rotationGizmo ? Color.green : Color.red;
                if (GUILayout.Button("Rotation (↻)", GUILayout.ExpandWidth(true)))
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
            
            // Disabled - we use the buttons at the bottom instead
        }
        
        private static void QuaternionAsEulerField_Postfix(SerializedProperty property)
        {
            // Disabled - we use the buttons at the bottom instead
        }
        
        private static void GenericField_Postfix(object __instance, MethodBase __originalMethod, object[] __args)
        {
            // Disabled - we use the buttons at the bottom instead
        }
        
        private static void InspectorUtil_Field_Postfix(object[] __args, MethodBase __originalMethod)
        {
            // Disabled - we use the buttons at the bottom instead
        }
        
        private static void FloatField_Postfix(float __result, object[] __args)
        {
            // Disabled - we use the buttons at the bottom instead
        }
        
        private static void Vector3Field_Postfix(Vector3 __result, object[] __args)
        {
            // Disabled - we use the buttons at the bottom instead
        }
        
        private static void EditorGUI_FloatField_Postfix(float __result, object[] __args)
        {
            // Disabled - we use the buttons at the bottom instead
        }
        
        private static void EditorGUI_Vector3Field_Postfix(Vector3 __result, object[] __args)
        {
            // Disabled - we use the buttons at the bottom instead
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
                _lastLabel = label;
                Debug.Log($"[EnhancedDynamics] PrefixLabel: {label}");
            }
        }
        
        private static void LabelField_Postfix(object[] __args)
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
                Debug.Log($"[EnhancedDynamics] LabelField: {label}");
                
                // Check if this is one of our target labels
                if (label == "Radius" || label == "Height" || label == "Position" || label == "Rotation")
                {
                    _pendingButtonProperty = label.ToLower();
                    _shouldDrawButtonAfterProperty = true;
                }
            }
        }
        
        private static void GetRect_Postfix(Rect __result, float width, float height)
        {
            if (!_isDrawingPhysBoneInspector || _currentCollider == null)
                return;
            
            // Track the last rect created
            _lastPropertyRect = __result;
            
            // If we have a pending property, check if this rect might be for it
            if (!string.IsNullOrEmpty(_lastLabel))
            {
                Debug.Log($"[EnhancedDynamics] GetRect called after label: {_lastLabel}, rect: {__result}");
                
                if (_lastLabel == "Radius" || _lastLabel == "Height" || _lastLabel == "Position" || _lastLabel == "Rotation")
                {
                    _lastFieldRects[_lastLabel.ToLower()] = __result;
                }
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
                Handles.color = new Color(0f, 1f, 0.5f, 0.8f);
                
                // Calculate capsule endpoints
                var halfHeight = activeCollider.height * 0.5f;
                var upVector = activeCollider.rotation * Vector3.up;
                var topPos = activeCollider.position + upVector * halfHeight;
                var bottomPos = activeCollider.position - upVector * halfHeight;
                
                // Draw top arrow handle
                EditorGUI.BeginChangeCheck();
                var newTopPos = Handles.Slider(topPos, upVector, HandleUtility.GetHandleSize(topPos) * 0.5f, Handles.ArrowHandleCap, 0.1f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(activeCollider, "Change PhysBone Height");
                    var localTop = Quaternion.Inverse(activeCollider.rotation) * (newTopPos - activeCollider.position);
                    activeCollider.height = Mathf.Max(0.01f, localTop.y * 2f);
                    EditorUtility.SetDirty(activeCollider);
                }
                
                // Draw bottom arrow handle
                EditorGUI.BeginChangeCheck();
                var newBottomPos = Handles.Slider(bottomPos, -upVector, HandleUtility.GetHandleSize(bottomPos) * 0.5f, Handles.ArrowHandleCap, 0.1f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(activeCollider, "Change PhysBone Height");
                    var localBottom = Quaternion.Inverse(activeCollider.rotation) * (activeCollider.position - newBottomPos);
                    activeCollider.height = Mathf.Max(0.01f, localBottom.y * 2f);
                    EditorUtility.SetDirty(activeCollider);
                }
                
                // Draw connecting line
                Handles.DrawLine(topPos, bottomPos);
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