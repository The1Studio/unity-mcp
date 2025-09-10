"""
STUDIO: Operation queuing tool for batch execution of MCP commands.
Allows AI assistants to queue multiple operations and execute them atomically for better performance.
"""

from mcp.server.fastmcp import FastMCP, Context
from unity_connection import send_command_with_retry
from typing import Dict, Any, Optional, List
import logging

logger = logging.getLogger(__name__)

def register_manage_queue(mcp: FastMCP):
    """Register the manage_queue tool with the MCP server."""
    
    @mcp.tool(description=(
        "STUDIO: Manage operation queue for batch execution of Unity MCP commands.\n\n"
        "Actions:\n"
        "- 'add': Add operation to queue (requires 'tool', 'parameters', optional 'timeout_ms')\n"
        "- 'execute': Execute all queued operations in batch (synchronous)\n"
        "- 'execute_async': Execute all queued operations asynchronously (non-blocking)\n"
        "- 'list': List operations in queue (optional 'status' and 'limit' filters)\n"
        "- 'clear': Clear completed operations from queue (optional 'status' filter)\n"
        "- 'stats': Get queue statistics\n"
        "- 'remove': Remove specific operation (requires 'operation_id')\n"
        "- 'cancel': Cancel running operation (requires 'operation_id')\n\n"
        "Benefits:\n"
        "- Reduced Unity Editor freezing during multiple operations\n"
        "- Async execution with timeout support\n"
        "- Better performance for bulk operations\n"
        "- Operation cancellation support\n\n"
        "Example usage:\n"
        "1. Add script creation: action='add', tool='manage_script', parameters={'action': 'create', 'name': 'Player'}, timeout_ms=30000\n"
        "2. Add asset import: action='add', tool='manage_asset', parameters={'action': 'import', 'path': 'model.fbx'}\n"
        "3. Execute async: action='execute_async'"
    ))
    def manage_queue(
        ctx: Context,
        action: str,
        tool: Optional[str] = None,
        parameters: Optional[Dict[str, Any]] = None,
        operation_id: Optional[str] = None,
        status: Optional[str] = None,
        limit: Optional[int] = None,
        timeout_ms: Optional[int] = None
    ) -> Dict[str, Any]:
        """
        Manage operation queue for batch execution of Unity MCP commands.
        
        Args:
            ctx: The MCP context
            action: Operation to perform (add, execute, execute_async, list, clear, stats, remove, cancel)
            tool: Tool name for 'add' action (e.g., 'manage_script', 'manage_asset')
            parameters: Parameters for the tool (required for 'add' action)
            operation_id: Operation ID for 'remove'/'cancel' actions
            status: Status filter for 'list' and 'clear' actions (pending, executing, executed, failed, timeout)
            limit: Maximum number of operations to return for 'list' action
            timeout_ms: Timeout in milliseconds for 'add' action (default: 30000)
            
        Returns:
            Dictionary with success status and operation results
        """
        try:
            # Build parameters for Unity
            params = {
                "action": action.lower()
            }
            
            # Add action-specific parameters
            if action.lower() == "add":
                if not tool:
                    return {
                        "success": False, 
                        "error": "Tool parameter is required for 'add' action",
                        "suggestion": "Specify tool name (e.g., 'manage_script', 'manage_asset')"
                    }
                if not parameters:
                    return {
                        "success": False, 
                        "error": "Parameters are required for 'add' action",
                        "suggestion": "Provide parameters object for the tool"
                    }
                params["tool"] = tool
                params["parameters"] = parameters
                if timeout_ms is not None:
                    params["timeout_ms"] = max(1000, timeout_ms)  # Minimum 1 second
                
            elif action.lower() in ["remove", "cancel"]:
                if not operation_id:
                    return {
                        "success": False, 
                        "error": f"Operation ID is required for '{action}' action",
                        "suggestion": "Use 'list' action to see available operation IDs"
                    }
                params["operation_id"] = operation_id
                
            elif action.lower() in ["list", "clear"]:
                if status:
                    params["status"] = status.lower()
                if action.lower() == "list" and limit is not None and limit > 0:
                    params["limit"] = limit

            # Send to Unity
            logger.debug(f"STUDIO: Sending queue command to Unity: {action}")
            response = send_command_with_retry("manage_queue", params)
            
            # Process response
            if isinstance(response, dict):
                if response.get("success"):
                    return {
                        "success": True,
                        "message": response.get("message", "Queue operation completed"),
                        "data": response.get("data")
                    }
                else:
                    return {
                        "success": False,
                        "error": response.get("error", "Queue operation failed"),
                        "details": response.get("error_details"),
                        "code": response.get("code")
                    }
            else:
                return {"success": False, "error": f"Unexpected response format: {response}"}
            
        except Exception as e:
            logger.error(f"STUDIO: Queue operation failed: {str(e)}")
            return {
                "success": False, 
                "error": f"Python error in manage_queue: {str(e)}",
                "suggestion": "Check Unity console for additional error details"
            }

    @mcp.tool(description=(
        "STUDIO: Quick helper to add multiple operations to the queue at once.\n\n"
        "This is a convenience function that adds multiple operations and optionally executes them.\n"
        "Each operation should be a dict with 'tool' and 'parameters' keys.\n"
        "Optional 'timeout_ms' can be added per operation or set globally.\n\n"
        "Example:\n"
        "operations=[\n"
        "  {'tool': 'manage_script', 'parameters': {'action': 'create', 'name': 'Player'}, 'timeout_ms': 15000},\n"
        "  {'tool': 'manage_asset', 'parameters': {'action': 'import', 'path': 'model.fbx'}}\n"
        "], execute_immediately=True, use_async=True"
    ))
    def queue_batch_operations(
        ctx: Context,
        operations: List[Dict[str, Any]],
        execute_immediately: bool = True,
        use_async: bool = False,
        default_timeout_ms: Optional[int] = None
    ) -> Dict[str, Any]:
        """
        Add multiple operations to the queue and optionally execute them.
        
        Args:
            ctx: The MCP context
            operations: List of operations, each with 'tool' and 'parameters' keys, optional 'timeout_ms'
            execute_immediately: Whether to execute the batch immediately after queuing
            use_async: Whether to use asynchronous execution (non-blocking)
            default_timeout_ms: Default timeout for operations that don't specify one
            
        Returns:
            Dictionary with batch results
        """
        try:
            if not operations or not isinstance(operations, list):
                return {
                    "success": False,
                    "error": "Operations parameter must be a non-empty list",
                    "suggestion": "Provide list of operations with 'tool' and 'parameters' keys"
                }
            
            # Add all operations to queue
            operation_ids = []
            for i, op in enumerate(operations):
                if not isinstance(op, dict) or 'tool' not in op or 'parameters' not in op:
                    return {
                        "success": False,
                        "error": f"Operation {i} is invalid - must have 'tool' and 'parameters' keys",
                        "suggestion": "Each operation should be: {'tool': 'tool_name', 'parameters': {...}}"
                    }
                
                # Add individual operation with timeout support
                timeout_ms = op.get('timeout_ms', default_timeout_ms)
                add_result = manage_queue(ctx, "add", op['tool'], op['parameters'], timeout_ms=timeout_ms)
                if not add_result.get("success"):
                    return {
                        "success": False,
                        "error": f"Failed to queue operation {i}: {add_result.get('error')}",
                        "failed_operation": op
                    }
                
                if add_result.get("data", {}).get("operation_id"):
                    operation_ids.append(add_result["data"]["operation_id"])
            
            logger.info(f"STUDIO: Queued {len(operation_ids)} operations: {operation_ids}")
            
            # Execute if requested
            if execute_immediately:
                execute_action = "execute_async" if use_async else "execute"
                execute_result = manage_queue(ctx, execute_action)
                execution_type = "async" if use_async else "sync"
                return {
                    "success": True,
                    "message": f"Queued and executed {len(operations)} operations ({execution_type})",
                    "data": {
                        "queued_operations": operation_ids,
                        "execution_result": execute_result.get("data"),
                        "execution_type": execution_type
                    }
                }
            else:
                return {
                    "success": True,
                    "message": f"Queued {len(operations)} operations",
                    "data": {
                        "queued_operations": operation_ids,
                        "execute_with": "manage_queue with action='execute'"
                    }
                }
                
        except Exception as e:
            logger.error(f"STUDIO: Batch queue operation failed: {str(e)}")
            return {
                "success": False,
                "error": f"Python error in queue_batch_operations: {str(e)}",
                "suggestion": "Check operation format and Unity connection"
            }