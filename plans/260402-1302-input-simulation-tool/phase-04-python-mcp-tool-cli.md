---
phase: 4
status: pending
effort: M
blocked_by: [1]
---

# Phase 4: Python MCP Tool + CLI

## Context Links

- Pattern: `Server/src/services/tools/manage_input_system.py` — single composite tool, param routing, `send_with_unity_instance`
- Pattern: `Server/src/services/tools/run_tests.py` — complex tool with models, polling support
- Pattern: `Server/src/services/registry/tool_registry.py` — `@mcp_for_unity_tool` decorator, `group` param, TOOL_GROUPS
- Pattern: `Server/src/cli/commands/editor.py` — Click commands, `@handle_unity_errors`, `run_command`, `format_output`
- Pattern: `Server/src/cli/main.py:248-280` — `optional_commands` list for CLI registration

## Overview

Create the Python MCP tool that exposes all input simulation actions to AI assistants, plus CLI commands for manual developer use.

## Key Insights

- Python tool is a thin pass-through: validate params Python-side, serialize to JSON, send to Unity C#, return response.
- Use `Annotated[Literal[...], "..."]` for `action` param to enumerate all valid actions.
- The `target` param is a JSON object with `mode` + mode-specific fields. Python type: `dict | None`.
- For `sequence`, the `steps` param is a list of dicts.
- CLI commands: one `@click.group()` called `input_simulation` with subcommands for each action.
- CLI uses `run_command("manage_input_simulation", params, config)` — same pattern as all other CLI commands.

## Requirements

### Functional
- `manage_input_simulation` MCP tool with all 15 actions as Literal type
- All target params accept JSON object `{ "mode": "...", ... }`
- `sequence` param `steps` is `list[dict]`
- CLI group `input-simulation` with subcommands: discover, bounds, click, double-click, move, drag, scroll, key, combo, text, tap, swipe, pinch, sequence, status
- Tool description lists all actions concisely

