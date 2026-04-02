using System;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEngine;
#if UNITY_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace MCPForUnity.Editor.Tools.InputSimulation
{
    internal static class InputSimulationTouch
    {
        // Target resolution helper (same pattern as InputSimulationActions)
        static (Vector2? pos, object err) Resolve(ToolParams p, string param = "target")
        {
            var tok = p.GetRaw(param);
            if (tok == null || tok.Type != JTokenType.Object)
                return (null, new ErrorResponse($"'{param}' must be a JSON object."));
            var (pos, msg) = InputTargetResolver.ResolveTarget((JObject)tok);
            return msg != null ? (null, (object)new ErrorResponse(msg)) : (pos, null);
        }

#if UNITY_INPUT_SYSTEM
        static Touchscreen EnsureTouchscreen()
        {
            if (Touchscreen.current != null) return Touchscreen.current;
            return InputSystem.AddDevice<Touchscreen>();
        }

        static void SimTouch(Touchscreen ts, int finger, Vector2 pos, UnityEngine.InputSystem.TouchPhase phase, bool flush)
        {
            var state = new TouchState
            {
                touchId = finger + 1,
                position = pos,
                phase = phase,
            };
            InputState.Change(ts.touches[finger], state);
            if (flush) InputSystem.Update();
        }
#endif

        internal static object TouchTap(ToolParams p)
        {
#if UNITY_INPUT_SYSTEM
            var (pos, e) = Resolve(p); if (e != null) return e;
            bool fl = p.GetBool("flush", true);
            var ts = EnsureTouchscreen();
            SimTouch(ts, 0, pos.Value, UnityEngine.InputSystem.TouchPhase.Began, fl);
            SimTouch(ts, 0, pos.Value, UnityEngine.InputSystem.TouchPhase.Ended, fl);
            return new SuccessResponse($"Touch tap at ({pos.Value.x:F0}, {pos.Value.y:F0}).", new { x = pos.Value.x, y = pos.Value.y });
#else
            return new ErrorResponse("Touch simulation requires com.unity.inputsystem package.");
#endif
        }

        internal static object TouchSwipe(ToolParams p)
        {
#if UNITY_INPUT_SYSTEM
            var (from, e1) = Resolve(p, "from_target"); if (e1 != null) return e1;
            var (to, e2) = Resolve(p, "to_target"); if (e2 != null) return e2;
            bool fl = p.GetBool("flush", true);
            var ts = EnsureTouchscreen();
            SimTouch(ts, 0, from.Value, UnityEngine.InputSystem.TouchPhase.Began, fl);
            // Midpoint for smoother swipe
            var mid = (from.Value + to.Value) * 0.5f;
            SimTouch(ts, 0, mid, UnityEngine.InputSystem.TouchPhase.Moved, fl);
            SimTouch(ts, 0, to.Value, UnityEngine.InputSystem.TouchPhase.Moved, fl);
            SimTouch(ts, 0, to.Value, UnityEngine.InputSystem.TouchPhase.Ended, fl);
            return new SuccessResponse($"Swipe ({from.Value.x:F0},{from.Value.y:F0}) → ({to.Value.x:F0},{to.Value.y:F0}).",
                new { from_x = from.Value.x, from_y = from.Value.y, to_x = to.Value.x, to_y = to.Value.y });
#else
            return new ErrorResponse("Touch simulation requires com.unity.inputsystem package.");
#endif
        }

        internal static object TouchPinch(ToolParams p)
        {
#if UNITY_INPUT_SYSTEM
            var (center, e) = Resolve(p, "center_target"); if (e != null) return e;
            float startDist = p.GetFloat("start_distance") ?? 100f;
            float endDist = p.GetFloat("end_distance") ?? 50f;
            bool fl = p.GetBool("flush", true);
            var ts = EnsureTouchscreen();

            // Two fingers symmetrically around center
            Vector2 c = center.Value;
            Vector2 startA = c + new Vector2(-startDist / 2, 0);
            Vector2 startB = c + new Vector2(startDist / 2, 0);
            Vector2 endA = c + new Vector2(-endDist / 2, 0);
            Vector2 endB = c + new Vector2(endDist / 2, 0);

            SimTouch(ts, 0, startA, UnityEngine.InputSystem.TouchPhase.Began, false);
            SimTouch(ts, 1, startB, UnityEngine.InputSystem.TouchPhase.Began, fl);
            SimTouch(ts, 0, endA, UnityEngine.InputSystem.TouchPhase.Moved, false);
            SimTouch(ts, 1, endB, UnityEngine.InputSystem.TouchPhase.Moved, fl);
            SimTouch(ts, 0, endA, UnityEngine.InputSystem.TouchPhase.Ended, false);
            SimTouch(ts, 1, endB, UnityEngine.InputSystem.TouchPhase.Ended, fl);

            return new SuccessResponse($"Pinch at ({c.x:F0},{c.y:F0}) dist {startDist}→{endDist}.",
                new { center_x = c.x, center_y = c.y, start_distance = startDist, end_distance = endDist });
#else
            return new ErrorResponse("Touch simulation requires com.unity.inputsystem package.");
#endif
        }
    }
}
