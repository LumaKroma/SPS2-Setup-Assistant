using System;
using UnityEngine;
using Sps3PathPrototype;
namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    // Preparation only. No capability gate or production generator calls this adapter yet.
    internal static class Sps3NativePathGeometry
    {
        internal readonly struct StopPose
        {
            internal readonly Vector3 Position;
            internal readonly Quaternion Rotation;
            internal readonly bool Shrink;
            internal StopPose(Vector3 p, Quaternion r, bool shrink) { Position=p; Rotation=r; Shrink=shrink; }
        }
        internal static bool TryBuild(Vector3 mouth, Vector3 anus, Vector3 throat, Vector3 hips,
            Vector3 mouthForward, Vector3 anusForward, Vector3 avatarForward, Vector3 avatarUp,
            Vector3 avatarRight, bool collapse, out StopPose[] oral, out StopPose[] reverse, out string reason)
        {
            oral=null; reverse=null; reason=null;
            try {
                var f=P(avatarForward).Unit(); var u=P(avatarUp).Unit(); var right=P(avatarRight).Unit();
                if (Math.Abs(Point.Dot(right,f))>1e-6 || Math.Abs(Point.Dot(right,u))>1e-6)
                    throw new ArgumentException("Avatar rotation axes must be orthogonal.");
                var plan=NativePathPlan.Build(P(mouth),P(anus),P(throat),P(hips),P(mouthForward),P(anusForward),f,u,collapse);
                StopPose[] Convert(Stop[] stops) {
                    var poses=new StopPose[stops.Length];
                    for(int i=0;i<stops.Length;i++) {
                        // Existing generator uses avatar.right as LookRotation up. Reject degenerate frames.
                        if(Math.Abs(Point.Dot(stops[i].Forward,right))>1-1e-6)
                            throw new ArgumentException("Path direction is parallel to the rotation up axis.");
                        var p=V(stops[i].Position); var rotation=Quaternion.LookRotation(V(stops[i].Forward),V(right));
                        if(!Finite(rotation.x)||!Finite(rotation.y)||!Finite(rotation.z)||!Finite(rotation.w))
                            throw new ArgumentException("Invalid path rotation.");
                        poses[i]=new StopPose(p,rotation,stops[i].Shrink);
                    }
                    return poses;
                }
                var a=Convert(plan.Mouth); var b=Convert(plan.Anus);
                oral=a; reverse=b; return true;
            } catch(ArgumentException e) { reason=e.Message; return false; }
        }
        private static Point P(Vector3 v)=>new Point(v.x,v.y,v.z);
        private static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        private static Vector3 V(Point p) {
            var v=new Vector3((float)p.X,(float)p.Y,(float)p.Z);
            if(!Finite(v.x)||!Finite(v.y)||!Finite(v.z)) throw new ArgumentException("Path exceeds float range.");
            return v;
        }
    }
}
