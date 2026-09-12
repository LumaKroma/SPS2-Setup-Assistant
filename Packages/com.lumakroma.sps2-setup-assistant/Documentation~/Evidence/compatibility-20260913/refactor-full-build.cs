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
 LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.ApplyPreset(settings,2); settings.instant=true; settings.localOnly=true; settings.showInternalThickness=true;
 LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.DetectMouth(settings,descriptor);
 LumaKroma.Sps2SetupAssistant.Editor.Generation.Sps2SetupContext root; string message;
 var result=new System.Collections.Generic.List<string>();
 bool created=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(descriptor,settings,true,out root,out message);
 result.Add("generated="+created+";message="+message); if(!created)return result;
 result.Add("sockets="+root.sockets.Count);
 foreach(var id in new[]{"mouth","anus"}){var pathSocket=root.sockets.Single(s=>s.id==id);var serialized=new UnityEditor.SerializedObject(pathSocket.socket);var stops=serialized.FindProperty("guidedPathStops");result.Add(id+"Collapsed="+string.Join(",",Enumerable.Range(0,stops.arraySize).Select(i=>stops.GetArrayElementAtIndex(i).FindPropertyRelative("shrink").boolValue)));result.Add(id+"Throat="+pathSocket.pathStops.Single(t=>t.name.Contains("Throat")).position.ToString("F5"));}
 result.Add("paths="+root.sockets.Find(s=>s.id=="mouth").pathStops.Length+","+root.sockets.Find(s=>s.id=="anus").pathStops.Length);
 var mouth=root.sockets.Find(s=>s.id=="mouth"); var pose=mouth.pose;
 pose.localPosition+=new UnityEngine.Vector3(.003f,.002f,.001f); var adjusted=pose.localPosition;
 bool applied=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(descriptor,settings,false,out root,out message);
 result.Add("nonreplace="+applied+";samePose="+(root.sockets.Find(s=>s.id=="mouth").pose==pose)+";position="+(pose.localPosition==adjusted)+";message="+message);
 bool longPlug=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.ShowLongTestPlug(descriptor,out message); result.Add("longPlug="+longPlug+";message="+message);
 bool plug=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.ShowTestPlug(descriptor,out message);
 result.Add("plug="+plug+";message="+message); if(!plug)return result;
 root=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Find(descriptor); var plugObject=root.testPlug;
 bool plugAgain=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.ShowTestPlug(descriptor,out message);
 result.Add("plugReuse="+(plugAgain&&root.testPlug==plugObject)+";tag="+plugObject.tag);
 var existing=new UnityEngine.GameObject("Existing Socket"); existing.transform.SetParent(avatar.transform,false);existing.transform.localPosition=new UnityEngine.Vector3(0,1,.1f);
 var socket=com.vrcfury.api.FuryComponents.CreateSocket(existing);socket.SetName("Existing Socket");socket.SetMode(com.vrcfury.api.Components.FurySocket.Mode.Hole);
 var output=System.IO.Path.GetFullPath(UnityEngine.Application.dataPath+"/../../../../issue121-definition/menu-placement-build-observations.txt");
 System.IO.File.WriteAllLines(output,result);
 foreach(var t in avatar.GetComponentsInChildren<UnityEngine.Transform>(true)) {
  var instance=UnityEditor.PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
  if(instance!=null)UnityEditor.PrefabUtility.UnpackPrefabInstance(instance,UnityEditor.PrefabUnpackMode.Completely,UnityEditor.InteractionMode.AutomatedAction);
 }
 bool built=VRC.SDKBase.Editor.BuildPipeline.VRCBuildPipelineCallbacks.OnPreprocessAvatar(avatar);
 result.Add("preprocess="+built);
 if(!built)return result;
 var holeBindings=new System.Collections.Generic.Dictionary<string,System.Collections.Generic.HashSet<float>>();
var controllers=avatar.GetComponentsInChildren<UnityEngine.Animator>(true).Select(a=>a.runtimeAnimatorController).Concat(descriptor.baseAnimationLayers.Select(l=>l.animatorController)).Concat(descriptor.specialAnimationLayers.Select(l=>l.animatorController)).Where(c=>c!=null).Distinct();
foreach(var controller in controllers)foreach(var clip in controller.animationClips.Distinct())foreach(var binding in UnityEditor.AnimationUtility.GetCurveBindings(clip))if(binding.propertyName=="material._SPS_SocketHole" && (binding.path.Contains("mouth")||binding.path.Contains("anus"))){if(!holeBindings.ContainsKey(binding.path))holeBindings[binding.path]=new System.Collections.Generic.HashSet<float>();foreach(var key in UnityEditor.AnimationUtility.GetEditorCurve(clip,binding).keys)holeBindings[binding.path].Add(key.value);}
result.Add("nativeHoleBindings="+string.Join("|",holeBindings.Select(k=>k.Key+"="+string.Join(",",k.Value))));
 var fx=(UnityEditor.Animations.AnimatorController)descriptor.baseAnimationLayers.First(l=>l.type==VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX).animatorController;
 var queue=new System.Collections.Generic.Queue<VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu>();queue.Enqueue(descriptor.expressionsMenu);
 var seen=new System.Collections.Generic.HashSet<VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu>();
 string param=null;
 while(queue.Count>0){var m=queue.Dequeue();if(!seen.Add(m))continue;foreach(var c in m.controls){if(c.name==settings.parts.Find(p=>p.id=="mouth").name && c.parameter!=null)param=c.parameter.name;if(c.subMenu!=null)queue.Enqueue(c.subMenu);}}
 if(param==null)return "mouth-menu-missing";
 var local=seen.SelectMany(m=>m.controls).Single(c=>c.name=="Local Only"); var lp=descriptor.expressionParameters.parameters.Single(p=>p.name==local.parameter.name); result.Add("localOnlySaved="+lp.saved+";default="+lp.defaultValue);
