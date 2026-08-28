---
title: manage_timeline
sidebar_label: manage_timeline
description: "Unity Timeline management."
---

# `manage_timeline`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_timeline`

## Description

Unity Timeline management. Actions: list_directors (all PlayableDirector components), get_director (state, time, duration, wrap mode), play/pause/stop (control playback), set_time (seek to time), list_tracks (tracks in timeline asset), get_bindings (track-to-object bindings). Requires com.unity.timeline package.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['list_directors', 'get_director', 'play', 'pause', 'stop', 'set_time', 'list_tracks', 'get_bindings']` | yes | Action to perform on Unity Timeline. |
| `target` | `str \| None` | — | GameObject name or instance ID |
| `time` | `float \| None` | — | Time in seconds (for set_time) |
| `page_size` | `int \| None` | — | Max results to return (default 50) |
| `cursor` | `int \| None` | — | Pagination cursor (0-based offset) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

