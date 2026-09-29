// RoslynRuntimeCompiler.cs
// Single-file Unity tool for Editor+PlayMode dynamic C# compilation using Roslyn.
// Features:
// - EditorWindow GUI with a large text area for LLM-generated code
// - Compile button (compiles in-memory using Roslyn)
// - Run button (invokes a well-known entry point in the compiled assembly)
// - Shows compile errors and runtime exceptions
// - Safe: Does NOT write .cs files to Assets (no Domain Reload)
//
// Requirements:
// 1) Add Microsoft.CodeAnalysis.CSharp.dll and Microsoft.CodeAnalysis.dll to your Unity project
//    (place under Assets/Plugins or Packages and target the Editor). These come from the Roslyn nuget package.
// 2) This tool is designed to run in the Unity Editor (Play Mode or Edit Mode). It uses Assembly.Load(byte[]).
// 3) Generated code should expose a public type and a public static entry method matching one of the supported signatures:
//    - public static void Run(UnityEngine.GameObject host)
//    - public static void Run(UnityEngine.MonoBehaviour host)
//    - public static System.Collections.IEnumerator RunCoroutine(UnityEngine.MonoBehaviour host) // if you want a coroutine
//    By convention this demo looks for a type name you specify in the window (default: "AIGenerated").
//
// Usage:
// - Window -> Roslyn Runtime Compiler
// - Paste code into the big text area (or use LLM output pasted there)
// - Optionally set Entry Type (default AIGenerated) and Entry Method (default Run)
// - Press "Compile". Compiler diagnostics appear below.
// - In Play Mode, press "Run" to invoke the entry method. In Edit Mode it will attempt to run if valid.
//
// Security note: Any dynamically compiled code runs with the same permissions as the editor. Be careful when running untrusted code.

#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
#endif

/// <summary>
/// Compiles LLM-supplied C# in memory with Roslyn and invokes a well-known entry point,
/// without writing script files to the project (so no domain reload is triggered).
/// Attach to a GameObject to compile and run code against it in the editor or play mode.
/// </summary>
public class RoslynRuntimeCompiler : MonoBehaviour
{

    /// <summary>C# source compiled on demand; must expose the entry type and method named below.</summary>
    [TextArea(8, 20)]
    [Tooltip("Code to compile at runtime. Example class name: AIGenerated with public static void Run(GameObject host)")]
    public string code = "using UnityEngine;\npublic class AIGenerated {\n    public static void Run(GameObject host) {\n        Debug.Log($\"Hello from AI - {host.name}\");\n        host.transform.Rotate(Vector3.up * 45f * Time.deltaTime);\n    }\n}";

    /// <summary>Fully qualified name of the type in <see cref="code"/> that holds the entry method.</summary>
    [Tooltip("Fully qualified type name to invoke (default: AIGenerated)")]
    public string entryTypeName = "AIGenerated";

    /// <summary>Name of the static method invoked on the entry type, matching a supported signature.</summary>
    [Tooltip("Method name to call on entry type (default: Run)")]
    public string entryMethodName = "Run";
    
    /// <summary>When true, the generated MonoBehaviour is attached to the target GameObject instead of only being invoked.</summary>
    [Header("MonoBehaviour Support")]
    [Tooltip("If true, attempts to attach generated MonoBehaviour to target GameObject")]
    public bool attachAsComponent = false;

    /// <summary>GameObject the compiled code runs against and receives as its host argument; falls back to this object.</summary>
    [Tooltip("Target GameObject to attach component to (if null, uses this.gameObject)")]
    public GameObject targetGameObject;

    /// <summary>Records every compile/run attempt into the shared compilation history.</summary>
    [Header("History & Tracing")]
    [Tooltip("Enable automatic history tracking of compiled scripts")]
    public bool enableHistory = true;

    /// <summary>Upper bound on retained history entries; oldest entries are dropped beyond it.</summary>
    [Tooltip("Maximum number of history entries to keep")]
    public int maxHistoryEntries = 20;

    // compiled assembly & method cache
    private Assembly compiledAssembly;
    private MethodInfo entryMethod;
    private Type entryType;
    private Component attachedComponent; // Track dynamically attached component

    /// <summary>True once a compilation has produced a loaded assembly.</summary>
    public bool HasCompiledAssembly => compiledAssembly != null;

    /// <summary>True when the entry method was successfully resolved in the compiled assembly.</summary>
    public bool HasEntryMethod => entryMethod != null;

    /// <summary>True when the entry type was successfully resolved in the compiled assembly.</summary>
    public bool HasEntryType => entryType != null;

    /// <summary>The resolved entry type from the last successful compilation; null before one succeeds.</summary>
    public Type EntryType => entryType; // Public accessor for editor

    // compile result diagnostics (string-friendly)

    /// <summary>Diagnostics text from the most recent compilation, empty when it succeeded.</summary>
    public string lastCompileDiagnostics = "";
    
    // History tracking - SHARED across all instances

    /// <summary>
    /// One record of a compile/run attempt: the source, the resolved entry point, and the outcome.
    /// </summary>
    [System.Serializable]
    public class CompilationHistoryEntry
    {

        /// <summary>When the attempt was recorded.</summary>
        public string timestamp;

