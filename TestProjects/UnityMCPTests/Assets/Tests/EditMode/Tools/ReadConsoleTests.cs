using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using MCPForUnity.Editor.Tools;
using static MCPForUnityTests.Editor.TestUtilities;

namespace MCPForUnityTests.Editor.Tools
{
    public class ReadConsoleTests
    {
        // --- Bee/Tundra build-failure surfacing (kit issue #53) ---

        private static string TundraLogPath =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "Bee", "tundra.log.json");

        private string _preexistingTundraLogBackup;

        [SetUp]
        public void SaveExistingTundraLog()
        {
            // Some CI/dev machines may already have a real tundra.log.json from an actual
            // build. Back it up so these tests never leak fixture data into the real log
            // or lose the developer's genuine build state.
            _preexistingTundraLogBackup = null;
            if (File.Exists(TundraLogPath))
            {
                _preexistingTundraLogBackup = TundraLogPath + ".test-backup";
                File.Copy(TundraLogPath, _preexistingTundraLogBackup, overwrite: true);
            }
        }

        [TearDown]
        public void RestoreExistingTundraLog()
        {
            if (_preexistingTundraLogBackup != null && File.Exists(_preexistingTundraLogBackup))
            {
                File.Copy(_preexistingTundraLogBackup, TundraLogPath, overwrite: true);
                File.Delete(_preexistingTundraLogBackup);
            }
            else if (File.Exists(TundraLogPath))
            {
                File.Delete(TundraLogPath);
            }
        }

        private static void WriteTundraLog(string ndjson)
        {
            string dir = Path.GetDirectoryName(TundraLogPath);
            Directory.CreateDirectory(dir);
            File.WriteAllText(TundraLogPath, ndjson);
            File.SetLastWriteTimeUtc(TundraLogPath, DateTime.UtcNow);
        }

