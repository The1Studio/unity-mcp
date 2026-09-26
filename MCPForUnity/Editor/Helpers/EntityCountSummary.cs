using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Helpers
{
    /// <summary>
    /// Builds the <c>entities</c> block of a validation snapshot.
    ///
    /// Deliberately free of Unity.Entities references so the rule that matters most here —
    /// "an unreadable count must never be reported as 0" — is unit-testable without a live
    /// Editor or the com.unity.entities package. The caller does the actual ECS reading and
    /// hands the raw numbers in.
    ///
    /// Why this exists: the counts come from a component lookup that can fail to resolve
    /// (see <c>ValidationSnapshot.ResolveComponentType</c>). When it fails, the world may
    /// hold dozens of live entities while the tool reports 0 — a false negative of exactly
    /// the kind the zero-entity-bake guard is meant to catch. Reporting 0 there is worse
    /// than reporting an error, because a caller reasonably concludes the world is empty.
    /// </summary>
    public static class EntityCountSummary
    {
        /// <summary>Number of named component types the caller tried and failed to resolve.</summary>
        public const string UnreadableComponentsKey = "unreadable_components";

        /// <summary>World-wide entity count, independent of the per-component lookup.</summary>
        public const string WorldTotalKey = "world_total";

        /// <summary>
        /// Assembles the <c>entities</c> block.
        /// </summary>
        /// <param name="readable">
        /// True when every component type needed for the counts resolved. False when the
        /// lookup failed — in which case the counts are unknown, not zero.
        /// </param>
        /// <param name="total">Entities matching the component query. Ignored when unreadable.</param>
        /// <param name="alive">Entities matching the query without the dead tag.</param>
        /// <param name="dead">Entities matching the query with the dead tag.</param>
        /// <param name="teams">Per-team counts keyed by team id, or null/empty when there are none.</param>
        /// <param name="unresolvedComponentNames">
        /// The component type names that failed to resolve, for the caller's diagnosis.
        /// </param>
        /// <param name="worldTotal">
        /// World-wide entity count when the caller could compute it, else null. Kept separate
        /// from <c>total</c> because it counts every entity, not just the ones matching the
        /// component query — the two are not interchangeable.
        /// </param>
        public static Dictionary<string, object> Build(
            bool readable,
            int total,
            int alive,
            int dead,
            IReadOnlyDictionary<byte, int> teams,
            IReadOnlyCollection<string> unresolvedComponentNames,
            int? worldTotal)
        {
            // Unreadable: omit total/alive/dead entirely (they serialize as explicit nulls
            // rather than a plausible-looking 0) and say why. A caller that checks for null,
            // or simply reads the flag, cannot mistake this for an empty world.
            if (!readable)
            {
                var unreadable = new Dictionary<string, object>
                {
                    ["total"] = null,
                    ["alive"] = null,
                    ["dead"] = null,
                    [UnreadableComponentsKey] = BuildUnresolvedList(unresolvedComponentNames),
                    ["note"] = "Entity counts unavailable: required component types did not resolve. "
                             + "This is NOT an empty world — total/alive/dead are null because the "
                             + "count could not be read."
                };

                if (worldTotal.HasValue)
                    unreadable[WorldTotalKey] = worldTotal.Value;

                return unreadable;
            }

            // Readable: a genuine zero is a real answer and must stay 0.
            var result = new Dictionary<string, object>
            {
                ["total"] = total,
                ["alive"] = alive,
                ["dead"] = dead,
            };

            if (worldTotal.HasValue)
                result[WorldTotalKey] = worldTotal.Value;

            if (teams != null && teams.Count > 0)
            {
                var byTeam = new Dictionary<string, int>();
                foreach (var kvp in teams)
                    byTeam[$"team_{kvp.Key}"] = kvp.Value;
                result["by_team"] = byTeam;
            }

            return result;
        }

        /// <summary>
        /// True when this <c>entities</c> block carries a usable total. False for both the
        /// unreadable shape (total null / flag present) and a block lacking the key entirely,
        /// so a consumer that feeds the value into a delta cannot silently read it as 0.
        /// </summary>
        public static bool IsTotalReadable(JToken entities)
        {
            if (entities == null || entities.Type == JTokenType.Null)
                return false;

            if (entities[UnreadableComponentsKey] != null)
                return false;

            var total = entities["total"];
            return total != null && total.Type != JTokenType.Null;
        }

        private static List<string> BuildUnresolvedList(IReadOnlyCollection<string> names)
        {
            var list = new List<string>();
            if (names == null)
                return list;

            foreach (var name in names)
            {
                if (!string.IsNullOrEmpty(name))
                    list.Add(name);
            }
            return list;
        }
    }
}