        /// <summary>C# source that was compiled in this attempt.</summary>
        public string sourceCode;

        /// <summary>Entry type name used for this attempt.</summary>
        public string typeName;

        /// <summary>Entry method name used for this attempt.</summary>
        public string methodName;

        /// <summary>Whether compilation and invocation both succeeded.</summary>
        public bool success;

        /// <summary>Compiler or runtime diagnostics produced by the attempt.</summary>
        public string diagnostics;

        /// <summary>Name of the GameObject the code was executed against, when applicable.</summary>
        public string executionTarget;
    }
    
    // Static shared history
    private static System.Collections.Generic.List<CompilationHistoryEntry> _sharedHistory = new System.Collections.Generic.List<CompilationHistoryEntry>();
    
    /// <summary>All recorded compile/run attempts, shared across every instance of the component.</summary>
    public System.Collections.Generic.List<CompilationHistoryEntry> CompilationHistory => _sharedHistory;

    // public wrapper so EditorWindow or other runtime UI can call compile/run

    /// <summary>Compiles <see cref="code"/> in memory with Roslyn and loads the resulting assembly.</summary>
    /// <param name="diagnostics">Compiler diagnostics; empty on success.</param>
    /// <returns>True when compilation succeeded and the assembly was loaded.</returns>
    public bool CompileInMemory(out string diagnostics)
    {
#if UNITY_EDITOR
        diagnostics = string.Empty;
        lastCompileDiagnostics = string.Empty;

        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code ?? string.Empty);

            // collect references from loaded assemblies (Editor-safe)
            var refs = new List<MetadataReference>();

            // Always include mscorlib / system.runtime
            refs.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

            // Add all currently loaded assemblies' locations that are not dynamic and have a location
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Distinct();

            foreach (var a in assemblies)
            {
                try
                {
                    refs.Add(MetadataReference.CreateFromFile(a.Location));
                }
                catch { }
            }

