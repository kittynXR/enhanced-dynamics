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
            if (_isDrawingPhysBoneInspector && _currentProperty != null)
            {
                Debug.Log($"[EnhancedDynamics] EndProperty: {_currentProperty.name}");
                
                // Check if we just finished drawing one of our target properties
                if (_currentProperty.name == "radius" || _currentProperty.name == "height" || 
                    _currentProperty.name == "position" || _currentProperty.name == "rotation")
                {
                    DrawInlineButton(_currentProperty.name);
                }
                
                _currentProperty = null;
                _currentPropertyLabel = null;
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
            
            if (_isDrawingPhysBoneInspector)
            {
                Debug.Log("[EnhancedDynamics] Starting to draw PhysBone inspector");
                
                // Try to access the serialized object
                var serializedObjectField = __instance.GetType().GetProperty("serializedObject", BindingFlags.Public | BindingFlags.Instance);
                if (serializedObjectField != null)
                {
                    var serializedObject = serializedObjectField.GetValue(__instance) as SerializedObject;
                    if (serializedObject != null)
                    {
                        // Log the properties we're interested in
                        var radiusProp = serializedObject.FindProperty("radius");
                        var heightProp = serializedObject.FindProperty("height");
                        var positionProp = serializedObject.FindProperty("position");
                        var rotationProp = serializedObject.FindProperty("rotation");
                        
                        Debug.Log($"[EnhancedDynamics] Found properties - radius: {radiusProp != null}, height: {heightProp != null}, position: {positionProp != null}, rotation: {rotationProp != null}");
                    }
                }
            }
        }
        
        private static void OnInspectorGUI_Postfix(UnityEditor.Editor __instance)
        {
            // Check if we have a pending button to draw
            if (_shouldDrawButtonAfterProperty && !string.IsNullOrEmpty(_pendingButtonProperty))
            {
                DrawInlineButton(_pendingButtonProperty);
                _shouldDrawButtonAfterProperty = false;
                _pendingButtonProperty = null;
            }
            
            // Try to detect fields by monitoring the last rect
            if (_isDrawingPhysBoneInspector && Event.current.type == EventType.Repaint)
            {
                var lastRect = GUILayoutUtility.GetLastRect();
                if (lastRect.height > 0 && lastRect.height < 25) // Typical field height
                {
                    // Check if we just drew a field by looking at the current event
                    var currentControl = GUIUtility.GetControlID(FocusType.Passive);
                    if (currentControl != _lastControlID)
                    {
                        _lastControlID = currentControl;
                        Debug.Log($"[EnhancedDynamics] Control drawn: ID={currentControl}, Rect={lastRect}");
                    }
                }
            }
            
            // Keep the fallback section at the bottom
            var collider = __instance.target as VRCPhysBoneCollider;
            if (collider != null)
            {
                var instanceId = collider.GetInstanceID();
                if (!_gizmoStates.ContainsKey(instanceId))
                {
                    _gizmoStates[instanceId] = new GizmoState();
                }
                var state = _gizmoStates[instanceId];
                
                // Add a section for all gizmo buttons as fallback
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
        
        private static void EditorGUI_FloatField_Postfix(float __result, object[] __args)
        {
            if (!_isDrawingPhysBoneInspector || _currentCollider == null)
                return;
            
            // EditorGUI.FloatField has different parameter order - Rect is first
            string label = null;
            GUIContent content = null;
            
            // Try to find label in args (skip Rect which is usually first)
            for (int i = 1; i < __args.Length; i++)
            {
                if (__args[i] is string str)
                {
                    label = str;
                    break;
                }
                else if (__args[i] is GUIContent gc)
                {
                    content = gc;
                    label = gc.text;
                    break;
                }
            }
            
            Debug.Log($"[EnhancedDynamics] EditorGUI.FloatField_Postfix - Label: {label ?? "null"}, Args: {__args.Length}");
            
            if (label == "Radius" || label == "Height")
            {
                DrawInlineButton(label.ToLower());
            }
        }
        
        private static void EditorGUI_Vector3Field_Postfix(Vector3 __result, object[] __args)
        {
            if (!_isDrawingPhysBoneInspector || _currentCollider == null)
                return;
            
            // Similar to FloatField
            string label = null;
            for (int i = 1; i < __args.Length; i++)
            {
                if (__args[i] is string str)
                {
                    label = str;
                    break;
                }
                else if (__args[i] is GUIContent content)
                {
                    label = content.text;
                    break;
                }
            }
            
            Debug.Log($"[EnhancedDynamics] EditorGUI.Vector3Field_Postfix - Label: {label ?? "null"}");
            
            if (label == "Position")
            {
                DrawInlineButton("position");
            }
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