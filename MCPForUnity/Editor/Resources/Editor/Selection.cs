using System;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using MCPForUnity.Runtime.Helpers;

namespace MCPForUnity.Editor.Resources.Editor
{
    /// <summary>
    /// Provides detailed information about the current editor selection.
    /// </summary>
    [McpForUnityResource("get_selection")]
    public static class Selection
    {
        /// <summary>
        /// Handles the <c>get_selection</c> command: reports the active object/game object/
        /// transform, the selection count, and the full list of selected objects and game objects
        /// with their types, instance IDs and asset GUIDs.
        /// </summary>
        /// <param name="params">Unused; the selection is global editor state.</param>
        /// <returns>A success envelope with the selection detail, or an error response if it throws.</returns>
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var selectionInfo = new
                {
                    activeObject = UnityEditor.Selection.activeObject?.name,
                    activeGameObject = UnityEditor.Selection.activeGameObject?.name,
                    activeTransform = UnityEditor.Selection.activeTransform?.name,
                    activeInstanceID = UnityEditor.Selection.activeObject?.GetInstanceIDCompat() ?? 0,
                    count = UnityEditor.Selection.count,
                    objects = UnityEditor.Selection.objects
                        .Select(obj => new
                        {
                            name = obj?.name,
                            type = obj?.GetType().FullName,
                            instanceID = obj?.GetInstanceIDCompat()
                        })
                        .ToList(),
                    gameObjects = UnityEditor.Selection.gameObjects
                        .Select(go => new
                        {
                            name = go?.name,
                            instanceID = go?.GetInstanceIDCompat()
                        })
                        .ToList(),
                    assetGUIDs = UnityEditor.Selection.assetGUIDs
                };

                return new SuccessResponse("Retrieved current selection details.", selectionInfo);
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error getting selection: {e.Message}");
            }
        }
    }
}
