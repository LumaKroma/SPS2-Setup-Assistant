using System;
using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    public sealed partial class Sps2SetupAssistantWindow
    {
        private BlendShapePoint draggedBlendShapePoint;
        internal static void InitializeCustomEndpoints(DepthActionSettings action)
        {
            if(action.blendShapePoints != null && action.blendShapePoints.Count != 0) return;
            int index=action.renderer?.sharedMesh!=null ? action.renderer.sharedMesh.GetBlendShapeIndex(action.shape) : -1;
            // Without a selected shape keep the configured value at both ends. Never assume a zero start.
            float start=index>=0 ? action.renderer.GetBlendShapeWeight(index) : action.weight;
            action.blendShapePoints=new System.Collections.Generic.List<BlendShapePoint> {
                new BlendShapePoint { position=0,value=start }, new BlendShapePoint { position=1,value=action.weight }
            };
        }

        private void DrawBlendShapePointTrack(SocketSettings part,DepthActionSettings action)
        {
            var rect=EditorGUILayout.GetControlRect(false,24);rect.xMin+=8;rect.xMax-=8;
            int id=GUIUtility.GetControlID(FocusType.Passive);var e=Event.current;
            if(e.type==EventType.Repaint){
                EditorGUI.DrawRect(new Rect(rect.x,rect.center.y-2,rect.width,4),Color.gray);
                if(action.blendShapePoints!=null)foreach(var p in action.blendShapePoints.Where(p=>p!=null&&BlendShapePointPlan.Finite(p.position))){
                    float x=rect.x+Mathf.Clamp01(p.position)*rect.width;
                    EditorGUI.DrawRect(new Rect(x-4,rect.center.y-6,8,12),p.position<0||p.position>1?Color.red:Color.cyan);
                }
            }
            if(!OwnsDepthAction(part,action)||action.blendShapePoints==null)return;
            if(e.type==EventType.MouseDown&&e.button==0&&rect.Contains(e.mousePosition)){
                var interior=action.blendShapePoints.Skip(1).Take(Math.Max(0,action.blendShapePoints.Count-2))
                    .Where(p=>p!=null&&BlendShapePointPlan.Finite(p.position)).OrderBy(p=>Mathf.Abs(rect.x+p.position*rect.width-e.mousePosition.x)).FirstOrDefault();
                if(interior!=null&&Mathf.Abs(rect.x+interior.position*rect.width-e.mousePosition.x)<=10){draggedBlendShapePoint=interior;GUIUtility.hotControl=id;e.Use();}
            }
            if(GUIUtility.hotControl==id){
                if(e.type==EventType.MouseDrag&&action.blendShapePoints.Contains(draggedBlendShapePoint)){
                    float position=Mathf.Clamp01((e.mousePosition.x-rect.x)/Mathf.Max(1,rect.width));
                    if(draggedBlendShapePoint.position!=position)Change(()=>draggedBlendShapePoint.position=position);e.Use();
                } else if(e.type==EventType.MouseUp||!action.blendShapePoints.Contains(draggedBlendShapePoint)){
                    GUIUtility.hotControl=0;draggedBlendShapePoint=null;if(e.type==EventType.MouseUp)e.Use();
                }
            }
        }
        private void DrawBlendShapePoints(SocketSettings part,DepthActionSettings action)
        {
            int mode=EditorGUILayout.Popup(L("補間"),(int)action.blendShapeMode,new[]{L("通常"),L("カスタム")});
            if(mode!=(int)action.blendShapeMode && OwnsDepthAction(part,action)) {
                Change(()=>{
                    var current=action;
                    if(part.animationModelVersion==0 && mode==1){
                        int index=part.actions.IndexOf(action);part.InitializeAnimationGroups();
                        current=part.animationGroups.Single(g=>g.id==SocketAnimationGroup.LegacyId).actions[index];
                    }
                    current.blendShapeMode=(BlendShapeMode)mode;
                    // Explicit switch only: use current native value, not an assumed zero, for the start.
                    if(mode==1&&(current.blendShapePoints==null||current.blendShapePoints.Count==0)){
                        InitializeCustomEndpoints(current);
                    }
                });
                return;
            }
            if(action.blendShapeMode!=BlendShapeMode.Custom)return;
            var group=part.animationGroups?.FirstOrDefault(g=>g.actions.Contains(action));
            if(group==null){EditorGUILayout.HelpBox("グループ参照が不明です。元の設定を保持します。",MessageType.Warning);return;}
            int interpolation=EditorGUILayout.Popup(L("補間"),(int)action.blendShapeInterpolation,new[]{L("リニア"),L("スムーズ"),L("円弧 In/Out")});
            if(interpolation!=(int)action.blendShapeInterpolation && OwnsDepthAction(part,action))
                Change(()=>action.blendShapeInterpolation=(BlendShapeInterpolation)interpolation);
            var range=group.ActiveSettings.range;bool depth=group.kind==SocketAnimationKind.Depth;
            EditorGUILayout.LabelField("入力位置 / BlendShape 値 ("+group.ActiveSettings.units+")");
            if (depth) using (new EditorGUILayout.HorizontalScope()) { EditorGUILayout.LabelField("\u5916\u5074\uff08\u9060\uff09", EditorStyles.miniLabel); GUILayout.FlexibleSpace(); EditorGUILayout.LabelField("\u5185\u5074\uff08\u8fd1\uff09", EditorStyles.miniLabel, GUILayout.Width(70)); }
            DrawBlendShapePointTrack(part,action);
            if(action.blendShapePoints!=null)foreach(var point in action.blendShapePoints.ToArray()) {
                if(point==null)continue;
                bool endpoint=ReferenceEquals(point,action.blendShapePoints.First())||ReferenceEquals(point,action.blendShapePoints.Last());
                using(new EditorGUILayout.HorizontalScope()) {
                    float input=BlendShapePointPlan.Input(point.position,range.x,range.y,depth);
                    float next=input;
                    using(new EditorGUI.DisabledScope(endpoint))next=EditorGUILayout.FloatField(input);
                    float value=EditorGUILayout.FloatField(point.value);
                    if(BlendShapePointPlan.Finite(next)&&BlendShapePointPlan.Finite(value)&&(next!=input||value!=point.value)&&OwnsDepthAction(part,action)) {
                        if(range.x<range.y)Change(()=>{point.position=BlendShapePointPlan.Progress(next,range.x,range.y,depth);point.value=value;});
                    }
                    using(new EditorGUI.DisabledScope(endpoint))if(GUILayout.Button("−",GUILayout.Width(24))&&OwnsDepthAction(part,action))Change(()=>action.blendShapePoints.Remove(point));
                }
            }
            if(GUILayout.Button(new GUIContent("+", "中間点を追加"),GUILayout.Width(28))&&OwnsDepthAction(part,action))Change(()=>{
                if(action.blendShapePoints==null||action.blendShapePoints.Count<2){
                    action.blendShapePoints=new System.Collections.Generic.List<BlendShapePoint>{new BlendShapePoint{position=0,value=action.weight},new BlendShapePoint{position=1,value=action.weight}};return;
                }
                var list=action.blendShapePoints;int best=0;float gap=-1;
                for(int i=0;i<list.Count-1;i++)if(list[i]!=null&&list[i+1]!=null&&list[i+1].position-list[i].position>gap){best=i;gap=list[i+1].position-list[i].position;}
                if(gap>0){float mid=(list[best].position+list[best+1].position)*.5f;
                    if(mid>list[best].position&&mid<list[best+1].position)list.Insert(best+1,new BlendShapePoint{position=mid,value=(list[best].value+list[best+1].value)*.5f});}
            });
            try{BlendShapePointPlan.Validate(action);}catch(InvalidOperationException e){EditorGUILayout.HelpBox(TryUserReason(e.Message, out var reason) ? reason : L("BlendShape の制御点を確認してください。"),MessageType.Warning);}
        }
    }
}
