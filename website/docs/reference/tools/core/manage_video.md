---
title: manage_video
sidebar_label: manage_video
description: "Unity VideoPlayer operations."
---

# `manage_video`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_video`

## Description

Unity VideoPlayer operations. Actions: list_players (all VideoPlayer components), get_player (URL/clip, playback state, time, length), set_player (modify source, playback speed, loop, audio output), play (play video), pause (pause video), stop (stop video), set_time (seek to time). Uses built-in UnityEngine.Video — no package dependency.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['list_players', 'get_player', 'set_player', 'play', 'pause', 'stop', 'set_time']` | yes | Action to perform on Unity VideoPlayer. |
| `target` | `str \| None` | — | GameObject name or instance ID with VideoPlayer |
| `properties` | `str \| None` | — | JSON object with properties to set (for set_player) |
| `time` | `float \| None` | — | Time in seconds (for set_time) |
| `page_size` | `int \| None` | — | Max results to return (default 50) |
| `cursor` | `int \| None` | — | Pagination cursor (0-based offset) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

