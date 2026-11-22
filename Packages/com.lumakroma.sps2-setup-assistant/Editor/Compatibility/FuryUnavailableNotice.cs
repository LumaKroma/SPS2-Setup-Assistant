namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    internal static class FuryUnavailableNotice
    {
        internal const string GuideUrl = "https://vrcfury.com/download/";
        internal static string Explain(string reason) =>
            "保存可能な SPS Socket を確認できないため、生成を中止します。既存のシーンと生成物は変更しません。\n" +
            (string.IsNullOrEmpty(reason) ? "" : "理由: " + reason + "\n") +
            "VRCFury の最新版を公式手順で導入し、必要に応じて Unity を再読み込みしてください。\n" + GuideUrl;
    }
}
