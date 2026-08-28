---
title: manage_navigation
sidebar_label: manage_navigation
description: "Unity AI Navigation management."
---

# `manage_navigation`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_navigation`

## Description

Unity AI Navigation management. Actions: list_surfaces (all NavMeshSurface components), bake (bake NavMesh for a surface), clear (clear baked NavMesh), list_agents (all NavMeshAgent components), get_agent (speed, radius, destination, path status), set_agent_destination (set agent destination — Play mode), list_obstacles (all NavMeshObstacle components), sample_position (nearest point on NavMesh), calculate_path (path between two points). Requires com.unity.ai.navigation package.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['list_surfaces', 'bake', 'clear', 'list_agents', 'get_agent', 'set_agent_destination', 'list_obstacles', 'sample_position', 'calculate_path']` | yes | Action to perform on Unity AI Navigation. |
| `target` | `str \| None` | — | GameObject name or instance ID |
| `position` | `str \| None` | — | Position as 'x,y,z' |
| `start` | `str \| None` | — | Start position as 'x,y,z' (for calculate_path) |
| `end` | `str \| None` | — | End position as 'x,y,z' (for calculate_path) |
| `max_distance` | `float \| None` | — | Max sample distance (for sample_position) |
| `area_mask` | `int \| None` | — | NavMesh area mask (default -1 = all) |
| `page_size` | `int \| None` | — | Max results to return (default 50) |
| `cursor` | `int \| None` | — | Pagination cursor (0-based offset) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

