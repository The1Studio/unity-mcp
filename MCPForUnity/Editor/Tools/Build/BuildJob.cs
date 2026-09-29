using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace MCPForUnity.Editor.Tools.Build
{
    /// <summary>
    /// Lifecycle state of a build (or batch) job as reported back to the MCP client.
    /// </summary>
    public enum BuildJobState
    {
        /// <summary>Queued but not yet started.</summary>
        Pending,

        /// <summary>Currently running in the editor's build pipeline.</summary>
        Building,

        /// <summary>Finished without errors.</summary>
        Succeeded,

        /// <summary>Finished with at least one error.</summary>
        Failed,

        /// <summary>Aborted by the caller before it completed.</summary>
        Cancelled,

        /// <summary>Not run, e.g. its platform was excluded from a batch.</summary>
        Skipped
    }

    /// <summary>
    /// State and outcome of a single-platform player build, captured from the
    /// <see cref="BuildReport"/> at completion so the heavy native report object is not retained.
    /// </summary>
    public class BuildJob
    {
        /// <summary>Identifier the client uses to poll this job via the <c>build</c> tool.</summary>
        public string JobId { get; }

        /// <summary>Current lifecycle state; defaults to <see cref="BuildJobState.Pending"/>.</summary>
        public BuildJobState State { get; set; } = BuildJobState.Pending;

        /// <summary>Platform this player build targets.</summary>
        public BuildTarget Target { get; set; }

        /// <summary>Absolute path the built player is written to.</summary>
        public string OutputPath { get; set; }

        /// <summary>UTC timestamp the build started; <c>default</c> until it does.</summary>
        public DateTime StartedAt { get; set; }

        /// <summary>UTC timestamp the build finished, or null while it is still running.</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>Failure detail; null or empty when the build did not fail.</summary>
        public string ErrorMessage { get; set; }

        /// <summary>Total size of the built output in megabytes, from the build report.</summary>
        public double TotalSizeMb { get; set; }

        /// <summary>Number of errors the build reported.</summary>
        public int TotalErrors { get; set; }

        /// <summary>Number of warnings the build reported.</summary>
        public int TotalWarnings { get; set; }

        /// <summary>
        /// Creates a job in the <see cref="BuildJobState.Pending"/> state.
        /// </summary>
        /// <param name="jobId">Identifier the client will poll with.</param>
        /// <param name="target">Platform this build targets.</param>
        /// <param name="outputPath">Absolute path the built player is written to.</param>
        public BuildJob(string jobId, BuildTarget target, string outputPath)
        {
            JobId = jobId;
            Target = target;
            OutputPath = outputPath;
        }

        /// <summary>
        /// Projects the job into the JSON shape the <c>build</c> tool returns. Timing and
        /// size/error fields are included only once they are meaningful, so a client polling a
        /// pending job does not read stale or zeroed values.
        /// </summary>
        /// <returns>A dictionary ready to serialize as the tool's status payload.</returns>
        public object ToStatusResponse()
        {
            var data = new Dictionary<string, object>
            {
                ["job_id"] = JobId,
                ["result"] = State.ToString().ToLowerInvariant(),
                ["platform"] = Target.ToString(),
                ["output_path"] = OutputPath
            };

            if (StartedAt != default)
                data["started_at"] = StartedAt.ToString("O");

            if (CompletedAt.HasValue)
            {
                data["duration_seconds"] = (CompletedAt.Value - StartedAt).TotalSeconds;
                data["completed_at"] = CompletedAt.Value.ToString("O");
            }

            if (State == BuildJobState.Succeeded || State == BuildJobState.Failed)
            {
                data["total_size_mb"] = TotalSizeMb;
                data["errors"] = TotalErrors;
                data["warnings"] = TotalWarnings;
            }

            if (!string.IsNullOrEmpty(ErrorMessage))
                data["error"] = ErrorMessage;

            return data;
        }
    }

    /// <summary>
    /// A multi-platform build request: the parent job that owns the per-platform
    /// <see cref="BuildJob"/> children built one after another.
    /// </summary>
    public class BatchJob
    {
        /// <summary>Identifier the client uses to poll the whole batch.</summary>
        public string JobId { get; }

        /// <summary>Aggregate lifecycle state; defaults to <see cref="BuildJobState.Pending"/>.</summary>
        public BuildJobState State { get; set; } = BuildJobState.Pending;

        /// <summary>Per-platform builds, in execution order.</summary>
        public List<BuildJob> Children { get; } = new();

        /// <summary>Index of the child currently building, or -1 when none is running.</summary>
        public int CurrentIndex { get; set; } = -1;

        /// <summary>
        /// Creates a batch job in the <see cref="BuildJobState.Pending"/> state.
        /// </summary>
        /// <param name="jobId">Identifier the client will poll with.</param>
        public BatchJob(string jobId)
        {
            JobId = jobId;
        }

        /// <summary>
        /// Projects the batch into the JSON shape the <c>build</c> tool returns: the aggregate
        /// state, how many children are terminal, which child is building, and each child's own
        /// status payload.
        /// </summary>
        /// <returns>A dictionary ready to serialize as the tool's status payload.</returns>
        public object ToStatusResponse()
        {
            int completed = 0;
            string currentBuild = null;
            var builds = new List<object>();

            foreach (var child in Children)
            {
                if (child.State == BuildJobState.Succeeded || child.State == BuildJobState.Failed
                    || child.State == BuildJobState.Skipped || child.State == BuildJobState.Cancelled)
                    completed++;
                if (child.State == BuildJobState.Building)
                    currentBuild = child.JobId;
                builds.Add(child.ToStatusResponse());
            }

            return new Dictionary<string, object>
            {
                ["job_id"] = JobId,
                ["result"] = State.ToString().ToLowerInvariant(),
                ["completed"] = completed,
                ["total"] = Children.Count,
                ["current_build"] = currentBuild,
                ["builds"] = builds
            };
        }
    }

    /// <summary>
    /// Static store for all build jobs. Note: static fields are cleared on domain reload,
    /// but this is acceptable because BuildPipeline.BuildPlayer blocks the editor thread,
    /// preventing domain reload during a build. For batch builds with platform switches,
    /// the batch scheduling happens after each build completes via EditorApplication.update
    /// callbacks (ScheduleOnNextUpdate / WaitForCompletion), so state is maintained within
    /// a single domain lifecycle.
    /// </summary>
    public static class BuildJobStore
    {
        private static readonly Dictionary<string, BuildJob> _buildJobs = new();
        private static readonly Dictionary<string, BatchJob> _batchJobs = new();
        private static BuildJob _lastCompletedJob;

        /// <summary>
        /// Generates a short unique id for a single-platform build job.
        /// </summary>
        /// <returns>An id of the form <c>build-</c> plus 9 hex characters.</returns>
        public static string CreateJobId() => $"build-{Guid.NewGuid():N}".Substring(0, 16);

        /// <summary>
        /// Generates a short unique id for a batch job.
        /// </summary>
        /// <returns>An id of the form <c>batch-</c> plus 9 hex characters.</returns>
        public static string CreateBatchId() => $"batch-{Guid.NewGuid():N}".Substring(0, 16);

        /// <summary>Registers a single-platform build job so clients can poll it by id.</summary>
        /// <param name="job">The job to add; an existing job with the same id is replaced.</param>
        public static void AddBuildJob(BuildJob job) => _buildJobs[job.JobId] = job;

        /// <summary>Registers a batch job so clients can poll it by id.</summary>
        /// <param name="job">The batch to add; an existing batch with the same id is replaced.</param>
        public static void AddBatchJob(BatchJob job) => _batchJobs[job.JobId] = job;

        /// <summary>
        /// Looks up a previously registered build job.
        /// </summary>
        /// <param name="jobId">Id returned by <see cref="CreateJobId"/>.</param>
        /// <returns>The job, or null when the id is unknown or the job was pruned.</returns>
        public static BuildJob GetBuildJob(string jobId)
        {
            _buildJobs.TryGetValue(jobId, out var job);
            return job;
        }

        /// <summary>
        /// Looks up a previously registered batch job.
        /// </summary>
        /// <param name="jobId">Id returned by <see cref="CreateBatchId"/>.</param>
        /// <returns>The batch, or null when the id is unknown or the batch was pruned.</returns>
        public static BatchJob GetBatchJob(string jobId)
        {
            _batchJobs.TryGetValue(jobId, out var job);
            return job;
        }

        /// <summary>
        /// The most recently finished build, kept regardless of pruning so a client can retrieve
        /// the last result without holding its id. Null until the first build completes.
        /// </summary>
        public static BuildJob LastCompletedJob => _lastCompletedJob;

        /// <summary>
        /// Records the just-finished build as the last completed one and prunes the job store back
        /// under its retention cap.
        /// </summary>
        /// <param name="job">The build that just finished.</param>
        public static void SetLastCompleted(BuildJob job)
        {
            _lastCompletedJob = job;
            PruneOldJobs();
        }

        private const int MaxRetainedJobs = 50;

        private static void PruneOldJobs()
        {
            if (_buildJobs.Count <= MaxRetainedJobs) return;

            var toRemove = new List<string>();
            foreach (var kvp in _buildJobs)
            {
                if (kvp.Value.State != BuildJobState.Building && kvp.Value.State != BuildJobState.Pending
                    && kvp.Value != _lastCompletedJob)
                    toRemove.Add(kvp.Key);
            }

            foreach (var key in toRemove)
            {
                _buildJobs.Remove(key);
                if (_buildJobs.Count <= MaxRetainedJobs / 2) break;
            }

            // Also prune batch jobs whose children are all terminal
            var batchesToRemove = new List<string>();
            foreach (var kvp in _batchJobs)
            {
                var batch = kvp.Value;
                if (batch.State == BuildJobState.Building || batch.State == BuildJobState.Pending)
                    continue;
                // Remove child references that were already pruned from _buildJobs
                batch.Children.RemoveAll(c => !_buildJobs.ContainsKey(c.JobId));
                if (batch.Children.Count == 0)
                    batchesToRemove.Add(kvp.Key);
            }
            foreach (var key in batchesToRemove)
                _batchJobs.Remove(key);
        }
    }
}
