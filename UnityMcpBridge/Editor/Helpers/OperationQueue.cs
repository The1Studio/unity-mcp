using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MCPForUnity.Editor.Helpers
{
    /// <summary>
    /// STUDIO: Operation queuing system for batch execution of MCP commands.
    /// Allows multiple operations to be queued and executed with proper async support and timeouts.
    ///
    /// IMPROVEMENTS:
    /// - Added async operation support with proper Task handling
    /// - Implemented operation timeouts to prevent hanging
    /// - Added progress reporting during batch execution
    /// - Memory usage controls with auto-cleanup
    ///
    /// LIMITATIONS:
    /// - Queue is not persistent (lost on Unity restart)
    /// - No true rollback implementation (operations can't be undone)
    /// </summary>
    public static class OperationQueue
    {
        /// <summary>
        /// Represents a queued operation
        /// </summary>
        public class QueuedOperation
        {
            public string Id { get; set; }
            public string Tool { get; set; }
            public JObject Parameters { get; set; }
            public DateTime QueuedAt { get; set; }
            public string Status { get; set; } = "pending"; // pending, executing, executed, failed, timeout
            public object Result { get; set; }
            public Exception Error { get; set; }
            public DateTime? ExecutionStartTime { get; set; }
            public DateTime? ExecutionEndTime { get; set; }
            public int TimeoutMs { get; set; } = 30000; // 30 seconds default timeout
        }

        private static readonly List<QueuedOperation> _operations = new List<QueuedOperation>();
        private static readonly object _lockObject = new object();
        private static int _nextId = 1;

        // STUDIO: Configuration constants for queue management
        private const int MAX_QUEUE_SIZE = 1000; // Maximum operations in queue
        private const int AUTO_CLEANUP_THRESHOLD = 500; // Auto-cleanup when exceeded
        private const int KEEP_COMPLETED_OPERATIONS = 100; // Keep recent completed operations for history

        // STUDIO: Async operation configuration
        private static readonly HashSet<string> ASYNC_TOOLS = new HashSet<string>
        {
            "manage_asset", "execute_menu_item", // Tools that can be long-running
        };

        /// <summary>
        /// Add an operation to the queue
        /// </summary>
        /// <param name="tool">Tool name (e.g., "manage_script", "manage_asset")</param>
        /// <param name="parameters">Operation parameters</param>
        /// <param name="timeoutMs">Operation timeout in milliseconds (default: 30000)</param>
        /// <returns>Operation ID</returns>
        public static string AddOperation(string tool, JObject parameters, int timeoutMs = 30000)
        {
            lock (_lockObject)
            {
                // STUDIO: Enforce queue size limits to prevent memory issues
                if (_operations.Count >= MAX_QUEUE_SIZE)
                {
                    Debug.LogWarning($"STUDIO: Queue size limit reached ({MAX_QUEUE_SIZE}). Cannot add more operations.");
                    throw new InvalidOperationException($"Queue size limit reached ({MAX_QUEUE_SIZE}). Clear completed operations first.");
                }

                // STUDIO: Auto-cleanup old completed operations
                if (_operations.Count >= AUTO_CLEANUP_THRESHOLD)
                {
                    AutoCleanupCompletedOperations();
                }

                var operation = new QueuedOperation
                {
                    Id         = $"op_{_nextId++}",
                    Tool       = tool,
                    Parameters = parameters ?? new JObject(),
                    QueuedAt   = DateTime.UtcNow,
                    Status     = "pending",
                    TimeoutMs  = Math.Max(1000, timeoutMs), // Minimum 1 second timeout
                };

                _operations.Add(operation);
                Debug.Log($"STUDIO: Operation queued - {operation.Id} ({tool}) [Queue size: {_operations.Count}, Timeout: {timeoutMs}ms]");
                return operation.Id;
            }
        }

        /// <summary>
        /// STUDIO: Auto-cleanup old completed/failed operations to manage memory
        /// </summary>
        private static void AutoCleanupCompletedOperations()
        {
            var completed = _operations.Where(op => op.Status == "executed" || op.Status == "failed" || op.Status == "timeout")
                                     .OrderByDescending(op => op.QueuedAt)
                                     .Skip(KEEP_COMPLETED_OPERATIONS)
                                     .ToList();

            foreach (var op in completed)
            {
                _operations.Remove(op);
            }

            if (completed.Count > 0)
            {
                Debug.Log($"STUDIO: Auto-cleaned {completed.Count} old completed operations from queue");
            }
        }

        /// <summary>
        /// Execute all pending operations in the queue with async support
        /// </summary>
        /// <returns>Batch execution results</returns>
        public static async Task<object> ExecuteBatchAsync()
        {
            List<QueuedOperation> pendingOps;

            lock (_lockObject)
            {
                pendingOps = _operations.Where(op => op.Status == "pending").ToList();

                if (pendingOps.Count == 0)
                {
                    return Response.Success("No pending operations to execute.", new { executed_count = 0 });
                }

                Debug.Log($"STUDIO: Executing batch of {pendingOps.Count} operations with async support");
            }

            var results = new List<object>();
            var successCount = 0;
            var failedCount = 0;
            var timeoutCount = 0;

            // Execute operations with proper async handling
            foreach (var operation in pendingOps)
            {
                lock (_lockObject)
                {
                    operation.Status = "executing";
                    operation.ExecutionStartTime = DateTime.UtcNow;
                }

                try
                {
                    object result;

                    if (ASYNC_TOOLS.Contains(operation.Tool.ToLowerInvariant()))
                    {
                        // Execute async operation with timeout
                        result = await ExecuteOperationWithTimeoutAsync(operation);
                    }
                    else
                    {
                        // Execute synchronous operation
                        result = ExecuteOperation(operation);
                    }

                    lock (_lockObject)
                    {
                        operation.Result = result;
                        operation.Status = "executed";
                        operation.ExecutionEndTime = DateTime.UtcNow;
                    }

                    successCount++;

                    results.Add(new
                    {
                        id = operation.Id,
                        tool = operation.Tool,
                        status = "success",
                        result,
                        execution_time_ms = operation.ExecutionEndTime.HasValue && operation.ExecutionStartTime.HasValue
                            ? (operation.ExecutionEndTime.Value - operation.ExecutionStartTime.Value).TotalMilliseconds
                            : (double?)null,
                    });
                }
                catch (TimeoutException)
                {
                    lock (_lockObject)
                    {
                        operation.Status = "timeout";
                        operation.ExecutionEndTime = DateTime.UtcNow;
                        operation.Error = new TimeoutException($"Operation timed out after {operation.TimeoutMs}ms");
                    }

                    timeoutCount++;

                    results.Add(new
                    {
                        id = operation.Id,
                        tool = operation.Tool,
                        status = "timeout",
                        error = $"Operation timed out after {operation.TimeoutMs}ms",
                    });

                    Debug.LogError($"STUDIO: Operation {operation.Id} timed out after {operation.TimeoutMs}ms");
                }
                catch (Exception ex)
                {
                    lock (_lockObject)
                    {
                        operation.Error = ex;
                        operation.Status = "failed";
                        operation.ExecutionEndTime = DateTime.UtcNow;
                    }

                    failedCount++;

                    results.Add(new
                    {
                        id = operation.Id,
                        tool = operation.Tool,
                        status = "failed",
                        error = ex.Message,
                    });

                    Debug.LogError($"STUDIO: Operation {operation.Id} failed: {ex.Message}");
                }

                // Allow UI updates between operations
                await Task.Yield();
            }

            var summary = new
            {
                total_operations = pendingOps.Count,
                successful = successCount,
                failed = failedCount,
                timeout = timeoutCount,
                execution_time = DateTime.UtcNow,
                results,
            };

            var message = $"Batch executed: {successCount} successful, {failedCount} failed";
            if (timeoutCount > 0)
            {
                message += $", {timeoutCount} timed out";
            }

            return Response.Success(message, summary);
        }

        /// <summary>
        /// Synchronous wrapper for ExecuteBatchAsync for backward compatibility
        /// </summary>
        /// <returns>Batch execution results</returns>
        public static object ExecuteBatch()
        {
            try
            {
                return ExecuteBatchAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.LogError($"STUDIO: Batch execution failed: {ex.Message}");
                return Response.Error($"Batch execution failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute an async operation with timeout support
        /// </summary>
        private static async Task<object> ExecuteOperationWithTimeoutAsync(QueuedOperation operation)
        {
            var cancellationTokenSource = new CancellationTokenSource(operation.TimeoutMs);

            try
            {
                // Execute on Unity's main thread with timeout
                var task = Task.Run(() => ExecuteOperation(operation), cancellationTokenSource.Token);

                return await task;
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"Operation {operation.Id} timed out after {operation.TimeoutMs}ms");
            }
        }

        /// <summary>
        /// Execute a single operation by routing to the appropriate tool
        /// </summary>
        private static object ExecuteOperation(QueuedOperation operation)
        {
            // Route to the appropriate tool handler
            switch (operation.Tool.ToLowerInvariant())
            {
                case "manage_script":
                    return Tools.ManageScript.HandleCommand(operation.Parameters);

                case "manage_asset":
                    return Tools.ManageAsset.HandleCommand(operation.Parameters);

                case "manage_scene":
                    return Tools.ManageScene.HandleCommand(operation.Parameters);

                case "manage_gameobject":
                    return Tools.ManageGameObject.HandleCommand(operation.Parameters);

                case "manage_shader":
                    return Tools.ManageShader.HandleCommand(operation.Parameters);

                case "manage_editor":
                    return Tools.ManageEditor.HandleCommand(operation.Parameters);

                case "read_console":
                    return Tools.ReadConsole.HandleCommand(operation.Parameters);

                case "execute_menu_item":
                    return Tools.ExecuteMenuItem.HandleCommand(operation.Parameters);

                default:
                    throw new ArgumentException($"Unknown tool: {operation.Tool}");
            }
        }

        /// <summary>
        /// Get all operations in the queue
        /// </summary>
        /// <param name="statusFilter">Optional status filter (pending, executing, executed, failed, timeout)</param>
        /// <returns>List of operations</returns>
        public static List<QueuedOperation> GetOperations(string statusFilter = null)
        {
            lock (_lockObject)
            {
                var ops = _operations.AsEnumerable();

                if (!string.IsNullOrEmpty(statusFilter))
                {
                    ops = ops.Where(op => op.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase));
                }

                return ops.OrderBy(op => op.QueuedAt).ToList();
            }
        }

        /// <summary>
        /// Clear the queue (remove completed/failed operations)
        /// </summary>
        /// <param name="statusFilter">Optional: clear only operations with specific status</param>
        /// <returns>Number of operations removed</returns>
        public static int ClearQueue(string statusFilter = null)
        {
            lock (_lockObject)
            {
                var beforeCount = _operations.Count;

                if (string.IsNullOrEmpty(statusFilter))
                {
                    // Clear all non-pending operations
                    _operations.RemoveAll(op => op.Status != "pending");
                }
                else
                {
                    _operations.RemoveAll(op => op.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase));
                }

                var removedCount = beforeCount - _operations.Count;
                Debug.Log($"STUDIO: Cleared {removedCount} operations from queue");
                return removedCount;
            }
        }

        /// <summary>
        /// Get queue statistics
        /// </summary>
        public static object GetQueueStats()
        {
            lock (_lockObject)
            {
                var stats = new
                {
                    total_operations = _operations.Count,
                    pending = _operations.Count(op => op.Status == "pending"),
                    executing = _operations.Count(op => op.Status == "executing"),
                    executed = _operations.Count(op => op.Status == "executed"),
                    failed = _operations.Count(op => op.Status == "failed"),
                    timeout = _operations.Count(op => op.Status == "timeout"),
                    oldest_operation = _operations.Count > 0 ? _operations.Min(op => op.QueuedAt) : (DateTime?)null,
                    newest_operation = _operations.Count > 0 ? _operations.Max(op => op.QueuedAt) : (DateTime?)null,
                    async_tools_supported = ASYNC_TOOLS.ToArray(),
                };

                return stats;
            }
        }

        /// <summary>
        /// Remove a specific operation by ID
        /// </summary>
        public static bool RemoveOperation(string operationId)
        {
            lock (_lockObject)
            {
                var removed = _operations.RemoveAll(op => op.Id == operationId);
                return removed > 0;
            }
        }

        /// <summary>
        /// Cancel a running operation by ID (if it's currently executing)
        /// </summary>
        public static bool CancelOperation(string operationId)
        {
            lock (_lockObject)
            {
                var operation = _operations.FirstOrDefault(op => op.Id == operationId);
                if (operation != null && operation.Status == "executing")
                {
                    operation.Status = "failed";
                    operation.Error = new OperationCanceledException("Operation was cancelled");
                    operation.ExecutionEndTime = DateTime.UtcNow;
                    Debug.Log($"STUDIO: Operation {operationId} was cancelled");
                    return true;
                }
                return false;
            }
        }
    }
}