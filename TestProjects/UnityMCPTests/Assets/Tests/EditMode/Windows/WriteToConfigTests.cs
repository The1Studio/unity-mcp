using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using MCPForUnity.Editor.Models;
using MCPForUnity.Editor.Windows;

namespace MCPForUnityTests.Editor.Windows
{
    public class WriteToConfigTests
    {
        private string tempRoot;
        private string fakeUvPath;
        private string serverSrcDir;

        [SetUp]
        public void SetUp()
        {
            // Tests are designed for Linux/macOS runners. Skip on Windows due to ProcessStartInfo
            // restrictions when UseShellExecute=false for .cmd/.bat scripts.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Ignore("WriteToConfig tests are skipped on Windows (CI runs linux).\n" +
                              "ValidateUvBinarySafe requires launching an actual exe on Windows.");
            }
            this.tempRoot = Path.Combine(Path.GetTempPath(), "UnityMCPTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.tempRoot);

            // Create a fake uv executable that prints a valid version string
            this.fakeUvPath = Path.Combine(this.tempRoot, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "uv.cmd" : "uv");
            File.WriteAllText(this.fakeUvPath, "#!/bin/sh\n\necho 'uv 9.9.9'\n");
            TryChmodX(this.fakeUvPath);

            // Create a fake server directory with server.py
            this.serverSrcDir = Path.Combine(this.tempRoot, "server-src");
            Directory.CreateDirectory(this.serverSrcDir);
            File.WriteAllText(Path.Combine(this.serverSrcDir, "server.py"), "# dummy server\n");

            // Point the editor to our server dir (so ResolveServerSrc() uses this)
            EditorPrefs.SetString("MCPForUnity.ServerSrc", this.serverSrcDir);
            // Ensure no lock is enabled
            EditorPrefs.SetBool("MCPForUnity.LockCursorConfig", false);
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up editor preferences set during SetUp
            EditorPrefs.DeleteKey("MCPForUnity.ServerSrc");
            EditorPrefs.DeleteKey("MCPForUnity.LockCursorConfig");

            // Remove temp files
            try { if (Directory.Exists(this.tempRoot)) Directory.Delete(this.tempRoot, true); } catch { }
        }

        // --- Tests ---

        [Test]
        public void AddsEnvAndDisabledFalse_ForWindsurf()
        {
            var configPath = Path.Combine(this.tempRoot, "windsurf.json");
            WriteInitialConfig(configPath, isVSCode:false, command:this.fakeUvPath, directory:"/old/path");

            var client = new McpClient { name = "Windsurf", mcpType = McpTypes.Windsurf };
            InvokeWriteToConfig(configPath, client);

            var root = JObject.Parse(File.ReadAllText(configPath));
            var unity = (JObject)root.SelectToken("mcpServers.unityMCP");
            Assert.NotNull(unity, "Expected mcpServers.unityMCP node");
            Assert.NotNull(unity["env"], "env should be present for all clients");
            Assert.IsTrue(unity["env"]!.Type == JTokenType.Object, "env should be an object");
            Assert.AreEqual(false, (bool)unity["disabled"], "disabled:false should be set for Windsurf when missing");
        }

        [Test]
        public void AddsEnvAndDisabledFalse_ForKiro()
        {
            var configPath = Path.Combine(this.tempRoot, "kiro.json");
            WriteInitialConfig(configPath, isVSCode:false, command:this.fakeUvPath, directory:"/old/path");

            var client = new McpClient { name = "Kiro", mcpType = McpTypes.Kiro };
            InvokeWriteToConfig(configPath, client);

            var root = JObject.Parse(File.ReadAllText(configPath));
            var unity = (JObject)root.SelectToken("mcpServers.unityMCP");
            Assert.NotNull(unity, "Expected mcpServers.unityMCP node");
            Assert.NotNull(unity["env"], "env should be present for all clients");
            Assert.IsTrue(unity["env"]!.Type == JTokenType.Object, "env should be an object");
            Assert.AreEqual(false, (bool)unity["disabled"], "disabled:false should be set for Kiro when missing");
        }

