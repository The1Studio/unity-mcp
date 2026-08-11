using System;
using System.Collections;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MCPForUnity.Editor.Services.Transport.Transports;

namespace MCPForUnityTests.Editor.Services
{
    /// <summary>
    /// Tests that StdioBridgeHost correctly handles client reconnection scenarios.
    /// After an abrupt client disconnect, a new client must be able to connect and
    /// have its commands processed — this was broken by the zombie state bug (#785).
    /// </summary>
    [TestFixture]
    public class StdioBridgeReconnectTests
    {
        private const int ConnectTimeoutMs = 5000;
        private const int ReadTimeoutMs = 10000;

        [UnityTest]
        public IEnumerator NewClient_AfterAbruptDisconnect_CanSendAndReceiveCommands()
        {
            if (!StdioBridgeHost.IsRunning)
            {
                Assert.Ignore("StdioBridgeHost is not running; skipping reconnect test.");
                yield break;
            }

            int port = StdioBridgeHost.GetCurrentPort();

            // --- First client: connect, verify ping/pong, then abruptly close ---
            using (var client1 = new TcpClient())
            {
                Assert.IsTrue(client1.ConnectAsync("127.0.0.1", port).Wait(ConnectTimeoutMs),
                    "First client connect timed out");
                client1.ReceiveTimeout = ReadTimeoutMs;
                var stream1 = client1.GetStream();

                string handshake1 = ReadLine(stream1, ReadTimeoutMs);
                Assert.That(handshake1, Does.Contain("FRAMING=1"), "First client should receive handshake");

                // Send a framed ping
                SendFrame(stream1, Encoding.UTF8.GetBytes("ping"));
                byte[] pongBytes = ReadFrame(stream1, ReadTimeoutMs);
                string pong1 = Encoding.UTF8.GetString(pongBytes);
                Assert.That(pong1, Does.Contain("pong"), "First client should get pong response");

                // Abrupt close — simulates server crash / domain reload disconnect
                client1.Client.LingerState = new LingerOption(true, 0);
                client1.Close();
            }

            // Wait a few frames for cleanup
            for (int i = 0; i < 10; i++)
                yield return null;

            // --- Second client: connect and verify commands still work ---
            using (var client2 = new TcpClient())
            {
                Assert.IsTrue(client2.ConnectAsync("127.0.0.1", port).Wait(ConnectTimeoutMs),
                    "Second client connect timed out");
                client2.ReceiveTimeout = ReadTimeoutMs;
                var stream2 = client2.GetStream();

                string handshake2 = ReadLine(stream2, ReadTimeoutMs);
                Assert.That(handshake2, Does.Contain("FRAMING=1"), "Second client should receive handshake");

                // Send a framed ping — this is the critical check that would fail
                // if the bridge is in zombie state.
                SendFrame(stream2, Encoding.UTF8.GetBytes("ping"));
                byte[] pongBytes2 = ReadFrame(stream2, ReadTimeoutMs);
                string pong2 = Encoding.UTF8.GetString(pongBytes2);
                Assert.That(pong2, Does.Contain("pong"), "Second client should get pong response after reconnect");

                client2.Close();
            }
        }

