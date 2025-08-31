using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EnhancedDynamics.Editor
{
    /// <summary>
    /// Intercepts Unity build callbacks to allow selective filtering during physics preview.
    /// This allows us to block third-party build scripts while letting VRChat SDK through.
    /// </summary>
    public static class BuildCallbackInterceptor
    {
        // Whitelisted namespaces/assemblies that are allowed during physics preview
        private static readonly HashSet<string> AllowedNamespaces = new HashSet<string>
        {
            "VRC",
            "VRCSDK",
            "VRChat",
            "Unity",
            "UnityEngine",
            "UnityEditor",
            "System",
            "EnhancedDynamics" // Allow our own code
        };
        
        // Storage for temporarily removed callbacks
        private static List<IPreprocessBuildWithReport> _removedPreprocessCallbacks = new List<IPreprocessBuildWithReport>();
        private static List<IPostprocessBuildWithReport> _removedPostprocessCallbacks = new List<IPostprocessBuildWithReport>();
        private static List<IProcessSceneWithReport> _removedSceneCallbacks = new List<IProcessSceneWithReport>();
        // Store removed EditorApplication delegates by event name so we can restore properly
        private static readonly Dictionary<string, List<Delegate>> _removedEditorEventDelegates = new Dictionary<string, List<Delegate>>();
        
        private static bool _isIntercepting = false;
        
        /// <summary>
        /// Start intercepting build callbacks, removing non-VRChat ones
        /// </summary>
        public static void StartIntercepting()
        {
            if (_isIntercepting)
            {
                Debug.LogWarning("[EnhancedDynamics] Already intercepting build callbacks");
                return;
            }
            
            if (EnhancedDynamicsSettings.DebugMode)
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Starting build callback interception...");
                }
            }
            
            try
            {
                InterceptBuildProcessors();
                // Strip disallowed EditorApplication subscribers (common for ApplyOnPlay triggers)
                InterceptEditorEvent("playModeStateChanged");
                InterceptEditorEvent("update");
                InterceptEditorEvent("delayCall");
                InterceptEditorEvent("projectChanged");
                InterceptEditorEvent("hierarchyChanged");
                
                _isIntercepting = true;
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log("[EnhancedDynamics] Build callback interception active");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to intercept build callbacks: {e}");
                StopIntercepting(); // Clean up on failure
            }
        }
        
        /// <summary>
        /// Stop intercepting and restore all callbacks
        /// </summary>
        public static void StopIntercepting()
        {
            if (!_isIntercepting)
            {
                return;
            }
            
            if (EnhancedDynamicsSettings.DebugMode)
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Stopping build callback interception...");
                }
            }
            
            try
            {
                RestoreBuildProcessors();
                // Restore EditorApplication subscribers that we stripped
                RestoreEditorEvent("playModeStateChanged");
                RestoreEditorEvent("update");
                RestoreEditorEvent("delayCall");
                RestoreEditorEvent("projectChanged");
                RestoreEditorEvent("hierarchyChanged");
                
                _isIntercepting = false;
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Build callbacks restored");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to restore build callbacks: {e}");
            }
        }
        
        private static void InterceptBuildProcessors()
        {
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log("[EnhancedDynamics] Attempting to intercept build processors...");
            }
            
            // First, let's try a different approach - intercept through BuildPipeline
            var buildPipelineType = typeof(BuildPipeline);
            if (EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log($"[EnhancedDynamics] BuildPipeline type: {buildPipelineType}");
            }
            
            // Get all build interfaces from the current domain
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        // Check for IPreprocessBuildWithReport implementations
                        if (typeof(IPreprocessBuildWithReport).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                        {
                            if (EnhancedDynamicsSettings.DebugMode)
                            {
                                Debug.Log($"[EnhancedDynamics] Found IPreprocessBuildWithReport: {type.FullName} in {assembly.GetName().Name}");
                            }
                        }
                        
                        // Check for IPostprocessBuildWithReport implementations
                        if (typeof(IPostprocessBuildWithReport).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                        {
                            if (EnhancedDynamicsSettings.DebugMode)
                            {
                                Debug.Log($"[EnhancedDynamics] Found IPostprocessBuildWithReport: {type.FullName} in {assembly.GetName().Name}");
                            }
                        }
                        
                        // Check for IProcessSceneWithReport implementations
                        if (typeof(IProcessSceneWithReport).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                        {
                            if (EnhancedDynamicsSettings.DebugMode)
                            {
                                Debug.Log($"[EnhancedDynamics] Found IProcessSceneWithReport: {type.FullName} in {assembly.GetName().Name}");
                            }
                        }
                    }
                }
                catch { }
            }
            
            // Get BuildPlayerWindow type using reflection
            var buildPlayerWindowType = typeof(BuildPlayerWindow);
            
            // Access internal BuildPlatforms class
            var buildPlatformsType = buildPlayerWindowType.Assembly.GetType("UnityEditor.Build.BuildPlatforms");
            if (buildPlatformsType == null)
            {
                Debug.LogWarning("[EnhancedDynamics] Could not find BuildPlatforms type");
                return;
            }
            
            // Get the instance property
            var instanceProp = buildPlatformsType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProp == null)
            {
                Debug.LogWarning("[EnhancedDynamics] Could not find BuildPlatforms.instance");
                return;
            }
            
            var buildPlatforms = instanceProp.GetValue(null);
            if (buildPlatforms == null)
            {
                Debug.LogWarning("[EnhancedDynamics] BuildPlatforms.instance is null");
                return;
            }
            
            // Try to intercept through BuildPlayerProcessor
            var processorType = buildPlayerWindowType.Assembly.GetType("UnityEditor.BuildPlayerProcessor");
            if (processorType != null)
            {
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log($"[EnhancedDynamics] Found BuildPlayerProcessor type: {processorType}");
                }
                
                // Get all build callbacks through reflection
                InterceptCallbackList<IPreprocessBuildWithReport>(processorType, "buildPreprocessors", _removedPreprocessCallbacks);
                InterceptCallbackList<IPostprocessBuildWithReport>(processorType, "buildPostprocessors", _removedPostprocessCallbacks);
                InterceptCallbackList<IProcessSceneWithReport>(processorType, "sceneProcessors", _removedSceneCallbacks);
            }
            else
            {
                Debug.LogWarning("[EnhancedDynamics] Could not find BuildPlayerProcessor type");
            }
            
            // Also check for callbacks registered through BuildPlayerHandler
            var handlerType = buildPlayerWindowType.Assembly.GetType("UnityEditor.BuildPlayerHandler");
            if (handlerType != null)
            {
                // Additional interception points if needed
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log($"[EnhancedDynamics] Found BuildPlayerHandler type: {handlerType}");
                }
            }
        }
        
        private static void InterceptCallbackList<T>(Type containerType, string fieldName, List<T> storage) where T : class
        {
            try
            {
                var field = containerType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (field == null)
                {
                    Debug.LogWarning($"[EnhancedDynamics] Could not find field {fieldName} in {containerType}");
                    return;
                }
                
                var list = field.GetValue(null) as IList<T>;
                if (list == null)
                {
                    Debug.LogWarning($"[EnhancedDynamics] Field {fieldName} is null or not a list");
                    return;
                }
                
                // Filter and remove non-VRChat callbacks
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var callback = list[i];
                    if (!IsAllowedCallback(callback))
                    {
                        storage.Add(callback);
                        list.RemoveAt(i);
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            Debug.Log($"[EnhancedDynamics] Removed {callback.GetType().FullName} from {fieldName}");
                        }
                    }
                }
                
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log($"[EnhancedDynamics] Intercepted {storage.Count} callbacks from {fieldName}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to intercept {fieldName}: {e}");
            }
        }
        
        private static void InterceptEditorEvent(string fieldName)
        {
            try
            {
                var field = typeof(EditorApplication).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
                if (field == null)
                {
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log($"[EnhancedDynamics] Could not find EditorApplication event field '{fieldName}'");
                    }
                    return;
                }
                var current = field.GetValue(null) as Delegate;
                if (current == null) return;

                var removed = new List<Delegate>();
                Delegate kept = null;
                foreach (var d in current.GetInvocationList())
                {
                    if (IsAllowedEditorDelegate(d))
                    {
                        kept = kept == null ? d : Delegate.Combine(kept, d);
                    }
                    else
                    {
                        removed.Add(d);
                        if (EnhancedDynamicsSettings.DebugMode)
                        {
                            var decl = d.Method.DeclaringType;
                            Debug.Log($"[EnhancedDynamics] Stripping {fieldName} subscriber: {decl?.FullName}.{d.Method.Name} from {decl?.Assembly.GetName().Name}");
                        }
                    }
                }
                field.SetValue(null, kept);

                if (removed.Count > 0)
                {
                    _removedEditorEventDelegates[fieldName] = removed;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error intercepting EditorApplication.{fieldName}: {e}");
            }
        }
        
        private static void RestoreBuildProcessors()
        {
            // Restore all removed callbacks
            var buildPlayerWindowType = typeof(BuildPlayerWindow);
            var processorType = buildPlayerWindowType.Assembly.GetType("UnityEditor.BuildPlayerProcessor");
            
            if (processorType != null)
            {
                RestoreCallbackList<IPreprocessBuildWithReport>(processorType, "buildPreprocessors", _removedPreprocessCallbacks);
                RestoreCallbackList<IPostprocessBuildWithReport>(processorType, "buildPostprocessors", _removedPostprocessCallbacks);
                RestoreCallbackList<IProcessSceneWithReport>(processorType, "sceneProcessors", _removedSceneCallbacks);
            }
            
            _removedPreprocessCallbacks.Clear();
            _removedPostprocessCallbacks.Clear();
            _removedSceneCallbacks.Clear();
        }
        
        private static void RestoreCallbackList<T>(Type containerType, string fieldName, List<T> storage) where T : class
        {
            try
            {
                var field = containerType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (field == null)
                {
                    return;
                }
                
                var list = field.GetValue(null) as IList<T>;
                if (list == null)
                {
                    return;
                }
                
                // Restore all callbacks
                foreach (var callback in storage)
                {
                    list.Add(callback);
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        Debug.Log($"[EnhancedDynamics] Restored {callback.GetType().FullName} to {fieldName}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to restore {fieldName}: {e}");
            }
        }
        
        private static void RestoreEditorEvent(string fieldName)
        {
            try
            {
                if (!_removedEditorEventDelegates.TryGetValue(fieldName, out var removed)) return;

                var field = typeof(EditorApplication).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
                if (field == null) return;

                var current = field.GetValue(null) as Delegate;
                foreach (var d in removed)
                {
                    current = current == null ? d : Delegate.Combine(current, d);
                    if (EnhancedDynamicsSettings.DebugMode)
                    {
                        var decl = d.Method.DeclaringType;
                        Debug.Log($"[EnhancedDynamics] Restored {fieldName} subscriber: {decl?.FullName}.{d.Method.Name}");
                    }
                }
                field.SetValue(null, current);
                _removedEditorEventDelegates.Remove(fieldName);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Error restoring EditorApplication.{fieldName}: {e}");
            }
        }
        
        private static bool IsAllowedCallback(object callback)
        {
            if (callback == null) return false; // Null target typically indicates static method; decide via method-based path elsewhere
            var type = callback.GetType();
            return IsAllowedType(type);
        }

        private static bool IsAllowedEditorDelegate(Delegate d)
        {
            // Prefer method/declaring type for robust filtering (handles static handlers)
            var method = d?.Method;
            return IsAllowedMethod(method);
        }

        private static bool IsAllowedType(Type type)
        {
            if (type == null) return false;
            var ns = type.Namespace ?? "";
            var an = type.Assembly.GetName().Name;
            bool isAllowed = AllowedNamespaces.Any(allowed => ns.StartsWith(allowed) || an.StartsWith(allowed));

            // Dynamically allowlists based on user settings
            if (!isAllowed)
            {
                // If VRCFury prevention is OFF, allow VF/com.vrcfury assemblies
                if (!EnhancedDynamicsSettings.PreventVRCFuryInPreview)
                {
                    if (ns.StartsWith("VF.") || an.Contains("VRCFury") || an.Contains("com.vrcfury"))
                        isAllowed = true;
                }
                // If Modular Avatar prevention is OFF, allow nadena.dev.ndmf + modular_avatar
                if (!EnhancedDynamicsSettings.PreventModularAvatarInPreview)
                {
                    if (ns.StartsWith("nadena.dev.ndmf") || ns.StartsWith("nadena.dev.modular_avatar") || an.Contains("ModularAvatar"))
                        isAllowed = true;
                }
            }
            if (!isAllowed && EnhancedDynamicsSettings.DebugMode)
            {
                Debug.Log($"[EnhancedDynamics] Blocking callback type: {type.FullName} (Namespace: {ns}, Assembly: {an})");
            }
            return isAllowed;
        }

        private static bool IsAllowedMethod(MethodInfo method)
        {
            var declType = method?.DeclaringType;
            return IsAllowedType(declType);
        }
    }
}
