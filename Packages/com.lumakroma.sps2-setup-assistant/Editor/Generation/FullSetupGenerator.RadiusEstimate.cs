using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using System;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using LumaKroma.Sps2SetupAssistant.Editor.Planning;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    public static partial class FullSetupGenerator
    {
        internal static Matrix4x4 RadiusEstimateFrame(VRCAvatarDescriptor avatar, SetupSettings settings, SocketSettings part, bool newPlacement = false)
        {
            if(avatar==null) throw new InvalidOperationException("アバターを選択してください。");
            var old=Find(avatar);
            var socket=old?.sockets?.SingleOrDefault(s=>s.id==part.id)?.socket;
            if(socket!=null && !newPlacement) { RequireRadiusFrame(socket.transform.localToWorldMatrix); return socket.transform.worldToLocalMatrix; }
            // New placement uses the valid, uniform avatar-root frame for the visible socket.
            // The preview and saved radius describe the same visible authoring-scene geometry.
            if(part.IsFingerRing) throw new InvalidOperationException("指輪の自動補正は未検証です。");
            if(!HumanoidSnapshot.TryCapture(avatar,out var snapshot,out var error))throw new InvalidOperationException(error);
            using var surface=new AvatarSurface(avatar);
            var placement=Place(part,avatar,BodyBasisBuilder.Build(snapshot),surface);
            if(placement==null||placement.first==null||placement.second!=null)throw new InvalidOperationException("Socket 配置を確定できません。");
            // Validate the source hierarchy without interpreting nonuniform scale as a radius.
            SpsAuthoringCompatibility.RequireSupportedScaleChain(placement.first);
            RequireRadiusFrame(avatar.transform.localToWorldMatrix);
            bool nonunit=false;
            foreach(var start in new[]{avatar.transform,placement.first})
                for(var t=start;t!=null;t=t.parent) {
                    var scale=t.localScale;
                    if((scale-Vector3.one).sqrMagnitude>1e-10f) nonunit=true;
                }
            // The non-unit local-radius mapping has only been verified for native Fury SPS3.
            if(nonunit && (settings.family!=SpsFamily.VrcFury ||
                           SpsAuthoringCompatibility.SelectContract(settings.family,old,newPlacement)!=SpsAuthoringContract.Sps3Beta14))
                throw new InvalidOperationException("この Socket のスケールと半径の対応を確認できないため、既定値を保持します。");
            // Use the authoring-scene socket frame, independently of its later build-time attachment.
            var socketScale=avatar.transform.lossyScale.x;
            return Matrix4x4.TRS(placement.position,placement.rotation,Vector3.one*socketScale).inverse;
        }
        internal static void RequireRadiusFrame(Matrix4x4 m)
        {
            var x=m.MultiplyVector(Vector3.right);var y=m.MultiplyVector(Vector3.up);var z=m.MultiplyVector(Vector3.forward);
            float s=x.magnitude;
            if(float.IsNaN(s)||float.IsInfinity(s)||s<1e-6f||Mathf.Abs(y.magnitude-s)>s*1e-5f||Mathf.Abs(z.magnitude-s)>s*1e-5f||
                Mathf.Abs(Vector3.Dot(x,y))>s*s*1e-5f||Mathf.Abs(Vector3.Dot(x,z))>s*s*1e-5f||Mathf.Abs(Vector3.Dot(y,z))>s*s*1e-5f||m.determinant<=0)
                throw new InvalidOperationException("反転・非一様 scale / shear は自動補正できません。");
        }
    }
}
