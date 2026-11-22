using System;
using System.Collections.Generic;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Model;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    // Detached transaction: filtering and toggles never mutate the draft or saved asset.
    internal sealed class AutoParticipantSelection
    {
        internal sealed class Row
        {
            internal readonly string Id, Label;
            internal readonly bool Initial;
            internal bool Selected;
            internal Row(string id, string label, bool selected) { Id = id; Label = label; Initial = Selected = selected; }
        }
        private readonly SetupSettings draft;
        private readonly Func<bool> current, validateApply;
        private readonly AutoDraftStamp draftStamp;
        private readonly Action<Action> commit;
        private readonly SocketSettings[] snapshot;
        private readonly bool[] included, effective;
        private readonly string[] ids;
        private bool closed;
        private readonly int eligibilityVersion, policyVersion;
        private readonly SpsFamily family;
        private readonly bool autoMode;
        internal readonly Row[] Rows;
        internal readonly bool HasAmbiguousIds;
        internal string Query = "";
        internal AutoParticipantSelection(SetupSettings draft, Func<SocketSettings, string> label, Func<bool> current, Action<Action> commit, Func<bool> validateApply = null)
        {
            this.draft = draft; this.current = current; this.commit = commit;
            this.validateApply = validateApply ?? current; draftStamp = new AutoDraftStamp(draft);
            eligibilityVersion = draft.sps3AutoEligibilityVersion; policyVersion = draft.sps3PolicyVersion;
            family = draft.family; autoMode = draft.autoMode;
            snapshot = (draft.parts ?? new List<SocketSettings>()).ToArray();
            included = snapshot.Select(p => p != null && p.included).ToArray();
            effective = snapshot.Select(draft.Sps3IncludeInAuto).ToArray();
            ids = snapshot.Select(p => p?.id).ToArray();
            var counts = snapshot.Where(p => p != null && !string.IsNullOrEmpty(p.id)).GroupBy(p => p.id).ToDictionary(g => g.Key, g => g.Count());
            HasAmbiguousIds = snapshot.Any(p => p != null && p.included && (string.IsNullOrEmpty(p.id) || counts[p.id] != 1));
            Rows = snapshot.Where(p => p != null && p.included && !string.IsNullOrEmpty(p.id) && counts[p.id] == 1)
                .Select(p => new Row(p.id, label(p), draft.Sps3IncludeInAuto(p))).ToArray();
        }
        internal bool IsCurrent
        {
            get
            {
                if (closed || !draftStamp.Matches(draft) || eligibilityVersion < 0 || eligibilityVersion > 1 ||
                    draft.sps3AutoEligibilityVersion != eligibilityVersion || draft.sps3PolicyVersion != policyVersion ||
                    draft.family != family || draft.autoMode != autoMode || !current() || draft.parts == null || draft.parts.Count != snapshot.Length) return false;
                for (int i = 0; i < snapshot.Length; i++)
                {
                    var p = draft.parts[i];
                    if (!ReferenceEquals(p, snapshot[i]) || p?.id != ids[i] ||
                        (p != null && p.included) != included[i] || draft.Sps3IncludeInAuto(p) != effective[i]) return false;
                }
                return true;
            }
        }
        internal IEnumerable<Row> VisibleRows => Rows.Where(r => string.IsNullOrEmpty(Query) ||
            r.Label.IndexOf(Query, StringComparison.OrdinalIgnoreCase) >= 0 || r.Id.IndexOf(Query, StringComparison.OrdinalIgnoreCase) >= 0);
        internal void Cancel() { closed = true; }
        internal bool Apply()
        {
            if (!IsCurrent) { closed = true; return false; }
            try { if (!validateApply() || !IsCurrent) { closed = true; return false; } }
            catch { closed = true; return false; }
            bool changed = Rows.Any(r => r.Selected != r.Initial);
            closed = true;
            if (!changed) return false;
            commit(() => {
                FullSetupCatalog.InitializeSps3Controls(draft);
                draft.BeginExplicitAutoSelection();
                // Only unique IDs captured from currently included sockets may be changed.
                foreach (var row in Rows)
                    draft.parts.Single(p => p != null && p.id == row.Id).sps3AutoParticipant = row.Selected;
            });
            return true;
        }
    }
}
