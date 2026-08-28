---
title: manage_terrain
sidebar_label: manage_terrain
description: "Inspect and modify Unity Terrain at runtime or in Edit mode."
---

# `manage_terrain`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_terrain`

## Description

Inspect and modify Unity Terrain at runtime or in Edit mode. Actions: get_info (heightmap resolution, size, layer/tree counts), get_height (sample world-space height at x/z), set_heights (paint circular brush with set/raise/lower/smooth modes), flatten (set entire heightmap to uniform normalized height), get_splat_weights (texture layer weights at world position), paint_texture (paint terrain texture layer with circular brush), get_heightmap_sample (read NxN heightmap patch around world position). All actions accept an optional 'target' param (GameObject name or instance ID) to select a specific Terrain; defaults to the active terrain in the scene.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['get_info', 'get_height', 'set_heights', 'flatten', 'get_splat_weights', 'paint_texture', 'get_heightmap_sample']` | yes | Action to perform on the Terrain. |
| `target` | `str \| None` | — | GameObject name or instance ID of the Terrain. Defaults to the active terrain. |
| `x` | `float \| None` | — | World-space X coordinate (for get_height, set_heights, get_splat_weights, paint_texture, get_heightmap_sample) |
| `z` | `float \| None` | — | World-space Z coordinate (for get_height, set_heights, get_splat_weights, paint_texture, get_heightmap_sample) |
| `radius` | `float \| None` | — | Brush radius in world units (for set_heights, paint_texture) |
| `height` | `float \| None` | — | Normalized height 0-1 (for set_heights, flatten) |
| `mode` | `Literal['set', 'raise', 'lower', 'smooth'] \| None` | — | Brush mode for set_heights (default: set) |
| `layer_index` | `int \| None` | — | Terrain layer index to paint (for paint_texture) |
| `strength` | `float \| None` | — | Paint strength 0-1 (for paint_texture) |
| `size` | `int \| None` | — | Patch size NxN in heightmap pixels, clamped to 64 (for get_heightmap_sample) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

