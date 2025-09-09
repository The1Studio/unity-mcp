# The One Game Studio - MCP for Unity Extensions

## Overview
This document outlines The One Game Studio's custom extensions to the MCP for Unity project. These features are designed to enhance our Unity development workflow with AI-assisted automation.

## Planned Features

### 1. 🧪 Automation Testing
**Status**: In Development  
**Priority**: High

#### Objectives
- Execute Unity Test Framework tests via MCP commands
- Generate and run test scenarios based on natural language descriptions
- Provide real-time test results and failure analysis
- Support both Edit Mode and Play Mode tests

#### Planned MCP Tools
- `manage_test` - Create, read, update, delete test scripts
- `execute_tests` - Run specific tests or test suites
- `analyze_test_results` - Parse and report test outcomes
- `generate_test` - AI-assisted test generation from requirements

#### Implementation Plan
1. Create C# test runner integration in Unity Bridge
2. Implement Python MCP tools for test management
3. Add test result parsing and reporting
4. Integrate with Unity Test Framework APIs

---

### 2. 📦 Addressables Management
**Status**: In Development  
**Priority**: High

#### Objectives
- Full CRUD operations for Addressable assets
- Manage Addressable groups and profiles
- Build and release Addressable content
- Analyze and optimize Addressable dependencies

#### Planned MCP Tools
- `manage_addressable` - CRUD operations for Addressable assets
- `manage_addressable_group` - Group creation and configuration
- `build_addressables` - Trigger Addressable builds
- `analyze_addressables` - Dependency analysis and optimization

#### Implementation Plan
1. Integrate with Unity Addressables package APIs
2. Create tools for asset marking and group management
3. Implement build pipeline integration
4. Add analysis and reporting capabilities

---

### 3. 🚀 DOTS Implementation (Wishlist)
**Status**: Future Consideration  
**Priority**: Low (Wishlist)

#### Objectives
- Support for Entity Component System (ECS) operations
- Generate and manage DOTS-compatible code
- Convert GameObject hierarchies to entities
- Performance profiling for DOTS systems

#### Potential MCP Tools
- `manage_entity` - Entity creation and component management
- `manage_system` - System creation and configuration
- `convert_to_dots` - GameObject to Entity conversion
- `profile_dots` - Performance analysis tools

#### Considerations
- Requires Unity DOTS packages installed
- Complex implementation due to DOTS architecture
- May need significant refactoring of existing tools

---

## Development Guidelines

### Adding New Studio Features
1. Create feature branch from main: `feature/studio-{feature-name}`
2. Implement both Unity (C#) and Python (MCP) components
3. Add comprehensive tests
4. Update this documentation
5. Create PR with detailed description

### Testing Requirements
- Unit tests for all new tools
- Integration tests with Unity Editor
- Stress testing for performance-critical features
- Documentation of test scenarios

### Code Standards
- Follow existing project conventions
- Maintain backward compatibility with upstream
- Document all studio-specific code clearly
- Use `STUDIO:` prefix in commit messages for studio features

---

## Integration with Existing Tools

### Automation Testing + Existing Tools
- `manage_script` - Generate test scripts
- `read_console` - Capture test output
- `execute_menu_item` - Trigger test runs via menu

### Addressables + Existing Tools
- `manage_asset` - Base for Addressable operations
- `manage_scene` - Scene-based Addressable management
- `manage_editor` - Editor settings for Addressables

---

## Versioning Strategy

### Package Version
- Main version follows upstream (e.g., 1.2.0)
- Studio features add suffix: `1.2.0-studio.1`
- Update `package.json` name: `com.theonegamestudio.unity-mcp`

### Compatibility
- Maintain compatibility with upstream for easy merging
- Document any breaking changes clearly
- Keep studio features modular and optional

---

## Support and Contact

**Internal Support**: The One Game Studio Development Team  
**Documentation**: This file and inline code comments  
**Issue Tracking**: Internal project management system  

---

## Changelog

### [Unreleased]
- Initial fork from CoplayDev/unity-mcp
- Added studio feature planning documentation
- Prepared structure for automation testing
- Prepared structure for Addressables management

---

*Last Updated: [Current Date]*  
*Maintained by: The One Game Studio*