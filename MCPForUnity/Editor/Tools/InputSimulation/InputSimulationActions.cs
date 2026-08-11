using System;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
#if UNITY_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace MCPForUnity.Editor.Tools.InputSimulation
{
    /// <summary>
    /// Mouse + keyboard input simulation actions.
    /// Dual backend: New Input System (InputState.Change for byte-aligned controls,
    /// event-queued writes via WriteButton for bitfield controls) + Legacy (ExecuteEvents).
    /// </summary>
    internal static class InputSimulationActions
    {
        // ── Shared helper ──────────────────────────────────────────────────────────
        static (Vector2? pos, object err) Resolve(ToolParams p, string param = "target")
        {
            var tok = p.GetRaw(param);
            if (tok == null || tok.Type != JTokenType.Object)
                return (null, new ErrorResponse($"'{param}' must be a JSON object."));
            var (pos, msg) = InputTargetResolver.ResolveTarget((JObject)tok);
            return msg != null ? (null, (object)new ErrorResponse(msg)) : (pos, null);
        }

#if UNITY_INPUT_SYSTEM
        // ── New Input System helpers ───────────────────────────────────────────────
        static int BtnIndex(string n) => n?.ToLowerInvariant() == "right" ? 1 : n?.ToLowerInvariant() == "middle" ? 2 : 0;
        static bool TryKey(string n, out Key k) => Enum.TryParse(n ?? "", true, out k);
        static void Move(Vector2 p) => InputState.Change(Mouse.current.position, p);

        // Key/Button controls are bitfield-backed (packed sub-byte state) and cannot be
        // written via InputState.Change — that overload only supports byte-aligned state
        // memory and throws "Cannot change state of bitfield control ... using this method".
        // The supported way to flip a bitfield control is to write the value into a fresh
        // state event for the owning device, then queue that event (same pattern Unity's
        // own OnScreenControl uses for on-screen buttons/keys).
        static void WriteButton(UnityEngine.InputSystem.Controls.ButtonControl control, float value)
        {
            using (StateEvent.From(control.device, out var eventPtr))
            {
                control.WriteValueIntoEvent(value, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
        }

        static void Btn(int b, float v)
        {
            var control = b == 1 ? Mouse.current.rightButton : b == 2 ? Mouse.current.middleButton : Mouse.current.leftButton;
            WriteButton(control, v);
        }
        static void Flush(bool f) { if (f) InputSystem.Update(); }
        static void DoClick(Vector2 p, int b, bool f) { Move(p); Btn(b, 1f); Flush(f); Btn(b, 0f); Flush(f); }
#endif

        // ── Mouse Actions ──────────────────────────────────────────────────────────
        internal static object Click(ToolParams p)
        {
            var (pos, e) = Resolve(p); if (e != null) return e;
            string bn = p.Get("button", "left"); bool fl = p.GetBool("flush", true);
#if UNITY_INPUT_SYSTEM
            DoClick(pos.Value, BtnIndex(bn), fl);
            return new SuccessResponse($"Clicked ({pos.Value.x:F0}, {pos.Value.y:F0}).", new { x = pos.Value.x, y = pos.Value.y, button = bn });
#else
            return new ErrorResponse("Legacy backend: click not supported. Enable com.unity.inputsystem.");
#endif
        }

        internal static object DoubleClick(ToolParams p)
        {
            var (pos, e) = Resolve(p); if (e != null) return e;
            string bn = p.Get("button", "left"); bool fl = p.GetBool("flush", true);
#if UNITY_INPUT_SYSTEM
            int b = BtnIndex(bn); DoClick(pos.Value, b, fl); DoClick(pos.Value, b, fl);
            return new SuccessResponse($"Double-clicked ({pos.Value.x:F0}, {pos.Value.y:F0}).", new { x = pos.Value.x, y = pos.Value.y, button = bn });
#else
            return new ErrorResponse("Legacy backend: double-click not supported. Enable com.unity.inputsystem.");
#endif
        }

        internal static object MouseMove(ToolParams p)
        {
            var (pos, e) = Resolve(p); if (e != null) return e;
#if UNITY_INPUT_SYSTEM
            Move(pos.Value); Flush(p.GetBool("flush", true));
            return new SuccessResponse($"Mouse moved to ({pos.Value.x:F0}, {pos.Value.y:F0}).", new { x = pos.Value.x, y = pos.Value.y });
#else
            return new ErrorResponse("Legacy backend: mouse move not supported. Enable com.unity.inputsystem.");
#endif
        }

        internal static object Drag(ToolParams p)
        {
            var (fr, e1) = Resolve(p, "from_target"); if (e1 != null) return e1;
            var (to, e2) = Resolve(p, "to_target");   if (e2 != null) return e2;
#if UNITY_INPUT_SYSTEM
            bool fl = p.GetBool("flush", true);
            Move(fr.Value); WriteButton(Mouse.current.leftButton, 1f); Flush(fl);
            Move(to.Value); Flush(fl);
            WriteButton(Mouse.current.leftButton, 0f); Flush(fl);
            return new SuccessResponse($"Dragged ({fr.Value.x:F0},{fr.Value.y:F0}) → ({to.Value.x:F0},{to.Value.y:F0}).",
                new { from_x = fr.Value.x, from_y = fr.Value.y, to_x = to.Value.x, to_y = to.Value.y });
#else
            return new ErrorResponse("Legacy backend: drag not supported. Enable com.unity.inputsystem.");
#endif
        }

        internal static object Scroll(ToolParams p)
        {
            var (pos, e) = Resolve(p); if (e != null) return e;
            float dx = p.GetFloat("delta_x") ?? 0f, dy = p.GetFloat("delta_y") ?? 0f;
#if UNITY_INPUT_SYSTEM
            Move(pos.Value); InputState.Change(Mouse.current.scroll, new Vector2(dx, dy)); Flush(p.GetBool("flush", true));
            return new SuccessResponse($"Scrolled ({pos.Value.x:F0},{pos.Value.y:F0}) Δ({dx},{dy}).",
                new { x = pos.Value.x, y = pos.Value.y, delta_x = dx, delta_y = dy });
#else
            return new ErrorResponse("Legacy backend: scroll not supported. Enable com.unity.inputsystem.");
#endif
        }

        // ── Keyboard Actions ───────────────────────────────────────────────────────
        internal static object KeyPress(ToolParams p)
        {
            var r = p.GetRequired("key"); if (!r.IsSuccess) return new ErrorResponse(r.ErrorMessage);
            string mode = p.Get("mode", "tap");
#if UNITY_INPUT_SYSTEM
            if (!TryKey(r.Value, out var k)) return new ErrorResponse($"Unknown key '{r.Value}'. Use Key enum names (e.g. Space, A, Escape).");
            bool fl = p.GetBool("flush", true);
            if (mode == "release") { WriteButton(Keyboard.current[k], 0f); }
            else { WriteButton(Keyboard.current[k], 1f); Flush(fl); if (mode == "tap") WriteButton(Keyboard.current[k], 0f); }
            Flush(fl);
            return new SuccessResponse($"Key '{r.Value}' {mode}.", new { key = r.Value, mode });
#else
            return new ErrorResponse("Legacy backend: key press not supported. Enable com.unity.inputsystem.");
#endif
        }

        internal static object KeyCombo(ToolParams p)
        {
            var r = p.GetRequired("key"); if (!r.IsSuccess) return new ErrorResponse(r.ErrorMessage);
            string[] mods = p.GetStringArray("modifiers");
#if UNITY_INPUT_SYSTEM
            if (!TryKey(r.Value, out var k)) return new ErrorResponse($"Unknown key '{r.Value}'.");
            bool fl = p.GetBool("flush", true);
            if (mods != null) foreach (var m in mods) if (TryKey(m, out var mk)) WriteButton(Keyboard.current[mk], 1f);
            WriteButton(Keyboard.current[k], 1f); Flush(fl);
            WriteButton(Keyboard.current[k], 0f);
            if (mods != null) foreach (var m in mods) if (TryKey(m, out var mk)) WriteButton(Keyboard.current[mk], 0f);
            Flush(fl);
            return new SuccessResponse($"Combo [{string.Join("+", mods ?? Array.Empty<string>())}]+{r.Value}.", new { key = r.Value, modifiers = mods });
#else
            return new ErrorResponse("Legacy backend: key combo not supported. Enable com.unity.inputsystem.");
#endif
        }

        internal static object TextInput(ToolParams p)
        {
            var r = p.GetRequired("text"); if (!r.IsSuccess) return new ErrorResponse(r.ErrorMessage);
#if UNITY_INPUT_SYSTEM
            bool fl = p.GetBool("flush", true);
            foreach (char c in r.Value) { InputSystem.QueueTextEvent(Keyboard.current, c); Flush(fl); }
            return new SuccessResponse($"Typed {r.Value.Length} character(s).", new { text = r.Value, length = r.Value.Length });
#else
            return new ErrorResponse("Legacy backend: text input not supported. Enable com.unity.inputsystem.");
#endif
        }
    }
}
