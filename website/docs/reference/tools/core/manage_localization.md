---
title: manage_localization
sidebar_label: manage_localization
description: "Unity Localization operations."
---

# `manage_localization`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_localization`

## Description

Unity Localization operations. Actions: list_locales (available locales), get_active_locale (current active locale), set_active_locale (switch active locale), list_tables (string/asset table collections), get_entry (get localized string for key+locale), set_entry (set localized string). Requires com.unity.localization package.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['list_locales', 'get_active_locale', 'set_active_locale', 'list_tables', 'get_entry', 'set_entry']` | yes | Action to perform on Unity Localization. |
| `locale_code` | `str \| None` | — | Locale code (e.g. 'en', 'ja', 'fr') |
| `table` | `str \| None` | — | String table collection name |
| `key` | `str \| None` | — | Localization key/entry name |
| `locale` | `str \| None` | — | Target locale for get/set entry |
| `value` | `str \| None` | — | Localized string value (for set_entry) |
| `type` | `str \| None` | — | Table type: 'string' or 'asset' (for list_tables) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

