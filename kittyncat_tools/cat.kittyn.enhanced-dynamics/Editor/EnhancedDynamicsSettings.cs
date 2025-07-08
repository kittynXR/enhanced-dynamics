using UnityEditor;
using UnityEngine;

namespace EnhancedDynamics.Editor
{
    /// <summary>
    /// Settings for Enhanced Dynamics stored in EditorPrefs
    /// </summary>
    public static class EnhancedDynamicsSettings
    {
        private const string DEBUG_MODE_KEY = "EnhancedDynamics.DebugMode";
        
        public static bool DebugMode
        {
            get => EditorPrefs.GetBool(DEBUG_MODE_KEY, false);
            set => EditorPrefs.SetBool(DEBUG_MODE_KEY, value);
        }
        
        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/🐞 Toggle Debug Logging", false, 1510)]
        private static void ToggleDebugMode()
        {
            DebugMode = !DebugMode;
            
            if (DebugMode)
            {
                Debug.Log("[EnhancedDynamics] Debug logging enabled");
            }
            else
            {
                Debug.Log("[EnhancedDynamics] Debug logging disabled");
            }
        }
        
        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/🐞 Toggle Debug Logging", true)]
        private static bool ToggleDebugModeValidate()
        {
            Menu.SetChecked("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/🐞 Toggle Debug Logging", DebugMode);
            return true;
        }
    }
}