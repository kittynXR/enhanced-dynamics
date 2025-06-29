using UnityEditor;
using EnhancedDynamics.Runtime;

namespace EnhancedDynamics.Editor
{
    [InitializeOnLoad]
    public static class EnhancedDynamicsInitializer
    {
        static EnhancedDynamicsInitializer()
        {
            // Initialize Harmony patches when Unity Editor loads
            HarmonyPatcher.Initialize();
            
            // Clean up when entering play mode or recompiling
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }
        
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                HarmonyPatcher.Cleanup();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                HarmonyPatcher.Initialize();
            }
        }
        
        private static void OnBeforeAssemblyReload()
        {
            HarmonyPatcher.Cleanup();
        }
    }
}