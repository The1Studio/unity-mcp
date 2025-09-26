# Code Style and Conventions for Unity MCP Test Project

## C# Code Style
- **Namespace**: Use appropriate namespaces (e.g., `MCPForUnity.Editor`, `MCPForUnity.Editor.Tools`)
- **Class Naming**: PascalCase for classes (e.g., `ManageScript`, `CustomComponent`)
- **Method Naming**: PascalCase for public methods, camelCase for private
- **Field Naming**: Private fields with underscore prefix (e.g., `_fieldName`)
- **Properties**: PascalCase for properties

## Unity Conventions
- **MonoBehaviour Scripts**: Inherit from MonoBehaviour for Unity components
- **Editor Scripts**: Place in Editor folders, use UnityEditor namespace
- **Assembly Definitions**: Use .asmdef files to organize code into assemblies
- **Meta Files**: Unity generates .meta files for all assets (tracked in git)

## File Organization
- **Editor Code**: Place in `Assets/Editor/` or assembly-specific Editor folders
- **Runtime Code**: Place in `Assets/Scripts/`
- **Tests**: Place in `Assets/Tests/EditMode/` for editor tests
- **Assembly Structure**: Group related code with assembly definition files

## Testing Conventions
- **Test Assembly**: Include `UNITY_INCLUDE_TESTS` in define constraints
- **Test Framework**: Use NUnit for unit testing
- **Test Naming**: Descriptive test names explaining what is being tested
- **Platform Restriction**: Editor tests restricted to Editor platform only

## Error Handling
- Wrap Unity operations in try-catch blocks
- Return structured JSON responses with success/error fields
- Log detailed errors to Unity console
- Provide helpful error messages for debugging

## Communication Protocol
- Use snake_case for MCP tool names (e.g., `manage_script`, `read_console`)
- JSON-RPC style messages with request/response pattern
- Base64 encode large text contents for safe transmission
- Include validation levels for script operations

## Documentation
- XML documentation comments for public APIs
- Clear descriptions in MCP tool decorators
- Include usage examples in tool descriptions
- Document parameters and return values

## Version Management
- Synchronize versions between:
  - `UnityMcpBridge/package.json` (Unity package)
  - `UnityMcpBridge/UnityMcpServer~/src/pyproject.toml` (Python server)

## Platform Compatibility
- Handle Windows/macOS/Linux path differences
- Use `Path.Combine()` for path operations
- Use Unity's cross-platform APIs when available
- Account for different Unity installation locations