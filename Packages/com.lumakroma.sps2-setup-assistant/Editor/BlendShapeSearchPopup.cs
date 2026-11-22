using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    internal sealed class BlendShapeSearchPopup : PopupWindowContent
    {
        private const float RowHeight = 20;
        private const float HeaderHeight = 30;
        private readonly BlendShapeSelection selection;
        private readonly float width;
        private readonly SearchField search = new SearchField();
        private readonly BlendShapeImeGuard ime = new BlendShapeImeGuard();
        private Vector2 scroll;
        private GUIStyle rowStyle;
        private bool revealHighlight = true;
        private int pressedRow = -1;
        internal BlendShapeSearchPopup(BlendShapeSelection selection, float width)
        {
            this.selection = selection;
            this.width = Mathf.Clamp(width, 280, 560);
        }
        public override Vector2 GetWindowSize() => new Vector2(width, HeaderHeight +
            Mathf.Min(12, Mathf.Max(2, selection.RowCount)) * RowHeight + 4);
        public override void OnOpen()
        {
            rowStyle = new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip, wordWrap = false };
            editorWindow.wantsMouseMove = true;
            search.SetFocus();
            EditorApplication.update += CheckContext;
        }
        public override void OnClose()
        {
            selection.Cancel();
            EditorApplication.update -= CheckContext;
        }
        private void CheckContext()
        {
            if (!selection.IsCurrent && editorWindow != null) editorWindow.Close();
        }
        public override void OnGUI(Rect rect)
        {
            if (!selection.IsCurrent) { editorWindow.Close(); return; }
            var e = Event.current;
            bool enter = e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter;
            bool composing = ime.Observe(!string.IsNullOrEmpty(Input.compositionString),
                e.rawType == EventType.KeyDown && enter, e.rawType == EventType.KeyUp && enter);
            if (e.type == EventType.KeyDown && !composing)
            {
                if (e.keyCode == KeyCode.Escape) { e.Use(); editorWindow.Close(); return; }
                if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.DownArrow)
                {
                    selection.Move(e.keyCode == KeyCode.UpArrow ? -1 : 1);
                    revealHighlight = true;
                    editorWindow.Repaint();
                    e.Use();
                }
                else if (enter && selection.HighlightedRow >= 0)
                {
                    selection.Confirm(selection.HighlightedRow);
                    e.Use(); editorWindow.Close(); return;
                }
            }
            GUI.Label(new Rect(6, 5, 45, 20), L("検索"));
            string next = search.OnGUI(new Rect(52, 5, rect.width - 58, 20), selection.Query);
            if (selection.SetQuery(next)) { scroll = Vector2.zero; revealHighlight = true; pressedRow = -1; editorWindow.Repaint(); }
            var viewport = new Rect(2, HeaderHeight, rect.width - 4, rect.height - HeaderHeight - 2);
            if (revealHighlight && selection.HighlightedRow >= 0)
            {
                float top = selection.HighlightedRow * RowHeight;
                if (top < scroll.y) scroll.y = top;
                else if (top + RowHeight > scroll.y + viewport.height) scroll.y = top + RowHeight - viewport.height;
                revealHighlight = false;
            }
            int visibleRows = selection.MatchCount == 0 ? 2 : selection.RowCount;
            float contentWidth = viewport.width - 16;
            bool pointerInViewport = viewport.Contains(e.mousePosition);
            scroll = GUI.BeginScrollView(viewport, scroll,
                new Rect(0, 0, contentWidth, visibleRows * RowHeight), false, false);
            int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowHeight));
            int last = Mathf.Min(selection.RowCount, first + Mathf.CeilToInt(viewport.height / RowHeight) + 1);
            int clicked = -1;
            for (int row = first; row < last; row++)
            {
                var rowRect = new Rect(0, row * RowHeight, contentWidth, RowHeight);
                if (row == selection.HighlightedRow)
                    EditorGUI.DrawRect(rowRect, EditorGUIUtility.isProSkin ? new Color(.17f, .35f, .55f) : new Color(.55f, .73f, .9f));
                string name = row == 0 ? L("未設定") : selection.NameAtRow(row);
                GUI.Label(new Rect(6, rowRect.y, rowRect.width - 8, rowRect.height), new GUIContent(name, name), rowStyle);
                if (pointerInViewport && e.button == 0 && rowRect.Contains(e.mousePosition))
                {
                    if (e.type == EventType.MouseDown) { pressedRow = row; e.Use(); }
                    else if (e.type == EventType.MouseUp && pressedRow == row) { clicked = row; e.Use(); }
                }
            }
            if (selection.MatchCount == 0)
                GUI.Label(new Rect(6, RowHeight, contentWidth - 8, RowHeight), L("一致するシェイプがありません"), EditorStyles.miniLabel);
            GUI.EndScrollView();
            if (e.rawType == EventType.MouseUp) pressedRow = -1;
            if (clicked >= 0) { selection.Confirm(clicked); editorWindow.Close(); }
        }
    }
}
