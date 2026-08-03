using System;
using System.IO;
using NUnit.Framework;
using MCPForUnity.Editor.Services.Transport.Transports;

namespace MCPForUnityTests.Editor.Services
{
    /// <summary>
    /// Tests the client read/write loop's benign-exception classification.
    ///
    /// Regression: closing a client socket on purpose (stale-client eviction, or Stop())
    /// faults the owning handler, which is parked in a read, with ObjectDisposedException.
    /// That exception derives from InvalidOperationException — not IOException — so it fell
    /// through to Debug.LogError. During an EditMode run Unity's LogAssert then failed
    /// whichever unrelated test happened to be executing, so a full-suite run produced a
    /// different bogus failure every time. See The1Studio/unity-mcp#40.
    /// </summary>
    [TestFixture]
    public class StdioBridgeBenignExceptionTests
    {
        private static ObjectDisposedException DisposedStream()
            => new ObjectDisposedException("System.Net.Sockets.NetworkStream");

        // --- The regression itself -------------------------------------------------

        [Test]
        public void DisposedStream_WhenWeClosedTheSocket_IsBenign()
        {
            Assert.IsTrue(
                StdioBridgeHost.IsBenignClientException(DisposedStream(), closedIntentionally: true),
                "A socket we closed ourselves (eviction/Stop) must not report its parked read as an error.");
        }

        [Test]
        public void DisposedStream_OnLiveConnection_IsNotBenign()
        {
            Assert.IsFalse(
                StdioBridgeHost.IsBenignClientException(DisposedStream(), closedIntentionally: false),
                "A use-after-dispose on a live connection is a real bug and must keep reporting.");
        }

        /// <summary>
        /// The guard that keeps the fix narrow: ObjectDisposedException derives from
        /// InvalidOperationException, so classifying by the base type would silently
        /// swallow every unrelated InvalidOperationException from the read loop.
        /// </summary>
        [Test]
        public void UnrelatedInvalidOperationException_IsNotBenign_EvenWhenClosedIntentionally()
        {
            Assert.IsFalse(
                StdioBridgeHost.IsBenignClientException(
                    new InvalidOperationException("collection was modified"), closedIntentionally: true),
                "Only ObjectDisposedException is expected teardown — not its base type.");
        }

        [Test]
        public void Cancellation_WhenShuttingDown_IsBenign()
        {
            Assert.IsTrue(
                StdioBridgeHost.IsBenignClientException(new OperationCanceledException(), closedIntentionally: true),
                "Stop() cancels the CTS before closing clients; the resulting cancellation is expected.");
        }

        [Test]
        public void Cancellation_OnLiveConnection_IsNotBenign()
        {
            Assert.IsFalse(
                StdioBridgeHost.IsBenignClientException(new OperationCanceledException(), closedIntentionally: false),
                "Cancellation with no teardown in progress is not expected teardown.");
        }

        // --- Pre-existing classifications must be preserved ------------------------

        [Test]
        public void IOException_IsBenign_RegardlessOfTeardown()
        {
            Assert.IsTrue(StdioBridgeHost.IsBenignClientException(new IOException("peer reset"), closedIntentionally: false));
            Assert.IsTrue(StdioBridgeHost.IsBenignClientException(new IOException("peer reset"), closedIntentionally: true));
        }

        [Test]
        public void ConnectionClosedMessage_IsBenign_RegardlessOfTeardown()
        {
            var ex = new Exception("Connection closed before reading expected bytes");
            Assert.IsTrue(StdioBridgeHost.IsBenignClientException(ex, closedIntentionally: false));
        }

        [Test]
        public void ReadTimedOutMessage_IsBenign_RegardlessOfTeardown()
        {
            var ex = new Exception("Read timed out");
            Assert.IsTrue(StdioBridgeHost.IsBenignClientException(ex, closedIntentionally: false));
        }

        [Test]
        public void MessageMatching_IsCaseInsensitive()
        {
            var ex = new Exception("CONNECTION CLOSED BEFORE READING EXPECTED BYTES");
            Assert.IsTrue(StdioBridgeHost.IsBenignClientException(ex, closedIntentionally: false));
        }

        // --- Everything else still reports ----------------------------------------

        [Test]
        public void UnrelatedException_IsNotBenign()
        {
            Assert.IsFalse(
                StdioBridgeHost.IsBenignClientException(new FormatException("bad frame header"), closedIntentionally: true),
                "A genuine protocol/parsing fault must always reach the console.");
        }

        [Test]
        public void NullException_IsNotBenign()
        {
            Assert.IsFalse(StdioBridgeHost.IsBenignClientException(null, closedIntentionally: true));
        }
    }
}
