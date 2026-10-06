using System.Collections.Generic;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Server;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MCPForUnityTests.Editor.Services.Server
{
    /// <summary>
    /// The automatic quit-time stop must never terminate a server this Editor did not launch
    /// (e.g. a shared daemon), and must do nothing in batch mode. All process access is faked.
    /// </summary>
    [TestFixture]
    public class ServerShutdownOwnershipTests
    {
        private const int Port = 8080;
        private const int ServerPid = 4242;
        private const string Token = "tok-abc123";
        private const string PidFile = "/tmp/fake-mcp-8080.pid";

        private string _savedUrl;

        [SetUp]
        public void SetUp()
        {
            _savedUrl = EditorPrefs.GetString(EditorPrefKeys.HttpBaseUrl, string.Empty);
            EditorPrefs.SetString(EditorPrefKeys.HttpBaseUrl, "http://127.0.0.1:" + Port);
        }

        [TearDown]
        public void TearDown()
        {
            if (string.IsNullOrEmpty(_savedUrl)) EditorPrefs.DeleteKey(EditorPrefKeys.HttpBaseUrl);
            else EditorPrefs.SetString(EditorPrefKeys.HttpBaseUrl, _savedUrl);
        }

        private sealed class FakeTerminator : IProcessTerminator
        {
            public readonly List<int> Terminated = new List<int>();
            public bool Terminate(int pid) { Terminated.Add(pid); return true; }
        }

        private sealed class FakeDetector : IProcessDetector
        {
            public string CommandLine = "uvx mcp-for-unity --transport http " + Token;
            public bool LooksLikeMcpServerProcess(int pid) => true;
            public bool TryGetProcessCommandLine(int pid, out string argsLower) { argsLower = CommandLine; return true; }
            public List<int> GetListeningProcessIdsForPort(int port) => new List<int> { ServerPid };
            public int GetCurrentProcessId() => 1;
            public bool ProcessExists(int pid) => true;
            public string NormalizeForMatch(string input) => input;
        }

        private sealed class FakePidFiles : IPidFileManager
        {
            public bool HasHandshake;
            public string GetPidDirectory() => "/tmp";
            public string GetPidFilePath(int port) => PidFile;
            public bool TryReadPid(string pidFilePath, out int pid) { pid = ServerPid; return true; }
            public bool TryGetPortFromPidFilePath(string pidFilePath, out int port) { port = Port; return true; }
            public void DeletePidFile(string pidFilePath) { }
            public void StoreHandshake(string pidFilePath, string instanceToken) { }
            public bool TryGetHandshake(out string pidFilePath, out string instanceToken)
            {
                pidFilePath = HasHandshake ? PidFile : string.Empty;
                instanceToken = HasHandshake ? Token : string.Empty;
                return HasHandshake;
            }
            public void StoreTracking(int pid, int port, string argsHash = null) { }
            public bool TryGetStoredPid(int expectedPort, out int pid) { pid = 0; return false; }
            public string GetStoredArgsHash() => string.Empty;
            public void ClearTracking() { }
            public string ComputeShortHash(string input) => input;
        }

        private static ServerManagementService Build(FakePidFiles pids, FakeTerminator term)
        {
            return new ServerManagementService(new FakeDetector(), pids, term);
        }

        [Test]
        public void ForeignServer_NoHandshake_IsNotTerminated()
        {
            var term = new FakeTerminator();
            var service = Build(new FakePidFiles { HasHandshake = false }, term);

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("not stopping HTTP server on port 8080"));
            bool stopped = McpEditorShutdownCleanup.StopOwnedServerOnQuit(service, isBatchMode: false);

            Assert.IsFalse(stopped);
            Assert.IsEmpty(term.Terminated, "A listener this editor did not launch must never be terminated.");
        }

        [Test]
        public void BatchMode_NeverTerminates_EvenWithHandshake()
        {
            var term = new FakeTerminator();
            var service = Build(new FakePidFiles { HasHandshake = true }, term);

            bool stopped = McpEditorShutdownCleanup.StopOwnedServerOnQuit(service, isBatchMode: true);

            Assert.IsFalse(stopped);
            Assert.IsEmpty(term.Terminated);
        }

        [Test]
        public void OwnedServer_PidfileAndTokenMatch_IsStoppedOnQuit()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                Assert.Ignore("Token validation uses PowerShell on Windows; not faked here.");
            }

            var term = new FakeTerminator();
            var service = Build(new FakePidFiles { HasHandshake = true }, term);

            bool stopped = McpEditorShutdownCleanup.StopOwnedServerOnQuit(service, isBatchMode: false);

            Assert.IsTrue(stopped);
            Assert.AreEqual(new[] { ServerPid }, term.Terminated.ToArray());
        }
    }
}