        [UnityTest]
        public IEnumerator NewClient_WhileOldClientStillConnected_ClosesStaleClient()
        {
            if (!StdioBridgeHost.IsRunning)
            {
                Assert.Ignore("StdioBridgeHost is not running; skipping reconnect test.");
                yield break;
            }

            int port = StdioBridgeHost.GetCurrentPort();

            // --- First client: connect and verify handshake (but don't close) ---
            var client1 = new TcpClient();
            try
            {
                Assert.IsTrue(client1.ConnectAsync("127.0.0.1", port).Wait(ConnectTimeoutMs),
                    "First client connect timed out");
                client1.ReceiveTimeout = ReadTimeoutMs;
                var stream1 = client1.GetStream();

                string handshake1 = ReadLine(stream1, ReadTimeoutMs);
                Assert.That(handshake1, Does.Contain("FRAMING=1"), "First client should receive handshake");

                // Verify ping works on first client
                SendFrame(stream1, Encoding.UTF8.GetBytes("ping"));
                byte[] pong1Bytes = ReadFrame(stream1, ReadTimeoutMs);
                Assert.That(Encoding.UTF8.GetString(pong1Bytes), Does.Contain("pong"));

                // --- Second client: connect while first is still open ---
                using (var client2 = new TcpClient())
                {
                    Assert.IsTrue(client2.ConnectAsync("127.0.0.1", port).Wait(ConnectTimeoutMs),
                        "Second client connect timed out");
                    client2.ReceiveTimeout = ReadTimeoutMs;
                    var stream2 = client2.GetStream();

                    string handshake2 = ReadLine(stream2, ReadTimeoutMs);
                    Assert.That(handshake2, Does.Contain("FRAMING=1"), "Second client should receive handshake");

                    // Stale-client cleanup runs synchronously in HandleClientAsync before
                    // the read loop, so by the time we read the handshake it's already done.
                    // No yield needed — yielding here creates a window for the MCP Python
                    // server to reconnect and close our test client as stale.
                    SendFrame(stream2, Encoding.UTF8.GetBytes("ping"));
                    byte[] pong2Bytes = ReadFrame(stream2, ReadTimeoutMs);
                    Assert.That(Encoding.UTF8.GetString(pong2Bytes), Does.Contain("pong"),
                        "Second client should get pong after stale client cleanup");

                    client2.Close();
                }

                // First client should now be disconnected by the bridge.
                // A read attempt should throw or return 0 bytes.
                yield return null;
                bool firstClientDisconnected = false;
                try
                {
                    SendFrame(stream1, Encoding.UTF8.GetBytes("ping"));
                    ReadFrame(stream1, 2000);
                }
                catch
                {
                    firstClientDisconnected = true;
                }

                Assert.IsTrue(firstClientDisconnected, "First client should be disconnected after second client connects");
            }
            finally
            {
                try { client1.Close(); } catch { }
            }
        }

        [UnityTest]
        public IEnumerator NewClient_SendsStructuredEvictionFrame_BeforeClosingStaleClient()
        {
            if (!StdioBridgeHost.IsRunning)
            {
                Assert.Ignore("StdioBridgeHost is not running; skipping eviction-frame test.");
                yield break;
            }

            int port = StdioBridgeHost.GetCurrentPort();

            // --- First client: connect and consume handshake ---
            var client1 = new TcpClient();
            try
            {
                Assert.IsTrue(client1.ConnectAsync("127.0.0.1", port).Wait(ConnectTimeoutMs),
                    "First client connect timed out");
                client1.ReceiveTimeout = ReadTimeoutMs;
                var stream1 = client1.GetStream();

                string handshake1 = ReadLine(stream1, ReadTimeoutMs);
                Assert.That(handshake1, Does.Contain("FRAMING=1"), "First client should receive handshake");

                // --- Second client: connect — this triggers stale-client eviction ---
                using (var client2 = new TcpClient())
                {
                    Assert.IsTrue(client2.ConnectAsync("127.0.0.1", port).Wait(ConnectTimeoutMs),
                        "Second client connect timed out");
                    client2.ReceiveTimeout = ReadTimeoutMs;
                    var stream2 = client2.GetStream();

                    string handshake2 = ReadLine(stream2, ReadTimeoutMs);
                    Assert.That(handshake2, Does.Contain("FRAMING=1"), "Second client should receive handshake");

                    // First client should now receive an eviction-notice frame before
                    // its socket is closed. The bridge writes this frame with a 500ms
                    // best-effort timeout, so we give it a comfortable 2000ms window.
                    byte[] evictionFrame = ReadFrame(stream1, 2000);
                    string evictionJson = Encoding.UTF8.GetString(evictionFrame);

                    var parsed = JObject.Parse(evictionJson);
                    Assert.AreEqual("evicted", (string)parsed["status"],
                        "Eviction frame must have status=\"evicted\"");
                    Assert.AreEqual("stdio_single_client_stomped", (string)parsed["reason"],
                        "Eviction frame must have reason=\"stdio_single_client_stomped\"");
                    Assert.That((string)parsed["error"], Is.Not.Null.And.Not.Empty,
                        "Eviction frame must carry a human-readable error message");
                    Assert.That((string)parsed["new_client_endpoint"], Is.Not.Null.And.Not.Empty,
                        "Eviction frame must identify the stomping client's endpoint");
                    Assert.That(parsed["evicted_at_unix_ms"], Is.Not.Null,
                        "Eviction frame must carry an eviction timestamp");

                    client2.Close();
                }
            }
            finally
            {
                try { client1.Close(); } catch { }
            }
        }

