using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Poll a previously started async test job by job_id.
    /// </summary>
    [McpForUnityTool("get_test_job", AutoRegister = false, Group = "testing")]
    public static class GetTestJob
    {

        /// <summary>Dispatches one <c>get_test_job</c> action and returns its response envelope.</summary>
        /// <param name="params">Action name plus the action-specific arguments; must not be null.</param>
        /// <returns>A success response with the action result, or an error response describing the failure.</returns>
        public static object HandleCommand(JObject @params)
        {
            string jobId = @params?["job_id"]?.ToString() ?? @params?["jobId"]?.ToString();
            if (string.IsNullOrWhiteSpace(jobId))
            {
                return new ErrorResponse("Missing required parameter 'job_id'.");
            }

            var p = new ToolParams(@params);
            bool includeDetails = p.GetBool("includeDetails");
            bool includeFailedTests = p.GetBool("includeFailedTests");

            var job = TestJobManager.GetJob(jobId);
            if (job == null)
            {
                return new ErrorResponse("Unknown job_id.");
            }

            var payload = TestJobManager.ToSerializable(job, includeDetails, includeFailedTests);
            return new SuccessResponse("Test job status retrieved.", payload);
        }
    }
}
