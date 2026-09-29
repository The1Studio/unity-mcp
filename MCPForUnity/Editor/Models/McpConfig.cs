using System;
using Newtonsoft.Json;

namespace MCPForUnity.Editor.Models
{
    /// <summary>Root object of a client config file, holding the map of configured MCP servers.</summary>
    [Serializable]
    public class McpConfig
    {
        /// <summary>Server entries in the config file, keyed by "mcpServers".</summary>
        [JsonProperty("mcpServers")]
        public McpConfigServers mcpServers;
    }
}
