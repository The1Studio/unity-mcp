using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{

    /// <summary>Configures Codex through <c>~/.codex/config.toml</c>; the settings file is TOML rather than JSON, and the configurator can also install the Unity MCP skill.</summary>
    public class CodexConfigurator : CodexMcpConfigurator
    {

        /// <summary>Creates the Codex client definition with its <c>config.toml</c> path.</summary>
        public CodexConfigurator() : base(new McpClient
        {
            name = "Codex",
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "config.toml"),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "config.toml"),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "config.toml")
        })
        { }

        public override bool SupportsSkills => true;

        public override string GetSkillInstallPath()
        {
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userHome, ".codex", "skills", "unity-mcp-skill");
        }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "Run 'codex config edit' in a terminal\nOR open the config file at the path above",
            "Paste the configuration TOML",
            "Save and restart Codex"
        };
    }
}
