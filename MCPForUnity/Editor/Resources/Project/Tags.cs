using System;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditorInternal;

namespace MCPForUnity.Editor.Resources.Project
{
    /// <summary>
    /// Provides list of all tags in the project.
    /// </summary>
    [McpForUnityResource("get_tags")]
    public static class Tags
    {
        /// <summary>
        /// Handles the <c>get_tags</c> command, returning every tag defined in the project's
        /// TagManager.
        /// </summary>
        /// <param name="params">Unused; tags come from the project settings.</param>
        /// <returns>A success envelope with the tag list, or an error response if it throws.</returns>
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string[] tags = InternalEditorUtility.tags;
                return new SuccessResponse("Retrieved current tags.", tags);
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Failed to retrieve tags: {e.Message}");
            }
        }
    }
}
