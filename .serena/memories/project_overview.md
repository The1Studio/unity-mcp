# MCP for Unity Project Overview

## Purpose
MCP for Unity is a bridge that enables AI assistants (Claude, Cursor, etc.) to interact directly with Unity Editor via the Model Context Protocol (MCP). It allows LLMs to manage assets, control scenes, edit scripts, and automate tasks within Unity.

## Tech Stack
- **Unity Package**: C# (.NET/Unity 2021.3+ LTS)
- **MCP Server**: Python 3.10+ with MCP protocol implementation
- **Build System**: Unity Package Manager (UPM) for Unity side, uv for Python dependencies
- **Testing**: pytest for Python tests, Unity Test Framework for C# tests
- **CI/CD**: GitHub Actions for automated testing

## Architecture
1. **Unity Bridge** (`UnityMcpBridge/`): Unity package that runs inside the Editor
2. **Python Server** (`UnityMcpBridge/UnityMcpServer~/src/`): MCP server that communicates between Unity and MCP clients
3. **Communication**: TCP socket with custom framing protocol between Unity and Python server

## Key Components
- Editor tools for asset, scene, script, shader, and GameObject management
- Socket-based communication with strict framing for reliability
- Auto-configuration for popular MCP clients (Claude, Cursor, VSCode)
- Validation system with optional Roslyn support for advanced C# validation