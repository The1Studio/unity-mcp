namespace MCPForUnity.Editor.Services.Transport
{
    /// <summary>
    /// Lightweight snapshot of a transport's runtime status for editor UI and diagnostics.
    /// </summary>
    public sealed class TransportState
    {
        /// <summary>True when the transport currently holds a live connection to the MCP server.</summary>
        public bool IsConnected { get; }

        /// <summary>Name of the transport backing this state (for example "stdio" or "http").</summary>
        public string TransportName { get; }

        /// <summary>Listening port when the transport is socket-based; null otherwise.</summary>
        public int? Port { get; }

        /// <summary>Identifier assigned to the current session so responses can be correlated; null when disconnected.</summary>
        public string SessionId { get; }

        /// <summary>Human-readable diagnostic detail about the connection, shown in the editor connection panel.</summary>
        public string Details { get; }

        /// <summary>Failure message when the transport could not connect; null on success.</summary>
        public string Error { get; }

        private TransportState(
            bool isConnected,
            string transportName,
            int? port,
            string sessionId,
            string details,
            string error)
        {
            IsConnected = isConnected;
            TransportName = transportName;
            Port = port;
            SessionId = sessionId;
            Details = details;
            Error = error;
        }

        /// <summary>Creates a state describing a healthy, connected transport.</summary>
        /// <param name="transportName">Name of the transport backing the connection.</param>
        /// <param name="port">Listening port when socket-based; omit for in-process transports.</param>
        /// <param name="sessionId">Session identifier used to correlate requests and responses.</param>
        /// <param name="details">Optional diagnostic detail surfaced in the editor UI.</param>
        /// <returns>A connected <see cref="TransportState"/> with no error.</returns>
        public static TransportState Connected(
            string transportName,
            int? port = null,
            string sessionId = null,
            string details = null)
            => new TransportState(true, transportName, port, sessionId, details, null);

        /// <summary>Creates a state describing a transport that is not connected.</summary>
        /// <param name="transportName">Name of the transport that failed or was stopped.</param>
        /// <param name="error">Failure message to display; omit when the transport was stopped deliberately.</param>
        /// <param name="port">Port the transport would have used, when known.</param>
        /// <returns>A disconnected <see cref="TransportState"/> carrying the error, if any.</returns>
        public static TransportState Disconnected(
            string transportName,
            string error = null,
            int? port = null)
            => new TransportState(false, transportName, port, null, null, error);

        /// <summary>Returns a copy of this state with the supplied error attached, preserving the connection flags.</summary>
        /// <param name="error">Message describing what went wrong.</param>
        /// <returns>A new state identical to this one except for <see cref="Error"/>.</returns>
        public TransportState WithError(string error) => new TransportState(
            IsConnected,
            TransportName,
            Port,
            SessionId,
            Details,
            error);
    }
}
