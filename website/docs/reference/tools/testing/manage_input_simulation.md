---
title: manage_input_simulation
sidebar_label: manage_input_simulation
description: "Play Mode input simulation."
---

# `manage_input_simulation`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `testing` &nbsp;·&nbsp; **Module:** `services.tools.manage_input_simulation`

## Description

Play Mode input simulation. Actions: discover (list interactive UI elements), get_element_bounds (bounding rect of a UI element), click (left/right/middle mouse click), double_click (double mouse click), mouse_move (move cursor to position), drag (click-hold-drag from one target to another), scroll (mouse wheel scroll), key_press (press/release/tap a key), key_combo (key with modifier keys, e.g. Ctrl+Z), text_input (type a string of text), touch_tap (single touch tap), touch_swipe (touch swipe gesture), touch_pinch (two-finger pinch gesture), sequence (execute multiple input steps in order), status (poll async sequence job by job_id).

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['discover', 'get_element_bounds', 'click', 'double_click', 'mouse_move', 'drag', 'scroll', 'key_press', 'key_combo', 'text_input', 'touch_tap', 'touch_swipe', 'touch_pinch', 'sequence', 'status']` | yes | Action to perform |
| `target` | `dict \| None` | — | Target: {mode:'coordinates'\|'gameobject'\|'ui_element'\|'anchor', ...} |
| `from_target` | `dict \| None` | — | Source target for drag/swipe |
| `to_target` | `dict \| None` | — | Destination target for drag/swipe |
| `center_target` | `dict \| None` | — | Center target for pinch |
| `button` | `str \| None` | — | Mouse button: left, right, middle |
| `key` | `str \| None` | — | Key name (e.g. space, a, escape) |
| `modifiers` | `list[str] \| None` | — | Modifier keys (ctrl, shift, alt) |
| `mode` | `str \| None` | — | Key mode: press, release, tap |
| `text` | `str \| None` | — | Text to type |
| `delta_x` | `float \| None` | — | Scroll delta X |
| `delta_y` | `float \| None` | — | Scroll delta Y |
| `duration_ms` | `int \| None` | — | Duration in ms for drag/swipe/pinch |
| `start_distance` | `float \| None` | — | Start distance for pinch (px) |
| `end_distance` | `float \| None` | — | End distance for pinch (px) |
| `steps` | `list[dict] \| None` | — | Sequence steps [{action, params, delay_ms}] |
| `job_id` | `str \| None` | — | Job ID for status polling |
| `flush` | `bool \| None` | — | Flush input immediately (default true) |
| `capture_after` | `bool \| None` | — | Take screenshot after action (default false) |
| `filter` | `str \| None` | — | Filter for discover (substring match) |
| `page_size` | `int \| None` | — | Max results (default 50) |
| `cursor` | `int \| None` | — | Pagination cursor |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

