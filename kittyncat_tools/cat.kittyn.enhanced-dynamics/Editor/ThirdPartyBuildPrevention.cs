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

        // Brute-force backup of static fields (bools/delegates) we flip off
        private static readonly System.Collections.Generic.Dictionary<FieldInfo, object> _savedStaticFields = new System.Collections.Generic.Dictionary<FieldInfo, object>();
        private static readonly string[] TargetAssemblyNameFragments = new[]
        {
            "nadena.dev.ndmf",
            "nadena.dev.modular_avatar",
            "ModularAvatar",
            "VRCFury",
            "com.vrcfury"
        };
        
        public static void StartPreventing()
        {
            if (_isPreventingBuilds)
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Already preventing third-party builds");
                }
                return;
            }
            
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log("[EnhancedDynamics] Starting third-party build prevention...");
            }
            
            if (EnhancedDynamicsSettings.PreventModularAvatarInPreview)
            {
                DisableNDMFApplyOnPlay();
                DisableModularAvatar();
            }
            if (EnhancedDynamicsSettings.PreventVRCFuryInPreview)
            {
                DisableVRCFury();
            }
            BruteForceDisableAssemblies();
            DestroyKnownRuntimeTriggerObjects();
            
            _isPreventingBuilds = true;
        }
        
        public static void StopPreventing()
        {
            if (!_isPreventingBuilds)
            {
                return;
            }
            
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log("[EnhancedDynamics] Stopping third-party build prevention...");
            }
            
            if (EnhancedDynamicsSettings.PreventModularAvatarInPreview)
            {
                RestoreNDMFApplyOnPlay();
                RestoreModularAvatar();
            }
            if (EnhancedDynamicsSettings.PreventVRCFuryInPreview)
            {
                RestoreVRCFury();
            }
            RestoreBruteForcedFields();
            
            _isPreventingBuilds = false;
        }

        private static void BruteForceDisableAssemblies()
        {
            try
            {
                // Build dynamic target assembly list based on settings
                var fragments = new System.Collections.Generic.List<string>();
                if (EnhancedDynamicsSettings.PreventModularAvatarInPreview)
                {
                    fragments.Add("nadena.dev.ndmf");
                    fragments.Add("nadena.dev.modular_avatar");
                    fragments.Add("ModularAvatar");
                }
                if (EnhancedDynamicsSettings.PreventVRCFuryInPreview)
                {
                    fragments.Add("VRCFury");
                    fragments.Add("com.vrcfury");
                }

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var an = asm.GetName().Name;
                    bool target = fragments.Exists(f => an.Contains(f));
                    if (!target) continue;

                    foreach (var type in asm.GetTypes())
                    {
                        foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            try
                            {
                                // Skip readonly fields
                                if (field.IsInitOnly) continue;
                                var ft = field.FieldType;
                                if (ft == typeof(bool))
                                {
                                    // Save original
                                    if (!_savedStaticFields.ContainsKey(field))
                                        _savedStaticFields[field] = field.GetValue(null);

                                    var name = field.Name.ToLowerInvariant();
                                    // If field sounds like a disable flag, set true; otherwise set false
                                    bool newVal = name.Contains("disable") || name.Contains("disabled");
                                    field.SetValue(null, newVal);
                                    if (EnhancedDynamicsSettings.DebugMode)
                                    {
                                        Debug.Log($"[EnhancedDynamics] Brute-force set {type.FullName}.{field.Name} = {newVal}");
                                    }
                                }
                                else if (typeof(Delegate).IsAssignableFrom(ft))
                                {
                                    // Nuke static delegate subscribers
                                    var current = field.GetValue(null);
                                    if (current != null)
                                    {
                                        if (!_savedStaticFields.ContainsKey(field))
                                            _savedStaticFields[field] = current;
                                        field.SetValue(null, null);
                                        if (EnhancedDynamicsSettings.DebugMode)
                                        {
                                            Debug.Log($"[EnhancedDynamics] Cleared delegate {type.FullName}.{field.Name}");
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error in BruteForceDisableAssemblies: {e}");
            }
        }

        private static void RestoreBruteForcedFields()
        {
            foreach (var kvp in _savedStaticFields)
            {
                try
                {
                    kvp.Key.SetValue(null, kvp.Value);
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log($"[EnhancedDynamics] Restored {kvp.Key.DeclaringType?.FullName}.{kvp.Key.Name}");
                    }
                }
                catch { }
            }
            _savedStaticFields.Clear();
        }

        private static void DestroyKnownRuntimeTriggerObjects()
        {
            try
            {
                // Destroy VRCFury play mode trigger object if present
                if (EnhancedDynamicsSettings.PreventVRCFuryInPreview)
                {
                    var vrcfObj = GameObject.Find("__vrcf_play_mode_trigger");
                    if (vrcfObj != null)
                    {
                        GameObject.DestroyImmediate(vrcfObj);
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log("[EnhancedDynamics] Destroyed __vrcf_play_mode_trigger object");
                        }
                    }
                }

                // Remove any NDMF GlobalActivator/AvatarActivator runtime components already present in scene
                if (EnhancedDynamicsSettings.PreventModularAvatarInPreview)
                {
                    var behaviours = GameObject.FindObjectsOfType<MonoBehaviour>(true);
                    foreach (var mb in behaviours)
                    {
                        if (mb == null) continue;
                        var tn = mb.GetType().FullName ?? string.Empty;
                        if (tn == "nadena.dev.ndmf.runtime.ApplyOnPlayGlobalActivator" ||
                            tn == "nadena.dev.ndmf.runtime.AvatarActivator")
                        {
                            GameObject.DestroyImmediate(mb);
                            if (EnhancedDynamicsSettings.DebugMode)
                            {
                                Debug.Log($"[EnhancedDynamics] Removed runtime activator: {tn}");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EnhancedDynamics] Error destroying runtime triggers: {e}");
            }
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
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log($"[EnhancedDynamics] Found VRCFury assembly: {assembly.GetName().Name}");
                        }
                        
                        // Look for the main build processor and PlayModeTrigger
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.Name.Contains("VRCFuryBuilder") || type.Name.Contains("VRCFuryProcessor"))
                            {
                                vrcFuryType = type;
                                if (EnhancedDynamicsSettings.DebugMode)
                                {
                                    Debug.Log($"[EnhancedDynamics] Found VRCFury type: {type.FullName}");
                                }
                            }
                            else if (type.Name == "PlayModeTrigger" && type.Namespace.Contains("VF"))
                            {
                                playModeTriggerType = type;
                                if (EnhancedDynamicsSettings.DebugMode)
                                {
                                    Debug.Log($"[EnhancedDynamics] Found VRCFury PlayModeTrigger: {type.FullName}");
                                }
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
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log("[EnhancedDynamics] Disabled VRCFury PlayModeTrigger via 'enabled' field");
                        }
                    }
                    else if (disabledField != null)
                    {
                        disabledField.SetValue(null, true);
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log("[EnhancedDynamics] Disabled VRCFury PlayModeTrigger via 'disabled' field");
                        }
                    }
                    else if (initField != null)
                    {
                        initField.SetValue(null, false);
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log("[EnhancedDynamics] Disabled VRCFury PlayModeTrigger via 'init' field");
                        }
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
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log("[EnhancedDynamics] Disabled VRCFury via 'disabled' field");
                        }
                    }
                    else if (enabledField != null)
                    {
                        _vrcFuryOriginalState = enabledField.GetValue(null);
                        enabledField.SetValue(null, false);
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log("[EnhancedDynamics] Disabled VRCFury via 'enabled' field");
                        }
                    }
                    else if (skipField != null)
                    {
                        _vrcFuryOriginalState = skipField.GetValue(null);
                        skipField.SetValue(null, true);
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log("[EnhancedDynamics] Disabled VRCFury via 'skip' field");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[EnhancedDynamics] Could not find a way to disable VRCFury");
                    }
                }
                else
                {
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log("[EnhancedDynamics] VRCFury not found in project");
                    }
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
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log($"[EnhancedDynamics] Found Modular Avatar assembly: {assembly.GetName().Name}");
                        }
                        
                        // Look for the main build processor
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.Name.Contains("AvatarProcessor") || type.Name.Contains("ModularAvatarBuilder"))
                            {
                                maType = type;
                                if (EnhancedDynamicsSettings.DebugMode)
                                {
                                    Debug.Log($"[EnhancedDynamics] Found Modular Avatar type: {type.FullName}");
                                }
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
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log("[EnhancedDynamics] Disabled Modular Avatar via 'disabled' field");
                        }
                    }
                    else if (enabledField != null)
                    {
                        _modularAvatarOriginalState = enabledField.GetValue(null);
                        enabledField.SetValue(null, false);
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log("[EnhancedDynamics] Disabled Modular Avatar via 'enabled' field");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[EnhancedDynamics] Could not find a way to disable Modular Avatar");
                    }
                }
                else
                {
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log("[EnhancedDynamics] Modular Avatar not found in project");
                    }
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
                                    if (EnhancedDynamicsSettings.DebugMode)
                                    {
                                        Debug.Log("[EnhancedDynamics] Restored VRCFury 'disabled' field");
                                    }
                                }
                                else if (enabledField != null)
                                {
                                    enabledField.SetValue(null, _vrcFuryOriginalState);
                                    if (EnhancedDynamicsSettings.DebugMode)
                                    {
                                        Debug.Log("[EnhancedDynamics] Restored VRCFury 'enabled' field");
                                    }
                                }
                                else if (skipField != null)
                                {
                                    skipField.SetValue(null, _vrcFuryOriginalState);
                                    if (EnhancedDynamicsSettings.DebugMode)
                                    {
                                        Debug.Log("[EnhancedDynamics] Restored VRCFury 'skip' field");
                                    }
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
                                    if (EnhancedDynamicsSettings.DebugMode)
                                    {
                                        Debug.Log("[EnhancedDynamics] Restored Modular Avatar 'disabled' field");
                                    }
                                }
                                else if (enabledField != null)
                                {
                                    enabledField.SetValue(null, _modularAvatarOriginalState);
                                    if (EnhancedDynamicsSettings.DebugMode)
                                    {
                                        Debug.Log("[EnhancedDynamics] Restored Modular Avatar 'enabled' field");
                                    }
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
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Disabling NDMF ApplyOnPlay system...");
                }
                
                // Look for NDMF ApplyOnPlay
                Type applyOnPlayType = null;
                Type globalActivatorType = null;
                
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name.Contains("nadena.dev.ndmf"))
                    {
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log($"[EnhancedDynamics] Found NDMF assembly: {assembly.GetName().Name}");
                        }
                        
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.Name == "ApplyOnPlay")
                            {
                                applyOnPlayType = type;
                                if (EnhancedDynamicsSettings.DebugMode)
                                {
                                    Debug.Log($"[EnhancedDynamics] Found ApplyOnPlay type: {type.FullName}");
                                }
                            }
                            else if (type.Name == "ApplyOnPlayGlobalActivator")
                            {
                                globalActivatorType = type;
                                if (EnhancedDynamicsSettings.DebugMode)
                                {
                                    Debug.Log($"[EnhancedDynamics] Found ApplyOnPlayGlobalActivator type: {type.FullName}");
                                }
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
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log("[EnhancedDynamics] Disabled NDMF ApplyOnPlay via 'enabled' field");
                        }
                    }
                    else if (disabledField != null)
                    {
                        _ndmfApplyOnPlayState = disabledField.GetValue(null);
                        disabledField.SetValue(null, true);
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log("[EnhancedDynamics] Disabled NDMF ApplyOnPlay via 'disabled' field");
                        }
                    }
                    else if (skipField != null)
                    {
                        _ndmfApplyOnPlayState = skipField.GetValue(null);
                        skipField.SetValue(null, true);
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log("[EnhancedDynamics] Disabled NDMF ApplyOnPlay via 'skipProcessing' field");
                        }
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
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log("[EnhancedDynamics] Disabled NDMF GlobalActivator via 'enabled' field");
                        }
                    }
                    else if (disabledField != null)
                    {
                        _ndmfGlobalActivatorState = disabledField.GetValue(null);
                        disabledField.SetValue(null, true);
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log("[EnhancedDynamics] Disabled NDMF GlobalActivator via 'disabled' field");
                        }
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
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Restoring NDMF ApplyOnPlay system...");
                }
                
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
                                        if (EnhancedDynamicsSettings.DebugMode)
                                        {
                                            Debug.Log("[EnhancedDynamics] Restored NDMF ApplyOnPlay 'enabled' field");
                                        }
                                    }
                                    else if (disabledField != null)
                                    {
                                        disabledField.SetValue(null, _ndmfApplyOnPlayState);
                                        if (EnhancedDynamicsSettings.DebugMode)
                                        {
                                            Debug.Log("[EnhancedDynamics] Restored NDMF ApplyOnPlay 'disabled' field");
                                        }
                                    }
                                    else if (skipField != null)
                                    {
                                        skipField.SetValue(null, _ndmfApplyOnPlayState);
                                        if (EnhancedDynamicsSettings.DebugMode)
                                        {
                                            Debug.Log("[EnhancedDynamics] Restored NDMF ApplyOnPlay 'skipProcessing' field");
                                        }
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
                                        if (EnhancedDynamicsSettings.DebugMode)
                                        {
                                            Debug.Log("[EnhancedDynamics] Restored NDMF GlobalActivator 'enabled' field");
                                        }
                                    }
                                    else if (disabledField != null)
                                    {
                                        disabledField.SetValue(null, _ndmfGlobalActivatorState);
                                        if (EnhancedDynamicsSettings.DebugMode)
                                        {
                                            Debug.Log("[EnhancedDynamics] Restored NDMF GlobalActivator 'disabled' field");
                                        }
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
