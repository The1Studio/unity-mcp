---
phase: 1
status: pending
effort: M
blocks: [2, 3]
---

# Phase 1: C# Target Resolution + Discovery

## Context Links

- Pattern: `MCPForUnity/Editor/Tools/ManageInputSystem.cs` — action dispatch, `#if UNITY_INPUT_SYSTEM`, ToolParams
- Pattern: `MCPForUnity/Editor/Helpers/GameObjectLookup.cs` — `FindByTarget(JToken, string, bool)` for GO lookup
- Pattern: `MCPForUnity/Editor/Helpers/ObjectResolver.cs` — flexible object resolution
- Pattern: `MCPForUnity/Editor/Helpers/Pagination.cs` — `PaginationRequest.FromParams()`, `PaginationResponse<T>.Create()`
- Pattern: `MCPForUnity/Editor/Helpers/ToolParams.cs` — `GetRequired()`, `Get()`, `GetInt()`, `GetFloat()`, `GetBool()`
- Pattern: `MCPForUnity/Editor/Helpers/Response.cs` — `SuccessResponse`, `ErrorResponse`

## Overview

Build the main entry-point `ManageInputSimulation.cs` (action dispatch + Play Mode guard) and the `InputTargetResolver` helper that converts all 4 targeting modes to screen coordinates. Implement `discover` and `get_element_bounds` actions.

## Key Insights

- `GameObjectLookup.FindByTarget(JToken, string, bool)` already handles by_name, by_path, by_id, by_tag, by_id_or_name_or_path. Reuse it for `gameobject` target mode.
- UI Toolkit `VisualElement` world rect: `ve.worldBound` gives `Rect` in panel space. For UGUI `RectTransform`, use `RectTransformUtility.PixelAdjustRect()`.
- `Camera.main.WorldToScreenPoint()` for 3D GameObjects. Note: Unity screen coords are bottom-left origin; Input System expects this.
- Discovery needs to enumerate: `Selectable.allSelectablesArray` (UGUI), root `VisualElement` tree (UI Toolkit), plus renderable GameObjects.

## Requirements

### Functional
- Parse `target` param as JSON object with `mode` field: `coordinates`, `gameobject`, `ui_element`, `anchor`
- `coordinates` mode: extract `x`, `y` directly
- `gameobject` mode: extract `name` or `path` or `instance_id`, find GO, get screen center via renderer bounds. Optional `camera_name`/`camera_tag` to override Camera.main
- `ui_element` mode: extract `name`, find Selectable or VisualElement, get screen rect center
- `anchor` mode: extract `anchor` (center, top_left, top_right, bottom_left, bottom_right, top_center, bottom_center, center_left, center_right) + optional `offset_x`, `offset_y`
- `discover` action: paginated list of interactable elements with name, type (ugui/uitoolkit/gameobject), screen rect
- `get_element_bounds` action: screen rect for a single target
- All actions require Play Mode — return clear error if `!EditorApplication.isPlaying`

### Non-Functional
- ManageInputSimulation.cs must stay under 200 lines (just dispatch + guard)
- InputTargetResolver.cs handles all resolution logic

## Architecture

```csharp
// ManageInputSimulation.cs — ~80 lines
[McpForUnityTool("manage_input_simulation", AutoRegister = true, Group = "testing")]
public static class ManageInputSimulation
{
    public static object HandleCommand(JObject @params)
    {
        // 1. Play Mode guard
        // 2. Parse action
        // 3. Switch dispatch to appropriate handler
    }
}

// InputTargetResolver.cs — ~150 lines
internal static class InputTargetResolver
{
    // Returns (Vector2 screenPos, string error)
    internal static (Vector2?, string) ResolveTarget(JObject targetObj) { ... }
    internal static (Rect?, string) ResolveTargetBounds(JObject targetObj) { ... }
    internal static object Discover(ToolParams p) { ... }
    internal static object GetElementBounds(ToolParams p) { ... }
}
```

## Related Code Files

### Create
- `MCPForUnity/Editor/Tools/ManageInputSimulation.cs`
- `MCPForUnity/Editor/Tools/InputSimulation/InputTargetResolver.cs`

### Read (reference patterns)
- `MCPForUnity/Editor/Tools/ManageInputSystem.cs`
- `MCPForUnity/Editor/Helpers/GameObjectLookup.cs`
- `MCPForUnity/Editor/Helpers/Pagination.cs`
- `MCPForUnity/Editor/Helpers/ToolParams.cs`

