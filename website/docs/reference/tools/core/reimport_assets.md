---
title: reimport_assets
sidebar_label: reimport_assets
description: "Reimport specific Unity assets by path."
---

# `reimport_assets`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.reimport_assets`

## Description

Reimport specific Unity assets by path. Faster and more granular than 'Reimport All'. No confirmation dialog — works seamlessly in automated workflows. Supports individual files and recursive folder reimport.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `paths` | `list[str]` | yes | Asset paths to reimport (e.g. ['Assets/Prefabs/Unit.prefab', 'Assets/Shaders/']). |
| `force` | `bool` | — | Use ForceUpdate import option. Default: true. |
| `recursive` | `bool` | — | Recursively reimport all assets in folders. Default: true. |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

