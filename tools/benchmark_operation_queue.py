#!/usr/bin/env python3
"""
STUDIO: Performance benchmarking tool for Operation Queue system.
Tests synchronous vs asynchronous execution performance and validates the claimed "3x faster" improvement.

Requirements:
- Unity Editor with MCP Bridge running
- Python 3.10+ with required dependencies
- Test Unity project with sample assets

Usage:
    python tools/benchmark_operation_queue.py --operations 10 --runs 3
    python tools/benchmark_operation_queue.py --operations 50 --runs 5 --async-only
"""

import asyncio
import argparse
import json
import statistics
import sys
import time
from pathlib import Path
from typing import List, Dict, Any
from dataclasses import dataclass
from datetime import datetime

# Add the src directory to Python path for imports
sys.path.insert(0, str(Path(__file__).parent.parent / "UnityMcpBridge/UnityMcpServer~/src"))

try:
    from unity_connection import send_command_with_retry
except ImportError as e:
    print(f"Error: Could not import unity_connection: {e}")
    print("Make sure Unity MCP Bridge is running and Python dependencies are installed.")
    sys.exit(1)

@dataclass
class BenchmarkResult:
    """Results from a single benchmark run."""
    operation_count: int
    execution_time_ms: float
    successful_operations: int
    failed_operations: int
    timeout_operations: int
    method: str  # "individual", "queue_sync", "queue_async"
    operations_per_second: float
    memory_usage_mb: float = 0.0

