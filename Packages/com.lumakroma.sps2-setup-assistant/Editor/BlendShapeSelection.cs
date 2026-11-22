using System;
using System.Collections.Generic;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    // A popup owns its name snapshot and tentative highlight, never the saved setting.
    internal sealed class BlendShapeSelection
    {
        private readonly string[] names;
        private readonly string current;
        private readonly Func<bool> isCurrent;
        private readonly Func<string, bool> commit;
        private readonly List<int> matches = new List<int>();
        private string query;
        internal int HighlightedRow { get; private set; } = -1;
        internal int MatchCount => matches.Count;
        internal int RowCount => matches.Count + 1; // Explicit unset stays available while filtering.
        internal bool Closed { get; private set; }
        internal bool IsCurrent => !Closed && isCurrent();
        internal string Query => query;
        internal BlendShapeSelection(string[] names, string current, Func<bool> isCurrent, Func<string, bool> commit)
        {
            this.names = (string[])names.Clone();
            this.current = current ?? "";
            this.isCurrent = isCurrent;
            this.commit = commit;
            SetQuery("");
        }
        internal string NameAtRow(int row) => row == 0 ? "" : names[matches[row - 1]];
        internal bool SetQuery(string value)
        {
            value = value ?? "";
            if (query == value) return false;
            query = value;
            matches.Clear();
            HighlightedRow = -1;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].IndexOf(value, StringComparison.OrdinalIgnoreCase) < 0) continue;
                matches.Add(i);
                if (names[i] == current && HighlightedRow < 0) HighlightedRow = matches.Count;
            }
            if (HighlightedRow < 0 && matches.Count > 0 && value.Length > 0) HighlightedRow = 1;
            if (current.Length == 0 && value.Length == 0) HighlightedRow = 0;
            return true;
        }
        internal void Move(int delta)
        {
            if (HighlightedRow < 0) HighlightedRow = delta < 0 ? RowCount - 1 : 0;
            else HighlightedRow = Math.Max(0, Math.Min(RowCount - 1, HighlightedRow + delta));
        }
        internal bool Confirm(int row)
        {
            if (!IsCurrent || row < 0 || row >= RowCount) return false;
            Closed = true;
            return commit(NameAtRow(row));
        }
        internal void Cancel() { Closed = true; }
    }

    // Keep IME's Enter available to the text field, including its final empty-composition event.
    internal sealed class BlendShapeImeGuard
    {
        private bool wasComposing;
        private bool confirmHeld;
        internal bool Observe(bool composing, bool confirmPressed, bool confirmReleased)
        {
            // Only an actual confirm key creates a release latch. Escape, mouse acceptance
            // and input-mode changes must not leave keyboard navigation disabled.
            if (confirmPressed && (composing || wasComposing)) confirmHeld = true;
            bool blocked = composing || confirmHeld;
            wasComposing = composing;
            if (confirmReleased) confirmHeld = false;
            return blocked;
        }
    }
}
