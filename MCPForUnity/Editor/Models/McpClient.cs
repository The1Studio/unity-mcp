using System.Collections.Generic;

namespace MCPForUnity.Editor.Models
{
    /// <summary>
    /// Describes one MCP client application (Claude Desktop, Cursor, VS Code, and so on): where its
    /// configuration file lives per platform, its current configuration status, and the layout and
    /// transport flags that drive the client-specific configurators.
    /// </summary>
    public class McpClient
    {
        /// <summary>Display name of the client application.</summary>
        public string name;
        /// <summary>Path to the client's configuration file on Windows.</summary>
        public string windowsConfigPath;
        /// <summary>Path to the client's configuration file on macOS.</summary>
        public string macConfigPath;
        /// <summary>Path to the client's configuration file on Linux.</summary>
        public string linuxConfigPath;
        /// <summary>Human-readable status text used by the editor UI.</summary>
        public string configStatus;
        /// <summary>Structured configuration/connection status of this client.</summary>
        public McpStatus status = McpStatus.NotConfigured;
        /// <summary>Transport currently written into the client's configuration, or Unknown when unset.</summary>
        public ConfiguredTransport configuredTransport = ConfiguredTransport.Unknown;

        // Capability flags/config for JSON-based configurators
        /// <summary>True when the config file follows the VS Code layout, with the env object at the root.</summary>
        public bool IsVsCodeLayout; // Whether the config file follows VS Code layout (env object at root)
        /// <summary>True when this client can be configured to use the HTTP transport.</summary>
        public bool SupportsHttpTransport = true; // Whether the MCP server supports HTTP transport
        /// <summary>True when the configurator must ensure an env object is present in the config.</summary>
        public bool EnsureEnvObject; // Whether to ensure the env object is present in the config
        /// <summary>True when the configurator should strip the env object when the client does not require it.</summary>
        public bool StripEnvWhenNotRequired; // Whether to strip the env object when not required
        /// <summary>Name of the config property holding the HTTP URL for this client.</summary>
        public string HttpUrlProperty = "url"; // The property name for the HTTP URL in the config
        /// <summary>Default fields merged into the Unity MCP server entry for this client.</summary>
        public Dictionary<string, object> DefaultUnityFields = new();

        // Helper method to convert the enum to a display string
        /// <summary>Maps the status enum to the display label shown in the editor; the raw error text is used for Error.</summary>
        /// <returns>A display string for the current <see cref="status"/>.</returns>
        public string GetStatusDisplayString()
        {
            return status switch
            {
                McpStatus.NotConfigured => "Not Configured",
                McpStatus.Configured => "Configured",
                McpStatus.Running => "Running",
                McpStatus.Connected => "Connected",
                McpStatus.IncorrectPath => "Incorrect Path",
                McpStatus.CommunicationError => "Communication Error",
                McpStatus.NoResponse => "No Response",
                McpStatus.UnsupportedOS => "Unsupported OS",
                McpStatus.MissingConfig => "Missing MCPForUnity Config",
                McpStatus.Error => configStatus?.StartsWith("Error:") == true ? configStatus : "Error",
                McpStatus.VersionMismatch => "Version Mismatch",
                _ => "Unknown",
            };
        }

        // Helper method to set both status enum and string for backward compatibility
        /// <summary>Sets the status enum and refreshes the display string, keeping error detail for Error/VersionMismatch.</summary>
        /// <param name="newStatus">Status to record for this client.</param>
        /// <param name="errorDetails">Detail stored as the status text for Error and VersionMismatch; ignored otherwise.</param>
        public void SetStatus(McpStatus newStatus, string errorDetails = null)
        {
            status = newStatus;

            if ((newStatus == McpStatus.Error || newStatus == McpStatus.VersionMismatch) && !string.IsNullOrEmpty(errorDetails))
            {
                configStatus = errorDetails;
            }
            else
            {
                configStatus = GetStatusDisplayString();
            }
        }
    }
}
