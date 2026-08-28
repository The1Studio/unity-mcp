using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MCPForUnity.Editor.Tools.Prefabs;
using static MCPForUnityTests.Editor.TestUtilities;

namespace MCPForUnityTests.Editor.Tools
{
    /// <summary>
    /// Regression tests for issue #77: modify_contents and the prefab stage held two
    /// disconnected in-memory copies of the same asset with no cross-invalidation.
    /// modify_contents called PrefabUtility.LoadPrefabContents (its own isolated copy) and
    /// saved it; the stage's separate copy was then written over the file by the next
    /// save_prefab_stage or close_prefab_stage, silently destroying the modify_contents work.
    /// Both writes succeeded, so there was no error path to notice.
    /// </summary>
    public class ManagePrefabsStageCoherenceTests
    {
        private const string TempDirectory = "Assets/Temp/ManagePrefabsStageCoherenceTests";

        [SetUp]
        public void SetUp()
        {
            StageUtility.GoToMainStage();
            EnsureFolder(TempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            StageUtility.GoToMainStage();

            if (AssetDatabase.IsValidFolder(TempDirectory))
            {
                AssetDatabase.DeleteAsset(TempDirectory);
            }

            CleanupEmptyParentFolders(TempDirectory);
        }

        private static string CreatePrefab(string name)
        {
            string prefabPath = Path.Combine(TempDirectory, name + ".prefab").Replace('\\', '/');
            var source = new GameObject(name);
            try
            {
                PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
            AssetDatabase.Refresh();
            return prefabPath;
        }

        private static Vector3 ReadPositionFromDisk(string prefabPath)
        {
            // Load the asset fresh so the assertion reads the file, not a cached editing copy.
            AssetDatabase.Refresh();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(asset, $"Prefab asset missing at '{prefabPath}'.");
            return asset.transform.localPosition;
        }

        /// <summary>
        /// The reported data loss: with a stage open on the same asset, a modify_contents write
        /// followed by save_prefab_stage left the file holding the STAGE's untouched copy.
        /// </summary>
        [Test]
        public void ModifyContents_WithStageOpenOnSameAsset_SurvivesSavePrefabStage()
        {
            string prefabPath = CreatePrefab("StageCoherence_Save");

            var opened = ToJObject(ManagePrefabs.HandleCommand(new JObject
            {
                ["action"] = "open_prefab_stage",
                ["prefabPath"] = prefabPath
            }));
            Assert.IsTrue(opened.Value<bool>("success"), opened.Value<string>("message"));

            var modified = ToJObject(ManagePrefabs.HandleCommand(new JObject
            {
                ["action"] = "modify_contents",
                ["prefabPath"] = prefabPath,
                ["position"] = new JArray { 1f, 2f, 3f }
            }));
            Assert.IsTrue(modified.Value<bool>("success"), modified.Value<string>("message"));
            Assert.IsTrue(
                modified["data"].Value<bool>("editedOpenPrefabStage"),
                "modify_contents must edit the open stage's copy, not load a second one.");

            var saved = ToJObject(ManagePrefabs.HandleCommand(new JObject
            {
                ["action"] = "save_prefab_stage"
            }));
            Assert.IsTrue(saved.Value<bool>("success"), saved.Value<string>("message"));

            ManagePrefabs.HandleCommand(new JObject { ["action"] = "close_prefab_stage" });

            Assert.AreEqual(
                new Vector3(1f, 2f, 3f),
                ReadPositionFromDisk(prefabPath),
                "save_prefab_stage overwrote the modify_contents work with the stage's stale copy.");
        }

        /// <summary>
        /// The second reported shape: closing the stage without an explicit save wiped the
        /// modify_contents write, because the stage's copy was written over the file on the way out.
        /// </summary>
        [Test]
        public void ModifyContents_WithStageOpenOnSameAsset_SurvivesCloseWithoutSave()
        {
            string prefabPath = CreatePrefab("StageCoherence_Close");

            ManagePrefabs.HandleCommand(new JObject
            {
                ["action"] = "open_prefab_stage",
                ["prefabPath"] = prefabPath
            });

            var modified = ToJObject(ManagePrefabs.HandleCommand(new JObject
            {
                ["action"] = "modify_contents",
                ["prefabPath"] = prefabPath,
                ["position"] = new JArray { 4f, 5f, 6f }
            }));
            Assert.IsTrue(modified.Value<bool>("success"), modified.Value<string>("message"));

            var closed = ToJObject(ManagePrefabs.HandleCommand(new JObject
            {
                ["action"] = "close_prefab_stage"
            }));
            Assert.IsTrue(closed.Value<bool>("success"), closed.Value<string>("message"));

            Assert.AreEqual(
                new Vector3(4f, 5f, 6f),
                ReadPositionFromDisk(prefabPath),
                "Closing the stage discarded the modify_contents write.");
        }

        /// <summary>
        /// A stage open on a DIFFERENT asset must not divert the write — modify_contents still
        /// edits its own isolated copy of the requested path.
        /// </summary>
        [Test]
        public void ModifyContents_WithStageOpenOnDifferentAsset_EditsRequestedPrefab()
        {
            string stagePath = CreatePrefab("StageCoherence_Other");
            string targetPath = CreatePrefab("StageCoherence_Target");

            ManagePrefabs.HandleCommand(new JObject
            {
                ["action"] = "open_prefab_stage",
                ["prefabPath"] = stagePath
            });

            var modified = ToJObject(ManagePrefabs.HandleCommand(new JObject
            {
                ["action"] = "modify_contents",
                ["prefabPath"] = targetPath,
                ["position"] = new JArray { 7f, 8f, 9f }
            }));
            Assert.IsTrue(modified.Value<bool>("success"), modified.Value<string>("message"));
            Assert.IsFalse(
                modified["data"].Value<bool>("editedOpenPrefabStage"),
                "A stage on a different asset must not be treated as the target's editing copy.");

            ManagePrefabs.HandleCommand(new JObject { ["action"] = "close_prefab_stage" });

            Assert.AreEqual(new Vector3(7f, 8f, 9f), ReadPositionFromDisk(targetPath));
            Assert.AreEqual(Vector3.zero, ReadPositionFromDisk(stagePath),
                "The staged prefab must be untouched by a write aimed at another path.");
        }

        /// <summary>
        /// No stage open at all: the original headless path must keep working unchanged.
        /// </summary>
        [Test]
        public void ModifyContents_WithNoStageOpen_StillEditsHeadlessly()
        {
            string prefabPath = CreatePrefab("StageCoherence_Headless");

            var modified = ToJObject(ManagePrefabs.HandleCommand(new JObject
            {
                ["action"] = "modify_contents",
                ["prefabPath"] = prefabPath,
                ["position"] = new JArray { 10f, 11f, 12f }
            }));

            Assert.IsTrue(modified.Value<bool>("success"), modified.Value<string>("message"));
            Assert.IsFalse(modified["data"].Value<bool>("editedOpenPrefabStage"));
            Assert.AreEqual(new Vector3(10f, 11f, 12f), ReadPositionFromDisk(prefabPath));
        }

        /// <summary>
        /// Pins the reporting contract: a close that discards unsaved stage edits must say so.
        /// A silent clean-looking close is exactly how the original data loss went unnoticed.
        /// </summary>
        [Test]
        public void ClosePrefabStage_ReportsWhetherUnsavedChangesExisted()
        {
            string prefabPath = CreatePrefab("StageCoherence_DirtyReport");

            ManagePrefabs.HandleCommand(new JObject
            {
                ["action"] = "open_prefab_stage",
                ["prefabPath"] = prefabPath
            });

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            Assert.IsNotNull(stage);
            var child = new GameObject("UnsavedChild");
            child.transform.SetParent(stage.prefabContentsRoot.transform, false);
            EditorSceneManager.MarkSceneDirty(stage.scene);

            var closed = ToJObject(ManagePrefabs.HandleCommand(new JObject
            {
                ["action"] = "close_prefab_stage"
            }));

            Assert.IsTrue(closed.Value<bool>("success"), closed.Value<string>("message"));
            Assert.IsTrue(
                closed["data"].Value<bool>("wasDirty"),
                "close_prefab_stage must report that unsaved in-memory changes existed.");
            Assert.IsFalse(closed["data"].Value<bool>("savedBeforeClose"));
        }
    }
}
