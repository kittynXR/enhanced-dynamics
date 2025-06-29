using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace EnhancedDynamics.Runtime
{
    [InitializeOnLoad]
    public static class HarmonyPatcher
    {
        private static Harmony _harmony;
        private const string HarmonyId = "com.enhanceddynamics.physbone";
        
        static HarmonyPatcher()
        {
            Initialize();
        }
        
        public static void Initialize()
        {
            try
            {
                if (_harmony != null)
                {
                    Debug.Log("[EnhancedDynamics] Harmony already initialized");
                    return;
                }
                
                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                
                Debug.Log("[EnhancedDynamics] Harmony patches applied successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to apply Harmony patches: {e}");
            }
        }
        
        public static void Cleanup()
        {
            try
            {
                _harmony?.UnpatchAll(HarmonyId);
                _harmony = null;
                Debug.Log("[EnhancedDynamics] Harmony patches removed");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to remove Harmony patches: {e}");
            }
        }
    }
}