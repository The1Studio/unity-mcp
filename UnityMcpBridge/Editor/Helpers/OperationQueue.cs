using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MCPForUnity.Editor.Helpers
{
    /// <summary>
    /// STUDIO: Operation queuing system for batch execution of MCP commands.
    /// Allows multiple operations to be queued and executed atomically for better performance.
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
            public string Status { get; set; } = "pending"; // pending, executed, failed
            public object Result { get; set; }
            public Exception Error { get; set; }
        }

        private static readonly List<QueuedOperation> _operations = new List<QueuedOperation>();
        private static readonly object _lockObject = new object();
        private static int _nextId = 1;

        /// <summary>
        /// Add an operation to the queue
        /// </summary>
        /// <param name="tool">Tool name (e.g., "manage_script", "manage_asset")</param>
        /// <param name="parameters">Operation parameters</param>
        /// <returns>Operation ID</returns>
        public static string AddOperation(string tool, JObject parameters)
        {
            lock (_lockObject)
            {
                var operation = new QueuedOperation
                {
                    Id = $"op_{_nextId++}",
                    Tool = tool,
                    Parameters = parameters ?? new JObject(),
                    QueuedAt = DateTime.UtcNow,
                    Status = "pending"
                };

                _operations.Add(operation);
                Debug.Log($"STUDIO: Operation queued - {operation.Id} ({tool})");
                return operation.Id;
            }
        }

        /// <summary>
        /// Execute all pending operations in the queue
        /// </summary>
        /// <returns>Batch execution results</returns>
        public static object ExecuteBatch()
        {
            lock (_lockObject)
            {
                var pendingOps = _operations.Where(op => op.Status == "pending").ToList();
                
                if (pendingOps.Count == 0)
                {
                    return Response.Success("No pending operations to execute.", new { executed_count = 0 });
                }

                Debug.Log($"STUDIO: Executing batch of {pendingOps.Count} operations");
                
                var results = new List<object>();
                var successCount = 0;
                var failedCount = 0;

                foreach (var operation in pendingOps)
                {
                    try
                    {
                        // Execute the operation by routing to the appropriate tool
                        var result = ExecuteOperation(operation);
                        operation.Result = result;
                        operation.Status = "executed";
                        successCount++;
                        
                        results.Add(new
                        {
                            id = operation.Id,
                            tool = operation.Tool,
                            status = "success",
                            result = result
                        });
                    }
                    catch (Exception ex)
                    {
                        operation.Error = ex;
                        operation.Status = "failed";
                        failedCount++;
                        
                        results.Add(new
                        {
                            id = operation.Id,
                            tool = operation.Tool,
                            status = "failed",
                            error = ex.Message
                        });

                        Debug.LogError($"STUDIO: Operation {operation.Id} failed: {ex.Message}");
                    }
                }

                var summary = new
                {
                    total_operations = pendingOps.Count,
                    successful = successCount,
                    failed = failedCount,
                    execution_time = DateTime.UtcNow,
                    results = results
                };

                return Response.Success($"Batch executed: {successCount} successful, {failedCount} failed", summary);
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
        /// <param name="statusFilter">Optional status filter (pending, executed, failed)</param>
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
                    executed = _operations.Count(op => op.Status == "executed"),
                    failed = _operations.Count(op => op.Status == "failed"),
                    oldest_operation = _operations.Count > 0 ? _operations.Min(op => op.QueuedAt) : (DateTime?)null,
                    newest_operation = _operations.Count > 0 ? _operations.Max(op => op.QueuedAt) : (DateTime?)null
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
    }
}