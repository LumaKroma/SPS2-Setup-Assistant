using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Reviewed source boundaries; no native service is replaced or invoked.
    internal static class Sps3MenuLayoutSources
    {
        private static void Pin(string root, string file, string expected) {
            using var sha = SHA256.Create();
            var actual=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(root,file)))).Replace("-","").ToLowerInvariant();
            if(actual!=expected)throw new InvalidOperationException("Menu layout source contract changed: "+file);
        }
        internal static bool Supported() { try { Require(); return true; } catch { return false; } }
        internal static void Require() {
            var type=VrcFuryApi.FindType("VF.Model.VRCFury");
            var package=type==null?null:UnityEditor.PackageManager.PackageInfo.FindForAssembly(type.Assembly);
            if(package==null || package.name!="com.vrcfury.vrcfury")
                throw new InvalidOperationException("Fury menu model package identity is unavailable.");
            Sps3OwnedGlobalOptions.RequirePinnedSchema();
        }
        // Read-only witness that the public NDMF BuildContext serializer will run later.
        // No dependency is added; if absent/disabled/changed, preserve the native menu.
        internal static void RequireCollector(GameObject avatar) {
            Component holder=null;
            foreach(var component in avatar.GetComponents<Component>())
                if(component!=null && component.GetType().FullName=="nadena.dev.ndmf.VRChat.ContextHolder") {
                    if(holder!=null)throw new InvalidOperationException("Ambiguous menu asset collector.");
                    holder=component;
                }
            if(holder==null)throw new InvalidOperationException("No active reviewed asset collector; native menu output is retained.");
            var package=UnityEditor.PackageManager.PackageInfo.FindForAssembly(holder.GetType().Assembly);
            if(package==null || package.name!="nadena.dev.ndmf" || package.version!="1.14.3")
                throw new InvalidOperationException("Unreviewed menu asset collector; native output is retained.");
            Pin(package.resolvedPath,"Editor/VRChat/BuildFrameworkPreprocessHook.cs","6a192be2baa0c436437176eec88dd903e4666cb5c0af90add4963f94db65ab8d");
            Pin(package.resolvedPath,"Editor/API/BuildContext.cs","bea5784f55989b438524aee5b50d8cbbc5b259563ee1901fe8decb20f9c443d6");
            Pin(package.resolvedPath,"Editor/API/Util/VisitAssets.cs","fd3d4c425519f56f2d02207d3efc856552edc0b296c8976f80914ddcd7efc8a0");
            Pin(package.resolvedPath,"Editor/API/Serialization/AssetSaver.cs","8fe037f7e1c584ba082822bb29884f702f754601866da85a82738c7c86cb29ba");
            var context=holder.GetType().GetField("context",BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(holder);
            var container=context?.GetType().GetProperty("AssetContainer")?.GetValue(context) as UnityEngine.Object;
            var root=context?.GetType().GetProperty("AvatarRootObject")?.GetValue(context) as GameObject;
            if(root!=avatar || container==null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(container)))
                throw new InvalidOperationException("The build has no persistent container for generated menu assets.");
        }
    }
}
