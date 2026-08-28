---
title: manage_tilemap
sidebar_label: manage_tilemap
description: "Unity 2D Tilemap operations."
---

# `manage_tilemap`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_tilemap`

## Description

Unity 2D Tilemap operations. Actions: list_tilemaps (all Tilemap components), get_info (size, cell layout, tile count), get_tile (tile at position), set_tile (place tile), clear_tile (remove tile), clear_all (clear entire tilemap), get_bounds (used tile bounds), fill_area (fill rectangle with tile). Requires com.unity.2d.tilemap package.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['list_tilemaps', 'get_info', 'get_tile', 'set_tile', 'clear_tile', 'clear_all', 'get_bounds', 'fill_area']` | yes | Action to perform on Unity Tilemap. |
| `target` | `str \| None` | — | GameObject name or instance ID with Tilemap component |
| `position` | `str \| None` | — | Cell position as 'x,y,z' |
| `tile_asset` | `str \| None` | — | Asset path to TileBase asset |
| `min` | `str \| None` | — | Min cell position as 'x,y,z' (for fill_area) |
| `max` | `str \| None` | — | Max cell position as 'x,y,z' (for fill_area) |
| `page_size` | `int \| None` | — | Max results to return (default 50) |
| `cursor` | `int \| None` | — | Pagination cursor (0-based offset) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