        /// <summary>
        /// Evicting a stale client must not put an error in the console. The evicted
        /// handler is parked in a read when we close its socket, so it faults with
        /// ObjectDisposedException — expected teardown, not a fault. While that reached
        /// Debug.LogError, Unity's LogAssert failed whichever unrelated test happened to
        /// be running, so every full-suite run produced a different bogus failure.
        /// See The1Studio/unity-mcp#40.
        ///
        /// This covers the wiring (every deliberate close is routed through
        /// CloseClientIntentionally); StdioBridgeBenignExceptionTests covers the
        /// classification itself.
        /// </summary>
        [UnityTest]
        public IEnumerator EvictingStaleClient_DoesNotLogConsoleError()
        {
            if (!StdioBridgeHost.IsRunning)
            {
                Assert.Ignore("StdioBridgeHost is not running; skipping eviction-logging test.");
                yield break;
            }

            int port = StdioBridgeHost.GetCurrentPort();

            var client1 = new TcpClient();
            try
            {
                Assert.IsTrue(client1.ConnectAsync("127.0.0.1", port).Wait(ConnectTimeoutMs),
                    "First client connect timed out");
                client1.ReceiveTimeout = ReadTimeoutMs;
                string handshake1 = ReadLine(client1.GetStream(), ReadTimeoutMs);
                Assert.That(handshake1, Does.Contain("FRAMING=1"), "First client should receive handshake");

                // Second client connects — this evicts client1 and closes its socket.
                using (var client2 = new TcpClient())
                {
                    Assert.IsTrue(client2.ConnectAsync("127.0.0.1", port).Wait(ConnectTimeoutMs),
                        "Second client connect timed out");
                    client2.ReceiveTimeout = ReadTimeoutMs;
                    string handshake2 = ReadLine(client2.GetStream(), ReadTimeoutMs);
                    Assert.That(handshake2, Does.Contain("FRAMING=1"), "Second client should receive handshake");

                    client2.Close();
                }

                // The evicted handler faults on a threadpool continuation, so give it
                // room to surface before asserting the console stayed clean.
                for (int i = 0; i < 30; i++)
                    yield return null;

                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                try { client1.Close(); } catch { }
            }
        }

        #region Frame protocol helpers

        private static string ReadLine(NetworkStream stream, int timeoutMs)
        {
            var sb = new StringBuilder();
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            stream.ReadTimeout = timeoutMs;

            while (DateTime.UtcNow < deadline)
            {
                int b = stream.ReadByte();
                if (b < 0)
                    throw new IOException("Connection closed while reading line");
                if (b == '\n')
                    return sb.ToString();
                sb.Append((char)b);
            }
            throw new TimeoutException("Timed out reading line from stream");
        }

        private static void SendFrame(NetworkStream stream, byte[] payload)
        {
            byte[] header = new byte[8];
            ulong len = (ulong)payload.LongLength;
            header[0] = (byte)(len >> 56);
            header[1] = (byte)(len >> 48);
            header[2] = (byte)(len >> 40);
            header[3] = (byte)(len >> 32);
            header[4] = (byte)(len >> 24);
            header[5] = (byte)(len >> 16);
            header[6] = (byte)(len >> 8);
            header[7] = (byte)(len);
            stream.Write(header, 0, 8);
            stream.Write(payload, 0, payload.Length);
            stream.Flush();
        }

        private static byte[] ReadFrame(NetworkStream stream, int timeoutMs)
        {
            stream.ReadTimeout = timeoutMs;

            byte[] header = ReadExact(stream, 8, timeoutMs);
            ulong payloadLen =
                ((ulong)header[0] << 56) | ((ulong)header[1] << 48) |
                ((ulong)header[2] << 40) | ((ulong)header[3] << 32) |
                ((ulong)header[4] << 24) | ((ulong)header[5] << 16) |
                ((ulong)header[6] << 8)  | header[7];

            if (payloadLen == 0 || payloadLen > 16 * 1024 * 1024)
                throw new IOException($"Invalid frame length: {payloadLen}");

            return ReadExact(stream, (int)payloadLen, timeoutMs);
        }

        private static byte[] ReadExact(NetworkStream stream, int count, int timeoutMs)
        {
            byte[] buffer = new byte[count];
            int offset = 0;
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (offset < count)
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException($"Timed out reading {count} bytes (got {offset})");

                int remaining = count - offset;
                int read = stream.Read(buffer, offset, remaining);
                if (read == 0)
                    throw new IOException("Connection closed before reading expected bytes");
                offset += read;
            }

            return buffer;
        }

        #endregion
    }
}
