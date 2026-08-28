---
title: manage_ui_toolkit
sidebar_label: manage_ui_toolkit
description: "Unity UI Toolkit (UIElements) operations."
---

# `manage_ui_toolkit`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_ui_toolkit`

## Description

Unity UI Toolkit (UIElements) operations. Actions: list_documents (all UIDocument components), get_document (panel settings, source UXML, root element summary), query_elements (find elements by USS selector), get_element (element properties — style, class list, text, layout), set_style (modify inline style property), list_uxml_assets (find UXML assets in project). Uses built-in UIElements — no package dependency.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['list_documents', 'get_document', 'query_elements', 'get_element', 'set_style', 'list_uxml_assets']` | yes | Action to perform on Unity UI Toolkit. |
| `target` | `str \| None` | — | GameObject name or instance ID with UIDocument |
| `query` | `str \| None` | — | USS selector to find elements (e.g. '.my-class', '#my-id', 'Button') |
| `property` | `str \| None` | — | Style property name (for set_style, e.g. 'background-color') |
| `value` | `str \| None` | — | Style property value (for set_style) |
| `filter` | `str \| None` | — | Filter string for asset search |
| `page_size` | `int \| None` | — | Max results to return (default 50) |
| `cursor` | `int \| None` | — | Pagination cursor (0-based offset) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

