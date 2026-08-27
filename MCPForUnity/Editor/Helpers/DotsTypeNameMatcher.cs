using System;
using System.Collections.Generic;
using System.Text;

namespace MCPForUnity.Editor.Helpers
{
    /// <summary>
    /// Name-matching rules used to resolve an ECS component type name against the entries
    /// registered in TypeManager. Deliberately free of any Unity.Entities dependency so the
    /// rules stay unit-testable in projects without com.unity.entities installed.
    /// </summary>
    public static class DotsTypeNameMatcher
    {
        /// <summary>One TypeManager registration: its debug type name and its TypeIndex value.</summary>
        public readonly struct Registration
        {
            public readonly string DebugName;
            public readonly int TypeIndex;

            public Registration(string debugName, int typeIndex)
            {
                DebugName = debugName;
                TypeIndex = typeIndex;
            }
        }

        /// <summary>
        /// True when <paramref name="debugName"/> is the query itself
        /// ("Unity.Transforms.LocalTransform") or ends with it as a namespace-qualified
        /// short name ("LocalTransform"). The leading '.' is what keeps "Health" from
        /// matching "MaxHealth".
        /// </summary>
        public static bool Matches(string debugName, string query)
        {
            if (string.IsNullOrEmpty(debugName) || string.IsNullOrEmpty(query))
                return false;

            return string.Equals(debugName, query, StringComparison.OrdinalIgnoreCase)
                || debugName.EndsWith("." + query, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Every registration matching <paramref name="query"/>, in registration order.
        /// One name can map to several TypeIndexes when the same type is registered more
        /// than once (duplicate assembly loads). Each of those is a distinct component to
        /// ECS, so keeping only the first silently answers for a registration the live
        /// World's chunks may not use.
        /// </summary>
        public static List<Registration> SelectMatches(IEnumerable<Registration> registrations, string query)
        {
            var matches = new List<Registration>();
            if (registrations == null)
                return matches;

            foreach (var registration in registrations)
            {
                if (Matches(registration.DebugName, query))
                    matches.Add(registration);
            }
            return matches;
        }

        /// <summary>
        /// Renders the candidate set as "Name (type_index=N), Name (type_index=M)".
        /// Every candidate is named so a caller can retry against a specific registration.
        /// </summary>
        public static string DescribeCandidates(IReadOnlyList<Registration> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return "(none)";

            var sb = new StringBuilder();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(candidates[i].DebugName)
                  .Append(" (type_index=")
                  .Append(candidates[i].TypeIndex)
                  .Append(')');
            }
            return sb.ToString();
        }

        /// <summary>
        /// The error text for a name that resolved to more than one registration. Names every
        /// candidate rather than picking one, so an ambiguous name can never be answered with a
        /// silent 0 from whichever registration happened to be enumerated first.
        /// </summary>
        public static string FormatAmbiguity(string query, IReadOnlyList<Registration> candidates)
        {
            int count = candidates?.Count ?? 0;
            return $"Component type '{query}' is ambiguous — {count} registrations match: "
                 + DescribeCandidates(candidates)
                 + ". Pass a fully-qualified name if the registrations differ by namespace, or use "
                 + "get_entity on a known entity_index to see which registration that entity carries.";
        }
    }
}
