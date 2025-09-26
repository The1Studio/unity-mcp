# Task Completion Checklist for Unity MCP Test Project

## After Making Code Changes

### 1. Validation & Testing
- [ ] Run Unity compilation check (no errors in Unity console)
- [ ] Run Python tests: `pytest tests/`
- [ ] Run Unity Test Runner for affected test suites
- [ ] Check for Unity console warnings/errors
- [ ] Verify MCP bridge connection still works

### 2. Code Quality Checks
- [ ] Ensure code follows C# conventions
- [ ] Verify proper error handling is in place
- [ ] Check that all Unity operations are wrapped in try-catch
- [ ] Validate JSON response structures

### 3. Cross-Platform Verification
- [ ] Test on current platform (Linux)
- [ ] Ensure paths use cross-platform methods
- [ ] Verify no hardcoded paths or credentials

### 4. Integration Testing
- [ ] Test with actual MCP client connection
- [ ] Verify auto-configuration still works
- [ ] Test all affected MCP tools
- [ ] Run stress test if protocol/connection changed

### 5. Documentation Updates
- [ ] Update CLAUDE.md if adding new features
- [ ] Update tool descriptions if modified
- [ ] Document any new MCP tools added
- [ ] Update version numbers if releasing

## Before Committing

1. **Check Git Status**
   ```bash
   git status
   git diff
   ```

2. **Ensure Tests Pass**
   - All Python tests passing
   - Unity compilation successful
   - No new Unity console errors

3. **Version Synchronization** (if needed)
   - Update `UnityMcpBridge/package.json`
   - Update `UnityMcpBridge/UnityMcpServer~/src/pyproject.toml`

## Unity-Specific Checks

- [ ] Unity Editor shows MCP bridge as "Running"
- [ ] Status file created in `~/.unity-mcp/`
- [ ] TCP port (default 6400) is accessible
- [ ] MCP tools respond correctly

## Common Issues to Check

1. **Unity Bridge Not Connecting**
   - Restart Unity Editor
   - Check TCP port availability
   - Verify status in Window > MCP for Unity

2. **MCP Server Issues**
   - Ensure `uv` is installed
   - Check server path in config
   - Run server manually to see errors

3. **Test Failures**
   - Clear Unity cache: Assets → Reimport All
   - Check assembly definition references
   - Verify test runner settings

## Final Verification
- [ ] All changes work as expected
- [ ] No regression in existing functionality
- [ ] Performance is acceptable
- [ ] Error messages are helpful
- [ ] Code is ready for review