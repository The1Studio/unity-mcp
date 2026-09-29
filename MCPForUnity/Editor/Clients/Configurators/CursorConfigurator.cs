using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{

    /// <summary>Configures Cursor's global MCP server list at <c>~/.cursor/mcp.json</c>.</summary>
    public class CursorConfigurator : JsonFileMcpConfigurator
    {

        /// <summary>Creates the Cursor client definition with its config path.</summary>
        public CursorConfigurator() : base(new McpClient
        {
            name = "Cursor",
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cursor", "mcp.json"),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cursor", "mcp.json"),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cursor", "mcp.json")
        })
        { }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "Open Cursor",
            "Go to File > Preferences > Cursor Settings > MCP > Add new global MCP server\nOR open the config file at the path above",
            "Paste the configuration JSON",
            "Save and restart Cursor"
        };
    }
}
