using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools
{
    using System.Collections.Generic;

    /// <summary>
    /// STUDIO: Handles operation queuing for batch execution of MCP commands.
    /// Allows AI assistants to queue multiple operations and execute them atomically.
    /// </summary>
    public static class ManageQueue
    {
        /// <summary>
        /// Main handler for queue management commands
        /// </summary>
        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
            {
                return Response.EnhancedError(
                    "Parameters cannot be null",
                    "Queue management command received null parameters",
                    "Provide action parameter (add, execute, execute_async, list, clear, stats, cancel)",
                    new[] { "add", "execute", "execute_async", "list", "clear", "stats", "cancel" },
                    "NULL_PARAMS"
                );
            }

            var action = @params["action"]?.ToString()?.ToLower();

            if (string.IsNullOrEmpty(action))
            {
                return Response.EnhancedError(
                    "Action parameter is required",
                    "Queue management requires an action to be specified",
                    "Use one of: add, execute, execute_async, list, clear, stats, remove, cancel",
                    new[] { "add", "execute", "execute_async", "list", "clear", "stats", "remove", "cancel" },
                    "MISSING_ACTION"
                );
            }

            switch (action)
            {
                case "add":
                    return AddOperation(@params);

                case "execute":
                    return ExecuteBatch(@params);

                case "execute_async":
                    return ExecuteBatchAsync(@params);

                case "list":
                    return ListOperations(@params);

                case "clear":
                    return ClearQueue(@params);

                case "stats":
                    return GetQueueStats(@params);

                case "remove":
                    return RemoveOperation(@params);

                case "cancel":
                    return CancelOperation(@params);

                default:
                    return Response.EnhancedError(
                        $"Unknown queue action: '{action}'",
                        "Queue management action not recognized",
                        "Use one of: add, execute, execute_async, list, clear, stats, remove, cancel",
                        new[] { "add", "execute", "execute_async", "list", "clear", "stats", "remove", "cancel" },
                        "UNKNOWN_ACTION"
                    );
            }
        }

        /// <summary>
        /// Add an operation to the queue
        /// </summary>
        private static object AddOperation(JObject @params)
        {
            try
            {
                var tool = @params["tool"]?.ToString();
                var operationParams = @params["parameters"] as JObject;
                var timeoutMs = @params["timeout_ms"]?.ToObject<int>() ?? 30000;

                if (string.IsNullOrEmpty(tool))
                {
                    return Response.EnhancedError(
                        "Tool parameter is required for add action",
                        "Adding operation to queue requires specifying which tool to execute",
                        "Specify tool name (e.g., 'manage_script', 'manage_asset')",
                        new[] { "manage_script", "manage_asset", "manage_scene", "manage_gameobject" },
                        "MISSING_TOOL"
                    );
                }

                if (operationParams == null)
                {
                    return Response.EnhancedError(
                        "Parameters object is required for add action",
                        "Adding operation to queue requires parameters for the tool",
                        "Provide parameters object with the required fields for the tool",
                        null,
                        "MISSING_PARAMETERS"
                    );
                }

                var operationId = OperationQueue.AddOperation(tool, operationParams, timeoutMs);

                return Response.Success(
                    $"Operation queued successfully with ID: {operationId}",
                    new
                    {
                        operation_id = operationId,
                        tool,
                        timeout_ms = timeoutMs,
                        queued_at = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                        queue_stats = OperationQueue.GetQueueStats()
                    }
                );
            }
            catch (Exception ex)
            {
                return Response.EnhancedError(
                    $"Failed to add operation to queue: {ex.Message}",
                    "Error occurred while adding operation to execution queue",
                    "Check tool name and parameters format",
                    null,
                    "ADD_OPERATION_ERROR"
                );
            }
        }

        /// <summary>
        /// Execute all queued operations
        /// </summary>
        private static object ExecuteBatch(JObject @params)
        {
            try
            {
                return OperationQueue.ExecuteBatch();
            }
            catch (Exception ex)
            {
                return Response.EnhancedError(
                    $"Failed to execute batch operations: {ex.Message}",
                    "Error occurred during batch execution of queued operations",
                    "Check Unity console for detailed error messages",
                    null,
                    "BATCH_EXECUTION_ERROR"
                );
            }
        }

        /// <summary>
        /// Execute all queued operations asynchronously
        /// </summary>
        private static object ExecuteBatchAsync(JObject @params)
        {
            try
            {
                // For Unity Editor, we need to use Unity's main thread dispatcher
                // Since Unity doesn't handle async well in the editor, we'll use a coroutine approach
                var asyncResult = ExecuteBatchAsyncUnityCompatible();
                return asyncResult;
            }
            catch (Exception ex)
            {
                return Response.EnhancedError(
                    $"Failed to execute batch operations asynchronously: {ex.Message}",
                    "Error occurred during async batch execution of queued operations",
                    "Check Unity console for detailed error messages, consider using synchronous execution",
                    null,
                    "ASYNC_BATCH_EXECUTION_ERROR"
                );
            }
        }

        /// <summary>
        /// Unity-compatible async batch execution using EditorCoroutines
        /// </summary>
        private static object ExecuteBatchAsyncUnityCompatible()
        {
            // For Unity Editor compatibility, we'll execute with yielding between operations
            // This prevents UI freezing while still being "async" from Unity's perspective

            var pendingOps = OperationQueue.GetOperations("pending");
            if (pendingOps.Count == 0)
            {
                return Response.Success("No pending operations to execute.", new { executed_count = 0 });
            }

            Debug.Log($"STUDIO: Starting async execution of {pendingOps.Count} operations");

            // Start the async execution using Unity's EditorApplication.delayCall
            // This allows Unity Editor to remain responsive
            EditorApplication.delayCall += () => ExecuteOperationsWithYield(pendingOps);

            return Response.Success(
                $"Started async execution of {pendingOps.Count} operations",
                new
                {
                    total_operations = pendingOps.Count,
                    status = "started_async",
                    message = "Use 'stats' action to monitor progress"
                }
            );
        }

        /// <summary>
        /// Execute operations with yielding to keep Unity Editor responsive
        /// </summary>
        private static async void ExecuteOperationsWithYield(List<OperationQueue.QueuedOperation> operations)
        {
            foreach (var operation in operations)
            {
                try
                {
                    // Update status to executing
                    operation.Status = "executing";
                    operation.ExecutionStartTime = DateTime.UtcNow;

                    Debug.Log($"STUDIO: Executing operation {operation.Id} ({operation.Tool})");

                    // Execute the operation
                    var result = await Task.Run(() =>
                    {
                        try
                        {
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
                        catch (Exception e)
                        {
                            throw new Exception($"Operation {operation.Id} failed: {e.Message}", e);
                        }
                    });

                    // Update operation status
                    operation.Result = result;
                    operation.Status = "executed";
                    operation.ExecutionEndTime = DateTime.UtcNow;

                    Debug.Log($"STUDIO: Completed operation {operation.Id}");
                }
                catch (Exception ex)
                {
                    operation.Error = ex;
                    operation.Status = "failed";
                    operation.ExecutionEndTime = DateTime.UtcNow;
                    Debug.LogError($"STUDIO: Operation {operation.Id} failed: {ex.Message}");
                }

                // Yield control back to Unity Editor to keep it responsive
                await Task.Yield();
            }

            Debug.Log("STUDIO: Async batch execution completed");
        }

        /// <summary>
        /// Cancel a running operation
        /// </summary>
        private static object CancelOperation(JObject @params)
        {
            try
            {
                var operationId = @params["operation_id"]?.ToString();

                if (string.IsNullOrEmpty(operationId))
                {
                    return Response.EnhancedError(
                        "Operation ID is required for cancel action",
                        "Cancelling operation requires operation ID",
                        "Use 'list' action to see available operation IDs",
                        null,
                        "MISSING_OPERATION_ID"
                    );
                }

                var cancelled = OperationQueue.CancelOperation(operationId);

                if (cancelled)
                {
                    return Response.Success(
                        $"Operation {operationId} cancelled successfully",
                        new
                        {
                            operation_id = operationId,
                            cancelled = true,
                            queue_stats = OperationQueue.GetQueueStats()
                        }
                    );
                }
                else
                {
                    return Response.EnhancedError(
                        $"Operation {operationId} could not be cancelled",
                        "Operation may not exist or is not currently executing",
                        "Use 'list' action to see available operation IDs and their status",
                        null,
                        "CANCEL_FAILED"
                    );
                }
            }
            catch (Exception ex)
            {
                return Response.EnhancedError(
                    $"Failed to cancel operation: {ex.Message}",
                    "Error occurred while cancelling operation",
                    "Check operation ID format and queue accessibility",
                    null,
                    "CANCEL_OPERATION_ERROR"
                );
            }
        }

        /// <summary>
        /// List operations in the queue
        /// </summary>
        private static object ListOperations(JObject @params)
        {
            try
            {
                var statusFilter = @params["status"]?.ToString()?.ToLower();
                var limit = @params["limit"]?.ToObject<int?>();

                var operations = OperationQueue.GetOperations(statusFilter);

                if (limit.HasValue && limit.Value > 0)
                {
                    operations = operations.Take(limit.Value).ToList();
                }

                var operationData = operations.Select(op => new
                {
                    id = op.Id,
                    tool = op.Tool,
                    status = op.Status,
                    queued_at = op.QueuedAt.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                    parameters = op.Parameters,
                    result = op.Status == "executed" ? op.Result : null,
                    error = op.Status == "failed" ? op.Error?.Message : null
                }).ToList();

                return Response.Success(
                    $"Found {operationData.Count} operations" + (statusFilter != null ? $" with status '{statusFilter}'" : ""),
                    new
                    {
                        operations = operationData,
                        total_count = operations.Count,
                        status_filter = statusFilter,
                        queue_stats = OperationQueue.GetQueueStats()
                    }
                );
            }
            catch (Exception ex)
            {
                return Response.EnhancedError(
                    $"Failed to list queue operations: {ex.Message}",
                    "Error occurred while retrieving queue operations",
                    "Check if queue system is properly initialized",
                    null,
                    "LIST_OPERATIONS_ERROR"
                );
            }
        }

        /// <summary>
        /// Clear operations from the queue
        /// </summary>
        private static object ClearQueue(JObject @params)
        {
            try
            {
                var statusFilter = @params["status"]?.ToString()?.ToLower();
                var removedCount = OperationQueue.ClearQueue(statusFilter);

                var message = statusFilter != null
                    ? $"Cleared {removedCount} operations with status '{statusFilter}'"
                    : $"Cleared {removedCount} completed operations from queue";

                return Response.Success(message, new
                {
                    removed_count = removedCount,
                    status_filter = statusFilter,
                    queue_stats = OperationQueue.GetQueueStats()
                });
            }
            catch (Exception ex)
            {
                return Response.EnhancedError(
                    $"Failed to clear queue: {ex.Message}",
                    "Error occurred while clearing queue operations",
                    "Check if queue system is accessible",
                    null,
                    "CLEAR_QUEUE_ERROR"
                );
            }
        }

        /// <summary>
        /// Get queue statistics
        /// </summary>
        private static object GetQueueStats(JObject @params)
        {
            try
            {
                var stats = OperationQueue.GetQueueStats();
                return Response.Success("Queue statistics retrieved", stats);
            }
            catch (Exception ex)
            {
                return Response.EnhancedError(
                    $"Failed to get queue statistics: {ex.Message}",
                    "Error occurred while retrieving queue statistics",
                    "Check if queue system is properly initialized",
                    null,
                    "QUEUE_STATS_ERROR"
                );
            }
        }

        /// <summary>
        /// Remove a specific operation from the queue
        /// </summary>
        private static object RemoveOperation(JObject @params)
        {
            try
            {
                var operationId = @params["operation_id"]?.ToString();

                if (string.IsNullOrEmpty(operationId))
                {
                    return Response.EnhancedError(
                        "Operation ID is required for remove action",
                        "Removing specific operation requires operation ID",
                        "Use 'list' action to see available operation IDs",
                        null,
                        "MISSING_OPERATION_ID"
                    );
                }

                var removed = OperationQueue.RemoveOperation(operationId);

                if (removed)
                {
                    return Response.Success(
                        $"Operation {operationId} removed from queue",
                        new
                        {
                            operation_id = operationId,
                            queue_stats = OperationQueue.GetQueueStats()
                        }
                    );
                }
                else
                {
                    return Response.EnhancedError(
                        $"Operation {operationId} not found in queue",
                        "Specified operation ID does not exist in the queue",
                        "Use 'list' action to see available operation IDs",
                        null,
                        "OPERATION_NOT_FOUND",
                        null,
                        null
                    );
                }
            }
            catch (Exception ex)
            {
                return Response.EnhancedError(
                    $"Failed to remove operation: {ex.Message}",
                    "Error occurred while removing operation from queue",
                    "Check operation ID format and queue accessibility",
                    null,
                    "REMOVE_OPERATION_ERROR"
                );
            }
        }
    }
}