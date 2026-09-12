var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>(true)).Single(a=>a.name=="MANUKA_lilToon");
var results=new List<object>();
foreach(bool regenerate in new[]{false,true}) {
 var go=UnityEngine.Object.Instantiate(original.gameObject);go.name="InstantRemovalProbe";
 var errors=new List<string>();UnityEngine.Application.LogCallback capture=(m,s,t)=>{if(t==UnityEngine.LogType.Error||t==UnityEngine.LogType.Exception||t==UnityEngine.LogType.Assert)errors.Add(m);};UnityEngine.Application.logMessageReceived+=capture;
 try {
  var a=go.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
  var root=LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Find(a);
  bool oldEnabled=root.asset.stateJson.Contains("\"instant\":true");
  if(regenerate){var settings=root.settings.Copy();string error;if(!LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(a,settings,true,out root,out error))throw new System.Exception(error);}
  else {
   // Exercise the former component format with an enabled obsolete field, on the disposable clone only.
   var legacy=root.gameObject.AddComponent<LumaKroma.Sps2SetupAssistant.Sps2SetupRoot>();
   legacy.identity=root.identity;legacy.settings=root.settings.Copy();legacy.sockets=root.sockets;legacy.testPlug=root.testPlug;
   var json=UnityEngine.JsonUtility.ToJson(legacy.settings);json=json.Insert(1,"\"instant\":true,");
   UnityEngine.JsonUtility.FromJsonOverwrite(json,legacy.settings);oldEnabled=json.Contains("\"instant\":true");
  }
  foreach(var c in go.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true))if(c!=null&&c!=a&&!c.transform.IsChildOf(root.transform))UnityEngine.Object.DestroyImmediate(c);
  a.customExpressions=false;a.expressionsMenu=null;a.expressionParameters=null;a.customizeAnimationLayers=true;
  foreach(var layers in new[]{a.baseAnimationLayers,a.specialAnimationLayers})for(int i=0;i<layers.Length;i++){
   var c=new UnityEditor.Animations.AnimatorController{name="Removal fixture "+layers[i].type};c.AddLayer("Fixture");var state=c.layers[0].stateMachine.AddState("Idle");state.writeDefaultValues=true;state.motion=new UnityEngine.AnimationClip();layers[i].isDefault=false;layers[i].animatorController=c;
  }
  go.GetComponent<UnityEngine.Animator>().runtimeAnimatorController=null;
  bool built=VRC.SDKBase.Editor.BuildPipeline.VRCBuildPipelineCallbacks.OnPreprocessAvatar(go);if(!built)throw new System.Exception("Build failed "+string.Join("|",errors));
  var fx=(UnityEditor.Animations.AnimatorController)a.baseAnimationLayers.Single(l=>l.type==VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX).animatorController;
  var seen=new HashSet<VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu>();var queue=new Queue<VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu>();queue.Enqueue(a.expressionsMenu);
  while(queue.Count>0){var m=queue.Dequeue();if(m==null||!seen.Add(m))continue;foreach(var c in m.controls)if(c.subMenu!=null)queue.Enqueue(c.subMenu);}
  var controls=seen.SelectMany(m=>m.controls).ToArray();
  bool residue=controls.Any(c=>c.name.Contains("インスタント"))||fx.layers.Any(l=>l.name.Contains("SPS2 Instant"))||fx.parameters.Any(p=>p.name.EndsWith("_Instant"))||a.expressionParameters.parameters.Any(p=>p.name.EndsWith("_Instant"))||go.GetComponentsInChildren<UnityEngine.Transform>(true).Any(t=>t.name.StartsWith("__SPS2_Instant_"));
  if(residue)throw new System.Exception("Instant output remains");
  string param=controls.Single(c=>c.name=="口").parameter.name;
  var paths=new HashSet<string>();
  void Scan(UnityEngine.Motion motion,bool selected){if(motion is UnityEngine.AnimationClip clip){if(selected)foreach(var b in UnityEditor.AnimationUtility.GetCurveBindings(clip))if(b.type==typeof(UnityEngine.GameObject)&&b.propertyName=="m_IsActive")paths.Add(b.path);}else if(motion is UnityEditor.Animations.BlendTree tree){foreach(var ch in tree.children)Scan(ch.motion,selected||ch.directBlendParameter==param);}}
  void States(UnityEditor.Animations.AnimatorStateMachine sm){foreach(var s in sm.states)Scan(s.state.motion,false);foreach(var child in sm.stateMachines)States(child.stateMachine);}
  foreach(var layer in fx.layers)States(layer.stateMachine);
  if(paths.Count==0)throw new System.Exception("No native mouth activation paths");
  var animator=go.GetComponent<UnityEngine.Animator>();go.SetActive(true);animator.enabled=true;animator.avatar=null;animator.runtimeAnimatorController=null;animator.cullingMode=UnityEngine.AnimatorCullingMode.AlwaysAnimate;
  var graph=UnityEngine.Playables.PlayableGraph.Create("Removal socket lifecycle");var sequence=new List<object>();
  try {
   graph.SetTimeUpdateMode(UnityEngine.Playables.DirectorUpdateMode.Manual);
   var playable=UnityEngine.Animations.AnimatorControllerPlayable.Create(graph,fx);var output=UnityEngine.Animations.AnimationPlayableOutput.Create(graph,"FX",animator);UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output,playable);graph.Play();
   void Set(string name,float value){var p=fx.parameters.FirstOrDefault(x=>x.name==name);if(p==null)return;if(p.type==UnityEngine.AnimatorControllerParameterType.Bool)playable.SetBool(name,value>.5f);else if(p.type==UnityEngine.AnimatorControllerParameterType.Float)playable.SetFloat(name,value);}
   Set("IsLocal",1);foreach(var p in fx.parameters)if(p.name.Contains("Auto")||p.name.Contains("Legacy"))Set(p.name,0);
   foreach(float value in new[]{0f,1f,0f,1f,0f}){Set(param,value);for(int i=0;i<20;i++)graph.Evaluate(.05f);var states=paths.Select(p=>new{path=p,active=go.transform.Find(p)?.gameObject.activeSelf}).ToArray();if(states.Any(s=>s.active!=(value>.5f)))throw new System.Exception("Socket toggle mismatch " + value);sequence.Add(new{value,states});}
  }finally{graph.Destroy();}
  results.Add(new{regenerate,oldEnabled,built,residue,sequence,errors});
 }finally{
  UnityEngine.Application.logMessageReceived-=capture;UnityEditor.Undo.FlushUndoRecordObjects();foreach(var t in go.GetComponentsInChildren<UnityEngine.Transform>(true)){foreach(var c in t.GetComponents<UnityEngine.Component>())if(c!=null)UnityEditor.Undo.ClearUndo(c);UnityEditor.Undo.ClearUndo(t.gameObject);}UnityEngine.Object.DestroyImmediate(go);
 }
}
return results;
