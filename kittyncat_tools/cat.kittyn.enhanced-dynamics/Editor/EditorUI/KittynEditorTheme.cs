using UnityEngine;

namespace EnhancedDynamics.Editor.EditorUI
{
    /// <summary>
    /// Consistent color theme for kittyn.cat editor tools
    /// </summary>
    public static class KittynEditorTheme
    {
        // Main brand colors
        public static readonly Color PrimaryColor = new Color(0.01f, 0.2f, 0.2f);
        public static readonly Color AccentColor = new Color(0.2f, 0.8f, 0.8f);
        public static readonly Color IconBackgroundColor = new Color(0.05f, 0.1f, 0.1f);
        
        // UI element colors
        public static readonly Color HeaderColor = new Color(0.9f, 0.9f, 0.9f);
        public static readonly Color SubHeaderColor = new Color(0.8f, 0.8f, 0.8f);
        public static readonly Color SectionBackgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.3f);
        
        // Status colors
        public static readonly Color SuccessColor = new Color(0.3f, 0.8f, 0.3f);
        public static readonly Color WarningColor = new Color(0.9f, 0.7f, 0.2f);
        public static readonly Color ErrorColor = new Color(0.9f, 0.3f, 0.3f);
        public static readonly Color InfoColor = new Color(0.3f, 0.7f, 0.9f);
        
        // Button colors
        public static readonly Color ButtonNormalColor = new Color(0.4f, 0.4f, 0.4f);
        public static readonly Color ButtonHoverColor = new Color(0.5f, 0.5f, 0.5f);
        public static readonly Color ButtonActiveColor = AccentColor;
        public static readonly Color ButtonDisabledColor = new Color(0.3f, 0.3f, 0.3f);
        
        // Text colors
        public static readonly Color DefaultTextColor = new Color(0.9f, 0.9f, 0.9f);
        public static readonly Color SubtleTextColor = new Color(0.7f, 0.7f, 0.7f);
        public static readonly Color HighlightTextColor = AccentColor;
        
        // Specialized UI colors
        public static readonly Color PreviewModeColor = new Color(0.5f, 0.8f, 1f);
        public static readonly Color DangerZoneColor = new Color(0.8f, 0.2f, 0.2f);
        public static readonly Color ToolbarColor = new Color(0.2f, 0.2f, 0.2f);
        
        /// <summary>
        /// Get gradient colors for headers
        /// </summary>
        public static Gradient GetHeaderGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(PrimaryColor, 0.0f), 
                    new GradientColorKey(AccentColor, 1.0f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(1.0f, 0.0f), 
                    new GradientAlphaKey(1.0f, 1.0f) 
                }
            );
            return gradient;
        }
        
        /// <summary>
        /// Apply theme to GUI style
        /// </summary>
        public static void ApplyToStyle(GUIStyle style, bool isHeader = false)
        {
            style.normal.textColor = isHeader ? HeaderColor : DefaultTextColor;
            if (isHeader)
            {
                style.fontStyle = FontStyle.Bold;
                style.fontSize = 14;
            }
        }
    }
}