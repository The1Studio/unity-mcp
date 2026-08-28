---
title: manage_splines
sidebar_label: manage_splines
description: "Unity Splines operations."
---

# `manage_splines`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_splines`

## Description

Unity Splines operations. Actions: list_splines (all SplineContainer components), get_spline (knot count, length, closed state), get_knot (knot position, rotation, tangents), add_knot (add knot at position), remove_knot (remove knot by index), set_knot (modify knot position/tangents), evaluate (position/tangent/up at t 0-1). Requires com.unity.splines package.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['list_splines', 'get_spline', 'get_knot', 'add_knot', 'remove_knot', 'set_knot', 'evaluate']` | yes | Action to perform on Unity Splines. |
| `target` | `str \| None` | — | GameObject name or instance ID with SplineContainer |
| `spline_index` | `int \| None` | — | Index of spline in container (default 0) |
| `knot_index` | `int \| None` | — | Knot index within spline |
| `position` | `str \| None` | — | Knot position as 'x,y,z' |
| `rotation` | `str \| None` | — | Knot rotation as 'x,y,z,w' quaternion |
| `t` | `float \| None` | — | Normalized position along spline (0-1) for evaluate |
| `page_size` | `int \| None` | — | Max results to return (default 50) |
| `cursor` | `int \| None` | — | Pagination cursor (0-based offset) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

