using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Validates a C# script for structural issues (brace balance, duplicate methods).
    /// C# handler mirrors the Python-side validate_script tool so batch_execute can dispatch it.
    /// </summary>
    [McpForUnityTool("validate_script", AutoRegister = false, Group = "core")]
    public static class ValidateScript
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);

            var uriResult = p.GetRequired("uri");
            if (!uriResult.IsSuccess) return new ErrorResponse(uriResult.ErrorMessage);
            string uri = uriResult.Value;

            string level = p.Get("level", "basic");
            bool includeDiagnostics = p.GetBool("include_diagnostics", false);

            string filePath = ResolveUri(uri);
            if (!File.Exists(filePath))
                return new ErrorResponse($"Script not found: '{uri}'");

            try
            {
                string content = File.ReadAllText(filePath);
                var diagnostics = new List<object>();

                // Brace balance check
                int braceCount = 0;
                for (int i = 0; i < content.Length; i++)
                {
                    if (content[i] == '{') braceCount++;
                    else if (content[i] == '}') braceCount--;
                }
                if (braceCount != 0)
                {
                    diagnostics.Add(new
                    {
                        line = 0, col = 0, severity = "error",
                        message = $"Unbalanced braces: {(braceCount > 0 ? $"{braceCount} unclosed" : $"{-braceCount} extra closing")}."
                    });
                }

                // Duplicate method signature detection (standard level)
                if (level == "standard")
                {
                    var methodPattern = new Regex(
                        @"(?:public|private|protected|internal|static|\s)+\s+\w+[\w<>\[\],\s]*\s+(\w+)\s*\(([^)]*)\)",
                        RegexOptions.Multiline);
                    var signatures = new Dictionary<string, int>();
                    var matches = methodPattern.Matches(content);

                    foreach (Match m in matches)
                    {
                        string methodName = m.Groups[1].Value;
                        string paramList = m.Groups[2].Value.Trim();
                        int paramCount = string.IsNullOrEmpty(paramList) ? 0 : paramList.Split(',').Length;
                        string sig = $"{methodName}({paramCount})";

                        if (signatures.ContainsKey(sig))
                        {
                            diagnostics.Add(new
                            {
                                line = 0, col = 0, severity = "error",
                                message = $"Duplicate method signature detected: '{methodName}' with {paramCount} parameter(s). This may indicate a corrupted edit."
                            });
                        }
                        else
                        {
                            signatures[sig] = 1;
                        }
                    }
                }

                if (diagnostics.Count > 0)
                {
                    return new ErrorResponse("Validation failed.",
                        new { diagnostics });
                }

                if (includeDiagnostics)
                {
                    return new SuccessResponse("Validation passed.", new
                    {
                        diagnostics = new List<object>(),
                        summary = "No issues found."
                    });
                }

                return new SuccessResponse("Validation passed.");
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error validating script: {e.Message}");
            }
        }

        private static string ResolveUri(string uri)
        {
            if (uri.StartsWith("mcpforunity://path/", StringComparison.OrdinalIgnoreCase))
                uri = uri.Substring("mcpforunity://path/".Length);
            else if (uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                uri = uri.Substring("file://".Length);

            if (!Path.IsPathRooted(uri))
                uri = Path.Combine(UnityEngine.Application.dataPath, "..", uri);

            return Path.GetFullPath(uri);
        }
    }
}
