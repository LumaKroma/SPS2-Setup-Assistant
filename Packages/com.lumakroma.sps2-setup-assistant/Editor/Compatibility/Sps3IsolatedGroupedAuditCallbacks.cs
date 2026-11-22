using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;
namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility {
    // Independent bridge in existing Editor assembly: test assembly has no SDKBase.Editor reference.
    // Inert unless the explicit test-side Arm method has admitted one isolated run. Never changes native return values.
    internal static class Sps3IsolatedGroupedAuditBridge {
        internal static bool Observe(GameObject avatar,string phase) {
            var assembly=AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a=>a.GetName().Name=="LumaKroma.Sps2SetupAssistant.Editor.Tests");
            var type=assembly?.GetType("LumaKroma.Sps2SetupAssistant.Editor.Tests.Sps3IsolatedGroupedAudit",false);
            if(type==null)return true;
            try {
                var method=type.GetMethod("Observe",BindingFlags.Public|BindingFlags.Static,null,new[]{typeof(GameObject),typeof(string)},null);
                if(method==null)throw new InvalidOperationException("Installed grouped audit observer API differs.");
                return (bool)method.Invoke(null,new object[]{avatar,phase});
            } catch(Exception e) {Debug.LogError("Grouped SDK observer bridge failed: "+e);return false;}
        }
    }
    internal sealed class Sps3GroupedBeforeFury { public int callbackOrder=>-10002;public bool OnPreprocessAvatar(GameObject avatar)=>Sps3IsolatedGroupedAuditBridge.Observe(avatar,"before-fury"); }
    internal sealed class Sps3GroupedNativeMenu { public int callbackOrder=>-9999;public bool OnPreprocessAvatar(GameObject avatar)=>Sps3IsolatedGroupedAuditBridge.Observe(avatar,"native"); }
    internal sealed class Sps3GroupedArrangedMenu { public int callbackOrder=>-9997;public bool OnPreprocessAvatar(GameObject avatar)=>Sps3IsolatedGroupedAuditBridge.Observe(avatar,"arranged"); }
    internal sealed class Sps3GroupedSerializedOutput { public int callbackOrder=>-1024;public bool OnPreprocessAvatar(GameObject avatar)=>Sps3IsolatedGroupedAuditBridge.Observe(avatar,"serialized"); }
}
