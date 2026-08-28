---
title: manage_shader_tool
sidebar_label: manage_shader_tool
description: "Inspect and manage Unity shaders at runtime."
---

# `manage_shader_tool`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_shader_tool`

## Description

Inspect and manage Unity shaders at runtime. Actions: reimport (force reimport a shader asset by path), get_errors (list compiler errors/warnings with file and line info), get_info (shader name, isSupported, pass count, subshader count, render queue), get_passes (list all passes with names and enabled state per subshader), find (locate shader by name, returns asset path and info), is_compiling (check if any shaders are currently compiling). Use 'path' param for asset-path-based lookup (e.g. 'Packages/com.foo/Shaders/MyShader.shader'), or 'name' for shader declaration name (e.g. 'Universal Render Pipeline/Lit').

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['reimport', 'get_errors', 'get_info', 'get_passes', 'find', 'is_compiling']` | yes | Action to perform on shaders. |
| `path` | `str \| None` | — | Asset path to the shader (e.g. 'Assets/Shaders/MyShader.shader' or 'Packages/com.foo/MyShader.shader'). Used by: reimport, get_errors, get_info, get_passes. |
| `name` | `str \| None` | — | Shader declaration name as used in Shader.Find() (e.g. 'Universal Render Pipeline/Lit'). Used by: find, get_errors, get_info, get_passes. |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

