using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MCPForUnity.Runtime
{
    /// <summary>
    /// Step definition for input simulation sequences.
    /// </summary>
    public class SequenceStep
    {
        public string Action;
        public JObject Params;
        public int DelayMs;
    }

    /// <summary>
    /// State tracking for a running sequence job.
    /// </summary>
    public class SequenceJobState
    {
        public string JobId;
        public string Status; // "running", "completed", "failed"
        public int CurrentStep;
        public int TotalSteps;
        public string Error;
    }

    /// <summary>
    /// Runtime MonoBehaviour that executes input simulation sequences as coroutines.
    /// Singleton — auto-created on Play Mode start, survives scene loads.
    /// </summary>
    public class InputSimulationBridge : MonoBehaviour
    {
        static InputSimulationBridge _instance;
        public static InputSimulationBridge Instance
        {
            get
            {
                if (_instance == null) CreateInstance();
                return _instance;
            }
        }

        const float TimeoutSeconds = 60f;
        readonly Dictionary<string, SequenceJobState> _jobs = new Dictionary<string, SequenceJobState>();
        readonly Dictionary<string, Coroutine> _coroutines = new Dictionary<string, Coroutine>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate() => CreateInstance();

        static void CreateInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("[MCP] InputSimulationBridge");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<InputSimulationBridge>();
        }

        public string StartSequence(List<SequenceStep> steps)
        {
            string jobId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var state = new SequenceJobState
            {
                JobId = jobId,
                Status = "running",
                CurrentStep = 0,
                TotalSteps = steps.Count
            };
            _jobs[jobId] = state;
            _coroutines[jobId] = StartCoroutine(RunSequence(jobId, steps, state));
            return jobId;
        }

        public SequenceJobState GetJobState(string jobId)
        {
            _jobs.TryGetValue(jobId, out var state);
            return state;
        }

        IEnumerator RunSequence(string jobId, List<SequenceStep> steps, SequenceJobState state)
        {
            float startTime = Time.realtimeSinceStartup;

            for (int i = 0; i < steps.Count; i++)
            {
                // Timeout check
                if (Time.realtimeSinceStartup - startTime > TimeoutSeconds)
                {
                    state.Status = "failed";
                    state.Error = $"Sequence timed out after {TimeoutSeconds}s at step {i}.";
                    yield break;
                }

                state.CurrentStep = i;
                var step = steps[i];

                // Delay before action
                if (step.DelayMs > 0)
                    yield return new WaitForSeconds(step.DelayMs / 1000f);

                // Execute step via delegate — bridge Runtime→Editor assembly boundary
                try
                {
                    if (_commandInvoker != null)
                    {
                        var result = _commandInvoker("manage_input_simulation", step.Params);
                        // Check if result indicates failure
                        if (result is string s && s.StartsWith("ERROR:"))
                        {
                            state.Status = "failed";
                            state.Error = $"Step {i} ({step.Action}) failed: {s}";
                            yield break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    state.Status = "failed";
                    state.Error = $"Step {i} ({step.Action}) threw: {ex.Message}";
                    yield break;
                }

                // Yield a frame between steps for input processing
                yield return null;
            }

            state.CurrentStep = steps.Count;
            state.Status = "completed";
            _coroutines.Remove(jobId);
        }

        // Delegate for Editor → Runtime command invocation
        static Func<string, JObject, object> _commandInvoker;

        /// <summary>
        /// Called by Editor code to register the command invoker delegate.
        /// This bridges the Runtime → Editor assembly boundary.
        /// </summary>
        public static void RegisterCommandInvoker(Func<string, JObject, object> invoker)
        {
            _commandInvoker = invoker;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