        [Test]
        public void HandleCommand_Get_SurfacesTundraBuildFailure()
        {
            // Arrange — a build node with a non-zero exitcode, as produced by a real compile
            // failure (e.g. CS1061) in an editor-test assembly.
            string uniqueMarker = $"TundraFailureMarker_{Guid.NewGuid():N}";
            string ndjson =
                "{\"msg\":\"noderesult\",\"annotation\":\"Csc " + uniqueMarker + ".dll\",\"index\":1,\"exitcode\":0,\"outputfile\":\"Library/ScriptAssemblies/Ok.dll\",\"stdout\":\"\"}\n" +
                "{\"msg\":\"noderesult\",\"annotation\":\"Csc " + uniqueMarker + ".dll\",\"index\":2,\"exitcode\":1,\"outputfile\":\"Library/ScriptAssemblies/" + uniqueMarker + ".dll\",\"stdout\":\"Assets/Tests/Broken.cs(10,20): error CS1061: " + uniqueMarker + "\"}\n";
            WriteTundraLog(ndjson);

            var paramsObj = new JObject
            {
                ["action"] = "get",
                ["types"] = new JArray { "error" },
                ["format"] = "detailed",
                ["count"] = 1000,
            };

            // Act
            var result = ToJObject(ReadConsole.HandleCommand(paramsObj));

            // Assert
            Assert.IsTrue(result.Value<bool>("success"), result.ToString());
            var data = result["data"] as JArray;
            Assert.IsNotNull(data, "Data array should not be null.");

            bool found = false;
            foreach (var entry in data)
            {
                if (entry["message"]?.ToString().Contains(uniqueMarker) == true)
                {
                    Assert.AreEqual("Error", entry["type"]?.ToString(), "Build-failure entry should be typed as Error.");
                    Assert.AreEqual("BeeBuildLog", entry["source"]?.ToString(), "Build-failure entry should carry a distinct source so callers can tell it apart from a real console entry.");
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(
                found,
                $"read_console(types=[\"error\"]) did not surface the failed Bee build node — this is the false-green regression from issue #53."
            );
        }

        [Test]
        public void HandleCommand_Get_CleanTundraLog_ProducesNoBuildFailureEntries()
        {
            // Arrange — every node succeeded (exitcode 0); nothing should be synthesized.
            string uniqueMarker = $"TundraCleanMarker_{Guid.NewGuid():N}";
            string ndjson =
                "{\"msg\":\"noderesult\",\"annotation\":\"Csc " + uniqueMarker + ".dll\",\"index\":1,\"exitcode\":0,\"outputfile\":\"Library/ScriptAssemblies/" + uniqueMarker + ".dll\",\"stdout\":\"\"}\n";
            WriteTundraLog(ndjson);

            var paramsObj = new JObject
            {
                ["action"] = "get",
                ["types"] = new JArray { "error" },
                ["format"] = "detailed",
                ["count"] = 1000,
            };

            // Act
            var result = ToJObject(ReadConsole.HandleCommand(paramsObj));

            // Assert
            Assert.IsTrue(result.Value<bool>("success"), result.ToString());
            var data = result["data"] as JArray ?? new JArray();

            bool foundSpurious = false;
            foreach (var entry in data)
            {
                if (entry["message"]?.ToString().Contains(uniqueMarker) == true)
                {
                    foundSpurious = true;
                    break;
                }
            }

            Assert.IsFalse(foundSpurious, "A clean tundra.log.json (all exitcode 0) must not synthesize any build-failure entries.");
        }

        [Test]
        public void HandleCommand_Get_StaleTundraLog_IsIgnored()
        {
            // Arrange — a failed node, but the log file itself is old (simulating a leftover
            // from a previous session). Must not be resurrected as a "current" error.
            string uniqueMarker = $"TundraStaleMarker_{Guid.NewGuid():N}";
            string ndjson =
                "{\"msg\":\"noderesult\",\"annotation\":\"Csc " + uniqueMarker + ".dll\",\"index\":1,\"exitcode\":1,\"outputfile\":\"Library/ScriptAssemblies/" + uniqueMarker + ".dll\",\"stdout\":\"error CS1061: " + uniqueMarker + "\"}\n";
            WriteTundraLog(ndjson);
            File.SetLastWriteTimeUtc(TundraLogPath, DateTime.UtcNow.AddHours(-2));

            var paramsObj = new JObject
            {
                ["action"] = "get",
                ["types"] = new JArray { "error" },
                ["format"] = "detailed",
                ["count"] = 1000,
            };

            // Act
            var result = ToJObject(ReadConsole.HandleCommand(paramsObj));

            // Assert
            Assert.IsTrue(result.Value<bool>("success"), result.ToString());
            var data = result["data"] as JArray ?? new JArray();

            bool foundStale = false;
            foreach (var entry in data)
            {
                if (entry["message"]?.ToString().Contains(uniqueMarker) == true)
                {
                    foundStale = true;
                    break;
                }
            }

            Assert.IsFalse(foundStale, "An old tundra.log.json outside the staleness window must not be resurrected as a current error.");
        }

        [Test]
        public void HandleCommand_Clear_Works()
        {
            // Arrange
            // Ensure there's something to clear
            Debug.Log("Log to clear");
            
            // Verify content exists before clear
            var getBefore = ToJObject(ReadConsole.HandleCommand(new JObject { ["action"] = "get", ["types"] = new JArray { "error", "warning", "log" }, ["count"] = 10 }));
            Assert.IsTrue(getBefore.Value<bool>("success"), getBefore.ToString());
            var entriesBefore = getBefore["data"] as JArray;
            
            // Ideally we'd assert count > 0, but other tests/system logs might affect this.
            // Just ensuring the call doesn't fail is a baseline, but let's try to be stricter if possible.
            // Since we just logged, there should be at least one entry.
            Assert.IsTrue(entriesBefore != null && entriesBefore.Count > 0, "Setup failed: console should have logs.");

            // Act
            var result = ToJObject(ReadConsole.HandleCommand(new JObject { ["action"] = "clear" }));

            // Assert
            Assert.IsTrue(result.Value<bool>("success"), result.ToString());
            
            // Verify clear effect
            var getAfter = ToJObject(ReadConsole.HandleCommand(new JObject { ["action"] = "get", ["types"] = new JArray { "error", "warning", "log" }, ["count"] = 10 }));
            Assert.IsTrue(getAfter.Value<bool>("success"), getAfter.ToString());
            var entriesAfter = getAfter["data"] as JArray;
            Assert.IsTrue(entriesAfter == null || entriesAfter.Count == 0, "Console should be empty after clear.");
        }

        [Test]
        public void HandleCommand_Get_Works()
        {
            // Arrange
            string uniqueMessage = $"Test Log Message {Guid.NewGuid()}";
            Debug.Log(uniqueMessage);
            
            var paramsObj = new JObject
            {
                ["action"] = "get",
                ["types"] = new JArray { "error", "warning", "log" },
                ["format"] = "detailed",
                ["count"] = 1000 // Fetch enough to likely catch our message
            };

            // Act
            var result = ToJObject(ReadConsole.HandleCommand(paramsObj));

            // Assert
            Assert.IsTrue(result.Value<bool>("success"), result.ToString());
            var data = result["data"] as JArray;
            Assert.IsNotNull(data, "Data array should not be null.");
            Assert.IsTrue(data.Count > 0, "Should retrieve at least one log entry.");

            // Verify content
            bool found = false;
            foreach (var entry in data)
            {
                if (entry["message"]?.ToString().Contains(uniqueMessage) == true)
                {
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, $"The unique log message '{uniqueMessage}' was not found in retrieved logs.");
        }
    }
}
