# Development Commands for MCP for Unity

## Testing
```bash
# Run Python tests
cd tests
pytest

# Run specific test file
pytest tests/test_script_tools.py

# Run Unity tests (requires Unity installation)
# Use Unity Test Framework in Unity Editor via Window > General > Test Runner
```

## Development Scripts
```bash
# Deploy development code to test installation
./deploy-dev.bat  # Windows only - deploys to Unity package cache and MCP server location

# Restore from backup
./restore-dev.bat  # Windows only - restores original files

# Switch package sources
python mcp_source.py  # Switch between upstream, remote branch, or local workspace

# Run stress test
python tools/stress_mcp.py --duration 60 --clients 8
```

## Python Environment
```bash
# Install dependencies (using uv)
cd UnityMcpBridge/UnityMcpServer~/src
uv sync

# Run the MCP server manually
uv run server.py
```

## Unity Package Management
```bash
# Install via Git URL in Unity Package Manager
# URL: https://github.com/The1Studio/unity-mcp.git?path=/UnityMcpBridge
# Original: https://github.com/CoplayDev/unity-mcp.git?path=/UnityMcpBridge

# Or via OpenUPM (Studio fork)
openupm add com.theonegamestudio.unity-mcp
```

## Version Management
- Version is stored in `UnityMcpBridge/package.json` and `UnityMcpBridge/UnityMcpServer~/src/pyproject.toml`
- Keep both versions in sync when updating