using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Reflection;
using System.Linq;

namespace EnhancedDynamics.Editor.EditorUI
{
    /// <summary>
    /// Unified icon management utility for kittyn.cat tools
    /// Provides consistent icon loading across Unity 2022+ versions
    /// </summary>
    public static class KittynIconUtility
    {
        private static readonly Dictionary<string, Texture2D> _iconCache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<Type, Texture2D> _componentIconCache = new Dictionary<Type, Texture2D>();
        
        /// <summary>
        /// Load an icon by name, searching through multiple paths
        /// </summary>
        /// <param name="iconName">Name of the icon file (without extension)</param>
        /// <param name="searchPaths">Array of paths to search for the icon</param>
        /// <returns>The loaded icon texture, or null if not found</returns>
        public static Texture2D LoadIcon(string iconName, params string[] searchPaths)
        {
            if (string.IsNullOrEmpty(iconName))
                return null;
                
            // Check cache first
            if (_iconCache.TryGetValue(iconName, out var cached))
                return cached;
            
            // If no explicit paths provided, use default search locations
            if (searchPaths == null || searchPaths.Length == 0)
            {
                searchPaths = GetDefaultSearchPaths(iconName);
            }
            
            // Search for the icon
            foreach (var path in searchPaths)
            {
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (icon != null)
                {
                    _iconCache[iconName] = icon;
                    return icon;
                }
            }
            
            // Try Editor Default Resources as fallback
            var defaultResourcePath = $"Icons/{iconName}";
            var defaultIcon = EditorGUIUtility.Load(defaultResourcePath) as Texture2D;
            if (defaultIcon != null)
            {
                _iconCache[iconName] = defaultIcon;
                return defaultIcon;
            }
            
            return null;
        }
        
        /// <summary>
        /// Get icon for a component type
        /// </summary>
        public static Texture2D GetComponentIcon(Type componentType)
        {
            if (componentType == null)
                return null;
                
            // Check cache
            if (_componentIconCache.TryGetValue(componentType, out var cached))
                return cached;
            
            // Method 1: Try to get icon from Unity's ObjectContent
            var content = EditorGUIUtility.ObjectContent(null, componentType);
            if (content != null && content.image != null && content.image is Texture2D texture)
            {
                _componentIconCache[componentType] = texture;
                return texture;
            }
            
            // Method 2: Try to get icon from MonoScript
            var scripts = AssetDatabase.FindAssets($"t:MonoScript {componentType.Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith($"{componentType.Name}.cs"));
                
            foreach (var scriptPath in scripts)
            {
                var monoImporter = AssetImporter.GetAtPath(scriptPath) as MonoImporter;
                if (monoImporter != null)
                {
                    var scriptIcon = monoImporter.GetIcon();
                    if (scriptIcon != null)
                    {
                        _componentIconCache[componentType] = scriptIcon;
                        return scriptIcon;
                    }
                }
            }
            
            // Method 3: Try Icon attribute (Unity 2021.2+)
            #if UNITY_2021_2_OR_NEWER
            var iconAttr = componentType.GetCustomAttribute<IconAttribute>();
            if (iconAttr != null && !string.IsNullOrEmpty(iconAttr.path))
            {
                var attrIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconAttr.path);
                if (attrIcon != null)
                {
                    _componentIconCache[componentType] = attrIcon;
                    return attrIcon;
                }
            }
            #endif
            
            // Method 4: Try to load from conventional paths
            var iconName = componentType.Name;
            var icon = LoadIcon(iconName);
            if (icon != null)
            {
                _componentIconCache[componentType] = icon;
                return icon;
            }
            
            return null;
        }
        
        /// <summary>
        /// Set icon for a MonoScript asset
        /// </summary>
        public static void SetScriptIcon(MonoScript script, Texture2D icon)
        {
            if (script == null || icon == null)
                return;
                
            EditorGUIUtility.SetIconForObject(script, icon);
            EditorUtility.SetDirty(script);
        }
        
