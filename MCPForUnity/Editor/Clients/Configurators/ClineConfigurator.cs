using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{

    /// <summary>Configures the Cline VS Code extension through its <c>cline_mcp_settings.json</c>, pre-approving tools with an empty auto-approve list.</summary>
    public class ClineConfigurator : JsonFileMcpConfigurator
    {

        /// <summary>Creates the Cline client definition with its per-OS config path and the default capped auto-approve field.</summary>
        public ClineConfigurator() : base(new McpClient
        {
            name = "Cline",
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json"),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json"),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json"),
            DefaultUnityFields = { { "disabled", false }, { "autoApprove", new object[] { } } }
        })
        { }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "Open Cline in VS Code",
            "Click the MCP Servers icon in the Cline pane",
            "Go to Configure tab and click 'Configure MCP Servers'\nOR open the config file at the path above",
            "Paste the configuration JSON into the mcpServers object",
            "Save and restart VS Code"
        };
    }
}
