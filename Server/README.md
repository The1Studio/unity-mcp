# MCP for Unity Server

[![MCP](https://badge.mcpx.dev?status=on 'MCP Enabled')](https://modelcontextprotocol.io/introduction)
[![python](https://img.shields.io/badge/Python-3.10+-3776AB.svg?style=flat&logo=python&logoColor=white)](https://www.python.org)
[![License](https://img.shields.io/badge/License-MIT-red.svg 'MIT License')](https://opensource.org/licenses/MIT)
[![Discord](https://img.shields.io/badge/discord-join-red.svg?logo=discord&logoColor=white)](https://discord.gg/y4p8KfzrN4)

Model Context Protocol server for Unity Editor integration. Control Unity through natural language using AI assistants like Claude, Cursor, and more.

**Maintained by [Coplay](https://www.coplay.dev/?ref=unity-mcp)** - This project is not affiliated with Unity Technologies.

💬 **Join our community:** [Discord Server](https://discord.gg/y4p8KfzrN4)

**Required:** Install the [Unity MCP Plugin](https://github.com/CoplayDev/unity-mcp?tab=readme-ov-file#-step-1-install-the-unity-package) to connect Unity Editor with this MCP server. You also need `uvx` (requires [uv](https://docs.astral.sh/uv/)) to run the server.

---

## Installation

### Option 1: PyPI

Install and run directly from PyPI using `uvx`.

**Run Server (HTTP):**

```bash
uvx --from mcpforunityserver mcp-for-unity --transport http --http-url http://localhost:8080
```

**MCP Client Configuration (HTTP):**

```json
{
  "mcpServers": {
    "UnityMCP": {
      "url": "http://localhost:8080/mcp"
    }
  }
}
```

**MCP Client Configuration (stdio):**

```json
{
  "mcpServers": {
    "UnityMCP": {
      "command": "uvx",
      "args": [
        "--from",
        "mcpforunityserver",
        "mcp-for-unity",
        "--transport",
        "stdio"
      ]
    }
  }
}
```

### Option 2: From GitHub Source

Use this to run the latest released version from the repository. Change the version to `main` to run the latest unreleased changes from the repository.

```json
{
  "mcpServers": {
    "UnityMCP": {
      "command": "uvx",
      "args": [
        "--from",
        "git+https://github.com/CoplayDev/unity-mcp@v9.7.1#subdirectory=Server",
        "mcp-for-unity",
        "--transport",
        "stdio"
      ]
    }
  }
}
```

### Option 3: Docker

**Use Pre-built Image:**

```bash
docker run -p 8080:8080 msanatan/mcp-for-unity-server:latest --transport http --http-url http://0.0.0.0:8080
```

**Build Locally:**

```bash
docker build -t unity-mcp-server .
docker run -p 8080:8080 unity-mcp-server --transport http --http-url http://0.0.0.0:8080
```

Configure your MCP client with `"url": "http://localhost:8080/mcp"`.

### Option 4: Local Development

For contributing or modifying the server code:

```bash
# Clone the repository
git clone https://github.com/CoplayDev/unity-mcp.git
cd unity-mcp/Server

# Run with uv
uv run src/main.py --transport stdio
```

---

## Configuration

The server connects to Unity Editor automatically when both are running. Most users do not need to change any settings.

### CLI options

These options apply to the `mcp-for-unity` command (whether run via `uvx`, Docker, or `python src/main.py`).

- `--transport {stdio,http}` - Transport protocol (default: `stdio`)
- `--http-url URL` - Base URL used to derive host/port defaults (default: `http://localhost:8080`)
- `--http-host HOST` - Override HTTP bind host (overrides URL host)
- `--http-port PORT` - Override HTTP bind port (overrides URL port)
- `--http-remote-hosted` - Treat HTTP transport as remotely hosted
  - Requires API key authentication (see below)
  - Disables local/CLI-only HTTP routes (`/api/command`, `/api/instances`, `/api/custom-tools`)
  - Forces explicit Unity instance selection for MCP tool/resource calls
  - Isolates Unity sessions per user
- `--api-key-validation-url URL` - External endpoint to validate API keys (required when `--http-remote-hosted` is set)
- `--api-key-login-url URL` - URL where users can obtain/manage API keys (served by `/api/auth/login-url`)
- `--api-key-cache-ttl SECONDS` - Cache duration for validated keys (default: `300`)
- `--api-key-service-token-header HEADER` - Header name for server-to-auth-service authentication (e.g. `X-Service-Token`)
- `--api-key-service-token TOKEN` - Token value sent to the auth service for server authentication
- `--default-instance INSTANCE` - Default Unity instance to target (project name, hash, or `Name@hash`)
- `--project-scoped-tools` - Keep custom tools scoped to the active Unity project and enable the custom tools resource
- `--unity-instance-token TOKEN` - Optional per-launch token set by Unity for deterministic lifecycle management
- `--pidfile PATH` - Optional path where the server writes its PID on startup (used by Unity-managed terminal launches)

### Environment variables

- `UNITY_MCP_TRANSPORT` - Transport protocol: `stdio` or `http`
- `UNITY_MCP_HTTP_URL` - HTTP server URL (default: `http://localhost:8080`)
- `UNITY_MCP_HTTP_HOST` - HTTP bind host (overrides URL host)
- `UNITY_MCP_HTTP_PORT` - HTTP bind port (overrides URL port)
- `UNITY_MCP_HTTP_REMOTE_HOSTED` - Enable remote-hosted mode (`true`, `1`, or `yes`)
- `UNITY_MCP_DEFAULT_INSTANCE` - Default Unity instance to target (project name, hash, or `Name@hash`)
- `UNITY_MCP_SKIP_STARTUP_CONNECT=1` - Skip initial Unity connection attempt on startup
- `UNITY_MCP_LOG_DIR` - Override the rotating server log directory. Default: `%LOCALAPPDATA%\UnityMCP\Logs` (Windows), `~/Library/Application Support/UnityMCP/Logs` (macOS), `$XDG_STATE_HOME/UnityMCP/Logs` (Linux/BSD, defaults to `~/.local/state/UnityMCP/Logs`).

API key authentication (remote-hosted mode):

- `UNITY_MCP_API_KEY_VALIDATION_URL` - External endpoint to validate API keys
- `UNITY_MCP_API_KEY_LOGIN_URL` - URL where users can obtain/manage API keys
- `UNITY_MCP_API_KEY_CACHE_TTL` - Cache TTL for validated keys in seconds (default: `300`)
- `UNITY_MCP_API_KEY_SERVICE_TOKEN_HEADER` - Header name for server-to-auth-service authentication
- `UNITY_MCP_API_KEY_SERVICE_TOKEN` - Token value sent to the auth service for server authentication

Telemetry:

- `DISABLE_TELEMETRY=1` - Disable anonymous telemetry (opt-out)
- `UNITY_MCP_DISABLE_TELEMETRY=1` - Same as `DISABLE_TELEMETRY`
- `MCP_DISABLE_TELEMETRY=1` - Same as `DISABLE_TELEMETRY`
- `UNITY_MCP_TELEMETRY_ENDPOINT` - Override telemetry endpoint URL
- `UNITY_MCP_TELEMETRY_TIMEOUT` - Override telemetry request timeout (seconds)

### Examples

**Stdio (default):**

```bash
uvx --from mcpforunityserver mcp-for-unity --transport stdio
```

**HTTP (local):**

```bash
uvx --from mcpforunityserver mcp-for-unity --transport http --http-host 127.0.0.1 --http-port 8080
```

**HTTP (remote-hosted with API key auth):**

```bash
uvx --from mcpforunityserver mcp-for-unity \
  --transport http \
  --http-host 0.0.0.0 \
  --http-port 8080 \
  --http-remote-hosted \
  --api-key-validation-url https://auth.example.com/api/validate-key \
  --api-key-login-url https://app.example.com/api-keys
```

**Disable telemetry:**

```bash
DISABLE_TELEMETRY=1 uvx --from mcpforunityserver mcp-for-unity --transport stdio
```

---

## Run one shared daemon for many editors

By default every MCP client (each Claude Code session) launches its own stdio `mcp-for-unity` process. If you run many sessions and/or many Unity Editors, run **one** HTTP daemon instead: every Editor connects to it, every client talks to it, and calls are routed per Editor.

Why HTTP and not stdio for this: the Editor's stdio bridge (a TCP listener in the Editor) accepts a single client and evicts the previous one, so stdio cannot be shared. The HTTP transport has each Editor open a WebSocket to the daemon (`/hub/plugin`), which is what makes many-to-many routing possible.

### 1. Start the daemon (once per machine)

```bash
uvx --from mcpforunityserver mcp-for-unity --transport http --http-host 127.0.0.1 --http-port 8080
```

Bind to `127.0.0.1` only (this is a local, unauthenticated daemon; use `--http-remote-hosted` with API keys if it must be reachable by other machines). Keep it running under your service manager of choice (systemd user unit, launchd, a Windows scheduled task). Health check: `curl http://127.0.0.1:8080/health`.

### 2. Register it once in each MCP client

Claude Code (`.mcp.json`, or `~/.claude.json` for user scope):

```json
{
  "mcpServers": {
    "unityMCP": {
      "type": "http",
      "url": "http://127.0.0.1:8080/mcp"
    }
  }
}
```

or `claude mcp add --transport http unityMCP http://127.0.0.1:8080/mcp`. Note the `/mcp` path.

### 3. Point each Unity Editor at the daemon

Each Editor must be in **HTTP** transport, **local** scope, with the daemon's base URL, and must have its bridge started (a live WebSocket to the daemon). These are stored in the Editor's `EditorPrefs`:

| EditorPrefs key | Value | Default if unset |
|---|---|---|
| `MCPForUnity.UseHttpTransport` | `true` | `true` |
| `MCPForUnity.HttpTransportScope` | `local` (not `remote`) | empty, treated as `local` |
| `MCPForUnity.HttpUrl` | `http://127.0.0.1:<port>` (base URL, no `/mcp`) | `http://127.0.0.1:8080` |
| `MCPForUnity.AutoStartOnLoad` | `true` to connect on Editor load | `true` |

So an Editor with untouched defaults already targets `http://127.0.0.1:8080`: run the daemon on port 8080 and the Editor connects to it on load with no manual step ("Auto-Start Server on Editor Load" in Advanced Settings now defaults to on; if the daemon is already reachable the Editor just connects to it and does not launch its own server; a value you saved earlier, including off, is kept). On another port, set the HTTP URL in the window to match.

Where the values live (Unity's `EditorPrefs` store; per [Unity's EditorPrefs documentation](https://docs.unity3d.com/ScriptReference/EditorPrefs.html)): macOS `~/Library/Preferences/com.unity3d.UnityEditor5.x.plist`, Windows registry key `HKCU\Software\Unity Technologies\Unity Editor 5.x`, Linux `~/.local/share/unity3d/prefs`. These locations come from Unity's docs and were not checked on each OS here. **Window > MCP For Unity > Edit EditorPrefs** edits the values from inside the Editor.

**Pre-seeding for automation: not possible from outside the Editor today.** No environment variable or config file sets the transport mode or HTTP URL. (The Editor package reads environment variables only for unrelated things: `UNITY_MCP_STATUS_DIR`, `UNITY_MCP_ALLOW_BATCH`, the telemetry opt-outs, `CLAUDE_CLI`, and platform path lookups.) Your options are the defaults above (daemon on port 8080), setting the keys by hand once per machine, or an Editor-side `-executeMethod` that calls `EditorPrefs.SetString("MCPForUnity.HttpUrl", ...)` at launch. A small Editor change (honour an env var such as `UNITY_MCP_HTTP_URL` in `HttpEndpointUtility.GetLocalBaseUrl`) would remove the manual step but has not been made because it cannot be exercised without an interactive Editor.

### 4. How a call picks its Editor

Rules, in order:

1. **`unity_instance` on the call** (every Editor-routed tool advertises this optional argument): `Name@hash`, or a unique hash prefix. Port numbers are a stdio-bridge concept and are rejected on the HTTP transport with an explanatory error. Applies to that call only: it does not become the session's default, and the next call without a selection is resolved by rules 2-4 as if it had never been given.
2. **`set_active_instance`** for this MCP client session: sticky until changed. State is per client session (`mcp-session-id`), so two sessions on one daemon can each hold a different Editor without affecting each other. **Exception:** if a client sends a `client_id` in its request metadata, that outranks `mcp-session-id` as the key (existing order), so two sessions that send the *same* `client_id` share one selection. Clients that cannot guarantee a distinct `client_id` should pass `unity_instance` explicitly on every call.
3. **Exactly one Editor connected**: used automatically, unless its project is unrelated to the daemon's working directory (then the call is refused rather than answered by an Editor you did not mean; set `UNITY_MCP_ALLOW_CROSS_PROJECT_AUTOSELECT=1` to opt out).
4. **More than one Editor connected**: the daemon picks the single Editor whose project directory contains the daemon process's working directory; if none or several do, **the call fails** with an error that lists the connected `Name@hash` values and tells you to pass `unity_instance` or call `set_active_instance`. It never falls back to "the last one used".

Caveat for a shared daemon: rule 4 compares against the *daemon's* working directory, not each client's, so it only helps when the daemon is started from inside one project. In practice give each session an explicit target (rule 1 or 2); put `set_active_instance` or `unity_instance` in the project's agent instructions.

`mcpforunity://instances` lists the connected Editors and is updated as Editors connect and disconnect; an Editor that drops without closing its socket is reaped by the server's stale-session sweep.

---

## Remote-Hosted Mode

When deploying the server as a shared remote service (e.g. for a team or Asset Store users), enable `--http-remote-hosted` to activate API key authentication and per-user session isolation.

**Requirements:**

- An external HTTP endpoint that validates API keys. The server POSTs `{"api_key": "..."}` and expects `{"valid": true, "user_id": "..."}` or `{"valid": false}` in response.
- `--api-key-validation-url` must be provided (or `UNITY_MCP_API_KEY_VALIDATION_URL`). The server exits with code 1 if this is missing.

**What changes in remote-hosted mode:**

- All MCP tool/resource calls and Unity plugin WebSocket connections require a valid `X-API-Key` header.
- Each user only sees Unity instances that connected with their API key (session isolation).
- Auto-selection of a sole Unity instance is disabled; users must explicitly call `set_active_instance`.
- CLI REST routes (`/api/command`, `/api/instances`, `/api/custom-tools`) are disabled.
- `/health` and `/api/auth/login-url` remain accessible without authentication.

**MCP client config with API key:**

```json
{
  "mcpServers": {
    "UnityMCP": {
      "url": "http://remote-server:8080/mcp",
      "headers": {
        "X-API-Key": "<your-api-key>"
      }
    }
  }
}
```

For full details, see [Remote Server Auth Guide](../docs/guides/REMOTE_SERVER_AUTH.md) and [Architecture Reference](../docs/reference/REMOTE_SERVER_AUTH_ARCHITECTURE.md).

---

## MCP Resources

The server provides read-only MCP resources for querying Unity Editor state. Resources provide up-to-date information about your Unity project without modifying it.

**Accessing Resources:**

Resources are accessed by their URI (not their name). Always use `ListMcpResources` to get the correct URI format.

**Example URIs:**
- `mcpforunity://editor/state` - Editor readiness snapshot
- `mcpforunity://project/tags` - All project tags
- `mcpforunity://scene/gameobject/{instance_id}` - GameObject details by ID
- `mcpforunity://prefab/{encoded_path}` - Prefab info by asset path

**Important:** Resource names use underscores (e.g., `editor_state`) but URIs use slashes/hyphens (e.g., `mcpforunity://editor/state`). Always use the URI from `ListMcpResources()` when reading resources.

**All resource descriptions now include their URI** for easy reference. List available resources to see the complete catalog with URIs.

---

## Example Prompts

Once connected, try these commands in your AI assistant:

- "Create a 3D player controller with WASD movement"
- "Add a rotating cube to the scene with a red material"
- "Create a simple platformer level with obstacles"
- "Generate a shader that creates a holographic effect"
- "List all GameObjects in the current scene"

---

## Documentation

For complete documentation, troubleshooting, and advanced usage:

📖 **[Full Documentation](https://github.com/CoplayDev/unity-mcp#readme)**

---

## Requirements

- **Python:** 3.10 or newer
- **Unity Editor:** 2021.3 LTS or newer
- **uv:** Python package manager ([Installation Guide](https://docs.astral.sh/uv/getting-started/installation/))

---

## License

MIT License - See [LICENSE](https://github.com/CoplayDev/unity-mcp/blob/main/LICENSE)
