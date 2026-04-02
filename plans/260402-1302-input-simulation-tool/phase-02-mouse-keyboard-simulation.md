---
phase: 2
status: pending
effort: L
blocked_by: [1]
blocks: [3]
---

# Phase 2: C# Mouse + Keyboard Input Simulation

## Context Links

- Pattern: `MCPForUnity/Editor/Tools/ManageInputSystem.cs` — `#if UNITY_INPUT_SYSTEM` conditional compilation
- Phase 1: `ManageInputSimulation.cs` — action dispatch (wire new actions here)
- Phase 1: `InputTargetResolver.cs` — `ResolveTarget()` for screen coords
- Unity API: `UnityEngine.InputSystem.InputState.Change()`, `UnityEngine.InputSystem.LowLevel.QueueStateEvent()`
- Unity API: `UnityEngine.EventSystems.ExecuteEvents`
- Unity API: `UnityEngine.Event` for legacy IMGUI

## Overview

Implement mouse actions (click, double_click, mouse_move, drag, scroll) and keyboard actions (key_press, key_combo, text_input) with dual-backend support: New Input System via InputState manipulation, legacy via ExecuteEvents.

## Key Insights

- New Input System: `Mouse.current`, `Keyboard.current` are the device singletons. `InputState.Change(Mouse.current.position, screenPos)` moves the pointer. `InputState.Change(Mouse.current.press, 1f)` presses button.
- For click, sequence: move → press → release. For double_click: move → press → release → press → release with short delay.
- Legacy UGUI: `ExecuteEvents.Execute(target, pointerEventData, ExecuteEvents.pointerClickHandler)` directly triggers click on a specific GameObject. This only works for UI elements with EventSystem handlers.
- Legacy `Event` queue: `new Event { type = EventType.KeyDown, keyCode = KeyCode.Space }` can be sent for IMGUI.
- `key_combo` (e.g., Ctrl+S): press modifier(s), press key, release key, release modifier(s).
- `text_input`: For New Input System, use `Keyboard.current.text` control. For legacy, use `Event` with `EventType.KeyDown` and `character` field.

## Requirements

### Functional
- All simulation actions accept optional `flush` param (default true). When true, calls `InputSystem.Update()` after state change for immediate processing. When false, batches changes for next frame (useful in sequences).
- `click(target, button?)`: Resolve target → screen coords. Simulate pointer move + press + release. `button` defaults to "left".
- `double_click(target, button?)`: Two rapid click sequences.
- `mouse_move(target)`: Move pointer to target position without clicking.
- `drag(from_target, to_target, duration_ms?)`: Move to from, press, interpolate to to, release. `duration_ms` defaults to 200.
- `scroll(target, delta_x?, delta_y?)`: Scroll wheel at target position. At least one delta required.
- `key_press(key, mode?)`: `mode` = press | release | tap (default: tap). `key` is key name string (e.g., "space", "a", "escape").
- `key_combo(modifiers[], key)`: Simultaneous modifier keys + main key.
- `text_input(text)`: Type text string character by character into focused element.

### Non-Functional
- Conditional compilation: `#if UNITY_INPUT_SYSTEM` for new system, `#else` for legacy
- Mouse button mapping: "left"→0, "right"→1, "middle"→2
- Key name mapping: string → `Key` enum (new) or `KeyCode` enum (legacy)
- All actions return `SuccessResponse` with what was done, or `ErrorResponse`

## Architecture

```csharp
// InputSimulationActions.cs — ~180 lines
namespace MCPForUnity.Editor.Tools.InputSimulation
{
    internal static class InputSimulationActions
    {
        // Mouse actions
        internal static object Click(ToolParams p) { ... }
        internal static object DoubleClick(ToolParams p) { ... }
        internal static object MouseMove(ToolParams p) { ... }
        internal static object Drag(ToolParams p) { ... }
        internal static object Scroll(ToolParams p) { ... }

        // Keyboard actions
        internal static object KeyPress(ToolParams p) { ... }
        internal static object KeyCombo(ToolParams p) { ... }
        internal static object TextInput(ToolParams p) { ... }

        // Internal helpers
        private static void SimulateMouseMove(Vector2 screenPos) { ... }
        private static void SimulateMouseButton(int button, bool pressed) { ... }
        private static void SimulateKey(string keyName, bool pressed) { ... }
    }
}
```

Dual backend pattern per method:
```csharp
private static void SimulateMouseMove(Vector2 screenPos)
{
#if UNITY_INPUT_SYSTEM
    if (Mouse.current != null)
        InputState.Change(Mouse.current.position, screenPos);
#else
    // Legacy: ExecuteEvents with synthesized PointerEventData
    // or store position for next raycast
#endif
}
```

## Related Code Files

### Create
- `MCPForUnity/Editor/Tools/InputSimulation/InputSimulationActions.cs`

