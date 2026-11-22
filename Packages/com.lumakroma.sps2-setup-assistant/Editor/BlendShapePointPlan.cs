using System;
using System.Linq;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    internal static class BlendShapePointPlan
    {
        internal static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        internal static void Validate(DepthActionSettings action)
        {
            if(action.blendShapeMode==BlendShapeMode.Normal)return;
            if(action.blendShapeMode!=BlendShapeMode.Custom)throw new InvalidOperationException("未知の BlendShape モードです。");
            if (!Enum.IsDefined(typeof(BlendShapeInterpolation),action.blendShapeInterpolation)) throw new InvalidOperationException("Unknown interpolation mode.");
            var points=action.blendShapePoints;
            if(points==null||points.Count<2||points.Any(p=>p==null))throw new InvalidOperationException("端点を含む2点以上が必要です。");
            if(points[0].position!=0||points[points.Count-1].position!=1)throw new InvalidOperationException("端点の入力位置は 0 と 1 が必要です。");
            for(int i=0;i<points.Count;i++)if(!Finite(points[i].position)||!Finite(points[i].value)||points[i].position<0||points[i].position>1||
                points[i].value<0||points[i].value>100||i>0&&points[i-1].position>=points[i].position)
                throw new InvalidOperationException("入力位置は昇順・重複なし、BlendShape 値は 0～100 が必要です。保存値は自動補正しません。");
        }
        internal static float Input(float progress,float min,float max,bool depth)=>depth?max+(min-max)*progress:min+(max-min)*progress;
        internal static float Progress(float input,float min,float max,bool depth)
        {
            if(!Finite(input)||!Finite(min)||!Finite(max)||min>=max)throw new InvalidOperationException("有効な入力範囲が必要です。");
            return depth?(max-input)/(max-min):(input-min)/(max-min);
        }
        internal static bool Same(DepthActionSettings a,DepthActionSettings b)
        {
            if(a.blendShapeMode!=b.blendShapeMode)return false;
            if(a.blendShapeMode==BlendShapeMode.Normal)return true; // dormant custom bank does not change normal output
            return a.blendShapeInterpolation==b.blendShapeInterpolation && a.blendShapePoints!=null&&b.blendShapePoints!=null&&a.blendShapePoints.Count==b.blendShapePoints.Count&&
                a.blendShapePoints.Zip(b.blendShapePoints,(x,y)=>x!=null&&y!=null&&x.position==y.position&&x.value==y.value).All(v=>v);
        }
    }
}
