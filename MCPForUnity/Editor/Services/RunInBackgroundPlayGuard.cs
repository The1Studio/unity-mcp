using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Mitigates an unfocused-editor Play-mode stall (issue #62): with the
    /// consuming project's Player Setting "Run In Background" disabled, an
    /// editor window that never holds OS focus can silently stop advancing
    /// the *player* loop while remaining fully MCP-responsive (the editor
    /// loop keeps ticking). <c>manage_editor action=play</c> opts into this
    /// guard by default; it sets <see cref="Application.runInBackground"/>
    /// for the duration of the Play session and restores it on the first
    /// <see cref="EditorApplication.playModeStateChanged"/> transition back
    /// to Edit mode -- whichever path triggered it: our own "stop" action,
    /// the user clicking Stop in the Editor, or a script-driven stop
    /// elsewhere. Unity's own PlayMode test runner does the equivalent
    /// set/restore around a test run
    /// (<c>PreparePlayModeRunTask</c> / <c>RestoreProjectSettingsTask</c>).
    ///
    /// <para>
    /// <see cref="Application.runInBackground"/> writes through to
    /// <c>PlayerSettings.runInBackground</c>, which serializes into the
    /// tracked <c>ProjectSettings/ProjectSettings.asset</c> -- so an aborted
    /// restore leaves a diff behind. <see cref="SessionState"/> survives the
    /// domain reload that entering/exiting Play mode can trigger ("Enter
    /// Play Mode Settings" -> Reload Domain), so the original value and the
    /// "we changed it" flag are recoverable across that reload.
    /// </para>
    ///
    /// <para>
    /// <b>Known, accepted gap:</b> this does NOT survive an editor quit or
    /// crash that happens while the guard is still active (i.e. Play mode
    /// never reached <see cref="PlayModeStateChange.EnteredEditMode"/>
    /// before the process exited). <see cref="SessionState"/> is in-memory
    /// only and is gone on the next launch, so there is nothing left to read
    /// at startup that would tell a future session "restore
    /// runInBackground". Closing that gap needs a project-local marker file
    /// read on every editor startup (a larger, untested-here change) --
    /// documented as a known limitation rather than silently risked.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class RunInBackgroundPlayGuard
    {
        private const string SessionKeyDirty = "MCPForUnity.RunInBackgroundGuard.Dirty";
        private const string SessionKeyOriginalValue = "MCPForUnity.RunInBackgroundGuard.OriginalValue";

        static RunInBackgroundPlayGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>
        /// Call before setting <c>EditorApplication.isPlaying = true</c> from
        /// the manage_editor "play" action. No-op when
        /// <see cref="Application.runInBackground"/> is already true, or when
        /// a previously-armed guard has not yet been restored (avoids
        /// clobbering the recorded original value with an already-mutated
        /// "current" value on a re-entrant/duplicate play request).
        /// </summary>
        public static void OnEnteringPlayMode()
        {
            if (SessionState.GetBool(SessionKeyDirty, false))
            {
                return;
            }

            bool original = Application.runInBackground;
            if (original)
            {
                // Already backgrounded -- nothing to mitigate or restore later.
                return;
            }

            SessionState.SetBool(SessionKeyOriginalValue, original);
            SessionState.SetBool(SessionKeyDirty, true);
            Application.runInBackground = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }
            Restore();
        }

        private static void Restore()
        {
            if (!SessionState.GetBool(SessionKeyDirty, false))
            {
                return;
            }

            bool original = SessionState.GetBool(SessionKeyOriginalValue, false);
            Application.runInBackground = original;
            SessionState.EraseBool(SessionKeyDirty);
            SessionState.EraseBool(SessionKeyOriginalValue);
        }
    }
}
