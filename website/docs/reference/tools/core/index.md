---
title: "core tools"
sidebar_label: "core"
description: "MCP for Unity tools in the core group."
---

# `core` tools

Essential scene, script, asset & editor tools (always on by default)

- **[`apply_text_edits`](./apply_text_edits.md)** — Apply small text edits to a C# script identified by URI.
- **[`batch_execute`](./batch_execute.md)** — Executes multiple MCP commands in a single batch for dramatically better performance.
- **[`create_script`](./create_script.md)** — Create a new C# script at the given project path.
- **[`debug_request_context`](./debug_request_context.md)** — Return the current FastMCP request context details (client_id, session_id, and meta dump).
- **[`delete_script`](./delete_script.md)** — Delete a C# script by URI or Assets-relative path.
- **[`execute_custom_tool`](./execute_custom_tool.md)** — Execute a project-scoped custom tool registered by Unity.
- **[`execute_menu_item`](./execute_menu_item.md)** — Execute a Unity menu item by path.
- **[`find_gameobjects`](./find_gameobjects.md)** — Search for GameObjects in the scene by name, tag, layer, component type, or path.
- **[`find_in_file`](./find_in_file.md)** — Searches a file with a regex pattern and returns line numbers and excerpts.
- **[`get_sha`](./get_sha.md)** — Get SHA256 and basic metadata for a Unity C# script without returning file contents.
- **[`manage_addressables`](./manage_addressables.md)** — Unity Addressables asset system operations.
- **[`manage_asset`](./manage_asset.md)** — Performs asset operations (import, create, modify, delete, etc.) in Unity.
- **[`manage_asset_hunter`](./manage_asset_hunter.md)** — Interact with Asset Hunter Pro to analyze project assets.
- **[`manage_audio`](./manage_audio.md)** — Unity audio system management.
- **[`manage_behavior`](./manage_behavior.md)** — Unity Behavior (AI) operations.
- **[`manage_build`](./manage_build.md)** — Manage Unity player builds — trigger builds, switch platforms, configure settings, manage build scenes and profiles, run batch builds across platforms.
- **[`manage_camera`](./manage_camera.md)** — Manage cameras (Unity Camera + Cinemachine).
- **[`manage_cinemachine`](./manage_cinemachine.md)** — Unity Cinemachine virtual camera management.
- **[`manage_components`](./manage_components.md)** — Add, remove, or set properties on components attached to GameObjects.
- **[`manage_dots`](./manage_dots.md)** — Debug and monitor Unity DOTS ECS at runtime.
- **[`manage_dots_graphics`](./manage_dots_graphics.md)** — Debug Unity DOTS Entities Graphics rendering at runtime.
- **[`manage_dots_physics`](./manage_dots_physics.md)** — Debug Unity DOTS Physics at runtime.
- **[`manage_dots_subscene`](./manage_dots_subscene.md)** — Manage Unity DOTS SubScenes at runtime.
- **[`manage_editor`](./manage_editor.md)** — Controls and queries the Unity editor's state and settings.
- **[`manage_gameobject`](./manage_gameobject.md)** — Performs CRUD operations on GameObjects.
- **[`manage_graphics`](./manage_graphics.md)** — Manage rendering graphics: volumes, post-processing, light baking, rendering stats, pipeline settings, and URP renderer features.
- **[`manage_input_system`](./manage_input_system.md)** — Unity Input System inspection.
- **[`manage_lighting`](./manage_lighting.md)** — Unity lighting system management.
- **[`manage_localization`](./manage_localization.md)** — Unity Localization operations.
- **[`manage_material`](./manage_material.md)** — Manages Unity materials (set properties, colors, shaders, etc).
- **[`manage_mesh`](./manage_mesh.md)** — Inspect and modify Unity Mesh data on GameObjects.
- **[`manage_navigation`](./manage_navigation.md)** — Unity AI Navigation management.
- **[`manage_netcode`](./manage_netcode.md)** — Unity Netcode for GameObjects operations.
- **[`manage_packages`](./manage_packages.md)** — Manage Unity packages: query, install, remove, embed, and configure registries.
- **[`manage_physics`](./manage_physics.md)** — Manage physics settings, collision matrix, materials, joints, queries, and validation.
- **[`manage_physics2d`](./manage_physics2d.md)** — Unity 2D physics operations.
- **[`manage_prefabs`](./manage_prefabs.md)** — Manages Unity Prefab assets.
- **[`manage_render_pipeline`](./manage_render_pipeline.md)** — Unity render pipeline management (URP/HDRP).
- **[`manage_scene`](./manage_scene.md)** — Performs CRUD operations on Unity scenes.
- **[`manage_script`](./manage_script.md)** — Compatibility router for legacy script operations.
- **[`manage_script_capabilities`](./manage_script_capabilities.md)** — Get manage_script capabilities (supported ops, limits, and guards).
- **[`manage_shader_tool`](./manage_shader_tool.md)** — Inspect and manage Unity shaders at runtime.
- **[`manage_splines`](./manage_splines.md)** — Unity Splines operations.
- **[`manage_terrain`](./manage_terrain.md)** — Inspect and modify Unity Terrain at runtime or in Edit mode.
- **[`manage_tilemap`](./manage_tilemap.md)** — Unity 2D Tilemap operations.
- **[`manage_timeline`](./manage_timeline.md)** — Unity Timeline management.
- **[`manage_tools`](./manage_tools.md)** — Manage which tool groups are visible in this session.
- **[`manage_ui_toolkit`](./manage_ui_toolkit.md)** — Unity UI Toolkit (UIElements) operations.
- **[`manage_video`](./manage_video.md)** — Unity VideoPlayer operations.
- **[`read_console`](./read_console.md)** — Gets messages from or clears the Unity Editor console.
- **[`refresh_unity`](./refresh_unity.md)** — Request a Unity asset database refresh and optionally a script compilation.
- **[`reimport_assets`](./reimport_assets.md)** — Reimport specific Unity assets by path.
- **[`rendering_stats`](./rendering_stats.md)** — Read Unity rendering and performance statistics.
- **[`script_apply_edits`](./script_apply_edits.md)** — Structured C# edits (methods/classes) with safer boundaries - prefer this over raw text.
- **[`set_active_instance`](./set_active_instance.md)** — Set the active Unity instance for this client/session.
- **[`validate_script`](./validate_script.md)** — Validate a C# script and return diagnostics.
- **[`validation_snapshot`](./validation_snapshot.md)** — Capture a full runtime validation snapshot in ONE call — entity counts (total/alive/dead/by-team), health distribution (min/max/mean), position samples, NaN bounds check, rendering stats (FPS/draw calls/batches), battle state, console er…
