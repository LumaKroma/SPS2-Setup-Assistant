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
 settings.modularAvatar=true; LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.ApplyPreset(settings,2);
 LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.DetectMouth(settings,descriptor);
 LumaKroma.Sps2SetupAssistant.Sps2SetupRoot root; string message;
 var result=new System.Collections.Generic.List<string>();
 bool created=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(descriptor,settings,true,out root,out message);
 result.Add("generated="+created+";message="+message); if(!created)return result;
 result.Add("sockets="+root.sockets.Count);
 result.Add("paths="+root.sockets.Find(s=>s.id=="mouth").pathStops.Length+","+root.sockets.Find(s=>s.id=="anus").pathStops.Length);
 var mouth=root.sockets.Find(s=>s.id=="mouth"); var pose=mouth.pose;
 pose.localPosition+=new UnityEngine.Vector3(.003f,.002f,.001f); var adjusted=pose.localPosition;
 bool applied=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(descriptor,settings,false,out root,out message);
 result.Add("nonreplace="+applied+";samePose="+(root.sockets.Find(s=>s.id=="mouth").pose==pose)+";position="+(pose.localPosition==adjusted)+";message="+message);
 bool plug=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.ShowTestPlug(descriptor,out message);
 result.Add("plug="+plug+";message="+message); if(!plug)return result;
 var plugObject=root.testPlug;
 bool plugAgain=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.ShowTestPlug(descriptor,out message);
 result.Add("plugReuse="+(plugAgain&&root.testPlug==plugObject)+";tag="+plugObject.tag);
 var existing=new UnityEngine.GameObject("Existing Socket"); existing.transform.SetParent(avatar.transform,false);existing.transform.localPosition=new UnityEngine.Vector3(0,1,.1f);
 var socket=com.vrcfury.api.FuryComponents.CreateSocket(existing);socket.SetName("Existing Socket");socket.SetMode(com.vrcfury.api.Components.FurySocket.Mode.Hole);
 var furyType=System.Linq.Enumerable.Single(UnityEditor.TypeCache.GetTypesDerivedFrom<UnityEngine.MonoBehaviour>(),t=>t.FullName=="VF.Model.VRCFury");
 var optionsType=furyType.Assembly.GetType("VF.Model.Feature.SpsOptions",true);
 var options=avatar.AddComponent(furyType);
 var data=new UnityEditor.SerializedObject(options);
 data.FindProperty("content").managedReferenceValue=System.Activator.CreateInstance(optionsType);
 data.ApplyModifiedPropertiesWithoutUndo(); data.Update();
 data.FindProperty("content.saveSockets").boolValue=true; data.ApplyModifiedPropertiesWithoutUndo();
 result.Add("existingNativeSaveOption="+data.FindProperty("content.saveSockets").boolValue);
 var output=System.IO.Path.GetFullPath(UnityEngine.Application.dataPath+"/../../../../issue121-definition/implementation-session-3/ma-saved-build-observations.txt");
 System.IO.File.WriteAllLines(output,result);
 foreach(var t in avatar.GetComponentsInChildren<UnityEngine.Transform>(true)) {
  var instance=UnityEditor.PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
  if(instance!=null)UnityEditor.PrefabUtility.UnpackPrefabInstance(instance,UnityEditor.PrefabUnpackMode.Completely,UnityEditor.InteractionMode.AutomatedAction);
 }
 bool built=VRC.SDKBase.Editor.BuildPipeline.VRCBuildPipelineCallbacks.OnPreprocessAvatar(avatar);
 result.Add("preprocess="+built);
 result.Add("metadata="+avatar.GetComponentsInChildren<LumaKroma.Sps2SetupAssistant.Sps2SetupRoot>(true).Length);
 result.Add("plugRetained="+(plugObject!=null));
 if(descriptor.expressionParameters!=null)foreach(var p in descriptor.expressionParameters.parameters)result.Add("param="+p.name+";type="+p.valueType+";saved="+p.saved+";default="+p.defaultValue);
 var seen=new System.Collections.Generic.HashSet<VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu>();
 var menus=new System.Collections.Generic.Queue<VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu>();
 if(descriptor.expressionsMenu!=null)menus.Enqueue(descriptor.expressionsMenu);
 while(menus.Count>0){var menu=menus.Dequeue();if(!seen.Add(menu))continue;result.Add("page="+menu.name+";controls="+menu.controls.Count);foreach(var c in menu.controls){result.Add("menu="+c.name+";type="+c.type+";param="+(c.parameter==null?"":c.parameter.name));if(c.subMenu!=null)menus.Enqueue(c.subMenu);}}
 foreach(var layer in descriptor.baseAnimationLayers)if(layer.type==VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX){
  var fx=layer.animatorController as UnityEditor.Animations.AnimatorController;
  result.Add("fxAsset="+UnityEditor.AssetDatabase.GetAssetPath(fx));
  for(int i=0;i<fx.layers.Length;i++)if(fx.layers[i].name=="SPS2 Instant"){
   var machine=fx.layers[i].stateMachine;result.Add("instantMachineAsset="+UnityEditor.AssetDatabase.GetAssetPath(machine));
   foreach(var child in machine.states){result.Add("state="+child.state.name+";clipAsset="+UnityEditor.AssetDatabase.GetAssetPath(child.state.motion));foreach(var b in child.state.behaviours)result.Add("behaviour="+b.GetType().Name+";asset="+UnityEditor.AssetDatabase.GetAssetPath(b));}
   var evaluator=new UnityEngine.GameObject("Instant Animator Evaluation");
   try{
    var evaluatorController=new UnityEditor.Animations.AnimatorController();evaluatorController.parameters=fx.parameters;
    evaluatorController.AddLayer(new UnityEditor.Animations.AnimatorControllerLayer{name="SPS2 Instant",defaultWeight=1,stateMachine=machine});
    var animator=evaluator.AddComponent<UnityEngine.Animator>();animator.runtimeAnimatorController=evaluatorController;animator.Rebind();
    var trigger=System.Linq.Enumerable.Single(fx.parameters,p=>p.name.EndsWith("_Instant")).name;
    var legacy=System.Linq.Enumerable.Single(fx.parameters,p=>p.name.EndsWith("_legacy")).name;
    void Step(string label,bool pressed,float legacyValue){animator.SetBool(trigger,pressed);animator.SetFloat(legacy,legacyValue);animator.Update(.02f);animator.Update(.02f);var info=animator.GetCurrentAnimatorStateInfo(0);var names=new[]{"Ready","Fire","Held","DeniedHeld"};var state=System.Linq.Enumerable.FirstOrDefault(names,n=>info.shortNameHash==UnityEngine.Animator.StringToHash(n));result.Add("instant="+label+";state="+state);}
    Step("initial",false,1);Step("deny",true,1);Step("denyHeldAfterLegacyOff",true,0);Step("release",false,0);Step("press1",true,0);Step("held",true,0);Step("release2",false,0);Step("press2",true,0);Step("finalRelease",false,0);
   }finally{UnityEngine.Object.DestroyImmediate(evaluator);}
  }
 }
 System.IO.File.WriteAllLines(output,result);
 return result;
} finally {UnityEditor.Selection.activeObject=null; UnityEngine.Object.DestroyImmediate(avatar);}


