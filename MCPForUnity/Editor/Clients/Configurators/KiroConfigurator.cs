using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{

    /// <summary>Configures Kiro through <c>~/.kiro/settings/mcp.json</c>.</summary>
    public class KiroConfigurator : JsonFileMcpConfigurator
    {

        /// <summary>Creates the Kiro client definition with its config path.</summary>
        public KiroConfigurator() : base(new McpClient
        {
            name = "Kiro",
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kiro", "settings", "mcp.json"),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kiro", "settings", "mcp.json"),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kiro", "settings", "mcp.json"),
            EnsureEnvObject = true,
            DefaultUnityFields = { { "disabled", false } }
        })
        { }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "Open Kiro",
            "Go to File > Settings > Settings > Search for \"MCP\" > Open Workspace MCP Config\nOR open the config file at the path above",
            "Paste the configuration JSON",
            "Save and restart Kiro"
        };
    }
}
