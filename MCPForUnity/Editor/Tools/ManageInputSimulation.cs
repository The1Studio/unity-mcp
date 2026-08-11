using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools.InputSimulation;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Play Mode input simulation — lets AI assistants interact with running Unity games.
    /// Actions: discover, get_element_bounds, click, double_click, mouse_move, drag, scroll,
    /// key_press, key_combo, text_input, touch_tap, touch_swipe, touch_pinch, sequence, status.
    /// </summary>
    [McpForUnityTool("manage_input_simulation", AutoRegister = true, Group = "testing")]
    public static class ManageInputSimulation
    {
        // Actions that inject device state the running game is expected to observe.
        // Discovery/sequence/status actions don't move a device, so they're exempt.
        private static readonly HashSet<string> GameFacingActions = new HashSet<string>
        {
            "click", "double_click", "mouse_move", "drag", "scroll",
            "key_press", "key_combo", "text_input",
            "touch_tap", "touch_swipe", "touch_pinch",
        };

        public static object HandleCommand(JObject @params)
        {
            if (@params == null) return new ErrorResponse("Parameters cannot be null.");

            // Play Mode guard — input simulation only works while game is running
            if (!EditorApplication.isPlaying)
                return new ErrorResponse("Play Mode required. Use manage_editor action='play' first.");

            var p = new ToolParams(@params);
            var actionResult = p.GetRequired("action");
            if (!actionResult.IsSuccess) return new ErrorResponse(actionResult.ErrorMessage);
            string action = actionResult.Value.ToLowerInvariant();
            bool isGameFacingAction = GameFacingActions.Contains(action);

            try
            {
                object result;
                if (isGameFacingAction)
                {
                    // Force all device input to the Game view for the duration of the
                    // simulated action, then restore whatever the user had configured.
                    using (InputSimulationFocusGuard.ForceGameViewInput())
                    {
                        result = Dispatch(action, p);
                    }

                    // A "success" that never reached the game is a false positive — surface
                    // it as a warning instead of silently reporting plain success.
                    if (result is SuccessResponse success && !InputSimulationFocusGuard.IsGameViewFocused())
                        success.Warning = InputSimulationFocusGuard.FocusWarning;
                }
                else
                {
                    result = Dispatch(action, p);
                }

                return result;
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Internal error during '{action}': {e.Message}");
            }
        }

        private static object Dispatch(string action, ToolParams p) => action switch
        {
            // Discovery (Phase 1)
            "discover"           => InputDiscovery.Discover(p),
            "get_element_bounds" => InputDiscovery.GetElementBounds(p),

            // Mouse actions (Phase 2)
            "click"              => InputSimulationActions.Click(p),
            "double_click"       => InputSimulationActions.DoubleClick(p),
            "mouse_move"         => InputSimulationActions.MouseMove(p),
            "drag"               => InputSimulationActions.Drag(p),
            "scroll"             => InputSimulationActions.Scroll(p),

            // Keyboard actions (Phase 2)
            "key_press"          => InputSimulationActions.KeyPress(p),
            "key_combo"          => InputSimulationActions.KeyCombo(p),
            "text_input"         => InputSimulationActions.TextInput(p),

            // Touch actions (Phase 3)
            "touch_tap"          => InputSimulationTouch.TouchTap(p),
            "touch_swipe"        => InputSimulationTouch.TouchSwipe(p),
            "touch_pinch"        => InputSimulationTouch.TouchPinch(p),

            // Sequence actions (Phase 3)
            "sequence"           => InputSimulationSequence.StartSequence(p),
            "status"             => InputSimulationSequence.GetStatus(p),

            _ => new ErrorResponse(
                $"Unknown action: '{action}'. Valid actions: discover, get_element_bounds, " +
                "click, double_click, mouse_move, drag, scroll, " +
                "key_press, key_combo, text_input, " +
                "touch_tap, touch_swipe, touch_pinch, sequence, status.")
        };
    }
}
