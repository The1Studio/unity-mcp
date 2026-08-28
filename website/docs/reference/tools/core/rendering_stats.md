---
title: rendering_stats
sidebar_label: rendering_stats
description: "Read Unity rendering and performance statistics."
---

# `rendering_stats`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.rendering_stats`

## Description

Read Unity rendering and performance statistics. Actions: get_stats (single-frame snapshot: draw calls, batches, FPS, cpuMainMs), get_memory (allocated/reserved/mono/graphics memory), get_profiler (frame timing, time scale, system info), get_stats_aggregated (N-frame aggregated: min/max/avg/p50/p95 for FPS, CPU, draw calls — uses long-lived ProfilerRecorders, much more reliable than single snapshots. Param: frames=number of recent frames to aggregate, 0=all available), get_system_stats (per-DOTS-system CPU breakdown sorted by cost — shows which systems consume the most frame budget. Param: top_n=number of systems), get_session_report (full Play session report from start to stop — includes Markdown summary, JSON timeline, CSV. Params: include_timeline=bool, include_csv=bool), list_sessions (list saved session files from Logs/PerfSessions/ — works anytime), load_session (load a saved session JSON by filename), analyze_session (analyze a saved session: bottleneck detection, system ranking, issues. Param: filename=session JSON file). Aggregated/system/session actions require Play mode; list/load/analyze work anytime.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['get_stats', 'get_memory', 'get_profiler', 'get_stats_aggregated', 'get_system_stats', 'get_session_report', 'list_sessions', 'load_session', 'analyze_session']` | yes | Action to perform. get_stats=single snapshot, get_stats_aggregated=N-frame percentiles, get_system_stats=per-system CPU breakdown, get_session_report=full session timeline+summary, list_sessions=list saved sessions, load_session=load session JSON, analyze_session=bottleneck analysis. |
| `frames` | `int \| None` | — | For get_stats_aggregated: number of recent frames (0=all). |
| `top_n` | `int \| None` | — | For get_system_stats: number of top systems to return. |
| `include_timeline` | `bool \| None` | — | For get_session_report: include JSON timeline. |
| `include_csv` | `bool \| None` | — | For get_session_report: include CSV data. |
| `filename` | `str \| None` | — | For load_session/analyze_session: session JSON filename. |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

