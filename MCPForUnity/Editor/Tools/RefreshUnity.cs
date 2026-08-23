using System;
using System.Threading;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Explicitly refreshes Unity's asset database and optionally requests a script compilation.
    /// This is side-effectful and should be treated as a tool.
    /// </summary>
    [McpForUnityTool("refresh_unity", AutoRegister = false)]
    public static class RefreshUnity
    {
        private const int DefaultWaitTimeoutSeconds = 60;

        public static async Task<object> HandleCommand(JObject @params)
        {
            string mode = @params?["mode"]?.ToString() ?? "if_dirty";
            string scope = @params?["scope"]?.ToString() ?? "all";
            string compile = @params?["compile"]?.ToString() ?? "none";
            bool waitForReady = ParamCoercion.CoerceBool(@params?["wait_for_ready"], false);
            bool allowDuringPlay = ParamCoercion.CoerceBool(@params?["allow_during_play"], false);

            if (TestRunStatus.IsRunning)
            {
                return new ErrorResponse("tests_running", new
                {
                    reason = "tests_running",
                    retry_after_ms = 5000
                });
            }

            // A refresh/compile triggered while Play mode is live forces a domain reload.
            // On a DOTS project that reload permanently disposes the ECS Default World for
            // the rest of the Play session -- nothing recreates it until Play is stopped and
            // re-entered (Unity.Entities only rebuilds it via a BeforeSceneLoad bootstrap,
            // which does not re-fire mid-Play), and any SubScene-baked content goes with it.
            // The managed/DI side restarts and keeps rendering, so the failure surfaces later
            // as an unrelated-looking null-world exception. Refuse by default; an explicit
            // allow_during_play=true opts back in for callers who understand the consequence.
            // Deliberately NOT auto-recreating the World here -- a rebuilt World would carry
            // no baked SubScene content, trading a loud failure for a silent, wrong one.
            if (EditorApplication.isPlaying && !allowDuringPlay)
            {
                return new ErrorResponse("play_mode_active", new
                {
                    reason = "play_mode_active",
                    message = "refresh_unity was refused because Play mode is active. Refreshing/compiling " +
                        "during Play triggers a domain reload that permanently disposes the DOTS Default World " +
                        "for the rest of this Play session (nothing recreates it until Play is stopped and " +
                        "re-entered) and drops any SubScene-baked content. Stop Play mode first, or pass " +
                        "allow_during_play=true if you understand and accept this.",
                });
            }

            bool refreshTriggered = false;
            bool compileRequested = false;
            string skipReason = null;

            try
            {
                bool isForce = string.Equals(mode, "force", StringComparison.OrdinalIgnoreCase);
                // Best-effort semantics: if_dirty currently behaves like force unless future dirty signals are added.
                bool shouldRefresh = isForce
                                     || string.Equals(mode, "if_dirty", StringComparison.OrdinalIgnoreCase);

                if (shouldRefresh)
                {
                    bool isScriptsScope = string.Equals(scope, "scripts", StringComparison.OrdinalIgnoreCase);

                    if (isScriptsScope && !isForce)
                    {
                        // For scripts under the softer if_dirty mode, requesting compilation is usually
                        // the meaningful action, so we avoid a heavyweight full refresh by default.
                        // mode="force" bypasses this shortcut below — it must not silently no-op when
                        // externally-created .cs files have no .meta and Unity's directory watcher never
                        // flagged them (see issue #38).
                        skipReason = "scripts_scope_if_dirty_skips_refresh_use_mode_force_to_import_new_files";
                    }
                    else
                    {
                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                        refreshTriggered = true;
                    }
                }
                else
                {
                    skipReason = $"mode_{mode}_does_not_trigger_refresh";
                }

                if (string.Equals(compile, "request", StringComparison.OrdinalIgnoreCase))
                {
                    CompilationPipeline.RequestScriptCompilation();
                    compileRequested = true;
                }

                if (string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase) && !refreshTriggered)
                {
                    // If the caller asked for "all" and we skipped refresh above (e.g., scripts-only path),
                    // do a lightweight refresh now. Use ForceSynchronousImport to ensure the refresh
                    // completes before returning, preventing stalls when Unity is backgrounded.
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    refreshTriggered = true;
                }

                if (refreshTriggered)
                {
                    skipReason = null;
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"refresh_failed: {ex.Message}");
            }

            // Unity 6+ fix: Skip wait_for_ready when compile was requested.
            // The EditorApplication.update polling in WaitForUnityReadyAsync doesn't survive
            // domain reloads properly in Unity 6+, causing infinite compilation loops.
            // When compilation is requested, return immediately and let client poll editor_state.
            // Earlier Unity versions retain the original behavior.
#if UNITY_6000_0_OR_NEWER
            bool shouldWaitForReady = waitForReady && !compileRequested;
#else
            bool shouldWaitForReady = waitForReady;
#endif
            if (shouldWaitForReady)
            {
                try
                {
                    await WaitForUnityReadyAsync(
                        TimeSpan.FromSeconds(DefaultWaitTimeoutSeconds)).ConfigureAwait(true);
                }
                catch (TimeoutException)
                {
                    return new ErrorResponse("refresh_timeout_waiting_for_ready", new
                    {
                        refresh_triggered = refreshTriggered,
                        compile_requested = compileRequested,
                        resulting_state = "unknown",
                        skip_reason = skipReason,
                    });
                }
                catch (Exception ex)
                {
                    return new ErrorResponse($"refresh_wait_failed: {ex.Message}");
                }
            }

            string resultingState = EditorApplication.isCompiling
                ? "compiling"
                : (EditorApplication.isUpdating ? "asset_import" : "idle");

            return new SuccessResponse("Refresh requested.", new
            {
                refresh_triggered = refreshTriggered,
                compile_requested = compileRequested,
                resulting_state = resultingState,
                skip_reason = skipReason,
                hint = shouldWaitForReady
                    ? "Unity refresh completed; editor should be ready."
                    : "If Unity enters compilation/domain reload, poll editor_state until ready_for_tools is true."
            });
        }

        private static Task WaitForUnityReadyAsync(TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var start = DateTime.UtcNow;

            void Tick()
            {
                try
                {
                    if (tcs.Task.IsCompleted)
                    {
                        EditorApplication.update -= Tick;
                        return;
                    }

                    if ((DateTime.UtcNow - start) > timeout)
                    {
                        EditorApplication.update -= Tick;
                        tcs.TrySetException(new TimeoutException());
                        return;
                    }

                    if (!EditorApplication.isCompiling
                        && !EditorApplication.isUpdating
                        && !TestRunStatus.IsRunning
                        && !EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        EditorApplication.update -= Tick;
                        tcs.TrySetResult(true);
                    }
                }
                catch (Exception ex)
                {
                    EditorApplication.update -= Tick;
                    tcs.TrySetException(ex);
                }
            }

            EditorApplication.update += Tick;
            // Nudge Unity to pump once in case update is throttled.
            try { EditorApplication.QueuePlayerLoopUpdate(); } catch { }
            return tcs.Task;
        }
    }
}
