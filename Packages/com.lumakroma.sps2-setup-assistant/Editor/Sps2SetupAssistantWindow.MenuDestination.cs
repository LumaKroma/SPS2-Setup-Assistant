using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    public sealed partial class Sps2SetupAssistantWindow
    {
        [System.NonSerialized] private string menuSelectionError;
        [System.NonSerialized] private string checkedMenuPath;
        [System.NonSerialized] private bool savedMenuIsRelocated;

        private void SetMenuDestination(string path)
        {
            if (SpsMenuDestination.IsRelocatedDescendant(path))
            {
                menuSelectionError = L("ルートの SPS・Holes・Sockets の下は指定できません。直前の設定を保持しました。");
                Repaint();
                return;
            }
            menuSelectionError = null;
            if (path != (settings.sps3MenuPath ?? "")) Change(() => settings.sps3MenuPath = path);
            Repaint();
        }

        private void PickMenuDestination(VRCAvatarDescriptor avatar, SetupSettings draft, object selection)
        {
            if (this == null || avatar != descriptor || !ReferenceEquals(settings, draft)) return;
            if (SpsMenuDestination.TryResolveParent(avatar, selection, out var parent))
                SetMenuDestination(SpsMenuDestination.AtParent(parent, draft.sps3MenuPath));
            else
            {
                menuSelectionError = L("アバター内のサブメニューを1つ特定できません。メニューの一覧から選んでください。");
                Repaint();
            }
        }

        private void DrawSpsMenuDestination()
        {
            if (!Sps3OwnedGlobalOptions.SupportsMenuPath)
            {
                if (!string.IsNullOrWhiteSpace(settings.sps3MenuPath))
                    EditorGUILayout.HelpBox(L("この Fury では既定のメニュー位置を使います。"), MessageType.Info);
                return;
            }
            string previous = settings.sps3MenuPath ?? "";
            string parent = SpsMenuDestination.ParentOf(previous);
            var row = EditorGUILayout.GetControlRect();
            var value = EditorGUI.PrefixLabel(row, new GUIContent(L("追加先")));
            var select = new Rect(value.xMax - 48, value.y, 48, value.height);
            var field = new Rect(value.x, value.y, Mathf.Max(20, value.width - 52), value.height);
            bool canClear = parent.Length != 0;
            var clear = new Rect(field.xMax - 18, field.y, 18, field.height);
            if (canClear) field.width -= 18;
            using (new EditorGUI.DisabledScope(descriptor == null))
            {
                EditorGUI.BeginChangeCheck();
                var assigned = EditorGUI.ObjectField(field, GUIContent.none, null, typeof(Object), true);
                if (EditorGUI.EndChangeCheck() && assigned != null)
                    PickMenuDestination(descriptor, settings, assigned);
                if (Event.current.type == EventType.Repaint)
                {
                    string caption = parent.Length == 0 ? L("アバターのルートメニュー") : parent.Replace(@"\/", "／");
                    EditorStyles.objectField.Draw(field, new GUIContent(caption, previous), false, false, false, false);
                    GUI.skin.GetStyle("ObjectFieldButton").Draw(new Rect(field.xMax - 19, field.y, 19, field.height),
                        GUIContent.none, false, false, false, false);
                }
                if (canClear && GUI.Button(clear, "×", EditorStyles.miniButton))
                    SetMenuDestination(SpsMenuDestination.AtParent("", previous));
                if (GUI.Button(select, L("選択")))
                    ShowMenuParents(GUIUtility.GUIToScreenRect(select));
            }
            if (checkedMenuPath != settings.sps3MenuPath)
            {
                checkedMenuPath = settings.sps3MenuPath;
                savedMenuIsRelocated = SpsMenuDestination.IsRelocatedDescendant(checkedMenuPath);
            }
            if (!string.IsNullOrEmpty(menuSelectionError))
                EditorGUILayout.HelpBox(menuSelectionError, MessageType.Warning);
            else if (savedMenuIsRelocated)
                EditorGUILayout.HelpBox(L("保存済みのパスは Fury が移動するルートの内側です。再生成前に別の親を選んでください。"), MessageType.Warning);
            else if (!string.IsNullOrWhiteSpace(settings.sps3MenuPath) && settings.sps3MenuPath != "SPS")
                EditorGUILayout.HelpBox(L("Fury は既存のルート SPS・Holes・Sockets の内容も指定先へ移します。"), MessageType.Info);
        }

        private void ShowMenuParents(Rect anchor)
        {
            var avatar = descriptor;
            var draft = settings;
            if (SpsMenuDestination.TryOpenPicker(avatar, anchor, selected => PickMenuDestination(avatar, draft, selected),
                L("追加先"))) return;
            var menu = new GenericMenu();
            void Choose(string path)
            {
                if (this != null && avatar == descriptor && ReferenceEquals(settings, draft))
                    SetMenuDestination(path);
            }
            string active = SpsMenuDestination.ParentOf(draft.sps3MenuPath);
            menu.AddItem(new GUIContent(L("アバターのルートメニュー")), active.Length == 0,
                () => Choose(SpsMenuDestination.AtParent("", draft.sps3MenuPath)));
            foreach (var parent in SpsMenuDestination.Parents(avatar))
            {
                var target = SpsMenuDestination.AtParent(parent, draft.sps3MenuPath);
                var label = new GUIContent(parent.Replace(@"\/", "／") + "/" + L("ここに追加"));
                if (SpsMenuDestination.IsRelocatedDescendant(target)) menu.AddDisabledItem(label);
                else menu.AddItem(label, parent == active, () => Choose(target));
            }
            menu.ShowAsContext();
        }
    }
}