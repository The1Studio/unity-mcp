"""
Tests for Operation Queue functionality.
STUDIO: Comprehensive testing for operation queuing system.
"""

import pytest
import json
import asyncio
from unittest.mock import Mock, patch, MagicMock
import sys
import os

# Add the src directory to Python path for imports
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '../UnityMcpBridge/UnityMcpServer~/src'))

from tools.manage_queue import register_manage_queue
from mcp.server.fastmcp import FastMCP, Context

@pytest.fixture
def mcp_server():
    """Create MCP server with queue tools registered."""
    mcp = FastMCP("test-unity-mcp")
    register_manage_queue(mcp)
    return mcp

@pytest.fixture
def mock_context():
    """Create mock MCP context."""
    return Mock(spec=Context)

@pytest.fixture
def sample_operations():
    """Sample operations for testing."""
    return [
        {
            "tool": "manage_script",
            "parameters": {
                "action": "create",
                "name": "TestScript1",
                "path": "Assets/Scripts"
            }
        },
        {
            "tool": "manage_script", 
            "parameters": {
                "action": "create",
                "name": "TestScript2",
                "path": "Assets/Scripts"
            }
        },
        {
            "tool": "manage_asset",
            "parameters": {
                "action": "import",
                "path": "Assets/Models/test.fbx"
            }
        }
    ]

class TestManageQueue:
    """Test the manage_queue MCP tool."""
    
    @patch('tools.manage_queue.send_command_with_retry')
    def test_add_operation_success(self, mock_send, mcp_server, mock_context):
        """Test adding an operation to the queue."""
        # Mock Unity response
        mock_send.return_value = {
            "success": True,
            "message": "Operation queued successfully with ID: op_1",
            "data": {
                "operation_id": "op_1",
                "tool": "manage_script",
                "queued_at": "2025-01-20 15:30:45 UTC",
                "queue_stats": {"total_operations": 1, "pending": 1}
            }
        }
        
        # Get the manage_queue tool
        manage_queue = None
        for tool in mcp_server._tools.values():
            if tool.name == 'manage_queue':
                manage_queue = tool.fn
                break
        
        assert manage_queue is not None, "manage_queue tool not found"
        
        # Test adding operation
        result = manage_queue(
            ctx=mock_context,
            action="add",
            tool="manage_script",
            parameters={"action": "create", "name": "TestScript"}
        )
        
        # Verify result
        assert result["success"] is True
        assert "operation_id" in result["data"]
        assert result["data"]["operation_id"] == "op_1"
        
        # Verify Unity was called correctly
        mock_send.assert_called_once_with("manage_queue", {
            "action": "add",
            "tool": "manage_script", 
            "parameters": {"action": "create", "name": "TestScript"}
        })
    
    def test_add_operation_missing_tool(self, mcp_server, mock_context):
        """Test adding operation without tool parameter."""
        manage_queue = None
        for tool in mcp_server._tools.values():
            if tool.name == 'manage_queue':
                manage_queue = tool.fn
                break
        
        result = manage_queue(
            ctx=mock_context,
            action="add",
            parameters={"action": "create", "name": "TestScript"}
        )
        
        assert result["success"] is False
        assert "Tool parameter is required" in result["error"]
    
    def test_add_operation_missing_parameters(self, mcp_server, mock_context):
        """Test adding operation without parameters."""
        manage_queue = None
        for tool in mcp_server._tools.values():
            if tool.name == 'manage_queue':
                manage_queue = tool.fn
                break
        
        result = manage_queue(
            ctx=mock_context,
            action="add",
            tool="manage_script"
        )
        
        assert result["success"] is False
        assert "Parameters are required" in result["error"]
    
    @patch('tools.manage_queue.send_command_with_retry')
    def test_execute_batch_success(self, mock_send, mcp_server, mock_context):
        """Test executing batch operations."""
        mock_send.return_value = {
            "success": True,
            "message": "Batch executed: 2 successful, 0 failed",
            "data": {
                "total_operations": 2,
                "successful": 2,
                "failed": 0,
                "results": [
                    {"id": "op_1", "tool": "manage_script", "status": "success"},
                    {"id": "op_2", "tool": "manage_asset", "status": "success"}
                ]
            }
        }
        
        manage_queue = None
        for tool in mcp_server._tools.values():
            if tool.name == 'manage_queue':
                manage_queue = tool.fn
                break
        
        result = manage_queue(ctx=mock_context, action="execute")
        
        assert result["success"] is True
        assert result["data"]["successful"] == 2
        assert result["data"]["failed"] == 0
        
        mock_send.assert_called_once_with("manage_queue", {"action": "execute"})
    
    @patch('tools.manage_queue.send_command_with_retry')
    def test_list_operations(self, mock_send, mcp_server, mock_context):
        """Test listing operations in queue."""
        mock_send.return_value = {
            "success": True,
            "message": "Found 2 operations",
            "data": {
                "operations": [
                    {
                        "id": "op_1",
                        "tool": "manage_script", 
                        "status": "pending",
                        "queued_at": "2025-01-20 15:30:45 UTC"
                    },
                    {
                        "id": "op_2",
                        "tool": "manage_asset",
                        "status": "executed", 
                        "queued_at": "2025-01-20 15:31:00 UTC"
                    }
                ]
            }
        }
        
        manage_queue = None
        for tool in mcp_server._tools.values():
            if tool.name == 'manage_queue':
                manage_queue = tool.fn
                break
        
        result = manage_queue(ctx=mock_context, action="list", status="pending", limit=10)
        
        assert result["success"] is True
        assert len(result["data"]["operations"]) == 2
        
        mock_send.assert_called_once_with("manage_queue", {
            "action": "list",
            "status": "pending",
            "limit": 10
        })

