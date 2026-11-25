using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;
using System;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using UnityEditor;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    public sealed partial class Sps2SetupAssistantWindow
    {
        [NonSerialized] private bool blendPreviewTransition;
        [NonSerialized] private int previewSelectionId;
        [NonSerialized] private AnimationModeDriver blendPreviewDriver;
        [NonSerialized] private AnimationClip blendPreviewClip;
        [NonSerialized] private Matrix4x4 blendPreviewFrame;
        [NonSerialized] private Vector2 blendPreviewRadii;
        [NonSerialized] private bool blendPreviewRadiiValid;
        [NonSerialized] private string blendPreviewMessage;
        [NonSerialized] private string blendPreviewPartId, blendPreviewGroupId;
        private sealed class PreviewOriginal { internal SkinnedMeshRenderer renderer; internal Mesh mesh; internal string shape; internal int index; internal float weight, previewWeight; internal bool sceneWasDirty; internal GameObject prefabRoot; internal UnityEngine.Object prefabSource; internal string overrideEvidence; }
        [NonSerialized] private System.Collections.Generic.List<PreviewOriginal> previewOriginals;
        private static bool ExistingPrefabBlendOverride(SkinnedMeshRenderer renderer, string path) {
            var root=PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject);
            var nearest=PrefabUtility.GetNearestPrefabInstanceRoot(renderer.gameObject);
            var source=PrefabUtility.GetCorrespondingObjectFromSource(renderer);
            if(root==null || nearest!=root || source==null) throw new InvalidOperationException("Nested or unresolved Prefab override cannot be verified.");
            var matches=(PrefabUtility.GetPropertyModifications(root) ?? Array.Empty<PropertyModification>()).Where(m=>m!=null && m.propertyPath==path).ToArray();
            if(matches.Any(m=>m.target!=source) || matches.Length>1) throw new InvalidOperationException("Prefab override is ambiguous.");
            return matches.Length==1;
        }
        private static string PreviewOverrideEvidence(GameObject root, UnityEngine.Object source) {
            if(root==null || source==null) return null;
            return string.Join("\n",(PrefabUtility.GetPropertyModifications(root) ?? Array.Empty<PropertyModification>())
                .Where(m=>m!=null && m.target==source)
                .Select(m=>m.propertyPath+"\0"+m.value+"\0"+(m.objectReference!=null?m.objectReference.GetInstanceID().ToString():""))
                .OrderBy(v=>v,StringComparer.Ordinal));
        }
        // Restore only a value still carrying our own preview. A different current
        // value can be another editor's change and must not be overwritten.
        private bool RestorePreviewOriginals() {
            if (previewOriginals == null) return true;
            bool contaminated = false;
            var issues = new System.Collections.Generic.List<string>();
            foreach (var item in previewOriginals) {
                var renderer = item.renderer;
                if (renderer == null) { issues.Add(L("プレビュー対象が見つかりません。")); continue; }
                if (renderer.sharedMesh != item.mesh || item.mesh == null || item.index < 0 ||
                    item.index >= item.mesh.blendShapeCount || item.mesh.GetBlendShapeName(item.index) != item.shape) {
                    issues.Add(renderer.name + ": " + L("メッシュが変わったため重みを復元できません。")); continue;
                }
                float current = renderer.GetBlendShapeWeight(item.index);
                if (!Mathf.Approximately(current, item.weight)) {
                    if (Mathf.Approximately(current, item.previewWeight)) {
                        try { renderer.SetBlendShapeWeight(item.index, item.weight); }
                        catch (Exception e) { issues.Add(renderer.name + ": " + item.shape + " / " + SetupFeedback.Error(e, "元の重みを復元できませんでした。対象のメッシュを確認してください。")); }
                        current = renderer.GetBlendShapeWeight(item.index);
                        if (!Mathf.Approximately(current, item.weight)) {
                            contaminated = true;
                            issues.Add(renderer.name + ": " + item.shape + " / " + L("プレビュー値が残っています。"));
                        }
                    } else issues.Add(renderer.name + ": " + item.shape + " / " + L("プレビュー中に編集されたため、現在の値を保持しました。"));
                }
                if (!ReferenceEquals(item.prefabRoot, null) &&
                    (item.prefabRoot == null || item.prefabSource == null || PreviewOverrideEvidence(item.prefabRoot,item.prefabSource) != item.overrideEvidence))
                    issues.Add(renderer.name + ": " + L("Prefab オーバーライドを確認してください。"));
                if (!item.sceneWasDirty && renderer.gameObject.scene.IsValid() && renderer.gameObject.scene.isDirty)
                    issues.Add(renderer.name + ": " + L("シーンに未保存の変更があります。"));
            }
            if (!contaminated) previewOriginals = null;
            if (issues.Count != 0) blendPreviewMessage = string.Join("\n", issues.Distinct());
            return !contaminated;
        }
        private bool FinishPreviewForOperation() {
            EndBlendPreview();
            if (RestorePreviewOriginals()) return true;
            SetMessage(L("プレビュー値が残っています。測定前に元の値を確認してください。") + "\n" + blendPreviewMessage, false);
            return false;
        }
        private void EndBlendPreview() {
            EditorApplication.update -= CheckPreviewSelection;
            var driver=blendPreviewDriver; var clip=blendPreviewClip;
            blendPreviewDriver=null; blendPreviewClip=null; blendPreviewRadiiValid=false; blendPreviewPartId=null; blendPreviewGroupId=null;
            if(driver==null && clip==null && previewOriginals==null) return;
            try { if(driver!=null && AnimationMode.InAnimationMode(driver)) AnimationMode.StopAnimationMode(driver); }
            catch(Exception e) { blendPreviewMessage=SetupFeedback.Error(e, "プレビューの終了を確認できません。元の重みを確認してください。"); }
            finally {
                if(clip!=null) DestroyImmediate(clip); if(driver!=null) DestroyImmediate(driver);
                RestorePreviewOriginals();
                SceneView.RepaintAll();
            }
        }
        private void StopPreviewOnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path) { EndBlendPreview(); }
        private void BeginBlendPreview(SocketSettings part, SocketAnimationGroup group) {
            if(!FinishPreviewForOperation()) return;
            blendPreviewMessage=null;
            if(AnimationMode.InAnimationMode()) { blendPreviewMessage=L("別のアニメーションプレビューを終了してから開始してください。"); return; }
            AnimationModeDriver driver=null; AnimationClip clip=null;
            try {
                if(descriptor==null || group?.actions==null) throw new InvalidOperationException("Select an avatar and animation group.");
                var actions=group.actions.Where(a=>a!=null && a.kind==DepthActionKind.BlendShape).ToArray();
                if(actions.Length==0) throw new InvalidOperationException("No BlendShape actions are selected.");
                var bindings=new System.Collections.Generic.List<(EditorCurveBinding binding, SkinnedMeshRenderer renderer, int index, float original, float value, bool keepOverride)>();
                var originals=new System.Collections.Generic.List<PreviewOriginal>();
                var keys=new System.Collections.Generic.HashSet<string>();
                foreach(var action in actions) {
                    var renderer=action.renderer;
                    if(renderer==null || EditorUtility.IsPersistent(renderer) || renderer.sharedMesh==null ||
                       !renderer.transform.IsChildOf(descriptor.transform) || action.blendShapeMode!=BlendShapeMode.Normal ||
                       float.IsNaN(action.weight)||float.IsInfinity(action.weight)) throw new InvalidOperationException("A BlendShape reference or value is invalid.");
                    var index=renderer.sharedMesh.GetBlendShapeIndex(action.shape);
                    if(index<0 || !keys.Add(renderer.GetInstanceID()+"\0"+action.shape)) throw new InvalidOperationException("A BlendShape is missing or duplicated.");
                    var path=AnimationUtility.CalculateTransformPath(renderer.transform,descriptor.transform);
                    var binding=EditorCurveBinding.FloatCurve(path,typeof(SkinnedMeshRenderer),"blendShape."+action.shape);
                    if(AnimationUtility.GetAnimatedObject(descriptor.gameObject,binding)!=renderer ||
                       !AnimationUtility.GetFloatValue(descriptor.gameObject,binding,out var value) ||
                       !Mathf.Approximately(value,renderer.GetBlendShapeWeight(index))) throw new InvalidOperationException("A BlendShape binding is ambiguous.");
                    var prefabRoot=PrefabUtility.IsPartOfPrefabInstance(renderer) ? PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject) : null;
                    var prefabSource=prefabRoot!=null ? PrefabUtility.GetCorrespondingObjectFromSource(renderer) : null;
                    if(prefabRoot!=null && prefabSource==null) throw new InvalidOperationException("Prefab source for preview cannot be verified.");
                    originals.Add(new PreviewOriginal {renderer=renderer,mesh=renderer.sharedMesh,shape=action.shape,index=index,weight=value,previewWeight=action.weight,sceneWasDirty=renderer.gameObject.scene.isDirty,prefabRoot=prefabRoot,prefabSource=prefabSource,overrideEvidence=PreviewOverrideEvidence(prefabRoot,prefabSource)});
                    bool keepOverride=prefabRoot!=null && ExistingPrefabBlendOverride(renderer,binding.propertyName);
                    bindings.Add((binding,renderer,index,value,action.weight,keepOverride));
                }
                blendPreviewRadiiValid=false;
                try {
                    var depth=group.depthSettings;
                    Vector2 radii=depth.radiusAutoCorrection ? RadiusAutoCorrection.Estimate(descriptor,settings,part,group) : new Vector2(depth.radiusZero,depth.radiusFull);
                    // When weighting is off and no manual radius exists, estimate only on this explicit preview action.
                    if (!depth.radiusWeightEnabled && radii.y<=0) radii=RadiusAutoCorrection.Estimate(descriptor,settings,part,group);
                    if(!float.IsNaN(radii.x)&&!float.IsNaN(radii.y)&&!float.IsInfinity(radii.x)&&!float.IsInfinity(radii.y)&&radii.x>=0&&radii.y>0&&radii.x<=radii.y) {
                        blendPreviewFrame=FullSetupGenerator.RadiusEstimateFrame(descriptor,settings,part,false).inverse;
                        blendPreviewRadii=radii; blendPreviewRadiiValid=true;
                    } else blendPreviewMessage=L("BlendShape を表示しています。半径を確認できないため円を省略しました。");
                } catch(Exception e) { blendPreviewMessage=SetupFeedback.Error(e, "BlendShape を表示しています。半径を確認できないため円を省略しました。"); }
                driver=CreateInstance<AnimationModeDriver>(); driver.hideFlags=HideFlags.HideAndDontSave;
                AnimationMode.StartAnimationMode(driver);
                if(!AnimationMode.InAnimationMode(driver)) throw new InvalidOperationException("Could not start owned AnimationMode.");
                previewOriginals=originals;
                foreach(var item in bindings) {
                    var modification=new PropertyModification { target=item.renderer, propertyPath=item.binding.propertyName,
                        value=item.original.ToString("R",System.Globalization.CultureInfo.InvariantCulture) };
                    AnimationMode.AddPropertyModification(item.binding,modification,item.keepOverride);
                    item.renderer.SetBlendShapeWeight(item.index,item.value);
                }
                blendPreviewDriver=driver; blendPreviewClip=clip; blendPreviewPartId=part.id; blendPreviewGroupId=group.id; driver=null; clip=null;
                previewSelectionId=Selection.activeInstanceID;
                EditorApplication.update += CheckPreviewSelection;
                SceneView.RepaintAll();
            } catch(Exception e) { blendPreviewMessage=SetupFeedback.Error(e, "プレビューを開始できませんでした。対象のメッシュを確認してください。"); }
            finally {
                if(driver!=null) { if(AnimationMode.InAnimationMode(driver)) AnimationMode.StopAnimationMode(driver); DestroyImmediate(driver); }
                if(clip!=null) DestroyImmediate(clip);
                if(blendPreviewDriver==null) RestorePreviewOriginals();
            }
        }
        private void DrawBlendPreviewScene(SceneView view) {
            if(blendPreviewDriver==null) return;
            if(Event.current.type==EventType.KeyDown && Event.current.keyCode==KeyCode.Escape) { EndBlendPreview(); Event.current.Use(); Repaint(); return; }
            if(Event.current.type!=EventType.Repaint || !blendPreviewRadiiValid) return;
            var matrix=Handles.matrix; var color=Handles.color;
            try { Handles.matrix=blendPreviewFrame; Handles.color=Color.cyan; Handles.DrawWireDisc(Vector3.zero,Vector3.forward,blendPreviewRadii.x); Handles.color=Color.magenta; Handles.DrawWireDisc(Vector3.zero,Vector3.forward,blendPreviewRadii.y); }
            finally { Handles.matrix=matrix; Handles.color=color; }
        }
        private void CheckPreviewSelection() {
            // The Hierarchy selection can become current before Unity dispatches selectionChanged.
            if (Selection.activeInstanceID != previewSelectionId) PreviewSelectionChanged();
        }
        private void PreviewSelectionChanged() {
            if(blendPreviewDriver==null) return;
            previewSelectionId=Selection.activeInstanceID;
            var selected=Selection.activeGameObject;
            if(selected==null || descriptor==null || !(selected.transform==descriptor.transform || selected.transform.IsChildOf(descriptor.transform))) { EndBlendPreview(); Repaint(); }
        }
        private void ResolveRadiusPreview(SocketSettings part, SocketAnimationGroup group, bool auto)
        {
            if(!FinishPreviewForOperation()) return;
            EditAnimationGroup(part,group,g=>{
                g.depthSettings.SetRadiusAutoCorrection(auto);
                if(auto) RadiusAutoCorrection.ResolvePreview(g.depthSettings,
                    ()=>RadiusAutoCorrection.Estimate(descriptor,settings,part,g),
                    ()=>RadiusAutoCorrection.NativeDefault(descriptor,settings,part,false));
            });
        }
        private void DrawRadiusAutoToggle(SocketSettings part, SocketAnimationGroup group)
        {
            bool auto=EditorGUILayout.ToggleLeft(new GUIContent(L("自動補正"), L("Plug 半径で重み付けを有効にすると使用できます。")),group.depthSettings.radiusAutoCorrection,GUILayout.Width(100));
            if(auto!=group.depthSettings.radiusAutoCorrection) ResolveRadiusPreview(part,group,auto);
        }
        private void DrawBlendPreviewControls(SocketSettings part, SocketAnimationGroup group)
        {
            bool active=blendPreviewDriver!=null && blendPreviewPartId==part.id && blendPreviewGroupId==group.id; var prior=GUI.backgroundColor;
            try {
                if(active) GUI.backgroundColor=new Color(.3f,.6f,1f,1f);
                if(GUILayout.Button(active ? L("プレビューを終了") : L("BlendShape プレビュー"))) {
                    // Consume the activation before AnimationMode callbacks can re-enter IMGUI.
                    if(Event.current!=null) Event.current.Use();
                    if(!blendPreviewTransition) {
                        blendPreviewTransition=true;
                        try { if(active) FinishPreviewForOperation(); else BeginBlendPreview(part,group); }
                        finally { blendPreviewTransition=false; }
                    }
                }
            } finally { GUI.backgroundColor=prior; }
            if(!string.IsNullOrEmpty(blendPreviewMessage)) EditorGUILayout.HelpBox(blendPreviewMessage,MessageType.Warning);
        }
        private void DrawRadiusCorrection(SocketSettings part,SocketAnimationGroup group)
        {
            var value=group.depthSettings;
            bool auto=value.radiusAutoCorrection;
            if(auto && GUILayout.Button(L("再計算"))) {
                ResolveRadiusPreview(part,group,true);
                value=part.animationGroups.Single(g=>g.id==group.id).depthSettings;
            }
            float zero=auto&&value.radiusAutoHasResult?value.radiusAutoZero:value.radiusZero;
            float full=auto&&value.radiusAutoHasResult?value.radiusAutoFull:value.radiusFull;
            bool hasMinimum = RadiusMinimumForDrawing();
            using(new EditorGUI.DisabledScope(auto)) {
                // The minimum is not present in early Fury versions. Never silently
                // rewrite an existing manual minimum merely because it is unavailable.
                if (hasMinimum) {
                    float lo=FullSetupCatalog.DistanceToSlider(zero), hi=FullSetupCatalog.DistanceToSlider(full);
                    bool input=Event.current!=null && AnimationRangeInputEvent(Event.current.type);
                    EditorGUI.BeginChangeCheck();EditorGUILayout.MinMaxSlider(ref lo,ref hi,.25f,1);
                    bool slider=EditorGUI.EndChangeCheck();
                    if(!auto && slider && input) { zero=FullSetupCatalog.SliderToDistance(lo);full=FullSetupCatalog.SliderToDistance(hi); }
                }
                using(new EditorGUILayout.HorizontalScope()) {
                    if(auto && !value.radiusAutoHasResult) {
                        if(hasMinimum) EditorGUILayout.LabelField(L("重み 0"), L("未計算"));
                        EditorGUILayout.LabelField(L("重み 1"), L("未計算"));
                    } else {
                        if(hasMinimum) zero=EditorGUILayout.FloatField(new GUIContent(L("重み 0"), L("深度アニメーションの重みが 0 になる Plug 半径です。Socket のローカル単位で入力します。")),zero);
                        full=EditorGUILayout.FloatField(new GUIContent(L("重み 1"), L("深度アニメーションの重みが最大になる Plug 半径です。Socket のローカル単位で入力します。")),full);
                    }
                    EditorGUILayout.LabelField("Socket Local",GUILayout.Width(85));
                }
                if(!hasMinimum && !auto && zero!=0) {
                    EditorGUILayout.HelpBox(L("この VRCFury には重み 0 の設定がありません。保存された手動値は保持しています。適用するには明示的に 0 にしてください。"),MessageType.Warning);
                    if(GUILayout.Button("手動の重み 0 を 0 にする")) zero=0;
                }
                if(!auto && AnimationFinite(zero)&&AnimationFinite(full)&&(zero!=value.radiusZero||full!=value.radiusFull))
                    EditAnimationGroup(part,group,g=>{g.depthSettings.radiusZero=zero;g.depthSettings.radiusFull=full;});
            }
            if(auto && !value.radiusAutoHasResult)
                EditorGUILayout.HelpBox(L("「再計算」を押してください。計算できない場合は生成時にFury標準値を使います。"),MessageType.Warning);
            if(auto && !string.IsNullOrEmpty(value.radiusAutoMessage))
                EditorGUILayout.HelpBox(value.radiusAutoMessage,MessageType.Warning);

        }
    }
}
