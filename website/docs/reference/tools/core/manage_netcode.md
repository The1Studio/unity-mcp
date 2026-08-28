---
title: manage_netcode
sidebar_label: manage_netcode
description: "Unity Netcode for GameObjects operations."
---

# `manage_netcode`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_netcode`

## Description

Unity Netcode for GameObjects operations. Actions: get_network_manager (transport, connection state, clients), list_network_objects (all NetworkObject components), get_network_object (ownership, network ID, spawn state), start_host (start as host), start_server (start as server), start_client (start as client), shutdown (stop networking). Requires com.unity.netcode.gameobjects package.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['get_network_manager', 'list_network_objects', 'get_network_object', 'start_host', 'start_server', 'start_client', 'shutdown']` | yes | Action to perform on Unity Netcode. |
| `target` | `str \| None` | — | GameObject name or instance ID with NetworkObject |
| `page_size` | `int \| None` | — | Max results to return (default 50) |
| `cursor` | `int \| None` | — | Pagination cursor (0-based offset) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

