# Codebase summary — MCP for Unity (`unity-mcp`)

Architecture overview of this repository: what the pieces are, how a request travels through
them, and where the key types live. Written from the checked-out sources; paths are relative to
the repo root.

## What this repo is

A bridge that lets an AI assistant drive the Unity Editor over the
[Model Context Protocol](https://modelcontextprotocol.io/introduction). The assistant talks MCP
to a Python server; the Python server talks to a C# editor plugin running inside Unity; the
plugin is the only part that touches the Unity Editor API.

```text
AI client (Claude Code / Cursor / VS Code / …)
        |  MCP: stdio or HTTP
        v
Python server            Server/            FastMCP + Click + Starlette
        |  WebSocket (/hub/plugin) or legacy TCP
        v
Unity C# editor package  MCPForUnity/       [McpForUnityTool] / [McpForUnityResource]
        |
        v
Unity Editor API         scene, assets, scripts, test runner
```

The two halves are released separately: the C# half is the UPM package
(`com.coplaydev.unity-mcp`, version in `MCPForUnity/package.json`), the Python half is the
`mcpforunityserver` wheel on PyPI. `manifest.json` at the root is the MCP bundle manifest that
wires the two together (`server.entry_point`, and the tool-name list a client sees).

## Repository layout

| Path | What it is |
|---|---|
| `MCPForUnity/` | The Unity package. Two assemblies: `Editor/` (all editor-side tooling) and `Runtime/` (the small runtime half that must be available in a player build). |
| `Server/` | The Python MCP server: MCP tools, MCP resources, a Click CLI, the transports, and the test suite. |
| `unity-mcp-skill/` | An agent-facing skill (`SKILL.md` + `references/`) shipped in-repo, giving a client operating guidance for the tool set. |
| `TestProjects/` | Unity projects the EditMode/PlayMode suites run against (`UnityMCPTests`), plus an Asset Store upload fixture. |
| `tools/` | Release, docs and CI helper scripts (version bump, publish, doc-reference generation, the multi-version compile check). |
| `website/` | The Docusaurus docs site published at `coplaydev.github.io/unity-mcp`. |
| `docs/` | In-repo images, the i18n README, and `docs/development/` (tool-design guidelines). |
| `plans/` | Per-feature phase plans kept with the repo. |
| `mcp_source.py` | Switches a consuming Unity project's `Packages/manifest.json` between the upstream package, this checkout, and a local `file:` source. |

## The Unity package — `MCPForUnity/`

`MCPForUnity.Editor.asmdef` compiles only for the Editor platform and declares optional package
dependencies as `versionDefines` (`UNITY_ENTITIES`, `PROBUILDER`, `UNITY_CINEMACHINE`,
`UNITY_ADDRESSABLES`, `UNITY_VFX_GRAPH`, …). A domain whose package is absent is compiled out
rather than failing the build — this is why tool code sits behind `#if` guards.
`MCPForUnity.Runtime.asmdef` has no references and is the half that must survive into a player.

### Editor assembly (`MCPForUnity/Editor/`)

| Folder | Role | Representative types |
|---|---|---|
| `Tools/` | The MCP tools themselves, one file per domain (large domains split further, e.g. `Tools/GameObjects/`, `Tools/Prefabs/`, `Tools/Vfx/`) | `CommandRegistry.cs`, `Tools/GameObjects/ManageGameObject.cs`, `ManageAsset.cs`, `ManageScene.cs`, `ManageScript.cs`, `BatchExecute.cs`, `ReadConsole.cs`, `RunTests.cs`, `UnityReflect.cs` |
| `Resources/` | Read-only state exposed as MCP resources | `Resources/Editor/` (`Selection`, `EditorState`, `ActiveTool`, `Windows`, `GetPrefabStage`, `ToolStates`), `Resources/Scene/` (`GameObjectResource`, `CamerasResource`, `VolumesResource`, `RenderingStatsResource`, `RendererFeaturesResource`), `Resources/Project/` (`ProjectInfo`, `Layers`, `Tags`), `Resources/MenuItems/`, `Resources/Tests/` |
| `Services/` | Long-lived services, resolved through `MCPServiceLocator` | `BridgeControlService.cs`, `ServerManagementService.cs`, `ClientConfigurationService.cs`, `PathResolverService.cs`, `PackageUpdateService.cs`, `TestRunnerService.cs`, `EditorStateCache.cs`, `ToolDiscoveryService.cs` |
| `Services/Transport/` | The transports that carry commands to the Python server | `TransportManager`, `Transports/StdioBridgeHost.cs`, `Transports/WebSocketTransportClient.cs`, `TransportCommandDispatcher.cs` |
| `Clients/` | Per-client MCP config writers | `McpClientConfiguratorBase.cs`, `McpClientRegistry.cs`, `Configurators/` (one file per supported client) |
| `Dependencies/` | Python/uv presence detection, per platform | `DependencyManager.cs`, `PlatformDetectors/` (`PlatformDetectorBase`, `Windows`/`MacOS`/`Linux` detectors), `Models/` |
| `Helpers/` | Shared utilities | `ToolParams.cs`, `ParamCoercion.cs`, `GameObjectLookup.cs`, `GameObjectSerializer.cs`, `Pagination.cs`, `Response.cs`, `ExecPath.cs`, `PortManager.cs`, `UnityTypeResolver.cs`, `PropertyConversion.cs`, `McpLog.cs` |
| `Windows/` | The editor UI | `MCPForUnityEditorWindow.cs`, `MCPSetupWindow.cs`, `Windows/Components/**` |
| `Setup/` | One-shot installers | `RoslynInstaller.cs` (runtime-compilation DLLs), `McpForUnitySkillInstaller.cs` + `SkillSyncService.cs` (the bundled skill) |
| `MenuItems/` | Unity menu wiring | `MCPForUnityMenu.cs` |
| `Migrations/` | Upgrade shims run at load | `LegacyServerSrcMigration.cs`, `StdIoVersionMigration.cs` |
| `Models/` | Serialization DTOs for the MCP payloads | `McpClient.cs`, `McpConfig.cs`, `MCPConfigServer.cs`, `McpStatus.cs`, `Command.cs` |
| `McpCiBoot.cs` | Headless/CI entry point | forces the stdio transport, then starts the bridge |
| `External/` | Vendored third-party (Tommy TOML, MIT) — not first-party code |

### Runtime assembly (`MCPForUnity/Runtime/`)

Small and dependency-free: `Helpers/` (including the Unity version-compatibility shims, whose
catalog is documented on `MCPForUnity/Runtime/Helpers/UnityCompatShims.cs`), `Serialization/`, and
`InputSimulationBridge.cs`. Anything the editor and a player build both need belongs here.

## The Python server — `Server/`

Three layers that are **not** generated from one another; adding a capability normally means
touching all three.

| Layer | Location | Framework | Reaches Unity via |
|---|---|---|---|
| MCP tools | `Server/src/services/tools/` | FastMCP, `@mcp_for_unity_tool` | WebSocket `send_with_unity_instance` |
| MCP resources | `Server/src/services/resources/` | FastMCP, `@mcp_for_unity_resource` | same command path, read-only |
| CLI | `Server/src/cli/commands/` | Click, `@click.command` | HTTP `run_command` |

All three end at the same C# `HandleCommand` for a domain — the Python tool
`Server/src/services/tools/manage_material.py` and the C# `Editor/Tools/ManageMaterial.cs` are
two faces of one operation.

Supporting packages: `Server/src/transport/` (`plugin_hub.py` WebSocket hub, `plugin_registry.py`,
`unity_instance_middleware.py`, `instance_selection.py`, and `transport/legacy/` for the older TCP
bridge), `Server/src/services/registry/` (the tool/resource registries and their group tags),
`Server/src/core/` (config, telemetry, logging decorators), and `Server/src/models/`.

**Tool groups.** Every tool declares a `group`; only `core` is enabled by default and the rest
(`vfx`, `animation`, `ui`, `scripting_ext`, `testing`, `probuilder`, `profiling`, `docs`) are
toggled per session by the `manage_tools` meta-tool. The group table lives in
`Server/src/services/registry/tool_registry.py`.

**Transports.** *Stdio* runs one Python process per client and uses the legacy TCP bridge — a new
connection displaces the old one. *HTTP* runs a single shared server with a WebSocket hub at
`/hub/plugin` and isolates clients by `client_id`, which is what makes multi-instance routing
possible. Operational detail: `website/docs/architecture/transports.md`.

## Request and response flow

1. The client calls an MCP tool by name. The Python tool validates parameters and resolves the
   target Unity instance from the request context.
2. The server sends the command over the active transport to the editor plugin.
3. `CommandRegistry.Initialize()` has already reflected over the assembly for `[McpForUnityTool]`
   and `[McpForUnityResource]`, so the command name resolves to a `HandlerInfo` holding either a
   sync `Func<JObject, object>` or an async `Func<JObject, Task<object>>`.
4. The handler parses arguments through `ToolParams` / `ParamCoercion`, performs the operation
   against the Unity Editor API, and returns a response envelope.
5. Anything large is paged (`page_size` + `cursor`, returning `next_cursor`) rather than returned
   whole — hierarchies, component listings and search results.
6. The editor state a client needs before acting (`EditorStateCache`, `Selection`,
   `ProjectInfo`, …) is exposed as resources, not as tool calls.

Async handlers do not block on the editor: the canonical pattern defers work onto
`EditorApplication.update` and completes a `TaskCompletionSource` — see `Editor/Tools/RefreshUnity.cs`.

## Contracts shared across the boundary

- **Response envelope** — `MCPForUnity/Editor/Helpers/Response.cs` (`SuccessResponse` / `ErrorResponse`).
  Handlers return these rather than throwing, so a failure reaches the client as data.
- **Parameter parsing** — `ToolParams` (typed getters plus `Require*` variants) on top of
  `ParamCoercion` (loose int/bool/string conversion, snake_case and camelCase aliases).
- **Attributes** — `[McpForUnityTool]` (`Editor/Tools/McpForUnityToolAttribute.cs`) and
  `[McpForUnityResource]` (`Editor/Resources/McpForUnityResourceAttribute.cs`); discovery is by
  reflection, so a new handler needs no registration entry.
- **Naming** — a domain's Python tools, CLI commands and C# tool share one verb/noun shape; the
  registry converts C# type names to snake_case command names (`StringCaseUtility.ToSnakeCase`).
- **Unity version compatibility** — a renamed or removed Unity API gets a shim under
  `MCPForUnity/Runtime/Helpers/Unity*Compat.cs`, and callers route through it, rather than
  sprinkling `#if UNITY_x_y_OR_NEWER` at each call site.

## Testing

| Surface | Location | How to run |
|---|---|---|
| Python unit + integration | `Server/tests/` and `Server/tests/integration/` | `cd Server && uv run pytest tests/ -v` |
| Unity EditMode / PlayMode | `TestProjects/UnityMCPTests/Assets/Tests/` | open the project, Unity Test Runner window |
| Multi-version compile check | `tools/check-unity-versions.sh` (+ `tools/unity-versions.json`) | `tools/check-unity-versions.sh`, `--full` to also run EditMode tests |
| Tooling unit tests | `tools/tests/` | `pytest tools/tests/` |

A change that crosses the boundary generally needs tests on both sides; a tool that touches an
API gated by a `versionDefine` additionally needs the compile check.

## Where to read next

- `CLAUDE.md` (repo root) — the working conventions, the code philosophy, and the step-by-step
  recipe for adding a new tool.
- `MCPForUnity/README.md` — the editor window, client configuration, and troubleshooting.
- `Server/README.md` — installing and running the Python server, in both transports.
- `docs/development/TOOL_DESIGN_GUIDELINES.md` — how a new tool should be shaped.
- `website/docs/architecture/` — transports, Python layers, telemetry, Unity compatibility.
