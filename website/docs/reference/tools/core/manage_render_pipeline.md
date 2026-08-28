---
title: manage_render_pipeline
sidebar_label: manage_render_pipeline
description: "Unity render pipeline management (URP/HDRP)."
---

# `manage_render_pipeline`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_render_pipeline`

## Description

Unity render pipeline management (URP/HDRP). Actions: get_pipeline_info (active render pipeline type, asset name), list_volumes (all Volume components — global/local), get_volume (volume profile overrides and values), set_volume_override (modify a volume override value), list_renderer_features (URP renderer features), get_render_pipeline_asset (active pipeline asset settings), list_post_processing (active post-processing effects summary), toggle_volume_override (enable/disable a specific override). Uses built-in GraphicsSettings — no package dependency.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['get_pipeline_info', 'list_volumes', 'get_volume', 'set_volume_override', 'list_renderer_features', 'get_render_pipeline_asset', 'list_post_processing', 'toggle_volume_override']` | yes | Action to perform on Unity render pipeline. |
| `target` | `str \| None` | — | Volume GameObject name or instance ID |
| `override_type` | `str \| None` | — | Volume override type name (e.g. Bloom, ColorAdjustments) |
| `property` | `str \| None` | — | Override property name to set |
| `value` | `str \| None` | — | Value to set (string representation) |
| `enabled` | `bool \| None` | — | Enable/disable override (for toggle_volume_override) |
| `page_size` | `int \| None` | — | Max results to return (default 50) |
| `cursor` | `int \| None` | — | Pagination cursor (0-based offset) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

