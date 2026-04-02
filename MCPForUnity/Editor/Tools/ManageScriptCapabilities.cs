using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Returns supported script editing operations and limits.
    /// C# handler mirrors the Python-side manage_script_capabilities tool so batch_execute can dispatch it.
    /// </summary>
    [McpForUnityTool("manage_script_capabilities", AutoRegister = false, Group = "core")]
    public static class ManageScriptCapabilities
    {
        public static object HandleCommand(JObject @params)
        {
            return new SuccessResponse("Script capabilities retrieved.", new
            {
                ops = new[]
                {
                    "replace_class", "delete_class",
                    "replace_method", "delete_method", "insert_method",
                    "anchor_insert", "anchor_delete", "anchor_replace"
                },
                text_ops = new[]
                {
                    "replace_range", "regex_replace", "prepend", "append"
                },
                max_edit_payload_bytes = 262144,
                guards = new { using_guard = true },
                extras = new { get_sha = true }
            });
        }
    }
}
