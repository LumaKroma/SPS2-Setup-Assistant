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
 LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.ApplyPreset(settings,2); settings.localOnly=true; settings.showInternalThickness=true;
 LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.DetectMouth(settings,descriptor);
 LumaKroma.Sps2SetupAssistant.Editor.Generation.Sps2SetupContext root; string message;
 string active="anus",excluded="mouth"; settings.parts.Single(p=>p.id==excluded).included=false; settings.showInternalThickness=false; settings.modularAvatar=true;
 var result=new System.Collections.Generic.List<string>();
 bool created=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(descriptor,settings,true,out root,out message);
 result.Add("generated="+created+";message="+message); if(!created)return result;
 result.Add("sockets="+root.sockets.Count);
 var included=root.sockets.Single(s=>s.id==active);result.Add("stops="+included.pathStops.Length+";disabledSocketAbsent="+!root.sockets.Any(s=>s.id==excluded));
 foreach(var t in avatar.GetComponentsInChildren<UnityEngine.Transform>(true)){var instance=UnityEditor.PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);if(instance!=null)UnityEditor.PrefabUtility.UnpackPrefabInstance(instance,UnityEditor.PrefabUnpackMode.Completely,UnityEditor.InteractionMode.AutomatedAction);}
 bool built=VRC.SDKBase.Editor.BuildPipeline.VRCBuildPipelineCallbacks.OnPreprocessAvatar(avatar);result.Add("preprocess="+built);if(!built)return result;
 var menus=new HashSet<VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu>();var queue=new Queue<VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu>();queue.Enqueue(descriptor.expressionsMenu);while(queue.Count>0){var m=queue.Dequeue();if(m==null||!menus.Add(m))continue;foreach(var c in m.controls)if(c.subMenu!=null)queue.Enqueue(c.subMenu);}
 result.Add("disabledMenuAbsent="+!menus.SelectMany(m=>m.controls).Any(c=>c.name==settings.parts.Single(p=>p.id==excluded).name));result.Add("activeMenuPresent="+menus.SelectMany(m=>m.controls).Any(c=>c.name==settings.parts.Single(p=>p.id==active).name));
 var fx=descriptor.baseAnimationLayers.Single(l=>l.type==VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX).animatorController;
 var holes=new Dictionary<string,HashSet<float>>();foreach(var clip in fx.animationClips.Distinct())foreach(var b in UnityEditor.AnimationUtility.GetCurveBindings(clip))if(b.propertyName=="material._SPS_SocketHole"&&b.path.Contains(active)){if(!holes.ContainsKey(b.path))holes[b.path]=new HashSet<float>();foreach(var k in UnityEditor.AnimationUtility.GetEditorCurve(clip,b).keys)holes[b.path].Add(k.value);}result.Add("nativeMarkers="+string.Join("|",holes.Select(h=>h.Key+"="+string.Join(",",h.Value))));result.Add("missingConstraintSources="+avatar.GetComponentsInChildren<UnityEngine.Animations.ParentConstraint>(true).Count(c=>Enumerable.Range(0,c.sourceCount).Any(i=>c.GetSource(i).sourceTransform==null)));return result;
}finally{UnityEditor.Selection.activeObject=null;UnityEngine.Object.DestroyImmediate(avatar);}
