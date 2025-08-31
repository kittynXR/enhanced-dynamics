using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace EnhancedDynamics.Editor
{
    [Overlay(typeof(SceneView), "Enhanced Dynamics Preview")]
    public class PhysicsPreviewOverlay : Overlay
    {
        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement
            {
                style =
                {
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 6,
                    paddingBottom = 6,
                    minWidth = 320
                }
            };

            var title = new Label("Physics Preview")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, unityTextAlign = TextAnchor.MiddleLeft }
            };
            root.Add(title);

            var status = new Label(PlayModeHook.IsInAnyPreview ? "Active" : "Inactive");
            root.Add(status);

            // Row: toggles
            var togglesRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var showBones = new Toggle("Show Bones") { value = EnhancedDynamicsSettings.ShowBones };
            showBones.RegisterValueChangedCallback(e => { EnhancedDynamicsSettings.ShowBones = e.newValue; SceneView.RepaintAll(); });
            togglesRow.Add(showBones);
            root.Add(togglesRow);

            // Build prevention toggles
            var preventVrcf = new Toggle("Prevent VRCFury builds in preview") { value = EnhancedDynamicsSettings.PreventVRCFuryInPreview };
            preventVrcf.RegisterValueChangedCallback(e => { EnhancedDynamicsSettings.PreventVRCFuryInPreview = e.newValue; });
            root.Add(preventVrcf);

            var preventMa = new Toggle("Prevent MA builds in preview") { value = EnhancedDynamicsSettings.PreventModularAvatarInPreview };
            preventMa.RegisterValueChangedCallback(e => { EnhancedDynamicsSettings.PreventModularAvatarInPreview = e.newValue; });
            root.Add(preventMa);

            // Row: gizmo actions
            var gizmoRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var recenterBtn = new Button(() =>
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv != null) AvatarGizmoHandler.RecenterToCamera(sv);
            }) { text = "Re-center Gizmo" };
            gizmoRow.Add(recenterBtn);

            var dropBtn = new Button(() =>
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv != null) AvatarGizmoHandler.DropAnchorUnderMouse(sv, sv.position.size * 0.5f);
            }) { text = $"Drop Under Mouse ({EnhancedDynamicsSettings.DropGizmoKey})" };
            gizmoRow.Add(dropBtn);
            root.Add(gizmoRow);

            // Hotkey display
            var hotkeyLabel = new Label($"Hotkey: {EnhancedDynamicsSettings.DropGizmoKey}") { style = { color = Color.gray } };
            root.Add(hotkeyLabel);

            // Row: control buttons
            var controls = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var saveBtn = new Button(() => SaveChanges()) { text = "Save Changes" };
            controls.Add(saveBtn);
            var saveExitBtn = new Button(() => { if (SaveChanges()) PhysicsPreviewManager.StopPreview(); }) { text = "Save + Exit" };
            controls.Add(saveExitBtn);
            var exitBtn = new Button(() => PhysicsPreviewManager.StopPreview()) { text = "Exit Preview" };
            controls.Add(exitBtn);
            root.Add(controls);

            // Update enable state based on mode
            root.schedule.Execute(() =>
            {
                var fast = EnhancedDynamicsSettings.FastPreview;
                saveBtn.SetEnabled(!fast);
                saveExitBtn.SetEnabled(!fast);
                status.text = PlayModeHook.IsInAnyPreview ? "Active" : "Inactive";
            }).Every(200);

            return root;
        }

        private static bool SaveChanges()
        {
            try
            {
                var originalAvatar = AvatarHiding.OriginalAvatarForClone;
                var physicsClone = AvatarHiding.PhysicsClone;

                if (originalAvatar == null || physicsClone == null)
                {
                    if (EnhancedDynamicsSettings.FastPreview)
                    {
                        var sv = SceneView.lastActiveSceneView; sv?.ShowNotification(new GUIContent("Save not available in Fast Preview"), 2.0);
                        return false;
                    }
                    else
                    {
                        var sv = SceneView.lastActiveSceneView; sv?.ShowNotification(new GUIContent("Error: Missing avatar references"), 2.0);
                        return false;
                    }
                }

                bool hasChanges = PhysicsChangeMemory.CaptureChangesToMemory(originalAvatar, physicsClone);
                var sceneView = SceneView.lastActiveSceneView;
                sceneView?.ShowNotification(new GUIContent(hasChanges ? "Changes saved! Will apply on exit." : "No changes to save"), hasChanges ? 2.0 : 1.5);
                return hasChanges;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Overlay save error: {e}");
                var sceneView = SceneView.lastActiveSceneView; sceneView?.ShowNotification(new GUIContent("Error saving changes!"), 3.0);
                return false;
            }
        }
    }
}
