using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace EnhancedDynamics.Editor
{
    /// <summary>
    /// Draws bones in SceneView during physics preview and allows simple selection/handles.
    /// </summary>
    [InitializeOnLoad]
    public static class BoneOverlay
    {
        private static GameObject _currentAvatarRoot;
        private static readonly List<Transform> _bones = new List<Transform>();
        private static readonly Dictionary<Transform, List<Transform>> _childMap = new Dictionary<Transform, List<Transform>>();
        private static readonly HashSet<Transform> _physBoneChain = new HashSet<Transform>();
        private static readonly HashSet<Transform> _physBoneRoots = new HashSet<Transform>();
        private static readonly System.Collections.Generic.Dictionary<Transform, System.Collections.Generic.List<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone>> _rootToPhysBones = new System.Collections.Generic.Dictionary<Transform, System.Collections.Generic.List<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone>>();
        private static int _lastBoneCount = 0;

        static BoneOverlay()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            try
            {
                if (!EnhancedDynamicsSettings.ShowBones) return;
                if (!PlayModeHook.IsInAnyPreview) return;

                var root = GetActiveAvatarRoot();
                if (root == null) return;

                if (root != _currentAvatarRoot)
                {
                    BuildBoneCache(root);
                    _currentAvatarRoot = root;
                }

                DrawBones();
                DrawSelectedBoneHighlight();
            }
            catch (Exception e)
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.LogWarning($"[EnhancedDynamics] Bone overlay error: {e}");
                }
            }
        }

        private static void BuildBoneCache(GameObject root)
        {
            _bones.Clear();
            _childMap.Clear();
            _physBoneChain.Clear();
            _physBoneRoots.Clear();
            _rootToPhysBones.Clear();
            _lastBoneCount = 0;

            // Prefer bones referenced by skinned meshes for non-human rigs
            var sms = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var boneSet = new HashSet<Transform>();
            foreach (var smr in sms)
            {
                foreach (var b in smr.bones)
                {
                    if (b != null) boneSet.Add(b);
                }
                if (smr.rootBone != null) boneSet.Add(smr.rootBone);
            }

            if (boneSet.Count == 0)
            {
                // Fallback: walk entire hierarchy but limit count
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    // Skip non-bone helper objects (heuristic)
                    if (t == root.transform) continue;
                    boneSet.Add(t);
                    if (boneSet.Count > 1024) break; // cap to keep perf reasonable
                }
            }

            _bones.AddRange(boneSet);
            foreach (var t in _bones)
            {
                if (!_childMap.ContainsKey(t)) _childMap[t] = new List<Transform>();
            }
            foreach (var t in _bones)
            {
                if (t.parent != null && boneSet.Contains(t.parent))
                {
                    _childMap[t.parent].Add(t);
                }
            }

            // Build PhysBone chain and root maps
            try
            {
                var physBones = root.GetComponentsInChildren<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone>(true);
                foreach (var pb in physBones)
                {
                    var chainRoot = pb.rootTransform != null ? pb.rootTransform : pb.transform;
                    if (chainRoot == null) continue;
                    _physBoneRoots.Add(chainRoot);
                    if (!_rootToPhysBones.TryGetValue(chainRoot, out var list))
                    {
                        list = new System.Collections.Generic.List<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone>();
                        _rootToPhysBones[chainRoot] = list;
                    }
                    list.Add(pb);
                    foreach (var t in chainRoot.GetComponentsInChildren<Transform>(true))
                    {
                        _physBoneChain.Add(t);
                    }
                }
            }
            catch (Exception e)
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.LogWarning($"[EnhancedDynamics] Could not build PhysBone chain map: {e}");
                }
            }

            _lastBoneCount = _bones.Count;
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log($"[EnhancedDynamics] Bone overlay cached {_lastBoneCount} bones for {root.name}");
            }
        }

        private static void DrawBones()
        {
            if (_bones.Count == 0) return;

            Handles.color = new Color(0.2f, 0.9f, 0.9f, 0.7f);
            foreach (var t in _bones)
            {
                if (t == null) continue;
                if (_childMap.TryGetValue(t, out var children))
                {
                    foreach (var c in children)
                    {
                        if (c == null) continue;
                        Handles.DrawAAPolyLine(2.0f, new Vector3[] { t.position, c.position });
                    }
                }

                // Highlighting/selectability:
                //  - PhysBone roots: blue, larger, selectable
                //  - PhysBone chain (non-roots): gray, non-selectable
                //  - Others: green, selectable
                bool isPhysBoneRoot = _physBoneRoots.Contains(t);
                bool inPhysBoneChain = _physBoneChain.Contains(t);
                float baseSize = HandleUtility.GetHandleSize(t.position) * 0.10f;
                float size = isPhysBoneRoot ? baseSize * 2.0f : baseSize;
                var prev = Handles.color;

                if (isPhysBoneRoot)
                {
                    Handles.color = new Color(0.2f, 0.6f, 1.0f, 0.95f);
                    if (Handles.Button(t.position, Quaternion.identity, size, size, Handles.SphereHandleCap))
                    {
                        Selection.activeTransform = t;
                        if (_rootToPhysBones.TryGetValue(t, out var pbs) && pbs.Count > 0)
                        {
                            EditorGUIUtility.PingObject(pbs[0]);
                        }
                    }
                    var labelStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = new Color(0.4f, 0.75f, 1f, 0.95f) } };
                    Handles.Label(t.position + Vector3.up * (size * 0.6f), "PB", labelStyle);
                }
                else if (inPhysBoneChain)
                {
                    // Gray non-selectable node for PB chain segments
                    Handles.color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
                    Handles.SphereHandleCap(0, t.position, Quaternion.identity, size, EventType.Repaint);
                }
                else
                {
                    // Regular selectable bone
                    Handles.color = new Color(0.1f, 1.0f, 0.3f, 0.95f);
                    if (Handles.Button(t.position, Quaternion.identity, size, size, Handles.SphereHandleCap))
                    {
                        Selection.activeTransform = t;
                    }
                }
                Handles.color = prev;
            }
        }

        private static void DrawSelectedBoneHighlight()
        {
            var selected = Selection.activeTransform;
            if (selected == null) return;
            if (!_bones.Contains(selected)) return;

            // Only draw a subtle ring around the selected bone; rely on Unity's default gizmo for manipulation
            var prev = Handles.color;
            Handles.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            float ringSize = HandleUtility.GetHandleSize(selected.position) * 0.12f;
            Handles.DrawWireDisc(selected.position, SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera.transform.forward : Vector3.forward, ringSize);
            Handles.color = prev;
        }

        private static GameObject GetActiveAvatarRoot()
        {
            // Prefer physics clone when available
            if (AvatarHiding.PhysicsClone != null)
            {
                return AvatarHiding.PhysicsClone;
            }

            // Try the avatar of current selection
            if (Selection.activeGameObject != null)
            {
                var root = FindAvatarRoot(Selection.activeGameObject);
                if (root != null) return root;
            }

            // Fallback: first avatar descriptor in scene
            var avatars = GameObject.FindObjectsOfType<VRCAvatarDescriptor>();
            if (avatars != null && avatars.Length > 0) return avatars[0].gameObject;
            return null;
        }

        private static GameObject FindAvatarRoot(GameObject obj)
        {
            var t = obj.transform;
            while (t != null)
            {
                var desc = t.GetComponent<VRCAvatarDescriptor>();
                if (desc != null) return t.gameObject;
                t = t.parent;
            }
            return null;
        }
    }
}
