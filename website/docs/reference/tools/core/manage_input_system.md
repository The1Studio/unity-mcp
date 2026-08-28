---
title: manage_input_system
sidebar_label: manage_input_system
description: "Unity Input System inspection."
---

# `manage_input_system`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_input_system`

## Description

Unity Input System inspection. Actions: list_action_assets (find InputActionAsset in project), get_action_map (actions in a map with bindings), get_action (bindings, interactions, processors), list_devices (connected input devices), get_device (device layout, controls), list_player_inputs (find PlayerInput components). Requires com.unity.inputsystem package.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['list_action_assets', 'get_action_map', 'get_action', 'list_devices', 'get_device', 'list_player_inputs']` | yes | Action to perform on Unity Input System. |
| `asset` | `str \| None` | — | InputActionAsset name or path |
| `map_name` | `str \| None` | — | Action map name |
| `action_name` | `str \| None` | — | Input action name |
| `device_name` | `str \| None` | — | Device name or layout |
| `page_size` | `int \| None` | — | Max results to return (default 50) |
| `cursor` | `int \| None` | — | Pagination cursor (0-based offset) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

