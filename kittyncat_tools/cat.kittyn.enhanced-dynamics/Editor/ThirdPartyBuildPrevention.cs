using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace EnhancedDynamics.Editor
{
    /// <summary>
    /// Prevents third-party build tools from running during physics preview.
    /// This is a more direct approach that targets specific tools.
    /// </summary>
    public static class ThirdPartyBuildPrevention
    {
        private static bool _isPreventingBuilds = false;
        
        // Storage for original values
        private static object _vrcFuryOriginalState = null;
        private static object _modularAvatarOriginalState = null;
        private static object _ndmfApplyOnPlayState = null;
        private static object _ndmfGlobalActivatorState = null;
        
        public static void StartPreventing()
        {
            if (_isPreventingBuilds)
            {
                Debug.Log("[EnhancedDynamics] Already preventing third-party builds");
                return;
            }
            
            Debug.Log("[EnhancedDynamics] Starting third-party build prevention...");
            
            DisableNDMFApplyOnPlay();
            DisableVRCFury();
            DisableModularAvatar();
            
            _isPreventingBuilds = true;
        }
        
        public static void StopPreventing()
        {
            if (!_isPreventingBuilds)
            {
                return;
            }
            
            Debug.Log("[EnhancedDynamics] Stopping third-party build prevention...");
            
            RestoreNDMFApplyOnPlay();
            RestoreVRCFury();
            RestoreModularAvatar();
            
            _isPreventingBuilds = false;
        }
        
        private static void DisableVRCFury()
        {
            try
            {
                // Look for VRCFury main class and PlayModeTrigger
                Type vrcFuryType = null;
                Type playModeTriggerType = null;
                
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name.Contains("VRCFury"))
                    {
                        Debug.Log($"[EnhancedDynamics] Found VRCFury assembly: {assembly.GetName().Name}");
                        
                        // Look for the main build processor and PlayModeTrigger
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.Name.Contains("VRCFuryBuilder") || type.Name.Contains("VRCFuryProcessor"))
                            {
                                vrcFuryType = type;
                                Debug.Log($"[EnhancedDynamics] Found VRCFury type: {type.FullName}");
                            }
                            else if (type.Name == "PlayModeTrigger" && type.Namespace.Contains("VF"))
                            {
                                playModeTriggerType = type;
                                Debug.Log($"[EnhancedDynamics] Found VRCFury PlayModeTrigger: {type.FullName}");
                            }
                        }
                    }
                }
                
                // Disable PlayModeTrigger
                if (playModeTriggerType != null)
                {
                    var enabledField = playModeTriggerType.GetField("enabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    var disabledField = playModeTriggerType.GetField("disabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    var initField = playModeTriggerType.GetField("init", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    
                    if (enabledField != null)
                    {
                        enabledField.SetValue(null, false);
                        Debug.Log("[EnhancedDynamics] Disabled VRCFury PlayModeTrigger via 'enabled' field");
                    }
                    else if (disabledField != null)
                    {
                        disabledField.SetValue(null, true);
                        Debug.Log("[EnhancedDynamics] Disabled VRCFury PlayModeTrigger via 'disabled' field");
                    }
                    else if (initField != null)
                    {
                        initField.SetValue(null, false);
                        Debug.Log("[EnhancedDynamics] Disabled VRCFury PlayModeTrigger via 'init' field");
                    }
                }
                
                if (vrcFuryType != null)
                {
                    // Try to disable it via static field or property
                    var disableField = vrcFuryType.GetField("disabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    var enabledField = vrcFuryType.GetField("enabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    var skipField = vrcFuryType.GetField("skip", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    
                    if (disableField != null)
                    {
                        _vrcFuryOriginalState = disableField.GetValue(null);
                        disableField.SetValue(null, true);
                        Debug.Log("[EnhancedDynamics] Disabled VRCFury via 'disabled' field");
                    }
                    else if (enabledField != null)
                    {
                        _vrcFuryOriginalState = enabledField.GetValue(null);
                        enabledField.SetValue(null, false);
                        Debug.Log("[EnhancedDynamics] Disabled VRCFury via 'enabled' field");
                    }
                    else if (skipField != null)
                    {
                        _vrcFuryOriginalState = skipField.GetValue(null);
                        skipField.SetValue(null, true);
                        Debug.Log("[EnhancedDynamics] Disabled VRCFury via 'skip' field");
                    }
                    else
                    {
                        Debug.LogWarning("[EnhancedDynamics] Could not find a way to disable VRCFury");
                    }
                }
                else
                {
                    Debug.Log("[EnhancedDynamics] VRCFury not found in project");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error disabling VRCFury: {e}");
            }
        }
        
        private static void DisableModularAvatar()
        {
            try
            {
                // Look for Modular Avatar
                Type maType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name.Contains("ModularAvatar") || assembly.GetName().Name.Contains("nadena.dev"))
                    {
                        Debug.Log($"[EnhancedDynamics] Found Modular Avatar assembly: {assembly.GetName().Name}");
                        
                        // Look for the main build processor
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.Name.Contains("AvatarProcessor") || type.Name.Contains("ModularAvatarBuilder"))
                            {
                                maType = type;
                                Debug.Log($"[EnhancedDynamics] Found Modular Avatar type: {type.FullName}");
                                break;
                            }
                        }
                    }
                }
                
                if (maType != null)
                {
                    // Try to disable it
                    var disableField = maType.GetField("disabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    var enabledField = maType.GetField("enabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    
                    if (disableField != null)
                    {
                        _modularAvatarOriginalState = disableField.GetValue(null);
                        disableField.SetValue(null, true);
                        Debug.Log("[EnhancedDynamics] Disabled Modular Avatar via 'disabled' field");
                    }
                    else if (enabledField != null)
                    {
                        _modularAvatarOriginalState = enabledField.GetValue(null);
                        enabledField.SetValue(null, false);
                        Debug.Log("[EnhancedDynamics] Disabled Modular Avatar via 'enabled' field");
                    }
                    else
                    {
                        Debug.LogWarning("[EnhancedDynamics] Could not find a way to disable Modular Avatar");
                    }
                }
                else
                {
                    Debug.Log("[EnhancedDynamics] Modular Avatar not found in project");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error disabling Modular Avatar: {e}");
            }
        }
        
        private static void RestoreVRCFury()
        {
            if (_vrcFuryOriginalState == null) return;
            
            try
            {
                // Find and restore VRCFury state
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name.Contains("VRCFury"))
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.Name.Contains("VRCFuryBuilder") || type.Name.Contains("VRCFuryProcessor"))
                            {
                                var disableField = type.GetField("disabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                                var enabledField = type.GetField("enabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                                var skipField = type.GetField("skip", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                                
                                if (disableField != null)
                                {
                                    disableField.SetValue(null, _vrcFuryOriginalState);
                                    Debug.Log("[EnhancedDynamics] Restored VRCFury 'disabled' field");
                                }
                                else if (enabledField != null)
                                {
                                    enabledField.SetValue(null, _vrcFuryOriginalState);
                                    Debug.Log("[EnhancedDynamics] Restored VRCFury 'enabled' field");
                                }
                                else if (skipField != null)
                                {
                                    skipField.SetValue(null, _vrcFuryOriginalState);
                                    Debug.Log("[EnhancedDynamics] Restored VRCFury 'skip' field");
                                }
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error restoring VRCFury: {e}");
            }
            
            _vrcFuryOriginalState = null;
        }
        
        private static void RestoreModularAvatar()
        {
            if (_modularAvatarOriginalState == null) return;
            
            try
            {
                // Find and restore Modular Avatar state
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name.Contains("ModularAvatar") || assembly.GetName().Name.Contains("nadena.dev"))
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.Name.Contains("AvatarProcessor") || type.Name.Contains("ModularAvatarBuilder"))
                            {
                                var disableField = type.GetField("disabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                                var enabledField = type.GetField("enabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                                
                                if (disableField != null)
                                {
                                    disableField.SetValue(null, _modularAvatarOriginalState);
                                    Debug.Log("[EnhancedDynamics] Restored Modular Avatar 'disabled' field");
                                }
                                else if (enabledField != null)
                                {
                                    enabledField.SetValue(null, _modularAvatarOriginalState);
                                    Debug.Log("[EnhancedDynamics] Restored Modular Avatar 'enabled' field");
                                }
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error restoring Modular Avatar: {e}");
            }
            
            _modularAvatarOriginalState = null;
        }
        
        private static void DisableNDMFApplyOnPlay()
        {
            try
            {
                Debug.Log("[EnhancedDynamics] Disabling NDMF ApplyOnPlay system...");
                
                // Look for NDMF ApplyOnPlay
                Type applyOnPlayType = null;
                Type globalActivatorType = null;
                
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name.Contains("nadena.dev.ndmf"))
                    {
                        Debug.Log($"[EnhancedDynamics] Found NDMF assembly: {assembly.GetName().Name}");
                        
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.Name == "ApplyOnPlay")
                            {
                                applyOnPlayType = type;
                                Debug.Log($"[EnhancedDynamics] Found ApplyOnPlay type: {type.FullName}");
                            }
                            else if (type.Name == "ApplyOnPlayGlobalActivator")
                            {
                                globalActivatorType = type;
                                Debug.Log($"[EnhancedDynamics] Found ApplyOnPlayGlobalActivator type: {type.FullName}");
                            }
                        }
                    }
                }
                
                // Disable ApplyOnPlay
                if (applyOnPlayType != null)
                {
                    var enabledField = applyOnPlayType.GetField("enabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    var disabledField = applyOnPlayType.GetField("disabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    var skipField = applyOnPlayType.GetField("skipProcessing", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    
                    if (enabledField != null)
                    {
                        _ndmfApplyOnPlayState = enabledField.GetValue(null);
                        enabledField.SetValue(null, false);
                        Debug.Log("[EnhancedDynamics] Disabled NDMF ApplyOnPlay via 'enabled' field");
                    }
                    else if (disabledField != null)
                    {
                        _ndmfApplyOnPlayState = disabledField.GetValue(null);
                        disabledField.SetValue(null, true);
                        Debug.Log("[EnhancedDynamics] Disabled NDMF ApplyOnPlay via 'disabled' field");
                    }
                    else if (skipField != null)
                    {
                        _ndmfApplyOnPlayState = skipField.GetValue(null);
                        skipField.SetValue(null, true);
                        Debug.Log("[EnhancedDynamics] Disabled NDMF ApplyOnPlay via 'skipProcessing' field");
                    }
                    else
                    {
                        Debug.LogWarning("[EnhancedDynamics] Could not find a way to disable NDMF ApplyOnPlay");
                    }
                }
                
                // Also try to disable the global activator
                if (globalActivatorType != null)
                {
                    var enabledField = globalActivatorType.GetField("enabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    var disabledField = globalActivatorType.GetField("disabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    
                    if (enabledField != null)
                    {
                        _ndmfGlobalActivatorState = enabledField.GetValue(null);
                        enabledField.SetValue(null, false);
                        Debug.Log("[EnhancedDynamics] Disabled NDMF GlobalActivator via 'enabled' field");
                    }
                    else if (disabledField != null)
                    {
                        _ndmfGlobalActivatorState = disabledField.GetValue(null);
                        disabledField.SetValue(null, true);
                        Debug.Log("[EnhancedDynamics] Disabled NDMF GlobalActivator via 'disabled' field");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error disabling NDMF ApplyOnPlay: {e}");
            }
        }
        
        private static void RestoreNDMFApplyOnPlay()
        {
            try
            {
                Debug.Log("[EnhancedDynamics] Restoring NDMF ApplyOnPlay system...");
                
                // Restore ApplyOnPlay state
                if (_ndmfApplyOnPlayState != null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (assembly.GetName().Name.Contains("nadena.dev.ndmf"))
                        {
                            foreach (var type in assembly.GetTypes())
                            {
                                if (type.Name == "ApplyOnPlay")
                                {
                                    var enabledField = type.GetField("enabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                                    var disabledField = type.GetField("disabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                                    var skipField = type.GetField("skipProcessing", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                                    
                                    if (enabledField != null)
                                    {
                                        enabledField.SetValue(null, _ndmfApplyOnPlayState);
                                        Debug.Log("[EnhancedDynamics] Restored NDMF ApplyOnPlay 'enabled' field");
                                    }
                                    else if (disabledField != null)
                                    {
                                        disabledField.SetValue(null, _ndmfApplyOnPlayState);
                                        Debug.Log("[EnhancedDynamics] Restored NDMF ApplyOnPlay 'disabled' field");
                                    }
                                    else if (skipField != null)
                                    {
                                        skipField.SetValue(null, _ndmfApplyOnPlayState);
                                        Debug.Log("[EnhancedDynamics] Restored NDMF ApplyOnPlay 'skipProcessing' field");
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
                
                // Restore GlobalActivator state
                if (_ndmfGlobalActivatorState != null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (assembly.GetName().Name.Contains("nadena.dev.ndmf"))
                        {
                            foreach (var type in assembly.GetTypes())
                            {
                                if (type.Name == "ApplyOnPlayGlobalActivator")
                                {
                                    var enabledField = type.GetField("enabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                                    var disabledField = type.GetField("disabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                                    
                                    if (enabledField != null)
                                    {
                                        enabledField.SetValue(null, _ndmfGlobalActivatorState);
                                        Debug.Log("[EnhancedDynamics] Restored NDMF GlobalActivator 'enabled' field");
                                    }
                                    else if (disabledField != null)
                                    {
                                        disabledField.SetValue(null, _ndmfGlobalActivatorState);
                                        Debug.Log("[EnhancedDynamics] Restored NDMF GlobalActivator 'disabled' field");
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
                
                _ndmfApplyOnPlayState = null;
                _ndmfGlobalActivatorState = null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error restoring NDMF ApplyOnPlay: {e}");
            }
        }
    }
}