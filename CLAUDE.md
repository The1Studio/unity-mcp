# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MCP for Unity is a bridge enabling AI assistants to interact with Unity Editor via the Model Context Protocol (MCP). 

**Repository**: https://github.com/CoplayDev/unity-mcp (maintained by Coplay)
**Discord**: https://discord.gg/y4p8KfzrN4

The project consists of:
- **Unity Package** (`UnityMcpBridge/`): C# code running inside Unity Editor
- **Python MCP Server** (`UnityMcpBridge/UnityMcpServer~/src/`): Communicates between Unity and MCP clients
- **Test Suite** (`tests/`): Python tests using pytest framework

## Key Development Commands

### Testing
```bash
# Run Python tests
pytest tests/

# Run specific test
pytest tests/test_script_tools.py -v

# Unity tests - use Unity Test Runner in Editor
# Window > General > Test Runner
```

### Development Deployment (Windows)
```bash
# Deploy dev code to test locations (creates backup first)
./deploy-dev.bat

# Restore from backup
./restore-dev.bat

# Switch package sources (upstream/branch/local)
python mcp_source.py
```

### Manual Server Testing
```bash
cd UnityMcpBridge/UnityMcpServer~/src
uv run server.py
```

### Stress Testing
```bash
python tools/stress_mcp.py --duration 60 --clients 8 --unity-file "TestProjects/UnityMCPTests/Assets/Scripts/LongUnityScriptClaudeTest.cs"
```

## Architecture & Key Components

### Unity Bridge (C#)
- **Entry Point**: `UnityMcpBridge/Editor/MCPForUnityBridge.cs` - Main TCP server and connection handler
- **Tools**: `UnityMcpBridge/Editor/Tools/` - Individual MCP tool implementations:
  - `ManageScript.cs` - Script CRUD and editing with validation
  - `ManageAsset.cs` - Asset operations (import, create, modify, delete)
  - `ManageScene.cs` - Scene management and hierarchy operations
  - `ManageGameObject.cs` - GameObject and component manipulation
  - `ManageShader.cs` - Shader operations
  - `ExecuteMenuItem.cs` - Execute Unity menu items
  - `ReadConsole.cs` - Read Unity console messages

### Python Server
- **Entry**: `server.py` - MCP server implementation
- **Unity Connection**: `unity_connection.py` - TCP client to Unity with framing protocol
- **Tools**: `tools/` directory - Python implementations of MCP tools that forward to Unity

### Communication Protocol
- TCP socket between Unity (server) and Python (client)
- Custom framing protocol with 8-byte header for reliability
- Handshake: `{"command": "handshake", "strict_framing": true}`
- JSON-RPC style messages with request/response pattern
- Port discovery via status files in `~/.unity-mcp/` directory (default port: 6400)

## Code Validation Levels

The system supports three validation levels for C# scripts:
1. **Basic**: Structural validation (braces, syntax)
2. **Standard**: More comprehensive checks
3. **Strict**: Full Roslyn compiler validation (requires Roslyn DLLs)

## Important Patterns

### Text Editing
- Uses `apply_text_edits` with LSP-style ranges
- Requires `precondition_sha256` for large files
- Supports atomic multi-edit batches
- Options: `refresh` ("immediate"/"debounced"), `allow_noop`

### Error Handling
- Unity operations wrapped in try-catch
- Errors returned as structured JSON responses
- Detailed logging to Unity console and Python logs

### Platform Support
- Handle Windows/macOS/Linux path differences
- Use `EditorUtility.StandardPath` for cross-platform paths
- Account for different MCP client config locations

## Version Management

When updating versions, synchronize:
- `UnityMcpBridge/package.json` - Unity package version
- `UnityMcpBridge/UnityMcpServer~/src/pyproject.toml` - Python server version

## Available MCP Tools

The following tools are available for Unity control:

1. **`read_console`** - Read/clear Unity console messages
2. **`manage_script`** - Full C# script CRUD and editing operations
3. **`manage_editor`** - Control Unity Editor state and settings
4. **`manage_scene`** - Scene operations (load, save, create, hierarchy)
5. **`manage_asset`** - Asset operations (import, create, modify, delete)
6. **`manage_shader`** - Shader CRUD operations
7. **`manage_gameobject`** - GameObject and component manipulation
8. **`execute_menu_item`** - Execute Unity menu items (e.g., "File/Save Project")
9. **`apply_text_edits`** - Advanced text editing with LSP-style ranges
10. **`script_apply_edits`** - Structured C# method/class edits
11. **`validate_script`** - Script validation (basic/standard/strict)

## Installation & Setup

### Prerequisites
- Python 3.10+ with `uv` package manager
- Unity 2021.3 LTS or newer
- MCP Client (Claude Desktop, Claude Code, Cursor, VSCode Copilot, Windsurf)

