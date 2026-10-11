using nadena.dev.ndmf;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using VRC.SDK3.Avatars.Components;

[assembly: ExportsPlugin(typeof(LumaKroma.Sps2SetupAssistant.Editor.Compatibility.Ndmf.Sps2NdmfPlugin))]
namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility.Ndmf
{
    // Public NDMF API only. SPSNDMF's Transforming pass owns native generation and
    // MA merging. Do not finalize inside its active AnimatorServicesContext extension.
    public sealed class Sps2NdmfPlugin : Plugin<Sps2NdmfPlugin>
    {
        public override string QualifiedName => "com.lumakroma.sps2-setup-assistant.ndmf";
        public override string DisplayName => "SPS2 Setup Assistant";
        private sealed class Captured { public Sps2SetupContext root; }
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming).BeforePlugin("net.nrzk.spsndmf")
                .Run("Capture SPS2 ownership", context =>
                {
                    var state = context.GetState<Captured>(); state.root = null;
                    state.root = Sps2BuildIntegration.CaptureNdmf(context.AvatarRootObject);
                });
            InPhase(BuildPhase.Optimizing).BeforePlugin("com.anatawa12.avatar-optimizer")
                .Run("Finalize SPS2 menu and parameters", context =>
                {
                    var state = context.GetState<Captured>();
                    if (state.root == null) return;
                    try { Sps2BuildIntegration.Complete(context.AvatarRootObject.GetComponent<VRCAvatarDescriptor>(), state.root); }
                    finally { state.root = null; }
                });
        }
    }
}
