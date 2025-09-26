# Unity MCP Setup Guide

## Current Setup Status
- **Unity Project**: UnityMCPTests is open in Unity Editor
- **Unity Version**: 6000.2.5f1
- **MCP Package**: Installed via local reference to `../../../UnityMcpBridge`
- **Project Path**: `/mnt/Work/1M/unity-mcp/TestProjects/UnityMCPTests`

## How to Enable Unity MCP Bridge

### 1. In Unity Editor
1. Open Window → MCP for Unity
2. Check the status - should show "Running" when active
3. If not running, click "Start MCP Bridge"
4. Note the TCP port (default: 6400)

### 2. Auto-Setup MCP Client
1. In Unity: Window → MCP for Unity → Auto-Setup
2. Select your MCP client (Claude Code, Cursor, etc.)
3. Follow the setup wizard
4. Verify configuration was added to client

### 3. Manual Setup (if auto-setup fails)
Configure your MCP client's JSON config:
```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "uv",
      "args": ["run", "--directory", "/path/to/UnityMcpServer~/src", "server.py"],
      "env": {}
    }
  }
}
```

## Available MCP Tools
1. **read_console** - Read/clear Unity console messages
2. **manage_script** - Full C# script CRUD operations
3. **manage_editor** - Control Unity Editor state
4. **manage_scene** - Scene operations
5. **manage_asset** - Asset operations
6. **manage_shader** - Shader CRUD operations
7. **manage_gameobject** - GameObject manipulation
8. **execute_menu_item** - Execute Unity menu items
9. **apply_text_edits** - Advanced text editing
10. **script_apply_edits** - Structured C# edits
11. **validate_script** - Script validation

## Verification Steps
1. Check Unity MCP status: `cat ~/.unity-mcp/unity-mcp-status-*.json`
2. Verify Unity process: `ps aux | grep Unity | grep UnityMCPTests`
3. Test MCP connection in your client
4. Try a simple command like `read_console`

## Troubleshooting

### Bridge Not Starting
- Restart Unity Editor
- Check if port 6400 is in use: `lsof -i :6400`
- Look for errors in Unity console
- Verify package is properly installed

### MCP Client Issues
- Ensure `uv` is installed: `uv --version`
- Check Python 3.10+ is available
- Verify server path in client config
- Run server manually: `cd UnityMcpBridge/UnityMcpServer~/src && uv run server.py`

### Connection Problems
- Check firewall settings
- Verify TCP connection to Unity
- Review status files in `~/.unity-mcp/`
- Check Unity console for MCP bridge logs

## Testing the Setup
1. In your MCP client, try: "Read the Unity console"
2. Create a test script: "Create a C# script called TestMCP"
3. Query project info: "What scenes are in this project?"
4. If all work, Unity MCP is properly configured!