        [Test]
        public void DoesNotAddEnvOrDisabled_ForCursor()
        {
            var configPath = Path.Combine(this.tempRoot, "cursor.json");
            WriteInitialConfig(configPath, isVSCode:false, command:this.fakeUvPath, directory:"/old/path");

            var client = new McpClient { name = "Cursor", mcpType = McpTypes.Cursor };
            InvokeWriteToConfig(configPath, client);

            var root = JObject.Parse(File.ReadAllText(configPath));
            var unity = (JObject)root.SelectToken("mcpServers.unityMCP");
            Assert.NotNull(unity, "Expected mcpServers.unityMCP node");
            Assert.IsNull(unity["env"], "env should not be added for non-Windsurf/Kiro clients");
            Assert.IsNull(unity["disabled"], "disabled should not be added for non-Windsurf/Kiro clients");
        }

        [Test]
        public void DoesNotAddEnvOrDisabled_ForVSCode()
        {
            var configPath = Path.Combine(this.tempRoot, "vscode.json");
            WriteInitialConfig(configPath, isVSCode:true, command:this.fakeUvPath, directory:"/old/path");

            var client = new McpClient { name = "VSCode", mcpType = McpTypes.VSCode };
            InvokeWriteToConfig(configPath, client);

            var root = JObject.Parse(File.ReadAllText(configPath));
            var unity = (JObject)root.SelectToken("servers.unityMCP");
            Assert.NotNull(unity, "Expected servers.unityMCP node");
            Assert.IsNull(unity["env"], "env should not be added for VSCode client");
            Assert.IsNull(unity["disabled"], "disabled should not be added for VSCode client");
            Assert.AreEqual("stdio", (string)unity["type"], "VSCode entry should include type=stdio");
        }

        [Test]
        public void PreservesExistingEnvAndDisabled()
        {
            var configPath = Path.Combine(this.tempRoot, "preserve.json");

            // Existing config with env and disabled=true should be preserved
            var json = new JObject
            {
                ["mcpServers"] = new JObject
                {
                    ["unityMCP"] = new JObject
                    {
                        ["command"]  = this.fakeUvPath,
                        ["args"]     = new JArray("run", "--directory", "/old/path", "server.py"),
                        ["env"]      = new JObject { ["FOO"] = "bar" },
                        ["disabled"] = true,
                    },
                },
            };
            File.WriteAllText(configPath, json.ToString());

            var client = new McpClient { name = "Windsurf", mcpType = McpTypes.Windsurf };
            InvokeWriteToConfig(configPath, client);

            var root = JObject.Parse(File.ReadAllText(configPath));
            var unity = (JObject)root.SelectToken("mcpServers.unityMCP");
            Assert.NotNull(unity, "Expected mcpServers.unityMCP node");
            Assert.AreEqual("bar", (string)unity["env"]!["FOO"], "Existing env should be preserved");
            Assert.AreEqual(true, (bool)unity["disabled"], "Existing disabled value should be preserved");
        }

        // --- Helpers ---

        private static void TryChmodX(string path)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = "+x \"" + path + "\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(2000);
            }
            catch { /* best-effort on non-Unix */ }
        }

        private static void WriteInitialConfig(string configPath, bool isVSCode, string command, string directory)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            JObject root;
            if (isVSCode)
            {
                root = new JObject
                {
                    ["servers"] = new JObject
                    {
                        ["unityMCP"] = new JObject
                        {
                            ["command"] = command,
                            ["args"] = new JArray("run", "--directory", directory, "server.py"),
                            ["type"] = "stdio",
                        },
                    },
                };
            }
            else
            {
                root = new JObject
                {
                    ["mcpServers"] = new JObject
                    {
                        ["unityMCP"] = new JObject
                        {
                            ["command"] = command,
                            ["args"] = new JArray("run", "--directory", directory, "server.py"),
                        },
                    },
                };
            }
            File.WriteAllText(configPath, root.ToString());
        }

        private static MCPForUnityEditorWindow CreateWindow()
        {
            return ScriptableObject.CreateInstance<MCPForUnityEditorWindow>();
        }

        private static void InvokeWriteToConfig(string configPath, McpClient client)
        {
            var window = CreateWindow();
            var mi = typeof(MCPForUnityEditorWindow).GetMethod("WriteToConfig", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(mi, "Could not find WriteToConfig via reflection");

            // pythonDir is unused by WriteToConfig, but pass server src to keep it consistent
            var result = (string)mi!.Invoke(window, new object[] {
                /* pythonDir */ string.Empty,
                /* configPath */ configPath,
                /* mcpClient */ client,
            });

            Assert.AreEqual("Configured successfully", result, "WriteToConfig should return success");
        }
    }
}