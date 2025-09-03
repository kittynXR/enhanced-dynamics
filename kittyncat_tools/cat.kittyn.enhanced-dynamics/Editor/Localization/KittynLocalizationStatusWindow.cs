using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kittyn.Tools.EnhancedDynamics
{
    public class KittynLocalizationStatusWindow : EditorWindow
    {
        private class LangData
        {
            public string code;
            public string display;
            public Dictionary<string, object> keys = new Dictionary<string, object>();
            public bool foldout;
        }

        private Vector2 _scroll;
        private string _filter = "";
        private List<LangData> _langs = new List<LangData>();
        private HashSet<string> _allKeys = new HashSet<string>();
        private HashSet<string> _baseKeys = new HashSet<string>();
        private string _baseLang = "en";
        private double _lastLoad;

        [MenuItem("Tools/⚙️🎨 kittyn.cat 🐟/🧪 QA/Enhanced Dynamics Localization Status", false, 810)]
        public static void ShowWindow()
        {
            var win = GetWindow<KittynLocalizationStatusWindow>("Localization Status");
            win.minSize = new Vector2(520, 360);
            win.Reload();
        }

        private void OnEnable()
        {
            Reload();
        }

        private void Reload()
        {
            _langs.Clear();
            _allKeys.Clear();
            _baseKeys.Clear();

            var assets = Resources.LoadAll<TextAsset>("Localization/");
            foreach (var ta in assets)
            {
                if (!ta.name.StartsWith("kittyn.localization.")) continue;
                var code = ta.name.Replace("kittyn.localization.", "");
                try
                {
                    var data = MiniJSON.Json.Deserialize(ta.text) as Dictionary<string, object>;
                    var flat = Flatten(data);
                    var lang = new LangData
                    {
                        code = code,
                        display = KittynLocalization.GetLanguageDisplayName(code),
                        keys = flat
                    };
                    _langs.Add(lang);
                    foreach (var k in flat.Keys) _allKeys.Add(k);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Localization QA] Failed parsing {ta.name}: {e.Message}");
                }
            }

            if (_langs.All(l => l.code != _baseLang) && _langs.Count > 0)
            {
                _baseLang = _langs[0].code;
            }
            var baseLang = _langs.FirstOrDefault(l => l.code == _baseLang);
            _baseKeys = baseLang != null ? new HashSet<string>(baseLang.keys.Keys) : new HashSet<string>();
            _lastLoad = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawSummary();
            EditorGUILayout.Space();
            DrawDetails();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label($"Loaded: {_langs.Count} languages | Keys: {_allKeys.Count} | Base: {_baseLang}", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                GUILayout.Label("Filter", GUILayout.Width(36));
                var newFilter = GUILayout.TextField(_filter, EditorStyles.toolbarTextField, GUILayout.Width(180));
                if (newFilter != _filter)
                {
                    _filter = newFilter;
                }

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    KittynLocalization.RefreshLanguages();
                    Reload();
                }
            }
        }

        private void DrawSummary()
        {
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            foreach (var lang in _langs.OrderBy(l => l.code))
            {
                var missing = _baseKeys.Except(lang.keys.Keys).Count();
                var extra = lang.keys.Keys.Except(_baseKeys).Count();
                var style = new GUIStyle(EditorStyles.label);
                if (lang.code == _baseLang) style.fontStyle = FontStyle.Bold;
                EditorGUILayout.LabelField($"{lang.display} ({lang.code}) — keys: {lang.keys.Count}, missing: {missing}, extra: {extra}", style);
            }
        }

        private void DrawDetails()
        {
            EditorGUILayout.LabelField("Details", EditorStyles.boldLabel);
            foreach (var lang in _langs.OrderBy(l => l.code))
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    lang.foldout = EditorGUILayout.Foldout(lang.foldout, $"{lang.display} ({lang.code})", true);
                    if (!lang.foldout) continue;

                    var missing = _baseKeys.Except(lang.keys.Keys).OrderBy(k => k);
                    var extra = lang.keys.Keys.Except(_baseKeys).OrderBy(k => k);

                    EditorGUILayout.LabelField($"Missing vs {_baseLang}: {missing.Count()}", EditorStyles.miniBoldLabel);
                    foreach (var k in missing)
                    {
                        if (!PassesFilter(k)) continue;
                        EditorGUILayout.LabelField($"• {k}", EditorStyles.miniLabel);
                    }

                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField($"Extra keys (not in {_baseLang}): {extra.Count()}", EditorStyles.miniBoldLabel);
                    foreach (var k in extra)
                    {
                        if (!PassesFilter(k)) continue;
                        EditorGUILayout.LabelField($"• {k}", EditorStyles.miniLabel);
                    }
                }
            }
        }

        private bool PassesFilter(string key)
        {
            if (string.IsNullOrEmpty(_filter)) return true;
            return key.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Dictionary<string, object> Flatten(Dictionary<string, object> source, string prefix = "")
        {
            var result = new Dictionary<string, object>();
            if (source == null) return result;
            foreach (var kvp in source)
            {
                var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";
                if (kvp.Value is Dictionary<string, object> nested)
                {
                    foreach (var inner in Flatten(nested, key)) result[inner.Key] = inner.Value;
                }
                else
                {
                    result[key] = kvp.Value;
                }
            }
            return result;
        }
    }
}

