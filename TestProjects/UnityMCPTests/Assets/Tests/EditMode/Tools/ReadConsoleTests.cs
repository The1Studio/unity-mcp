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
        private bool _hadPreexistingTundraLog;

        [SetUp]
        public void SaveExistingTundraLog()
        {
            // Some CI/dev machines may already have a real tundra.log.json from an actual
            // build. Back it up so these tests never leak fixture data into the real log
            // or lose the developer's genuine build state.
            _preexistingTundraLogBackup = null;
            _hadPreexistingTundraLog = File.Exists(TundraLogPath);
            if (_hadPreexistingTundraLog)
            {
                string backupPath = TundraLogPath + ".test-backup";
                File.Copy(TundraLogPath, backupPath, overwrite: true);
                // Only record the backup path once the copy has actually succeeded. If
                // File.Copy throws (e.g. Bee holding the log mid-build — plausible, since
                // EditMode tests run right after a compile), _preexistingTundraLogBackup
                // must stay null so TearDown's _hadPreexistingTundraLog guard below can
                // tell "no real log ever existed" apart from "a real log existed but its
                // backup is unconfirmed" — and never delete the latter.
                _preexistingTundraLogBackup = backupPath;
            }
        }

        [TearDown]
        public void RestoreExistingTundraLog()
        {
            try
            {
                if (_preexistingTundraLogBackup != null && File.Exists(_preexistingTundraLogBackup))
                {
                    File.Copy(_preexistingTundraLogBackup, TundraLogPath, overwrite: true);
                    File.Delete(_preexistingTundraLogBackup);
                }
                else if (!_hadPreexistingTundraLog && File.Exists(TundraLogPath))
                {
                    // No real pre-existing log was ever present — safe to remove the
                    // fixture file this test wrote.
                    File.Delete(TundraLogPath);
                }
                // else: a real log existed but its backup could not be confirmed (SetUp's
                // File.Copy most likely threw) — leave the file untouched rather than risk
                // deleting a developer's genuine build log.
            }
            finally
            {
                _preexistingTundraLogBackup = null;
                _hadPreexistingTundraLog = false;
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
        public void HandleCommand_Get_OldMtimeTundraLog_IsStillSurfaced()
        {
            // Arrange — a failed node whose log file has an old mtime (e.g. a build that
            // failed hours/days ago and was never re-run). Bee truncates and rewrites
            // tundra.log.json at the START of every build (confirmed against real project
            // logs — each contains exactly one {"msg":"init"...} line, always line 1), so the
            // file always describes exactly one build session: the most recent one. There is
            // no cross-session contamination for an mtime guard to defend against, so an old
            // mtime must NOT hide a live, still-unresolved failure (this was the issue #53
            // regression: a wall-clock staleness window silently deleted true positives).
            string uniqueMarker = $"TundraOldMtimeMarker_{Guid.NewGuid():N}";
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

            bool found = false;
            foreach (var entry in data)
            {
                if (entry["message"]?.ToString().Contains(uniqueMarker) == true)
                {
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found, "A live build failure must be surfaced regardless of the log file's mtime — Bee owns truncation/rewrite, not this tool.");
        }

        [Test]
        public void HandleCommand_Get_DefaultCallShape_SurfacesActualCompilerDiagnostic()
        {
            // Arrange — a build node with a non-zero exitcode carrying a real compiler
            // diagnostic in stdout. Regression coverage for the false-green-turned-content
            // -free-false-red bug: the DEFAULT read_console() call shape (format="plain",
            // includeStacktrace=false — see read_console.py) must surface the actual
            // "error CS####" diagnostic text, not just the
            // "[Bee build failure] ... (exit code N):" header with zero content. The
            // existing "detailed" test above only incidentally passed because the unique
            // marker also happened to sit in the header's annotation text.
            string uniqueMarker = $"TundraDefaultShapeMarker_{Guid.NewGuid():N}";
            string ndjson =
                "{\"msg\":\"noderesult\",\"annotation\":\"Csc Other.dll\",\"index\":1,\"exitcode\":1,\"outputfile\":\"Library/ScriptAssemblies/Other.dll\",\"stdout\":\"Assets/Tests/Broken.cs(16,73): error CS0023: " + uniqueMarker + "\"}\n";
            WriteTundraLog(ndjson);

            // Only "action" and "count" are set — format/includeStacktrace/types all take
            // the tool's real defaults, matching a plain read_console() call.
            var paramsObj = new JObject
            {
                ["action"] = "get",
                ["count"] = 1000,
            };

            // Act
            var result = ToJObject(ReadConsole.HandleCommand(paramsObj));

            // Assert
            Assert.IsTrue(result.Value<bool>("success"), result.ToString());
            var data = result["data"] as JArray;
            Assert.IsNotNull(data, "Data array should not be null.");

            bool foundDiagnostic = false;
            foreach (var entry in data)
            {
                // In "plain" format (the default) each entry IS the message string, not an
                // object — unlike the "detailed"/"json" format used by the other tests here.
                string entryText = entry.Type == JTokenType.Object
                    ? entry["message"]?.ToString()
                    : entry.ToString();

                if (entryText != null
                    && entryText.Contains(uniqueMarker)
                    && entryText.Contains("error CS0023")
                    && entryText.Contains("Broken.cs"))
                {
                    foundDiagnostic = true;
                    break;
                }
            }

            Assert.IsTrue(
                foundDiagnostic,
                "Default read_console() call (format=\"plain\", includeStacktrace=false) must surface the actual compiler diagnostic line (\"Broken.cs(16,73): error CS0023: ...\"), not just the build-failure header."
            );
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
