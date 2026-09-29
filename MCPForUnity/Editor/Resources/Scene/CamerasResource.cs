using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools.Cameras;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MCPForUnity.Editor.Resources.Scene
{
    /// <summary>
    /// Read-only <c>get_cameras</c> resource that lists the cameras in the loaded scene(s),
    /// delegating the actual work to the camera tool so the resource and the tool cannot report
    /// different views.
    /// </summary>
    [McpForUnityResource("get_cameras")]
    public static class CamerasResource
    {
        /// <summary>
        /// Handles the <c>get_cameras</c> command.
        /// </summary>
        /// <param name="params">Command arguments; may be <c>null</c>.</param>
        /// <returns>Camera listing from the camera tool, or an error response if it throws.</returns>
        public static object HandleCommand(JObject @params)
        {
            try
            {
                return CameraControl.ListCameras(@params ?? new JObject());
            }
            catch (Exception e)
            {
                McpLog.Error($"[CamerasResource] Error listing cameras: {e}");
                return new ErrorResponse($"Error listing cameras: {e.Message}");
            }
        }
    }
}
