using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using MCPForUnity.Runtime.Helpers;

namespace MCPForUnity.Editor.Resources.Editor
{
    /// <summary>
    /// Provides list of all open editor windows.
    /// </summary>
    [McpForUnityResource("get_windows")]
    public static class Windows
    {
        /// <summary>
        /// Handles the <c>get_windows</c> command, listing every open <c>EditorWindow</c> with its
        /// title, type, focus state, screen rect and instance ID. A window that fails to describe
        /// itself is warned about and skipped rather than failing the whole listing.
        /// </summary>
        /// <param name="params">Unused; the open windows are global editor state.</param>
        /// <returns>A success envelope with the window list, or an error response if it throws.</returns>
        public static object HandleCommand(JObject @params)
        {
            try
            {
                EditorWindow[] allWindows = UnityEngine.Resources.FindObjectsOfTypeAll<EditorWindow>();
                var openWindows = new List<object>();

                foreach (EditorWindow window in allWindows)
                {
                    if (window == null)
                        continue;

                    try
                    {
                        openWindows.Add(new
                        {
                            title = window.titleContent.text,
                            typeName = window.GetType().FullName,
                            isFocused = EditorWindow.focusedWindow == window,
                            position = new
                            {
                                x = window.position.x,
                                y = window.position.y,
                                width = window.position.width,
                                height = window.position.height
                            },
                            instanceID = window.GetInstanceIDCompat()
                        });
                    }
                    catch (Exception ex)
                    {
                        McpLog.Warn($"Could not get info for window {window.GetType().Name}: {ex.Message}");
                    }
                }

                return new SuccessResponse("Retrieved list of open editor windows.", openWindows);
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error getting editor windows: {e.Message}");
            }
        }
    }
}
