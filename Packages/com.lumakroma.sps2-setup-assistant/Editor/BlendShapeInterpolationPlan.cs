using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    // Circular mode is an explicit prototype definition, not quaternion Slerp.
    internal static class BlendShapeInterpolationPlan
    {
        internal const double CircularTolerance = .05; // BlendShape percentage points; circular join has infinite slope
        internal static double Ease(BlendShapeInterpolation mode, double t)
        {
            if (double.IsNaN(t)||double.IsInfinity(t)||t<0||t>1) throw new ArgumentOutOfRangeException(nameof(t));
            switch(mode) {
                case BlendShapeInterpolation.Linear: return t;
                case BlendShapeInterpolation.Smooth: return t*t*(3-2*t);
                case BlendShapeInterpolation.CircularInOut:
                    return t<=.5 ? (1-Math.Sqrt(Math.Max(0,1-4*t*t)))/2
                        : (Math.Sqrt(Math.Max(0,1-4*(1-t)*(1-t)))+1)/2;
                default: throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }
        internal static Keyframe[] Keys(DepthActionSettings action)
        {
            BlendShapePointPlan.Validate(action);
            var points=action.blendShapePoints;
            if(action.blendShapeInterpolation!=BlendShapeInterpolation.CircularInOut) {
                var result=new Keyframe[points.Count];
                for(int i=0;i<result.Length;i++) {
                    float before=0,after=0;
                    if(action.blendShapeInterpolation==BlendShapeInterpolation.Linear) {
                        if(i>0)before=(points[i].value-points[i-1].value)/(points[i].position-points[i-1].position);
                        if(i<result.Length-1)after=(points[i+1].value-points[i].value)/(points[i+1].position-points[i].position);
                    }
                    result[i]=new Keyframe(points[i].position,points[i].value,before,after){weightedMode=WeightedMode.None};
                }
                return result;
            }
            var samples=new List<Vector2>{new Vector2(points[0].position,points[0].value)};
            for(int i=0;i<points.Count-1;i++) {
                var a=points[i];var b=points[i+1];
                // Split at the vertical-tangent join: never extrapolate a cubic through it.
                Add(samples,a,b,0,.5,0);Add(samples,a,b,.5,1,0);
            }
            var keys=new Keyframe[samples.Count];
            for(int i=0;i<keys.Length;i++) {
                float before=i==0?0:(samples[i].y-samples[i-1].y)/(samples[i].x-samples[i-1].x);
                float after=i==keys.Length-1?0:(samples[i+1].y-samples[i].y)/(samples[i+1].x-samples[i].x);
                if(float.IsNaN(before)||float.IsInfinity(before)||float.IsNaN(after)||float.IsInfinity(after))
                    throw new InvalidOperationException("Circular interpolation cannot resolve this point spacing safely.");
                // Unity's unweighted evaluation loses precision for the very short
                // intervals around the circular vertical tangent. One-third Bezier
                // weights retain the same linear segment while using its stable evaluator.
                keys[i]=new Keyframe(samples[i].x,samples[i].y,before,after){weightedMode=WeightedMode.Both,inWeight=1f/3f,outWeight=1f/3f};
            }
            return keys;
        }
        // Bound chord error at the analytic extrema, using representable key times.
        private static double ChordError(double lo, double hi, double v0, double v1)
        {
            double slope=(v1-v0)/(hi-lo);
            double t=slope/(2*Math.Sqrt(1+slope*slope));
            double error=0;
            foreach(double x in new[]{lo,hi,.5,t,1-t}) {
                if(x<lo||x>hi)continue;
                error=Math.Max(error,Math.Abs(Ease(BlendShapeInterpolation.CircularInOut,x)-(v0+slope*(x-lo))));
            }
            return error;
        }
        private static void Add(List<Vector2> samples,BlendShapePoint a,BlendShapePoint b,double lo,double hi,int depth)
        {
            if(samples.Count>=16384)throw new InvalidOperationException("Circular interpolation exceeds the 16384 key safety limit.");
            double width=(double)b.position-a.position;
            float left=samples[samples.Count-1].x;
            float right=(float)(a.position+width*hi);
            if(right<=left)throw new InvalidOperationException("Circular interpolation point spacing is below float precision.");
            double u0=((double)left-a.position)/width,u1=((double)right-a.position)/width;
            double v0=Ease(BlendShapeInterpolation.CircularInOut,u0),v1=Ease(BlendShapeInterpolation.CircularInOut,u1);
            double error=ChordError(u0,u1,v0,v1)*Math.Abs((double)b.value-a.value);
            // Keep 0.005 points for key/value rounding and Unity evaluation; total stays 0.05.
            if(error>CircularTolerance-.005) {
                float mid=(float)(((double)left+right)*.5);
                if(depth>=26||samples.Count>=16384||mid<=left||mid>=right)
                    throw new InvalidOperationException("Circular interpolation cannot meet the 0.05 precision bound at this point spacing.");
                double split=((double)mid-a.position)/width;
                Add(samples,a,b,u0,split,depth+1);Add(samples,a,b,split,u1,depth+1);return;
            }
            samples.Add(new Vector2(right,(float)(a.value+((double)b.value-a.value)*v1)));
        }
    }
}
