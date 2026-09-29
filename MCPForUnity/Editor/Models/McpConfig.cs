using System;
using Newtonsoft.Json;

namespace MCPForUnity.Editor.Models
{
    [Serializable]
    /// <summary>Root object of a client config file, holding the map of configured MCP servers.</summary>
    public class McpConfig
    {
        [JsonProperty("mcpServers")]
        /// <summary>Server entries in the config file, keyed by "mcpServers".</summary>
        public McpConfigServers mcpServers;
    }
}
