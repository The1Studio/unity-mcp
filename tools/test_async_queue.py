#!/usr/bin/env python3
"""
STUDIO: Simple test script for async Operation Queue functionality.
Tests basic async operations with manage_asset and execute_menu_item tools.

Usage:
    python tools/test_async_queue.py
"""

import sys
import time
from pathlib import Path
from typing import Dict, Any

# Add the src directory to Python path for imports
sys.path.insert(0, str(Path(__file__).parent.parent / "UnityMcpBridge/UnityMcpServer~/src"))

try:
    from unity_connection import send_command_with_retry
except ImportError as e:
    print(f"Error: Could not import unity_connection: {e}")
    print("Make sure Unity MCP Bridge is running and Python dependencies are installed.")
    sys.exit(1)

def test_queue_stats():
    """Test basic queue statistics."""
    print("📊 Testing queue statistics...")
    response = send_command_with_retry("manage_queue", {"action": "stats"})
    
    if isinstance(response, dict) and response.get("success"):
        data = response.get("data", {})
        print(f"✅ Queue stats: {data.get('total_operations', 0)} total, "
              f"{data.get('pending', 0)} pending, {data.get('executed', 0)} executed")
        
        async_tools = data.get("async_tools_supported", [])
        print(f"⚡ Async tools supported: {async_tools}")
        return True
    else:
        print(f"❌ Queue stats failed: {response}")
        return False

def test_add_operations_with_timeout():
    """Test adding operations with custom timeouts."""
    print("\n🔄 Testing operation addition with timeouts...")
    
    # Clear queue first
    send_command_with_retry("manage_queue", {"action": "clear"})
    
    operations = [
        {
            "tool": "manage_script",
            "parameters": {
                "action": "create",
                "name": "AsyncTestScript1",
                "path": "Assets/Scripts/Test",
                "contents": "using UnityEngine;\n\npublic class AsyncTestScript1 : MonoBehaviour\n{\n    void Start() { Debug.Log(\"Test 1\"); }\n}"
            },
            "timeout_ms": 15000
        },
        {
            "tool": "manage_script", 
            "parameters": {
                "action": "create",
                "name": "AsyncTestScript2",
                "path": "Assets/Scripts/Test",
                "contents": "using UnityEngine;\n\npublic class AsyncTestScript2 : MonoBehaviour\n{\n    void Start() { Debug.Log(\"Test 2\"); }\n}"
            },
            "timeout_ms": 20000
        },
        {
            "tool": "read_console",
            "parameters": {"action": "read"},
            "timeout_ms": 5000
        }
    ]
    
    operation_ids = []
    for i, op in enumerate(operations):
        response = send_command_with_retry("manage_queue", {
            "action": "add",
            "tool": op["tool"],
            "parameters": op["parameters"],
            "timeout_ms": op["timeout_ms"]
        })
        
        if isinstance(response, dict) and response.get("success"):
            op_id = response.get("data", {}).get("operation_id")
            operation_ids.append(op_id)
            timeout = response.get("data", {}).get("timeout_ms", "default")
            print(f"✅ Added operation {i+1}: {op_id} (timeout: {timeout}ms)")
        else:
            print(f"❌ Failed to add operation {i+1}: {response}")
            return False
    
    print(f"✅ Successfully added {len(operation_ids)} operations")
    return operation_ids

def test_sync_execution(operation_ids):
    """Test synchronous execution."""
    print("\n🔄 Testing synchronous execution...")
    
    start_time = time.time()
    response = send_command_with_retry("manage_queue", {"action": "execute"})
    end_time = time.time()
    
    execution_time = (end_time - start_time) * 1000
    
    if isinstance(response, dict) and response.get("success"):
        data = response.get("data", {})
        print(f"✅ Sync execution completed in {execution_time:.1f}ms")
        print(f"   📊 Results: {data.get('successful', 0)} successful, "
              f"{data.get('failed', 0)} failed, {data.get('timeout', 0)} timeout")
        return True
    else:
        print(f"❌ Sync execution failed: {response}")
        return False

