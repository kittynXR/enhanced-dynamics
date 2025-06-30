using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using VRC.SDK3.Dynamics.PhysBone.Components;
using EnhancedDynamics.Runtime.Physics;

namespace EnhancedDynamics.Editor
{
    public static class PhysicsPreviewManager
    {
        // Preview state
        private static bool _isPreviewActive = false;
        private static double _lastUpdateTime = 0;
        private const float PHYSICS_UPDATE_RATE = 1f / 60f; // 60 FPS simulation
        
        // Component tracking
        private static List<VRCPhysBone> _activePhysBones = new List<VRCPhysBone>();
        private static Dictionary<VRCPhysBone, PhysBoneState> _originalStates = new Dictionary<VRCPhysBone, PhysBoneState>();
        private static Dictionary<VRCPhysBone, PhysBoneState> _modifiedStates = new Dictionary<VRCPhysBone, PhysBoneState>();
        
        // Preview components
        private static List<PhysBonePreviewComponent> _previewComponents = new List<PhysBonePreviewComponent>();
        
        // Transform tracking for movement
        private static Transform _selectedTransform;
        private static Vector3 _originalPosition;
        private static Quaternion _originalRotation;
        
        // UI State
        private static bool _showOverlay = true;
        private static Rect _overlayRect = new Rect(10, 10, 300, 100);
        
        // Reflection cache
        private static MethodInfo _updateMethod;
        private static FieldInfo _isAnimatedField;
        private static bool _reflectionInitialized = false;
        
        private class PhysBoneState
        {
            public float pull;
            public float spring;
            public float stiffness;
            public float gravity;
            public float gravityFalloff;
            public float immobilize;
            public bool hasImmobilize;
            
            // Transform states
            public Dictionary<Transform, TransformState> boneTransforms = new Dictionary<Transform, TransformState>();
        }
        
        private class TransformState
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }
        
        public static bool IsPreviewActive => _isPreviewActive;
        
        public static void StartPreview()
        {
            if (_isPreviewActive) return;
            
            Debug.Log("[EnhancedDynamics] Starting Physics Preview Mode");
            
            // Find all PhysBones in the scene
            _activePhysBones = GameObject.FindObjectsOfType<VRCPhysBone>()
                .Where(pb => pb.enabled && pb.gameObject.activeInHierarchy)
                .ToList();
            
            if (_activePhysBones.Count == 0)
            {
                Debug.LogWarning("[EnhancedDynamics] No active PhysBones found in scene");
                return;
            }
            
            // Store original states
            StoreOriginalStates();
            
            // Add preview components to each PhysBone
            _previewComponents.Clear();
            foreach (var physBone in _activePhysBones)
            {
                try
                {
                    var previewComp = physBone.gameObject.AddComponent<PhysBonePreviewComponent>();
                    _previewComponents.Add(previewComp);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[EnhancedDynamics] Failed to add preview component to {physBone.name}: {e}");
                }
            }
            
            // Set up selected transform for movement
            if (Selection.activeTransform != null)
            {
                _selectedTransform = Selection.activeTransform;
                _originalPosition = _selectedTransform.position;
                _originalRotation = _selectedTransform.rotation;
            }
            
            // Start preview
            _isPreviewActive = true;
            _lastUpdateTime = EditorApplication.timeSinceStartup;
            
            // Subscribe to scene GUI only (components handle their own updates)
            SceneView.duringSceneGui += DrawSceneGUI;
            EditorApplication.update += ForceSceneRepaint;
            
            // Force scene view to repaint continuously
            SceneView.RepaintAll();
            
            Debug.Log($"[EnhancedDynamics] Physics Preview started with {_activePhysBones.Count} PhysBones");
        }
        
        public static void StopPreview(bool applyChanges = false)
        {
            if (!_isPreviewActive) return;
            
            Debug.Log($"[EnhancedDynamics] Stopping Physics Preview (Apply Changes: {applyChanges})");
            
            // Remove preview components
            foreach (var component in _previewComponents)
            {
                if (component != null)
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
            }
            _previewComponents.Clear();
            
            // Unsubscribe from events
            SceneView.duringSceneGui -= DrawSceneGUI;
            EditorApplication.update -= ForceSceneRepaint;
            
            if (applyChanges)
            {
                // Apply modified parameters
                ApplyModifiedStates();
            }
            else
            {
                // Restore original states
                RestoreOriginalStates();
            }
            
            // Clean up
            _activePhysBones.Clear();
            _originalStates.Clear();
            _modifiedStates.Clear();
            _selectedTransform = null;
            _isPreviewActive = false;
            
            SceneView.RepaintAll();
        }
        
