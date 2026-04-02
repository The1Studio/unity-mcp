---
phase: 5
status: pending
effort: S
blocked_by: [4]
---

# Phase 5: Tests

## Context Links

- Pattern: `Server/tests/integration/test_manage_input_system.py` — monkeypatch `async_send_command_with_retry`, DummyContext, captured params
- Pattern: `Server/tests/integration/test_helpers.py` — `DummyContext` class
- Test command: `cd Server && uv run pytest tests/integration/test_manage_input_simulation.py -v`

## Overview

Python integration tests for the MCP tool. Tests verify parameter routing (correct action + params sent to Unity) and error handling. These do NOT test actual Unity input simulation — that requires a running Unity instance.

## Key Insights

- Existing test pattern: monkeypatch `async_send_command_with_retry` on the tool module, capture `cmd` and `params`, verify routing.
- `_fake_send_factory(captured, response)` — reusable factory for mock send functions.
- Each test: construct DummyContext, call tool function directly, assert captured params.
- Also test: None params stripped, Python exception caught, target dict serialized correctly.

## Requirements

### Functional
- Test each action routes correct `action` param
- Test target dict passes through correctly
- Test optional params stripped when None
- Test Python exception returns `success: False`
- Test sequence `steps` list passes through
- Test `job_id` param for status action

### Non-Functional
- File: `Server/tests/integration/test_manage_input_simulation.py`
- Use `pytest.mark.asyncio`
- Follow exact pattern from `test_manage_input_system.py`

## Implementation Steps

1. Create `Server/tests/integration/test_manage_input_simulation.py`
2. Import helpers:
   ```python
   import pytest
   from .test_helpers import DummyContext
   import services.tools.manage_input_simulation as sim_mod
   ```
3. Create `_fake_send_factory` (copy pattern from test_manage_input_system.py)
4. Implement tests:
   - `test_discover` — action="discover", verify filter/page_size pass through
   - `test_get_element_bounds` — action="get_element_bounds", target dict
   - `test_click` — action="click", target + button
   - `test_double_click` — action="double_click", target
   - `test_mouse_move` — action="mouse_move", target
   - `test_drag` — action="drag", from_target + to_target + duration_ms
   - `test_scroll` — action="scroll", target + delta_x + delta_y
   - `test_key_press` — action="key_press", key + mode
   - `test_key_combo` — action="key_combo", modifiers + key
   - `test_text_input` — action="text_input", text
   - `test_touch_tap` — action="touch_tap", target
   - `test_touch_swipe` — action="touch_swipe", from_target + to_target
   - `test_touch_pinch` — action="touch_pinch", center_target + distances
   - `test_sequence` — action="sequence", steps list
   - `test_status` — action="status", job_id
   - `test_none_params_stripped` — call with minimal params, verify only non-None in captured
   - `test_python_exception_caught` — monkeypatch raising send, verify error response
   - `test_target_dict_passthrough` — verify target dict serialized as-is

## Todo

- [ ] Create test file
- [ ] Implement `_fake_send_factory`
- [ ] Implement per-action routing tests (15 actions)
- [ ] Implement `test_none_params_stripped`
- [ ] Implement `test_python_exception_caught`
- [ ] Implement `test_target_dict_passthrough`
- [ ] Run `cd Server && uv run pytest tests/integration/test_manage_input_simulation.py -v`
- [ ] Verify all tests pass

## Success Criteria

- 18+ tests covering all actions + edge cases
- All tests pass with `uv run pytest`
- No test depends on running Unity instance
- Tests follow exact same pattern as existing test files

## Risk Assessment

| Risk | L | I | Score | Mitigation |
|------|---|---|-------|------------|
| monkeypatch target differs due to import path change | 2 | 2 | 4 | Verify import matches: `sim_mod.async_send_command_with_retry` |
| DummyContext missing required fields for new tool | 1 | 2 | 2 | DummyContext is generic; no tool-specific fields needed |

## Timeline

Effort: S (small) — ~1-2 hours. Pattern is well-established; mostly boilerplate per action.
