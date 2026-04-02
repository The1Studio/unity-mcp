using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Searches a file with a regex pattern and returns line numbers and excerpts.
    /// C# handler mirrors the Python-side find_in_file tool so batch_execute can dispatch it.
    /// </summary>
    [McpForUnityTool("find_in_file", AutoRegister = false, Group = "core")]
    public static class FindInFile
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);

            var uriResult = p.GetRequired("uri");
            if (!uriResult.IsSuccess) return new ErrorResponse(uriResult.ErrorMessage);
            string uri = uriResult.Value;

            var patternResult = p.GetRequired("pattern");
            if (!patternResult.IsSuccess) return new ErrorResponse(patternResult.ErrorMessage);
            string pattern = patternResult.Value;

            bool ignoreCase = p.GetBool("ignore_case", true);
            int maxResults = p.GetInt("max_results") ?? 200;

            string filePath = ResolveUri(uri);
            if (!File.Exists(filePath))
                return new ErrorResponse($"File not found: '{uri}'");

            try
            {
                var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
                var regex = new Regex(pattern, options);
                var lines = File.ReadAllLines(filePath);
                var matches = new List<object>();
                int totalMatches = 0;

                for (int i = 0; i < lines.Length && matches.Count < maxResults; i++)
                {
                    var lineMatches = regex.Matches(lines[i]);
                    foreach (Match m in lineMatches)
                    {
                        totalMatches++;
                        if (matches.Count < maxResults)
                        {
                            matches.Add(new
                            {
                                line = i + 1,
                                content = lines[i].TrimEnd(),
                                match = m.Value,
                                start = m.Index,
                                end = m.Index + m.Length
                            });
                        }
                    }
                }

                return new SuccessResponse("Search complete.", new
                {
                    matches,
                    count = matches.Count,
                    total_matches = totalMatches
                });
            }
            catch (ArgumentException e)
            {
                return new ErrorResponse($"Invalid regex pattern: {e.Message}");
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error searching file: {e.Message}");
            }
        }

        private static string ResolveUri(string uri)
        {
            // Strip URI prefixes
            if (uri.StartsWith("mcpforunity://path/", StringComparison.OrdinalIgnoreCase))
                uri = uri.Substring("mcpforunity://path/".Length);
            else if (uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                uri = uri.Substring("file://".Length);

            // If relative (Assets/...), resolve against project root
            if (!Path.IsPathRooted(uri))
                uri = Path.Combine(UnityEngine.Application.dataPath, "..", uri);

            return Path.GetFullPath(uri);
        }
    }
}
