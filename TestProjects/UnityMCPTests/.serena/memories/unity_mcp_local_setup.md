# Unity MCP Local vs Global Setup Guide

## Overview

Unity MCP can be configured at two levels:
1. **Global (User-level)**: `~/.config/claude/claude-code.json`
2. **Local (Project-level)**: `{project}/.claude/claude-code.json`

## Local Setup (Recommended for Teams)

### Advantages
- **Version Control**: Configuration travels with the project
- **Team Consistency**: All developers use identical setup
- **Project Isolation**: No conflicts between projects
- **Portable**: Works on any machine with the project
- **Per-Project Customization**: Different ports, settings per project

### Setup Steps

1. **Create Local Configuration Directory**:
```bash
mkdir -p /path/to/unity-project/.claude
```

2. **Create Local MCP Config**:
```json
// .claude/claude-code.json
{
  "mcpServers": {
    "unity-mcp-local": {
      "command": "uv",
      "args": ["run", "server.py"],
      "cwd": "../../UnityMcpBridge/UnityMcpServer~/src",
      "env": {
        "UNITY_PROJECT_PATH": "${workspaceFolder}",
        "UNITY_MCP_PORT": "6400"
      }
    }
  }
}
```

3. **Add to Version Control**:
```bash
git add .claude/
git commit -m "Add local Unity MCP configuration"
```

## Global Setup (For Personal Projects)

### Advantages
- **Single Configuration**: One setup for all projects
- **User Preferences**: Personal settings stay with user
- **Quick Setup**: No per-project configuration needed

### Setup Steps

1. **Edit Global Config**:
```bash
vim ~/.config/claude/claude-code.json
```

2. **Add Unity Projects**:
```json
{
  "mcpServers": {
    "unity-project1": {
      "command": "uv",
      "args": ["run", "server.py"],
      "cwd": "/absolute/path/to/UnityMcpServer~/src"
    },
    "unity-project2": {
      "command": "uv",
      "args": ["run", "server.py"],
      "cwd": "/another/path/to/UnityMcpServer~/src"
    }
  }
}
```

## Hybrid Approach

Use both for maximum flexibility:

1. **Global**: Common tools (GitHub, Serena, etc.)
2. **Local**: Project-specific Unity MCP

### Priority Order
Claude Code reads configurations in this order:
1. Project `.claude/claude-code.json` (highest priority)
2. User `~/.config/claude/claude-code.json`
3. System defaults (lowest priority)

## Multiple Unity Projects Setup

### For Local Configurations

Each project has its own `.claude/` directory:

```
Project1/.claude/claude-code.json → Port 6400
Project2/.claude/claude-code.json → Port 6401
Project3/.claude/claude-code.json → Port 6402
```

### For Global Configuration

Use unique server names:

```json
{
  "mcpServers": {
    "unity-screw3d": { /* config */ },
    "unity-cattlepuller": { /* config */ },
    "unity-tests": { /* config */ }
  }
}
```

## Port Management

### Automatic Port Discovery
Unity MCP uses `~/.unity-mcp/unity-mcp-status-{projectId}.json` files to discover ports automatically.

### Manual Port Assignment
1. Set in Unity: Window → MCP for Unity → Settings → Port
2. Update configuration to match
3. Use unique ports per project (6400-6409 recommended)

## Best Practices

1. **Use Local for Team Projects**: Ensures consistency
2. **Use Global for Personal Tools**: Serena, GitHub, etc.
3. **Document Port Allocation**: Keep a registry of used ports
4. **Include Setup Instructions**: Add README for team members
5. **Test Configuration**: Verify MCP connects before committing

## Troubleshooting

### Local Config Not Loading
- Ensure `.claude/` is in project root
- Check file permissions
- Restart Claude Code

### Port Conflicts
- Use `lsof -i :6400` to check port usage
- Assign unique ports per project
- Update both Unity and config

### Server Not Starting
- Check `uv` is installed: `uv --version`
- Verify Python path in config
- Run server manually to see errors