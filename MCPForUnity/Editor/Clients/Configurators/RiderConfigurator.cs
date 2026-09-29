using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{

    /// <summary>Configures the GitHub Copilot plugin in Rider through the plugin's <c>intellij/mcp.json</c>.</summary>
    public class RiderConfigurator : JsonFileMcpConfigurator
    {

        /// <summary>Creates the Rider GitHub Copilot client definition with its per-OS config path.</summary>
        public RiderConfigurator() : base(new McpClient
        {
            name = "Rider GitHub Copilot",
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "github-copilot", "intellij", "mcp.json"),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "github-copilot", "intellij", "mcp.json"),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "github-copilot", "intellij", "mcp.json"),
            IsVsCodeLayout = true
        })
        { }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "Install GitHub Copilot plugin in Rider",
            "Open or create mcp.json at the path above",
            "Paste the configuration JSON",
            "Save and restart Rider"
        };
    }
}

