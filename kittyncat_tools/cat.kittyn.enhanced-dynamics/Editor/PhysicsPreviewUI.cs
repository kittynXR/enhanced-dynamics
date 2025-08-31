using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace EnhancedDynamics.Editor
{
    /// <summary>
    /// Provides floating viewport UI for physics preview control.
    /// Shows during physics preview with exit and save controls.
    /// </summary>
    [InitializeOnLoad]
    public static class PhysicsPreviewUI
    {
        private static bool _isUIVisible = false;
        private static Rect _windowRect = new Rect(20, 20, 280, 315);
        private static GUIStyle _windowStyle = null;
        private static GUIStyle _buttonStyle = null;
        private static GUIStyle _headerStyle = null;
        private static bool _stylesInitialized = false;
        private static List<string> _cachedChangesSummary = new List<string>();
        private static bool _changesSummaryDirty = true;
        private static bool _isResizing = false;
        private static Vector2 _resizeStartMouse;
        private static Rect _resizeStartRect;
        private const float RESIZE_HANDLE_SIZE = 16f;
        private const float MIN_WINDOW_W = 280f;
        private const float MIN_WINDOW_H = 240f;
        private static bool _capturingHotkey = false;
        private static bool _draggingHeader = false;
        private static Vector2 _dragStartMouse;
        private static Rect _dragStartRect;
        
        // Performance optimization: Frame rate throttling
        private static int _frameCounter = 0;
        private static bool _windowPositionCached = false;
        private const int UI_UPDATE_INTERVAL = 3; // Update every 3 frames for smooth performance
        
        // UI positioning options
        private static bool _moveToLeft = false;
        
        static PhysicsPreviewUI()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }
        
        /// <summary>
        /// Show the floating UI panel
        /// </summary>
        public static void ShowUI()
        {
            if (_isUIVisible)
            {
                Debug.LogWarning("[EnhancedDynamics] UI already visible");
                return;
            }
            
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log("[EnhancedDynamics] Showing physics preview UI");
            }
            _isUIVisible = true;
            _changesSummaryDirty = true; // Mark for update
            _windowPositionCached = false; // Reset position cache
            _frameCounter = 0; // Reset frame counter
            
            // Reset avatar gizmo cache
            AvatarGizmoHandler.ResetCache();
            
            // Check if we have pending changes from a previous session
            if (PhysicsChangeMemory.HasPendingChanges)
            {
                _cachedChangesSummary.Clear();
                _cachedChangesSummary.Add("⚠ Pending changes from previous session");
            }
            
            // Position the window in middle-right by default (avoiding perspective gizmo)
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                var sceneViewPosition = sceneView.position;
                if (_moveToLeft)
                {
                    _windowRect.x = 10;
                }
                else
                {
                    _windowRect.x = sceneViewPosition.width - _windowRect.width - 10;
                }
                _windowRect.y = sceneViewPosition.height / 2 - _windowRect.height / 2;
                _windowPositionCached = true;
            }
            
            // Single repaint instead of RepaintAll
            if (sceneView != null)
            {
                sceneView.Repaint();
            }
        }
        
        /// <summary>
        /// Hide the floating UI panel
        /// </summary>
        public static void HideUI()
        {
            if (!_isUIVisible)
            {
                return;
            }
            
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log("[EnhancedDynamics] Hiding physics preview UI");
            }
            _isUIVisible = false;
            
            // Single repaint instead of RepaintAll
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.Repaint();
            }
        }
        
        private static void OnSceneGUI(SceneView sceneView)
        {
            // Shortcut callback support: perform deferred drop if requested
            if (AvatarGizmoHandler.ConsumeDropUnderMouseRequest() && PlayModeHook.IsInAnyPreview)
            {
                AvatarGizmoHandler.DropAnchorUnderMouse(sceneView, Event.current.mousePosition);
            }

            // Global hotkey: Drop gizmo under mouse (configurable) & capture custom hotkey
            var ev = Event.current;
            if (_capturingHotkey && ev.type == EventType.KeyDown)
            {
                EnhancedDynamicsSettings.DropGizmoKey = ev.keyCode;
                _capturingHotkey = false;
                sceneView.ShowNotification(new GUIContent($"Drop hotkey set to {ev.keyCode}"), 1.5);
                ev.Use();
            }
            if (PlayModeHook.IsInAnyPreview && ev.type == EventType.KeyDown && ev.keyCode == EnhancedDynamicsSettings.DropGizmoKey)
            {
                AvatarGizmoHandler.DropAnchorUnderMouse(sceneView, ev.mousePosition);
                ev.Use();
            }

            if (!_isUIVisible || !PlayModeHook.IsInAnyPreview)
            {
                return;
            }
            
            // Performance optimization: Frame rate throttling
            _frameCounter++;
            bool shouldUpdate = _frameCounter % UI_UPDATE_INTERVAL == 0;
            
            // Frame throttling for performance
            
            // Initialize styles if needed (only once)
            if (!_stylesInitialized)
            {
                InitializeStyles();
            }
            
            // Update cached changes summary only when needed
            if (_changesSummaryDirty && shouldUpdate)
            {
                UpdateCachedChangesSummary();
                _changesSummaryDirty = false;
            }
            
            // Draw avatar gizmo BEFORE GUI operations (handles need to be drawn outside GUI context)
            try
            {
                AvatarGizmoHandler.DrawAvatarGizmo();
            }
            catch (Exception e)
            {
                // Catch physics system conflicts and other errors during gizmo drawing
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.LogError($"[EnhancedDynamics] Error drawing avatar gizmo: {e}");
                }
                // Don't spam the console with gizmo errors in non-debug mode
            }
            
            // Begin GUI for overlay
            Handles.BeginGUI();
            
            try
            {
                // Draw as a grouped panel and handle dragging manually for reliability
                GUI.BeginGroup(_windowRect, GUIContent.none, _windowStyle);
                DrawPhysicsPreviewWindow(0);
                GUI.EndGroup();
                
                // Cache window position calculations to reduce overhead
                if (!_windowPositionCached || shouldUpdate)
                {
                    var sceneRect = sceneView.position;
                    _windowRect.x = Mathf.Clamp(_windowRect.x, 0, sceneRect.width - _windowRect.width);
                    _windowRect.y = Mathf.Clamp(_windowRect.y, 0, sceneRect.height - _windowRect.height);
                    _windowPositionCached = true;
                }

                // Draw resize handle (bottom-right)
                var resizeRect = new Rect(_windowRect.xMax - RESIZE_HANDLE_SIZE, _windowRect.yMax - RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE);
                EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeUpLeft);
                GUI.Box(resizeRect, "");

                var e = Event.current;
                if (e.type == EventType.MouseDown && resizeRect.Contains(e.mousePosition))
                {
                    _isResizing = true;
                    _resizeStartMouse = e.mousePosition;
                    _resizeStartRect = _windowRect;
                    e.Use();
                }
                if (e.type == EventType.MouseDrag && _isResizing)
                {
                    var delta = e.mousePosition - _resizeStartMouse;
                    _windowRect.width = Mathf.Max(MIN_WINDOW_W, _resizeStartRect.width + delta.x);
                    _windowRect.height = Mathf.Max(MIN_WINDOW_H, _resizeStartRect.height + delta.y);
                    e.Use();
                }
                if (e.type == EventType.MouseUp && _isResizing)
                {
                    _isResizing = false;
                    e.Use();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error drawing physics preview UI: {e}");
            }
            finally
            {
                Handles.EndGUI();
            }
        }
        
        private static void DrawPhysicsPreviewWindow(int windowID)
        {
            GUILayout.BeginVertical();
            
            // Header drag bar area (immediate mode, absolute in window space)
            var headerRect = new Rect(0, 0, _windowRect.width, 22);
            GUI.Label(headerRect, "Physics Preview", _headerStyle);
            var gripColor = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.25f) : new Color(0f, 0f, 0f, 0.35f);
            float gx = headerRect.width - 22f;
            EditorGUI.DrawRect(new Rect(gx, 7f, 14f, 1f), gripColor);
            EditorGUI.DrawRect(new Rect(gx, 11f, 14f, 1f), gripColor);
            EditorGUIUtility.AddCursorRect(headerRect, MouseCursor.MoveArrow);
            // Manual drag
            var e = Event.current;
            if (e.type == EventType.MouseDown && headerRect.Contains(e.mousePosition))
            {
                _draggingHeader = true;
                _dragStartMouse = GUIUtility.GUIToScreenPoint(e.mousePosition);
                _dragStartRect = _windowRect;
                e.Use();
            }
            if (e.type == EventType.MouseDrag && _draggingHeader)
            {
                var currentScreen = GUIUtility.GUIToScreenPoint(e.mousePosition);
                var delta = currentScreen - _dragStartMouse;
                _windowRect.x = _dragStartRect.x + delta.x;
                _windowRect.y = _dragStartRect.y + delta.y;
                e.Use();
            }
            if (e.type == EventType.MouseUp && _draggingHeader)
            {
                _draggingHeader = false;
                e.Use();
            }
            // Layout area for content
            GUILayout.BeginArea(new Rect(0, 22, _windowRect.width, _windowRect.height - 22));
            GUILayout.BeginVertical();

            GUILayout.Label("Physics Preview Active", _headerStyle);
            GUILayout.Space(10);
            DrawChangesSummary();
            GUILayout.Space(10);

            var showBones = EnhancedDynamicsSettings.ShowBones;
            var newShowBones = GUILayout.Toggle(showBones, "Show Bones");
            if (newShowBones != showBones)
            {
                EnhancedDynamicsSettings.ShowBones = newShowBones;
                SceneView.RepaintAll();
            }

            // Build prevention toggles
            GUILayout.Space(6);
            var preventVrcf = EnhancedDynamicsSettings.PreventVRCFuryInPreview;
            var newPreventVrcf = GUILayout.Toggle(preventVrcf, "Prevent VRCFury builds in preview");
            if (newPreventVrcf != preventVrcf)
            {
                EnhancedDynamicsSettings.PreventVRCFuryInPreview = newPreventVrcf;
            }
            var preventMa = EnhancedDynamicsSettings.PreventModularAvatarInPreview;
            var newPreventMa = GUILayout.Toggle(preventMa, "Prevent MA builds in preview");
            if (newPreventMa != preventMa)
            {
                EnhancedDynamicsSettings.PreventModularAvatarInPreview = newPreventMa;
            }

            GUILayout.Space(6);
            if (GUILayout.Button("Re-center Gizmo", _buttonStyle, GUILayout.Height(18)))
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv != null) AvatarGizmoHandler.RecenterToCamera(sv);
            }
            if (GUILayout.Button($"Drop Gizmo Under Mouse ({EnhancedDynamicsSettings.DropGizmoKey})", _buttonStyle, GUILayout.Height(18)))
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv != null) AvatarGizmoHandler.DropAnchorUnderMouse(sv, Event.current.mousePosition);
            }
            if (GUILayout.Button(_capturingHotkey ? "Press any key…" : "Set Drop Hotkey…", _buttonStyle, GUILayout.Height(18)))
            {
                _capturingHotkey = !_capturingHotkey;
            }

            GUILayout.Label($"Hotkey: {EnhancedDynamicsSettings.DropGizmoKey} — Drop gizmo under mouse (Edit > Shortcuts)", EditorStyles.miniLabel);

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Size:", GUILayout.Width(32));
            if (GUILayout.Button("M", EditorStyles.miniButton, GUILayout.Width(24))) { _windowRect.width = 280; _windowRect.height = 315; }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            DrawControlButtons();

            GUILayout.EndVertical();
            GUILayout.EndArea();
            
            // No GUI.DragWindow() — we handle dragging manually above
        }
        
        private static void UpdateCachedChangesSummary()
        {
            // Simple status message for on-demand saving
            if (_cachedChangesSummary.Count == 0)
            {
                _cachedChangesSummary.Add("Ready to save changes on-demand");
            }
            // No need to clear and recreate the same message every time
        }
        
        private static void DrawChangesSummary()
        {
            // Performance optimization: Simplified static display
            GUILayout.Label("Status:", EditorStyles.boldLabel);
            
            // Show single static message without expensive scrollview
            if (_cachedChangesSummary.Count > 0)
            {
                GUILayout.Label(_cachedChangesSummary[0], EditorStyles.miniLabel);
            }
            else
            {
                GUILayout.Label("Ready to save changes on-demand", EditorStyles.miniLabel);
            }
        }
        
        private static void DrawControlButtons()
        {
            var originalColor = GUI.backgroundColor;
            GUILayout.BeginVertical();
            GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
            if (GUILayout.Button("Save Changes", _buttonStyle, GUILayout.Height(18))) { SaveChanges(); }
            GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
            if (GUILayout.Button("Save + Exit", _buttonStyle, GUILayout.Height(18))) { SaveChanges(); ExitPreview(); }
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
            if (GUILayout.Button("Exit Preview", _buttonStyle, GUILayout.Height(18))) { ExitPreview(); }
            GUI.backgroundColor = originalColor;
            GUILayout.EndVertical();
            
            // Show simplified note
            GUILayout.Label("Use inspector to modify physics components", EditorStyles.miniLabel);
        }
        
        private static void SaveChanges()
        {
            try
            {
                Debug.Log("[EnhancedDynamics] SaveChanges button clicked!");
                Debug.Log("[EnhancedDynamics] Saving physics preview changes to memory...");
                
                // Use memory-based save system
                var originalAvatar = AvatarHiding.OriginalAvatarForClone;
                var physicsClone = AvatarHiding.PhysicsClone;
                
                if (originalAvatar == null || physicsClone == null)
                {
                    if (EnhancedDynamicsSettings.FastPreview)
                    {
                        // In fast preview we don't clone—saving is not supported (would require separate snapshot path)
                        var sv = SceneView.lastActiveSceneView;
                        if (sv != null)
                        {
                            sv.ShowNotification(new GUIContent("Save not available in Fast Preview"), 2.0);
                        }
                        Debug.LogWarning("[EnhancedDynamics] Save not available in Fast Scene Preview (no clone). Use Safe Preview.");
                        return;
                    }
                    else
                    {
                        Debug.LogError("[EnhancedDynamics] Missing avatar references for save");
                        Debug.LogError($"  Original Avatar: {originalAvatar}");
                        Debug.LogError($"  Physics Clone: {physicsClone}");
                        var sceneView = SceneView.lastActiveSceneView;
                        if (sceneView != null)
                        {
                            sceneView.ShowNotification(new GUIContent("Error: Missing avatar references"), 2.0);
                        }
                        return;
                    }
                }
                
                Debug.Log($"[EnhancedDynamics] Original Avatar: {originalAvatar.name}");
                Debug.Log($"[EnhancedDynamics] Physics Clone: {physicsClone.name}");
                
                bool hasChanges = PhysicsChangeMemory.CaptureChangesToMemory(originalAvatar, physicsClone);
                
                // Show feedback
                var sceneView2 = SceneView.lastActiveSceneView;
                if (sceneView2 != null)
                {
                    if (hasChanges)
                    {
                        sceneView2.ShowNotification(new GUIContent("Changes saved! Will apply on exit."), 2.0);
                    }
                    else
                    {
                        sceneView2.ShowNotification(new GUIContent("No changes to save"), 1.5);
                    }
                }
                
                // Update the status message
                if (hasChanges)
                {
                    _cachedChangesSummary.Clear();
                    _cachedChangesSummary.Add("Changes saved - pending application");
                }
                
                Debug.Log($"[EnhancedDynamics] Save to memory completed - Had changes: {hasChanges}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error saving changes: {e}");
                
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                {
                    sceneView.ShowNotification(new GUIContent("Error saving changes!"), 3.0);
                }
                
                // Show error in status
                _cachedChangesSummary.Clear();
                _cachedChangesSummary.Add("Error during save operation");
            }
        }
        
        private static void ExitPreview()
        {
            try
            {
                Debug.Log("[EnhancedDynamics] Exiting physics preview from UI...");
                
                PhysicsPreviewManager.StopPreview();
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error exiting preview: {e}");
            }
        }
        
        private static void InitializeStyles()
        {
            if (_stylesInitialized) return;
            
            try
            {
                // Window style: use native skin for a true Unity look
                _windowStyle = new GUIStyle(GUI.skin.window)
                {
                    padding = new RectOffset(8, 8, 8, 8)
                };
                
                // Button style
                _buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                
                // Header style
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Color.white }
                };
                
                _stylesInitialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error initializing UI styles: {e}");
                
                // Fallback to default styles
                _windowStyle = GUI.skin.window;
                _buttonStyle = GUI.skin.button;
                _headerStyle = EditorStyles.boldLabel;
                _stylesInitialized = true;
            }
        }
        
        private static Texture2D MakeTexture(int width, int height, Color color)
        {
            var texture = new Texture2D(width, height);
            var pixels = new Color[width * height];
            
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
        
        
        /// <summary>
        /// Check if UI is currently visible
        /// </summary>
        public static bool IsUIVisible => _isUIVisible;
        
        /// <summary>
        /// Update window position if needed
        /// </summary>
        public static void UpdateWindowPosition()
        {
            if (!_isUIVisible) return;
            
            // Mark for update instead of immediate repaint
            _changesSummaryDirty = true;
        }
        
        /// <summary>
        /// Mark changes summary as dirty to trigger refresh
        /// </summary>
        public static void MarkChangesSummaryDirty()
        {
            _changesSummaryDirty = true;
        }
    }
}
