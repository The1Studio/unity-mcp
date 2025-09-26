# Suggested Commands for Unity MCP Test Project

## Unity Editor Commands
```bash
# Open Unity project with specific version
/home/tuha/Unity/Hub/Editor/6000.2.5f1/Editor/Unity -projectpath /mnt/Work/1M/unity-mcp/TestProjects/UnityMCPTests

# Open via Unity Hub
unity-hub --project /mnt/Work/1M/unity-mcp/TestProjects/UnityMCPTests
```

## Testing Commands
```bash
# Run Python tests for MCP server
cd /mnt/Work/1M/unity-mcp
pytest tests/

# Run specific test
pytest tests/test_script_tools.py -v

# Stress test MCP connection
python tools/stress_mcp.py --duration 60 --clients 8 --unity-file "TestProjects/UnityMCPTests/Assets/Scripts/LongUnityScriptClaudeTest.cs"
```

## Unity MCP Server Commands
```bash
# Manual server testing (from repository root)
cd UnityMcpBridge/UnityMcpServer~/src
uv run server.py

# Check MCP status
cat ~/.unity-mcp/unity-mcp-status-*.json

# Check Unity processes
ps aux | grep -i unity | grep -v grep
```

## Git Commands
```bash
# Check git status
git status

# View recent commits
git log --oneline -10

# Current branch
git branch --show-current
```

## Development Deployment (Windows specific, but included for reference)
```bash
# Deploy dev code to test locations (creates backup first)
./deploy-dev.bat

# Restore from backup
./restore-dev.bat

# Switch package sources (upstream/branch/local)
python mcp_source.py
```

## Unity Test Runner
- Open in Unity Editor: Window → General → Test Runner
- Run all tests or specific test suites
- Check test results in Unity console

## System Commands
```bash
# List files
ls -la

# Navigate directories
cd /mnt/Work/1M/unity-mcp/TestProjects/UnityMCPTests

# Find files
find . -name "*.cs" -type f

# Search in files (use ripgrep)
rg "pattern" --type cs
```

## Package Management
```bash
# Update packages via Unity Package Manager UI
# Or edit Packages/manifest.json directly

# Force Unity to reimport all assets (clears cache)
# In Unity: Assets → Reimport All
```