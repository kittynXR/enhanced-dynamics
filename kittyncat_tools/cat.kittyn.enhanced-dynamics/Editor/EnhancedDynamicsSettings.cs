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
        private const string FAST_PREVIEW_KEY = "EnhancedDynamics.FastPreview";
        private const string SHOW_BONES_KEY = "EnhancedDynamics.ShowBones";
        private const string DROP_GIZMO_KEY_KEY = "EnhancedDynamics.DropGizmoKey";
        private const string USE_OVERLAY_UI_KEY = "EnhancedDynamics.UseOverlayUI";
        private const string PREVENT_VRCFURY_KEY = "EnhancedDynamics.PreventVRCFuryInPreview";
        private const string PREVENT_MA_KEY = "EnhancedDynamics.PreventModularAvatarInPreview";
        
        public static bool DebugMode
        {
            get => EditorPrefs.GetBool(DEBUG_MODE_KEY, false);
            set => EditorPrefs.SetBool(DEBUG_MODE_KEY, value);
        }

        public static bool FastPreview
        {
            get => EditorPrefs.GetBool(FAST_PREVIEW_KEY, true);
            set => EditorPrefs.SetBool(FAST_PREVIEW_KEY, value);
        }

        public static bool ShowBones
        {
            get => EditorPrefs.GetBool(SHOW_BONES_KEY, false);
            set => EditorPrefs.SetBool(SHOW_BONES_KEY, value);
        }


        public static KeyCode DropGizmoKey
        {
            get => (KeyCode)EditorPrefs.GetInt(DROP_GIZMO_KEY_KEY, (int)KeyCode.G);
            set => EditorPrefs.SetInt(DROP_GIZMO_KEY_KEY, (int)value);
        }

        public static bool UseOverlayUI
        {
            get => EditorPrefs.GetBool(USE_OVERLAY_UI_KEY, false);
            set => EditorPrefs.SetBool(USE_OVERLAY_UI_KEY, value);
        }

        // Build prevention toggles
        // Defaults: VRCFury prevention ON, Modular Avatar prevention OFF
        public static bool PreventVRCFuryInPreview
        {
            get => EditorPrefs.GetBool(PREVENT_VRCFURY_KEY, true);
            set => EditorPrefs.SetBool(PREVENT_VRCFURY_KEY, value);
        }

        public static bool PreventModularAvatarInPreview
        {
            get => EditorPrefs.GetBool(PREVENT_MA_KEY, false);
            set => EditorPrefs.SetBool(PREVENT_MA_KEY, value);
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

        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/⚡ Use Fast Scene Preview", false, 1511)]
        private static void ToggleFastPreview()
        {
            FastPreview = !FastPreview;
            Debug.Log($"[EnhancedDynamics] Fast Scene Preview {(FastPreview ? "enabled" : "disabled")}");
        }

        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/⚡ Use Fast Scene Preview", true)]
        private static bool ToggleFastPreviewValidate()
        {
            Menu.SetChecked("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/⚡ Use Fast Scene Preview", FastPreview);
            return true;
        }

        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/🦴 Show Bones In Preview", false, 1512)]
        private static void ToggleShowBones()
        {
            ShowBones = !ShowBones;
            SceneView.RepaintAll();
        }

        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/🦴 Show Bones In Preview", true)]
        private static bool ToggleShowBonesValidate()
        {
            Menu.SetChecked("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/🦴 Show Bones In Preview", ShowBones);
            return true;
        }


        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/UI/Use Overlay UI", false, 1514)]
        private static void ToggleUseOverlayUI()
        {
            UseOverlayUI = !UseOverlayUI;
            SceneView.RepaintAll();
        }

        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/UI/Use Overlay UI", true)]
        private static bool ToggleUseOverlayUIValidate()
        {
            Menu.SetChecked("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/UI/Use Overlay UI", UseOverlayUI);
            return true;
        }

        

        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/Hotkeys/Set Drop Gizmo to G", false, 1520)]
        private static void SetDropGizmoToG()
        {
            DropGizmoKey = KeyCode.G;
            SceneView.RepaintAll();
        }

        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/Hotkeys/Set Drop Gizmo to H", false, 1521)]
        private static void SetDropGizmoToH()
        {
            DropGizmoKey = KeyCode.H;
            SceneView.RepaintAll();
        }

        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/Hotkeys/Set Drop Gizmo to J", false, 1522)]
        private static void SetDropGizmoToJ()
        {
            DropGizmoKey = KeyCode.J;
            SceneView.RepaintAll();
        }

        // Build prevention menu toggles
        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/Build Prevention/Prevent VRCFury builds in preview", false, 1530)]
        private static void TogglePreventVRCFury()
        {
            PreventVRCFuryInPreview = !PreventVRCFuryInPreview;
            Debug.Log($"[EnhancedDynamics] Prevent VRCFury in preview: {PreventVRCFuryInPreview}");
        }

        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/Build Prevention/Prevent VRCFury builds in preview", true)]
        private static bool TogglePreventVRCFuryValidate()
        {
            Menu.SetChecked("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/Build Prevention/Prevent VRCFury builds in preview", PreventVRCFuryInPreview);
            return true;
        }

        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/Build Prevention/Prevent MA builds in preview", false, 1531)]
        private static void TogglePreventMA()
        {
            PreventModularAvatarInPreview = !PreventModularAvatarInPreview;
            Debug.Log($"[EnhancedDynamics] Prevent Modular Avatar in preview: {PreventModularAvatarInPreview}");
        }

        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/Build Prevention/Prevent MA builds in preview", true)]
        private static bool TogglePreventMAValidate()
        {
            Menu.SetChecked("Tools/⚙️🎨 kittyn.cat 🐟/Enhanced Dynamics/Build Prevention/Prevent MA builds in preview", PreventModularAvatarInPreview);
            return true;
        }
    }
}