### Unity Package Installation
```bash
# Via Git URL in Unity Package Manager
https://github.com/CoplayDev/unity-mcp.git?path=/UnityMcpBridge

# Or via OpenUPM
openupm add com.coplaydev.unity-mcp
```

### MCP Client Configuration
- Use Auto-Setup in Unity: `Window > MCP for Unity > Auto-Setup`
- Or manually configure client's JSON config with server path

## CI/CD Workflows

### GitHub Actions
- **Unity Tests** (`unity-tests.yml`): Runs on push to main for Unity test projects
- **Claude NL Suite** (`claude-nl-suite-mini.yml`): Natural language editing tests
- **Version Bump** (`bump-version.yml`): Automated version management

### Test Locations
- Python tests: `tests/` directory
- Unity tests: `TestProjects/UnityMCPTests/`
- Test script for large edits: `TestProjects/UnityMCPTests/Assets/Scripts/LongUnityScriptClaudeTest.cs`

## Troubleshooting

### Common Issues

1. **Unity Bridge Not Connecting**
   - Check status: `Window > MCP for Unity`
   - Restart Unity Editor
   - Verify TCP port (default 6400) is not in use

2. **MCP Client Not Starting Server**
   - Verify `uv` is installed: `uv --version`
   - Check server path in client config matches installation
   - Run server manually to see errors: `uv run server.py`

3. **Windows UV Path Issues**
   - Use WinGet Links shim: `%LOCALAPPDATA%\Microsoft\WinGet\Links\uv.exe`
   - Set via: `Window > MCP for Unity > Choose uv Install Location`
   - See `CursorHelp.md` for detailed Windows troubleshooting

4. **Auto-Configure Failed**
   - Use manual configuration with correct paths
   - Check client config file permissions

## Adding New MCP Tools

To add a new MCP tool to this project, you need to implement it in both the Python server and Unity Bridge:

### 1. Python Server Side (`UnityMcpBridge/UnityMcpServer~/src/tools/`)

Create a new Python file for your tool:

```python
# tools/your_new_tool.py
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import send_command_with_retry
from typing import Dict, Any

def register_your_new_tool(mcp: FastMCP):
    @mcp.tool()
    def your_new_tool(
        ctx: Context,
        action: str,
        param1: str = None,
        param2: int = None
    ) -> Dict[str, Any]:
        """Description of what your tool does.
        
        Args:
            ctx: The MCP context
            action: The action to perform
            param1: Description of param1
            param2: Description of param2
            
        Returns:
            Dictionary with results
        """
        # Send command to Unity
        result = send_command_with_retry(
            "HandleYourNewTool",  # Must match Unity handler name
            {"action": action, "param1": param1, "param2": param2}
        )
        return result
```

Register it in `tools/__init__.py`:
```python
from .your_new_tool import register_your_new_tool

def register_all_tools(mcp):
    # ... existing registrations ...
    register_your_new_tool(mcp)
```

### 2. Unity Bridge Side (`UnityMcpBridge/Editor/Tools/`)

Create a C# handler class:

```csharp
// YourNewTool.cs
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace MCPForUnity.Editor.Tools
{
    public static class YourNewTool
    {
        public static object HandleCommand(JObject args)
        {
            string action = args["action"]?.Value<string>();
            
            switch (action)
            {
                case "your_action":
                    return HandleYourAction(args);
                default:
                    return new { error = $"Unknown action: {action}" };
            }
        }
        
        private static object HandleYourAction(JObject args)
        {
            // Your implementation here
            string param1 = args["param1"]?.Value<string>();
            // Process and return result
            return new { success = true, data = "result" };
        }
    }
}
```

Register it in `CommandRegistry.cs`:
```csharp
private static readonly Dictionary<string, Func<JObject, object>> _handlers = new()
{
    // ... existing handlers ...
    { "HandleYourNewTool", YourNewTool.HandleCommand },
};
```

### 3. Testing Your New Tool

1. **Python tests** - Add tests in `tests/test_your_tool.py`
2. **Unity testing** - Test in Unity Editor via Window > MCP for Unity
3. **Integration testing** - Test with actual MCP client connection

### Tool Design Guidelines

- **Naming**: Use descriptive names matching the pattern `manage_*`, `read_*`, or action verbs
- **Error Handling**: Always wrap Unity operations in try-catch blocks
- **Validation**: Validate all inputs, especially file paths
- **Return Format**: Return consistent JSON structures with `success`/`error` fields
- **Documentation**: Include clear descriptions in the `@mcp.tool` decorator

## Testing Checklist

Before completing changes:
1. Run `pytest tests/` for Python changes
2. Check Unity console for errors after C# changes
3. Test with actual MCP client connection
4. Verify auto-configuration still works
5. Run stress test for protocol/connection changes
6. Ensure no hardcoded paths or credentials
7. Handle all three platforms (Windows/macOS/Linux)