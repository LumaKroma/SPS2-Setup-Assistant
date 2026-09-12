var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/MANUKA/Prefab/MANUKA_lilToon.prefab");
if(prefab==null) return "fixture-prefab-missing";
var avatar=UnityEngine.Object.Instantiate(prefab);
avatar.name="Issue121FullBuildProbe";
try {
 var descriptor=avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
 foreach(var component in avatar.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true))
  if(component!=null && component!=descriptor) UnityEngine.Object.DestroyImmediate(component);
 descriptor.customExpressions=false; descriptor.expressionsMenu=null; descriptor.expressionParameters=null;
 descriptor.customizeAnimationLayers=true; var layers=descriptor.baseAnimationLayers;
 for(int i=0;i<layers.Length;i++){var c=new UnityEditor.Animations.AnimatorController {name="Probe "+layers[i].type}; c.AddLayer("Fixture"); var state=c.layers[0].stateMachine.AddState("Idle"); state.writeDefaultValues=true; state.motion=new UnityEngine.AnimationClip {name="Probe empty"}; layers[i].isDefault=false;layers[i].animatorController=c;}
 descriptor.baseAnimationLayers=layers;
 var special=descriptor.specialAnimationLayers;
 for(int i=0;i<special.Length;i++){var c=new UnityEditor.Animations.AnimatorController {name="Probe "+special[i].type}; c.AddLayer("Fixture"); var state=c.layers[0].stateMachine.AddState("Idle"); state.writeDefaultValues=true; state.motion=new UnityEngine.AnimationClip {name="Probe empty"}; special[i].isDefault=false;special[i].animatorController=c;}
 descriptor.specialAnimationLayers=special;
 avatar.GetComponent<UnityEngine.Animator>().runtimeAnimatorController=null;
 foreach(var a in avatar.GetComponentsInChildren<UnityEngine.Animator>(true))if(a.gameObject!=avatar)UnityEngine.Object.DestroyImmediate(a);
 var settings=LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.CreateDefault();
 LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.ApplyPreset(settings,2);
 LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.DetectMouth(settings,descriptor);
 var animator=avatar.GetComponent<UnityEngine.Animator>();
 settings.parts.Add(new LumaKroma.Sps2SetupAssistant.SocketSettings{id="custom",name="Custom",custom=true,included=true,category=5,target=animator.GetBoneTransform(UnityEngine.HumanBodyBones.Head)});
 var result=new System.Collections.Generic.List<string>();
 void Check(bool condition,string label){result.Add(label+"="+condition);if(!condition)throw new System.Exception(label);}
 LumaKroma.Sps2SetupAssistant.Sps2SetupRoot root;string message;
 Check(LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(descriptor,settings,true,out root,out message),"generate");
 Check(root.sockets.Count==16,"sixteenIncludingCustom");
 UnityEditor.Undo.FlushUndoRecordObjects();UnityEditor.Undo.PerformUndo();
 Check(LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Find(descriptor)==null,"undoRemovesGeneratedRoot");
 UnityEditor.Undo.PerformRedo();
 root=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Find(descriptor);
 Check(root!=null&&root.sockets.Count==16,"redoRestoresMetadataAndReferences");
 var identity=root.identity;var firstRoot=root;
 var mouth=root.sockets.Find(s=>s.id=="mouth").pose;var initialMouth=mouth.localPosition;
 mouth.localPosition+=new UnityEngine.Vector3(.01f,.02f,.03f);var manualMouth=mouth.localPosition;
 var custom=root.sockets.Find(s=>s.id=="custom").pose;var customPosition=custom.position;var customRotation=custom.rotation;
 settings.parts.Find(p=>p.custom).target=animator.GetBoneTransform(UnityEngine.HumanBodyBones.LeftHand);
 Check(LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(descriptor,settings,false,out root,out message),"nonreplace");
 Check(root==firstRoot&&root.sockets.Find(s=>s.id=="mouth").pose==mouth&&mouth.localPosition==manualMouth,"nonreplacePreservesIdentityAndManualPose");
 Check(UnityEngine.Vector3.Distance(custom.position,customPosition)<.00001f&&UnityEngine.Quaternion.Angle(custom.rotation,customRotation)<.001f,"customRebindPreservesWorldPose");
 Check(LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.ShowTestPlug(descriptor,out message),"plug");
 var plug=root.testPlug;
 Check(LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(descriptor,settings,true,out root,out message),"regenerate");
 Check(root!=firstRoot&&root.identity==identity,"regenerateStableIdentityNewRoot");
 Check(root.testPlug==plug,"regeneratePreservesSinglePlug");
 Check(UnityEngine.Vector3.Distance(root.sockets.Find(s=>s.id=="mouth").pose.localPosition,initialMouth)<.00001f,"regenerateRestoresAutoPose");
 UnityEditor.AssetDatabase.CreateFolder("Assets","ZZZ_GeneratedAssets");
 if(!UnityEditor.AssetDatabase.IsValidFolder("Assets/ZZZ_GeneratedAssets/Issue121"))UnityEditor.AssetDatabase.CreateFolder("Assets/ZZZ_GeneratedAssets","Issue121");
 string path="Assets/ZZZ_GeneratedAssets/Issue121/AuthoringValidation.prefab";
 UnityEditor.PrefabUtility.SaveAsPrefabAsset(avatar,path);
 var loaded=UnityEditor.PrefabUtility.LoadPrefabContents(path);
 try{
  var saved=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Find(loaded.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>());
  Check(saved!=null&&saved.identity==identity&&saved.sockets.Count==16,"prefabReloadMetadata");
  Check(saved.testPlug!=null&&saved.testPlug.transform.parent==saved.transform,"prefabReloadPlug");
  Check(saved.settings.parts.Find(p=>p.custom).target.IsChildOf(loaded.transform),"prefabReloadCustomReference");
  Check(saved.settings.parts.Find(p=>p.id=="mouth").actions[0].renderer.transform.IsChildOf(loaded.transform),"prefabReloadRendererReference");
 }finally{UnityEditor.PrefabUtility.UnloadPrefabContents(loaded);}
 var output=System.IO.Path.GetFullPath(UnityEngine.Application.dataPath+"/../../../../issue121-definition/implementation-session-3/authoring-observations.txt");
 System.IO.File.WriteAllLines(output,result);return result;
}finally{UnityEditor.Selection.activeObject=null;UnityEngine.Object.DestroyImmediate(avatar);}
