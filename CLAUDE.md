# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MCP for Unity is a bridge enabling AI assistants to interact with Unity Editor via the Model Context Protocol (MCP). It consists of:
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

## Testing Checklist

Before completing changes:
1. Run `pytest tests/` for Python changes
2. Check Unity console for errors after C# changes
3. Test with actual MCP client connection
4. Verify auto-configuration still works
5. Run stress test for protocol/connection changes
6. Ensure no hardcoded paths or credentials
7. Handle all three platforms (Windows/macOS/Linux)