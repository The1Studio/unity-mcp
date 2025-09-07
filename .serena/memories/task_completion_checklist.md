# Task Completion Checklist

When completing a task in the MCP for Unity codebase:

## For Python Changes
1. Run pytest to ensure tests pass: `pytest tests/`
2. Check for any print statements (should use logging instead)
3. Verify error handling returns proper MCP protocol responses
4. Test with actual Unity connection if modifying protocol/tools

## For C# Unity Changes  
1. Ensure no compilation errors in Unity Editor
2. Test the modified functionality in Unity Editor
3. Check Unity console for any errors or warnings
4. Verify compatibility with Unity 2021.3 LTS minimum version

## For Both
1. Update version in both `package.json` and `pyproject.toml` if needed
2. Ensure no sensitive information in code or commits
3. Test auto-configuration with target MCP clients if modifying setup
4. Run stress test if modifying connection/protocol code
5. Update README.md if adding new features or changing setup

## Before Committing
1. No hardcoded paths or credentials
2. Platform-specific code handles Windows/macOS/Linux
3. Error messages are helpful and actionable
4. Code follows established patterns in the codebase