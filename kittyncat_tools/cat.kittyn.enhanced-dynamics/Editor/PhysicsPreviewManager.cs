using System;
using UnityEngine;
using UnityEditor;

namespace EnhancedDynamics.Editor
{
    public static class PhysicsPreviewManager
    {
        // Menu items for testing
        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/🐟 Enter Physics Preview", false, 1501)]
        public static void StartPhysicsPreview()
        {
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log("[EnhancedDynamics] Menu: Enter Physics Preview clicked");
            }
            StartPreview();
        }
        
        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/🐟 Exit Physics Preview", false, 1502)]
        public static void StopPhysicsPreview()
        {
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log("[EnhancedDynamics] Menu: Exit Physics Preview clicked");
            }
            StopPreview();
        }
        
        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/🐟 Test Debug Output", false, 1503)]
        public static void TestDebugOutput()
        {
            Debug.Log("[EnhancedDynamics] === TEST DEBUG OUTPUT ===");
            Debug.Log($"[EnhancedDynamics] System is working! Time: {System.DateTime.Now}");
            Debug.LogWarning("[EnhancedDynamics] Warning test message");
            Debug.LogError("[EnhancedDynamics] Error test message");
        }
        
        public static bool IsPreviewActive => PlayModeHook.IsInAnyPreview;

        // Quick toggle with F7
        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/🐟 Toggle Physics Preview _F7", false, 1499)]
        public static void TogglePhysicsPreview()
        {
            if (IsPreviewActive)
            {
                StopPreview();
            }
            else
            {
                StartPreview();
            }
        }
        
        public static void StartPreview()
        {
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log("[EnhancedDynamics] === StartPreview() called ===");
            }
            
            if (IsPreviewActive)
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Preview already active, returning");
                }
                return;
            }
            
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[EnhancedDynamics] Cannot start physics preview while already in play mode");
                return;
            }
            
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log("[EnhancedDynamics] Starting VRChat Physics Preview with quality of life features");
            }
            
            try
            {
                // Validate we have a physics component selected or in the scene
                if (!ValidatePhysicsPreviewRequirements())
                {
                    return;
                }
                
                // Set fast play mode options BEFORE entering play mode
                var originalEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
                var originalEnterPlayModeEnabled = EditorSettings.enterPlayModeOptionsEnabled;
                
                EditorSettings.enterPlayModeOptionsEnabled = true;
                EditorSettings.enterPlayModeOptions = 
                    EnterPlayModeOptions.DisableDomainReload | 
                    EnterPlayModeOptions.DisableSceneReload;
                
                // Store original settings for later restoration
                PlayModeHook.StoreOriginalPlayModeSettings(originalEnterPlayModeEnabled, originalEnterPlayModeOptions);
                
                // Request physics preview mode - avatar hiding will happen in ExitingEditMode
                PlayModeHook.RequestPhysicsPreview();
                
                // Enter play mode - VRChat SDK will initialize physics naturally
                EditorApplication.EnterPlaymode();
                
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Entering play mode for physics preview...");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error starting physics preview: {e}");
                
                // Cleanup if something went wrong
                try
                {
                    AvatarHiding.RestoreAvatars();
                    BuildCallbackInterceptor.StopIntercepting();
                    ThirdPartyBuildPrevention.StopPreventing();
                }
                catch (Exception cleanupException)
                {
                    Debug.LogError($"[EnhancedDynamics] Error during cleanup: {cleanupException}");
                }
            }
        }
        
        public static void StopPreview()
        {
            if (!IsPreviewActive)
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Preview not active, returning");
                }
                return;
            }
            
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log("[EnhancedDynamics] Stopping VRChat Physics Preview");
            }
            
            try
            {
                // Exit play mode - this will trigger callback restoration
                EditorApplication.ExitPlaymode();
                
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Exiting play mode...");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error stopping physics preview: {e}");
            }
        }
        
        
        private static bool ValidatePhysicsPreviewRequirements()
        {
            try
            {
                // Check if we have any VRC avatars in the scene
                var avatars = GameObject.FindObjectsOfType<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
                if (avatars.Length == 0)
                {
                    EditorUtility.DisplayDialog(
                        "No VRC Avatars Found",
                        "Physics preview requires at least one VRCAvatarDescriptor in the scene.",
                        "OK"
                    );
                    return false;
                }
                
                // Check if we have physics components
                bool hasPhysics = false;
                foreach (var avatar in avatars)
                {
                    if (avatar.gameObject.GetComponentsInChildren<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone>(true).Length > 0 ||
                        avatar.gameObject.GetComponentsInChildren<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneCollider>(true).Length > 0)
                    {
                        hasPhysics = true;
                        break;
                    }
                }
                
                if (!hasPhysics)
                {
                    var result = EditorUtility.DisplayDialogComplex(
                        "No Physics Components Found",
                        "No VRCPhysBone or VRCPhysBoneCollider components found on any avatars in the scene. " +
                        "Physics preview is most useful when you have physics components to test.\n\n" +
                        "Do you want to continue anyway?",
                        "Continue",
                        "Cancel",
                        "Learn More"
                    );
                    
                    if (result == 1) // Cancel
                    {
                        return false;
                    }
                    else if (result == 2) // Learn More
                    {
                        Application.OpenURL("https://docs.vrchat.com/docs/physbones");
                        return false;
                    }
                }
                
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error validating physics preview requirements: {e}");
                return false;
            }
        }
    }
}
