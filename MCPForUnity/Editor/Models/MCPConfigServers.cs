using System;
using Newtonsoft.Json;

namespace MCPForUnity.Editor.Models
{
    /// <summary>
    /// Root object of a client's MCP configuration file, wrapping the Unity MCP server entry
    /// under the "unityMCP" key.
    /// </summary>
    [Serializable]
    public class McpConfigServers
    {
        /// <summary>The Unity MCP server entry as written to the client configuration file.</summary>
        [JsonProperty("unityMCP")]
        public McpConfigServer unityMCP;
    }
}