## Implementation Steps

1. Create directory `MCPForUnity/Editor/Tools/InputSimulation/`
2. Create `ManageInputSimulation.cs`:
   - Namespace: `MCPForUnity.Editor.Tools`
   - Attribute: `[McpForUnityTool("manage_input_simulation", AutoRegister = true, Group = "testing")]`
   - `public static object HandleCommand(JObject @params)`
   - First line: `if (!EditorApplication.isPlaying) return new ErrorResponse("Play Mode required. Use manage_editor play first.");`
   - Parse action via `ToolParams.GetRequired("action")`
   - Switch on action: `discover`, `get_element_bounds` → `InputTargetResolver` methods
   - All other actions (click, key_press, etc.) → placeholder `return new ErrorResponse("Not implemented: " + action);` (filled in Phase 2/3)
3. Create `InputTargetResolver.cs`:
   - Namespace: `MCPForUnity.Editor.Tools.InputSimulation`
   - `internal static class InputTargetResolver`
   - **ResolveTarget(JObject targetObj) -> (Vector2? screenPos, string error)**:
     - Read `targetObj["mode"]` (required)
     - `"coordinates"`: read `x`, `y` from targetObj, return as Vector2
     - `"gameobject"`: read `name`/`path`/`instance_id`, call `GameObjectLookup.FindByTarget()`, get `Renderer.bounds.center`. Camera: check `camera_name`/`camera_tag` in target, fallback to `Camera.main`. Error if no camera. `camera.WorldToScreenPoint()`, return xy
     - `"ui_element"`: read `name`, search `Selectable.allSelectablesArray` by GO name, get `RectTransform` screen center; also check UI Toolkit panels
     - `"anchor"`: read `anchor` string, map to screen fraction, add `offset_x`/`offset_y`
   - **ResolveTargetBounds(JObject targetObj) -> (Rect? screenRect, string error)**:
     - Same resolution but return full Rect instead of center point
   - **Discover(ToolParams p) -> object**:
     - Collect UGUI Selectables: `Selectable.allSelectablesArray` → name, type="ugui", screen rect
     - Collect UI Toolkit: iterate `UIDocument` instances → root VisualElement tree → interactive elements (Button, Toggle, etc.)
     - Collect ALL GameObjects with Colliders (type="gameobject", screen rect via camera projection)
     - Filter by optional `filter` param (substring match on name)
     - Paginate via `PaginationRequest.FromParams()` / `PaginationResponse<T>.Create()`
     - Return `SuccessResponse` with paginated items
   - **GetElementBounds(ToolParams p) -> object**:
     - Parse target param, call `ResolveTargetBounds`, return screen Rect

## Todo

- [ ] Create `MCPForUnity/Editor/Tools/InputSimulation/` directory
- [ ] Create `ManageInputSimulation.cs` with action dispatch + Play Mode guard
- [ ] Create `InputTargetResolver.cs` with all 4 target modes
- [ ] Implement `ResolveTarget()` — coordinates mode
- [ ] Implement `ResolveTarget()` — gameobject mode (GameObjectLookup + WorldToScreenPoint)
- [ ] Implement `ResolveTarget()` — ui_element mode (Selectable + VisualElement)
- [ ] Implement `ResolveTarget()` — anchor mode
- [ ] Implement `Discover()` with pagination
- [ ] Implement `GetElementBounds()`
- [ ] Verify compilation in Unity

## Success Criteria

- `ManageInputSimulation.cs` under 200 lines
- `discover` returns paginated list of interactable elements with screen rects
- `get_element_bounds` returns screen rect for any valid target
- Clear "Play Mode required" error when not playing
- Unknown action returns error listing valid actions

## Risk Assessment

| Risk | L | I | Score | Mitigation |
|------|---|---|-------|------------|
| UI Toolkit panels not accessible from Editor code | 2 | 3 | 6 | UIDocument.rootVisualElement accessible; fall back to UGUI-only |
| Camera.main null in some setups | 3 | 3 | 9 | Accept optional `camera` param; error if no camera found |
| Selectable.allSelectablesArray only returns enabled | 2 | 2 | 4 | Document: only enabled selectables discovered |

## Timeline

Effort: M (medium) — ~3-4 hours. GameObjectLookup reuse saves significant work.
