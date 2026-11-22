using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    internal static class CustomBlendShapeOutput
    {
        private static bool Custom(DepthActionSettings a)=>a!=null&&a.kind==DepthActionKind.BlendShape&&a.blendShapeMode==BlendShapeMode.Custom;
        internal static IEnumerable<SocketAnimationGroup> EmissionGroups(SocketSettings part)
        {
            foreach(var group in Sps3AnimationGroupNative.Ordered(part)) {
                if(group.kind==SocketAnimationKind.Active||!group.actions.Any(Custom)){yield return group;continue;}
                // Do not make ordinary static actions opt into Fury's group-wide MotionTime.
                var normal=group.Copy();normal.actions=group.actions.Where(a=>!Custom(a)).ToList();
                if(normal.actions.Count>0)yield return normal;
                foreach(var action in group.actions.Where(Custom)) {
                    var separate=group.Copy();separate.actions=new List<DepthActionSettings>{action};yield return separate;
                }
            }
        }
        internal static void Validate(VRCAvatarDescriptor avatar,SetupSettings settings)
        {
            foreach(var part in settings.parts)
                foreach(var a in Sps3AnimationGroupNative.AllSavedActions(part))
                    if(a!=null&&!string.IsNullOrEmpty(a.generatedBlendShapeSignature))RequireClip(a);
            foreach(var part in settings.parts.Where(p=>p.included&&p.AnimationEnabled))
                foreach(var group in part.EffectiveAnimationGroups().Where(g=>g!=null && g.kind!=SocketAnimationKind.Active))
                    foreach(var action in (group.actions ?? new List<DepthActionSettings>()).Where(a=>a!=null && a.kind==DepthActionKind.BlendShape)) {
                        BlendShapePointPlan.Validate(action);
                        if(!Custom(action))continue;
                        if(part.animationModelVersion!=1||settings.family!=SpsFamily.VrcFury||
                            !SpsBackend.PackageMatches(VrcFuryApi.FindType(SpsBackend.SocketType(SpsFamily.VrcFury)),SpsFamily.VrcFury,out _))
                            throw new InvalidOperationException("カスタム BlendShape に必要な SPS3 設定を確認できません。通常出力は変更しません。");
                        Sps3Beta14SocketAdapter.RequireObservedNativeContract();
                        RequireClipContract();
                        if(group.ActiveSettings.range.x>=group.ActiveSettings.range.y)throw new InvalidOperationException("カスタム曲線には幅のある入力範囲が必要です。");
                        Path(avatar,action);
                        if(action.generatedBlendShapeClip!=null)RequireClip(action);
                        else if(!string.IsNullOrEmpty(action.generatedBlendShapeSignature))throw new InvalidOperationException("保存済みカスタム Clip の参照が欠けています。再生成で消さず停止します。");
                    }
        }
        private static void RequireClipContract()
        {
            var action=VrcFuryApi.FindType("com.vrcfury.api.Actions.FuryActionSet");
            if (!SpsBackend.PackageMatches(action,SpsFamily.VrcFury,out _) ||
                VrcFuryApi.Method(action,"AddAnimationClip",typeof(AnimationClip))?.ReturnType != typeof(void))
                throw new InvalidOperationException("Fury Animation Clip API is unavailable.");
        }
        private static string Path(VRCAvatarDescriptor avatar,DepthActionSettings action)
        {
            var r=action.renderer;
            if(avatar==null||r==null||r.sharedMesh==null||!r.transform.IsChildOf(avatar.transform)||r.sharedMesh.GetBlendShapeIndex(action.shape)<0||
                r.GetComponents<SkinnedMeshRenderer>().Length!=1)throw new InvalidOperationException("アバター内の一意な Renderer と BlendShape が必要です。");
            var names=new List<string>();
            for(var t=r.transform;t!=avatar.transform;t=t.parent){
                if(t==null||t.name.Contains("/")||t.parent.Cast<Transform>().Count(c=>c.name==t.name)!=1)throw new InvalidOperationException("Clip binding の階層名が一意ではありません。");
                names.Add(t.name);
            }
            names.Reverse();return "/"+string.Join("/",names); // Fury's avatar-absolute binding; independent of Socket ancestry.
        }
        internal static AnimationClip MakeClip(DepthActionSettings action,string path)
        {
            BlendShapePointPlan.Validate(action);
            var clip=new AnimationClip{name="SPS Custom BlendShape",frameRate=60,wrapMode=WrapMode.ClampForever};
            var points=action.blendShapePoints;
            var keys=BlendShapeInterpolationPlan.Keys(action);
            // A time-zero constant clip is treated as static by the pinned Fury Auto mode.
            // Keep the value exactly constant, but encode a positive first key so it retains
            // MotionTime semantics. Clamp supplies the identical value at input zero.
            // This changes neither saved user point positions nor BlendShape values.
            if(points.All(p=>p.value==points[0].value))
                keys=new[]{new Keyframe(1f/60f,points[0].value,0,0),new Keyframe(1,points[0].value,0,0)};
            var curve=new AnimationCurve(keys){preWrapMode=WrapMode.ClampForever,postWrapMode=WrapMode.ClampForever};
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(SkinnedMeshRenderer),"blendShape."+action.shape),curve);
            return clip;
        }
        internal static string Signature(AnimationClip clip)
        {
            var bindings=AnimationUtility.GetCurveBindings(clip);
            if(bindings.Length!=1||AnimationUtility.GetObjectReferenceCurveBindings(clip).Length!=0||AnimationUtility.GetAnimationEvents(clip).Length!=0)
                throw new InvalidOperationException("生成 Clip の内容が変更されています。");
            var b=bindings[0];var curve=AnimationUtility.GetEditorCurve(clip,b);
            string Bits(float v)=>BitConverter.ToString(BitConverter.GetBytes(v));
            var text=b.path+"|"+b.type.FullName+"|"+b.propertyName+"|"+clip.legacy+"|"+clip.wrapMode+"|"+Bits(clip.frameRate)+"|"+curve.preWrapMode+"|"+curve.postWrapMode;
            // Loop/time settings are user-editable without changing curve keys. Preserve these edits too.
            text+="|"+JsonUtility.ToJson(AnimationUtility.GetAnimationClipSettings(clip));
            foreach(var k in curve.keys)text+="|"+Bits(k.time)+":"+Bits(k.value)+":"+Bits(k.inTangent)+":"+Bits(k.outTangent)+":"+k.weightedMode+":"+Bits(k.inWeight)+":"+Bits(k.outWeight);
            using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","");
        }
        internal static AnimationClip RequireClip(DepthActionSettings action)
        {
            var clip=action.generatedBlendShapeClip;
            if(clip==null||!AssetDatabase.Contains(clip)||string.IsNullOrEmpty(action.generatedBlendShapeSignature)||Signature(clip)!=action.generatedBlendShapeSignature)
                throw new InvalidOperationException("生成カスタム Clip が欠落または編集されています。元データを保全して停止します。");
            return clip;
        }
        internal static void Prepare(VRCAvatarDescriptor avatar,SetupSettings settings,Sps2SetupAsset owner)
        {
            foreach(var part in settings.parts.Where(p=>p.included&&p.AnimationEnabled&&p.animationModelVersion==1))
                foreach(var group in part.animationGroups.Where(g=>g!=null && g.kind!=SocketAnimationKind.Active))
                    foreach(var action in (group.actions ?? new List<DepthActionSettings>()).Where(Custom)){
                        if (!Sps3AnimationGroupNative.CanWriteOptional(part, group.kind, out _)) continue;
                        var clip=MakeClip(action,Path(avatar,action));
                        var signature=Signature(clip);
                        if(action.generatedBlendShapeClip!=null&&action.generatedBlendShapeSignature==signature){UnityEngine.Object.DestroyImmediate(clip);continue;}
                        // Append to the NEW settings asset only. Never rewrite/delete an old clip or saved asset.
                        AssetDatabase.AddObjectToAsset(clip,owner);
                        action.generatedBlendShapeClip=clip;action.generatedBlendShapeSignature=signature;
                        EditorUtility.SetDirty(clip);
                    }
        }
    }
}
