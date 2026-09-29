using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Resources.Editor
{
    /// <summary>
    /// Provides dynamic editor state information that changes frequently.
    /// </summary>
    [McpForUnityResource("get_editor_state")]
    public static class EditorState
    {
        /// <summary>
        /// Handles the <c>get_editor_state</c> command, returning the cached editor state snapshot
        /// (play mode, compiling, updating, focus, asset-import progress) maintained by
        /// <c>EditorStateCache</c> so the call never touches the editor from an arbitrary thread.
        /// </summary>
        /// <param name="params">Unused; the editor state is global.</param>
        /// <returns>A success envelope with the snapshot, or an error response if it throws.</returns>
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var snapshot = EditorStateCache.GetSnapshot();
                return new SuccessResponse("Retrieved editor state.", snapshot);
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error getting editor state: {e.Message}");
            }
        }
    }
}