### Non-Functional
- Group: `"testing"` (matches C# side)
- `ToolAnnotations(title="Manage Input Simulation", destructiveHint=True)` — simulating input can change game state
- CLI subcommand names use hyphens (click convention), but send snake_case actions to Unity

## Architecture

```python
# manage_input_simulation.py
@mcp_for_unity_tool(
    group="testing",
    description="Play Mode input simulation. Actions: discover, get_element_bounds, "
                "click, double_click, mouse_move, drag, scroll, "
                "key_press, key_combo, text_input, "
                "touch_tap, touch_swipe, touch_pinch, sequence, status.",
    annotations=ToolAnnotations(title="Manage Input Simulation", destructiveHint=True),
)
async def manage_input_simulation(
    ctx: Context,
    action: Annotated[Literal[...], "Action to perform"],
    target: Annotated[dict, "Target spec"] | None = None,
    # ... per-action optional params
) -> dict[str, Any]:
    ...
```

```python
# input_simulation.py (CLI)
@click.group()
def input_simulation():
    """Input simulation — interact with running Unity games."""
    pass

@input_simulation.command("click")
@click.option("--target", "-t", required=True, help="Target JSON")
@click.option("--button", "-b", default="left")
@handle_unity_errors
def click_cmd(target, button):
    ...
```

## Related Code Files

### Create
- `Server/src/services/tools/manage_input_simulation.py`
- `Server/src/cli/commands/input_simulation.py`

### Modify
- `Server/src/cli/main.py` — add to `optional_commands` list

### Read (reference)
- `Server/src/services/tools/manage_input_system.py`
- `Server/src/services/tools/run_tests.py`
- `Server/src/cli/commands/editor.py`

## Implementation Steps

1. Create `Server/src/services/tools/manage_input_simulation.py`:
   - Imports: `Annotated, Any, Literal`, `Context`, `ToolAnnotations`, registry, transport
   - `@mcp_for_unity_tool(group="testing", description=..., annotations=...)`
   - Function signature with all params:
     ```python
     async def manage_input_simulation(
         ctx: Context,
         action: Annotated[Literal[
             "discover", "get_element_bounds",
             "click", "double_click", "mouse_move", "drag", "scroll",
             "key_press", "key_combo", "text_input",
             "touch_tap", "touch_swipe", "touch_pinch",
             "sequence", "status"
         ], "Action to perform"],
         target: Annotated[dict, "Target: {mode:'coordinates'|'gameobject'|'ui_element'|'anchor', ...}"] | None = None,
         from_target: Annotated[dict, "Source target for drag/swipe"] | None = None,
         to_target: Annotated[dict, "Destination target for drag/swipe"] | None = None,
         center_target: Annotated[dict, "Center target for pinch"] | None = None,
         button: Annotated[str, "Mouse button: left, right, middle"] | None = None,
         key: Annotated[str, "Key name (e.g. space, a, escape)"] | None = None,
         modifiers: Annotated[list[str], "Modifier keys (ctrl, shift, alt)"] | None = None,
         mode: Annotated[str, "Key mode: press, release, tap"] | None = None,
         text: Annotated[str, "Text to type"] | None = None,
         delta_x: Annotated[float, "Scroll delta X"] | None = None,
         delta_y: Annotated[float, "Scroll delta Y"] | None = None,
         duration_ms: Annotated[int, "Duration in ms for drag/swipe/pinch"] | None = None,
         start_distance: Annotated[float, "Start distance for pinch (px)"] | None = None,
         end_distance: Annotated[float, "End distance for pinch (px)"] | None = None,
         steps: Annotated[list[dict], "Sequence steps [{action, params, delay_ms}]"] | None = None,
         job_id: Annotated[str, "Job ID for status polling"] | None = None,
         flush: Annotated[bool, "Flush input immediately (default true)"] | None = None,
         capture_after: Annotated[bool, "Take screenshot after action (default false)"] | None = None,
         filter: Annotated[str, "Filter for discover (substring match)"] | None = None,
         page_size: Annotated[int, "Max results (default 50)"] | None = None,
         cursor: Annotated[int, "Pagination cursor"] | None = None,
     ) -> dict[str, Any]:
     ```
   - Body: build params dict, strip None values, send via `send_with_unity_instance`
   - Response handling: same pattern as `manage_input_system.py`

2. Create `Server/src/cli/commands/input_simulation.py`:
   - `@click.group()` named `input_simulation`
   - Helper: `_build_target(target_json: str) -> dict` — parse JSON string to dict
   - Subcommands:
     - `discover` — `--filter`, `--page-size`, `--cursor`
     - `bounds` — `--target` (required)
     - `click` — `--target` (required), `--button`
     - `double-click` — `--target`, `--button`
     - `move` — `--target`
     - `drag` — `--from`, `--to`, `--duration`
     - `scroll` — `--target`, `--dx`, `--dy`
     - `key` — `KEY_NAME`, `--mode`
     - `combo` — `KEY_NAME`, `--mod` (multiple)
     - `text` — `TEXT`
     - `tap` — `--target`
     - `swipe` — `--from`, `--to`, `--duration`
     - `pinch` — `--center`, `--start-dist`, `--end-dist`, `--duration`
     - `sequence` — `STEPS_JSON`
     - `status` — `JOB_ID`
   - Each subcommand: parse args, build params dict, `run_command("manage_input_simulation", params, config)`, format output

3. Add to `Server/src/cli/main.py` `optional_commands`:
   ```python
   ("cli.commands.input_simulation", "input_simulation"),
   ```

## Todo

- [ ] Create `manage_input_simulation.py` MCP tool
- [ ] Define all action Literal types
- [ ] Define all optional params with Annotated types
- [ ] Implement param building and Unity send
- [ ] Create `input_simulation.py` CLI group
- [ ] Implement `discover` subcommand
- [ ] Implement `bounds` subcommand
- [ ] Implement `click` subcommand
- [ ] Implement `double-click` subcommand
- [ ] Implement `move` subcommand
- [ ] Implement `drag` subcommand
- [ ] Implement `scroll` subcommand
- [ ] Implement `key` subcommand
- [ ] Implement `combo` subcommand
- [ ] Implement `text` subcommand
- [ ] Implement `tap` subcommand
- [ ] Implement `swipe` subcommand
- [ ] Implement `pinch` subcommand
- [ ] Implement `sequence` subcommand
- [ ] Implement `status` subcommand
- [ ] Register CLI in `main.py`
- [ ] Run `cd Server && uv run python -c "import services.tools.manage_input_simulation"` to verify import

## Success Criteria

- `manage_input_simulation` tool appears in FastMCP tool listing under "testing" group
- All 15 actions accepted by Literal type
- None params stripped before send
- CLI group registers without errors
- Each CLI subcommand sends correct action + params to Unity
- Import succeeds without errors

## Risk Assessment

| Risk | L | I | Score | Mitigation |
|------|---|---|-------|------------|
| Too many params on one tool signature | 2 | 2 | 4 | Common pattern in codebase; AI assistants handle well |
| CLI target JSON parsing errors | 3 | 2 | 6 | Use `parse_json_dict_or_exit` from existing utils |
| Forgetting to register CLI in main.py | 1 | 3 | 3 | Explicit step in checklist |

## Timeline

Effort: M (medium) — ~3-4 hours. MCP tool is straightforward mirroring. CLI has many subcommands but each is boilerplate.