var sps=descriptor.expressionsMenu.controls.Single(c=>c.name=="SPS2").subMenu; result.Add("page1="+string.Join("|",sps.controls.Select(c=>c.name))); result.Add("page2="+string.Join("|",sps.controls.Single(c=>c.name=="次へ").subMenu.controls.Select(c=>c.name)));
result.Add("noOtherGroup="+!seen.SelectMany(m=>m.controls).Any(c=>c.name=="その他"));
result.Add("longMeshBuilt="+avatar.GetComponentsInChildren<UnityEngine.Renderer>(true).Any(r=>r.name=="Capsule"));
result.Add("menuRoots="+string.Join("|",descriptor.expressionsMenu.controls.Select(c=>c.name)));
result.Add("menuControls="+string.Join("|",seen.SelectMany(m=>m.controls).Select(c=>c.name)));
var paths=new System.Collections.Generic.HashSet<string>();
 void Scan(UnityEngine.Motion motion,bool selected){if(motion is UnityEngine.AnimationClip clip){if(selected)foreach(var b in UnityEditor.AnimationUtility.GetCurveBindings(clip))if(b.type==typeof(UnityEngine.GameObject)&&b.propertyName=="m_IsActive")paths.Add(b.path);}else if(motion is UnityEditor.Animations.BlendTree tree){foreach(var ch in tree.children)Scan(ch.motion,selected||ch.directBlendParameter==param);}}
 void States(UnityEditor.Animations.AnimatorStateMachine sm){foreach(var s in sm.states)Scan(s.state.motion,false);foreach(var child in sm.stateMachines)States(child.stateMachine);}
 foreach(var layer in fx.layers)States(layer.stateMachine);
 result.Add("targetParam="+param+";paths="+string.Join(",",paths));
 foreach(var useInstant in new[]{true,false}){
  var copy=UnityEngine.Object.Instantiate(avatar);copy.name="OffProbe";
  var a=copy.GetComponent<UnityEngine.Animator>();a.avatar=null;a.runtimeAnimatorController=null;a.cullingMode=UnityEngine.AnimatorCullingMode.AlwaysAnimate;
  var controller=UnityEngine.Object.Instantiate(fx);if(!useInstant)controller.layers=controller.layers.Where(l=>l.name!="SPS2 Instant").ToArray();
  var graph=UnityEngine.Playables.PlayableGraph.Create("SPS2 OFF AB");
  try{
   graph.SetTimeUpdateMode(UnityEngine.Playables.DirectorUpdateMode.Manual);
   var playable=UnityEngine.Animations.AnimatorControllerPlayable.Create(graph,controller);
   var playableOutput=UnityEngine.Animations.AnimationPlayableOutput.Create(graph,"FX",a);UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(playableOutput,playable);graph.Play();
   void Set(string name,float value){var p=controller.parameters.FirstOrDefault(x=>x.name==name);if(p==null)return;if(p.type==UnityEngine.AnimatorControllerParameterType.Bool)playable.SetBool(name,value>.5f);else if(p.type==UnityEngine.AnimatorControllerParameterType.Float)playable.SetFloat(name,value);else if(p.type==UnityEngine.AnimatorControllerParameterType.Int)playable.SetInteger(name,(int)value);}
   Set("IsLocal",1);foreach(var p in controller.parameters)if(p.name.Contains("Auto")||p.name.Contains("Legacy"))Set(p.name,0);
   for(int i=0;i<20;i++)graph.Evaluate(.05f);
   foreach(var value in new[]{0f,1f,0f,1f,0f}){Set(param,value);for(int i=0;i<20;i++)graph.Evaluate(.05f);result.Add("instant="+useInstant+";set="+value+";objects="+string.Join(",",paths.Select(p=>p+"="+(copy.transform.Find(p)!=null?copy.transform.Find(p).gameObject.activeSelf.ToString():"missing"))));}
  }finally{graph.Destroy();UnityEngine.Object.DestroyImmediate(copy);UnityEngine.Object.DestroyImmediate(controller);}
 }
 return result;
}finally{UnityEditor.Selection.activeObject=null;UnityEngine.Object.DestroyImmediate(avatar);}


