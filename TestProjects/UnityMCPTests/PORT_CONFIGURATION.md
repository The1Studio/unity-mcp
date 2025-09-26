# Unity MCP Port Configuration

## Test Project Configuration

This UnityMCPTests project is configured to use the **default port 6400**.

### Local Configuration
- Port: 6400 (default)
- Configuration file: `.claude/claude-code.json` (gitignored, local only)
- Unity setting: Window → MCP for Unity → Settings → Port: 6400

### Port Configuration
- Uses the standard default port 6400
- If you need to run multiple Unity projects simultaneously, consider changing to a different port
- Each project can have its own dedicated port if needed

### To Change Port
1. Update Unity: Window → MCP for Unity → Settings → Port
2. Update `.claude/claude-code.json` with matching port number
3. Restart Claude Code or use `/mcp` command to reconnect

### Status Check
You can verify the connection by:
- Checking Unity: Window → MCP for Unity (should show "Running" on port 6400)
- Running MCP tools in Claude Code to test connectivity
- Checking `~/.unity-mcp/unity-mcp-status-*.json` files for port information

**Note**: The `.claude/` directory is intentionally gitignored as it contains local development configurations that may vary between developers.