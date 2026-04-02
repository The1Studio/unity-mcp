using System;
using System.IO;
using System.Security.Cryptography;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Returns SHA256 hash and file size for a Unity script.
    /// C# handler mirrors the Python-side get_sha tool so batch_execute can dispatch it.
    /// </summary>
    [McpForUnityTool("get_sha", AutoRegister = false, Group = "core")]
    public static class GetSha
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);

            var uriResult = p.GetRequired("uri");
            if (!uriResult.IsSuccess) return new ErrorResponse(uriResult.ErrorMessage);
            string uri = uriResult.Value;

            string filePath = ResolveUri(uri);
            if (!File.Exists(filePath))
                return new ErrorResponse($"File not found: '{uri}'");

            try
            {
                byte[] fileBytes = File.ReadAllBytes(filePath);

                using (var sha256 = SHA256.Create())
                {
                    byte[] hash = sha256.ComputeHash(fileBytes);
                    string hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                    return new SuccessResponse("SHA256 computed.", new
                    {
                        sha256 = hex,
                        lengthBytes = fileBytes.Length
                    });
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error computing SHA256: {e.Message}");
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
