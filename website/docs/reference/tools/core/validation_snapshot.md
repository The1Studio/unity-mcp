---
title: validation_snapshot
sidebar_label: validation_snapshot
description: "Capture a full runtime validation snapshot in ONE call — entity counts (total/alive/dead/by-team), health distribution (min/max/mean), position samples, NaN bounds check, rendering stats (FPS/draw calls/batches), battle state, console er…"
---

# `validation_snapshot`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.validation_snapshot`

## Description

Capture a full runtime validation snapshot in ONE call — entity counts (total/alive/dead/by-team), health distribution (min/max/mean), position samples, NaN bounds check, rendering stats (FPS/draw calls/batches), battle state, console errors, editor state. Use 'compare' to diff two snapshots and detect movement, anomalies, deltas. Requires com.unity.entities + DOTSRPG components. Play mode required for entity data.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['capture', 'compare']` | yes | Action: 'capture' collects all validation data, 'compare' diffs two snapshots. |
| `sample_size` | `int \| None` | — | Number of entity positions to sample (default 20, max 100). For 'capture' only. |
| `snapshot_a` | `dict[str, Any] \| str \| None` | — | Previous snapshot JSON (for 'compare' action). |
| `snapshot_b` | `dict[str, Any] \| str \| None` | — | Current snapshot JSON (for 'compare' action). |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

