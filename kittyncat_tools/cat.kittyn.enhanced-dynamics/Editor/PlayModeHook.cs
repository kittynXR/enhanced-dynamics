using System;
using UnityEditor;
using UnityEngine;

namespace EnhancedDynamics.Editor
{
    /// <summary>
    /// Manages play mode state transitions for physics preview.
    /// Hooks into Unity's play mode state change events to intercept build callbacks.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeHook
    {
        private static bool _isPhysicsPreviewRequested = false;
        private static bool _wasIntercepting = false;
        private static bool _originalEnterPlayModeEnabled = false;
        private static EnterPlayModeOptions _originalEnterPlayModeOptions = EnterPlayModeOptions.None;
        private static bool _hasOriginalMaximizeOnPlay = false;
        private static bool _originalMaximizeOnPlay = false;
        private static bool _fastPreviewActive = false;
        private static int _focusGuardFrames = 0;
        
        static PlayModeHook()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }
        
        /// <summary>
        /// Request physics preview mode - this will intercept the next play mode entry
        /// </summary>
        public static void RequestPhysicsPreview()
        {
            Debug.Log("[EnhancedDynamics] Physics preview requested - will intercept next play mode");
            _isPhysicsPreviewRequested = true;
        }
        
        /// <summary>
        /// Store original play mode settings for restoration
        /// </summary>
        public static void StoreOriginalPlayModeSettings(bool originalEnabled, EnterPlayModeOptions originalOptions)
        {
            _originalEnterPlayModeEnabled = originalEnabled;
            _originalEnterPlayModeOptions = originalOptions;
        }
        
        /// <summary>
        /// Check if we're currently in physics preview mode
        /// </summary>
        public static bool IsInPhysicsPreview => _wasIntercepting && EditorApplication.isPlaying && AvatarHiding.IsHiding;
        
