---
title: manage_dots
sidebar_label: manage_dots
description: "Debug and monitor Unity DOTS ECS at runtime."
---

# `manage_dots`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_dots`

## Description

Debug and monitor Unity DOTS ECS at runtime. Actions: list_worlds (show all ECS Worlds), query_entities (find entities by component types), get_entity (inspect entity components — use component_types to filter specific components), list_systems (list systems with enabled status), get_system (system details, queries, ordering), performance_snapshot (chunk utilization, archetype stats, entity counts), toggle_system (enable/disable a system for debugging), list_component_types (discover all registered ECS types with optional filter), create_entity (create debug entity with components), destroy_entity (destroy entity by index/version), set_component (modify a component field value at runtime), add_component (add a component to an existing entity), remove_component (remove a component from an entity), query_count (fast entity count without fetching data), inspect_bdp_tree (show BDP behavior tree state — active branch, running task, task statuses). Requires com.unity.entities package. Most actions work in Edit mode; performance_snapshot and inspect_bdp_tree are most useful during Play mode. NOTE: a component name can match more than one registered TypeManager index. query_entities and query_count run every match and sum the (disjoint) results, reporting the candidates in ambiguous_type_names; get_entity on a known index is always authoritative and is the way to confirm a specific entity's components.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['list_worlds', 'query_entities', 'get_entity', 'list_systems', 'get_system', 'performance_snapshot', 'toggle_system', 'list_component_types', 'create_entity', 'destroy_entity', 'set_component', 'add_component', 'remove_component', 'query_count', 'inspect_bdp_tree']` | yes | Action to perform on DOTS ECS data. |
| `component_types` | `str \| None` | — | Comma-separated component type names for query_entities/create_entity/get_entity filter (e.g. 'Health,NavigationTarget') |
| `entity_index` | `int \| None` | — | Entity index for get_entity/destroy_entity |
| `entity_version` | `int \| None` | — | Entity version for get_entity/destroy_entity (default 1) |
| `system_name` | `str \| None` | — | System name (short or full) for get_system/toggle_system |
| `enabled` | `bool \| str \| None` | — | Enable/disable for toggle_system (true/false) |
| `component_name` | `str \| None` | — | Component type name for set_component/add_component/remove_component |
| `field_name` | `str \| None` | — | Field name to modify (for set_component) |
| `field_value` | `str \| None` | — | New field value as string (for set_component) |
| `world` | `str \| None` | — | Target world name (defaults to DefaultGameObjectInjectionWorld) |
| `group` | `str \| None` | — | Filter systems by group name (for list_systems) |
| `filter` | `str \| None` | — | Name filter for list_component_types |
| `category` | `str \| None` | — | Category filter for list_component_types (e.g. 'BufferData', 'ComponentData') |
| `page_size` | `int \| None` | — | Max entities/types to return (default 20, max 200) |
| `limit` | `int \| None` | — | Max archetypes in performance_snapshot (default 20) |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

