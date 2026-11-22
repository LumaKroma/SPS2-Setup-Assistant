using System;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;
using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    [Serializable]
    internal sealed class SetupUpgradeReceipt
    {
        // Retain the project and shown fields from the original receipt. Old pending
        // package/version evidence alone does not qualify a project.
        public string project;
        public bool shown;
        internal bool WasShown(string here) => shown && here == project;
        internal void MarkShown(string here) { project = here; shown = true; }
    }

    internal sealed class SetupUpgradeImport : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            if (didDomainReload) SpsSetupUpgradeWindow.AfterReloadImport();
        }
    }

    internal sealed class SpsSetupUpgradeWindow : EditorWindow
    {
        private const string Title = "Setup Assistant が新しくなりました！";
        private static bool importReady, queued, deferredInPrefabStage, presenting;
        private static string ProjectKey => Hash128.Compute(Application.dataPath).ToString();

        [InitializeOnLoadMethod]
        private static void Start()
        {
            if (Application.isBatchMode || AssetDatabase.IsAssetImportWorkerProcess()) return;
            EditorSceneManager.sceneOpened += SceneOpened;
            PrefabStage.prefabStageClosing += PrefabClosing;
        }

        internal static void AfterReloadImport()
        {
            importReady = true;
            QueueCheck();
        }

        private static void SceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (IsNormal(scene)) QueueCheck();
        }

        private static void PrefabClosing(PrefabStage stage)
        {
            if (deferredInPrefabStage) QueueCheck();
        }

        private static bool IsNormal(Scene scene) =>
            scene.IsValid() && scene.isLoaded && !EditorSceneManager.IsPreviewScene(scene);

        private static void QueueCheck()
        {
            if (!importReady || queued || presenting || Application.isBatchMode || AssetDatabase.IsAssetImportWorkerProcess() ||
                EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
            queued = true;
            EditorApplication.delayCall += Check;
        }

        private static void Check()
        {
            queued = false;
            if (Application.isBatchMode || AssetDatabase.IsAssetImportWorkerProcess() ||
                EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) {
                // Only keep this event's pending check until the editor settles. No idle update callback.
                QueueCheck();
                return;
            }
            deferredInPrefabStage = PrefabStageUtility.GetCurrentPrefabStage() != null;
            if (deferredInPrefabStage || StageUtility.GetCurrentStageHandle() != StageUtility.GetMainStageHandle()) return;
            var history = SetupUpgradeHistory.instance;
            var project = ProjectKey;
            if (history.receipt.WasShown(project) || !HasLoadedLegacySetup()) return;

            presenting = true;
            try {
                var window = CreateInstance<SpsSetupUpgradeWindow>();
                window.titleContent = new GUIContent(L(Title));
                window.minSize = new Vector2(450, 170);
                window.position = new Rect(0, 0, 475, 185) { center = EditorGUIUtility.GetMainWindowPosition().center };
                window.ShowModalUtility();
                history.receipt.MarkShown(project);
                history.Persist();
            } finally { presenting = false; }
        }

        internal static bool HasLoadedLegacySetup()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++) {
                var scene = SceneManager.GetSceneAt(i);
                if (!IsNormal(scene)) continue;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var avatar in root.GetComponentsInChildren<VRCAvatarDescriptor>(true)) {
                        // Cheap rejection; the name alone is never positive evidence.
                        if (avatar.transform.Find(FullSetupGenerator.RootName) == null) continue;
                        if (Sps3LegacySetupPrompt.InspectForUpgradeNews(avatar).State ==
                            Sps3LegacySetupState.VerifiedLegacySetup) return true;
                    }
            }
            return false;
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorDefaultMargins)) {
                GUILayout.Space(10);
                EditorGUILayout.LabelField(L(Title), EditorStyles.boldLabel);
                GUILayout.Space(6);
                EditorGUILayout.LabelField(L("SPS3 のソケット生成に対応しました。"), EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(L("SPS2 Setup の設定は、再生成時に引き継げます。"), EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(L("新しい項目には、初回生成と同じ初期値が入ります。"), EditorStyles.wordWrappedLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.HorizontalScope()) {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(L("閉じる"))) Close();
                    if (GUILayout.Button(L("Setup を開く"))) {
                        EditorApplication.delayCall += Sps2SetupAssistantWindow.Open;
                        Close();
                    }
                }
                GUILayout.Space(8);
            }
        }
    }
}