        /// <summary>
        /// In fast preview we don't hide avatars, still consider preview active.
        /// </summary>
        public static bool IsInAnyPreview => _wasIntercepting && EditorApplication.isPlaying;
        
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log($"[EnhancedDynamics] Play mode state changed: {state}");
            }
            
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    // About to enter play mode
                    if (_isPhysicsPreviewRequested)
                    {
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log("[EnhancedDynamics] Preparing physics preview with avatar hiding...");
                        }
                        if (EnhancedDynamicsSettings.FastPreview)
                        {
                            // Fast path: do not hide or clone; only prevent builders
                            ThirdPartyBuildPrevention.StartPreventing();
                            _fastPreviewActive = true;
                            _wasIntercepting = true;
                            _isPhysicsPreviewRequested = false;
                        }
                        else
                        {
                            // Safe path: Hide all avatars and create physics clone BEFORE disabling anything
                            if (!AvatarHiding.HideAvatarsAndCreatePhysicsClone())
                            {
                                Debug.LogError("[EnhancedDynamics] Failed to hide avatars and create physics clone, aborting preview");
                                _isPhysicsPreviewRequested = false;
                                return;
                            }
                            // Disable third-party play processors (fast path)
                            ThirdPartyBuildPrevention.StartPreventing();
                            _fastPreviewActive = false;
                            _wasIntercepting = true;
                            _isPhysicsPreviewRequested = false;
                        }
                        
                        // Restore settings after play mode entry
                        EditorApplication.delayCall += () =>
                        {
                            EditorSettings.enterPlayModeOptionsEnabled = _originalEnterPlayModeEnabled;
                            EditorSettings.enterPlayModeOptions = _originalEnterPlayModeOptions;
                        };
                    }
                    break;
                    
                case PlayModeStateChange.EnteredPlayMode:
                    // Now in play mode
                    if (_wasIntercepting)
                    {
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log("[EnhancedDynamics] Entered play mode with intercepted callbacks");
                        }
                        
                        // Show the floating UI in Scene view
                        PhysicsPreviewUI.ShowUI();
                        // Keep focus on Scene View and prevent Game View from maximizing
                        EditorApplication.delayCall += FocusSceneViewAndDisableMaximizeOnPlay;
                        ArmFocusGuard(10);
                        
                        // Physics should now be initialized by VRChat SDK
                        // The PhysicsPreviewManager can now work with the initialized physics
                    }
                    break;
                    
                case PlayModeStateChange.ExitingPlayMode:
                    // About to exit play mode
                    if (_wasIntercepting)
                    {
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log("[EnhancedDynamics] Exiting physics preview mode...");
                        }

                        // Hide the floating UI (no saving here)
                        PhysicsPreviewUI.HideUI();
                        ArmFocusGuard(6);
                    }
                    break;
                    
                case PlayModeStateChange.EnteredEditMode:
                    // Back in edit mode
                    if (_wasIntercepting)
                    {
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log("[EnhancedDynamics] Restoring avatars and build callbacks...");
                        }
                        
                        // 1. Restore hidden avatars (safe mode only)
                        if (!_fastPreviewActive)
                        {
                            AvatarHiding.RestoreAvatars();
                        }
                        
                        // 2. Restore third-party play processors
                        ThirdPartyBuildPrevention.StopPreventing();
                        
                        _wasIntercepting = false;
                        _fastPreviewActive = false;
                        
                        // 3. Force scene view refresh
                        SceneView.RepaintAll();
                        ArmFocusGuard(6);

                        // 4. Restore GameView maximizeOnPlay state if we changed it
                        RestoreMaximizeOnPlay();
                    }
                    break;
            }
        }

        private static void FocusSceneViewAndDisableMaximizeOnPlay()
        {
            try
            {
                // Attempt to disable Maximize On Play temporarily
                var editorAsm = typeof(EditorWindow).Assembly;
                var gameViewType = editorAsm.GetType("UnityEditor.GameView");
                if (gameViewType != null)
                {
                    var gameView = EditorWindow.GetWindow(gameViewType);
                    var maxProp = gameViewType.GetProperty("maximizeOnPlay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (gameView != null && maxProp != null)
                    {
                        var current = (bool)maxProp.GetValue(gameView);
                        _originalMaximizeOnPlay = current;
                        _hasOriginalMaximizeOnPlay = true;
                        if (current)
                        {
                            maxProp.SetValue(gameView, false);
                        }
                    }
                }

                // Focus Scene View so user stays in Scene tab
                var sceneView = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
                if (sceneView != null)
                {
                    sceneView.Focus();
                    sceneView.Repaint();
                }
            }
            catch (Exception e)
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.LogWarning($"[EnhancedDynamics] Could not adjust Game/Scene focus: {e}");
                }
            }
        }

        private static void RestoreMaximizeOnPlay()
        {
            try
            {
                if (!_hasOriginalMaximizeOnPlay) return;
                var editorAsm = typeof(EditorWindow).Assembly;
                var gameViewType = editorAsm.GetType("UnityEditor.GameView");
                if (gameViewType != null)
                {
                    var gameView = EditorWindow.GetWindow(gameViewType);
                    var maxProp = gameViewType.GetProperty("maximizeOnPlay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (gameView != null && maxProp != null)
                    {
                        maxProp.SetValue(gameView, _originalMaximizeOnPlay);
                    }
                }
            }
            catch (Exception e)
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.LogWarning($"[EnhancedDynamics] Could not restore Maximize On Play: {e}");
                }
            }
            finally
            {
                _hasOriginalMaximizeOnPlay = false;
            }
        }

        private static void ArmFocusGuard(int frames)
        {
            _focusGuardFrames = Mathf.Max(_focusGuardFrames, frames);
            EditorApplication.update -= FocusGuardUpdate;
            EditorApplication.update += FocusGuardUpdate;
        }

        private static void FocusGuardUpdate()
        {
            if (_focusGuardFrames <= 0)
            {
                EditorApplication.update -= FocusGuardUpdate;
                return;
            }
            _focusGuardFrames--;
            try
            {
                var sceneView = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
                if (sceneView != null)
                {
                    sceneView.Focus();
                    sceneView.Repaint();
                }
            }
            catch (Exception) { }
        }
    }
}
