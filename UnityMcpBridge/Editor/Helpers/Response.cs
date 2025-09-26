using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MCPForUnity.Editor.Helpers
{
    /// <summary>
    /// Provides static methods for creating standardized success and error response objects.
    /// Ensures consistent JSON structure for communication back to the Python server.
    /// </summary>
    public static class Response
    {
        /// <summary>
        /// Creates a standardized success response object.
        /// </summary>
        /// <param name="message">A message describing the successful operation.</param>
        /// <param name="data">Optional additional data to include in the response.</param>
        /// <returns>An object representing the success response.</returns>
        public static object Success(string message, object data = null)
        {
            if (data != null)
            {
                return new
                {
                    success = true,
                    message,
                    data,
                };
            }
            else
            {
                return new { success = true, message };
            }
        }

        /// <summary>
        /// Creates a standardized error response object.
        /// </summary>
        /// <param name="errorCodeOrMessage">A message describing the error.</param>
        /// <param name="data">Optional additional data (e.g., error details) to include.</param>
        /// <returns>An object representing the error response.</returns>
        public static object Error(string errorCodeOrMessage, object data = null)
        {
            if (data != null)
            {
                // Note: The key is "error" for error messages, not "message"
                return new
                {
                    success = false,
                    // Preserve original behavior while adding a machine-parsable code field.
                    // If callers pass a code string, it will be echoed in both code and error.
                    code = errorCodeOrMessage,
                    error = errorCodeOrMessage,
                    data,
                };
            }
            else
            {
                return new { success = false, code = errorCodeOrMessage, error = errorCodeOrMessage };
            }
        }

        /// <summary>
        /// Creates an enhanced error response with context, suggestions, and related information.
        /// STUDIO: Enhanced error reporting for better AI assistant interaction.
        /// </summary>
        /// <param name="message">Primary error message</param>
        /// <param name="context">Contextual information about what was being attempted</param>
        /// <param name="suggestion">Actionable suggestion to resolve the error</param>
        /// <param name="relatedItems">Array of related items (files, assets, etc.)</param>
        /// <param name="errorCode">Machine-parsable error code</param>
        /// <param name="filePath">File path where error occurred (if applicable)</param>
        /// <param name="lineNumber">Line number where error occurred (if applicable)</param>
        /// <returns>Enhanced error response object</returns>
        public static object EnhancedError(
            string message,
            string context = null,
            string suggestion = null,
            string[] relatedItems = null,
            string errorCode = null,
            string filePath = null,
            int? lineNumber = null)
        {
            var errorDetails = new Dictionary<string, object>
            {
                { "timestamp", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC") },
                { "unity_version", Application.unityVersion },
                { "platform", Application.platform.ToString() },
            };

            if (!string.IsNullOrEmpty(context))
                errorDetails["context"] = context;
            
            if (!string.IsNullOrEmpty(suggestion))
                errorDetails["suggestion"] = suggestion;
            
            if (relatedItems != null && relatedItems.Length > 0)
                errorDetails["related_items"] = relatedItems;
            
            if (!string.IsNullOrEmpty(filePath))
                errorDetails["file_path"] = filePath;
            
            if (lineNumber.HasValue)
                errorDetails["line_number"] = lineNumber.Value;

            return new
            {
                success = false,
                error = message,
                code = errorCode ?? "STUDIO_ERROR",
                error_details = errorDetails,
            };
        }

        /// <summary>
        /// Creates an enhanced error response for asset-related operations.
        /// STUDIO: Specialized error reporting for asset operations.
        /// </summary>
        public static object AssetError(string message, string assetPath, string assetType = null, string[] suggestions = null)
        {
            var context = $"Asset operation on '{assetPath}'";
            if (!string.IsNullOrEmpty(assetType))
                context += $" (type: {assetType})";

            var suggestion = "Check asset path and permissions.";
            if (suggestions != null && suggestions.Length > 0)
                suggestion = string.Join(" ", suggestions);

            var relatedItems = GetSimilarAssets(assetPath);
            
            return EnhancedError(message, context, suggestion, relatedItems, "ASSET_ERROR", assetPath);
        }

        /// <summary>
        /// Creates an enhanced error response for script-related operations.
        /// STUDIO: Specialized error reporting for script operations.
        /// </summary>
        public static object ScriptError(string message, string scriptPath, int? lineNumber = null, string[] suggestions = null)
        {
            var context = $"Script operation on '{scriptPath}'";
            if (lineNumber.HasValue)
                context += $" at line {lineNumber.Value}";

            var suggestion = "Check script syntax and Unity compilation messages.";
            if (suggestions != null && suggestions.Length > 0)
                suggestion = string.Join(" ", suggestions);

            return EnhancedError(message, context, suggestion, null, "SCRIPT_ERROR", scriptPath, lineNumber);
        }

        /// <summary>
        /// Helper method to find similar assets when an asset operation fails.
        /// STUDIO: Provides suggestions for similar assets to help users.
        /// </summary>
        private static string[] GetSimilarAssets(string assetPath)
        {
            try
            {
                if (string.IsNullOrEmpty(assetPath))
                    return new string[0];

                var fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                var directory = System.IO.Path.GetDirectoryName(assetPath);

                if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(directory))
                    return new string[0];

                // Find assets with similar names in the same directory
                var similarAssets = new List<string>();

                if (System.IO.Directory.Exists(directory))
                {
                    var files = System.IO.Directory.GetFiles(directory, "*" + fileName + "*", System.IO.SearchOption.TopDirectoryOnly);
                    foreach (var file in files.Take(3)) // Limit to 3 suggestions
                    {
                        var relativePath = file.Replace(Application.dataPath, "Assets");
                        if (relativePath != assetPath) // Don't include the failed path itself
                            similarAssets.Add(relativePath);
                    }
                }

                return similarAssets.ToArray();
            }
            catch
            {
                return new string[0]; // Return empty array on any error to avoid cascading failures
            }
        }
    }
}