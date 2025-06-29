using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using HarmonyLib;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace EnhancedDynamics.Editor
{
    [InitializeOnLoad]
    public static class PhysBoneColliderEditorPatcher
    {
        private static Harmony _harmony;
        private const string HarmonyId = "com.enhanceddynamics.physbone.editor";
        
        static PhysBoneColliderEditorPatcher()
        {
            _harmony = new Harmony(HarmonyId);
            PatchEditor();
        }
        
        private static void PatchEditor()
        {
            try
            {
                var originalEditorType = GetVRCPhysBoneColliderEditorType();
                if (originalEditorType == null)
                {
                    Debug.LogWarning("[EnhancedDynamics] Could not find VRCPhysBoneColliderEditor type");
                    return;
                }
                
                var originalOnInspectorGUI = originalEditorType.GetMethod("OnInspectorGUI", 
                    BindingFlags.Public | BindingFlags.Instance);
                    
                if (originalOnInspectorGUI != null)
                {
                    var postfix = typeof(PhysBoneColliderEditorPatcher).GetMethod(nameof(OnInspectorGUI_Postfix), 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    _harmony.Patch(originalOnInspectorGUI, postfix: new HarmonyMethod(postfix));
                    Debug.Log("[EnhancedDynamics] Successfully patched VRCPhysBoneCollider inspector");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to patch editor: {e}");
            }
        }
        
        private static Type GetVRCPhysBoneColliderEditorType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName.Contains("VRC.SDK3.Dynamics.PhysBone"))
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.Name == "VRCPhysBoneColliderEditor" || 
                            type.Name == "VRC_PhysBoneColliderEditor" ||
                            (type.IsSubclassOf(typeof(UnityEditor.Editor)) && 
                             type.Name.Contains("PhysBoneCollider")))
                        {
                            return type;
                        }
                    }
                }
            }
            return null;
        }
        
        private static void OnInspectorGUI_Postfix(UnityEditor.Editor __instance)
        {
            var target = __instance.target as VRCPhysBoneCollider;
            if (target == null) return;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Enhanced Dynamics", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Radius field with inline buttons (not shown for Plane type)
            if (target.shapeType != VRCPhysBoneColliderBase.ShapeType.Plane)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Radius Tools", GUILayout.Width(100));
                
                if (GUILayout.Button("×0.5", GUILayout.Width(50)))
                {
                    Undo.RecordObject(target, "Scale PhysBone Radius");
                    target.radius *= 0.5f;
                    EditorUtility.SetDirty(target);
                }
                
                if (GUILayout.Button("×0.8", GUILayout.Width(50)))
                {
                    Undo.RecordObject(target, "Scale PhysBone Radius");
                    target.radius *= 0.8f;
                    EditorUtility.SetDirty(target);
                }
                
                if (GUILayout.Button("×1.2", GUILayout.Width(50)))
                {
                    Undo.RecordObject(target, "Scale PhysBone Radius");
                    target.radius *= 1.2f;
                    EditorUtility.SetDirty(target);
                }
                
                if (GUILayout.Button("×2", GUILayout.Width(50)))
                {
                    Undo.RecordObject(target, "Scale PhysBone Radius");
                    target.radius *= 2f;
                    EditorUtility.SetDirty(target);
                }
                
                if (GUILayout.Button("Reset", GUILayout.Width(50)))
                {
                    Undo.RecordObject(target, "Reset PhysBone Radius");
                    target.radius = 0.02f;
                    EditorUtility.SetDirty(target);
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            // Height field with inline buttons (only for Capsule type)
            if (target.shapeType == VRCPhysBoneColliderBase.ShapeType.Capsule)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Height Tools", GUILayout.Width(100));
                
                if (GUILayout.Button("×0.5", GUILayout.Width(50)))
                {
                    Undo.RecordObject(target, "Scale PhysBone Height");
                    target.height *= 0.5f;
                    EditorUtility.SetDirty(target);
                }
                
                if (GUILayout.Button("×0.8", GUILayout.Width(50)))
                {
                    Undo.RecordObject(target, "Scale PhysBone Height");
                    target.height *= 0.8f;
                    EditorUtility.SetDirty(target);
                }
                
                if (GUILayout.Button("×1.2", GUILayout.Width(50)))
                {
                    Undo.RecordObject(target, "Scale PhysBone Height");
                    target.height *= 1.2f;
                    EditorUtility.SetDirty(target);
                }
                
                if (GUILayout.Button("×2", GUILayout.Width(50)))
                {
                    Undo.RecordObject(target, "Scale PhysBone Height");
                    target.height *= 2f;
                    EditorUtility.SetDirty(target);
                }
                
                if (GUILayout.Button("Reset", GUILayout.Width(50)))
                {
                    Undo.RecordObject(target, "Reset PhysBone Height");
                    target.height = 0f;
                    EditorUtility.SetDirty(target);
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            // Position tools
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Position Tools", GUILayout.Width(100));
            
            if (GUILayout.Button("Center", GUILayout.Width(60)))
            {
                Undo.RecordObject(target, "Center PhysBone Position");
                target.position = Vector3.zero;
                EditorUtility.SetDirty(target);
            }
            
            if (GUILayout.Button("Snap 0.01", GUILayout.Width(70)))
            {
                Undo.RecordObject(target, "Snap PhysBone Position");
                target.position = new Vector3(
                    Mathf.Round(target.position.x * 100f) / 100f,
                    Mathf.Round(target.position.y * 100f) / 100f,
                    Mathf.Round(target.position.z * 100f) / 100f
                );
                EditorUtility.SetDirty(target);
            }
            
            if (GUILayout.Button("Flip X", GUILayout.Width(50)))
            {
                Undo.RecordObject(target, "Flip PhysBone X Position");
                target.position = new Vector3(-target.position.x, target.position.y, target.position.z);
                EditorUtility.SetDirty(target);
            }
            
            if (GUILayout.Button("Flip Y", GUILayout.Width(50)))
            {
                Undo.RecordObject(target, "Flip PhysBone Y Position");
                target.position = new Vector3(target.position.x, -target.position.y, target.position.z);
                EditorUtility.SetDirty(target);
            }
            
            if (GUILayout.Button("Flip Z", GUILayout.Width(50)))
            {
                Undo.RecordObject(target, "Flip PhysBone Z Position");
                target.position = new Vector3(target.position.x, target.position.y, -target.position.z);
                EditorUtility.SetDirty(target);
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Rotation tools
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Rotation Tools", GUILayout.Width(100));
            
            if (GUILayout.Button("Reset", GUILayout.Width(60)))
            {
                Undo.RecordObject(target, "Reset PhysBone Rotation");
                target.rotation = Quaternion.identity;
                EditorUtility.SetDirty(target);
            }
            
            if (GUILayout.Button("+90° X", GUILayout.Width(60)))
            {
                Undo.RecordObject(target, "Rotate PhysBone X");
                target.rotation *= Quaternion.Euler(90, 0, 0);
                EditorUtility.SetDirty(target);
            }
            
            if (GUILayout.Button("+90° Y", GUILayout.Width(60)))
            {
                Undo.RecordObject(target, "Rotate PhysBone Y");
                target.rotation *= Quaternion.Euler(0, 90, 0);
                EditorUtility.SetDirty(target);
            }
            
            if (GUILayout.Button("+90° Z", GUILayout.Width(60)))
            {
                Undo.RecordObject(target, "Rotate PhysBone Z");
                target.rotation *= Quaternion.Euler(0, 0, 90);
                EditorUtility.SetDirty(target);
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Batch operations
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Batch Operations", EditorStyles.miniBoldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Copy Values"))
            {
                CopiedColliderValues.CopyFrom(target);
            }
            
            GUI.enabled = CopiedColliderValues.HasCopiedValues;
            if (GUILayout.Button("Paste Values"))
            {
                Undo.RecordObject(target, "Paste PhysBone Values");
                CopiedColliderValues.PasteTo(target);
                EditorUtility.SetDirty(target);
            }
            
            if (GUILayout.Button("Paste to All Selected"))
            {
                foreach (var obj in Selection.gameObjects)
                {
                    var collider = obj.GetComponent<VRCPhysBoneCollider>();
                    if (collider != null && collider != target)
                    {
                        Undo.RecordObject(collider, "Paste PhysBone Values");
                        CopiedColliderValues.PasteTo(collider);
                        EditorUtility.SetDirty(collider);
                    }
                }
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
    }
    
    // Helper class to store copied values
    internal static class CopiedColliderValues
    {
        private static bool _hasCopiedValues;
        private static float _radius;
        private static float _height;
        private static Vector3 _position;
        private static Quaternion _rotation;
        private static VRCPhysBoneColliderBase.ShapeType _shapeType;
        
        public static bool HasCopiedValues => _hasCopiedValues;
        
        public static void CopyFrom(VRCPhysBoneCollider collider)
        {
            _radius = collider.radius;
            _height = collider.height;
            _position = collider.position;
            _rotation = collider.rotation;
            _shapeType = collider.shapeType;
            _hasCopiedValues = true;
            
            Debug.Log("[EnhancedDynamics] Copied PhysBone collider values");
        }
        
        public static void PasteTo(VRCPhysBoneCollider collider)
        {
            if (!_hasCopiedValues) return;
            
            collider.shapeType = _shapeType;
            collider.radius = _radius;
            collider.height = _height;
            collider.position = _position;
            collider.rotation = _rotation;
        }
    }
}