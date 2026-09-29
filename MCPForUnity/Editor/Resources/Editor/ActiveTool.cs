using System;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MCPForUnity.Editor.Resources.Editor
{
    /// <summary>
    /// Provides information about the currently active editor tool.
    /// </summary>
    [McpForUnityResource("get_active_tool")]
    public static class ActiveTool
    {
        /// <summary>
        /// Handles the <c>get_active_tool</c> command: reports the active handle tool, pivot mode
        /// and rotation, and the current handle rotation/position. A custom tool has no enum name,
        /// so it is reported as "Unknown Custom Tool" with <c>isCustom</c> set.
        /// </summary>
        /// <param name="params">Unused; the active tool is global editor state.</param>
        /// <returns>A success envelope with the tool info, or an error response if reading it throws.</returns>
        public static object HandleCommand(JObject @params)
        {
            try
            {
                Tool currentTool = UnityEditor.Tools.current;
                string toolName = currentTool.ToString();
                bool customToolActive = UnityEditor.Tools.current == Tool.Custom;
                string activeToolName = customToolActive ? EditorTools.GetActiveToolName() : toolName;

                var toolInfo = new
                {
                    activeTool = activeToolName,
                    isCustom = customToolActive,
                    pivotMode = UnityEditor.Tools.pivotMode.ToString(),
                    pivotRotation = UnityEditor.Tools.pivotRotation.ToString(),
                    handleRotation = new
                    {
                        x = UnityEditor.Tools.handleRotation.eulerAngles.x,
                        y = UnityEditor.Tools.handleRotation.eulerAngles.y,
                        z = UnityEditor.Tools.handleRotation.eulerAngles.z
                    },
                    handlePosition = new
                    {
                        x = UnityEditor.Tools.handlePosition.x,
                        y = UnityEditor.Tools.handlePosition.y,
                        z = UnityEditor.Tools.handlePosition.z
                    }
                };

                return new SuccessResponse("Retrieved active tool information.", toolInfo);
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error getting active tool: {e.Message}");
            }
        }
    }

    // Helper class for custom tool names
    internal static class EditorTools
    {
        public static string GetActiveToolName()
        {
            if (UnityEditor.Tools.current == Tool.Custom)
            {
                return "Unknown Custom Tool";
            }
            return UnityEditor.Tools.current.ToString();
        }
    }
}
