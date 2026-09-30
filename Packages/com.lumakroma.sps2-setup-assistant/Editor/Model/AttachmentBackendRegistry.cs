using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Model
{
    internal interface IAttachmentBackendAdapter
    {
        AttachmentBackend Backend { get; }

        string DisplayName { get; }

        void Apply(GameObject anchor, HumanBodyBones bone);
    }

    internal static class AttachmentBackendRegistry
    {
        private static readonly Dictionary<AttachmentBackend, IAttachmentBackendAdapter> Adapters =
            new Dictionary<AttachmentBackend, IAttachmentBackendAdapter>
            {
                [AttachmentBackend.VrcFury] = new VrcFuryAttachmentBackendAdapter(),
            };

        internal static IReadOnlyList<AttachmentBackend> AvailableBackends =>
            Adapters.Keys.OrderBy(backend => (int)backend).ToArray();

        internal static bool IsAvailable(AttachmentBackend backend)
        {
            return Adapters.ContainsKey(backend);
        }

        internal static string GetDisplayName(AttachmentBackend backend)
        {
            return Adapters.TryGetValue(backend, out var adapter)
                ? adapter.DisplayName
                : backend == AttachmentBackend.VrcFuryWithModularAvatar
                    ? "VRCFury + Modular Avatar"
                    : backend.ToString();
        }

        internal static void Register(IAttachmentBackendAdapter adapter)
        {
            if (adapter == null)
            {
                throw new ArgumentNullException(nameof(adapter));
            }

            Adapters[adapter.Backend] = adapter;
        }

        internal static bool TryApply(
            AttachmentBackend backend,
            GameObject anchor,
            HumanBodyBones bone,
            out string error)
        {
            if (!Adapters.TryGetValue(backend, out var adapter))
            {
                error = string.Format(L("追従方式 {0} は利用できません。"), GetDisplayName(backend));
                return false;
            }

            try
            {
                adapter.Apply(anchor, bone);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = string.Format(L("追従方式 {0} が失敗しました: {1}"), adapter.DisplayName, exception.Message);
                return false;
            }
        }

        private sealed class VrcFuryAttachmentBackendAdapter : IAttachmentBackendAdapter
        {
            public AttachmentBackend Backend => AttachmentBackend.VrcFury;

            public string DisplayName => "VRCFury";

            public void Apply(GameObject anchor, HumanBodyBones bone)
            {
                VrcFuryApi.Attach(anchor, bone);
            }
        }
    }
}
