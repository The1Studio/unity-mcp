using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
#if UNITY_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
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
        // state event for the owning device, then queue that event.
        //
        // StateEvent.From snapshots the device's FULL current state at event-creation time.
        // Two such events queued back-to-back WITHOUT an intervening update do NOT
        // accumulate — each snapshots the state as of its own creation, so the second
        // silently overwrites every control the first one touched (a dropped modifier, or
        // a key left stuck down). Every control belonging to the SAME logical transition
        // (e.g. a modifier + its key going down together) MUST therefore be written into
        // ONE event via WriteButtons, and InputSystem.Update() is forced unconditionally
        // after every queue so the next snapshot in a sequence observes this transition
        // already applied. Correctness must not depend on the caller's "flush" parameter —
        // see key_combo below, which is exactly the sequence that broke without this.
        static void WriteButton(ButtonControl control, float value)
        {
            using (StateEvent.From(control.device, out var eventPtr))
            {
                control.WriteValueIntoEvent(value, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
            InputSystem.Update();
        }

        // Writes multiple controls of the SAME device into a single state event so they
        // land as one atomic transition. See WriteButton for why this matters.
        static void WriteButtons(InputDevice device, params (ButtonControl control, float value)[] writes)
        {
            using (StateEvent.From(device, out var eventPtr))
            {
                foreach (var (control, value) in writes) control.WriteValueIntoEvent(value, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
            InputSystem.Update();
        }

        static void Btn(int b, float v)
        {
            var control = b == 1 ? Mouse.current.rightButton : b == 2 ? Mouse.current.middleButton : Mouse.current.leftButton;
            WriteButton(control, v);
        }
        static void Flush(bool f) { if (f) InputSystem.Update(); }
        // WriteButton/Btn already force an update per transition (see above) — button
        // presses no longer depend on a subsequent Flush() call for correctness.
        static void DoClick(Vector2 p, int b) { Move(p); Btn(b, 1f); Btn(b, 0f); }
#endif

        // ── Mouse Actions ──────────────────────────────────────────────────────────
        internal static object Click(ToolParams p)
        {
            var (pos, e) = Resolve(p); if (e != null) return e;
            string bn = p.Get("button", "left");
#if UNITY_INPUT_SYSTEM
            DoClick(pos.Value, BtnIndex(bn));
            return new SuccessResponse($"Clicked ({pos.Value.x:F0}, {pos.Value.y:F0}).", new { x = pos.Value.x, y = pos.Value.y, button = bn });
#else
            return new ErrorResponse("Legacy backend: click not supported. Enable com.unity.inputsystem.");
#endif
        }

        internal static object DoubleClick(ToolParams p)
        {
            var (pos, e) = Resolve(p); if (e != null) return e;
            string bn = p.Get("button", "left");
#if UNITY_INPUT_SYSTEM
            int b = BtnIndex(bn); DoClick(pos.Value, b); DoClick(pos.Value, b);
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
            // Move() writes position immediately via InputState.Change; WriteButton()
            // forces its own update per transition (see WriteButton), so each subsequent
            // snapshot already observes the prior Move — no explicit flush needed here.
            Move(fr.Value);
            WriteButton(Mouse.current.leftButton, 1f);
            Move(to.Value);
            WriteButton(Mouse.current.leftButton, 0f);
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

            // Modifiers + main key must go down in ONE event and come up in ONE event —
            // see WriteButtons. Writing them as separate back-to-back events (the previous
            // implementation) silently drops the modifier on the way down and leaves the
            // main key stuck down on the way up, because each event only snapshots the
            // state as of its own creation.
            var down = new List<(ButtonControl control, float value)>();
            if (mods != null) foreach (var m in mods) if (TryKey(m, out var mk)) down.Add((Keyboard.current[mk], 1f));
            down.Add((Keyboard.current[k], 1f));
            WriteButtons(Keyboard.current, down.ToArray());

            var up = new List<(ButtonControl control, float value)>();
            up.Add((Keyboard.current[k], 0f));
            if (mods != null) foreach (var m in mods) if (TryKey(m, out var mk)) up.Add((Keyboard.current[mk], 0f));
            WriteButtons(Keyboard.current, up.ToArray());

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
