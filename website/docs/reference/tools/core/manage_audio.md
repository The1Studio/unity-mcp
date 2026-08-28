---
title: manage_audio
sidebar_label: manage_audio
description: "Unity audio system management."
---

# `manage_audio`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_audio`

## Description

Unity audio system management. Actions: list_sources (all AudioSource components), get_source (full AudioSource detail), set_source (modify volume, pitch, spatial blend), play/stop/pause (control playback — Play mode only), list_clips (AudioClip assets in project), get_clip_info (clip length, frequency, channels, load type), list_mixers (AudioMixer assets), get_mixer (mixer groups, exposed params, current values), set_mixer_param (set exposed mixer float). Uses built-in UnityEngine.Audio — no package dependency.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['list_sources', 'get_source', 'set_source', 'play', 'stop', 'pause', 'list_clips', 'get_clip_info', 'list_mixers', 'get_mixer', 'set_mixer_param']` | yes | Action to perform on Unity audio. |
| `target` | `str \| None` | — | GameObject name/ID (for source actions) or asset path/name (for clip/mixer) |
| `properties` | `str \| None` | — | JSON object of properties to set |
| `param_name` | `str \| None` | — | Exposed mixer parameter name (for set_mixer_param) |
| `value` | `float \| None` | — | Value to set (for set_mixer_param) |
| `filter` | `str \| None` | — | Name filter for list operations |
| `page_size` | `int \| None` | — | Max results to return (default 50) |
| `cursor` | `int \| None` | — | Pagination cursor (0-based offset) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