            var compilation = CSharpCompilation.Create(
                assemblyName: "RoslynRuntimeAssembly_" + Guid.NewGuid().ToString("N"),
                syntaxTrees: new[] { syntaxTree },
                references: refs,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            using (var ms = new MemoryStream())
            {
                var result = compilation.Emit(ms);
                if (!result.Success)
                {
                    var diagText = string.Join("\n", result.Diagnostics.Select(d => d.ToString()));
                    lastCompileDiagnostics = diagText;
                    diagnostics = diagText;
                    Debug.LogError("Roslyn compile failed:\n" + diagText);
                    return false;
                }

                ms.Seek(0, SeekOrigin.Begin);
                var assemblyData = ms.ToArray();
                compiledAssembly = Assembly.Load(assemblyData);

                // find entry type
                var type = compiledAssembly.GetType(entryTypeName);
                if (type == null)
                {
                    lastCompileDiagnostics = $"Type '{entryTypeName}' not found in compiled assembly.";
                    diagnostics = lastCompileDiagnostics;
                    return false;
                }
                
                entryType = type;

                // Check if it's a MonoBehaviour
                if (typeof(MonoBehaviour).IsAssignableFrom(type))
                {
                    lastCompileDiagnostics = $"Compilation OK. Type '{entryTypeName}' is a MonoBehaviour and can be attached as a component.";
                    diagnostics = lastCompileDiagnostics;
                    Debug.Log(diagnostics);
                    return true;
                }

                // try various method signatures for non-MonoBehaviour types
                entryMethod = type.GetMethod(entryMethodName, BindingFlags.Public | BindingFlags.Static);
                if (entryMethod == null)
                {
                    lastCompileDiagnostics = $"Static method '{entryMethodName}' not found on type '{entryTypeName}'.\n" +
                        $"For MonoBehaviour types, set 'attachAsComponent' to true instead.";
                    diagnostics = lastCompileDiagnostics;
                    return false;
                }

                lastCompileDiagnostics = "Compilation OK.";
                diagnostics = lastCompileDiagnostics;
                Debug.Log("Roslyn compilation successful.");
                return true;
            }
        }
        catch (Exception ex)
        {
            diagnostics = ex.ToString();
            lastCompileDiagnostics = diagnostics;
            Debug.LogError("Roslyn compile exception: " + diagnostics);
            return false;
        }
#else
        diagnostics = "Roslyn compilation is only supported in the Unity Editor when referencing Roslyn assemblies.";
        lastCompileDiagnostics = diagnostics;
        Debug.LogError(diagnostics);
        return false;
#endif
    }

    /// <summary>Invokes the resolved entry method, as a component, coroutine, or reflected call depending on its signature.</summary>
    /// <param name="host">GameObject passed to and targeted by the invoked code.</param>
    /// <param name="runtimeError">Exception message when invocation failed; empty on success.</param>
    /// <returns>True when the entry method ran without throwing.</returns>
    public bool InvokeEntry(GameObject host, out string runtimeError)
    {
        runtimeError = null;
        if (compiledAssembly == null || entryType == null)
        {
            runtimeError = "No compiled assembly / entry type. Call CompileInMemory first.";
            return false;
        }

        // Handle MonoBehaviour types
        if (typeof(MonoBehaviour).IsAssignableFrom(entryType))
        {
            return AttachMonoBehaviour(host, out runtimeError);
        }

        // Handle static method invocation
        if (entryMethod == null)
        {
            runtimeError = "No entry method found. For MonoBehaviour types, use attachAsComponent=true.";
            return false;
        }

        try
        {
            var parameters = entryMethod.GetParameters();
            if (parameters.Length == 0)
            {
                entryMethod.Invoke(null, null);
                return true;
            }
            else if (parameters.Length == 1)
            {
                var pType = parameters[0].ParameterType;
                if (pType == typeof(GameObject))
                    entryMethod.Invoke(null, new object[] { host });
                else if (typeof(MonoBehaviour).IsAssignableFrom(pType))
                {
                    var component = host.GetComponent(pType);
                    entryMethod.Invoke(null, new object[] { component != null ? component : (object)host });
                }
                else if (pType == typeof(Transform))
                    entryMethod.Invoke(null, new object[] { host.transform });
                else if (pType == typeof(object))
                    entryMethod.Invoke(null, new object[] { host });
                else
                    entryMethod.Invoke(null, new object[] { host }); // best effort

                return true;
            }
            else
            {
                runtimeError = "Entry method has unsupported parameter signature.";
                return false;
            }
        }
        catch (TargetInvocationException tie)
        {
            runtimeError = tie.InnerException?.ToString() ?? tie.ToString();
            Debug.LogError("Runtime invocation error: " + runtimeError);
            return false;
        }
        catch (Exception ex)
        {
            runtimeError = ex.ToString();
            Debug.LogError("Runtime invocation error: " + runtimeError);
            return false;
        }
    }

    /// <summary>
    /// Attaches a dynamically compiled MonoBehaviour to a GameObject
    /// </summary>
    public bool AttachMonoBehaviour(GameObject host, out string runtimeError)
    {
        runtimeError = null;
        
        if (host == null)
        {
            runtimeError = "Target GameObject is null.";
            return false;
        }

        if (entryType == null || !typeof(MonoBehaviour).IsAssignableFrom(entryType))
        {
            runtimeError = $"Type '{entryTypeName}' is not a MonoBehaviour.";
            return false;
        }

        try
        {
            // Check if component already exists
            var existing = host.GetComponent(entryType);
            if (existing != null)
            {
                Debug.LogWarning($"Component '{entryType.Name}' already exists on '{host.name}'. Removing old instance.");
                if (Application.isPlaying)
                    Destroy(existing);
                else
                    DestroyImmediate(existing);
            }

            // Add the component
            attachedComponent = host.AddComponent(entryType);
            
            if (attachedComponent == null)
            {
                runtimeError = "Failed to add component to GameObject.";
                return false;
            }

            Debug.Log($"Successfully attached '{entryType.Name}' to '{host.name}'");
            return true;
        }
        catch (Exception ex)
        {
            runtimeError = ex.ToString();
            Debug.LogError("Failed to attach MonoBehaviour: " + runtimeError);
            return false;
        }
    }

    /// <summary>
    /// Invokes a coroutine on the compiled type if it returns IEnumerator
    /// </summary>
    public bool InvokeCoroutine(MonoBehaviour host, out string runtimeError)
    {
        runtimeError = null;
        
        if (entryMethod == null)
        {
            runtimeError = "No entry method found.";
            return false;
        }

        if (!typeof(System.Collections.IEnumerator).IsAssignableFrom(entryMethod.ReturnType))
        {
            runtimeError = $"Method '{entryMethodName}' does not return IEnumerator.";
            return false;
        }

        try
        {
            var parameters = entryMethod.GetParameters();
            object result = null;

            if (parameters.Length == 0)
            {
                result = entryMethod.Invoke(null, null);
            }
            else if (parameters.Length == 1)
            {
                var pType = parameters[0].ParameterType;
                if (pType == typeof(GameObject))
                    result = entryMethod.Invoke(null, new object[] { host.gameObject });
                else if (typeof(MonoBehaviour).IsAssignableFrom(pType))
                    result = entryMethod.Invoke(null, new object[] { host });
                else
                    result = entryMethod.Invoke(null, new object[] { host });
            }

            if (result is System.Collections.IEnumerator coroutine)
            {
                host.StartCoroutine(coroutine);
                Debug.Log($"Started coroutine '{entryMethodName}' on '{host.name}'");
                return true;
            }
            else
            {
                runtimeError = "Method did not return a valid IEnumerator.";
                return false;
            }
        }
        catch (Exception ex)
        {
            runtimeError = ex.ToString();
            Debug.LogError("Failed to start coroutine: " + runtimeError);
            return false;
        }
    }

    /// <summary>
    /// MCP-callable function: Compiles code and optionally attaches to a GameObject
    /// </summary>
    /// <param name="sourceCode">C# source code to compile</param>
    /// <param name="typeName">Type name to instantiate/invoke</param>
    /// <param name="methodName">Method name to invoke (for static methods)</param>
    /// <param name="targetObject">Target GameObject (null = this.gameObject)</param>
    /// <param name="shouldAttachComponent">If true and type is MonoBehaviour, attach as component</param>
    /// <param name="errorMessage">Output error message if operation fails</param>
    /// <returns>True if successful, false otherwise</returns>
    public bool CompileAndExecute(
        string sourceCode, 
        string typeName, 
        string methodName, 
        GameObject targetObject, 
        bool shouldAttachComponent,
        out string errorMessage)
    {
        errorMessage = null;

        // Validate inputs
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            errorMessage = "Source code cannot be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(typeName))
        {
            errorMessage = "Type name cannot be empty.";
            return false;
        }

        // Set properties
        code = sourceCode;
        entryTypeName = typeName;
        entryMethodName = string.IsNullOrWhiteSpace(methodName) ? "Run" : methodName;
        attachAsComponent = shouldAttachComponent;
        targetGameObject = targetObject;

        // Determine target GameObject first
        GameObject target = targetGameObject != null ? targetGameObject : this.gameObject;
        string targetName = target != null ? target.name : "null";
        
        // Compile
        if (!CompileInMemory(out string compileError))
        {
            errorMessage = $"Compilation failed:\n{compileError}";
            AddHistoryEntry(sourceCode, typeName, entryMethodName, false, compileError, targetName);
            return false;
        }

        if (target == null)
        {
            errorMessage = "No target GameObject available.";
            AddHistoryEntry(sourceCode, typeName, entryMethodName, false, "No target GameObject", "null");
            return false;
        }

        // Execute based on type
        try
        {
            // MonoBehaviour attachment
            if (shouldAttachComponent && entryType != null && typeof(MonoBehaviour).IsAssignableFrom(entryType))
            {
                if (!AttachMonoBehaviour(target, out string attachError))
                {
                    errorMessage = $"Failed to attach MonoBehaviour:\n{attachError}";
                    AddHistoryEntry(sourceCode, typeName, entryMethodName, false, attachError, target.name);
                    return false;
                }
                
                Debug.Log($"[MCP] MonoBehaviour '{typeName}' successfully attached to '{target.name}'");
                AddHistoryEntry(sourceCode, typeName, entryMethodName, true, "Component attached successfully", target.name);
                return true;
            }
            
            // Coroutine invocation
            if (entryMethod != null && typeof(System.Collections.IEnumerator).IsAssignableFrom(entryMethod.ReturnType))
            {
                var host = target.GetComponent<MonoBehaviour>() ?? this;
                if (!InvokeCoroutine(host, out string coroutineError))
                {
                    errorMessage = $"Failed to start coroutine:\n{coroutineError}";
                    AddHistoryEntry(sourceCode, typeName, entryMethodName, false, coroutineError, target.name);
                    return false;
                }
                
                Debug.Log($"[MCP] Coroutine '{methodName}' started on '{target.name}'");
                AddHistoryEntry(sourceCode, typeName, entryMethodName, true, "Coroutine started successfully", target.name);
                return true;
            }
            
            // Static method invocation
            if (!InvokeEntry(target, out string invokeError))
            {
                errorMessage = $"Failed to invoke method:\n{invokeError}";
                AddHistoryEntry(sourceCode, typeName, entryMethodName, false, invokeError, target.name);
                return false;
            }
            
            Debug.Log($"[MCP] Method '{methodName}' executed successfully on '{target.name}'");
            AddHistoryEntry(sourceCode, typeName, entryMethodName, true, "Method executed successfully", target.name);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Execution error:\n{ex.Message}\n{ex.StackTrace}";
            return false;
        }
    }

    /// <summary>
    /// Simplified MCP-callable function with default parameters
    /// </summary>
    public bool CompileAndExecute(string sourceCode, string typeName, GameObject targetObject, out string errorMessage)
    {
        // Auto-detect if it's a MonoBehaviour by checking the source
        bool shouldAttach = sourceCode.Contains(": MonoBehaviour") || sourceCode.Contains(":MonoBehaviour");
        return CompileAndExecute(sourceCode, typeName, "Run", targetObject, shouldAttach, out errorMessage);
    }

    /// <summary>
    /// MCP-callable: Compile and attach to current GameObject
    /// </summary>
    public bool CompileAndAttachToSelf(string sourceCode, string typeName, out string errorMessage)
    {
        return CompileAndExecute(sourceCode, typeName, "Run", this.gameObject, true, out errorMessage);
    }

    // helper: convenience method to compile + run on this.gameObject

    /// <summary>Compiles the current source and runs it against this component's own GameObject, reporting any error.</summary>
    public void CompileAndRunOnSelf()
    {
        if (CompileInMemory(out var diag))
        {
            if (!Application.isPlaying)
                Debug.LogWarning("Running compiled code in Edit Mode. Some UnityEngine APIs may not behave as expected.");

            GameObject target = targetGameObject != null ? targetGameObject : this.gameObject;

            // Check if we should attach as component
            if (attachAsComponent && entryType != null && typeof(MonoBehaviour).IsAssignableFrom(entryType))
            {
                if (AttachMonoBehaviour(target, out var attachErr))
                {
                    Debug.Log($"MonoBehaviour '{entryTypeName}' attached successfully to '{target.name}'.");
                }
                else
                {
                    Debug.LogError("Failed to attach MonoBehaviour: " + attachErr);
                }
            }
            // Check if it's a coroutine
            else if (entryMethod != null && typeof(System.Collections.IEnumerator).IsAssignableFrom(entryMethod.ReturnType))
            {
                var host = target.GetComponent<MonoBehaviour>() ?? this;
                if (InvokeCoroutine(host, out var coroutineErr))
                {
                    Debug.Log("Coroutine started successfully.");
                }
                else
                {
                    Debug.LogError("Failed to start coroutine: " + coroutineErr);
                }
            }
            // Regular static method invocation
            else if (InvokeEntry(target, out var runtimeErr))
            {
                Debug.Log("Entry invoked successfully.");
            }
            else
            {
                Debug.LogError("Failed to invoke entry: " + runtimeErr);
            }
        }
        else
        {
            Debug.LogError("Compile failed: " + lastCompileDiagnostics);
        }
    }
    
    /// <summary>
    /// Adds an entry to the compilation history
    /// </summary>
    private void AddHistoryEntry(string sourceCode, string typeName, string methodName, bool success, string diagnostics, string target)
    {
        if (!enableHistory) return;
        
        var entry = new CompilationHistoryEntry
        {
            timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            sourceCode = sourceCode,
            typeName = typeName,
            methodName = methodName,
            success = success,
            diagnostics = diagnostics,
            executionTarget = target
        };
        
        _sharedHistory.Add(entry);
        
        // Trim if exceeded max
        while (_sharedHistory.Count > maxHistoryEntries)
        {
            _sharedHistory.RemoveAt(0);
        }
    }
    
    /// <summary>
    /// Saves the compilation history to a JSON file outside Assets
    /// </summary>
    public bool SaveHistoryToFile(out string savedPath, out string error)
    {
        error = "";
        savedPath = "";
        
        try
        {
            string projectRoot = Application.dataPath.Replace("/Assets", "").Replace("\\Assets", "");
            string historyDir = System.IO.Path.Combine(projectRoot, "RoslynHistory");
            
            if (!System.IO.Directory.Exists(historyDir))
            {
                System.IO.Directory.CreateDirectory(historyDir);
            }
            
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filename = $"RoslynHistory_{timestamp}.json";
            savedPath = System.IO.Path.Combine(historyDir, filename);
            
            string json = JsonUtility.ToJson(new HistoryWrapper { entries = _sharedHistory }, true);
            System.IO.File.WriteAllText(savedPath, json);
            
            Debug.Log($"[RuntimeRoslynDemo] Saved {_sharedHistory.Count} history entries to: {savedPath}");
            return true;
        }
        catch (System.Exception ex)
        {
            error = ex.Message;
            Debug.LogError($"[RuntimeRoslynDemo] Failed to save history: {error}");
            return false;
        }
    }
    
    /// <summary>
    /// Saves a specific history entry as a standalone .cs file outside Assets
    /// </summary>
    public bool SaveHistoryEntryAsScript(int index, out string savedPath, out string error)
    {
        error = "";
        savedPath = "";
        
        if (index < 0 || index >= _sharedHistory.Count)
        {
            error = "Invalid history index";
            return false;
        }
        
        try
        {
            var entry = _sharedHistory[index];
            string projectRoot = Application.dataPath.Replace("/Assets", "").Replace("\\Assets", "");
            string scriptsDir = System.IO.Path.Combine(projectRoot, "RoslynHistory", "Scripts");
            
            if (!System.IO.Directory.Exists(scriptsDir))
            {
                System.IO.Directory.CreateDirectory(scriptsDir);
            }
            
            string timestamp = System.DateTime.Parse(entry.timestamp).ToString("yyyyMMdd_HHmmss");
            string filename = $"{entry.typeName}_{timestamp}.cs";
            savedPath = System.IO.Path.Combine(scriptsDir, filename);
            
            // Add header comment
            string header = $"// Roslyn Runtime Compiled Script\n// Original Timestamp: {entry.timestamp}\n// Type: {entry.typeName}\n// Method: {entry.methodName}\n// Success: {entry.success}\n// Target: {entry.executionTarget}\n\n";
            
            System.IO.File.WriteAllText(savedPath, header + entry.sourceCode);
            
            Debug.Log($"[RuntimeRoslynDemo] Saved script to: {savedPath}");
            return true;
        }
        catch (System.Exception ex)
        {
            error = ex.Message;
            Debug.LogError($"[RuntimeRoslynDemo] Failed to save script: {error}");
            return false;
        }
    }
    
    /// <summary>
    /// Clears the compilation history
    /// </summary>
    public void ClearHistory()
    {
        _sharedHistory.Clear();
        Debug.Log("[RuntimeRoslynDemo] Compilation history cleared");
    }
    
    [System.Serializable]
    private class HistoryWrapper
    {
        public System.Collections.Generic.List<CompilationHistoryEntry> entries;
    }
}

