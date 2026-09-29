using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools.Graphics;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Resources.Scene
{
    /// <summary>
    /// Read-only <c>get_rendering_stats</c> resource that reports frame/render statistics such as
    /// draw calls, batches, triangles and set-pass calls, for profiling a running scene from a
    /// client.
    /// </summary>
    [McpForUnityResource("get_rendering_stats")]
    public static class RenderingStatsResource
    {
        /// <summary>
        /// Handles the <c>get_rendering_stats</c> command.
        /// </summary>
        /// <param name="params">Command arguments; may be <c>null</c>.</param>
        /// <returns>Statistics from the graphics tool, or an error response if it throws.</returns>
        public static object HandleCommand(JObject @params)
        {
            try
            {
                return RenderingStatsOps.GetStats(@params ?? new JObject());
            }
            catch (Exception e)
            {
                McpLog.Error($"[RenderingStatsResource] Error: {e}");
                return new ErrorResponse($"Error getting rendering stats: {e.Message}");
            }
        }
    }
}
