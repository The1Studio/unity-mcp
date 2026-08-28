using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnityTests.Editor.Tools
{
    /// <summary>
    /// Regression tests for issue #79: ComponentOps.SetProperty rejected every
    /// vector-family SerializedProperty with "Unsupported SerializedPropertyType",
    /// so manage_prefabs modify_contents could not write a RectTransform's
    /// m_AnchorMin / m_AnchorMax / m_SizeDelta / m_Pivot in either the object
    /// {"x":0,"y":0} or the array [0,0] form.
    ///
    /// These names take the SerializedProperty path rather than reflection:
    /// ParamCoercion.NormalizePropertyName("m_AnchorMin") yields "mAnchormin",
    /// which matches no RectTransform member, and RectTransform's fields are
    /// native so no serialized backing field is found either.
    /// </summary>
    public class ComponentOpsVectorPropertyTests
    {
        private GameObject go;
        private RectTransform rect;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("VectorPropertyTarget", typeof(RectTransform));
            rect = go.GetComponent<RectTransform>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null)
                Object.DestroyImmediate(go);
        }

        [Test]
        public void SetProperty_Vector2_ObjectForm_RoundTrips()
        {
            var value = new JObject { ["x"] = 0.25f, ["y"] = 0.75f };

            bool ok = ComponentOps.SetProperty(rect, "m_AnchorMin", value, out string error);

            Assert.IsTrue(ok, $"Expected the Vector2 object form to be accepted, got: {error}");
            Assert.IsNull(error);
            Assert.AreEqual(new Vector2(0.25f, 0.75f), rect.anchorMin);
        }

        [Test]
        public void SetProperty_Vector2_ArrayForm_RoundTrips()
        {
            var value = new JArray { 1f, 1f };

            bool ok = ComponentOps.SetProperty(rect, "m_AnchorMax", value, out string error);

            Assert.IsTrue(ok, $"Expected the Vector2 array form to be accepted, got: {error}");
            Assert.AreEqual(new Vector2(1f, 1f), rect.anchorMax);
        }

        [Test]
        public void SetProperty_Vector2_SizeDeltaAndPivot_RoundTrip()
        {
            Assert.IsTrue(
                ComponentOps.SetProperty(rect, "m_SizeDelta", new JObject { ["x"] = 100f, ["y"] = 50f }, out string sizeError),
                $"m_SizeDelta rejected: {sizeError}");
            Assert.IsTrue(
                ComponentOps.SetProperty(rect, "m_Pivot", new JArray { 0.5f, 0.5f }, out string pivotError),
                $"m_Pivot rejected: {pivotError}");

            Assert.AreEqual(new Vector2(100f, 50f), rect.sizeDelta);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), rect.pivot);
        }

        /// <summary>
        /// The dotted-path form documented as the workaround in issue #79 resolves to a
        /// Float leaf. It worked before the fix and must keep working after it.
        /// </summary>
        [Test]
        public void SetProperty_DottedFloatPath_StillWorks()
        {
            bool ok = ComponentOps.SetProperty(rect, "m_AnchorMin.x", new JValue(0.4f), out string error);

            Assert.IsTrue(ok, $"Dotted-path float write regressed: {error}");
            Assert.AreEqual(0.4f, rect.anchorMin.x, 0.0001f);
        }

        /// <summary>
        /// Pins the FAILURE state: an unparsable payload for a supported vector type must
        /// still be rejected with a type-specific error, never silently written as a zeroed
        /// default. Without this, the fix above could "pass" by accepting anything.
        /// </summary>
        [Test]
        public void SetProperty_Vector2_UnparsableValue_IsRejected()
        {
            Vector2 before = rect.anchorMin;

            bool ok = ComponentOps.SetProperty(rect, "m_AnchorMin", new JValue("not-a-vector"), out string error);

            Assert.IsFalse(ok, "An unparsable Vector2 payload must be rejected.");
            Assert.IsNotNull(error);
            Assert.IsFalse(
                error.Contains("Unsupported SerializedPropertyType"),
                $"Vector2 must no longer be reported as an unsupported type. Got: {error}");
            Assert.AreEqual(before, rect.anchorMin, "A rejected write must not mutate the property.");
        }

    }
}