        private static void InitializeReflection()
        {
            if (_reflectionInitialized) return;
            
            try
            {
                var physBoneType = typeof(VRCPhysBone);
                
                // Look for update method - might be named differently
                _updateMethod = physBoneType.GetMethod("UpdatePhysBone", BindingFlags.NonPublic | BindingFlags.Instance) ??
                               physBoneType.GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance) ??
                               physBoneType.GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
                
                // Look for isAnimated field
                _isAnimatedField = physBoneType.GetField("isAnimated", BindingFlags.NonPublic | BindingFlags.Instance) ??
                                  physBoneType.GetField("_isAnimated", BindingFlags.NonPublic | BindingFlags.Instance);
                
                _reflectionInitialized = true;
                
                if (_updateMethod == null)
                {
                    Debug.LogWarning("[EnhancedDynamics] Could not find PhysBone update method - simulation may not work correctly");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to initialize reflection: {e}");
            }
        }
        
        private static float GetImmobilize(VRCPhysBone physBone)
        {
            try
            {
                var prop = typeof(VRCPhysBone).GetProperty("immobilize", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    return (float)prop.GetValue(physBone);
                }
                
                var field = typeof(VRCPhysBone).GetField("immobilize", BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    return (float)field.GetValue(physBone);
                }
            }
            catch { }
            
            return 0f;
        }
        
        private static void SetImmobilize(VRCPhysBone physBone, float value)
        {
            try
            {
                var prop = typeof(VRCPhysBone).GetProperty("immobilize", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(physBone, value);
                    return;
                }
                
                var field = typeof(VRCPhysBone).GetField("immobilize", BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(physBone, value);
                }
            }
            catch { }
        }
        
        private static List<Transform> GetAffectedTransforms(VRCPhysBone physBone)
        {
            try
            {
                var method = typeof(VRCPhysBone).GetMethod("GetAffectedTransforms", BindingFlags.Public | BindingFlags.Instance);
                if (method != null)
                {
                    return method.Invoke(physBone, null) as List<Transform>;
                }
            }
            catch { }
            
            // Fallback: get transforms under the root
            var root = physBone.GetRootTransform();
            if (root != null)
            {
                var transforms = new List<Transform>();
                GetTransformsRecursive(root, transforms, 10); // Max depth
                return transforms;
            }
            
            return new List<Transform>();
        }
        
        private static void GetTransformsRecursive(Transform current, List<Transform> list, int maxDepth)
        {
            if (maxDepth <= 0) return;
            
            list.Add(current);
            foreach (Transform child in current)
            {
                GetTransformsRecursive(child, list, maxDepth - 1);
            }
        }
        
        private static bool HasImmobilize(VRCPhysBone physBone)
        {
            try
            {
                var prop = typeof(VRCPhysBone).GetProperty("immobilize", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null) return true;
                
                var field = typeof(VRCPhysBone).GetField("immobilize", BindingFlags.Public | BindingFlags.Instance);
                if (field != null) return true;
            }
            catch { }
            
            return false;
        }
        
        private static void StoreOriginalStates()
        {
            foreach (var physBone in _activePhysBones)
            {
                var state = new PhysBoneState
                {
                    pull = physBone.pull,
                    spring = physBone.spring,
                    stiffness = physBone.stiffness,
                    gravity = physBone.gravity,
                    gravityFalloff = physBone.gravityFalloff,
                    hasImmobilize = HasImmobilize(physBone)
                };
                
                if (state.hasImmobilize)
                {
                    state.immobilize = GetImmobilize(physBone);
                }
                
                // Store transform states for all affected bones
                var transforms = GetAffectedTransforms(physBone);
                if (transforms != null)
                {
                    foreach (var transform in transforms)
                    {
                        if (transform != null)
                        {
                            state.boneTransforms[transform] = new TransformState
                            {
                                position = transform.localPosition,
                                rotation = transform.localRotation,
                                scale = transform.localScale
                            };
                        }
                    }
                }
                
                _originalStates[physBone] = state;
                _modifiedStates[physBone] = state; // Start with original values
            }
        }
        
