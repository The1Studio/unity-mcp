# Unity MCP Port Configuration

## Test Project Configuration

This UnityMCPTests project is configured to use **port 6401** instead of the default 6400 to avoid conflicts when running multiple Unity projects simultaneously.

### Local Configuration
- Port: 6401
- Configuration file: `.claude/claude-code.json` (gitignored, local only)
- Unity setting: Window → MCP for Unity → Settings → Port: 6401

### Why Port 6401?
- Allows running multiple Unity projects with MCP simultaneously
- Prevents port conflicts with other Unity instances using default port 6400
- Each project can have its own dedicated port

### To Change Port
1. Update Unity: Window → MCP for Unity → Settings → Port
2. Update `.claude/claude-code.json` with matching port number
3. Restart Claude Code or use `/mcp` command to reconnect

### Status Check
You can verify the connection by:
- Checking Unity: Window → MCP for Unity (should show "Running" on port 6401)
- Running MCP tools in Claude Code to test connectivity
- Checking `~/.unity-mcp/unity-mcp-status-*.json` files for port information

**Note**: The `.claude/` directory is intentionally gitignored as it contains local development configurations that may vary between developers.