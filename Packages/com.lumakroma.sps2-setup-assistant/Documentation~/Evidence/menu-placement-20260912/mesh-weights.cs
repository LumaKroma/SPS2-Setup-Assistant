var avatar=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>(true)).Single(a=>a.name=="MANUKA_lilToon");
return avatar.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true).Where(r=>r.sharedMesh!=null&&(r.name=="Body"||r.name=="Manuka_body"||r.name=="Manuka_atama")).Select(r=>{
var totals=new float[r.bones.Length];foreach(var w in r.sharedMesh.GetAllBoneWeights())totals[w.boneIndex]+=w.weight;
return new{name=r.name,vertices=r.sharedMesh.vertexCount,bounds=r.sharedMesh.bounds.ToString(),weights=totals.Select((w,i)=>new{bone=r.bones[i]!=null?r.bones[i].name:"null",weight=w}).OrderByDescending(x=>x.weight).Take(14).ToArray()};}).ToArray();
