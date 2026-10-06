using System.Collections.Generic;
using UnityEngine;

namespace MCPForUnityTests.Editor.Tools.Fixtures
{
    // Stand-in for types such as AddressableAssetGroup that rebuild their serialized arrays in
    // OnAfterDeserialize. NOTE: File name matches class name so Unity can resolve a MonoScript.
    public class ManageScriptableObjectCallbackReceiverDefinition : ManageScriptableObjectTestDefinitionBase, ISerializationCallbackReceiver
    {
        [SerializeField] private List<string> entries = new();

        public IReadOnlyList<string> Entries => entries;

        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize() { }
    }
}
