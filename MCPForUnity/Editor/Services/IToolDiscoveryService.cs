using System.Collections.Generic;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Metadata for a discovered tool
    /// </summary>
    public class ToolMetadata
    {
        /// <summary>Tool name as exposed to the MCP client (for example "manage_scene").</summary>
        public string Name { get; set; }
        /// <summary>Human-readable description advertised to the MCP client.</summary>
        public string Description { get; set; }
        /// <summary>True when the tool returns structured output rather than only text.</summary>
        public bool StructuredOutput { get; set; }
        /// <summary>Parameters the tool accepts.</summary>
        public List<ParameterMetadata> Parameters { get; set; }
        /// <summary>Name of the handler class that implements the tool.</summary>
        public string ClassName { get; set; }
        /// <summary>Namespace of the handler class.</summary>
        public string Namespace { get; set; }
        /// <summary>Assembly that declares the handler class.</summary>
        public string AssemblyName { get; set; }
        /// <summary>True when the tool is registered without being explicitly listed.</summary>
        public bool AutoRegister { get; set; } = true;
        /// <summary>True when the tool starts long-running work that must be polled for completion.</summary>
        public bool RequiresPolling { get; set; } = false;
        /// <summary>Action used to poll the tool after it starts long-running work.</summary>
        public string PollAction { get; set; } = "status";
        /// <summary>Longest a client should poll this tool before treating it as stuck; 0 means no bound.</summary>
        public int MaxPollSeconds { get; set; } = 0;
        /// <summary>True when the tool ships with the package rather than being user-provided.</summary>
        public bool IsBuiltIn { get; set; }
        /// <summary>Logical group the tool belongs to, used to organise the tool list.</summary>
        public string Group { get; set; } = "core";
    }

    /// <summary>
    /// Metadata for a tool parameter
    /// </summary>
    public class ParameterMetadata
    {
        /// <summary>Parameter name as expected in the tool's request payload.</summary>
        public string Name { get; set; }
        /// <summary>Human-readable description advertised to the MCP client.</summary>
        public string Description { get; set; }
        /// <summary>Parameter type name such as "string", "int", "bool", or "float".</summary>
        public string Type { get; set; }  // "string", "int", "bool", "float", etc.
        /// <summary>True when the client must supply this parameter.</summary>
        public bool Required { get; set; }
        /// <summary>Value used when the client omits the parameter.</summary>
        public string DefaultValue { get; set; }
    }

    /// <summary>
    /// Service for discovering MCP tools via reflection
    /// </summary>
    public interface IToolDiscoveryService
    {
        /// <summary>
        /// Discovers all tools marked with [McpForUnityTool]
        /// </summary>
        List<ToolMetadata> DiscoverAllTools();

        /// <summary>
        /// Gets metadata for a specific tool
        /// </summary>
        ToolMetadata GetToolMetadata(string toolName);

        /// <summary>
        /// Returns only the tools currently enabled for registration
        /// </summary>
        List<ToolMetadata> GetEnabledTools();

        /// <summary>
        /// Checks whether a tool is currently enabled for registration
        /// </summary>
        bool IsToolEnabled(string toolName);

        /// <summary>
        /// Updates the enabled state for a tool
        /// </summary>
        void SetToolEnabled(string toolName, bool enabled);

        /// <summary>
        /// Invalidates the tool discovery cache
        /// </summary>
        void InvalidateCache();
    }
}
