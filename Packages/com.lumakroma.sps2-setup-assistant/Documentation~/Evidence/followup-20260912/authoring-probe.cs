var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>(true)).Single(a=>a.name=="MANUKA_lilToon");
var window=UnityEditor.EditorWindow.GetWindow<LumaKroma.Sps2SetupAssistant.Editor.Sps2SetupAssistantWindow>();window.BindAvatar(original);
var data=new UnityEditor.SerializedObject(window);string id=data.FindProperty("avatarId").stringValue;data.FindProperty("descriptor").objectReferenceValue=null;data.ApplyModifiedPropertiesWithoutUndo();window.RecoverAvatar();data.Update();
var result=new System.Collections.Generic.List<string>{"windowRecovered="+(data.FindProperty("descriptor").objectReferenceValue==original)+";id="+id};
var avatar=UnityEngine.Object.Instantiate(original.gameObject);avatar.name="Issue121 long plug path probe";
try{
var descriptor=avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
var settings=LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.CreateDefault();LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.ApplyPreset(settings,2);
LumaKroma.Sps2SetupAssistant.Editor.Generation.Sps2SetupContext root;string error;
if(!LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(descriptor,settings,true,out root,out error))return "generate failed "+error;
foreach(var id2 in new[]{"mouth","anus"}){var s=root.sockets.Single(s=>s.id==id2);var other=root.sockets.Single(s=>s.id==(id2=="mouth"?"anus":"mouth"));result.Add(id2+";position="+avatar.transform.InverseTransformPoint(s.pose.position).ToString("F5")+";axis="+s.pose.forward.ToString("F5")+";exitError="+UnityEngine.Vector3.Distance(s.pathStops.Last().position,other.pose.position)+";exitAxisDot="+UnityEngine.Vector3.Dot(s.pathStops.Last().forward,other.pose.forward));}
if(!LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.ShowLongTestPlug(descriptor,out error))return "long failed "+error;
root=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Find(descriptor);var plug=root.longTestPlug;var mesh=plug.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh;var mouth=root.sockets.Single(s=>s.id=="mouth").pose;
result.Add("meshVertices="+mesh.vertexCount+";length="+mesh.bounds.size.z+";meshPersistent="+UnityEditor.EditorUtility.IsPersistent(mesh)+";tipGap="+UnityEngine.Vector3.Distance(plug.transform.TransformPoint(new UnityEngine.Vector3(0,0,1.5f)),mouth.position));
LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.ShowTestPlug(descriptor,out error);
LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.ShowLongTestPlug(descriptor,out error);
root=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Find(descriptor);result.Add("reuse="+(root.longTestPlug==plug)+";twoPlugs="+(root.testPlug!=null&&root.longTestPlug!=root.testPlug));
LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(descriptor,settings,true,out root,out error);result.Add("regenerationRetains="+(root.longTestPlug==plug));
return result;
}finally{UnityEditor.Selection.activeGameObject=original.gameObject;UnityEngine.Object.DestroyImmediate(avatar);window.BindAvatar(original);window.Repaint();}
