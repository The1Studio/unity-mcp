using Newtonsoft.Json;

namespace MCPForUnity.Editor.Helpers
{
    /// <summary>
    /// Minimum contract every MCP tool result satisfies, so a caller that only needs the
    /// accept/reject verdict can read it without knowing the concrete response type.
    /// </summary>
    public interface IMcpResponse
    {
        /// <summary>True when the tool completed the requested operation.</summary>
        [JsonProperty("success")]
        bool Success { get; }
    }

    /// <summary>
    /// Standard "the operation completed" envelope, optionally carrying the produced data and an
    /// advisory warning for a success that may not have had its intended effect.
    /// </summary>
    public sealed class SuccessResponse : IMcpResponse
    {
        [JsonProperty("success")]
        public bool Success => true;

        /// <summary>Lowercase alias of <see cref="Success"/> kept for reflection-based tests.</summary>
        [JsonIgnore]
        public bool success => Success; // Backward-compatible casing for reflection-based tests

        /// <summary>Human-readable description of what the tool did.</summary>
        [JsonProperty("message")]
        public string Message { get; }

        /// <summary>Tool-specific payload; omitted from the serialized JSON when null.</summary>
        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public object Data { get; }

        /// <summary>Lowercase alias of <see cref="Data"/>.</summary>
        [JsonIgnore]
        public object data => Data;

        /// <summary>
        /// Advisory text flagging a success that may not have had its intended effect (e.g. input
        /// simulated while the Game view lacked focus). Opt-in and mutable; null when not set.
        /// </summary>
        [JsonProperty("warning", NullValueHandling = NullValueHandling.Ignore)]
        public string Warning { get; set; }

        /// <summary>
        /// Creates a success envelope.
        /// </summary>
        /// <param name="message">Human-readable description of what the tool did.</param>
        /// <param name="data">Tool-specific payload, or null when the tool produces none.</param>
        public SuccessResponse(string message, object data = null)
        {
            Message = message;
            Data = data;
        }
    }

    /// <summary>
    /// Failure envelope. Carries the machine-readable <see cref="Code"/> and the human-readable
    /// <see cref="Error"/> message; both are populated from the same ctor argument.
    /// </summary>
    public sealed class ErrorResponse : IMcpResponse
    {
        [JsonProperty("success")]
        public bool Success => false;

        /// <summary>Lowercase alias of <see cref="Success"/> kept for reflection-based tests.</summary>
        [JsonIgnore]
        public bool success => Success; // Backward-compatible casing for reflection-based tests

        /// <summary>Short machine-readable failure code, also used as the error message.</summary>
        [JsonProperty("code", NullValueHandling = NullValueHandling.Ignore)]
        public string Code { get; }

        /// <summary>Lowercase alias of <see cref="Code"/>.</summary>
        [JsonIgnore]
        public string code => Code;

        /// <summary>Human-readable failure message; identical to <see cref="Code"/>.</summary>
        [JsonProperty("error")]
        public string Error { get; }

        /// <summary>Lowercase alias of <see cref="Error"/>.</summary>
        [JsonIgnore]
        public string error => Error;

        /// <summary>Diagnostic payload (e.g. the offending request params); omitted when null.</summary>
        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public object Data { get; }

        /// <summary>Lowercase alias of <see cref="Data"/>.</summary>
        [JsonIgnore]
        public object data => Data;

        /// <summary>
        /// Creates a failure envelope, using the same text for both the code and the message.
        /// </summary>
        /// <param name="messageOrCode">Failure text, surfaced as both <c>code</c> and <c>error</c>.</param>
        /// <param name="data">Diagnostic payload, or null when none is attached.</param>
        public ErrorResponse(string messageOrCode, object data = null)
        {
            Code = messageOrCode;
            Error = messageOrCode;
            Data = data;
        }
    }

    /// <summary>
    /// Envelope for a long-running tool that has been accepted but not finished. Tells the client
    /// to poll again after <see cref="PollIntervalSeconds"/> while <see cref="Status"/> stays
    /// <c>pending</c>.
    /// </summary>
    public sealed class PendingResponse : IMcpResponse
    {
        [JsonProperty("success")]
        public bool Success => true;

        /// <summary>Lowercase alias of <see cref="Success"/> kept for reflection-based tests.</summary>
        [JsonIgnore]
        public bool success => Success; // Backward-compatible casing for reflection-based tests

        /// <summary>Job state constant, always <c>pending</c> for this envelope.</summary>
        [JsonProperty("_mcp_status")]
        public string Status => "pending";

        /// <summary>Lowercase alias of <see cref="Status"/>.</summary>
        [JsonIgnore]
        public string _mcp_status => Status;

        /// <summary>Seconds the client should wait before polling for the job's completion.</summary>
        [JsonProperty("_mcp_poll_interval")]
        public double PollIntervalSeconds { get; }

        /// <summary>Lowercase alias of <see cref="PollIntervalSeconds"/>.</summary>
        [JsonIgnore]
        public double _mcp_poll_interval => PollIntervalSeconds;

        /// <summary>Optional status text; null when the caller passed an empty message.</summary>
        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
        public string Message { get; }

        /// <summary>Lowercase alias of <see cref="Message"/>.</summary>
        [JsonIgnore]
        public string message => Message;

        /// <summary>Optional progress payload; omitted from the JSON when null.</summary>
        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public object Data { get; }

        /// <summary>Lowercase alias of <see cref="Data"/>.</summary>
        [JsonIgnore]
        public object data => Data;

        /// <summary>
        /// Creates a pending envelope.
        /// </summary>
        /// <param name="message">Optional status text; an empty string is normalized to null.</param>
        /// <param name="pollIntervalSeconds">Seconds the client should wait before polling again.</param>
        /// <param name="data">Optional progress payload, or null.</param>
        public PendingResponse(string message = "", double pollIntervalSeconds = 1.0, object data = null)
        {
            Message = string.IsNullOrEmpty(message) ? null : message;
            PollIntervalSeconds = pollIntervalSeconds;
            Data = data;
        }
    }
}
