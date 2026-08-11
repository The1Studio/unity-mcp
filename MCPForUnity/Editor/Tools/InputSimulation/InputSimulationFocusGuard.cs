using System;
using UnityEditor;
#if UNITY_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MCPForUnity.Editor.Tools.InputSimulation
{
    /// <summary>
    /// Simulated input only reaches the running game when the Game view is the input
    /// target. Two independent problems can defeat that:
    ///   1. The Editor is looking at the Scene view (or another window) instead of the
    ///      Game view — simulated devices still update, but nothing in the game observes
    ///      them because the Game view never processes the frame's input.
    ///   2. The Input System's "Play Mode Input Behavior" setting can route device input
    ///      away from the Game view depending on focus, so even a technically-successful
    ///      write may not surface to gameplay code.
    /// This helper surfaces (1) as a response warning instead of a silent false-positive
    /// "success", and works around (2) for the duration of a simulated action.
    /// </summary>
    internal static class InputSimulationFocusGuard
    {
        /// <summary>
        /// True if the currently focused Editor window is the Game view. Best-effort:
        /// returns true (no warning) if the check itself fails, since a warning must never
        /// be fabricated on top of an actual detection error.
        /// </summary>
        internal static bool IsGameViewFocused()
        {
            try
            {
                var focused = EditorWindow.focusedWindow;
                if (focused == null) return false;
                var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
                return gameViewType != null && focused.GetType() == gameViewType;
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

        /// <summary>
        /// Forces all device input to the Game view for the lifetime of the returned scope,
        /// then restores the previous setting. No-op (returns a no-op disposable) when the
        /// Input System package isn't present or the setting can't be reached.
        /// </summary>
        internal static IDisposable ForceGameViewInput()
        {
#if UNITY_INPUT_SYSTEM
            try
            {
                return new PlayModeInputScope();
            }
            catch
            {
                return NullScope.Instance;
            }
#else
            return NullScope.Instance;
#endif
        }

        private sealed class NullScope : IDisposable
        {
            internal static readonly NullScope Instance = new NullScope();
            public void Dispose() { }
        }

#if UNITY_INPUT_SYSTEM
        private sealed class PlayModeInputScope : IDisposable
        {
            private readonly InputSettings.EditorInputBehaviorInPlayMode _previous;
            private readonly bool _applied;

            internal PlayModeInputScope()
            {
                var settings = InputSystem.settings;
                if (settings == null) { _applied = false; return; }
                _previous = settings.editorInputBehaviorInPlayMode;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                _applied = true;
            }

            public void Dispose()
            {
                if (!_applied) return;
                try
                {
                    var settings = InputSystem.settings;
                    if (settings != null) settings.editorInputBehaviorInPlayMode = _previous;
                }
                catch { /* best-effort restore */ }
            }
        }
#endif
    }
}
