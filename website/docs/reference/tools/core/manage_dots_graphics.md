---
title: manage_dots_graphics
sidebar_label: manage_dots_graphics
description: "Debug Unity DOTS Entities Graphics rendering at runtime."
---

# `manage_dots_graphics`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_dots_graphics`

## Description

Debug Unity DOTS Entities Graphics rendering at runtime. Actions: get_render_stats (count rendered entities, LOD groups), list_rendered_entities (list entities with MaterialMeshInfo), get_entity_rendering (inspect render bounds, material, mesh, filter settings), list_registered_materials (unique materials from RenderMeshArrays), list_registered_meshes (unique meshes with vertex counts). Requires com.unity.entities.graphics package. Works best during Play mode.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['get_render_stats', 'list_rendered_entities', 'get_entity_rendering', 'list_registered_materials', 'list_registered_meshes']` | yes | Action to perform on DOTS Graphics data. |
| `entity_index` | `int \| None` | — | Entity index (for get_entity_rendering) |
| `world` | `str \| None` | — | Target world name |
| `page_size` | `int \| None` | — | Max results to return (default 20) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

