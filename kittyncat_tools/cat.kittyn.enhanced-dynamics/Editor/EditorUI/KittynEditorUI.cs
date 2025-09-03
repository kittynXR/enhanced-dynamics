using UnityEngine;
using UnityEditor;
using System;
using Kittyn.Tools.EnhancedDynamics;

namespace EnhancedDynamics.Editor.EditorUI
{
    /// <summary>
    /// Common UI utility methods for kittyn.cat editor tools
    /// Provides consistent styling and UI patterns across all tools
    /// </summary>
    public static class KittynEditorUI
    {
        private static GUIStyle _headerStyle;
        private static GUIStyle _subHeaderStyle;
        private static GUIStyle _foldoutHeaderStyle;
        private static GUIStyle _strikethroughStyle;
        private static GUIStyle _centeredLabelStyle;
        
        /// <summary>
        /// Get or create header style
        /// </summary>
        public static GUIStyle HeaderStyle
        {
            get
            {
                if (_headerStyle == null)
                {
                    _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 14,
                        fontStyle = FontStyle.Bold
                    };
                }
                return _headerStyle;
            }
        }
        
        /// <summary>
        /// Get or create sub-header style
        /// </summary>
        public static GUIStyle SubHeaderStyle
        {
            get
            {
                if (_subHeaderStyle == null)
                {
                    _subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 12
                    };
                }
                return _subHeaderStyle;
            }
        }
        
        /// <summary>
        /// Get or create foldout header style
        /// </summary>
        public static GUIStyle FoldoutHeaderStyle
        {
            get
            {
                if (_foldoutHeaderStyle == null)
                {
                    _foldoutHeaderStyle = new GUIStyle(EditorStyles.foldout)
                    {
                        fontStyle = FontStyle.Bold
                    };
                }
                return _foldoutHeaderStyle;
            }
        }
        
        /// <summary>
        /// Get or create strikethrough style for deprecated fields
        /// </summary>
        public static GUIStyle StrikethroughStyle
        {
            get
            {
                if (_strikethroughStyle == null)
                {
                    _strikethroughStyle = new GUIStyle(GUI.skin.label);
                    _strikethroughStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);
                }
                return _strikethroughStyle;
            }
        }
        
        /// <summary>
        /// Get or create centered label style
        /// </summary>
        public static GUIStyle CenteredLabelStyle
        {
            get
            {
                if (_centeredLabelStyle == null)
                {
                    _centeredLabelStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter
                    };
                }
                return _centeredLabelStyle;
            }
        }
        
        /// <summary>
        /// Draw a section header
        /// </summary>
        public static void DrawSectionHeader(string title)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(title, HeaderStyle);
        }
        
        /// <summary>
        /// Draw a sub-section header
        /// </summary>
        public static void DrawSubHeader(string title)
        {
            EditorGUILayout.LabelField(title, SubHeaderStyle);
        }
        
        /// <summary>
        /// Begin a section with a box
        /// </summary>
        public static void BeginSection(string title = null)
        {
            if (!string.IsNullOrEmpty(title))
            {
                DrawSectionHeader(title);
            }
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        }
        
        /// <summary>
        /// End a section
        /// </summary>
        public static void EndSection()
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }
        
        /// <summary>
        /// Draw a horizontal line separator
        /// </summary>
        public static void DrawSeparator(float height = 1, float spacing = 5)
        {
            EditorGUILayout.Space(spacing);
            var rect = EditorGUILayout.GetControlRect(false, height);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorGUILayout.Space(spacing);
        }
        
        /// <summary>
        /// Draw a toggle button that changes color when selected
        /// </summary>
        public static bool DrawToggleButton(string label, bool isSelected, float width = -1, Color? selectedColor = null)
        {
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = isSelected ? (selectedColor ?? Color.green) : Color.white;
            
            bool clicked;
            if (width > 0)
            {
                clicked = GUILayout.Button(label, GUILayout.Width(width));
            }
            else
            {
                clicked = GUILayout.Button(label);
            }
            
            GUI.backgroundColor = originalColor;
            return clicked;
        }
        
        /// <summary>
        /// Draw a field with a "Get Current" button
        /// </summary>
        public static float DrawFieldWithGetButton(GUIContent label, float currentValue, Action<float> setValue, Func<float> getCurrentValue, float min = 0, float max = 100)
        {
            EditorGUILayout.BeginHorizontal();
            var newValue = EditorGUILayout.Slider(label, currentValue, min, max);
            
            if (GUILayout.Button(KittynLocalization.Get("common.get_current"), GUILayout.Width(80)))
            {
                newValue = getCurrentValue();
                setValue(newValue);
            }
            EditorGUILayout.EndHorizontal();
            
            return newValue;
        }
        
        /// <summary>
        /// Draw a property field with a "Get Current" button for SerializedObject
        /// </summary>
        public static void DrawPropertyWithGetButton(SerializedObject serializedObject, string propertyName, GUIContent label, Func<float> getCurrentValue)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), label);
            
            if (GUILayout.Button(KittynLocalization.Get("common.get_current"), GUILayout.Width(80)))
            {
                var prop = serializedObject.FindProperty(propertyName);
                prop.floatValue = getCurrentValue();
                serializedObject.ApplyModifiedProperties();
            }
            EditorGUILayout.EndHorizontal();
        }
        
        /// <summary>
        /// Draw a help box with custom message type
        /// </summary>
        public static void DrawHelpBox(string message, MessageType type = MessageType.Info)
        {
            EditorGUILayout.HelpBox(message, type);
        }
        
        /// <summary>
        /// Draw a centered button
        /// </summary>
        public static bool DrawCenteredButton(string label, float width = 200)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool clicked = GUILayout.Button(label, GUILayout.Width(width));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            return clicked;
        }
        
        /// <summary>
        /// Draw a foldout with custom styling
        /// </summary>
        public static bool DrawFoldout(bool foldout, string label, bool toggleOnLabelClick = true)
        {
            return EditorGUILayout.Foldout(foldout, label, toggleOnLabelClick, FoldoutHeaderStyle);
        }
        
        /// <summary>
        /// Begin a disabled group conditionally
        /// </summary>
        public static void BeginDisabledGroup(bool disabled)
        {
            EditorGUI.BeginDisabledGroup(disabled);
        }
        
        /// <summary>
        /// End a disabled group
        /// </summary>
        public static void EndDisabledGroup()
        {
            EditorGUI.EndDisabledGroup();
        }
        
        /// <summary>
        /// Draw a progress bar
        /// </summary>
        public static void DrawProgressBar(float value, string label, float minValue = 0, float maxValue = 1)
        {
            var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            EditorGUI.ProgressBar(rect, Mathf.InverseLerp(minValue, maxValue, value), label);
        }
        
        /// <summary>
        /// Draw a colored box with text
        /// </summary>
        public static void DrawColoredBox(string text, Color backgroundColor, Color textColor = default)
        {
            var originalBgColor = GUI.backgroundColor;
            var originalContentColor = GUI.contentColor;
            
            GUI.backgroundColor = backgroundColor;
            if (textColor != default)
            {
                GUI.contentColor = textColor;
            }
            
            GUILayout.Box(text, GUILayout.ExpandWidth(true));
            
            GUI.backgroundColor = originalBgColor;
            GUI.contentColor = originalContentColor;
        }
        
        /// <summary>
        /// Draw an info box
        /// </summary>
        public static void DrawInfoBox(string message)
        {
            DrawColoredBox(message, new Color(0.3f, 0.5f, 0.8f, 0.3f));
        }
        
        /// <summary>
        /// Draw a warning box
        /// </summary>
        public static void DrawWarningBox(string message)
        {
            DrawColoredBox(message, new Color(0.8f, 0.6f, 0.3f, 0.3f));
        }
        
        /// <summary>
        /// Draw an error box
        /// </summary>
        public static void DrawErrorBox(string message)
        {
            DrawColoredBox(message, new Color(0.8f, 0.3f, 0.3f, 0.3f));
        }
    }
}