class OperationQueueBenchmark:
    """Benchmark suite for Operation Queue performance testing."""
    
    def __init__(self):
        self.test_operations = [
            {
                "tool": "manage_script",
                "parameters": {
                    "action": "create",
                    "name": f"BenchmarkScript_{i:03d}",
                    "path": "Assets/Scripts/Benchmark",
                    "contents": self._generate_test_script_content(f"BenchmarkScript_{i:03d}")
                }
            }
            for i in range(100)  # Generate 100 test operations
        ]
    
    def _generate_test_script_content(self, class_name: str) -> str:
        """Generate a simple test script content."""
        return f'''using UnityEngine;

public class {class_name} : MonoBehaviour
{{
    public float speed = 5.0f;
    public Vector3 direction = Vector3.forward;
    
    void Start()
    {{
        Debug.Log("{class_name} initialized");
    }}
    
    void Update()
    {{
        transform.Translate(direction * speed * Time.deltaTime);
    }}
}}
'''

    def cleanup_test_scripts(self):
        """Clean up test scripts created during benchmarking."""
        print("🧹 Cleaning up test scripts...")
        try:
            # Clear the queue first
            response = send_command_with_retry("manage_queue", {"action": "clear"})
            
            # Delete benchmark scripts
            for i in range(100):
                script_name = f"BenchmarkScript_{i:03d}"
                try:
                    send_command_with_retry("manage_script", {
                        "action": "delete",
                        "name": script_name,
                        "path": "Assets/Scripts/Benchmark"
                    })
                except:
                    pass  # Ignore errors for scripts that don't exist
                    
            print("✅ Cleanup completed")
        except Exception as e:
            print(f"⚠️ Cleanup failed: {e}")

    def benchmark_individual_operations(self, operation_count: int) -> BenchmarkResult:
        """Benchmark individual operation execution (baseline)."""
        print(f"📊 Running individual operations benchmark ({operation_count} operations)...")
        
        start_time = time.time()
        successful = 0
        failed = 0
        
        for i in range(operation_count):
            operation = self.test_operations[i % len(self.test_operations)]
            try:
                response = send_command_with_retry(operation["tool"], operation["parameters"])
                if isinstance(response, dict) and response.get("success"):
                    successful += 1
                else:
                    failed += 1
            except Exception as e:
                print(f"Operation {i} failed: {e}")
                failed += 1
        
        end_time = time.time()
        execution_time_ms = (end_time - start_time) * 1000
        ops_per_second = operation_count / (execution_time_ms / 1000) if execution_time_ms > 0 else 0
        
        return BenchmarkResult(
            operation_count=operation_count,
            execution_time_ms=execution_time_ms,
            successful_operations=successful,
            failed_operations=failed,
            timeout_operations=0,
            method="individual",
            operations_per_second=ops_per_second
        )

    def benchmark_queue_sync(self, operation_count: int) -> BenchmarkResult:
        """Benchmark synchronous queue execution."""
        print(f"📊 Running synchronous queue benchmark ({operation_count} operations)...")
        
        # Clear queue first
        send_command_with_retry("manage_queue", {"action": "clear"})
        
        # Add operations to queue
        start_time = time.time()
        for i in range(operation_count):
            operation = self.test_operations[i % len(self.test_operations)]
            send_command_with_retry("manage_queue", {
                "action": "add",
                "tool": operation["tool"],
                "parameters": operation["parameters"],
                "timeout_ms": 30000
            })
        
        # Execute batch synchronously
        response = send_command_with_retry("manage_queue", {"action": "execute"})
        end_time = time.time()
        
        execution_time_ms = (end_time - start_time) * 1000
        ops_per_second = operation_count / (execution_time_ms / 1000) if execution_time_ms > 0 else 0
        
        # Extract results
        data = response.get("data", {}) if isinstance(response, dict) else {}
        successful = data.get("successful", 0)
        failed = data.get("failed", 0)
        timeout = data.get("timeout", 0)
        
        return BenchmarkResult(
            operation_count=operation_count,
            execution_time_ms=execution_time_ms,
            successful_operations=successful,
            failed_operations=failed,
            timeout_operations=timeout,
            method="queue_sync",
            operations_per_second=ops_per_second
        )

    def benchmark_queue_async(self, operation_count: int) -> BenchmarkResult:
        """Benchmark asynchronous queue execution."""
        print(f"📊 Running asynchronous queue benchmark ({operation_count} operations)...")
        
        # Clear queue first
        send_command_with_retry("manage_queue", {"action": "clear"})
        
        # Add operations to queue
        start_time = time.time()
        for i in range(operation_count):
            operation = self.test_operations[i % len(self.test_operations)]
            send_command_with_retry("manage_queue", {
                "action": "add",
                "tool": operation["tool"],
                "parameters": operation["parameters"],
                "timeout_ms": 30000
            })
        
        # Execute batch asynchronously and monitor progress
        send_command_with_retry("manage_queue", {"action": "execute_async"})
        
        # Poll for completion
        while True:
            stats = send_command_with_retry("manage_queue", {"action": "stats"})
            if isinstance(stats, dict) and stats.get("success"):
                data = stats.get("data", {})
                executing = data.get("executing", 0)
                pending = data.get("pending", 0)
                
                if executing == 0 and pending == 0:
                    break
            time.sleep(0.1)  # Poll every 100ms
        
        end_time = time.time()
        
        execution_time_ms = (end_time - start_time) * 1000
        ops_per_second = operation_count / (execution_time_ms / 1000) if execution_time_ms > 0 else 0
        
        # Get final stats
        final_stats = send_command_with_retry("manage_queue", {"action": "stats"})
        data = final_stats.get("data", {}) if isinstance(final_stats, dict) else {}
        successful = data.get("executed", 0)
        failed = data.get("failed", 0)
        timeout = data.get("timeout", 0)
        
        return BenchmarkResult(
            operation_count=operation_count,
            execution_time_ms=execution_time_ms,
            successful_operations=successful,
            failed_operations=failed,
            timeout_operations=timeout,
            method="queue_async",
            operations_per_second=ops_per_second
        )

    def run_benchmark_suite(self, operation_counts: List[int], runs_per_test: int, async_only: bool = False) -> Dict[str, Any]:
        """Run complete benchmark suite."""
        print(f"🚀 Starting Operation Queue Benchmark Suite")
        print(f"📋 Test configurations: {operation_counts} operations")
        print(f"🔄 Runs per test: {runs_per_test}")
        print(f"⚡ Async only: {async_only}")
        print("-" * 50)
        
        results = {
            "timestamp": datetime.now().isoformat(),
            "configuration": {
                "operation_counts": operation_counts,
                "runs_per_test": runs_per_test,
                "async_only": async_only
            },
            "results": {}
        }
        
        for op_count in operation_counts:
            print(f"\n🎯 Testing with {op_count} operations...")
            results["results"][op_count] = {}
            
            methods = ["queue_async"] if async_only else ["individual", "queue_sync", "queue_async"]
            
            for method in methods:
                print(f"\n🔄 Method: {method}")
                method_results = []
                
                for run in range(runs_per_test):
                    print(f"  Run {run + 1}/{runs_per_test}...", end=" ")
                    
                    try:
                        if method == "individual":
                            result = self.benchmark_individual_operations(op_count)
                        elif method == "queue_sync":
                            result = self.benchmark_queue_sync(op_count)
                        elif method == "queue_async":
                            result = self.benchmark_queue_async(op_count)
                        
                        method_results.append(result)
                        print(f"✅ {result.execution_time_ms:.1f}ms ({result.operations_per_second:.1f} ops/s)")
                        
                        # Cleanup between runs
                        self.cleanup_test_scripts()
                        time.sleep(1)  # Brief pause between runs
                        
                    except Exception as e:
                        print(f"❌ Failed: {e}")
                        continue
                
                if method_results:
                    # Calculate statistics
                    execution_times = [r.execution_time_ms for r in method_results]
                    ops_per_sec = [r.operations_per_second for r in method_results]
                    
                    results["results"][op_count][method] = {
                        "runs": len(method_results),
                        "execution_time_ms": {
                            "mean": statistics.mean(execution_times),
                            "median": statistics.median(execution_times),
                            "stdev": statistics.stdev(execution_times) if len(execution_times) > 1 else 0,
                            "min": min(execution_times),
                            "max": max(execution_times)
                        },
                        "operations_per_second": {
                            "mean": statistics.mean(ops_per_sec),
                            "median": statistics.median(ops_per_sec),
                            "stdev": statistics.stdev(ops_per_sec) if len(ops_per_sec) > 1 else 0,
                            "min": min(ops_per_sec),
                            "max": max(ops_per_sec)
                        },
                        "success_rate": sum(r.successful_operations for r in method_results) / sum(r.operation_count for r in method_results),
                        "raw_results": [
                            {
                                "execution_time_ms": r.execution_time_ms,
                                "operations_per_second": r.operations_per_second,
                                "successful": r.successful_operations,
                                "failed": r.failed_operations,
                                "timeout": r.timeout_operations
                            } for r in method_results
                        ]
                    }
        
        self.print_summary(results)
        return results

    def print_summary(self, results: Dict[str, Any]):
        """Print benchmark summary."""
        print("\n" + "=" * 60)
        print("📊 BENCHMARK SUMMARY")
        print("=" * 60)
        
        for op_count, methods in results["results"].items():
            print(f"\n🎯 {op_count} Operations:")
            print("-" * 40)
            
            for method, data in methods.items():
                mean_time = data["execution_time_ms"]["mean"]
                mean_ops = data["operations_per_second"]["mean"]
                success_rate = data["success_rate"] * 100
                
                print(f"  {method:15} | {mean_time:8.1f}ms | {mean_ops:6.1f} ops/s | {success_rate:5.1f}% success")
            
            # Calculate speedup if we have baseline
            if "individual" in methods and len(methods) > 1:
                baseline_time = methods["individual"]["execution_time_ms"]["mean"]
                print(f"\n  📈 Speedup vs Individual:")
                for method, data in methods.items():
                    if method != "individual":
                        speedup = baseline_time / data["execution_time_ms"]["mean"]
                        print(f"     {method:12} | {speedup:.2f}x faster")

