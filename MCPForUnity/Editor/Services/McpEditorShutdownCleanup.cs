using System;
using System.Threading.Tasks;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services.Transport;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Best-effort cleanup when the Unity Editor is quitting.
    /// - Stops active transports so clients don't see a "hung" session longer than necessary.
    /// - If HTTP Local is selected, stops the local HTTP server only when this Editor launched it (never in batch mode).
    /// </summary>
    [InitializeOnLoad]
    internal static class McpEditorShutdownCleanup
    {
        static McpEditorShutdownCleanup()
        {
            // Guard against duplicate subscriptions across domain reloads.
            try { EditorApplication.quitting -= OnEditorQuitting; } catch { }
            EditorApplication.quitting += OnEditorQuitting;
        }

        private static void OnEditorQuitting()
        {
            // 1) Stop transports (best-effort, bounded wait).
            try
            {
                var transport = MCPServiceLocator.TransportManager;

                Task stopHttp = transport.StopAsync(TransportMode.Http);
                Task stopStdio = transport.StopAsync(TransportMode.Stdio);

                try { Task.WaitAll(new[] { stopHttp, stopStdio }, 750); } catch { }
            }
            catch (Exception ex)
            {
                // Avoid hard failures on quit.
                McpLog.Warn($"Shutdown cleanup: failed to stop transports: {ex.Message}");
            }

            // 2) Stop the local HTTP server only if this Editor launched it.
            StopOwnedServerOnQuit(MCPServiceLocator.Server, Application.isBatchMode);
        }

        /// <summary>
        /// Automatic quit-time stop. Never terminates a server this Editor did not launch
        /// (e.g. a shared daemon other Editors dial into) and does nothing in batch mode.
        /// </summary>
        internal static bool StopOwnedServerOnQuit(IServerManagementService server, bool isBatchMode)
        {
            if (isBatchMode)
            {
                // Batch runs (-runTests, -executeMethod) must be side-effect free on shared daemons.
                return false;
            }

            try
            {
                // Only the handshake-gated stop: it refuses anything without our pidfile+token / recorded PID.
                return server.StopManagedLocalHttpServer();
            }
            catch (Exception ex)
            {
                McpLog.Warn($"Shutdown cleanup: failed to stop local HTTP server: {ex.Message}");
                return false;
            }
        }
    }
}

