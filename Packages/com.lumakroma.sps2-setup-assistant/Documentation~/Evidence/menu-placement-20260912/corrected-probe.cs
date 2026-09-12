var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>(true)).Single(a=>a.name=="MANUKA_lilToon");
var avatar=UnityEngine.Object.Instantiate(original.gameObject);avatar.name="Issue121 placement correction probe";
try {
var descriptor=avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
var settings=LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.CreateDefault();LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.ApplyPreset(settings,2);LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.DetectMouth(settings,descriptor);
LumaKroma.Sps2SetupAssistant.Editor.Generation.Sps2SetupContext root;string message;
if(!LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(descriptor,settings,true,out root,out message))return "failed: "+message;
return root.sockets.Select(s=>new{id=s.id,position=avatar.transform.InverseTransformPoint(s.pose.position).ToString("F5"),axis=avatar.transform.InverseTransformDirection(s.pose.forward).ToString("F4"),radiusAxis=avatar.transform.InverseTransformDirection(s.pose.up).ToString("F4"),radiusOffset=new UnityEditor.SerializedObject(s.socket).FindProperty("useRadiusOffset").boolValue}).ToArray();
}finally{UnityEngine.Object.DestroyImmediate(avatar);}