def main():
    parser = argparse.ArgumentParser(description="Benchmark Operation Queue performance")
    parser.add_argument("--operations", type=int, nargs="+", default=[10, 25, 50], 
                       help="Number of operations to test (default: 10 25 50)")
    parser.add_argument("--runs", type=int, default=3,
                       help="Number of runs per test (default: 3)")
    parser.add_argument("--async-only", action="store_true",
                       help="Only test async operations (skip individual and sync)")
    parser.add_argument("--output", type=str, help="Save results to JSON file")
    parser.add_argument("--cleanup", action="store_true", help="Only run cleanup")
    
    args = parser.parse_args()
    
    benchmark = OperationQueueBenchmark()
    
    if args.cleanup:
        benchmark.cleanup_test_scripts()
        return
    
    try:
        # Test Unity connection
        print("🔍 Testing Unity connection...")
        response = send_command_with_retry("manage_queue", {"action": "stats"})
        if not isinstance(response, dict) or not response.get("success"):
            print("❌ Unity connection failed. Make sure Unity Editor with MCP Bridge is running.")
            sys.exit(1)
        print("✅ Unity connection successful")
        
        # Run benchmarks
        results = benchmark.run_benchmark_suite(
            operation_counts=args.operations,
            runs_per_test=args.runs,
            async_only=args.async_only
        )
        
        # Save results if requested
        if args.output:
            with open(args.output, 'w') as f:
                json.dump(results, f, indent=2)
            print(f"\n💾 Results saved to {args.output}")
        
    except KeyboardInterrupt:
        print("\n\n⚠️ Benchmark interrupted by user")
    except Exception as e:
        print(f"\n❌ Benchmark failed: {e}")
    finally:
        # Always cleanup
        print("\n🧹 Final cleanup...")
        benchmark.cleanup_test_scripts()

if __name__ == "__main__":
    main()