using System;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Services.Transport.Transports;
using UnityEditor;

namespace MCPForUnity.Editor
{
    /// <summary>
    /// Entry point used by the CI bootstrap to bring the bridge up on a headless editor.
    /// Forces the stdio transport (an HTTP listener cannot be reached from a test run) and
    /// starts the auto-connect sequence.
    /// </summary>
    public static class McpCiBoot
    {
        /// <summary>
        /// Disables the HTTP transport preference and starts the stdio bridge's auto-connect
        /// loop. Safe to call when the editor has no live MCP client: it only arms the bridge.
        /// </summary>
        public static void StartStdioForCi()
        {
            try 
            { 
                EditorPrefs.SetBool(EditorPrefKeys.UseHttpTransport, false); 
            }
            catch { /* ignore */ }

            StdioBridgeHost.StartAutoConnect();
        }
    }
}
