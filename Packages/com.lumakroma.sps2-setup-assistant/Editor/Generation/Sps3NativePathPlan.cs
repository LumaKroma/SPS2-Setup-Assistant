using System;
namespace Sps3PathPrototype
{
    // Pure world-space proposal. Not wired into production generation or an admission guard.
    internal readonly struct Point
    {
        public readonly double X,Y,Z;
        public Point(double x,double y,double z){X=x;Y=y;Z=z;}
        public bool Finite=>( !double.IsNaN(X)&&!double.IsInfinity(X)) && (!double.IsNaN(Y)&&!double.IsInfinity(Y)) && (!double.IsNaN(Z)&&!double.IsInfinity(Z));
        public double Length=>Math.Sqrt(X*X+Y*Y+Z*Z);
        public static Point operator +(Point a,Point b)=>new Point(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
        public static Point operator -(Point a,Point b)=>new Point(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        public static Point operator -(Point a)=>a*-1;
        public static Point operator *(Point a,double s)=>new Point(a.X*s,a.Y*s,a.Z*s);
        public static double Dot(Point a,Point b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        public Point Unit(){if(!Finite||Length<1e-12)throw new ArgumentException("Degenerate direction.");return this*(1/Length);}
        public static Point Lerp(Point a,Point b,double t)=>a*(1-t)+b*t;
    }
    internal readonly struct Stop
    {
        public readonly Point Position,Forward;
        // Native shrink[i] controls the segment entering stop[i], not the terminal exit.
        public readonly bool Shrink;
        public Stop(Point p,Point forward,bool shrink){Position=p;Forward=forward.Unit();Shrink=shrink;}
    }
    internal sealed class NativePathPlan
    {
        public readonly Stop[] Mouth,Anus;
        private NativePathPlan(Stop[] mouth,Stop[] anus){Mouth=mouth;Anus=anus;}
        public static NativePathPlan Build(Point mouth,Point anus,Point throat,Point hips,
            Point mouthForward,Point anusForward,Point avatarForward,Point avatarUp,bool collapse)
        {
            foreach(var p in new[]{mouth,anus,throat,hips,mouthForward,anusForward,avatarForward,avatarUp})
                if(!p.Finite)throw new ArgumentException("Nonfinite path input.");
            var f=avatarForward.Unit();var u=avatarUp.Unit();
            if(Math.Abs(Point.Dot(f,u))>1e-6)throw new ArgumentException("Avatar axes must be orthogonal.");
            double bend=(mouth-throat).Length;
            if(bend<1e-9||(throat-hips).Length<1e-9||(anus-throat).Length<1e-9)
                throw new ArgumentException("Coincident path references.");
            var c1=mouth-f*(bend*.8)+u*(bend*.2);
            var c2=throat+u*(bend*.8)+f*(bend*.16);
            var q0=Point.Lerp(mouth,c1,.4);var q1=Point.Lerp(c1,c2,.4);var q2=Point.Lerp(c2,throat,.4);
            var r0=Point.Lerp(q0,q1,.4);var r1=Point.Lerp(q1,q2,.4);var inside=Point.Lerp(r0,r1,.4);
            mouthForward.Unit();anusForward.Unit();
            return new NativePathPlan(new[]{
                new Stop(inside,r0-r1,false),new Stop(throat,throat-hips,collapse),new Stop(anus,-anusForward,collapse)
            },new[]{
                new Stop(throat,hips-throat,collapse),new Stop(inside,r1-r0,collapse),new Stop(mouth,-mouthForward,false)
            });
        }
    }
}
