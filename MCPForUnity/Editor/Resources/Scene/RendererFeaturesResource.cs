using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools.Graphics;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Resources.Scene
{
    /// <summary>
    /// Read-only <c>get_renderer_features</c> resource that lists the renderer features registered
    /// on the active render pipeline's renderers, so a client can see which post-processing and
    /// rendering passes are wired up.
    /// </summary>
    [McpForUnityResource("get_renderer_features")]
    public static class RendererFeaturesResource
    {
        /// <summary>
        /// Handles the <c>get_renderer_features</c> command.
        /// </summary>
        /// <param name="params">Command arguments; may be <c>null</c>.</param>
        /// <returns>Feature listing from the graphics tool, or an error response if it throws.</returns>
        public static object HandleCommand(JObject @params)
        {
            try
            {
                return RendererFeatureOps.ListFeatures(@params ?? new JObject());
            }
            catch (Exception e)
            {
                McpLog.Error($"[RendererFeaturesResource] Error: {e}");
                return new ErrorResponse($"Error listing renderer features: {e.Message}");
            }
        }
    }
}
