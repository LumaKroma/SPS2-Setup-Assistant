var avatar=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/MANUKA/Prefab/MANUKA_lilToon.prefab"));
avatar.name="Issue121 typed depth probe";
try {
var d=avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
var s=LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.CreateDefault();
LumaKroma.Sps2SetupAssistant.Editor.Model.FullSetupCatalog.DetectMouth(s,d);
foreach(var p in s.parts)p.included=p.id=="mouth";
var part=s.parts.Find(p=>p.id=="mouth"); part.depth=true;
var obj=new UnityEngine.GameObject("Depth fixture object");obj.transform.SetParent(avatar.transform,false);
var clip=new UnityEngine.AnimationClip {name="Depth fixture clip"};
UnityEditor.AnimationUtility.SetEditorCurve(clip,UnityEditor.EditorCurveBinding.FloatCurve("Depth fixture object",typeof(UnityEngine.Transform),"m_LocalPosition.x"),UnityEngine.AnimationCurve.Constant(0,1,.02f));
UnityEditor.AssetDatabase.CreateAsset(clip,UnityEditor.AssetDatabase.GenerateUniqueAssetPath("Assets/ZZZ_GeneratedAssets/Issue121/DepthFixture.anim"));
part.actions.Add(new LumaKroma.Sps2SetupAssistant.DepthActionSettings {kind=LumaKroma.Sps2SetupAssistant.DepthActionKind.AnimationClip,clip=clip});
part.actions.Add(new LumaKroma.Sps2SetupAssistant.DepthActionSettings {kind=LumaKroma.Sps2SetupAssistant.DepthActionKind.Object,target=obj,objectOn=false});
LumaKroma.Sps2SetupAssistant.Editor.Generation.Sps2SetupContext root;string message;
if(!LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(d,s,true,out root,out message))return "failed: "+message;
var data=new UnityEditor.SerializedObject(root.sockets[0].socket);var actions=data.FindProperty("depthActions2.Array.data[0].actionSet.actions");
var result=new System.Collections.Generic.List<string>(); result.Add("actions="+actions.arraySize);
for(int i=0;i<actions.arraySize;i++)result.Add(actions.GetArrayElementAtIndex(i).managedReferenceFullTypename);
result.Add("blendshape="+(actions.GetArrayElementAtIndex(0).FindPropertyRelative("renderer").objectReferenceValue==d.VisemeSkinnedMesh));
result.Add("clip="+(actions.GetArrayElementAtIndex(1).FindPropertyRelative("clip.objRef").objectReferenceValue==clip));
result.Add("object="+(actions.GetArrayElementAtIndex(2).FindPropertyRelative("obj").objectReferenceValue==obj));
var mode=actions.GetArrayElementAtIndex(2).FindPropertyRelative("mode");result.Add("objectMode="+mode.enumNames[mode.enumValueIndex]);
part.actions.RemoveAt(2);part.actions.RemoveAt(1);
if(!LumaKroma.Sps2SetupAssistant.Editor.Generation.FullSetupGenerator.Apply(d,s,false,out root,out message))return "update failed: "+message;
data=new UnityEditor.SerializedObject(root.sockets[0].socket);result.Add("updatedActions="+data.FindProperty("depthActions2.Array.data[0].actionSet.actions").arraySize);
return result;
}finally{UnityEngine.Object.DestroyImmediate(avatar);}
