---
title: manage_lighting
sidebar_label: manage_lighting
description: "Unity lighting system management."
---

# `manage_lighting`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_lighting`

## Description

Unity lighting system management. Actions: list_lights (all Light components, optional type filter), get_light (full light properties), set_light (modify color, intensity, range, shadows), bake (trigger lightmap bake — async), cancel_bake (cancel in-progress bake), get_bake_status (is baking? progress?), list_probes (light probes + reflection probes), get_probe (probe detail — type, bounds, mode), get_environment (RenderSettings — ambient, fog, skybox, sun), set_environment (modify RenderSettings), get_lightmap_settings (lightmapper config). Uses built-in Unity lighting APIs — no package dependency.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['list_lights', 'get_light', 'set_light', 'bake', 'cancel_bake', 'get_bake_status', 'list_probes', 'get_probe', 'get_environment', 'set_environment', 'get_lightmap_settings']` | yes | Action to perform on Unity lighting. |
| `target` | `str \| None` | — | GameObject name or instance ID |
| `properties` | `str \| None` | — | JSON object of properties to set |
| `type_filter` | `str \| None` | — | Light type filter: Directional, Point, Spot, Area |
| `page_size` | `int \| None` | — | Max results to return (default 50) |
| `cursor` | `int \| None` | — | Pagination cursor (0-based offset) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

