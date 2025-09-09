using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools
{
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
                    "Provide action parameter (add, execute, list, clear, stats)",
                    new[] { "add", "execute", "list", "clear", "stats" },
                    "NULL_PARAMS"
                );
            }

            string action = @params["action"]?.ToString()?.ToLower();
            
            if (string.IsNullOrEmpty(action))
            {
                return Response.EnhancedError(
                    "Action parameter is required",
                    "Queue management requires an action to be specified",
                    "Use one of: add, execute, list, clear, stats, remove",
                    new[] { "add", "execute", "list", "clear", "stats", "remove" },
                    "MISSING_ACTION"
                );
            }

            switch (action)
            {
                case "add":
                    return AddOperation(@params);
                
                case "execute":
                    return ExecuteBatch(@params);
                
                case "list":
                    return ListOperations(@params);
                
                case "clear":
                    return ClearQueue(@params);
                
                case "stats":
                    return GetQueueStats(@params);
                
                case "remove":
                    return RemoveOperation(@params);
                
                default:
                    return Response.EnhancedError(
                        $"Unknown queue action: '{action}'",
                        "Queue management action not recognized",
                        "Use one of: add, execute, list, clear, stats, remove",
                        new[] { "add", "execute", "list", "clear", "stats", "remove" },
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
                string tool = @params["tool"]?.ToString();
                JObject operationParams = @params["parameters"] as JObject;

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

                string operationId = OperationQueue.AddOperation(tool, operationParams);
                
                return Response.Success(
                    $"Operation queued successfully with ID: {operationId}",
                    new
                    {
                        operation_id = operationId,
                        tool = tool,
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
        /// List operations in the queue
        /// </summary>
        private static object ListOperations(JObject @params)
        {
            try
            {
                string statusFilter = @params["status"]?.ToString()?.ToLower();
                int? limit = @params["limit"]?.ToObject<int?>();
                
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
                string statusFilter = @params["status"]?.ToString()?.ToLower();
                int removedCount = OperationQueue.ClearQueue(statusFilter);
                
                string message = statusFilter != null 
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
                string operationId = @params["operation_id"]?.ToString();
                
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

                bool removed = OperationQueue.RemoveOperation(operationId);
                
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