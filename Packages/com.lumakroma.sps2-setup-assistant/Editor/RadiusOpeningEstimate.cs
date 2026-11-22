using System;
using System.Collections.Generic;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    // Deliberately conservative prototype: one planar, convex boundary around the socket axis.
    // No inference from an arbitrary bounding box or the set of moved vertices.
    internal static class RadiusOpeningEstimate
    {
        internal struct Point
        {
            internal double X, Y, Z;
            internal Point(double x, double y, double z) { X=x; Y=y; Z=z; }
        }
        internal static double[] Measure(Point[] before, Point[] after)
        {
            if (before == null || after == null || before.Length != after.Length || before.Length < 8)
                throw new InvalidOperationException("開口輪郭の点数が不足しています。");
            double factor = 0;
            for (int i=0; i<before.Length; i++) {
                var a=before[i]; var b=after[i];
                foreach(var v in new[]{a.X,a.Y,a.Z,b.X,b.Y,b.Z})
                    if(double.IsNaN(v)||double.IsInfinity(v)) throw new InvalidOperationException("非有限の頂点値です。");
                double r=Math.Sqrt(a.X*a.X+a.Y*a.Y), s=Math.Sqrt(b.X*b.X+b.Y*b.Y);
                if(r<1e-7 || s<=r+1e-7 || Math.Abs(a.Z)>r*.1 || Math.Abs(b.Z)>s*.1)
                    throw new InvalidOperationException("Socket 平面上の拡大する開口として確認できません。");
                double k=s/r;
                if(i==0) factor=k;
                // A uniform radial dilation proves monotonicity over a single linear blend frame.
                // General/asymmetric openings remain manual until a broader estimator is validated.
                if(Math.Abs(k-factor)>1e-4 || Math.Abs(b.X-k*a.X)>r*1e-4 || Math.Abs(b.Y-k*a.Y)>r*1e-4 || Math.Abs(b.Z-a.Z)>r*1e-4)
                    throw new InvalidOperationException("非単調または非一様な変形のため、自動推定対象外です。");
            }
            double zero=Inscribed(before), full=Inscribed(after);
            if(full<=zero) throw new InvalidOperationException("開口半径が増加していません。");
            return new[]{zero,full};
        }
        private static double Inscribed(Point[] ring)
        {
            double min=double.MaxValue,max=0,sign=0,angle=0;
            for(int i=0;i<ring.Length;i++) {
                var a=ring[i]; var b=ring[(i+1)%ring.Length]; var c=ring[(i+2)%ring.Length];
                double cross=a.X*b.Y-a.Y*b.X;
                if(Math.Abs(cross)<1e-12) throw new InvalidOperationException("軸を囲む開口ではありません。");
                if(sign==0) sign=Math.Sign(cross);
                if(Math.Sign(cross)!=sign || ((b.X-a.X)*(c.Y-b.Y)-(b.Y-a.Y)*(c.X-b.X))*sign<=0)
                    throw new InvalidOperationException("非凸または軸外の開口です。");
                angle+=Math.Atan2(cross,a.X*b.X+a.Y*b.Y);
                double dx=b.X-a.X,dy=b.Y-a.Y,len=dx*dx+dy*dy;
                if(len<1e-14) throw new InvalidOperationException("重複した輪郭点です。");
                double t=Math.Max(0,Math.Min(1,-(a.X*dx+a.Y*dy)/len));
                min=Math.Min(min,Math.Sqrt(Math.Pow(a.X+t*dx,2)+Math.Pow(a.Y+t*dy,2)));
                max=Math.Max(max,Math.Sqrt(a.X*a.X+a.Y*a.Y));
            }
            if(Math.Abs(Math.Abs(angle)-Math.PI*2)>1e-5 || min/max<.5)
                throw new InvalidOperationException("開口の巻き数または形状が推定条件外です。");
            return min;
        }
        // Only identical trajectories are duplicates. Near layers/seams are not merged.
        internal static List<int[]> DistinctTrajectories(List<int[]> loops, Point[] before, Point[] after)
        {
            if(loops==null||before==null||after==null||before.Length!=after.Length)
                throw new InvalidOperationException("Missing contour trajectory.");
            bool Equal(Point a,Point b)=>a.X==b.X&&a.Y==b.Y&&a.Z==b.Z;
            bool Match(int a,int b)=>Equal(before[a],before[b])&&Equal(after[a],after[b]);
            var unique=new List<int[]>();
            var buckets=new Dictionary<string,List<int[]>>();
            foreach(var loop in loops) {
                if(loop==null||loop.Length<3)throw new InvalidOperationException("Invalid contour.");
                foreach(int id in loop) {
                    if(id<0||id>=before.Length)throw new InvalidOperationException("Invalid contour vertex.");
                    foreach(double value in new[]{before[id].X,before[id].Y,before[id].Z,after[id].X,after[id].Y,after[id].Z})
                        if(double.IsNaN(value)||double.IsInfinity(value))throw new InvalidOperationException("Nonfinite trajectory.");
                }
                // Rotation/reversal invariant coarse key; collisions still require full exact comparison.
                int sum=0,xor=0;
                unchecked { foreach(int id in loop) {
                    int hash=17;
                    foreach(double v in new[]{before[id].X,before[id].Y,before[id].Z,after[id].X,after[id].Y,after[id].Z})hash=hash*31+v.GetHashCode();
                    sum+=hash;xor^=hash;
                }}
                string key=loop.Length+":"+sum+":"+xor;
                if(!buckets.TryGetValue(key,out var bucket)){bucket=new List<int[]>();buckets.Add(key,bucket);}
                bool duplicate=false;
                foreach(var candidate in bucket) {
                    if(candidate.Length!=loop.Length)continue;
                    for(int start=0;start<loop.Length&&!duplicate;start++) {
                        if(!Match(loop[0],candidate[start]))continue;
                        foreach(int direction in new[]{1,-1}) {
                            bool same=true;
                            for(int i=0;i<loop.Length&&same;i++)same=Match(loop[i],candidate[(start+direction*i+loop.Length)%loop.Length]);
                            if(same){duplicate=true;break;}
                        }
                    }
                    if(duplicate)break;
                }
                if(!duplicate){unique.Add(loop);bucket.Add(loop);}
            }
            return unique;
        }
        internal static List<int[]> Boundaries(int[] triangles, int vertices) => Boundaries(triangles,vertices,null);
        // Restrict only by observed movement of each entire connected boundary component.
        // Stationary branches cannot affect this linear blend frame; moving branches still fail.
        internal static List<int[]> Boundaries(int[] triangles, int vertices, Func<int,bool> moving)
        {
            if(triangles==null || triangles.Length%3!=0) throw new InvalidOperationException("三角形データが不正です。");
            var edges=new Dictionary<long,int>();
            for(int i=0;i<triangles.Length;i+=3) for(int j=0;j<3;j++) {
                int a=triangles[i+j],b=triangles[i+(j+1)%3];
                if(a<0||b<0||a>=vertices||b>=vertices||a==b) throw new InvalidOperationException("不正な辺です。");
                long key=((long)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);
                edges.TryGetValue(key,out int count); edges[key]=count+1;
                if(count>=2) throw new InvalidOperationException("非多様体のメッシュです。");
            }
            var graph=new Dictionary<int,List<int>>();
            foreach(var pair in edges) if(pair.Value==1) {
                int a=(int)(pair.Key>>32), b=(int)pair.Key;
                if(!graph.ContainsKey(a))graph[a]=new List<int>(); if(!graph.ContainsKey(b))graph[b]=new List<int>();
                graph[a].Add(b); graph[b].Add(a);
            }
            if(moving!=null) {
                var seen=new HashSet<int>();var keep=new HashSet<int>();
                foreach(int start in graph.Keys) {
                    if(!seen.Add(start))continue;
                    var pending=new Queue<int>();pending.Enqueue(start);var component=new List<int>();bool changed=false;
                    while(pending.Count>0){int id=pending.Dequeue();component.Add(id);changed|=moving(id);
                        foreach(int next in graph[id])if(seen.Add(next))pending.Enqueue(next);}
                    if(changed)foreach(int id in component)keep.Add(id);
                }
                var selected=new Dictionary<int,List<int>>();foreach(int id in keep)selected[id]=graph[id];graph=selected;
            }
            foreach(var neighbors in graph.Values) if(neighbors.Count!=2) throw new InvalidOperationException("分岐する開口輪郭です。");
            var visited=new HashSet<int>(); var result=new List<int[]>();
            foreach(int start in graph.Keys) {
                if(visited.Contains(start))continue;
                var loop=new List<int>(); int current=start,previous=-1;
                do { if(!visited.Add(current))throw new InvalidOperationException("輪郭が自己交差しています。");
                    loop.Add(current); var n=graph[current]; int next=n[0]==previous?n[1]:n[0]; previous=current;current=next;
                }while(current!=start);
                result.Add(loop.ToArray());
            }
            return result;
        }
    }
}
