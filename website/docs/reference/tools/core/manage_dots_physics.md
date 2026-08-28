---
title: manage_dots_physics
sidebar_label: manage_dots_physics
description: "Debug Unity DOTS Physics at runtime."
---

# `manage_dots_physics`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_dots_physics`

## Description

Debug Unity DOTS Physics at runtime. Actions: get_physics_world (body/joint counts), raycast (cast ray, get hit entities with position/normal), overlap_aabb (find bodies in axis-aligned bounding box), list_colliders (list entities with PhysicsCollider), get_body (inspect physics body — position, velocity, collider type). Requires com.unity.physics package. Works best during Play mode.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['get_physics_world', 'raycast', 'overlap_aabb', 'list_colliders', 'get_body']` | yes | Action to perform on DOTS Physics data. |
| `origin` | `str \| None` | — | Ray origin as 'x,y,z' (for raycast) |
| `direction` | `str \| None` | — | Ray direction as 'x,y,z' (for raycast) |
| `max_distance` | `float \| None` | — | Max ray distance (default 100) |
| `min` | `str \| None` | — | AABB min corner as 'x,y,z' (for overlap_aabb) |
| `max` | `str \| None` | — | AABB max corner as 'x,y,z' (for overlap_aabb) |
| `body_index` | `int \| None` | — | Physics body index (for get_body) |
| `world` | `str \| None` | — | Target world name |
| `page_size` | `int \| None` | — | Max results to return (default 20) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

