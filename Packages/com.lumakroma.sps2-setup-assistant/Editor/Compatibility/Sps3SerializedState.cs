using System;
using System.Collections.Generic;
using System.Linq;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Immutable comparison of observed serialized tokens. Does not invent native defaults or mutate input.
    internal sealed class Sps3SerializedState
    {
        private readonly Dictionary<string, string> values;
        internal Sps3SerializedState(IEnumerable<KeyValuePair<string, string>> input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in input)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value == null || values.ContainsKey(pair.Key))
                    throw new InvalidOperationException("Incomplete or duplicate serialized path.");
                values.Add(pair.Key, pair.Value);
            }
            if (values.Count == 0) throw new InvalidOperationException("Empty serialized snapshot.");
        }
        internal void RequireSame(Sps3SerializedState actual) => RequireOnlyChanges(actual, new string[0]);
        internal void RequireOnlyChanges(Sps3SerializedState actual, IEnumerable<string> allowedPaths)
        {
            if (actual == null || allowedPaths == null) throw new ArgumentNullException();
            var allowed = new HashSet<string>(allowedPaths, StringComparer.Ordinal);
            if (allowed.Any(string.IsNullOrEmpty)) throw new InvalidOperationException("Invalid allowed path.");
            foreach (var path in values.Keys.Union(actual.values.Keys))
            {
                if (allowed.Contains(path)) continue; // Exact paths only. No subtree or prefix exemptions.
                if (!values.TryGetValue(path, out var before) || !actual.values.TryGetValue(path, out var after) ||
                    !StringComparer.Ordinal.Equals(before, after))
                    throw new InvalidOperationException("Unexpected serialized change: " + path);
            }
        }
    }
}
