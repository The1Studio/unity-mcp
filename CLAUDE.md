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
import base64  # If handling large text content

def register_your_new_tool(mcp: FastMCP):
    """Register your new tool with the MCP server."""
    
    @mcp.tool()
    def your_new_tool(
        ctx: Context,
        action: str,
        name: str = None,
        path: str = None,
        contents: str = None
    ) -> Dict[str, Any]:
        """Brief description of what your tool does.
        
        Args:
            ctx: The MCP context
            action: Operation to perform (e.g., 'create', 'read', 'update', 'delete')
            name: Name of the resource
            path: Path to the resource (relative to Assets/)
            contents: Content for create/update operations
            
        Returns:
            Dictionary with 'success', 'message', and optional 'data'
        """
        try:
            # Prepare parameters for Unity
            params = {
                "action": action,
                "name": name,
                "path": path
            }
            
            # Optional: Base64 encode contents for safe transmission
            if contents is not None and action in ['create', 'update']:
                params["encodedContents"] = base64.b64encode(contents.encode('utf-8')).decode('utf-8')
                params["contentsEncoded"] = True
            
            # Remove None values
            params = {k: v for k, v in params.items() if v is not None}
            
            # Send to Unity - use the tool name, NOT a "Handle" prefix!
            response = send_command_with_retry("your_new_tool", params)
            
            # Process response
            if isinstance(response, dict) and response.get("success"):
                return {
                    "success": True, 
                    "message": response.get("message", "Operation successful"),
                    "data": response.get("data")
                }
            return response if isinstance(response, dict) else {"success": False, "message": str(response)}
            
        except Exception as e:
            return {"success": False, "message": f"Python error: {str(e)}"}
```

Register it in `tools/__init__.py`:
```python
from .your_new_tool import register_your_new_tool

def register_all_tools(mcp):
    # ... existing registrations ...
    register_your_new_tool(mcp)
    logger.info("MCP for Unity Server tool registration complete.")
```

### 2. Unity Bridge Side (`UnityMcpBridge/Editor/Tools/`)

Create a C# handler class:

```csharp
// YourNewTool.cs
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using MCPForUnity.Editor.Helpers;  // For Response class

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Handles operations for your new tool.
    /// </summary>
    public static class YourNewTool
    {
        /// <summary>
        /// Main handler for tool commands.
        /// </summary>
        public static object HandleCommand(JObject @params)
        {
            // Extract parameters
            string action = @params["action"]?.ToString()?.ToLower();
            string name = @params["name"]?.ToString();
            string path = @params["path"]?.ToString();
            string contents = null;
            
            // Handle base64 encoded contents if needed
            bool contentsEncoded = @params["contentsEncoded"]?.ToObject<bool>() ?? false;
            if (contentsEncoded && @params["encodedContents"] != null)
            {
                try
                {
                    byte[] data = Convert.FromBase64String(@params["encodedContents"].ToString());
                    contents = System.Text.Encoding.UTF8.GetString(data);
                }
                catch (Exception e)
                {
                    return Response.Error($"Failed to decode contents: {e.Message}");
                }
            }
            else
            {
                contents = @params["contents"]?.ToString();
            }
            
            // Validate required parameters
            if (string.IsNullOrEmpty(action))
                return Response.Error("Action parameter is required.");
            
            // Route to specific action handlers
            switch (action)
            {
                case "create":
                    return CreateResource(name, path, contents);
                case "read":
                    return ReadResource(name, path);
                case "update":
                    return UpdateResource(name, path, contents);
                case "delete":
                    return DeleteResource(name, path);
                default:
                    return Response.Error($"Unknown action: '{action}'");
            }
        }
        
        private static object CreateResource(string name, string path, string contents)
        {
            try
            {
                // Your implementation here
                // Use AssetDatabase APIs for Unity operations
                return Response.Success(
                    $"Resource '{name}' created successfully",
                    new { path = path }
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to create: {e.Message}");
            }
        }
        
        // ... implement other methods ...
    }
}
```

### 3. Register in Unity Bridge (`UnityMcpBridge/Editor/MCPForUnityBridge.cs`)

Add your tool to the command routing switch statement (around line 881):

```csharp
object result = command.type switch
{
    // ... existing cases ...
    "your_new_tool" => YourNewTool.HandleCommand(paramsObject),
    _ => throw new ArgumentException($"Unknown command type: {command.type}")
};
```

**Note**: The `CommandRegistry.cs` is currently NOT used by the actual message routing. The bridge uses a direct switch statement matching the tool name from Python.

### 4. Testing Your New Tool

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