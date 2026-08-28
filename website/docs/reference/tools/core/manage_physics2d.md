---
title: manage_physics2d
sidebar_label: manage_physics2d
description: "Unity 2D physics operations."
---

# `manage_physics2d`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_physics2d`

## Description

Unity 2D physics operations. Actions: raycast (2D raycast), raycast_all (all 2D hits), overlap_circle (entities in circle), overlap_box (entities in box), list_rigidbodies (all Rigidbody2D), get_rigidbody (body type, mass, velocity, gravity scale), list_colliders (all Collider2D), get_physics2d_settings (gravity, collision matrix). Uses built-in UnityEngine.Physics2D — no package dependency.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['raycast', 'raycast_all', 'overlap_circle', 'overlap_box', 'list_rigidbodies', 'get_rigidbody', 'list_colliders', 'get_physics2d_settings']` | yes | Action to perform on Unity 2D physics. |
| `origin` | `str \| None` | — | Ray origin as 'x,y' |
| `direction` | `str \| None` | — | Ray direction as 'x,y' |
| `max_distance` | `float \| None` | — | Max ray distance (default 100) |
| `layer_mask` | `int \| None` | — | Layer mask for filtering (default -1 = all) |
| `center` | `str \| None` | — | Overlap center as 'x,y' |
| `radius` | `float \| None` | — | Circle radius (for overlap_circle) |
| `size` | `str \| None` | — | Box size as 'x,y' (for overlap_box) |
| `angle` | `float \| None` | — | Box rotation angle (for overlap_box) |
| `target` | `str \| None` | — | GameObject name or instance ID |
| `page_size` | `int \| None` | — | Max results to return (default 50) |
| `cursor` | `int \| None` | — | Pagination cursor (0-based offset) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

