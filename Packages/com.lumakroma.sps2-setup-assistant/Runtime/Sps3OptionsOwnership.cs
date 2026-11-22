using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant
{
    // Authoring-only direct reference. Unity remaps it when a saved avatar prefab is instantiated.
    // This component has no runtime behavior and is stripped by the avatar SDK's editor-only contract.
    [AddComponentMenu(""), DisallowMultipleComponent]
    public sealed class Sps3OptionsOwnership : MonoBehaviour
#if VRC_SDK_VRCSDK3
        , VRC.SDKBase.IEditorOnly
#endif
    {
        [HideInInspector] public int schema = 1;
        [HideInInspector] public string setupIdentity;
        [HideInInspector] public Component options;
        // Separate from container identity schema. Missing means these fields were never owned.
        [HideInInspector] public int globalPolicyVersion;
        [HideInInspector] public int globalPolicyBaseline;
        // Opt-in only on new authoring or explicit regeneration. Missing fields remain native.
        [HideInInspector] public int menuLayoutVersion;
        [HideInInspector] public string menuSettingsLabel;
    }
}
