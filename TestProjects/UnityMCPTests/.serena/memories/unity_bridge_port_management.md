# Unity Bridge Port Management

## Key Concepts

Unity MCP uses a **two-layer port system**:

1. **Unity Bridge Port (TCP Server)**
   - Unity Editor listens on this port (default: 6400)
   - Each Unity project needs a unique port when running simultaneously
   - Configured in Unity Editor or via EditorPrefs

2. **Python MCP Server (TCP Client)**
   - Connects TO the Unity Bridge port
   - Discovers port automatically via status files
   - No separate port needed (it's a client, not server)

## Port Discovery Mechanism

### Unity Side (Bridge)
1. Checks EditorPrefs for saved port
2. Falls back to environment variable `UNITY_MCP_PORT`
3. Defaults to 6400
4. Auto-increments if port busy (6401, 6402...)
5. Writes status to `~/.unity-mcp/unity-mcp-status-{projectId}.json`

### Python Side (MCP Server)
1. Reads all status files in `~/.unity-mcp/`
2. Matches project path from environment
3. Connects to discovered Unity port
4. Falls back to port 6400 if no match

## Setting Unity Bridge Port

### Option 1: In Unity Editor (Persistent)
```
Window → MCP for Unity → Settings → Port: [6400-6409]
```

### Option 2: Via EditorPrefs (Programmatic)
```csharp
EditorPrefs.SetInt("MCPForUnity.Port", 6401);
```

### Option 3: Environment Variable (Launch-time)
```bash
UNITY_MCP_PORT=6401 Unity -projectpath /path/to/project
```

## Port Allocation Table

| Project | Unity Port | Status File ID |
|---------|------------|---------------|
| UnityMCPTests | 6400 | Default/Primary |
| Screw3D | 6401 | Per-project |
| CattlePuller | 6402 | Per-project |
| ScrewMatch | 6403 | Per-project |

## Configuring Multiple Projects

### Step 1: Assign Unique Ports
Each Unity project gets a dedicated port (6400-6409)

### Step 2: Configure Unity
In each Unity Editor, set the assigned port

### Step 3: Update Local Config
```json
// .claude/claude-code.json
{
  "mcpServers": {
    "unity-mcp-local": {
      "env": {
        "UNITY_MCP_PORT": "6401"  // Match Unity's port
      }
    }
  }
}
```

### Step 4: Verify Connection
Check status files to confirm correct ports:
```bash
cat ~/.unity-mcp/unity-mcp-status-*.json | jq '{path: .project_path, port: .unity_port}'
```

## Common Issues

### All Projects Using Same Port (6400)
- **Cause**: No per-project configuration
- **Fix**: Set unique ports in each Unity Editor

### MCP Can't Connect
- **Cause**: Port mismatch or Unity Bridge not running
- **Fix**: Check Unity Window → MCP for Unity status

### Port Already in Use
- **Cause**: Previous Unity instance didn't release port
- **Fix**: Kill orphaned Unity processes or use different port

## Best Practices

1. **Reserve Port Ranges**: 6400-6409 for Unity MCP only
2. **Document Allocations**: Keep port registry updated
3. **Use Status Files**: Let auto-discovery handle connections
4. **Clean Stale Files**: Remove old status files periodically
5. **Test Before Committing**: Verify port works for the project