        /// <summary>
        /// Get default search paths for an icon
        /// </summary>
        private static string[] GetDefaultSearchPaths(string iconName)
        {
            var paths = new List<string>();
            
            // Add common icon extensions
            var extensions = new[] { ".png", ".jpg", ".jpeg", ".svg" };
            
            // Common package paths
            var packagePrefixes = new[]
            {
                "Packages/cat.kittyn.immersive-scaler/Editor/Icons/",
                "Packages/cat.kittyn.immersive-scaler/Editor/EditorUI/Icons/",
                "Packages/cat.kittyn.comfi-hierarchy/Editor/Icons/",
                "Packages/cat.kittyn.comfi-hierarchy/Editor/EditorUI/Icons/",
                "Packages/cat.kittyn.enhanced-dynamics/Editor/Icons/",
                "Packages/cat.kittyn.enhanced-dynamics/Editor/EditorUI/Icons/"
            };
            
            // Assets paths (for non-VCC installations)
            var assetPrefixes = new[]
            {
                "Assets/kittyncat_tools/cat.kittyn.immersive-scaler/Editor/Icons/",
                "Assets/kittyncat_tools/cat.kittyn.immersive-scaler/Editor/EditorUI/Icons/",
                "Assets/kittyncat_tools/cat.kittyn.comfi-hierarchy/Editor/Icons/",
                "Assets/kittyncat_tools/cat.kittyn.comfi-hierarchy/Editor/EditorUI/Icons/",
                "Assets/kittyncat_tools/cat.kittyn.enhanced-dynamics/Editor/Icons/",
                "Assets/kittyncat_tools/cat.kittyn.enhanced-dynamics/Editor/EditorUI/Icons/"
            };
            
            // Generate all possible paths
            foreach (var prefix in packagePrefixes)
            {
                foreach (var ext in extensions)
                {
                    paths.Add(prefix + iconName + ext);
                }
            }
            
            foreach (var prefix in assetPrefixes)
            {
                foreach (var ext in extensions)
                {
                    paths.Add(prefix + iconName + ext);
                }
            }
            
            return paths.ToArray();
        }
        
        /// <summary>
        /// Clear icon cache (useful when icons are updated)
        /// </summary>
        public static void ClearCache()
        {
            _iconCache.Clear();
            _componentIconCache.Clear();
        }
        
        /// <summary>
        /// Create a simple colored texture (useful for backgrounds)
        /// </summary>
        public static Texture2D CreateColorTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
        
        /// <summary>
        /// Display an icon in the GUI with optional background
        /// </summary>
        public static void DisplayIcon(Texture2D icon, int size = 64, Color? backgroundColor = null)
        {
            if (icon == null)
                return;
                
            var style = new GUIStyle("box");
            
            if (backgroundColor.HasValue)
            {
                var bgTexture = CreateColorTexture(backgroundColor.Value);
                style.normal.background = bgTexture;
            }
            
            GUILayout.Box(icon, style, GUILayout.Width(size), GUILayout.Height(size));
        }
        
        /// <summary>
        /// Ensure a component type has its icon properly set
        /// </summary>
        public static bool EnsureComponentIcon(Type componentType, Texture2D icon)
        {
            if (componentType == null || icon == null)
                return false;
                
            var scripts = AssetDatabase.FindAssets($"t:MonoScript {componentType.Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith($"{componentType.Name}.cs"));
                
            bool success = false;
            foreach (var scriptPath in scripts)
            {
                var monoImporter = AssetImporter.GetAtPath(scriptPath) as MonoImporter;
                if (monoImporter != null)
                {
                    var currentIcon = monoImporter.GetIcon();
                    if (currentIcon != icon)
                    {
                        monoImporter.SetIcon(icon);
                        monoImporter.SaveAndReimport();
                        success = true;
                        Debug.Log($"[KittynIconUtility] Set icon for {componentType.Name} at {scriptPath}");
                    }
                    else
                    {
                        success = true;
                    }
                }
            }
            
            // Clear cache to force refresh
            if (success)
            {
                _componentIconCache.Remove(componentType);
            }
            
            return success;
        }
        
        /// <summary>
        /// Force refresh icon detection for a component type
        /// </summary>
        public static void RefreshComponentIcon(Type componentType)
        {
            _componentIconCache.Remove(componentType);
            
            // Force Unity to refresh its internal icon cache
            var content = EditorGUIUtility.ObjectContent(null, componentType);
            if (content != null)
            {
                // This forces Unity to re-query the icon
                EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<MonoScript>(
                    AssetDatabase.FindAssets($"t:MonoScript {componentType.Name}")
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .FirstOrDefault(p => p.EndsWith($"{componentType.Name}.cs"))
                ));
            }
        }
        
        /// <summary>
        /// Validate that all tool icons are present
        /// </summary>
        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/🧪 Validate All Icons")]
        public static void ValidateAllIcons()
        {
            Debug.Log("[KittynIconUtility] ========== Validating Tool Icons ==========");
            
            var toolIcons = new Dictionary<string, string[]>
            {
                { "ImmersiveScaler", new[] { "Immersive Scaler icon" } },
                { "ComfiHierarchy", new[] { "ComfiHierarchy icon" } },
                { "EnhancedDynamics", new[] { "Enhanced Dynamics icon" } }
            };
            
            foreach (var kvp in toolIcons)
            {
                var icon = LoadIcon(kvp.Key);
                if (icon != null)
                {
                    Debug.Log($"[KittynIconUtility] ✓ {kvp.Value[0]} found: {icon.name}");
                }
                else
                {
                    Debug.LogWarning($"[KittynIconUtility] ✗ {kvp.Value[0]} NOT found");
                }
            }
            
            Debug.Log("[KittynIconUtility] ========== Validation Complete ==========");
        }
    }
}