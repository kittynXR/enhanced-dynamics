using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace EnhancedDynamics.Editor
{
    public static class EnhancedDynamicsShortcuts
    {
        // Appears in Edit > Shortcuts under "Enhanced Dynamics/Drop Gizmo Under Mouse"
        // Default key: G (user can rebind in the Shortcuts window)
        [Shortcut("Enhanced Dynamics/Drop Gizmo Under Mouse", KeyCode.G)]
        private static void DropGizmoUnderMouseShortcut()
        {
            if (!PlayModeHook.IsInAnyPreview) return;
            AvatarGizmoHandler.RequestDropUnderMouse();
            SceneView.RepaintAll();
        }

        [Shortcut("Enhanced Dynamics/Re-center Gizmo", KeyCode.R, ShortcutModifiers.Shift)]
        private static void RecenterGizmoShortcut()
        {
            if (!PlayModeHook.IsInAnyPreview) return;
            var sv = SceneView.lastActiveSceneView;
            if (sv != null)
            {
                AvatarGizmoHandler.RecenterToCamera(sv);
                sv.Repaint();
            }
        }

        [Shortcut("Enhanced Dynamics/Toggle Show Bones", KeyCode.B, ShortcutModifiers.Shift)]
        private static void ToggleShowBonesShortcut()
        {
            if (!PlayModeHook.IsInAnyPreview) return;
            EnhancedDynamicsSettings.ShowBones = !EnhancedDynamicsSettings.ShowBones;
            SceneView.RepaintAll();
        }
    }
}
