var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/MANUKA/Prefab/MANUKA_lilToon.prefab");
var avatar=UnityEngine.Object.Instantiate(prefab); avatar.name="Issue121 storage probe";
UnityEngine.GameObject duplicate=null;
var observations=new System.Collections.Generic.List<string>();
try {
var descriptor=avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
var settings=LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.CreateDefault();
LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.ApplyPreset(settings,2);
LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.DetectMouth(settings,descriptor);
LumaKroma.Sps2SetupAssistant.Editor.Generation.Sps2SetupContext root; string message;
var timer=System.Diagnostics.Stopwatch.StartNew();
if(!LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(descriptor,settings,true,out root,out message))return "generate failed: "+message;
observations.Add("generateMs="+timer.ElapsedMilliseconds+";warnings="+message);
observations.Add("name="+root.gameObject.name+";ownMono="+root.gameObject.GetComponentsInChildren<LumaKroma.Sps2SetupAssistant.Sps2SetupRoot>(true).Length+";sockets="+root.sockets.Count+";asset="+UnityEditor.AssetDatabase.GetAssetPath(root.asset));
var loaded=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Find(descriptor);
observations.Add("reload="+(loaded.identity==root.identity&&loaded.sockets.Count==root.sockets.Count));
var pose=loaded.sockets[0].pose; pose.localPosition+=UnityEngine.Vector3.right*.002f; var position=pose.localPosition;
if(!LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(descriptor,settings,false,out root,out message))return "apply failed: "+message;
observations.Add("preservePose="+(root.sockets[0].pose==pose&&pose.localPosition==position));
timer.Restart();
if(!LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.ShowTestPlug(descriptor,out message))return "plug failed: "+message;
observations.Add("plugFirstMs="+timer.ElapsedMilliseconds);
loaded=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Find(descriptor); var plug=loaded.testPlug;
timer.Restart(); var oid=UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(descriptor); observations.Add("ownerIdMs="+timer.ElapsedMilliseconds); timer.Restart(); var allAvatars=UnityEngine.Resources.FindObjectsOfTypeAll<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>(); observations.Add("allAvatarsMs="+timer.ElapsedMilliseconds+";count="+allAvatars.Length); timer.Restart();
if(!LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.ShowTestPlug(descriptor,out message))return "plug repeat failed: "+message;
loaded=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Find(descriptor);
observations.Add("plugRepeatMs="+timer.ElapsedMilliseconds+";same="+(plug==loaded.testPlug));
timer.Restart(); for(int i=0;i<3;i++)loaded=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Find(descriptor); observations.Add("findThreeMs="+timer.ElapsedMilliseconds);
var originalJson=loaded.asset.stateJson; var originalIdentity=loaded.identity;
duplicate=UnityEngine.Object.Instantiate(avatar); duplicate.name="Issue121 duplicated storage probe";
var copyDescriptor=duplicate.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
settings=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Find(copyDescriptor).settings.Copy();
settings.parts[0].name="Changed duplicate";
if(!LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(copyDescriptor,settings,false,out root,out message))return "clone apply failed: "+message;
observations.Add("cloneIsolated="+(root.identity!=originalIdentity&&loaded.asset.stateJson==originalJson));
UnityEditor.Undo.PerformUndo();
var undone=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Find(copyDescriptor);
observations.Add("undoIdentity="+(undone.identity==originalIdentity));
UnityEditor.Undo.PerformRedo();
var redone=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Find(copyDescriptor);
observations.Add("redoIdentity="+(redone.identity==root.identity));
var prefabPath=UnityEditor.AssetDatabase.GenerateUniqueAssetPath("Assets/ZZZ_GeneratedAssets/Issue121/StorageRepair.prefab");
UnityEditor.PrefabUtility.SaveAsPrefabAsset(duplicate,prefabPath);
var contents=UnityEditor.PrefabUtility.LoadPrefabContents(prefabPath);
try {
var restored=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Find(contents.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>());
observations.Add("prefabRestore="+(restored.identity==redone.identity&&restored.sockets.Count==15&&restored.testPlug!=null&&contents.GetComponentsInChildren<LumaKroma.Sps2SetupAssistant.Sps2SetupRoot>(true).Length==0));
} finally {UnityEditor.PrefabUtility.UnloadPrefabContents(contents);}
return observations;
} finally {if(duplicate!=null)UnityEngine.Object.DestroyImmediate(duplicate);UnityEngine.Object.DestroyImmediate(avatar);}