### Modify
- `MCPForUnity/Editor/Tools/ManageInputSimulation.cs` — wire click/double_click/mouse_move/drag/scroll/key_press/key_combo/text_input actions

### Read (reference)
- `MCPForUnity/Editor/Tools/ManageInputSystem.cs` — `#if UNITY_INPUT_SYSTEM` pattern
- `MCPForUnity/Editor/Tools/InputSimulation/InputTargetResolver.cs` — target resolution

## Implementation Steps

1. Create `InputSimulationActions.cs` in `MCPForUnity/Editor/Tools/InputSimulation/`
2. Add key name mapping utility:
   - `ParseMouseButton(string name) -> int` — "left"→0, "right"→1, "middle"→2
   - `ParseKeyName(string name)` → returns `Key` (new) or `KeyCode` (legacy) via `Enum.TryParse`
3. Implement `SimulateMouseMove(Vector2 screenPos)`:
   - New: `InputState.Change(Mouse.current.position, screenPos)`
   - Legacy: store for `ExecuteEvents` raycast origin
4. Implement `SimulateMouseButton(int button, bool pressed)`:
   - New: `InputState.Change(Mouse.current.leftButton, pressed ? 1f : 0f)` (or rightButton/middleButton)
   - Legacy: create `PointerEventData`, `ExecuteEvents.Execute` on current raycast hit
5. Implement `Click(ToolParams p)`:
   - Parse `target` → `InputTargetResolver.ResolveTarget(targetObj)`
   - Parse `button` (default "left")
   - Move → press → release (using `InputTestFixture.CallProcessAfterEvents()` or similar to flush)
   - Return `SuccessResponse("Clicked at (x, y)", new { x, y, button })`
6. Implement `DoubleClick(ToolParams p)`:
   - Two click sequences
7. Implement `MouseMove(ToolParams p)`:
   - Move only, no press
8. Implement `Drag(ToolParams p)`:
   - Parse `from_target`, `to_target`, `duration_ms`
   - Move to from → press → move to to → release
   - Note: for immediate (sync) drag, just teleport from → to. Duration only matters in sequence mode.
9. Implement `Scroll(ToolParams p)`:
   - New: `InputState.Change(Mouse.current.scroll, new Vector2(deltaX, deltaY))`
   - Legacy: `ExecuteEvents.ExecuteHierarchy(target, scrollEventData, ExecuteEvents.scrollHandler)`
10. Implement `KeyPress(ToolParams p)`:
    - Parse `key` name, `mode` (press/release/tap)
    - New: `InputState.Change(Keyboard.current[parsedKey], pressed ? 1f : 0f)`
    - Legacy: `Event` queue
    - tap = press + release
11. Implement `KeyCombo(ToolParams p)`:
    - Parse `modifiers[]` and `key`
    - Press each modifier, press key, release key, release modifiers (reverse order)
12. Implement `TextInput(ToolParams p)`:
    - Parse `text`
    - New: for each char, `QueueTextEvent` on Keyboard
    - Legacy: `Event` with character field per char
13. Wire all new actions into `ManageInputSimulation.cs` switch statement

## Todo

- [ ] Create `InputSimulationActions.cs`
- [ ] Implement mouse button & key name parsing helpers
- [ ] Implement `SimulateMouseMove` (new + legacy)
- [ ] Implement `SimulateMouseButton` (new + legacy)
- [ ] Implement `Click` action
- [ ] Implement `DoubleClick` action
- [ ] Implement `MouseMove` action
- [ ] Implement `Drag` action
- [ ] Implement `Scroll` action
- [ ] Implement `SimulateKey` (new + legacy)
- [ ] Implement `KeyPress` action
- [ ] Implement `KeyCombo` action
- [ ] Implement `TextInput` action
- [ ] Wire all actions into ManageInputSimulation.cs dispatch
- [ ] Verify compilation with and without Input System package

## Success Criteria

- click/double_click/mouse_move/drag/scroll work with New Input System
- key_press/key_combo/text_input work with New Input System
- Legacy fallback compiles and provides partial coverage (UI events)
- All actions return structured SuccessResponse or ErrorResponse
- Compilation succeeds with `#if UNITY_INPUT_SYSTEM` both true and false

## Risk Assessment

| Risk | L | I | Score | Mitigation |
|------|---|---|-------|------------|
| InputState.Change doesn't trigger downstream handlers | 3 | 4 | 12 | Also call `InputSystem.Update()` after state change; test in actual Unity |
| Legacy drag has no clean API | 3 | 3 | 9 | Sync drag = teleport from→to; async drag in Phase 3 sequence |
| Key name parsing inconsistency | 2 | 2 | 4 | Normalize to lowercase; provide common aliases (space, enter, escape) |
| File exceeds 200 lines | 3 | 2 | 6 | Mouse in one region, keyboard in another; split if >250 |

## Timeline

Effort: L (large) — ~5-6 hours. Most actions, dual backend, thorough key/button mapping.
