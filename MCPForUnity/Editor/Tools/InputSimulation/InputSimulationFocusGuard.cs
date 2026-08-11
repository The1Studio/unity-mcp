using System;
using UnityEditor;

namespace MCPForUnity.Editor.Tools.InputSimulation
{
    /// <summary>
    /// Simulated input only reaches the running game when the Game view is the input
    /// target. Two independent things can defeat that:
    ///   1. Unity itself doesn't have OS-level focus (the user is in an MCP client, Unity
    ///      is in the background) — simulated devices still update, but nothing in the
    ///      game observes them because the Game view never processes the frame's input.
    ///   2. The Editor is looking at the Scene view (or another window) instead of the
    ///      Game view, even while Unity itself is focused.
    /// This helper surfaces either case as a response warning instead of a silent
    /// false-positive "success".
    /// </summary>
    internal static class InputSimulationFocusGuard
    {
        /// <summary>
        /// True if Unity has OS-level focus AND the currently focused Editor window is
        /// the Game view. Best-effort: a genuine detection failure (no focused-window
        /// state available, or the internal GameView type can't be resolved) returns
        /// true (no warning) — a warning must never be fabricated on top of an actual
        /// detection error. A real "not focused" signal (Unity backgrounded, or a
        /// different window focused) returns false so the warning fires.
        /// </summary>
        internal static bool IsGameViewFocused()
        {
            try
            {
                if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive) return false;

                var focused = EditorWindow.focusedWindow;
                if (focused == null) return true;
                var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
                if (gameViewType == null) return true;
                return focused.GetType() == gameViewType;
            }
            catch
            {
                return true;
            }
        }

        internal const string FocusWarning =
            "Game view is not focused — simulated input was sent to the input devices but may not reach " +
            "the running game. Focus the Game view (or call manage_editor to bring it forward) before " +
            "relying on this result.";
    }
}
