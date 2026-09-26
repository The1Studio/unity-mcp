using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnityTests.Editor.Helpers
{
    /// <summary>
    /// Regression tests for unity-mcp#95: <c>validation_snapshot</c> reported
    /// <c>entities.total: 0</c> while the Default World held dozens of live entities.
    ///
    /// The cause was a component lookup that silently failed, leaving the counts at their
    /// 0 initialisers — a "0" meaning "unreadable", which a caller reasonably reads as
    /// "the world is empty". That is the precise false negative the zero-entity-bake guard
    /// exists to catch, so these tests pin the FAILURE shape: an unreadable count must not
    /// be reportable as a number, and a genuine zero must still report zero.
    ///
    /// The helper under test is deliberately free of Unity.Entities, so this runs as a plain
    /// EditMode test with no live Editor, no Play mode, and no com.unity.entities package.
    /// </summary>
    public class EntityCountSummaryTests
    {
        private static readonly IReadOnlyDictionary<byte, int> NoTeams =
            new Dictionary<byte, int>();

        private static readonly IReadOnlyCollection<string> NoMisses = new List<string>();

        private static JObject ToJson(Dictionary<string, object> summary)
        {
            // Round-trips through the real serializer path so the assertions run against the
            // shape an MCP caller actually receives — a null that vanishes in serialization
            // would defeat the whole point of this fix.
            return JObject.FromObject(summary);
        }

        /// <summary>
        /// Newtonsoft represents an explicit JSON null as a JValue, not a null reference, so a
        /// bare Assert.IsNull is ambiguous here. This asks the question the fix actually cares
        /// about: is the field absent-or-null rather than a usable number?
        /// </summary>
        private static bool IsJsonNull(JToken token)
        {
            return token == null || token.Type == JTokenType.Null;
        }

        #region The reported defect — an unreadable count must not read as 0

        [Test]
        public void Build_WhenComponentLookupFails_DoesNotReportZero()
        {
            var summary = EntityCountSummary.Build(
                readable: false,
                total: 0,   // the old code returned exactly this, as if it were a real count
                alive: 0,
                dead: 0,
                teams: NoTeams,
                unresolvedComponentNames: new[] { "DOTSCombat.Health", "DOTSCore.DeadTag" },
                worldTotal: 63);

            var json = ToJson(summary);

            Assert.IsTrue(IsJsonNull(json["total"]), "An unreadable count must not be reported as 0.");
            Assert.IsTrue(IsJsonNull(json["alive"]), "An unreadable count must not be reported as 0.");
            Assert.IsTrue(IsJsonNull(json["dead"]), "An unreadable count must not be reported as 0.");
        }

        [Test]
        public void Build_WhenComponentLookupFails_IsNotCoercedToZeroByAConsumer()
        {
            // The sharpest form of the regression: a caller doing the pre-fix `?? 0` coercion
            // must not land on 0. This is the assertion that goes RED if the fix regresses to
            // returning numeric zero for an unreadable lookup.
            var summary = EntityCountSummary.Build(
                readable: false,
                total: 0,
                alive: 0,
                dead: 0,
                teams: NoTeams,
                unresolvedComponentNames: new[] { "DOTSCombat.Health" },
                worldTotal: 63);

            var json = ToJson(summary);

            Assert.AreNotEqual(0, (int?)json["total"],
                "Coercing an unreadable total must not yield the number 0.");
            Assert.IsFalse(EntityCountSummary.IsTotalReadable(json),
                "The block must advertise itself as unreadable.");
        }

        [Test]
        public void Build_WhenComponentLookupFails_SaysWhy()
        {
            var summary = EntityCountSummary.Build(
                readable: false,
                total: 0,
                alive: 0,
                dead: 0,
                teams: NoTeams,
                unresolvedComponentNames: new[] { "DOTSCombat.Health", "DOTSCore.DeadTag" },
                worldTotal: 63);

            var json = ToJson(summary);

            var unresolved = json[EntityCountSummary.UnreadableComponentsKey] as JArray;
            Assert.IsNotNull(unresolved, "Caller needs to see which component types failed to resolve.");
            Assert.AreEqual(2, unresolved.Count);

            var names = new List<string>();
            foreach (var entry in unresolved)
                names.Add((string)entry);

            CollectionAssert.Contains(names, "DOTSCombat.Health");
            CollectionAssert.Contains(names, "DOTSCore.DeadTag");

            Assert.IsNotNull(json["note"], "The unreadable state should be explained, not just flagged.");
        }

        [Test]
        public void Build_WhenComponentLookupFails_StillReportsWorldTotal()
        {
            // The whole point: entities ARE live, so the block must not lose that fact.
            var summary = EntityCountSummary.Build(
                readable: false,
                total: 0,
                alive: 0,
                dead: 0,
                teams: NoTeams,
                unresolvedComponentNames: new[] { "DOTSCombat.Health" },
                worldTotal: 63);

            var json = ToJson(summary);

            Assert.AreEqual(63, (int)json[EntityCountSummary.WorldTotalKey],
                "A world-wide count should survive an unreadable per-component lookup.");
        }

        #endregion

        #region A genuine zero must stay a zero

        [Test]
        public void Build_WhenReadableAndEmpty_ReportsZeroNotUnreadable()
        {
            var summary = EntityCountSummary.Build(
                readable: true,
                total: 0,
                alive: 0,
                dead: 0,
                teams: NoTeams,
                unresolvedComponentNames: NoMisses,
                worldTotal: 0);

            var json = ToJson(summary);

            Assert.AreEqual(0, (int)json["total"], "A real empty world is a real 0.");
            Assert.AreEqual(0, (int)json["alive"]);
            Assert.AreEqual(0, (int)json["dead"]);
            Assert.IsTrue(IsJsonNull(json[EntityCountSummary.UnreadableComponentsKey]),
                "A genuine zero is not an unreadable count.");
        }

        [Test]
        public void Build_WhenReadableAndPopulated_IsUnchangedFromBefore()
        {
            // Regression guard on the success path: the old shape must be preserved exactly.
            var summary = EntityCountSummary.Build(
                readable: true,
                total: 5,
                alive: 3,
                dead: 2,
                teams: new Dictionary<byte, int> { { 0, 3 }, { 1, 2 } },
                unresolvedComponentNames: NoMisses,
                worldTotal: 40);

            var json = ToJson(summary);

            Assert.AreEqual(5, (int)json["total"]);
            Assert.AreEqual(3, (int)json["alive"]);
            Assert.AreEqual(2, (int)json["dead"]);

            var byTeam = json["by_team"] as JObject;
            Assert.IsNotNull(byTeam, "by_team should still be emitted when teams exist.");
            Assert.AreEqual(3, (int)byTeam["team_0"]);
            Assert.AreEqual(2, (int)byTeam["team_1"]);
        }

        [Test]
        public void Build_WhenReadableWithNoTeams_OmitsByTeam()
        {
            var summary = EntityCountSummary.Build(
                readable: true,
                total: 1,
                alive: 1,
                dead: 0,
                teams: NoTeams,
                unresolvedComponentNames: NoMisses,
                worldTotal: 1);

            Assert.IsTrue(IsJsonNull(ToJson(summary)["by_team"]));
        }

        #endregion

        #region IsTotalReadable — what the compare path gates on

        [Test]
        public void IsTotalReadable_ReturnsFalseForUnreadableBlock()
        {
            var json = ToJson(EntityCountSummary.Build(
                readable: false, total: 0, alive: 0, dead: 0, teams: NoTeams,
                unresolvedComponentNames: new[] { "DOTSCombat.Health" }, worldTotal: 63));

            Assert.IsFalse(EntityCountSummary.IsTotalReadable(json),
                "Compare must not compute a delta from an unreadable block.");
        }

        [Test]
        public void IsTotalReadable_ReturnsTrueForReadableZero()
        {
            var json = ToJson(EntityCountSummary.Build(
                readable: true, total: 0, alive: 0, dead: 0, teams: NoTeams,
                unresolvedComponentNames: NoMisses, worldTotal: 0));

            Assert.IsTrue(EntityCountSummary.IsTotalReadable(json),
                "A genuine zero is readable and must still be comparable.");
        }

        [Test]
        public void IsTotalReadable_ReturnsFalseWhenTotalKeyMissing()
        {
            // A pre-fix snapshot (or a truncated one) has no usable total; treating the
            // absence as 0 is the same defect one layer up.
            Assert.IsFalse(EntityCountSummary.IsTotalReadable(new JObject()));
            Assert.IsFalse(EntityCountSummary.IsTotalReadable(null));
            Assert.IsFalse(EntityCountSummary.IsTotalReadable(JValue.CreateNull()));
        }

        #endregion
    }
}