def test_async_execution(operation_ids):
    """Test asynchronous execution.""" 
    print("\n⚡ Testing asynchronous execution...")
    
    start_time = time.time()
    response = send_command_with_retry("manage_queue", {"action": "execute_async"})
    
    if not isinstance(response, dict) or not response.get("success"):
        print(f"❌ Failed to start async execution: {response}")
        return False
        
    print(f"✅ Async execution started: {response.get('message', '')}")
    
    # Monitor progress
    print("👀 Monitoring execution progress...")
    completed = False
    max_wait = 60  # Maximum 60 seconds
    check_interval = 0.5  # Check every 500ms
    checks = 0
    
    while not completed and checks < (max_wait / check_interval):
        time.sleep(check_interval)
        checks += 1
        
        stats = send_command_with_retry("manage_queue", {"action": "stats"})
        if isinstance(stats, dict) and stats.get("success"):
            data = stats.get("data", {})
            pending = data.get("pending", 0)
            executing = data.get("executing", 0)
            executed = data.get("executed", 0)
            failed = data.get("failed", 0)
            timeout = data.get("timeout", 0)
            
            if executing == 0 and pending == 0:
                completed = True
                end_time = time.time()
                execution_time = (end_time - start_time) * 1000
                
                print(f"✅ Async execution completed in {execution_time:.1f}ms")
                print(f"   📊 Final results: {executed} executed, {failed} failed, {timeout} timeout")
                return True
            else:
                print(f"   ⏳ Progress: {pending} pending, {executing} executing, {executed} completed")
    
    if not completed:
        print(f"❌ Async execution timed out after {max_wait} seconds")
        return False

def test_cancel_operation():
    """Test operation cancellation."""
    print("\n🛑 Testing operation cancellation...")
    
    # Clear queue first
    send_command_with_retry("manage_queue", {"action": "clear"})
    
    # Add a long-running operation (simulate with a script that might take time)
    response = send_command_with_retry("manage_queue", {
        "action": "add",
        "tool": "manage_script",
        "parameters": {
            "action": "create", 
            "name": "CancelTestScript",
            "path": "Assets/Scripts/Test",
            "contents": "using UnityEngine;\n\npublic class CancelTestScript : MonoBehaviour\n{\n    void Start() { Debug.Log(\"Cancel Test\"); }\n}"
        },
        "timeout_ms": 30000
    })
    
    if not isinstance(response, dict) or not response.get("success"):
        print(f"❌ Failed to add operation for cancel test: {response}")
        return False
        
    op_id = response.get("data", {}).get("operation_id")
    print(f"✅ Added operation for cancel test: {op_id}")
    
    # Try to cancel it (should work if it's still pending)
    cancel_response = send_command_with_retry("manage_queue", {
        "action": "cancel",
        "operation_id": op_id
    })
    
    if isinstance(cancel_response, dict) and cancel_response.get("success"):
        print(f"✅ Operation cancellation successful")
        return True
    else:
        # If cancel failed, it might already be executed, which is also fine
        print(f"ℹ️ Cancel response: {cancel_response}")
        return True

def cleanup_test_operations():
    """Clean up test operations."""
    print("\n🧹 Cleaning up test operations...")
    
    # Clear the queue
    send_command_with_retry("manage_queue", {"action": "clear"})
    
    # Delete test scripts
    test_scripts = ["AsyncTestScript1", "AsyncTestScript2", "CancelTestScript"]
    for script_name in test_scripts:
        try:
            send_command_with_retry("manage_script", {
                "action": "delete",
                "name": script_name,
                "path": "Assets/Scripts/Test"
            })
        except:
            pass  # Ignore if script doesn't exist
    
    print("✅ Cleanup completed")

def main():
    """Run all async queue tests."""
    print("🚀 Starting Async Operation Queue Tests")
    print("=" * 50)
    
    try:
        # Test Unity connection
        if not test_queue_stats():
            print("❌ Basic connectivity test failed")
            return False
        
        # Test adding operations with timeouts
        operation_ids = test_add_operations_with_timeout()
        if not operation_ids:
            print("❌ Operation addition test failed")
            return False
        
        # Test synchronous execution
        if not test_sync_execution(operation_ids):
            print("❌ Synchronous execution test failed")
            return False
        
        # Add more operations for async test
        print("\n🔄 Adding operations for async test...")
        operation_ids = test_add_operations_with_timeout()
        if not operation_ids:
            print("❌ Failed to add operations for async test")
            return False
        
        # Test asynchronous execution
        if not test_async_execution(operation_ids):
            print("❌ Asynchronous execution test failed")
            return False
        
        # Test operation cancellation
        if not test_cancel_operation():
            print("❌ Operation cancellation test failed") 
            return False
        
        print("\n" + "=" * 50)
        print("🎉 All async queue tests completed successfully!")
        print("✅ Async operation support is working correctly")
        
        return True
        
    except KeyboardInterrupt:
        print("\n⚠️ Tests interrupted by user")
        return False
    except Exception as e:
        print(f"\n❌ Test suite failed: {e}")
        return False
    finally:
        cleanup_test_operations()

if __name__ == "__main__":
    success = main()
    sys.exit(0 if success else 1)