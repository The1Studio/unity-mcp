using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Runtime;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools.InputSimulation
{
    internal static class InputSimulationSequence
    {
        const int MaxSteps = 100;

        internal static object StartSequence(ToolParams p)
        {
            var stepsToken = p.GetRaw("steps");
            if (stepsToken == null || stepsToken.Type != JTokenType.Array)
                return new ErrorResponse("'steps' must be a JSON array.");

            var stepsArray = (JArray)stepsToken;
            if (stepsArray.Count == 0)
                return new ErrorResponse("'steps' array cannot be empty.");
            if (stepsArray.Count > MaxSteps)
                return new ErrorResponse($"Too many steps ({stepsArray.Count}). Maximum is {MaxSteps}.");

            var steps = new List<SequenceStep>();
            for (int i = 0; i < stepsArray.Count; i++)
            {
                var item = stepsArray[i] as JObject;
                if (item == null)
                    return new ErrorResponse($"Step {i} must be a JSON object.");

                string action = item["action"]?.ToString();
                if (string.IsNullOrEmpty(action))
                    return new ErrorResponse($"Step {i} missing 'action' field.");
                if (action == "sequence")
                    return new ErrorResponse($"Step {i}: recursive 'sequence' actions are not allowed.");

                var stepParams = item["params"] as JObject ?? new JObject();
                stepParams["action"] = action;
                int delayMs = (int?)item["delay_ms"] ?? 0;

                steps.Add(new SequenceStep { Action = action, Params = stepParams, DelayMs = delayMs });
            }

            var bridge = InputSimulationBridge.Instance;
            if (bridge == null)
                return new ErrorResponse("InputSimulationBridge not available. Ensure Play Mode is active.");

            string jobId = bridge.StartSequence(steps);
            return new SuccessResponse("Sequence started.", new
            {
                job_id = jobId,
                status = "running",
                total_steps = steps.Count
            });
        }

        internal static object GetStatus(ToolParams p)
        {
            var r = p.GetRequired("job_id");
            if (!r.IsSuccess) return new ErrorResponse(r.ErrorMessage);

            var bridge = InputSimulationBridge.Instance;
            if (bridge == null)
                return new ErrorResponse("InputSimulationBridge not available.");

            var state = bridge.GetJobState(r.Value);
            if (state == null)
                return new ErrorResponse($"No sequence job found with id '{r.Value}'.");

            return new SuccessResponse($"Sequence {state.Status}.", new
            {
                job_id = state.JobId,
                status = state.Status,
                current_step = state.CurrentStep,
                total_steps = state.TotalSteps,
                error = state.Error
            });
        }
    }
}
