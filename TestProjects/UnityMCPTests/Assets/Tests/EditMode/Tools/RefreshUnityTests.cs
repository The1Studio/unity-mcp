using Newtonsoft.Json.Linq;
using NUnit.Framework;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnityTests.Editor.Tools
{
    /// <summary>
    /// Tests for RefreshUnity tool functionality (issue #38: mode="force" must not
    /// silently no-op when scope="scripts", and any skipped refresh must report why).
    /// </summary>
    public class RefreshUnityTests
    {
        [Test]
        public void HandleCommand_ForceScripts_TriggersRefreshAndNoSkipReason()
        {
            var resultObj = MCPForUnity.Editor.Tools.RefreshUnity.HandleCommand(new JObject
            {
                ["mode"] = "force",
                ["scope"] = "scripts"
            }).GetAwaiter().GetResult();

            Assert.IsInstanceOf<SuccessResponse>(resultObj);
            var success = (SuccessResponse)resultObj;
            var data = JObject.FromObject(success.Data);

            Assert.IsTrue(data["refresh_triggered"]?.Value<bool>(), "mode=force must always trigger a refresh, even for scope=scripts");
            Assert.AreEqual(JTokenType.Null, data["skip_reason"]?.Type);
        }

        [Test]
        public void HandleCommand_IfDirtyScripts_SkipsRefreshButReportsReason()
        {
            var resultObj = MCPForUnity.Editor.Tools.RefreshUnity.HandleCommand(new JObject
            {
                ["mode"] = "if_dirty",
                ["scope"] = "scripts",
                ["compile"] = "none"
            }).GetAwaiter().GetResult();

            Assert.IsInstanceOf<SuccessResponse>(resultObj);
            var success = (SuccessResponse)resultObj;
            var data = JObject.FromObject(success.Data);

            Assert.IsFalse(data["refresh_triggered"]?.Value<bool>());
            Assert.IsFalse(string.IsNullOrEmpty(data["skip_reason"]?.ToString()),
                "A skipped refresh must always explain why via skip_reason (issue #38)");
        }

        [Test]
        public void HandleCommand_ForceAssets_TriggersRefresh()
        {
            var resultObj = MCPForUnity.Editor.Tools.RefreshUnity.HandleCommand(new JObject
            {
                ["mode"] = "force",
                ["scope"] = "assets"
            }).GetAwaiter().GetResult();

            Assert.IsInstanceOf<SuccessResponse>(resultObj);
            var success = (SuccessResponse)resultObj;
            var data = JObject.FromObject(success.Data);

            Assert.IsTrue(data["refresh_triggered"]?.Value<bool>());
            Assert.AreEqual(JTokenType.Null, data["skip_reason"]?.Type);
        }
    }
}
