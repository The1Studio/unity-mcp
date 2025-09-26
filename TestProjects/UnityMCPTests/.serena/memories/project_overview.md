# Unity MCP Test Project Overview

## Project Purpose
This is a test project for the Unity MCP (Model Context Protocol) bridge, which enables AI assistants to interact with Unity Editor via MCP. The project is used for testing and developing the Unity MCP functionality.

## Project Details
- **Name**: UnityMCPTests
- **Unity Version**: 6000.2.5f1 (Unity 6)
- **Location**: /mnt/Work/1M/unity-mcp/TestProjects/UnityMCPTests
- **Repository**: Part of https://github.com/CoplayDev/unity-mcp

## Tech Stack
- Unity 6000.2.5f1
- C# for Unity scripts
- Unity Test Framework for testing
- Unity MCP Bridge package (com.theonegamestudio.unity-mcp)
- Python MCP Server for communication

## Key Dependencies
- Unity MCP Bridge: Referenced locally from `../../../UnityMcpBridge`
- Unity Test Framework 1.5.1
- Unity AI Navigation 2.0.9
- Unity IDE integrations (Rider, Visual Studio, Windsurf)
- Unity modules for various features (UI, Audio, Physics, etc.)

## Project Structure
```
UnityMCPTests/
├── Assets/
│   ├── Editor/         # Editor scripts
│   ├── Scripts/        # Runtime scripts and test files
│   │   ├── Hello.cs
│   │   ├── LongUnityScriptClaudeTest.cs
│   │   └── TestAsmdef/ # Test assembly with CustomComponent
│   ├── Scenes/         # Unity scenes
│   └── Tests/          # Test framework files
│       └── EditMode/   # Editor mode tests
│           ├── Tools/  # Tool-specific tests
│           └── Windows/ # Window-specific tests
├── Packages/           # Unity package dependencies
├── ProjectSettings/    # Unity project settings
└── Library/           # Unity generated files (gitignored)
```

## Assembly Definitions
- **MCPForUnityTests.EditMode**: Test assembly for editor mode tests
  - References: MCPForUnity.Editor, TestAsmdef, UnityEngine.TestRunner
  - Platform: Editor only
  - Includes NUnit framework
- **TestAsmdef**: Custom test assembly in Scripts folder

## Communication Protocol
- TCP socket connection between Unity (server) and Python MCP (client)
- Default port: 6400
- Status files in `~/.unity-mcp/` directory
- JSON-RPC style messaging with custom framing protocol