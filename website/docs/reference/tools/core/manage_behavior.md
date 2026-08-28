---
title: manage_behavior
sidebar_label: manage_behavior
description: "Unity Behavior (AI) operations."
---

# `manage_behavior`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_behavior`

## Description

Unity Behavior (AI) operations. Actions: list_agents (all BehaviorGraphAgent components), get_agent (graph name, running state, current node), list_variables (blackboard variables on agent), get_variable (variable name, type, value), set_variable (set blackboard variable value). Requires com.unity.behavior package.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['list_agents', 'get_agent', 'list_variables', 'get_variable', 'set_variable']` | yes | Action to perform on Unity Behavior. |
| `target` | `str \| None` | — | GameObject name or instance ID with BehaviorGraphAgent |
| `variable_name` | `str \| None` | — | Blackboard variable name |
| `value` | `str \| None` | — | Variable value to set (as string) |
| `page_size` | `int \| None` | — | Max results to return (default 50) |
| `cursor` | `int \| None` | — | Pagination cursor (0-based offset) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

