---
title: manage_cinemachine
sidebar_label: manage_cinemachine
description: "Unity Cinemachine virtual camera management."
---

# `manage_cinemachine`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_cinemachine`

## Description

Unity Cinemachine virtual camera management. Actions: list_vcams (all CinemachineCamera components), get_vcam (priority, follow/look-at, body/aim settings), set_vcam (modify priority, follow target), get_brain (active camera, blend state, default blend), set_priority (change camera priority), list_blends (custom blend definitions). Requires com.unity.cinemachine package.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['list_vcams', 'get_vcam', 'set_vcam', 'get_brain', 'set_priority', 'list_blends']` | yes | Action to perform on Unity Cinemachine. |
| `target` | `str \| None` | — | GameObject name or instance ID |
| `properties` | `str \| None` | — | JSON object of properties to set |
| `priority` | `int \| None` | — | Camera priority value |
| `page_size` | `int \| None` | — | Max results to return (default 50) |
| `cursor` | `int \| None` | — | Pagination cursor (0-based offset) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

