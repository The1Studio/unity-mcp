---
title: Tool reference
sidebar_label: Tools
sidebar_class_name: sidebar-hidden
slug: /reference/tools
description: Auto-generated catalog of every MCP for Unity tool, grouped by domain.
---

# Tool reference

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

Every tool MCP for Unity exposes, generated directly from the Python `@mcp_for_unity_tool` registry under `Server/src/services/tools/`.

## `animation` &nbsp; (1 tool)
Animator control & AnimationClip creation
- **[`manage_animation`](./animation/manage_animation.md)** — Manage Unity animation: Animator control and AnimationClip creation.

## `core` &nbsp; (57 tools)
Essential scene, script, asset & editor tools (always on by default)
- **[`apply_text_edits`](./core/apply_text_edits.md)** — Apply small text edits to a C# script identified by URI.
- **[`batch_execute`](./core/batch_execute.md)** — Executes multiple MCP commands in a single batch for dramatically better performance.
- **[`create_script`](./core/create_script.md)** — Create a new C# script at the given project path.
- **[`debug_request_context`](./core/debug_request_context.md)** — Return the current FastMCP request context details (client_id, session_id, and meta dump).
- **[`delete_script`](./core/delete_script.md)** — Delete a C# script by URI or Assets-relative path.
- **[`execute_custom_tool`](./core/execute_custom_tool.md)** — Execute a project-scoped custom tool registered by Unity.
- **[`execute_menu_item`](./core/execute_menu_item.md)** — Execute a Unity menu item by path.
- **[`find_gameobjects`](./core/find_gameobjects.md)** — Search for GameObjects in the scene by name, tag, layer, component type, or path.
- **[`find_in_file`](./core/find_in_file.md)** — Searches a file with a regex pattern and returns line numbers and excerpts.
- **[`get_sha`](./core/get_sha.md)** — Get SHA256 and basic metadata for a Unity C# script without returning file contents.
- **[`manage_addressables`](./core/manage_addressables.md)** — Unity Addressables asset system operations.
- **[`manage_asset`](./core/manage_asset.md)** — Performs asset operations (import, create, modify, delete, etc.) in Unity.
- **[`manage_asset_hunter`](./core/manage_asset_hunter.md)** — Interact with Asset Hunter Pro to analyze project assets.
- **[`manage_audio`](./core/manage_audio.md)** — Unity audio system management.
- **[`manage_behavior`](./core/manage_behavior.md)** — Unity Behavior (AI) operations.
- **[`manage_build`](./core/manage_build.md)** — Manage Unity player builds — trigger builds, switch platforms, configure settings, manage build scenes and profiles, run batch builds across platforms.
- **[`manage_camera`](./core/manage_camera.md)** — Manage cameras (Unity Camera + Cinemachine).
- **[`manage_cinemachine`](./core/manage_cinemachine.md)** — Unity Cinemachine virtual camera management.
- **[`manage_components`](./core/manage_components.md)** — Add, remove, or set properties on components attached to GameObjects.
- **[`manage_dots`](./core/manage_dots.md)** — Debug and monitor Unity DOTS ECS at runtime.
- **[`manage_dots_graphics`](./core/manage_dots_graphics.md)** — Debug Unity DOTS Entities Graphics rendering at runtime.
- **[`manage_dots_physics`](./core/manage_dots_physics.md)** — Debug Unity DOTS Physics at runtime.
- **[`manage_dots_subscene`](./core/manage_dots_subscene.md)** — Manage Unity DOTS SubScenes at runtime.
- **[`manage_editor`](./core/manage_editor.md)** — Controls and queries the Unity editor's state and settings.
- **[`manage_gameobject`](./core/manage_gameobject.md)** — Performs CRUD operations on GameObjects.
- **[`manage_graphics`](./core/manage_graphics.md)** — Manage rendering graphics: volumes, post-processing, light baking, rendering stats, pipeline settings, and URP renderer features.
- **[`manage_input_system`](./core/manage_input_system.md)** — Unity Input System inspection.
- **[`manage_lighting`](./core/manage_lighting.md)** — Unity lighting system management.
- **[`manage_localization`](./core/manage_localization.md)** — Unity Localization operations.
- **[`manage_material`](./core/manage_material.md)** — Manages Unity materials (set properties, colors, shaders, etc).
- **[`manage_mesh`](./core/manage_mesh.md)** — Inspect and modify Unity Mesh data on GameObjects.
- **[`manage_navigation`](./core/manage_navigation.md)** — Unity AI Navigation management.
- **[`manage_netcode`](./core/manage_netcode.md)** — Unity Netcode for GameObjects operations.
- **[`manage_packages`](./core/manage_packages.md)** — Manage Unity packages: query, install, remove, embed, and configure registries.
- **[`manage_physics`](./core/manage_physics.md)** — Manage physics settings, collision matrix, materials, joints, queries, and validation.
- **[`manage_physics2d`](./core/manage_physics2d.md)** — Unity 2D physics operations.
- **[`manage_prefabs`](./core/manage_prefabs.md)** — Manages Unity Prefab assets.
- **[`manage_render_pipeline`](./core/manage_render_pipeline.md)** — Unity render pipeline management (URP/HDRP).
- **[`manage_scene`](./core/manage_scene.md)** — Performs CRUD operations on Unity scenes.
- **[`manage_script`](./core/manage_script.md)** — Compatibility router for legacy script operations.
- **[`manage_script_capabilities`](./core/manage_script_capabilities.md)** — Get manage_script capabilities (supported ops, limits, and guards).
- **[`manage_shader_tool`](./core/manage_shader_tool.md)** — Inspect and manage Unity shaders at runtime.
- **[`manage_splines`](./core/manage_splines.md)** — Unity Splines operations.
- **[`manage_terrain`](./core/manage_terrain.md)** — Inspect and modify Unity Terrain at runtime or in Edit mode.
- **[`manage_tilemap`](./core/manage_tilemap.md)** — Unity 2D Tilemap operations.
- **[`manage_timeline`](./core/manage_timeline.md)** — Unity Timeline management.
- **[`manage_tools`](./core/manage_tools.md)** — Manage which tool groups are visible in this session.
- **[`manage_ui_toolkit`](./core/manage_ui_toolkit.md)** — Unity UI Toolkit (UIElements) operations.
- **[`manage_video`](./core/manage_video.md)** — Unity VideoPlayer operations.
- **[`read_console`](./core/read_console.md)** — Gets messages from or clears the Unity Editor console.
- **[`refresh_unity`](./core/refresh_unity.md)** — Request a Unity asset database refresh and optionally a script compilation.
- **[`reimport_assets`](./core/reimport_assets.md)** — Reimport specific Unity assets by path.
- **[`rendering_stats`](./core/rendering_stats.md)** — Read Unity rendering and performance statistics.
- **[`script_apply_edits`](./core/script_apply_edits.md)** — Structured C# edits (methods/classes) with safer boundaries - prefer this over raw text.
- **[`set_active_instance`](./core/set_active_instance.md)** — Set the active Unity instance for this client/session.
- **[`validate_script`](./core/validate_script.md)** — Validate a C# script and return diagnostics.
- **[`validation_snapshot`](./core/validation_snapshot.md)** — Capture a full runtime validation snapshot in ONE call — entity counts (total/alive/dead/by-team), health distribution (min/max/mean), position samples, NaN bounds check, rendering stats (FPS/draw calls/batches), battle state, console er…

