using System.Collections.Generic;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Metadata for a discovered resource
    /// </summary>
    public class ResourceMetadata
    {
        /// <summary>Resource name as exposed to the MCP client (for example "unity://scene").</summary>
        public string Name { get; set; }

        /// <summary>Human-readable description advertised to the MCP client.</summary>
        public string Description { get; set; }

        /// <summary>Name of the handler class that implements the resource.</summary>
        public string ClassName { get; set; }

        /// <summary>Namespace of the handler class.</summary>
        public string Namespace { get; set; }

        /// <summary>Assembly that declares the handler class.</summary>
        public string AssemblyName { get; set; }

        /// <summary>True when the resource ships with the package rather than being user-provided.</summary>
        public bool IsBuiltIn { get; set; }
    }

    /// <summary>
    /// Service for discovering MCP resources via reflection
    /// </summary>
    public interface IResourceDiscoveryService
    {
        /// <summary>
        /// Discovers all resources marked with [McpForUnityResource]
        /// </summary>
        List<ResourceMetadata> DiscoverAllResources();

        /// <summary>
        /// Gets metadata for a specific resource
        /// </summary>
        ResourceMetadata GetResourceMetadata(string resourceName);

        /// <summary>
        /// Returns only the resources currently enabled
        /// </summary>
        List<ResourceMetadata> GetEnabledResources();

        /// <summary>
        /// Checks whether a resource is currently enabled
        /// </summary>
        bool IsResourceEnabled(string resourceName);

        /// <summary>
        /// Updates the enabled state for a resource
        /// </summary>
        void SetResourceEnabled(string resourceName, bool enabled);

        /// <summary>
        /// Invalidates the resource discovery cache
        /// </summary>
        void InvalidateCache();
    }
}
