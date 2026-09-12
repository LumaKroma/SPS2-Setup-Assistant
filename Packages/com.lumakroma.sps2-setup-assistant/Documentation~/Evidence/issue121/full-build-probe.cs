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
 var output=System.IO.Path.GetFullPath(UnityEngine.Application.dataPath+"/../../../../issue121-definition/implementation-session-3/full-build-observations.txt");
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
 System.IO.File.WriteAllLines(output,result);
 return result;
} finally {UnityEngine.Object.DestroyImmediate(avatar);}