/// <summary>
/// Static helper class for MCP tools to compile and execute C# code at runtime
/// </summary>
public static class RoslynMCPHelper
{
    private static RoslynRuntimeCompiler _compiler;
    
    /// <summary>
    /// Get or create the runtime compiler instance
    /// </summary>
    private static RoslynRuntimeCompiler GetOrCreateCompiler()
    {
        if (_compiler == null || _compiler.gameObject == null)
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<RoslynRuntimeCompiler>();
            if (existing != null)
            {
                _compiler = existing;
            }
            else
            {
                var go = new GameObject("MCPRoslynCompiler");
                _compiler = go.AddComponent<RoslynRuntimeCompiler>();
                if (!Application.isPlaying)
                {
                    go.hideFlags = HideFlags.HideAndDontSave;
                }
            }
        }
        return _compiler;
    }

    /// <summary>
    /// MCP Entry Point: Compile C# code and attach to a GameObject
    /// </summary>
    /// <param name="sourceCode">Complete C# source code</param>
    /// <param name="className">Name of the class to instantiate</param>
    /// <param name="targetGameObjectName">Name of GameObject to attach to (null = create new)</param>
    /// <param name="result">Output result message</param>
    /// <returns>True if successful</returns>
    public static bool CompileAndAttach(string sourceCode, string className, string targetGameObjectName, out string result)
    {
        try
        {
            var compiler = GetOrCreateCompiler();
            
            // Find or create target GameObject
            GameObject target = null;
            if (!string.IsNullOrEmpty(targetGameObjectName))
            {
                target = GameObject.Find(targetGameObjectName);
                if (target == null)
                {
                    result = $"GameObject '{targetGameObjectName}' not found.";
                    return false;
                }
            }
            else
            {
                // Create a new GameObject for the script
                target = new GameObject($"Generated_{className}");
                UnityEngine.Debug.Log($"[MCP] Created new GameObject: {target.name}");
            }

            // Compile and execute
            bool success = compiler.CompileAndExecute(sourceCode, className, target, out string error);
            
            if (success)
            {
                result = $"Successfully compiled and attached '{className}' to '{target.name}'";
                UnityEngine.Debug.Log($"[MCP] {result}");
                return true;
            }
            else
            {
                result = $"Failed: {error}";
                UnityEngine.Debug.LogError($"[MCP] {result}");
                return false;
            }
        }
        catch (Exception ex)
        {
            result = $"Exception: {ex.Message}";
            UnityEngine.Debug.LogError($"[MCP] {result}\n{ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// MCP Entry Point: Compile and execute static method
    /// </summary>
    /// <param name="sourceCode">Complete C# source code</param>
    /// <param name="className">Name of the class containing the method</param>
    /// <param name="methodName">Name of the static method to invoke</param>
    /// <param name="targetGameObjectName">GameObject to pass as parameter (optional)</param>
    /// <param name="result">Output result message</param>
    /// <returns>True if successful</returns>
    public static bool CompileAndExecuteStatic(string sourceCode, string className, string methodName, string targetGameObjectName, out string result)
    {
        try
        {
            var compiler = GetOrCreateCompiler();
            
            GameObject target = compiler.gameObject;
            if (!string.IsNullOrEmpty(targetGameObjectName))
            {
                var found = GameObject.Find(targetGameObjectName);
                if (found != null)
                {
                    target = found;
                }
            }

            bool success = compiler.CompileAndExecute(sourceCode, className, methodName, target, false, out string error);
            
            if (success)
            {
                result = $"Successfully compiled and executed '{className}.{methodName}'";
                UnityEngine.Debug.Log($"[MCP] {result}");
                return true;
            }
            else
            {
                result = $"Failed: {error}";
                UnityEngine.Debug.LogError($"[MCP] {result}");
                return false;
            }
        }
        catch (Exception ex)
        {
            result = $"Exception: {ex.Message}";
            UnityEngine.Debug.LogError($"[MCP] {result}\n{ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// MCP Entry Point: Quick compile and attach MonoBehaviour
    /// </summary>
    /// <param name="sourceCode">MonoBehaviour source code</param>
    /// <param name="className">MonoBehaviour class name</param>
    /// <param name="gameObjectName">Target GameObject name (creates if null)</param>
    /// <returns>Success status message</returns>
    public static string QuickAttachScript(string sourceCode, string className, string gameObjectName = null)
    {
        bool success = CompileAndAttach(sourceCode, className, gameObjectName, out string result);
        return result;
    }

    /// <summary>
    /// MCP Entry Point: Execute code snippet with minimal parameters
    /// </summary>
    public static string ExecuteCode(string sourceCode, string className = "AIGenerated")
    {
        bool success = CompileAndExecuteStatic(sourceCode, className, "Run", null, out string result);
        return result;
    }
}

#if UNITY_EDITOR
// Editor window

/// <summary>Editor window that edits and runs the Roslyn compiler component's code from a text area.</summary>
public class RoslynRuntimeCompilerWindow : EditorWindow
{
    private RoslynRuntimeCompiler helperInScene;
    private Vector2 scrollPos;
    private Vector2 diagScroll;
    private Vector2 historyScroll;
    private int selectedTab = 0;
    private string[] tabNames = { "Compiler", "History" };
    private int selectedHistoryIndex = -1;
    private Vector2 historyCodeScroll;

    // Editor UI state
    private string codeText = string.Empty;
    private string typeName = "AIGenerated";
    private string methodName = "Run";
    private bool attachAsComponent = false;
    private GameObject targetGameObject = null;

    /// <summary>Opens (or focuses) the Roslyn Runtime Compiler editor window.</summary>
    [MenuItem("Window/Roslyn Runtime Compiler")]
    public static void ShowWindow()
    {
        var w = GetWindow<RoslynRuntimeCompilerWindow>("Roslyn Runtime Compiler");
        w.minSize = new Vector2(600, 400);
    }

    void OnEnable()
    {
        // try to find an existing helper in scene
        helperInScene = FindFirstObjectByType<RoslynRuntimeCompiler>(FindObjectsInactive.Include);
        if (helperInScene == null)
        {
            var go = new GameObject("RoslynRuntimeHelper");
            helperInScene = go.AddComponent<RoslynRuntimeCompiler>();
            // Don't save this helper into scene assets
            go.hideFlags = HideFlags.HideAndDontSave;
        }

        if (helperInScene != null)
        {
            codeText = helperInScene.code;
            typeName = helperInScene.entryTypeName;
            methodName = helperInScene.entryMethodName;
            attachAsComponent = helperInScene.attachAsComponent;
            targetGameObject = helperInScene.targetGameObject;
        }
    }

    void OnDisable()
    {
        // keep editor text back to helper if it still exists
        if (helperInScene != null && helperInScene.gameObject != null)
        {
            helperInScene.code = codeText;
            helperInScene.entryTypeName = typeName;
            helperInScene.entryMethodName = methodName;
            helperInScene.attachAsComponent = attachAsComponent;
            helperInScene.targetGameObject = targetGameObject;
        }
    }
    
    void OnDestroy()
    {
        // Clean up helper object when window is destroyed
        if (helperInScene != null && helperInScene.gameObject != null)
        {
            DestroyImmediate(helperInScene.gameObject);
            helperInScene = null;
        }
    }

    void OnGUI()
    {
        // Ensure helper exists before drawing GUI - recreate if needed
        if (helperInScene == null || helperInScene.gameObject == null)
        {
            // Try to find existing helper first
            helperInScene = FindFirstObjectByType<RoslynRuntimeCompiler>(FindObjectsInactive.Include);
            
            // If still not found, create a new one
            if (helperInScene == null)
            {
                var go = new GameObject("RoslynRuntimeHelper");
                helperInScene = go.AddComponent<RoslynRuntimeCompiler>();
                go.hideFlags = HideFlags.HideAndDontSave;
                
                // Initialize with default values
                helperInScene.code = codeText;
                helperInScene.entryTypeName = typeName;
                helperInScene.entryMethodName = methodName;
                helperInScene.attachAsComponent = attachAsComponent;
                helperInScene.targetGameObject = targetGameObject;
            }
            else
            {
                // Load state from found helper
                codeText = helperInScene.code;
                typeName = helperInScene.entryTypeName;
                methodName = helperInScene.entryMethodName;
                attachAsComponent = helperInScene.attachAsComponent;
                targetGameObject = helperInScene.targetGameObject;
            }
        }

        EditorGUILayout.LabelField("Roslyn Runtime Compiler (Editor)", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // Tab selector
        selectedTab = GUILayout.Toolbar(selectedTab, tabNames);
        EditorGUILayout.Space();
        
        if (selectedTab == 0)
        {
            DrawCompilerTab();
        }
        else if (selectedTab == 1)
        {
            DrawHistoryTab();
        }
    }
    
    void DrawCompilerTab()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Entry Type:", GUILayout.Width(70));
        typeName = EditorGUILayout.TextField(typeName);
        EditorGUILayout.LabelField("Method:", GUILayout.Width(50));
        methodName = EditorGUILayout.TextField(methodName, GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        attachAsComponent = EditorGUILayout.Toggle("Attach as Component", attachAsComponent, GUILayout.Width(200));
        if (attachAsComponent)
        {
            EditorGUILayout.LabelField("Target:", GUILayout.Width(45));
            targetGameObject = (GameObject)EditorGUILayout.ObjectField(targetGameObject, typeof(GameObject), true);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Code (paste LLM output here):");
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(position.height * 0.55f));
        codeText = EditorGUILayout.TextArea(codeText, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Compile"))
        {
            ApplyToHelper();
            if (helperInScene != null)
            {
                var ok = helperInScene.CompileInMemory(out var diag);
                Debug.Log(ok ? "Compile OK" : "Compile Failed\n" + diag);
            }
        }

        bool canRun = helperInScene != null && helperInScene.HasCompiledAssembly && 
                      (helperInScene.HasEntryMethod || (helperInScene.HasEntryType && typeof(MonoBehaviour).IsAssignableFrom(helperInScene.EntryType)));
        GUI.enabled = canRun;
        if (GUILayout.Button("Run (invoke on selected)"))
        {
            ApplyToHelper();
            var sel = Selection.activeGameObject;
            if (sel == null && helperInScene != null && helperInScene.gameObject != null)
                sel = helperInScene.gameObject;
                
            if (sel != null && helperInScene != null)
            {
                if (helperInScene.InvokeEntry(sel, out var runtimeErr))
                    Debug.Log("Invocation OK on: " + sel.name);
                else
                    Debug.LogError("Invocation failed: " + runtimeErr);
            }
        }

        GUI.enabled = true;
        if (GUILayout.Button("Compile & Run on helper"))
        {
            ApplyToHelper();
            if (helperInScene != null)
            {
                helperInScene.CompileAndRunOnSelf();
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Diagnostics:");
        diagScroll = EditorGUILayout.BeginScrollView(diagScroll, GUILayout.Height(120));
        string diagnosticsText = (helperInScene != null && helperInScene.lastCompileDiagnostics != null) 
            ? helperInScene.lastCompileDiagnostics 
            : "No diagnostics available.";
        EditorGUILayout.HelpBox(diagnosticsText, MessageType.Info);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Notes:");
        EditorGUILayout.HelpBox("This compiles code in-memory using Roslyn. Do not write .cs files into Assets while running. Generated code runs with editor permissions.\n\n" +
            "Supported patterns:\n" +
            "1. Static method: public static void Run(GameObject host)\n" +
            "2. MonoBehaviour: Enable 'Attach as Component' for classes inheriting MonoBehaviour\n" +
            "3. Coroutine: public static IEnumerator RunCoroutine(MonoBehaviour host)\n" +
            "4. Parameterless: public static void Run()", MessageType.None);
    }
    
    void DrawHistoryTab()
    {
        if (helperInScene == null) return;
        
        var history = helperInScene.CompilationHistory;
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Compilation History ({history.Count} entries)", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Save History JSON", GUILayout.Width(140)))
        {
            if (helperInScene.SaveHistoryToFile(out string path, out string error))
            {
                EditorUtility.DisplayDialog("Success", $"History saved to:\n{path}", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Error", $"Failed to save history:\n{error}", "OK");
            }
        }
        
        if (GUILayout.Button("Clear History", GUILayout.Width(100)))
        {
            if (EditorUtility.DisplayDialog("Clear History", "Are you sure you want to clear all compilation history?", "Yes", "No"))
            {
                helperInScene.ClearHistory();
                selectedHistoryIndex = -1;
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        if (history.Count == 0)
        {
            EditorGUILayout.HelpBox("No compilation history yet. Compile and run scripts to see them here.", MessageType.Info);
            return;
        }
        
        EditorGUILayout.BeginHorizontal();
        
        // Left panel - history list
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.4f));
        EditorGUILayout.LabelField("History Entries:", EditorStyles.boldLabel);
        historyScroll = EditorGUILayout.BeginScrollView(historyScroll);
        
        for (int i = history.Count - 1; i >= 0; i--) // Reverse order (newest first)
        {
            var entry = history[i];
            GUIStyle entryStyle = new GUIStyle(GUI.skin.button);
            entryStyle.alignment = TextAnchor.MiddleLeft;
            entryStyle.normal.textColor = entry.success ? Color.green : Color.red;
            
            if (selectedHistoryIndex == i)
            {
                entryStyle.normal.background = Texture2D.grayTexture;
            }
            
            string label = $"[{i}] {entry.timestamp} - {entry.typeName}.{entry.methodName}";
            if (entry.success)
                label += " ✓";
            else
                label += " ✗";
                
            if (GUILayout.Button(label, entryStyle, GUILayout.Height(30)))
            {
                selectedHistoryIndex = i;
            }
        }
        
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        
        // Right panel - selected entry details
        EditorGUILayout.BeginVertical();
        
        if (selectedHistoryIndex >= 0 && selectedHistoryIndex < history.Count)
        {
            var entry = history[selectedHistoryIndex];
            
            EditorGUILayout.LabelField("Entry Details:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Timestamp:", entry.timestamp);
            EditorGUILayout.LabelField("Type:", entry.typeName);
            EditorGUILayout.LabelField("Method:", entry.methodName);
            EditorGUILayout.LabelField("Target:", entry.executionTarget);
            EditorGUILayout.LabelField("Success:", entry.success ? "Yes" : "No");
            
            EditorGUILayout.Space();
            
            if (!string.IsNullOrEmpty(entry.diagnostics))
            {
                EditorGUILayout.LabelField("Diagnostics:");
                EditorGUILayout.HelpBox(entry.diagnostics, entry.success ? MessageType.Info : MessageType.Error);
            }
            
            EditorGUILayout.Space();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load to Compiler", GUILayout.Height(25)))
            {
                codeText = entry.sourceCode;
                typeName = entry.typeName;
                methodName = entry.methodName;
                selectedTab = 0; // Switch to compiler tab
            }
            
            if (GUILayout.Button("Save as .cs File", GUILayout.Height(25)))
            {
                if (helperInScene.SaveHistoryEntryAsScript(selectedHistoryIndex, out string path, out string error))
                {
                    EditorUtility.DisplayDialog("Success", $"Script saved to:\n{path}", "OK");
                    EditorUtility.RevealInFinder(path);
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", $"Failed to save script:\n{error}", "OK");
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Source Code:");
            historyCodeScroll = EditorGUILayout.BeginScrollView(historyCodeScroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(entry.sourceCode, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.HelpBox("Select a history entry to view details.", MessageType.Info);
        }
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.EndHorizontal();
    }

    void ApplyToHelper()
    {
        if (helperInScene == null || helperInScene.gameObject == null)
        {
            Debug.LogError("Helper object is missing or destroyed. Cannot apply settings.");
            return;
        }

        helperInScene.code = codeText;
        helperInScene.entryTypeName = typeName;
        helperInScene.entryMethodName = methodName;
        helperInScene.attachAsComponent = attachAsComponent;
        helperInScene.targetGameObject = targetGameObject;
    }
}
#endif