        private static void RestoreOriginalStates()
        {
            foreach (var kvp in _originalStates)
            {
                var physBone = kvp.Key;
                var state = kvp.Value;
                
                if (physBone == null) continue;
                
                // Restore parameters
                physBone.pull = state.pull;
                physBone.spring = state.spring;
                physBone.stiffness = state.stiffness;
                physBone.gravity = state.gravity;
                physBone.gravityFalloff = state.gravityFalloff;
                
                if (state.hasImmobilize)
                {
                    SetImmobilize(physBone, state.immobilize);
                }
                
                // Restore transforms
                foreach (var transformKvp in state.boneTransforms)
                {
                    var transform = transformKvp.Key;
                    var transformState = transformKvp.Value;
                    
                    if (transform != null)
                    {
                        transform.localPosition = transformState.position;
                        transform.localRotation = transformState.rotation;
                        transform.localScale = transformState.scale;
                    }
                }
            }
            
            // Restore selected transform
            if (_selectedTransform != null)
            {
                _selectedTransform.position = _originalPosition;
                _selectedTransform.rotation = _originalRotation;
            }
        }
        
        private static void ApplyModifiedStates()
        {
            foreach (var physBone in _activePhysBones)
            {
                if (_modifiedStates.ContainsKey(physBone))
                {
                    // Record undo
                    Undo.RecordObject(physBone, "Apply Physics Preview Changes");
                    
                    // Parameters are already modified during preview
                    // Just mark as dirty
                    EditorUtility.SetDirty(physBone);
                }
            }
        }
        
        
        private static void TrackParameterChanges(VRCPhysBone physBone)
        {
            if (!_modifiedStates.ContainsKey(physBone))
            {
                _modifiedStates[physBone] = new PhysBoneState();
            }
            
            var state = _modifiedStates[physBone];
            state.pull = physBone.pull;
            state.spring = physBone.spring;
            state.stiffness = physBone.stiffness;
            state.gravity = physBone.gravity;
            state.gravityFalloff = physBone.gravityFalloff;
            
            if (HasImmobilize(physBone))
            {
                state.hasImmobilize = true;
                state.immobilize = GetImmobilize(physBone);
            }
        }
        
        private static void ForceSceneRepaint()
        {
            if (_isPreviewActive && SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.Repaint();
            }
        }
        
        private static void DrawSceneGUI(SceneView sceneView)
        {
            if (!_isPreviewActive || !_showOverlay) return;
            
            Handles.BeginGUI();
            
            // Draw overlay background
            GUI.Box(_overlayRect, GUIContent.none, EditorStyles.helpBox);
            
            GUILayout.BeginArea(_overlayRect);
            GUILayout.BeginVertical();
            
            // Title
            GUILayout.Label("Physics Preview Mode", EditorStyles.boldLabel);
            GUILayout.Label($"Simulating {_activePhysBones.Count} PhysBones", EditorStyles.miniLabel);
            
            GUILayout.Space(5);
            
            // Control buttons
            GUILayout.BeginHorizontal();
            
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("Reset", GUILayout.Height(25)))
            {
                RestoreOriginalStates();
                // Components will reset themselves via OnDisable/OnEnable
            }
            
            GUI.backgroundColor = new Color(1f, 1f, 0.5f);
            if (GUILayout.Button("Stop", GUILayout.Height(25)))
            {
                StopPreview(false);
            }
            
            GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
            if (GUILayout.Button("Apply Changes", GUILayout.Height(25)))
            {
                StopPreview(true);
            }
            
            GUI.backgroundColor = Color.white;
            
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndArea();
            
            Handles.EndGUI();
            
            // Draw transform handle for selected object
            if (_selectedTransform != null && Tools.current == Tool.Move)
            {
                EditorGUI.BeginChangeCheck();
                var newPosition = Handles.PositionHandle(_selectedTransform.position, _selectedTransform.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_selectedTransform, "Move in Physics Preview");
                    _selectedTransform.position = newPosition;
                }
            }
        }
    }
}