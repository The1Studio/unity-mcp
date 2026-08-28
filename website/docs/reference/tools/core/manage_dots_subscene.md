---
title: manage_dots_subscene
sidebar_label: manage_dots_subscene
description: "Manage Unity DOTS SubScenes at runtime."
---

# `manage_dots_subscene`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_dots_subscene`

## Description

Manage Unity DOTS SubScenes at runtime. Actions: list_subscenes (find all SubScene components in hierarchy), load_subscene (request async scene loading), unload_subscene (unload scene and destroy meta entities), get_subscene_status (streaming state, section counts, asset path), list_sections (inspect individual scene sections and their load state). Requires com.unity.entities package. Works best during Play mode.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['list_subscenes', 'load_subscene', 'unload_subscene', 'get_subscene_status', 'list_sections']` | yes | Action to perform on DOTS SubScenes. |
| `scene_name` | `str \| None` | — | SubScene name or GameObject name (for load/unload/status/sections) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

