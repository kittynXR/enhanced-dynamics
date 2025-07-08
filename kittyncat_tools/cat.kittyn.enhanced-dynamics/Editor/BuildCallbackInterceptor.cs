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
        private static Dictionary<MethodInfo, Delegate> _removedInitializeCallbacks = new Dictionary<MethodInfo, Delegate>();
        
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
                Debug.Log("[EnhancedDynamics] Starting build callback interception...");
            }
            
            try
            {
                InterceptBuildProcessors();
                InterceptInitializeOnLoadMethods();
                
                _isIntercepting = true;
                if (EnhancedDynamicsSettings.DebugMode)
                {
                    Debug.Log("[EnhancedDynamics] Build callback interception active");
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
                Debug.Log("[EnhancedDynamics] Stopping build callback interception...");
            }
            
            try
            {
                RestoreBuildProcessors();
                RestoreInitializeOnLoadMethods();
                
                _isIntercepting = false;
                Debug.Log("[EnhancedDynamics] Build callbacks restored");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to restore build callbacks: {e}");
            }
        }
        
        private static void InterceptBuildProcessors()
        {
            Debug.Log("[EnhancedDynamics] Attempting to intercept build processors...");
            
            // First, let's try a different approach - intercept through BuildPipeline
            var buildPipelineType = typeof(BuildPipeline);
            Debug.Log($"[EnhancedDynamics] BuildPipeline type: {buildPipelineType}");
            
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
                            Debug.Log($"[EnhancedDynamics] Found IPreprocessBuildWithReport: {type.FullName} in {assembly.GetName().Name}");
                        }
                        
                        // Check for IPostprocessBuildWithReport implementations
                        if (typeof(IPostprocessBuildWithReport).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                        {
                            Debug.Log($"[EnhancedDynamics] Found IPostprocessBuildWithReport: {type.FullName} in {assembly.GetName().Name}");
                        }
                        
                        // Check for IProcessSceneWithReport implementations
                        if (typeof(IProcessSceneWithReport).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                        {
                            Debug.Log($"[EnhancedDynamics] Found IProcessSceneWithReport: {type.FullName} in {assembly.GetName().Name}");
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
                Debug.Log($"[EnhancedDynamics] Found BuildPlayerProcessor type: {processorType}");
                
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
                Debug.Log($"[EnhancedDynamics] Found BuildPlayerHandler type: {handlerType}");
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
                        Debug.Log($"[EnhancedDynamics] Removed {callback.GetType().FullName} from {fieldName}");
                    }
                }
                
                Debug.Log($"[EnhancedDynamics] Intercepted {storage.Count} callbacks from {fieldName}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to intercept {fieldName}: {e}");
            }
        }
        
        private static void InterceptInitializeOnLoadMethods()
        {
            // This is more complex as InitializeOnLoadMethod callbacks are stored differently
            // They're typically executed through EditorApplication delegates
            
            // Intercept EditorApplication.playModeStateChanged if needed
            var delegateField = typeof(EditorApplication).GetField("playModeStateChanged", BindingFlags.Static | BindingFlags.NonPublic);
            if (delegateField != null)
            {
                var currentDelegate = delegateField.GetValue(null) as Delegate;
                if (currentDelegate != null)
                {
                    var invocationList = currentDelegate.GetInvocationList();
                    foreach (var d in invocationList)
                    {
                        if (!IsAllowedCallback(d.Target))
                        {
                            EditorApplication.playModeStateChanged -= (Action<PlayModeStateChange>)d;
                            _removedInitializeCallbacks[d.Method] = d;
                            Debug.Log($"[EnhancedDynamics] Removed playModeStateChanged callback: {d.Method.DeclaringType?.FullName}.{d.Method.Name}");
                        }
                    }
                }
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
                    Debug.Log($"[EnhancedDynamics] Restored {callback.GetType().FullName} to {fieldName}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnhancedDynamics] Failed to restore {fieldName}: {e}");
            }
        }
        
        private static void RestoreInitializeOnLoadMethods()
        {
            // Restore removed playModeStateChanged callbacks
            foreach (var kvp in _removedInitializeCallbacks)
            {
                EditorApplication.playModeStateChanged += (Action<PlayModeStateChange>)kvp.Value;
                Debug.Log($"[EnhancedDynamics] Restored playModeStateChanged callback: {kvp.Key.DeclaringType?.FullName}.{kvp.Key.Name}");
            }
            
            _removedInitializeCallbacks.Clear();
        }
        
        private static bool IsAllowedCallback(object callback)
        {
            if (callback == null)
                return true; // Allow null callbacks
            
            var type = callback.GetType();
            var namespaceName = type.Namespace ?? "";
            var assemblyName = type.Assembly.GetName().Name;
            
            // Check if namespace or assembly is whitelisted
            bool isAllowed = AllowedNamespaces.Any(allowed => 
                namespaceName.StartsWith(allowed) || 
                assemblyName.StartsWith(allowed));
            
            if (!isAllowed)
            {
                Debug.Log($"[EnhancedDynamics] Blocking callback: {type.FullName} (Namespace: {namespaceName}, Assembly: {assemblyName})");
            }
            
            return isAllowed;
        }
    }
}