using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{

    /// <summary>Configures Windsurf through <c>~/.codeium/windsurf/mcp_config.json</c>.</summary>
    public class WindsurfConfigurator : JsonFileMcpConfigurator
    {

        /// <summary>Creates the Windsurf client definition with its config path.</summary>
        public WindsurfConfigurator() : base(new McpClient
        {
            name = "Windsurf",
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codeium", "windsurf", "mcp_config.json"),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codeium", "windsurf", "mcp_config.json"),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codeium", "windsurf", "mcp_config.json"),
            HttpUrlProperty = "serverUrl",
            DefaultUnityFields = { { "disabled", false } },
            StripEnvWhenNotRequired = true
        })
        { }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "Open Windsurf",
            "Go to File > Preferences > Windsurf Settings > MCP > Manage MCPs > View raw config\nOR open the config file at the path above",
            "Paste the configuration JSON",
            "Save and restart Windsurf"
        };
    }
}
