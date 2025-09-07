# Code Style and Conventions

## C# (Unity Bridge)
- **Namespace**: `MCPForUnity.Editor` for editor code
- **Assembly Definition**: Uses `.asmdef` files for modularity
- **Naming**: PascalCase for classes/methods, camelCase for fields/parameters
- **Unity Conventions**: Follow Unity's coding standards
- **Error Handling**: Use try-catch with detailed logging via Debug.LogError
- **Serialization**: Use Newtonsoft.Json for JSON handling

## Python (MCP Server)
- **Style**: Follow PEP 8
- **Type Hints**: Use Python 3.10+ type annotations
- **Imports**: Standard library first, then third-party, then local
- **Error Handling**: Return structured error responses via MCP protocol
- **Async**: Use async/await for MCP tool handlers
- **Logging**: Log to stderr and rotating file, never to stdout

## General Principles
- No hardcoded paths - use config/environment variables
- Validate all inputs, especially file paths and Unity operations
- Handle platform differences (Windows/macOS/Linux) explicitly
- Clean up resources (sockets, file handles) properly
- Use defensive programming for Unity Editor state changes