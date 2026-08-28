---
title: manage_addressables
sidebar_label: manage_addressables
description: "Unity Addressables asset system operations."
---

# `manage_addressables`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_addressables`

## Description

Unity Addressables asset system operations. Actions: list_groups (all Addressable groups), get_group (entries, schemas, build/load paths), list_entries (entries in group with addresses and labels), get_entry (entry detail — GUID, address, labels), list_labels (all labels in settings), build (build Addressable content), analyze (run analyze rules for duplicates/issues). Requires com.unity.addressables package.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['list_groups', 'get_group', 'list_entries', 'get_entry', 'list_labels', 'build', 'analyze']` | yes | Action to perform on Unity Addressables. |
| `group_name` | `str \| None` | — | Addressable group name |
| `address` | `str \| None` | — | Addressable entry address |
| `guid` | `str \| None` | — | Addressable entry GUID |
| `clean` | `bool \| None` | — | Clean build (for build action) |
| `page_size` | `int \| None` | — | Max results to return (default 50) |
| `cursor` | `int \| None` | — | Pagination cursor (0-based offset) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

