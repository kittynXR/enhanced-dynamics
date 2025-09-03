using UnityEditor;
using UnityEngine;
using System.Linq;

namespace Kittyn.Tools.EnhancedDynamics
{
    public static class KittynLocalizationDebug
    {
        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/🧪 QA/Enhanced Dynamics - Log Localization Status", false, 811)]
        public static void LogStatus()
        {
            Debug.Log($"[Enhanced Dynamics Localization Debug] Testing Enhanced Dynamics localization system...");
            
            // Force init
            var langs = KittynLocalization.AvailableLanguages;
            Debug.Log($"[Enhanced Dynamics Localization] Current: {KittynLocalization.CurrentLanguage} | Available: {string.Join(", ", langs)}");

            // Sample keys that Enhanced Dynamics should have
            string[] sample = new[]
            {
                "common.language",
                "messages.language_changed",
                "enhanced_dynamics.status",
                "enhanced_dynamics.physics_preview_active",
                "enhanced_dynamics.debug_logging_enabled",
            };

            foreach (var code in langs.OrderBy(s => s))
            {
                var ok = sample.Select(k => (k, KittynLocalization.Get(k, code))).ToArray();
                Debug.Log($"[Enhanced Dynamics Localization] {code}: " + string.Join(" | ", ok.Select(p => $"{p.k}='{p.Item2}'")));
            }
            
            // Test keys that Enhanced Dynamics should NOT have (from other plugins)
            string[] shouldNotHave = new[]
            {
                "comfi_hierarchy.window_title",
                "immersive_scaler.window_title",
            };
            
            Debug.Log($"[Enhanced Dynamics Localization] Testing excluded keys (should return [key]):");
            foreach (var key in shouldNotHave)
            {
                var result = KittynLocalization.Get(key);
                Debug.Log($"[Enhanced Dynamics Localization] {key} = '{result}'" + (result.StartsWith("[") ? " ✅ Correctly excluded" : " ❌ Should be excluded"));
            }
        }
    }
}