## `docs` &nbsp; (2 tools)
Unity API reflection and documentation lookup
- **[`unity_docs`](./docs/unity_docs.md)** — Fetch official Unity documentation from docs.unity3d.com.
- **[`unity_reflect`](./docs/unity_reflect.md)** — Inspect Unity's live C# API via reflection.

## `probuilder` &nbsp; (1 tool)
ProBuilder 3D modeling – requires com.unity.probuilder package
- **[`manage_probuilder`](./probuilder/manage_probuilder.md)** — Manage ProBuilder meshes for in-editor 3D modeling.

## `profiling` &nbsp; (1 tool)
Unity Profiler session control, counters, memory snapshots & Frame Debugger
- **[`manage_profiler`](./profiling/manage_profiler.md)** — Unity Profiler session control, counter reads, memory snapshots, and Frame Debugger.

## `scripting_ext` &nbsp; (2 tools)
ScriptableObject management
- **[`execute_code`](./scripting_ext/execute_code.md)** — Execute arbitrary C# code inside the Unity Editor.
- **[`manage_scriptable_object`](./scripting_ext/manage_scriptable_object.md)** — Creates and modifies ScriptableObject assets using Unity SerializedObject property paths.

## `testing` &nbsp; (3 tools)
Test runner & async test jobs
- **[`get_test_job`](./testing/get_test_job.md)** — Polls an async Unity test job by job_id.
- **[`manage_input_simulation`](./testing/manage_input_simulation.md)** — Play Mode input simulation.
- **[`run_tests`](./testing/run_tests.md)** — Starts a Unity test run asynchronously and returns a job_id immediately.

## `ui` &nbsp; (1 tool)
UI Toolkit (UXML, USS, UIDocument)
- **[`manage_ui`](./ui/manage_ui.md)** — Manages Unity UI Toolkit elements (UXML documents, USS stylesheets, UIDocument components).

## `vfx` &nbsp; (3 tools)
Visual effects – VFX Graph, shaders, procedural textures
- **[`manage_shader`](./vfx/manage_shader.md)** — Manages shader scripts in Unity (create, read, update, delete).
- **[`manage_texture`](./vfx/manage_texture.md)** — Procedural texture generation for Unity.
- **[`manage_vfx`](./vfx/manage_vfx.md)** — Manage Unity VFX components (ParticleSystem, VisualEffect, LineRenderer, TrailRenderer).

