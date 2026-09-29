using MCPForUnity.Editor.Setup;
using MCPForUnity.Editor.Windows;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.MenuItems
{
    /// <summary>
    /// Builds the "Window / MCP For Unity" menu, the manual entry point to the package's three
    /// editor windows: the MCP bridge window, the local setup window, and the raw EditorPrefs
    /// editor used for debugging.
    /// </summary>
    public static class MCPForUnityMenu
    {
        /// <summary>
        /// Opens the main MCP window (default shortcut Ctrl/Cmd+Shift+M); the window restores its
        /// own state and drives connect/disconnect.
        /// </summary>
        [MenuItem("Window/MCP For Unity/Toggle MCP Window %#m", priority = 1)]
        public static void ToggleMCPWindow()
        {
            MCPForUnityEditorWindow.ShowWindow();
        }

        /// <summary>
        /// Opens the local setup window, which walks through the server install and client
        /// configuration steps that the main window links to.
        /// </summary>
        [MenuItem("Window/MCP For Unity/Local Setup Window", priority = 2)]
        public static void ShowSetupWindow()
        {
            SetupWindowService.ShowSetupWindow();
        }


        /// <summary>
        /// Opens a raw EditorPrefs browser for the keys this package writes, for troubleshooting a
        /// stale transport or path preference.
        /// </summary>
        [MenuItem("Window/MCP For Unity/Edit EditorPrefs", priority = 3)]
        public static void ShowEditorPrefsWindow()
        {
            EditorPrefsWindow.ShowWindow();
        }
    }
}
