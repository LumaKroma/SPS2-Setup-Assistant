using System;
using System.Collections.Generic;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Pure index projection. The caller validates native control types and retains their payloads.
    internal static class Sps3MenuLayoutPlan
    {
        internal const int SettingsItem = -1;

        internal sealed class Plan
        {
            internal readonly int[] RootIndices;
            internal readonly int[] SettingsIndices;

            internal Plan(int[] rootIndices, int[] settingsIndices)
            {
                RootIndices = rootIndices;
                SettingsIndices = settingsIndices;
            }
        }

        internal static Plan Create(IReadOnlyList<string> nativeParameters,
            IReadOnlyCollection<string> socketParameters, string autoParameter, string legacyParameter)
        {
            if (nativeParameters == null) throw new ArgumentNullException(nameof(nativeParameters));
            if (socketParameters == null) throw new ArgumentNullException(nameof(socketParameters));
            if (socketParameters.Count == 0)
                throw new ArgumentException("At least one socket is required.", nameof(socketParameters));

            var expected = new HashSet<string>(StringComparer.Ordinal);
            var sockets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in socketParameters)
            {
                AddExpected(expected, parameter, nameof(socketParameters));
                sockets.Add(parameter);
            }
            if (autoParameter != null) AddExpected(expected, autoParameter, nameof(autoParameter));
            if (legacyParameter != null) AddExpected(expected, legacyParameter, nameof(legacyParameter));

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var socketIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            var autoIndex = -1;
            var legacyIndex = -1;
            for (var i = 0; i < nativeParameters.Count; i++)
            {
                var parameter = nativeParameters[i];
                if (string.IsNullOrWhiteSpace(parameter) || !expected.Contains(parameter) || !seen.Add(parameter))
                    throw new ArgumentException("Native controls must match unique expected parameters exactly.", nameof(nativeParameters));
                if (sockets.Contains(parameter)) socketIndices.Add(parameter, i);
                else if (string.Equals(parameter, autoParameter, StringComparison.Ordinal)) autoIndex = i;
                else legacyIndex = i;
            }
            if (seen.Count != expected.Count)
                throw new ArgumentException("A required native control is missing.", nameof(nativeParameters));

            var root = new List<int>();
            var settings = new List<int>();
            if (autoIndex >= 0) settings.Add(autoIndex);
            if (legacyIndex >= 0) settings.Add(legacyIndex);
            if (settings.Count == 1) root.Add(settings[0]);
            else if (settings.Count > 1) root.Add(SettingsItem);
            foreach (var parameter in socketParameters) root.Add(socketIndices[parameter]);
            return new Plan(root.ToArray(), settings.Count > 1 ? settings.ToArray() : Array.Empty<int>());
        }

        private static void AddExpected(HashSet<string> expected, string parameter, string argument)
        {
            if (string.IsNullOrWhiteSpace(parameter) || !expected.Add(parameter))
                throw new ArgumentException("Expected parameters must be nonempty and distinct.", argument);
        }

        // Content indices only: the adapter creates each nonfinal page's next-link control.
        // SettingsItem is accepted only once, at the beginning of the root projection.
        internal static int[][] Paginate(IReadOnlyList<int> items, int capacity = 8)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (capacity < 2 || capacity > 8) throw new ArgumentOutOfRangeException(nameof(capacity));
            var seen = new HashSet<int>();
            for (var i = 0; i < items.Count; i++)
                if (items[i] < SettingsItem || (items[i] == SettingsItem && i != 0) || !seen.Add(items[i]))
                    throw new ArgumentException("Indices must be unique; Settings may appear only first.", nameof(items));

            var pages = new List<int[]>();
            for (var offset = 0; offset < items.Count;)
            {
                var remaining = items.Count - offset;
                var length = remaining <= capacity ? remaining : capacity - 1;
                var page = new int[length];
                for (var i = 0; i < length; i++) page[i] = items[offset + i];
                pages.Add(page);
                offset += length;
            }
            return pages.ToArray();
        }
    }
}
