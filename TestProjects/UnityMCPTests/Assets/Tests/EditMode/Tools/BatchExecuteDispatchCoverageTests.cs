using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Tools;

namespace MCPForUnityTests.Editor.Tools
{
    /// <summary>
    /// Regression coverage for issue #20: batch_execute must route every registered command
    /// type through the same handler that direct MCP calls use, instead of a hand-maintained
    /// subset. This walks the live tool registry (the same reflection-based discovery
    /// batch_execute itself relies on via CommandRegistry) so a newly-added tool is covered
    /// automatically — no per-tool test entry required.
    /// </summary>
    public class BatchExecuteDispatchCoverageTests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            CommandRegistry.Initialize();
        }

        [Test]
        public void EveryRegisteredTool_IsDispatchableThroughBatchExecute()
        {
            IToolDiscoveryService discovery = new ToolDiscoveryService();
            List<string> toolNames = discovery.DiscoverAllTools()
                .Select(t => t.Name)
                // batch_execute dispatching itself is out of scope for this coverage check.
                .Where(name => name != "batch_execute")
                .ToList();

            Assert.IsNotEmpty(toolNames, "Tool discovery should find at least one registered tool.");

            var commands = new JArray();
            foreach (var name in toolNames)
            {
                commands.Add(new JObject
                {
                    ["tool"] = name,
                    // Intentionally minimal/empty params: this test only proves the command
                    // *reaches* its handler, not that a no-arg call succeeds semantically.
                    ["params"] = new JObject()
                });
            }

            var batchParams = new JObject { ["commands"] = commands };

            var maxCommands = BatchExecute.GetMaxCommandsPerBatch();
            if (commands.Count > maxCommands)
            {
                // Stay under the configured per-batch ceiling by chunking instead of tripping
                // the "too many commands" guard rail.
                for (int i = 0; i < toolNames.Count; i += maxCommands)
                {
                    var chunk = new JArray(toolNames.Skip(i).Take(maxCommands)
                        .Select(name => (JToken)new JObject
                        {
                            ["tool"] = name,
                            ["params"] = new JObject()
                        }));
                    AssertChunkDispatches(new JObject { ["commands"] = chunk });
                }
            }
            else
            {
                AssertChunkDispatches(batchParams);
            }
        }

        private static void AssertChunkDispatches(JObject batchParams)
        {
            var result = BatchExecute.HandleCommand(batchParams).GetAwaiter().GetResult();
            var resultObj = JObject.FromObject(result);
            var results = (JArray)resultObj["data"]["results"];

            foreach (var entry in results)
            {
                string tool = entry.Value<string>("tool");

                // A tool may legitimately fail on empty params (missing required "action", no
                // scene selection, disabled optional package, etc.) — that's fine. What must
                // never happen is the dispatcher not knowing the command at all.
                string error = entry["error"]?.Value<string>()
                    ?? entry["result"]?["error"]?.Value<string>();

                Assert.IsFalse(
                    error != null && error.Contains("Unknown or unsupported command type"),
                    $"'{tool}' should be dispatchable through batch_execute, got: {error}");
            }
        }
    }
}
