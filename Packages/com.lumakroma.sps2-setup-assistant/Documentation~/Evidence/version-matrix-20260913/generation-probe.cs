var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/MANUKA/Prefab/MANUKA_lilToon.prefab");
if(prefab==null) return "fixture-prefab-missing";
var avatar=UnityEngine.Object.Instantiate(prefab);
avatar.name="MatrixFixture(Clone)";
var errors=new List<string>(); UnityEngine.Application.LogCallback capture=(message,stack,type)=>{if(type==UnityEngine.LogType.Error || type==UnityEngine.LogType.Exception || type==UnityEngine.LogType.Assert)errors.Add(message);}; UnityEngine.Application.logMessageReceived+=capture;
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
 var caps=LumaKroma.Sps2SetupAssistant.Editor.Compatibility.VrcFuryCapabilities.Current;
 var result=new List<string>{"version="+caps.Version,"canGenerate="+caps.CanGenerate,"sps2="+caps.Sps2,"path="+caps.Path,"collapse="+caps.Collapse,"localTangents="+caps.LocalTangents,"depth="+caps.Depth,"publicAttachment="+caps.PublicAttachment,"notices="+string.Join("|",caps.Notices())};
 var settings=LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.CreateDefault();
 LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.ApplyPreset(settings,2);settings.localOnly=true;settings.instant=true;settings.penetration=true;
 LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.DetectMouth(settings,descriptor);
 LumaKroma.Sps2SetupAssistant.Editor.Generation.Sps2SetupContext root;string message;
 var generated=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(descriptor,settings,true,out root,out message);
 result.Add("generated="+generated+";message="+message);if(!generated)return result;
 result.Add("sockets="+root.sockets.Count);result.Add("mouthStops="+root.sockets.Single(x=>x.id=="mouth").pathStops.Length);
 result.Add("effective="+UnityEngine.JsonUtility.ToJson(new { }));
 result.Add("effectivePenetration="+root.settings.penetration+";instant="+root.settings.instant+";legacy="+root.settings.legacy);
 var pose=root.sockets.Single(x=>x.id=="mouth").pose;pose.localPosition+=new UnityEngine.Vector3(.003f,.002f,.001f);var position=pose.localPosition;
 bool applied=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(descriptor,settings,false,out root,out message);
 result.Add("apply="+applied+";posePreserved="+(root!=null && root.sockets.Single(x=>x.id=="mouth").pose.localPosition==position)+";message="+message);if(!applied)return result;
 bool regenerated=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(descriptor,settings,true,out root,out message);
 result.Add("regenerated="+regenerated+";message="+message);if(!regenerated)return result;
 bool longPlug=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.ShowLongTestPlug(descriptor,out message);result.Add("longPlug="+longPlug+";message="+message);
 bool plug=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.ShowTestPlug(descriptor,out message);result.Add("plug="+plug+";message="+message);
 root=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Find(descriptor);
 result.Add("ownedCustomScripts="+root.gameObject.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true).Count(c=>c!=null&&c.GetType().Namespace!=null&&c.GetType().Namespace.StartsWith("LumaKroma.Sps2")));
 UnityEditor.Selection.activeGameObject=avatar;
 bool preprocess=VRC.SDKBase.Editor.BuildPipeline.VRCBuildPipelineCallbacks.OnPreprocessAvatar(avatar);
 result.Add("preprocess="+preprocess);result.Add("actualErrors="+string.Join("|",errors));if(!preprocess)return result;
 var seen=new HashSet<VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu>();var queue=new Queue<VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu>();queue.Enqueue(descriptor.expressionsMenu);
 while(queue.Count!=0){var menu=queue.Dequeue();if(menu==null||!seen.Add(menu))continue;foreach(var control in menu.controls)if(control.subMenu!=null)queue.Enqueue(control.subMenu);}
 result.Add("menuRoots="+string.Join("|",descriptor.expressionsMenu.controls.Select(c=>c.name)));
 result.Add("menusWithin8="+seen.All(m=>m.controls.Count<=8));result.Add("mouthMenu="+seen.SelectMany(m=>m.controls).Count(c=>c.name=="口"));
 result.Add("depthBindings="+descriptor.baseAnimationLayers.Where(l=>l.type==VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX).Select(l=>l.animatorController).OfType<UnityEditor.Animations.AnimatorController>().SelectMany(c=>c.animationClips).SelectMany(c=>UnityEditor.AnimationUtility.GetCurveBindings(c)).Count(b=>b.propertyName=="blendShape.vrc.v_oh"));
 var local=seen.SelectMany(m=>m.controls).Single(c=>c.name=="Local Only");var parameter=descriptor.expressionParameters.parameters.Single(p=>p.name==local.parameter.name);result.Add("localOnlySaved="+parameter.saved+";default="+parameter.defaultValue);
 return result;
}finally{UnityEngine.Application.logMessageReceived-=capture;UnityEditor.Selection.activeObject=null;UnityEngine.Object.DestroyImmediate(avatar);}
