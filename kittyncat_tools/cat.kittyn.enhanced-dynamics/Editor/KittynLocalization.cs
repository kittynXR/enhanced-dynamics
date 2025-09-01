using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kittyn.Tools
{
    public static class KittynLocalization
    {
        private const string PREF_KEY_LANGUAGE = "KittynTools_Language";
        private const string LOCALIZATION_PATH = "Localization/";
        private const string DEFAULT_LANGUAGE = "en";
        
        private static Dictionary<string, Dictionary<string, object>> _languages;
        private static string _currentLanguage;
        private static bool _initialized;
        
        public static event Action OnLanguageChanged;
        
        public static string CurrentLanguage
        {
            get
            {
                if (!_initialized) Initialize();
                return _currentLanguage;
            }
            set
            {
                if (value != _currentLanguage && _languages != null && _languages.ContainsKey(value))
                {
                    _currentLanguage = value;
                    EditorPrefs.SetString(PREF_KEY_LANGUAGE, value);
                    OnLanguageChanged?.Invoke();
                }
            }
        }
        
        public static string[] AvailableLanguages
        {
            get
            {
                if (!_initialized) Initialize();
                return _languages?.Keys.ToArray() ?? new[] { DEFAULT_LANGUAGE };
            }
        }
        
        public static string GetLanguageDisplayName(string languageCode)
        {
            return Get("common.language_name", languageCode) ?? languageCode.ToUpper();
        }
        
        private static void Initialize()
        {
            _initialized = true;
            _languages = new Dictionary<string, Dictionary<string, object>>();
            
            LoadAllLanguages();
            
            _currentLanguage = EditorPrefs.GetString(PREF_KEY_LANGUAGE, DEFAULT_LANGUAGE);
            if (!_languages.ContainsKey(_currentLanguage))
            {
                _currentLanguage = DEFAULT_LANGUAGE;
            }
        }
        
        private static void LoadAllLanguages()
        {
            var resources = Resources.LoadAll<TextAsset>(LOCALIZATION_PATH);
            
            foreach (var resource in resources)
            {
                if (resource.name.StartsWith("kittyn.localization.") && resource.name.EndsWith(".json"))
                {
                    var languageCode = resource.name.Replace("kittyn.localization.", "").Replace(".json", "");
                    try
                    {
                        var json = resource.text;
                        var data = MiniJSON.Json.Deserialize(json) as Dictionary<string, object>;
                        if (data != null)
                        {
                            _languages[languageCode] = FlattenDictionary(data);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to load language file {resource.name}: {e.Message}");
                    }
                }
            }
            
            if (!_languages.ContainsKey(DEFAULT_LANGUAGE))
            {
                Debug.LogWarning($"Default language '{DEFAULT_LANGUAGE}' not found. Creating minimal fallback.");
                _languages[DEFAULT_LANGUAGE] = new Dictionary<string, object>();
            }
        }
        
        private static Dictionary<string, object> FlattenDictionary(Dictionary<string, object> source, string prefix = "")
        {
            var result = new Dictionary<string, object>();
            
            foreach (var kvp in source)
            {
                var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";
                
                if (kvp.Value is Dictionary<string, object> nestedDict)
                {
                    var flattened = FlattenDictionary(nestedDict, key);
                    foreach (var nested in flattened)
                    {
                        result[nested.Key] = nested.Value;
                    }
                }
                else
                {
                    result[key] = kvp.Value;
                }
            }
            
            return result;
        }
        
        public static string Get(string key, string languageOverride = null)
        {
            if (!_initialized) Initialize();
            
            var language = languageOverride ?? _currentLanguage;
            
            if (_languages.TryGetValue(language, out var languageData))
            {
                if (languageData.TryGetValue(key, out var value))
                {
                    return value?.ToString();
                }
            }
            
            if (language != DEFAULT_LANGUAGE && _languages.TryGetValue(DEFAULT_LANGUAGE, out var defaultData))
            {
                if (defaultData.TryGetValue(key, out var defaultValue))
                {
                    return defaultValue?.ToString();
                }
            }
            
            #if KITTYN_DEBUG_LOCALIZATION
            Debug.LogWarning($"Localization key not found: {key}");
            #endif
            
            return $"[{key}]";
        }
        
        public static string GetFormat(string key, params object[] args)
        {
            var format = Get(key);
            if (format.StartsWith("[") && format.EndsWith("]"))
                return format;
            
            try
            {
                return string.Format(format, args);
            }
            catch
            {
                return format;
            }
        }
        
        public static void RefreshLanguages()
        {
            _initialized = false;
            Initialize();
            OnLanguageChanged?.Invoke();
        }
    }
    
    namespace MiniJSON
    {
        public static class Json
        {
            public static object Deserialize(string json)
            {
                if (json == null) return null;
                return Parser.Parse(json);
            }
            
            public static string Serialize(object obj)
            {
                return Serializer.Serialize(obj);
            }
            
            sealed class Parser : IDisposable
            {
                const string WORD_BREAK = "{}[],:\"";
                
                public static object Parse(string jsonString)
                {
                    using (var instance = new Parser(jsonString))
                    {
                        return instance.ParseValue();
                    }
                }
                
                StringReader json;
                
                Parser(string jsonString)
                {
                    json = new StringReader(jsonString);
                }
                
                public void Dispose()
                {
                    json.Dispose();
                    json = null;
                }
                
                Dictionary<string, object> ParseObject()
                {
                    var table = new Dictionary<string, object>();
                    json.Read();
                    
                    while (true)
                    {
                        switch (NextToken)
                        {
                            case TOKEN.NONE:
                                return null;
                            case TOKEN.CURLY_CLOSE:
                                return table;
                            default:
                                var name = ParseString();
                                if (name == null) return null;
                                if (NextToken != TOKEN.COLON) return null;
                                json.Read();
                                table[name] = ParseValue();
                                break;
                        }
                    }
                }
                
                List<object> ParseArray()
                {
                    var array = new List<object>();
                    json.Read();
                    
                    var parsing = true;
                    while (parsing)
                    {
                        var nextToken = NextToken;
                        switch (nextToken)
                        {
                            case TOKEN.NONE:
                                return null;
                            case TOKEN.SQUARED_CLOSE:
                                parsing = false;
                                break;
                            default:
                                object value = ParseByToken(nextToken);
                                array.Add(value);
                                break;
                        }
                    }
                    return array;
                }
                
                object ParseValue()
                {
                    var nextToken = NextToken;
                    return ParseByToken(nextToken);
                }
                
                object ParseByToken(TOKEN token)
                {
                    switch (token)
                    {
                        case TOKEN.STRING:
                            return ParseString();
                        case TOKEN.NUMBER:
                            return ParseNumber();
                        case TOKEN.CURLY_OPEN:
                            return ParseObject();
                        case TOKEN.SQUARED_OPEN:
                            return ParseArray();
                        case TOKEN.TRUE:
                            return true;
                        case TOKEN.FALSE:
                            return false;
                        case TOKEN.NULL:
                            return null;
                        default:
                            return null;
                    }
                }
                
                string ParseString()
                {
                    var s = "";
                    char c;
                    json.Read();
                    
                    bool parsing = true;
                    while (parsing)
                    {
                        if (json.Peek() == -1) break;
                        
                        c = NextChar;
                        switch (c)
                        {
                            case '"':
                                parsing = false;
                                break;
                            case '\\':
                                if (json.Peek() == -1)
                                {
                                    parsing = false;
                                    break;
                                }
                                c = NextChar;
                                switch (c)
                                {
                                    case '"':
                                    case '\\':
                                    case '/':
                                        s += c;
                                        break;
                                    case 'b':
                                        s += '\b';
                                        break;
                                    case 'f':
                                        s += '\f';
                                        break;
                                    case 'n':
                                        s += '\n';
                                        break;
                                    case 'r':
                                        s += '\r';
                                        break;
                                    case 't':
                                        s += '\t';
                                        break;
                                    case 'u':
                                        var hex = "";
                                        for (int i = 0; i < 4; i++)
                                        {
                                            hex += NextChar;
                                        }
                                        s += (char)Convert.ToInt32(hex, 16);
                                        break;
                                }
                                break;
                            default:
                                s += c;
                                break;
                        }
                    }
                    return s;
                }
                
                object ParseNumber()
                {
                    var number = NextWord;
                    if (number.IndexOf('.') == -1 && number.IndexOf('E') == -1 && number.IndexOf('e') == -1)
                    {
                        long parsedInt;
                        Int64.TryParse(number, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out parsedInt);
                        return parsedInt;
                    }
                    
                    double parsedDouble;
                    Double.TryParse(number, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out parsedDouble);
                    return parsedDouble;
                }
                
                void EatWhitespace()
                {
                    while (Char.IsWhiteSpace(PeekChar)) {
                        json.Read();
                        if (json.Peek() == -1) break;
                    }
                }
                
                char PeekChar => Convert.ToChar(json.Peek());
                char NextChar => Convert.ToChar(json.Read());
                
                string NextWord
                {
                    get
                    {
                        var word = "";
                        while (!IsWordBreak(PeekChar))
                        {
                            word += NextChar;
                            if (json.Peek() == -1) break;
                        }
                        return word;
                    }
                }
                
                TOKEN NextToken
                {
                    get
                    {
                        EatWhitespace();
                        if (json.Peek() == -1) return TOKEN.NONE;
                        
                        switch (PeekChar)
                        {
                            case '{':
                                return TOKEN.CURLY_OPEN;
                            case '}':
                                json.Read();
                                return TOKEN.CURLY_CLOSE;
                            case '[':
                                return TOKEN.SQUARED_OPEN;
                            case ']':
                                json.Read();
                                return TOKEN.SQUARED_CLOSE;
                            case ',':
                                json.Read();
                                return NextToken;
                            case '"':
                                return TOKEN.STRING;
                            case ':':
                                return TOKEN.COLON;
                            case '0':
                            case '1':
                            case '2':
                            case '3':
                            case '4':
                            case '5':
                            case '6':
                            case '7':
                            case '8':
                            case '9':
                            case '-':
                                return TOKEN.NUMBER;
                        }
                        
                        switch (NextWord)
                        {
                            case "false":
                                return TOKEN.FALSE;
                            case "true":
                                return TOKEN.TRUE;
                            case "null":
                                return TOKEN.NULL;
                        }
                        
                        return TOKEN.NONE;
                    }
                }
                
                static bool IsWordBreak(char c)
                {
                    return Char.IsWhiteSpace(c) || WORD_BREAK.IndexOf(c) != -1;
                }
                
                enum TOKEN
                {
                    NONE,
                    CURLY_OPEN,
                    CURLY_CLOSE,
                    SQUARED_OPEN,
                    SQUARED_CLOSE,
                    COLON,
                    COMMA,
                    STRING,
                    NUMBER,
                    TRUE,
                    FALSE,
                    NULL
                }
            }
            
            sealed class Serializer
            {
                public static string Serialize(object obj)
                {
                    return new Serializer().SerializeValue(obj);
                }
                
                string SerializeValue(object value)
                {
                    if (value == null) return "null";
                    
                    var asStr = value as string;
                    if (asStr != null) return SerializeString(asStr);
                    
                    if (value is bool) return (bool)value ? "true" : "false";
                    
                    var asList = value as IList<object>;
                    if (asList != null) return SerializeArray(asList);
                    
                    var asDict = value as IDictionary<string, object>;
                    if (asDict != null) return SerializeObject(asDict);
                    
                    if (value is float) return ((float)value).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                    else if (value is int || value is uint || value is long || value is sbyte || value is byte || value is short || value is ushort || value is ulong)
                        return value.ToString();
                    else if (value is double || value is decimal) return Convert.ToDouble(value).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                    else return SerializeString(value.ToString());
                }
                
                string SerializeString(string str)
                {
                    var builder = new System.Text.StringBuilder();
                    builder.Append('"');
                    
                    foreach (var c in str)
                    {
                        switch (c)
                        {
                            case '"': builder.Append("\\\""); break;
                            case '\\': builder.Append("\\\\"); break;
                            case '\b': builder.Append("\\b"); break;
                            case '\f': builder.Append("\\f"); break;
                            case '\n': builder.Append("\\n"); break;
                            case '\r': builder.Append("\\r"); break;
                            case '\t': builder.Append("\\t"); break;
                            default:
                                int codepoint = Convert.ToInt32(c);
                                if ((codepoint >= 32) && (codepoint <= 126))
                                {
                                    builder.Append(c);
                                }
                                else
                                {
                                    builder.Append("\\u");
                                    builder.Append(codepoint.ToString("x4"));
                                }
                                break;
                        }
                    }
                    
                    builder.Append('"');
                    return builder.ToString();
                }
                
                string SerializeObject(IDictionary<string, object> obj)
                {
                    var first = true;
                    var builder = new System.Text.StringBuilder();
                    builder.Append('{');
                    
                    foreach (var e in obj)
                    {
                        if (!first) builder.Append(',');
                        builder.Append(SerializeString(e.Key));
                        builder.Append(':');
                        builder.Append(SerializeValue(e.Value));
                        first = false;
                    }
                    
                    builder.Append('}');
                    return builder.ToString();
                }
                
                string SerializeArray(IList<object> array)
                {
                    var builder = new System.Text.StringBuilder();
                    builder.Append('[');
                    var first = true;
                    
                    foreach (var obj in array)
                    {
                        if (!first) builder.Append(',');
                        builder.Append(SerializeValue(obj));
                        first = false;
                    }
                    
                    builder.Append(']');
                    return builder.ToString();
                }
            }
        }
    }
}