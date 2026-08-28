---
title: manage_asset_hunter
sidebar_label: manage_asset_hunter
description: "Interact with Asset Hunter Pro to analyze project assets."
---

# `manage_asset_hunter`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_asset_hunter`

## Description

Interact with Asset Hunter Pro to analyze project assets. Actions: 'scan_unused' (find unused assets from latest build report), 'get_duplicates' (find duplicate assets by content hash), 'get_dependencies' (query asset references or reverse references), 'get_build_report' (summary of latest build report), 'get_settings' (current exclusion/ignore settings). Requires HeurekaGames Asset Hunter PRO package. Build report actions require a prior Unity build with AHP enabled.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['scan_unused', 'get_duplicates', 'get_dependencies', 'get_build_report', 'get_settings']` | yes | Action to perform. |
| `asset_path` | `str \| None` | — | Asset path for 'get_dependencies' (e.g. 'Assets/Sprites/icon.png'). |
| `direction` | `Literal['references', 'referenced_by'] \| None` | — | For 'get_dependencies': 'references' = what this asset uses, 'referenced_by' = what uses this asset. Default: 'references'. |
| `filter_type` | `str \| None` | — | For 'scan_unused': filter by asset type name (e.g. 'Texture2D', 'Material'). |
| `page_size` | `int \| str \| None` | — | Items per page (default 50, max 500). |
| `cursor` | `int \| str \| None` | — | Paging cursor (0-based offset). Use nextCursor from previous response. |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

