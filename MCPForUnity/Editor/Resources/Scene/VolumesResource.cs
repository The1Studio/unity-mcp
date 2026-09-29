using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools.Graphics;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Resources.Scene
{
    /// <summary>
    /// Read-only <c>get_volumes</c> resource that lists the post-processing volumes in the loaded
    /// scene(s), including their mode, priority and profile, for inspecting the active visual
    /// environment.
    /// </summary>
    [McpForUnityResource("get_volumes")]
    public static class VolumesResource
    {
        /// <summary>
        /// Handles the <c>get_volumes</c> command.
        /// </summary>
        /// <param name="params">Command arguments; may be <c>null</c>.</param>
        /// <returns>Volume listing from the graphics tool, or an error response if it throws.</returns>
        public static object HandleCommand(JObject @params)
        {
            try
            {
                return VolumeOps.ListVolumes(@params ?? new JObject());
            }
            catch (Exception e)
            {
                McpLog.Error($"[VolumesResource] Error listing volumes: {e}");
                return new ErrorResponse($"Error listing volumes: {e.Message}");
            }
        }
    }
}
