var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>(true)).Single(a=>a.name=="MANUKA_lilToon");
var poses=new System.Collections.Generic.Dictionary<string,(UnityEngine.Vector3 p,UnityEngine.Quaternion q)>();
var result=new System.Collections.Generic.List<string>();
foreach(var rotated in new[]{false,true}){
 var avatar=UnityEngine.Object.Instantiate(original.gameObject);
 try{
  avatar.name="Issue121 coordinate probe";
  if(rotated){avatar.transform.position=new UnityEngine.Vector3(2,.5f,-3);avatar.transform.rotation=UnityEngine.Quaternion.Euler(0,90,0);avatar.transform.localScale=UnityEngine.Vector3.one*1.2f;}
  var descriptor=avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
  var skins=avatar.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true);var before=skins.SelectMany(s=>Enumerable.Range(0,s.sharedMesh.blendShapeCount).Select(i=>s.GetBlendShapeWeight(i))).ToArray();
  var settings=LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.CreateDefault();LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.ApplyPreset(settings,2);
  LumaKroma.Sps2SetupAssistant.Editor.Generation.Sps2SetupContext root;string message;
  if(!LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(descriptor,settings,true,out root,out message))return "failed: "+message;
  var after=skins.SelectMany(s=>Enumerable.Range(0,s.sharedMesh.blendShapeCount).Select(i=>s.GetBlendShapeWeight(i))).ToArray();result.Add("rotated="+rotated+";sourceShapesUnchanged="+before.SequenceEqual(after));
  foreach(var s in root.sockets){var p=avatar.transform.InverseTransformPoint(s.pose.position);var q=UnityEngine.Quaternion.Inverse(avatar.transform.rotation)*s.pose.rotation;if(!rotated)poses[s.id]=(p,q);else result.Add(s.id+";positionError="+UnityEngine.Vector3.Distance(p,poses[s.id].p)+";angleError="+UnityEngine.Quaternion.Angle(q,poses[s.id].q));}
 }finally{UnityEngine.Object.DestroyImmediate(avatar);}
}
return result;
