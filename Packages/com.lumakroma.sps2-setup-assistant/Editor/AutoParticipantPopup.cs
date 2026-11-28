using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    internal sealed class AutoParticipantPopup : PopupWindowContent
    {
        private AutoParticipantSelection selection;
        private readonly System.Func<AutoParticipantSelection> refresh;
        private readonly float width;
        private readonly SearchField search = new SearchField();
        private Vector2 scroll;
        internal AutoParticipantPopup(AutoParticipantSelection selection, float width, System.Func<AutoParticipantSelection> refresh = null)
        { this.selection = selection; this.refresh = refresh; this.width = Mathf.Clamp(width, 320, 560); }
        public override Vector2 GetWindowSize() => new Vector2(width, 360);
        public override void OnOpen() { search.SetFocus(); EditorApplication.update += CheckContext; }
        public override void OnClose() { selection?.Cancel(); EditorApplication.update -= CheckContext; }
        internal bool RefreshIfNeeded()
        {
            if (selection != null && selection.IsCurrent) return true;
            string query = selection?.Query ?? "";
            selection?.Cancel();
            selection = refresh?.Invoke();
            if (selection == null || !selection.IsCurrent) return false;
            selection.Query = query;
            return true;
        }
        private void CheckContext() { if (!RefreshIfNeeded() && editorWindow != null) editorWindow.Close(); }
        public override void OnGUI(Rect rect)
        {
            if (!RefreshIfNeeded()) { editorWindow.Close(); return; }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape && string.IsNullOrEmpty(Input.compositionString))
            { Event.current.Use(); editorWindow.Close(); return; }
            GUILayout.Label(L("Auto 参加部位を選択"), EditorStyles.boldLabel);
            selection.Query = search.OnGUI(selection.Query);
            if (selection.HasAmbiguousIds)
                EditorGUILayout.HelpBox(L("ID が不明・重複している部位は安全のため編集できません。"), MessageType.Warning);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            bool changed=false;
            foreach (var row in selection.VisibleRows) {
                bool next=EditorGUILayout.ToggleLeft(new GUIContent(row.Label, row.Id), row.Selected);
                if(next!=row.Selected) { row.Selected=next; changed=true; }
            }
            EditorGUILayout.EndScrollView();
            GUILayout.Label(string.Format(L("{0} 部位を選択（検索対象外も保持）"), selection.Rows.Count(r => r.Selected)), EditorStyles.miniLabel);
            if(changed) {
                string query=selection.Query;
                if(!selection.Apply()) { editorWindow.Close(); return; }
                selection=refresh?.Invoke();
                if(selection==null) { editorWindow.Close(); return; }
                selection.Query=query;
            }
        }
    }
}
