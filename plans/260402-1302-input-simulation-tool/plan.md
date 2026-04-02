---
status: completed
created: 2026-04-02
branch: feat/update-tools
base: beta
---

# Plan: manage_input_simulation Tool

Play Mode input simulation tool for Unity MCP. Lets AI assistants click, type, drag, touch, and sequence interactions in running Unity games.

## Phases

| # | Phase | Scope | Status | Effort | Blocked By |
|---|-------|-------|--------|--------|------------|
| 1 | C# Target Resolution + Discovery | `ManageInputSimulation.cs`, `InputTargetResolver.cs` | pending | M | -- |
| 2 | C# Mouse + Keyboard Simulation | `InputSimulationActions.cs` (mouse/kb methods) | pending | L | Phase 1 |
| 3 | C# Touch + Sequence Mode | `InputSimulationTouch.cs`, `InputSimulationBridge.cs` | pending | M | Phase 2 |
| 4 | Python MCP Tool + CLI | `manage_input_simulation.py`, `input_simulation.py` (CLI) | pending | M | Phase 1 |
| 5 | Tests | `test_manage_input_simulation.py` | pending | S | Phase 4 |

## Files to Create

| File | Owner | Phase |
|------|-------|-------|
| `MCPForUnity/Editor/Tools/ManageInputSimulation.cs` | Phase 1-3 | Main handler + action dispatch |
| `MCPForUnity/Editor/Tools/InputSimulation/InputTargetResolver.cs` | Phase 1 | Target resolution (coords/GO/UI/anchor -> screen) |
| `MCPForUnity/Editor/Tools/InputSimulation/InputSimulationActions.cs` | Phase 2 | Mouse + keyboard injection |
| `MCPForUnity/Editor/Tools/InputSimulation/InputSimulationTouch.cs` | Phase 3 | Touch actions |
| `MCPForUnity/Runtime/InputSimulationBridge.cs` | Phase 3 | Runtime MonoBehaviour for sequences |
| `Server/src/services/tools/manage_input_simulation.py` | Phase 4 | Python MCP tool |
| `Server/src/cli/commands/input_simulation.py` | Phase 4 | CLI commands |
| `Server/tests/integration/test_manage_input_simulation.py` | Phase 5 | Integration tests |

## Files to Modify

| File | Change | Phase |
|------|--------|-------|
| `Server/src/cli/main.py` | Add `("cli.commands.input_simulation", "input_simulation")` to `optional_commands` | Phase 4 |

## Architecture

```
AI ─► Python manage_input_simulation ─► WebSocket ─► C# ManageInputSimulation
  HandleCommand dispatches by action:
  ├─ discover / get_element_bounds → InputTargetResolver
  ├─ click / double_click / mouse_move / drag / scroll → InputSimulationActions
  ├─ key_press / key_combo / text_input → InputSimulationActions
  ├─ touch_tap / touch_swipe / touch_pinch → InputSimulationTouch
  └─ sequence → InputSimulationBridge (async polling)
```

Target resolution chain:
- `coordinates` → direct (x, y) passthrough
- `gameobject` → `GameObjectLookup.FindByTarget()` → `Renderer.bounds` → `Camera.main.WorldToScreenPoint()`
- `ui_element` → find `Selectable`/`VisualElement` → `RectTransformUtility.WorldToScreenPoint()`
- `anchor` → screen percentage (center/top_left/etc.) + pixel offset

## Conditional Compilation Strategy

ManageInputSimulation.cs outer file: no #if guard (always compiles).
InputSimulationActions.cs and InputSimulationTouch.cs: `#if UNITY_INPUT_SYSTEM` blocks for New Input System path; `#else` blocks for legacy EventSystem fallback.

## Validation Decisions (2026-04-02)

| Question | Decision |
|----------|----------|
| Input flush after InputState.Change | Configurable `flush` param (default true = call InputSystem.Update() immediately) |
| Runtime assembly for Bridge | Keep in Runtime (as planned). JObject dep acceptable |
| Legacy Input support | Keep (as planned). Partial coverage via ExecuteEvents |
| Tool count | Single tool with 16 actions (as planned) |
| Camera for WorldToScreenPoint | Camera.main default + optional `camera_name`/`camera_tag` in target spec |
| Editor↔Runtime communication | Static callback pattern — Editor calls Bridge.StartSequence() directly |
| Discovery scope | UI elements (Selectable, VisualElement) + ALL GameObjects with Colliders, paginated |
| Visual feedback | Optional `capture_after` param — returns base64 screenshot in response when true |

## Dependencies

- `MCPForUnity.Editor.Helpers` (ToolParams, Response, Pagination, GameObjectLookup)
- `UnityEngine.InputSystem` (optional, via conditional compilation)
- `UnityEngine.EventSystems` (for legacy fallback)
- `MCPForUnity.Runtime` assembly (for InputSimulationBridge)

## Risk Assessment

| Risk | L | I | Score | Mitigation |
|------|---|---|-------|------------|
| Legacy Input.GetKey() unfakeable | 4 | 3 | 12 | Document limitation; legacy only covers UI via ExecuteEvents |
| Screen coords fragile across resolutions | 3 | 3 | 9 | Default to name-based targeting; anchor system |
| Play Mode guard forgotten | 2 | 4 | 8 | Central guard in HandleCommand — single check |
| ManageInputSimulation.cs exceeds 200 lines | 4 | 2 | 8 | Split into InputSimulation/ subfolder helpers |
| Sequence async polling complexity | 3 | 3 | 9 | Reuse proven RunTests/TestJobManager pattern |
| Touch requires InputSystem package | 3 | 2 | 6 | #if guard; clear error when missing |
| InputSimulationBridge needs Runtime assembly ref | 2 | 3 | 6 | Reference MCPForUnity.Runtime from Editor asmdef |

## Timeline

| Phase | Effort | Notes |
|-------|--------|-------|
| Phase 1 | M | Foundation — target resolution + discovery |
| Phase 2 | L | Most actions — mouse/keyboard + dual backend |
| Phase 3 | M | Touch + async sequence |
| Phase 4 | M | Python side — straightforward mirroring |
| Phase 5 | S | Test patterns well-established |
| **Total** | **~L** | Critical path: 1 → 2 → 3; Phase 4 can start after Phase 1 |
