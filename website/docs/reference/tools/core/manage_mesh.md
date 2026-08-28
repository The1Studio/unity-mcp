---
title: manage_mesh
sidebar_label: manage_mesh
description: "Inspect and modify Unity Mesh data on GameObjects."
---

# `manage_mesh`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_mesh`

## Description

Inspect and modify Unity Mesh data on GameObjects. Actions: inspect (all-in-one: info + attributes + color samples — use this first), get_info (vertex/triangle count, bounds, index format, submesh count, isReadable), get_attributes (list VertexAttributeDescriptor for each attribute: format, dimension, stream), has_attribute (check if mesh has a specific attribute: Position/Normal/Color/TexCoord0/Tangent/etc), sample_colors (sample vertex colors evenly spaced across the mesh), sample_vertices (sample vertex positions evenly spaced across the mesh), set_colors (set all vertex colors to a solid RGBA color), force_upload (call mesh.UploadMeshData(false) to upload pending changes). Target is a GameObject name or instance ID; mesh is read from its MeshFilter.sharedMesh.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['inspect', 'get_info', 'get_attributes', 'has_attribute', 'sample_colors', 'sample_vertices', 'set_colors', 'force_upload']` | yes | Action to perform on the mesh. |
| `target` | `str` | yes | GameObject name or instance ID whose MeshFilter.sharedMesh will be used. |
| `attribute` | `str \| None` | — | Vertex attribute name for has_attribute (e.g. Position, Normal, Color, TexCoord0, Tangent). |
| `color` | `str \| None` | — | RGBA color as 'r,g,b,a' floats 0-1 for set_colors (e.g. '1,0,0,1' for red). |
| `count` | `int \| None` | — | Number of samples to return for sample_* and inspect actions (default 10). |
| `offset` | `int \| None` | — | Start offset index for sampling (default 0). |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

