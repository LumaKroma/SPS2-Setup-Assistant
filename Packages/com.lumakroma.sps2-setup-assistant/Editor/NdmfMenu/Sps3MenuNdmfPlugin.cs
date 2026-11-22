using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;

// Intentionally not exported: leave menu arrangement to native authoring features.
namespace LumaKroma.Sps2SetupAssistant.NdmfMenu
{
    public sealed class Sps3MenuNdmfPlugin : Plugin<Sps3MenuNdmfPlugin>
    {
        public override string QualifiedName => "com.lumakroma.sps-setup.menu-layout";
        protected override void Configure()
        {
            // No build pass: menu changes must be authored with native tools.
        }
    }
}
