namespace MCPForUnity.Editor.Constants
{
    /// <summary>
    /// Constants for health check status values.
    /// Used for coordinating health state between Connection and Advanced sections.
    /// </summary>
    public static class HealthStatus
    {
        /// <summary>Health has not been probed yet, so no verdict is available.</summary>
        public const string Unknown = "Unknown";
        /// <summary>Ping succeeded and the MCP server reports a healthy response.</summary>
        public const string Healthy = "Healthy";
        /// <summary>The connection is up but the health ping itself failed.</summary>
        public const string PingFailed = "Ping Failed";
        /// <summary>The server responded but reported an unhealthy state.</summary>
        public const string Unhealthy = "Unhealthy";
    }
}
