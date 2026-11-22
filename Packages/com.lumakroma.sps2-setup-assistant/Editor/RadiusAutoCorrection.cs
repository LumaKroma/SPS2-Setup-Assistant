using System;
using System.Linq;
using System.Collections.Generic;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object=UnityEngine.Object;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    internal static class RadiusAutoCorrection
    {
        // BakeMesh(false) emits vertices in its translation/rotation frame.
        // Applying lossyScale here would scale the skinned result a second time.
        internal static Matrix4x4 BakedToSocket(Transform renderer,Matrix4x4 worldToSocket)
            => worldToSocket*Matrix4x4.TRS(renderer.position,renderer.rotation,Vector3.one);

        internal static void ResolvePreview(SocketAnimationSettings value,Func<Vector2> estimate,Func<Vector2> nativeDefault)
        {
            if (!value.radiusAutoCorrection) throw new InvalidOperationException("Automatic radius is off.");
            try { var result=estimate(); value.RecordRadiusAutoResult(result.x,result.y,false,""); }
            catch(InvalidOperationException error) {
                try { var result=nativeDefault(); value.RecordRadiusAutoResult(result.x,result.y,true,
                    "開口の推定不可: "+error.Message+" / Fury標準値を使用しています。"); }
                catch(InvalidOperationException unavailable) {
                    value.radiusAutoMessage="今回の計算は未適用: "+error.Message+" / Fury標準値も未確定: "+unavailable.Message+
                        "。前回結果または保存値を保持しています。";
                }
            }
        }
        internal static Vector2 Estimate(VRCAvatarDescriptor avatar,SetupSettings setup,SocketSettings part,SocketAnimationGroup group,bool regenerate=false)
        {
            if(group==null || group.kind!=SocketAnimationKind.Depth || group.actions==null || group.actions.Count==0 ||
                group.actions.Any(x=>x==null || x.kind!=DepthActionKind.BlendShape || x.blendShapeMode!=BlendShapeMode.Normal))
                throw new InvalidOperationException("通常 BlendShape のみを含むアクションが必要です。");
            var source=group.actions[0].renderer;var mesh=source!=null?source.sharedMesh:null;
            if(source==null||mesh==null||avatar==null||!source.transform.IsChildOf(avatar.transform)||!mesh.isReadable||mesh.vertexCount>200000)
                throw new InvalidOperationException("対象アバター内の読み取り可能な単一 Renderer が必要です。");
            var shapes=new List<KeyValuePair<int,float>>(); var unique=new HashSet<int>();
            foreach(var action in group.actions) {
                if(action.renderer!=source) throw new InvalidOperationException("複数 Renderer の合成は推定できません。");
                int shape=mesh.GetBlendShapeIndex(action.shape);
                if(shape<0 || !unique.Add(shape)) throw new InvalidOperationException("BlendShape の欠落または重複があります。");
                if(mesh.GetBlendShapeFrameCount(shape)!=1||!Mathf.Approximately(mesh.GetBlendShapeFrameWeight(shape,0),100)||
                   action.weight<=0||action.weight>100||float.IsNaN(action.weight)||float.IsInfinity(action.weight))
                    throw new InvalidOperationException("単一 100 フレーム、0 より大きく 100 以下の値のみ推定できます。");
                shapes.Add(new KeyValuePair<int,float>(shape,action.weight));
            }
            foreach(var otherPart in setup.parts.Where(p=>p.included&&p.AnimationEnabled))
                foreach(var otherGroup in otherPart.EffectiveAnimationGroups())
                    foreach(var other in otherGroup.actions)
                        if(other!=null && !group.actions.Any(x=>ReferenceEquals(x,other)) && (other.kind==DepthActionKind.AnimationClip ||
                            other.kind==DepthActionKind.BlendShape && other.renderer==source))
                            throw new InvalidOperationException("別のアクションとの合成を安全に推定できません。");
            for(int i=0;i<mesh.blendShapeCount;i++)if(source.GetBlendShapeWeight(i)!=0)
                throw new InvalidOperationException("現在の BlendShape が有効です。複合変形を推定しません。");
            FullSetupGenerator.RequireRadiusFrame(source.transform.localToWorldMatrix);
            var worldToSocket=FullSetupGenerator.RadiusEstimateFrame(avatar,setup,part,regenerate);
            GameObject temp=null,surfaceProbe=null;Mesh before=null,after=null; UnityEngine.SceneManagement.Scene surfaceScene=default;
            try {
                // Bake on an owned scratch renderer only. Never change the source renderer, weights or mesh.
                temp=new GameObject("SPS radius estimate scratch"){hideFlags=HideFlags.HideAndDontSave};temp.SetActive(false);
                temp.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
                temp.transform.localScale=source.transform.lossyScale;
                var copy=temp.AddComponent<SkinnedMeshRenderer>();copy.sharedMesh=mesh;copy.bones=source.bones;copy.rootBone=source.rootBone;copy.quality=source.quality;
                before=new Mesh{hideFlags=HideFlags.HideAndDontSave};after=new Mesh{hideFlags=HideFlags.HideAndDontSave};
                foreach(var shape in shapes) copy.SetBlendShapeWeight(shape.Key,0);
                copy.BakeMesh(before,false);
                foreach(var shape in shapes) copy.SetBlendShapeWeight(shape.Key,shape.Value);
                copy.BakeMesh(after,false);
                var a=before.vertices;var b=after.vertices;
                if(a.Length!=mesh.vertexCount||b.Length!=a.Length)throw new InvalidOperationException("BakeMesh の頂点数が一致しません。");
                var loops=ApproximateBoundaries(mesh.triangles,mesh.vertexCount,i=>(a[i]-b[i]).sqrMagnitude>1e-14f);
                var toSocket=BakedToSocket(copy.transform,worldToSocket);
                // Compare both baked endpoints; no tolerance-based welding or original mesh mutation.
                RadiusOpeningEstimate.Point Raw(Vector3 p)=>new RadiusOpeningEstimate.Point(p.x,p.y,p.z);
                loops=RadiusOpeningEstimate.DistinctTrajectories(loops,a.Select(Raw).ToArray(),b.Select(Raw).ToArray());
                // Rank all measurable moving contours by the same broad diagnostic approximation.
                // Scores favor contours near the socket origin and plane, without demanding symmetry.
                RadiusOpeningEstimate.Point Convert(Vector3 v){var p=toSocket.MultiplyPoint3x4(v);return new RadiusOpeningEstimate.Point(p.x,p.y,p.z);}
                double Area(RadiusOpeningEstimate.Point[] p) { double v=0;for(int i=0;i<p.Length;i++){var x=p[i];var y=p[(i+1)%p.Length];v+=x.X*y.Y-y.X*x.Y;}return Math.Abs(v)*.5; }
                                // Experimental ranking only: compare candidates to the outer surface
                // of the same owned baked mesh. If sampling is unavailable, retain
                // the original broad ranking rather than rejecting the estimate.
                MeshCollider surfaceCollider=null;
                try {
                    surfaceScene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
                    surfaceProbe=new GameObject("SPS radius surface ranking scratch"){hideFlags=HideFlags.HideAndDontSave};
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(surfaceProbe,surfaceScene);
                    surfaceProbe.transform.SetPositionAndRotation(copy.transform.position,copy.transform.rotation);
                    surfaceCollider=surfaceProbe.AddComponent<MeshCollider>();surfaceCollider.sharedMesh=after;
                    if(surfaceCollider.sharedMesh==null) surfaceCollider=null;
                } catch(Exception error) when(error is InvalidOperationException || error is ArgumentException || error is UnityException) {
                    if(surfaceProbe!=null) Object.DestroyImmediate(surfaceProbe);
                    if(surfaceScene.IsValid() && surfaceScene.isLoaded) UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(surfaceScene);
                    surfaceProbe=null; surfaceScene=default; surfaceCollider=null;
                    Debug.LogWarning("SPS radius surface ranking unavailable; using the existing contour ranking: "+error.Message);
                }

                var probeForward=worldToSocket.inverse.MultiplyVector(Vector3.forward).normalized;
                var bakeToWorld=Matrix4x4.TRS(copy.transform.position,copy.transform.rotation,Vector3.one);
                double best=double.PositiveInfinity;double[] result=null;
                foreach(var loop in loops) {
                    if(loop.Length<3 || !loop.Any(i=>(a[i]-b[i]).sqrMagnitude>1e-14f))continue;
                    var closed=loop.Select(i=>Convert(a[i])).ToArray();var opened=loop.Select(i=>Convert(b[i])).ToArray();
                    if(closed.Concat(opened).Any(p=>double.IsNaN(p.X)||double.IsInfinity(p.X)||double.IsNaN(p.Y)||double.IsInfinity(p.Y)||double.IsNaN(p.Z)||double.IsInfinity(p.Z)))continue;
                    var r0=Math.Sqrt(Area(closed)/Math.PI);var r1=Math.Sqrt(Area(opened)/Math.PI);
                    if(double.IsNaN(r1)||double.IsInfinity(r1)||r1<1e-10)continue;
                    double x=opened.Average(p=>p.X),y=opened.Average(p=>p.Y),z=opened.Average(p=>p.Z);
                    var score=Math.Sqrt(x*x+y*y)/r1+Math.Abs(z)/r1+Math.Max(0,r0-r1)/r1+3.0/loop.Length;
                    var depths=new List<float>();
                    if(surfaceCollider!=null) foreach(var i in loop) {
                        var pt=bakeToWorld.MultiplyPoint3x4(b[i]);
                        if(surfaceCollider.Raycast(new Ray(pt+probeForward*.1f,-probeForward),out var hit,.2f))
                            depths.Add(Vector3.Dot(hit.point-pt,probeForward));
                    }
                    if(depths.Count>=loop.Length*.8f) { depths.Sort();score+=2.0*Math.Max(0,depths[depths.Count/2])/r1; }
                    if(score<best){best=score;result=new[]{Math.Min(r0,r1),r1};}
                }
                if(result==null)throw new InvalidOperationException("No finite moving opening contour; using native fallback.");
                if(SpsBackend.PackageMatches(VrcFuryApi.FindType(SpsBackend.SocketType(SpsFamily.VrcFury)),SpsFamily.VrcFury,out _) &&
                    !FuryNativeFeatureShapes.InspectInstalled().RadiusZero && result[0]!=0)
                    throw new InvalidOperationException("このFury版は重み0の半径を設定できません。標準の0始点へフォールバックします。");
                return new Vector2((float)result[0],(float)result[1]);
            } finally {
                if(surfaceProbe!=null)Object.DestroyImmediate(surfaceProbe);if(surfaceScene.IsValid() && surfaceScene.isLoaded)UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(surfaceScene);if(before!=null)Object.DestroyImmediate(before);if(after!=null)Object.DestroyImmediate(after);if(temp!=null)Object.DestroyImmediate(temp);
            }
        }
        // Ignore branched and stationary components individually; preserve every closed, moving loop.
        // A separate candidate, never modifies the source mesh or the strict estimator.
        private static System.Collections.Generic.List<int[]> ApproximateBoundaries(int[] triangles,int vertices,Func<int,bool> moving) {
            if(triangles==null||triangles.Length%3!=0)throw new InvalidOperationException("Invalid mesh triangles.");
            var edges=new System.Collections.Generic.Dictionary<long,int>();
            for(int i=0;i<triangles.Length;i+=3)for(int j=0;j<3;j++){
                int a=triangles[i+j],b=triangles[i+(j+1)%3];if(a<0||b<0||a>=vertices||b>=vertices||a==b)throw new InvalidOperationException("Invalid mesh edge.");
                long key=((long)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);edges.TryGetValue(key,out int count);edges[key]=count+1;
            }
            var graph=new System.Collections.Generic.Dictionary<int,System.Collections.Generic.List<int>>();
            foreach(var pair in edges)if(pair.Value==1){int a=(int)(pair.Key>>32),b=(int)pair.Key;
                if(!graph.ContainsKey(a))graph[a]=new System.Collections.Generic.List<int>();if(!graph.ContainsKey(b))graph[b]=new System.Collections.Generic.List<int>();graph[a].Add(b);graph[b].Add(a);}
            var seen=new System.Collections.Generic.HashSet<int>();var result=new System.Collections.Generic.List<int[]>();
            foreach(int start in graph.Keys){if(!seen.Add(start))continue;var pending=new System.Collections.Generic.Queue<int>();pending.Enqueue(start);var ids=new System.Collections.Generic.List<int>();bool changed=false;
                while(pending.Count>0){int id=pending.Dequeue();ids.Add(id);changed|=moving(id);foreach(int next in graph[id])if(seen.Add(next))pending.Enqueue(next);}
                if(!changed||ids.Any(id=>graph[id].Count!=2))continue;
                var ordered=new System.Collections.Generic.List<int>();int current=start,previous=-1;
                do {if(ordered.Contains(current))throw new InvalidOperationException("Invalid boundary cycle.");ordered.Add(current);var n=graph[current];int next=n[0]==previous?n[1]:n[0];previous=current;current=next;}while(current!=start);
                if(ordered.Count==ids.Count)result.Add(ordered.ToArray());
            }
            return result;
        }
        internal static Vector2 NativeDefault(VRCAvatarDescriptor avatar,SetupSettings setup,SocketSettings part,bool regenerate)
        {
            // Inspector toggle defaults are not a public, unit-verifiable API.
            // Do not invent a value from a release label or a copied constant.
            throw new InvalidOperationException("Fury の重み有効時の標準値と単位を API で確認できません。ネイティブ初期値を保持します。");
        }
        internal static string DeferredKey(SocketSettings part,SocketAnimationGroup group) => part.id + "\0" + group.id;
        internal static bool ResolveDeferred(SocketSettings part,Transform futureSocket,HashSet<string> deferred,ICollection<string> warnings)
        {
            if (deferred == null || futureSocket == null || part.animationGroups == null) return false;
            return false;
        }
        internal static SetupSettings Resolve(VRCAvatarDescriptor avatar,SetupSettings original,ICollection<string> warnings,bool regenerate,out HashSet<string> deferred)
        {
            deferred=new HashSet<string>();
            if(!original.parts.Any(p=>p.included&&p.AnimationEnabled&&p.animationModelVersion==1&&
                p.animationGroups.Any(g=>g!=null&&g.kind==SocketAnimationKind.Depth&&g.depthSettings!=null&&g.depthSettings.radiusWeightEnabled&&g.depthSettings.radiusAutoCorrection)))return original;
            var copy=original.Copy();
            var existing=FullSetupGenerator.Find(avatar);
            foreach(var part in copy.parts.Where(p=>p.included&&p.AnimationEnabled&&p.animationModelVersion==1))
                foreach(var group in part.animationGroups.Where(g=>g!=null&&g.kind==SocketAnimationKind.Depth&&g.depthSettings!=null&&g.depthSettings.radiusWeightEnabled&&g.depthSettings.radiusAutoCorrection)) {
                    try {var result=Estimate(avatar,copy,part,group,regenerate);group.depthSettings.RecordRadiusAutoResult(result.x,result.y,false,"");}
                    catch(InvalidOperationException e){
                        group.depthSettings.radiusKeepNative=true;
                        warnings.Add("自動半径を適用できません: " + part.id + " / " + e.Message +
                            " / 半径フィールドは変更しません。新規の場合は Fury の初期状態（重みが無効の場合があります）を使います。保存済み入力は保持します。");
                        continue;
                    }
                }
            return copy;
        }
    }
}
