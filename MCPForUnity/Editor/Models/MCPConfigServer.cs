using System;
using Newtonsoft.Json;

namespace MCPForUnity.Editor.Models
{
    /// <summary>
    /// A single MCP server entry as written into a client's configuration file, mirroring the
    /// "command/args/type/url" shape used by Claude Desktop, VS Code, and similar clients.
    /// </summary>
    [Serializable]
    public class McpConfigServer
    {
        /// <summary>Executable the client launches to speak to the MCP server.</summary>
        [JsonProperty("command")]
        public string command;

        /// <summary>Arguments passed to <see cref="command"/>.</summary>
        [JsonProperty("args")]
        public string[] args;

        // VSCode expects a transport type; include only when explicitly set
        /// <summary>Transport type required by clients such as VS Code; omitted from JSON when unset.</summary>
        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        public string type;

        // URL for HTTP transport mode
        /// <summary>Endpoint URL used in HTTP transport mode; omitted from JSON when unset.</summary>
        [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
        public string url;
    }
}
