using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using LumaKroma.Sps2SetupAssistant.Editor.Planning;
using LumaKroma.Sps2SetupAssistant.Editor.Model;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    public static partial class FullSetupGenerator
    {
        private static void ValidateNativePathBeforeMutation(Sps2SetupContext old, VRCAvatarDescriptor avatar,
            SetupSettings settings, bool regenerate, Dictionary<string,Placement> placements, BodyBasis basis, AvatarSurface surface)
        {
            if(!settings.penetration || !placements.Keys.Any(k=>k=="mouth"||k=="anus")) return;
            bool rebuild=old==null || regenerate || old.settings.penetration!=settings.penetration ||
                !new HashSet<string>(old.sockets.Select(s=>s.id)).SetEquals(placements.Keys);
            if(!rebuild) return; // Unrelated Apply must not reinterpret or rewrite an existing path.
            var animator=avatar.GetComponent<Animator>();
            var throat=animator.GetBoneTransform(HumanBodyBones.Neck) ?? animator.GetBoneTransform(HumanBodyBones.Head);
            var hips=animator.GetBoneTransform(HumanBodyBones.Hips);
            if(throat==null||hips==null) throw new InvalidOperationException("SPS3 path requires Neck/Head and Hips; existing data is unchanged.");
            var center=throat.position; float reach=Vector3.Distance(center,hips.position);
            if(surface.Ray(center,avatar.transform.forward,reach,out var front) && surface.Ray(center,-avatar.transform.forward,reach,out var back)) center=(front+back)*.5f;
            Placement Pose(string id) {
                var existing=!regenerate && placements.ContainsKey(id)?old?.sockets.Find(s=>s.id==id):null;
                if(existing?.pose!=null) return new Placement {position=existing.pose.position,rotation=existing.pose.rotation};
                return placements.TryGetValue(id,out var p)?p:Place(new SocketSettings{id=id},avatar,basis,surface);
            }
            var mouth=Pose("mouth"); var anus=Pose("anus");
            if(mouth==null||anus==null) throw new InvalidOperationException("SPS3 path endpoint unavailable; existing data is unchanged.");
            if(!Sps3NativePathGeometry.TryBuild(mouth.position,anus.position,center,hips.position,
                mouth.rotation*Vector3.forward,anus.rotation*Vector3.forward,avatar.transform.forward,avatar.transform.up,
                avatar.transform.right,!settings.showInternalThickness,out _,out _,out var error))
                throw new InvalidOperationException("SPS3 path: "+error);
        }
    }
}
