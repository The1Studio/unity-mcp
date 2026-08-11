using UnityEngine;

namespace TestNamespace
{
    /// <summary>
    /// Minimal component used by GameObjectComponentHelpers / ComponentOps property-write
    /// regression tests (issues #19 / #42). Exposes private [SerializeField] members of the
    /// specific kinds those issues reported as silently dropped: a float, a LayerMask struct,
    /// and a UnityEngine.Object reference. Public read-only accessors let tests assert on the
    /// actual field value without going through Unity's serialization/inspector layer.
    /// </summary>
    public class PropertyWriteTestComponent : MonoBehaviour
    {
        [SerializeField]
        private float _radius = 1f;

        [SerializeField]
        private LayerMask _layerMask;

        [SerializeField]
        private GameObject _model;

        /// <summary>
        /// Public struct-typed field, used to exercise dotted-path writes through a
        /// value-type intermediate (e.g. "PublicLayerMask.value") — reflection's
        /// GetValue() on a struct member returns a boxed copy, so a dotted-path setter
        /// must write the mutated copy back into this field, not just the copy.
        /// </summary>
        public LayerMask PublicLayerMask;

        public float RadiusValue => _radius;
        public LayerMask LayerMaskValue => _layerMask;
        public GameObject ModelValue => _model;
    }
}