class TestQueueBatchOperations:
    """Test the queue_batch_operations helper tool."""
    
    @patch('tools.manage_queue.send_command_with_retry')
    def test_batch_operations_success(self, mock_send, mcp_server, mock_context, sample_operations):
        """Test batch operations with execute_immediately=True."""
        # Mock responses for add operations
        add_responses = [
            {
                "success": True,
                "data": {"operation_id": f"op_{i+1}"}
            } for i in range(len(sample_operations))
        ]
        
        # Mock response for execute
        execute_response = {
            "success": True,
            "data": {
                "total_operations": 3,
                "successful": 3,
                "failed": 0
            }
        }
        
        # Configure mock to return different responses for different calls
        mock_send.side_effect = add_responses + [execute_response]
        
        queue_batch_operations = None
        for tool in mcp_server._tools.values():
            if tool.name == 'queue_batch_operations':
                queue_batch_operations = tool.fn
                break
        
        assert queue_batch_operations is not None, "queue_batch_operations tool not found"
        
        result = queue_batch_operations(
            ctx=mock_context,
            operations=sample_operations,
            execute_immediately=True
        )
        
        assert result["success"] is True
        assert "Queued and executed 3 operations" in result["message"]
        assert len(result["data"]["queued_operations"]) == 3
        
        # Verify all operations were added plus one execute call
        assert mock_send.call_count == 4  # 3 adds + 1 execute
    
    def test_batch_operations_invalid_operation(self, mcp_server, mock_context):
        """Test batch operations with invalid operation format."""
        invalid_operations = [
            {"tool": "manage_script"},  # Missing parameters
            {"parameters": {"action": "create"}}  # Missing tool
        ]
        
        queue_batch_operations = None
        for tool in mcp_server._tools.values():
            if tool.name == 'queue_batch_operations':
                queue_batch_operations = tool.fn
                break
        
        result = queue_batch_operations(
            ctx=mock_context,
            operations=invalid_operations,
            execute_immediately=False
        )
        
        assert result["success"] is False
        assert "must have 'tool' and 'parameters' keys" in result["error"]
    
    def test_batch_operations_empty_list(self, mcp_server, mock_context):
        """Test batch operations with empty operations list."""
        queue_batch_operations = None
        for tool in mcp_server._tools.values():
            if tool.name == 'queue_batch_operations':
                queue_batch_operations = tool.fn
                break
        
        result = queue_batch_operations(
            ctx=mock_context,
            operations=[],
            execute_immediately=False
        )
        
        assert result["success"] is False
        assert "must be a non-empty list" in result["error"]

class TestErrorHandling:
    """Test error handling in queue operations."""
    
    @patch('tools.manage_queue.send_command_with_retry')
    def test_unity_connection_error(self, mock_send, mcp_server, mock_context):
        """Test handling Unity connection errors."""
        mock_send.side_effect = Exception("Connection failed")
        
        manage_queue = None
        for tool in mcp_server._tools.values():
            if tool.name == 'manage_queue':
                manage_queue = tool.fn
                break
        
        result = manage_queue(ctx=mock_context, action="stats")
        
        assert result["success"] is False
        assert "Python error in manage_queue" in result["error"]
        assert "Connection failed" in result["error"]
    
    @patch('tools.manage_queue.send_command_with_retry')
    def test_unity_error_response(self, mock_send, mcp_server, mock_context):
        """Test handling Unity error responses."""
        mock_send.return_value = {
            "success": False,
            "error": "Queue system not initialized",
            "code": "QUEUE_NOT_INITIALIZED"
        }
        
        manage_queue = None
        for tool in mcp_server._tools.values():
            if tool.name == 'manage_queue':
                manage_queue = tool.fn
                break
        
        result = manage_queue(ctx=mock_context, action="execute")
        
        assert result["success"] is False
        assert result["error"] == "Queue system not initialized"
        assert result["code"] == "QUEUE_NOT_INITIALIZED"

class TestEdgeCases:
    """Test edge cases and boundary conditions."""
    
    @patch('tools.manage_queue.send_command_with_retry')
    def test_large_batch_operations(self, mock_send, mcp_server, mock_context):
        """Test handling large number of batch operations."""
        # Create 100 operations
        large_operations = [
            {
                "tool": "manage_script",
                "parameters": {
                    "action": "create", 
                    "name": f"Script_{i}",
                    "path": "Assets/Scripts"
                }
            } for i in range(100)
        ]
        
        # Mock successful responses
        mock_send.return_value = {"success": True, "data": {"operation_id": "op_1"}}
        
        queue_batch_operations = None
        for tool in mcp_server._tools.values():
            if tool.name == 'queue_batch_operations':
                queue_batch_operations = tool.fn
                break
        
        result = queue_batch_operations(
            ctx=mock_context,
            operations=large_operations,
            execute_immediately=False
        )
        
        assert result["success"] is True
        assert len(result["data"]["queued_operations"]) == 100
    
    def test_invalid_action_parameter(self, mcp_server, mock_context):
        """Test handling invalid action parameters."""
        manage_queue = None
        for tool in mcp_server._tools.values():
            if tool.name == 'manage_queue':
                manage_queue = tool.fn
                break
        
        result = manage_queue(ctx=mock_context, action="invalid_action")
        
        assert result["success"] is False
        # Should be handled by Unity, but let's ensure it doesn't crash

if __name__ == "__main__":
    pytest.main([__file__, "-v"])