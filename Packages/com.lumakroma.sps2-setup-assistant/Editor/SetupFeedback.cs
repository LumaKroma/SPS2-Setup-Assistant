using System;
using UnityEditor;
using UnityEngine;
using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    internal enum OptionsChoice { Keep, Cancel, Replace }

    internal static class SetupFeedback
    {
        internal static OptionsChoice ReadChoice(int button)
        {
            switch (button) {
                case 0: return OptionsChoice.Keep;
                case 2: return OptionsChoice.Replace;
                default: return OptionsChoice.Cancel;
            }
        }

        internal static MessageType ApplyResult(bool complete, bool changed) =>
            complete ? MessageType.Info : changed ? MessageType.Warning : MessageType.Error;

        internal static string ApplyText(bool complete, string detail, string deferred)
        {
            if (string.IsNullOrEmpty(deferred)) return detail ?? L("反映先を確認できません。");
            if (complete || string.IsNullOrEmpty(detail)) return deferred;
            return detail + "\n" + deferred;
        }

        internal static string OptionalIssue(string diagnostic, string fallback)
        {
            if (TryUserReason(diagnostic, out var reason)) return reason;
            if (!string.IsNullOrEmpty(diagnostic)) Debug.LogWarning(diagnostic);
            return L(fallback);
        }

        internal static string Error(Exception exception, string fallback = "設定を反映できませんでした。詳細は Console で確認できます。", bool optional = false)
        {
            if (optional) Debug.LogWarning(exception);
            else Debug.LogException(exception);
            return TryUserReason(exception.Message, out var reason) ? reason : L(fallback);
        }
    }
}
