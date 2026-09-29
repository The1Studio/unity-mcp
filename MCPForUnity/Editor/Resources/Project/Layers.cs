using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MCPForUnity.Editor.Resources.Project
{
    /// <summary>
    /// Provides dictionary of layer indices to layer names.
    /// </summary>
    [McpForUnityResource("get_layers")]
    public static class Layers
    {
        private const int TotalLayerCount = 32;

        /// <summary>
        /// Handles the <c>get_layers</c> command, returning the project's named layers keyed by
        /// index. Unnamed slots (including the reserved built-in ones) are omitted, so the result
        /// is a sparse map rather than a 32-entry array.
        /// </summary>
        /// <param name="params">Unused; layer names come from the project's TagManager.</param>
        /// <returns>A success envelope mapping layer index to name, or an error response if it throws.</returns>
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var layers = new Dictionary<int, string>();
                for (int i = 0; i < TotalLayerCount; i++)
                {
                    string layerName = LayerMask.LayerToName(i);
                    if (!string.IsNullOrEmpty(layerName))
                    {
                        layers.Add(i, layerName);
                    }
                }

                return new SuccessResponse("Retrieved current named layers.", layers);
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Failed to retrieve layers: {e.Message}");
            }
        }
    }
}
