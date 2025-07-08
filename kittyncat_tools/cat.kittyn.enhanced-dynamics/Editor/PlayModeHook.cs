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
                        
                        // 1. Hide all avatars and create physics clone BEFORE disabling anything
                        if (!AvatarHiding.HideAvatarsAndCreatePhysicsClone())
                        {
                            Debug.LogError("[EnhancedDynamics] Failed to hide avatars and create physics clone, aborting preview");
                            _isPhysicsPreviewRequested = false;
                            return;
                        }
                        
                        // 2. Disable build systems
                        BuildCallbackInterceptor.StartIntercepting();
                        ThirdPartyBuildPrevention.StartPreventing();
                        
                        _wasIntercepting = true;
                        _isPhysicsPreviewRequested = false;
                        
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
                        
                        // Show the floating UI
                        PhysicsPreviewUI.ShowUI();
                        
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
                        
                        // Hide the floating UI
                        PhysicsPreviewUI.HideUI();
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
                        
                        // 1. Restore hidden avatars
                        AvatarHiding.RestoreAvatars();
                        
                        // 2. Stop intercepting and restore all callbacks
                        BuildCallbackInterceptor.StopIntercepting();
                        ThirdPartyBuildPrevention.StopPreventing();
                        
                        _wasIntercepting = false;
                        
                        // 3. Force scene view refresh
                        SceneView.RepaintAll();
                    }
                    break;
            }
        }
    }
}