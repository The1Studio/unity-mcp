#if UNITY_EDITOR
using System.Collections;
using System.IO;
using MCPForUnity.Runtime.Helpers;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MCPForUnityTests.PlayMode
{
    /// <summary>
    /// Regression tests for unity-mcp#131: capturing a composited screenshot in Play mode left
    /// <c>EditorApplication.isPaused</c> set to true, silently freezing the game for every
    /// subsequent call.
    ///
    /// Editor-only by nature (pause state lives on <c>EditorApplication</c>), so the whole file is
    /// compiled out of player builds.
    /// </summary>
    public class ScreenshotPauseStateTests
    {
        private bool _pausedAtEntry;
        private string _capturedFullPath;

        [SetUp]
        public void SetUp()
        {
            _pausedAtEntry = EditorApplication.isPaused;
            _capturedFullPath = null;
        }

        [TearDown]
        public void TearDown()
        {
            // Leave the editor exactly as the test found it, even when an assertion failed.
            EditorApplication.isPaused = _pausedAtEntry;

            if (!string.IsNullOrEmpty(_capturedFullPath))
            {
                if (File.Exists(_capturedFullPath)) File.Delete(_capturedFullPath);
                AssetDatabase.Refresh();
            }
        }

        [UnityTest]
        public IEnumerator CompositedScreenshot_RunningPlayMode_LeavesPauseStateFalse()
        {
            EditorApplication.isPaused = false;
            yield return null;

            Assert.IsFalse(EditorApplication.isPaused, "Precondition: Play mode must be running and unpaused.");

            CaptureAndRecord();

            Assert.IsFalse(
                EditorApplication.isPaused,
                "manage_camera screenshot must not leave Play mode paused (unity-mcp#131).");
        }

        [UnityTest]
        public IEnumerator CompositedScreenshot_AlreadyPaused_KeepsPauseStateTrue()
        {
            EditorApplication.isPaused = true;
            yield return null;

            Assert.IsTrue(EditorApplication.isPaused, "Precondition: the editor must be paused before the capture.");

            CaptureAndRecord();

            Assert.IsTrue(
                EditorApplication.isPaused,
                "A capture must restore the caller's pause state rather than resuming a deliberately paused game.");
        }

        private void CaptureAndRecord()
        {
            ScreenshotCaptureResult result = ScreenshotUtility.CaptureComposited(includeImage: true);
            _capturedFullPath = result.FullPath;
        }
    }
}
#endif
