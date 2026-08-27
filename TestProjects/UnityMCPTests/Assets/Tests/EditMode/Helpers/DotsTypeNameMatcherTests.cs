using System.Collections.Generic;
using NUnit.Framework;
using MCPForUnity.Editor.Helpers;
using Registration = MCPForUnity.Editor.Helpers.DotsTypeNameMatcher.Registration;

namespace MCPForUnityTests.Editor.Helpers
{
    /// <summary>
    /// Tests for the component-name resolution rules behind manage_dots.
    ///
    /// The defect these pin (issue #80): a name that matches several TypeManager
    /// registrations used to resolve to whichever one was enumerated first, so a query
    /// built from it silently returned 0 for entities carrying one of the others. Every
    /// match must survive resolution, and every candidate must be named in the report.
    /// </summary>
    public class DotsTypeNameMatcherTests
    {
        /// <summary>
        /// The three duplicate DOTSCombat.Health registrations from the issue report.
        /// </summary>
        private static List<Registration> DuplicateHealthRegistry() => new List<Registration>
        {
            new Registration("Unity.Transforms.LocalTransform", 12),
            new Registration("DOTSCombat.Health", 131263),
            new Registration("DOTSCombat.MaxHealth", 131400),
            new Registration("DOTSCombat.Health", 131513),
            new Registration("DOTSCombat.Health", 132102),
        };

        #region Matches

        [Test]
        public void Matches_ShortName_MatchesNamespaceQualifiedType()
        {
            Assert.IsTrue(DotsTypeNameMatcher.Matches("Unity.Transforms.LocalTransform", "LocalTransform"));
        }

        [Test]
        public void Matches_FullName_MatchesExactly()
        {
            Assert.IsTrue(DotsTypeNameMatcher.Matches("Unity.Transforms.LocalTransform", "Unity.Transforms.LocalTransform"));
        }

        [Test]
        public void Matches_IsCaseInsensitive()
        {
            Assert.IsTrue(DotsTypeNameMatcher.Matches("DOTSCombat.Health", "health"));
        }

        [Test]
        public void Matches_ShortName_DoesNotMatchLongerTypeWithSameSuffix()
        {
            // The leading '.' in the suffix test is what keeps "Health" off "MaxHealth".
            Assert.IsFalse(DotsTypeNameMatcher.Matches("DOTSCombat.MaxHealth", "Health"));
        }

        [Test]
        public void Matches_NullOrEmpty_IsFalse()
        {
            Assert.IsFalse(DotsTypeNameMatcher.Matches(null, "Health"));
            Assert.IsFalse(DotsTypeNameMatcher.Matches("DOTSCombat.Health", null));
            Assert.IsFalse(DotsTypeNameMatcher.Matches("DOTSCombat.Health", ""));
        }

        #endregion

        #region SelectMatches

        [Test]
        public void SelectMatches_DuplicateRegistrations_ReturnsEveryMatchNotJustTheFirst()
        {
            var matches = DotsTypeNameMatcher.SelectMatches(DuplicateHealthRegistry(), "Health");

            // A first-match resolver returns 1 here and silently answers for type_index 131263.
            Assert.AreEqual(3, matches.Count,
                "Every matching registration must survive resolution — keeping only the first is the #80 defect.");
        }

        [Test]
        public void SelectMatches_DuplicateRegistrations_ReturnsTheExactTypeIndices()
        {
            var matches = DotsTypeNameMatcher.SelectMatches(DuplicateHealthRegistry(), "Health");

            // Identity, not count: three matches of the wrong indices is still a wrong answer.
            var indices = new List<int>();
            foreach (var match in matches) indices.Add(match.TypeIndex);
            CollectionAssert.AreEquivalent(new[] { 131263, 131513, 132102 }, indices);
        }

        [Test]
        public void SelectMatches_UnambiguousName_ReturnsExactlyOne()
        {
            var matches = DotsTypeNameMatcher.SelectMatches(DuplicateHealthRegistry(), "LocalTransform");

            Assert.AreEqual(1, matches.Count);
            Assert.AreEqual(12, matches[0].TypeIndex);
        }

        [Test]
        public void SelectMatches_UnknownName_ReturnsEmpty()
        {
            Assert.AreEqual(0, DotsTypeNameMatcher.SelectMatches(DuplicateHealthRegistry(), "NoSuchComponent").Count);
        }

        [Test]
        public void SelectMatches_NullRegistry_ReturnsEmpty()
        {
            Assert.AreEqual(0, DotsTypeNameMatcher.SelectMatches(null, "Health").Count);
        }

        #endregion

        #region Reporting

        [Test]
        public void DescribeCandidates_NamesEveryCandidateWithItsTypeIndex()
        {
            var matches = DotsTypeNameMatcher.SelectMatches(DuplicateHealthRegistry(), "Health");
            var described = DotsTypeNameMatcher.DescribeCandidates(matches);

            // A report that drops a candidate leaves the caller unable to reach that registration.
            StringAssert.Contains("type_index=131263", described);
            StringAssert.Contains("type_index=131513", described);
            StringAssert.Contains("type_index=132102", described);
            StringAssert.Contains("DOTSCombat.Health", described);
        }

        [Test]
        public void DescribeCandidates_EmptySet_IsExplicit()
        {
            Assert.AreEqual("(none)", DotsTypeNameMatcher.DescribeCandidates(new List<Registration>()));
            Assert.AreEqual("(none)", DotsTypeNameMatcher.DescribeCandidates(null));
        }

        [Test]
        public void FormatAmbiguity_StatesTheCountAndEveryCandidate()
        {
            var matches = DotsTypeNameMatcher.SelectMatches(DuplicateHealthRegistry(), "Health");
            var message = DotsTypeNameMatcher.FormatAmbiguity("Health", matches);

            StringAssert.Contains("ambiguous", message);
            StringAssert.Contains("3 registrations", message);
            StringAssert.Contains("type_index=131263", message);
            StringAssert.Contains("type_index=131513", message);
            StringAssert.Contains("type_index=132102", message);
        }

        #endregion
    }
}
