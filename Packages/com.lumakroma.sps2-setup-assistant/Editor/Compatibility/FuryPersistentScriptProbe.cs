using System;
using UnityEditor;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Identity evidence only. This does not prove that a component survives save/reload.
    internal static class FuryPersistentScriptProbe
    {
        internal static bool HasPersistentBinding(Type type, string expectedPackage, out string reason)
        {
            reason = null;
            if (type == null || type.IsAbstract || !typeof(MonoBehaviour).IsAssignableFrom(type))
            { reason = "Component type unavailable"; return false; }
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(type.Assembly);
            if (package == null || package.name != expectedPackage)
            { reason = "Component package identity unavailable"; return false; }
            try {
                var path = package.assetPath + "/Runtime/Component/" + type.Name + ".cs";
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null || script.GetClass() != type)
                { reason = "MonoScript is not bound to the component type"; return false; }
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string guid, out long fileId) ||
                    string.IsNullOrEmpty(guid) || fileId == 0)
                { reason = "MonoScript has no persistent asset identity"; return false; }
                return true;
            }
            catch (Exception e) { reason = "Component inspection failed: " + e.GetType().Name; return false; }
        }
    }
}
