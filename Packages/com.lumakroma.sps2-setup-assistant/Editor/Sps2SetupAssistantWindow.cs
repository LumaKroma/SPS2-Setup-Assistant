using LumaKroma.Sps2SetupAssistant.Editor.Localization;
using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;
using System;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    public sealed partial class Sps2SetupAssistantWindow : EditorWindow
    {
        [SerializeField] private VRCAvatarDescriptor descriptor;
        [SerializeField] private SetupSettings settings;
        [SerializeField] private string avatarId;
        [SerializeField] private System.Collections.Generic.List<string> nameEditors = new System.Collections.Generic.List<string>();
        internal SetupSettings DraftSettings => settings;
        internal bool IsNameEditing(SocketSettings part) => nameEditors.Contains(part.id) || !string.IsNullOrWhiteSpace(part.menuNameOverride);
        internal void SetNameEditing(SocketSettings part, bool enabled)
        {
            Change(() => {
                if (enabled)
                {
                    if (!nameEditors.Contains(part.id)) nameEditors.Add(part.id);
                    if (string.IsNullOrWhiteSpace(part.menuNameOverride)) part.menuNameOverride = SocketDisplayNames.Resolve(part, settings.menuLanguage);
                }
                else { nameEditors.Remove(part.id); part.menuNameOverride = ""; }
            });
        }
        private int shapeSelectionRevision;
        private void InvalidateShapeSelection() { unchecked { shapeSelectionRevision++; } InvalidateAutoDisplayCache(); }
        private Vector2 scroll;
        private string message;
        private MessageType messageType;
        private Sps3LegacySetupNotice legacySetupNotice = Sps3LegacySetupNotice.None;
        private string packageVersionLabel;
        private GUIStyle titleStyle;
        private GUIStyle helpStyle;
        private readonly Vector3[] helpCirclePoints = new Vector3[33];
        private static string[] PresetLabels => new[] { L("カジュアル"), L("デフォルト"), L("フル") };
        private static string[] DepthUnitLabels => new[] { L("メートル"), L("プラグ長"), L("ローカル") };
        private static string[] ActionKindLabels => new[] { "BlendShape", "Animation Clip", L("オブジェクト ON・OFF") };
        private static readonly string[] ObjectStateLabels = { "ON", "OFF" };

        [MenuItem("Tools/LumaKroma/SPS Setup Assistant")]
        public static void Open()
        {
            bool isNewWindow = Resources.FindObjectsOfTypeAll<Sps2SetupAssistantWindow>().Length == 0;
            var window = GetWindow<Sps2SetupAssistantWindow>();
            window.InitializeMenuOpen(isNewWindow);
            window.titleContent = new GUIContent("SPS Setup");
            window.minSize = new Vector2(430, 520);
            window.Show();
        }
        private void RefreshLegacySetupNotice()
        {
            var next = Sps3LegacySetupPrompt.ObservedPackagePresent()
                ? Sps3LegacySetupPrompt.Inspect(descriptor) : Sps3LegacySetupNotice.None;
            if (legacySetupNotice == null || FuryNativeFeatureShapes.UiValueChanged(
                legacySetupNotice.Identity, (int)legacySetupNotice.State, next.Identity, (int)next.State))
                legacySetupNotice = next;
        }
        private void OnFocus()
        {
            RefreshUnnotifiedChangesOnFocus();
            var previous = legacySetupNotice;
            RefreshLegacySetupNotice();
            if (!ReferenceEquals(previous, legacySetupNotice)) Repaint();
        }
        private void OnEnable()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Sps2SetupAssistantWindow).Assembly);
            packageVersionLabel = string.IsNullOrEmpty(package?.version) ? null : "v" + package.version;
            // Restored drafts, including empty ones, are user data. Lifecycle callbacks
            // may restore a known binding but must not migrate or replace draft settings.
            if (settings == null) settings = SettingsForAvatar(null);
            FuryMinimalSps3Probe.InvalidatePreview();
            FuryPreviewEnvironmentCache.Invalidate();
            EnableAutoDisplayCache();
            EditorApplication.playModeStateChanged += PlayModeChanged;
            EditorApplication.delayCall += RecoverAvatar;
            SceneView.duringSceneGui += DrawBlendPreviewScene; Selection.selectionChanged += PreviewSelectionChanged; AssemblyReloadEvents.beforeAssemblyReload += EndBlendPreview; UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += StopPreviewOnSceneSaving;
        }
        private void OnDisable() { SaveWindowDraft(); SceneView.duringSceneGui -= DrawBlendPreviewScene; Selection.selectionChanged -= PreviewSelectionChanged; AssemblyReloadEvents.beforeAssemblyReload -= EndBlendPreview; UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= StopPreviewOnSceneSaving; EndBlendPreview(); DisableAutoDisplayCache(); InvalidateShapeSelection(); EditorApplication.playModeStateChanged -= PlayModeChanged; EditorApplication.delayCall -= RecoverAvatar; }
        private void PlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) { SaveWindowDraft(); EndBlendPreview(); RememberAvatar(); }
            if (state == PlayModeStateChange.EnteredEditMode) RecoverAvatar();
            Repaint();
        }
        private void RememberAvatar()
        {
            if (descriptor != null && !EditorApplication.isPlaying)
                avatarId = GlobalObjectId.GetGlobalObjectIdSlow(descriptor).ToString();
        }
        public void RecoverAvatar()
        {
            // hierarchyChanged already invalidates the display and requests a repaint
            // through AutoExternalContextChanged. Avoid a second request when recovery
            // did not change the binding; play-mode transitions repaint separately.
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var before = descriptor;
            if (descriptor == null && !string.IsNullOrEmpty(avatarId) && GlobalObjectId.TryParse(avatarId, out var id)) {
                var recovered = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as VRCAvatarDescriptor;
                // Only reload a persisted draft for the exact saved scene object.
                if (recovered != null && recovered.gameObject.scene.IsValid() && recovered.gameObject.scene.isLoaded &&
                    GlobalObjectId.GetGlobalObjectIdSlow(recovered).ToString() == avatarId) {
                    var key = WindowDraftKey(recovered);
                    if (key != null && EditorPrefs.HasKey(key)) BindAvatar(recovered);
                    else descriptor = recovered;
                }
            }
            RememberAvatar(); RefreshLegacySetupNotice();
            if (FuryNativeFeatureShapes.RecoveryNeedsRepaint(before, descriptor)) Repaint();
        }
        internal void InitializeMenuOpen(bool isNewWindow)
        {
            // Avatar discovery is synchronous and only for a newly opened window.
            // Refocusing an existing window must preserve an explicitly cleared draft.
            if (isNewWindow) DiscoverAvatarForExplicitRequest();
        }
        internal void DiscoverAvatarForExplicitRequest()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || descriptor != null) return;
            var selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<VRCAvatarDescriptor>() : null;
            var owned = UnityEngine.Object.FindObjectsOfType<VRCAvatarDescriptor>(true)
                .Where(a => a.gameObject.scene.IsValid() && FullSetupGenerator.HasGeneratedRoot(a)).ToArray();
            var candidate = selected != null ? selected : owned.Length == 1 ? owned[0] : null;
            if (candidate != null) BindAvatar(candidate);
        }
        [SerializeField] private Sps2SetupStorage.LegacyDraftOrigin legacyOrigin;
        private sealed class WindowDraftSession {
            internal Sps2SetupStorage.LegacyDraftOrigin legacyOrigin;
            internal VRCAvatarDescriptor avatar; internal SetupSettings settings;
            internal bool saveBlocked, cannotLoad; internal string warning; internal string[] unresolved, assignedReferences;
        }
        private static readonly System.Collections.Generic.Dictionary<int,WindowDraftSession> windowDraftSessions = new System.Collections.Generic.Dictionary<int,WindowDraftSession>();
        [NonSerialized] private bool draftSaveBlocked, draftCannotLoad;
        [NonSerialized] private string draftReferenceWarning;
        [NonSerialized] private string[] draftUnresolvedKeys;
        private string WindowDraftKey(VRCAvatarDescriptor avatar)
        {
            if(avatar==null || !avatar.gameObject.scene.IsValid() || !avatar.gameObject.scene.isLoaded || string.IsNullOrEmpty(avatar.gameObject.scene.path)) return null;
            var id=GlobalObjectId.GetGlobalObjectIdSlow(avatar);
            if(id.targetObjectId==0 || GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id)!=avatar) return null;
            return "LumaKroma.SpsSetup.WindowDraft.v1."+Hash128.Compute(Application.dataPath+"|"+id.ToString());
        }
        private string DescribeMissingDraftReferences()
        {
            var labels = settings?.parts?.Where(part => part != null && !string.IsNullOrEmpty(part.id) &&
                (draftUnresolvedKeys ?? Array.Empty<string>()).Any(key => key != null && key.StartsWith(part.id + ":", StringComparison.Ordinal)))
                .Select(part => SocketDisplayNames.Resolve(part, settings.menuLanguage)).Distinct().ToArray();
            return labels == null || labels.Length == 0 ? L("一部の保存済み参照を確認できません。該当欄を確認してください。") :
                string.Format(L("{0}の保存済み参照を確認できません。元の下書きは保持されています。該当欄を確認してください。"), string.Join(", ", labels));
        }
        private void SaveWindowDraft()
        {
            if(descriptor==null || settings==null || EditorApplication.isPlaying) return;
            try {
                windowDraftSessions[descriptor.GetInstanceID()]=new WindowDraftSession {legacyOrigin=legacyOrigin,avatar=descriptor,settings=settings.Copy(),saveBlocked=draftSaveBlocked,cannotLoad=draftCannotLoad,warning=draftReferenceWarning,unresolved=draftUnresolvedKeys?.ToArray(),assignedReferences=Sps2SetupStorage.AssignedDraftReferenceKeys(settings)};
                if(draftSaveBlocked || draftCannotLoad) return; // keep the original EditorPrefs record untouched
                var key=WindowDraftKey(descriptor);
                if(key==null) return; // unsaved scene: session copy remains available for this Editor session
                var json=Sps2SetupStorage.SerializeWindowDraftWithOrigin(descriptor,settings,legacyOrigin);
                EditorPrefs.SetString(key,json);
            } catch(Exception e) { Debug.LogWarning("SPS Setup: persistent draft was not saved; the session copy and prior saved draft were retained: "+e.Message,descriptor); }
        }
        public void BindAvatar(VRCAvatarDescriptor next)
        {
            SaveWindowDraft(); EndBlendPreview(); InvalidateShapeSelection();
            externalWatch = null; assetWatch = null; assetDependencies = null;
            descriptor = next; avatarId = null; nameEditors.Clear(); draftSaveBlocked=false; draftCannotLoad=false; draftReferenceWarning=null; draftUnresolvedKeys=null;
            legacyOrigin = default;
            bool restored = false;
            bool newDraft = false;
            try {
                if(next!=null && windowDraftSessions.TryGetValue(next.GetInstanceID(),out var session) && session.avatar==next) {
                    legacyOrigin=session.legacyOrigin; settings=session.settings.Copy(); restored=true; draftSaveBlocked=session.saveBlocked; draftCannotLoad=session.cannotLoad; draftReferenceWarning=session.warning; draftUnresolvedKeys=session.unresolved?.ToArray();
                    var destroyed=Sps2SetupStorage.ClearDestroyedDraftReferences(settings,session.assignedReferences);
                    if(destroyed.Length!=0) { draftSaveBlocked=true; draftUnresolvedKeys=(draftUnresolvedKeys??Array.Empty<string>()).Concat(destroyed).Distinct(StringComparer.Ordinal).ToArray(); draftReferenceWarning=DescribeMissingDraftReferences(); }
                } else {
                    var key=WindowDraftKey(next);
                    if(key!=null && EditorPrefs.HasKey(key)) {
                        try {
                            settings=Sps2SetupStorage.DeserializeWindowDraftWithOrigin(EditorPrefs.GetString(key),next,out var unresolved,out legacyOrigin); restored=true;
                            if(unresolved.Length!=0) { draftSaveBlocked=true; draftUnresolvedKeys=unresolved; draftReferenceWarning=DescribeMissingDraftReferences(); }
                        } catch { draftSaveBlocked=true; draftCannotLoad=true; throw; }
                    } else {
                        var existing=FullSetupGenerator.Find(next);
                        if (existing != null) {
                            bool savedAuto = existing.settings.autoMode;
                            FullSetupGenerator.SynchronizeNativeFloat(existing);
                            FullSetupGenerator.SynchronizeNativeAuto(existing, existing.settings);
                            if (existing.AuthoringContract == SpsAuthoringContract.LegacySps2 && existing.settings.family == SpsFamily.VrcFury)
                                legacyOrigin = new Sps2SetupStorage.LegacyDraftOrigin {
                                    identity = existing.identity, autoFromScene = existing.settings.autoMode != savedAuto };
                        }
                        var limited=existing==null ? FullSetupGenerator.FindLimitedDraft(next) : null;
                        restored=limited!=null; settings=limited ?? SettingsForAvatar(existing);
                        newDraft = existing == null && limited == null;
                        var missing = existing?.missingLegacyReferences?.ToList();
                        if (missing?.Count > 0) {
                            draftUnresolvedKeys = missing.ToArray();
                            draftSaveBlocked = true;
                            draftReferenceWarning = DescribeMissingDraftReferences();
                        }
                    }
                }
                if(next!=null && string.IsNullOrEmpty(next.gameObject.scene.path) && string.IsNullOrEmpty(draftReferenceWarning))
                    draftReferenceWarning="\u672a\u4fdd\u5b58\u30b7\u30fc\u30f3\u306e\u7de8\u96c6\u5024\u306f\u3053\u306e Editor \u30bb\u30c3\u30b7\u30e7\u30f3\u5185\u3067\u4fdd\u6301\u3057\u307e\u3059\u3002\u6c38\u7d9a\u5316\u306b\u306f\u30b7\u30fc\u30f3\u306e\u4fdd\u5b58\u304c\u5fc5\u8981\u3067\u3059\u3002";
                message=restored ? L("保存したウィンドウ設定を読み込みました。") : null;
                messageType=MessageType.Info;
            }
            catch(Exception e) { settings=FullSetupCatalog.CreateDefault(); SetMessage(SetupFeedback.Error(e),false); }
            if(!restored && !draftSaveBlocked) { FullSetupCatalog.UpgradeDisplayNames(settings); if (newDraft) FullSetupCatalog.DetectMouth(settings,next); }
            RememberAvatar(); RefreshLegacySetupNotice();
        }
        private void OnGUI()
        {
            BeginDepthDisplayPass();
            try {
                DrawWindow();
                // Build this only after initial display or a relevant invalidation,
                // never on every Layout/Repaint event.
                if (externalWatch == null) CaptureTransformWatch();
                if (assetWatch == null) CaptureAssetWatch();
            }
            finally { EndDepthDisplayPass(); }
        }
        private void DrawWindow()
        {
            if (titleStyle == null) titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 20 };
            EditorGUIUtility.labelWidth = 155;
            using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorDefaultMargins))
            {
                GUILayout.Space(20);
                var titleMode = descriptor == null ? FuryFeatureOutputPlan.Mode.LegacySps2 : ReadGenerationPreview(Event.current?.type).Route.Mode;
                string displayName = TitleForOutput(titleMode, Sps3ControlsForDrawing);
                if (titleContent.text != displayName) titleContent = new GUIContent(displayName);
                GUILayout.Label(displayName, titleStyle);
                if (packageVersionLabel != null) EditorGUILayout.LabelField(packageVersionLabel, EditorStyles.miniLabel);
                GUILayout.Space(10);
                var uiLanguage = (DisplayLanguage)EditorGUILayout.Popup(L("画面の言語"), (int)UiLanguage, LanguageNames);
                if (uiLanguage != UiLanguage) { UiLanguage = uiLanguage; VrcFuryCapabilities.Refresh(); message = null; Repaint(); }
                var menuLanguage = (DisplayLanguage)EditorGUILayout.Popup(L("メニューの言語"), (int)Normalize(settings.menuLanguage), LanguageNames);
                if (menuLanguage != settings.menuLanguage) Change(() => settings.menuLanguage = menuLanguage);
                var next = (VRCAvatarDescriptor)EditorGUILayout.ObjectField(L("アバター"), descriptor, typeof(VRCAvatarDescriptor), true);
                if (next != descriptor) Change(() => BindAvatar(next));
                if (descriptor != null && FullSetupGenerator.HasLimitedDraft(descriptor) && GUILayout.Button("保存済みの限定設定を読み込む")) {
                    try {
                        var saved = FullSetupGenerator.FindLimitedDraft(descriptor);
                        if (saved != null) { Change(() => settings = saved); SetMessage("限定出力の保存済み設定を読み込みました。既存の SPS と保存アセットは変更していません。", true); }
                    } catch (Exception e) { SetMessage(SetupFeedback.Error(e), false); }
                }
                if (legacySetupNotice.State == Sps3LegacySetupState.NeedsInspection)
                    EditorGUILayout.HelpBox(legacySetupNotice.Message(UiLanguage), MessageType.Warning);
                DrawFamilySelection();
                scroll = EditorGUILayout.BeginScrollView(scroll);
                ProfileDraw("SPS Setup/Compatibility", DrawCompatibility);
                ProfileDraw("SPS Setup/Presets", DrawPresets);
                ProfileDraw("SPS Setup/SocketParts", DrawSocketParts);
                if (!LimitedOutputForDrawing) {
                    ProfileDraw("SPS Setup/AutoOptions", DrawAutoOptions);
                    ProfileDraw("SPS Setup/LegacyOptions", DrawLegacyOptions);
                    ProfileDraw("SPS Setup/Options", DrawOptions);
                }
                EditorGUILayout.EndScrollView();
                ProfileDraw("SPS Setup/GenerationControls", DrawGenerationControls);
                GUILayout.Space(8);
            }
        }
        private static void ProfileDraw(string name, Action draw)
        {
            UnityEngine.Profiling.Profiler.BeginSample(name);
            try { draw(); }
            finally { UnityEngine.Profiling.Profiler.EndSample(); }
        }
        private static SetupSettings SettingsForAvatar(Sps2SetupContext existing)
        {
            if (existing != null) return existing.settings.Copy();
            var draft = CreateSettingsForAvatar(null, SpsAuthoringCompatibility.ForSelection(SpsFamily.VrcFury, null).CanGenerate,
                VrcFuryCapabilities.For(SpsFamily.SpsForNdmf).CanGenerate);
            if (draft.family == SpsFamily.VrcFury && SpsAuthoringCompatibility.ReviewedSps3Installed)
                draft = FullSetupCatalog.CreateSps3Default();
            return draft;
        }
        internal static SetupSettings CreateSettingsForAvatar(Sps2SetupContext existing, bool furyAvailable, bool ndmfAvailable)
        {
            if (existing != null) return existing.settings.Copy();
            var draft = FullSetupCatalog.CreateDefault();
            if (!furyAvailable && ndmfAvailable) draft.family = SpsFamily.SpsForNdmf;
            return draft;
        }
        private void DrawFamilySelection()
        {
            var snapshot = ReadFamilyDisplay(Event.current?.type);
            var fury = snapshot.Fury;
            var ndmf = snapshot.Ndmf;
            var selected = settings.family == SpsFamily.SpsForNdmf ? ndmf : fury;
            var rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, new GUIContent(L("SPS系列")));
            using (new EditorGUI.DisabledScope((!fury.CanGenerate && !ndmf.CanGenerate)))
            {
                var label = SpsBackend.Name(settings.family) + (selected.CanGenerate ? "" : " (" + L("利用不可") + ")");
                if (EditorGUI.DropdownButton(rect, new GUIContent(label), FocusType.Keyboard))
                {
                    var menu = new GenericMenu();
                    AddFamilyChoice(menu, SpsFamily.VrcFury, fury);
                    AddFamilyChoice(menu, SpsFamily.SpsForNdmf, ndmf);
                    menu.DropDown(rect);
                }
            }
            if (!string.IsNullOrEmpty(snapshot.Error)) EditorGUILayout.HelpBox(snapshot.Error, MessageType.Warning);
            else if (NeedsFamilyTransitionNotice(snapshot.ExistingFamily, settings.family))
                EditorGUILayout.HelpBox(string.Format(L("次の再生成で変更するSPS系列: {0} → {1}"),
                    SpsBackend.Name(snapshot.ExistingFamily.Value), SpsBackend.Name(settings.family)), MessageType.Info);
            // Unselected-family availability belongs in the disabled choice tooltip.
            // Keep an actionable warning only for the selected, unusable backend.
            if (!selected.CanGenerate)
                EditorGUILayout.HelpBox(SpsBackend.Name(settings.family)+": "+selected.BlockReason,MessageType.Warning);

        }
        internal static bool NeedsFamilyTransitionNotice(SpsFamily? generatedFamily, SpsFamily requestedFamily) =>
            generatedFamily.HasValue && Enum.IsDefined(typeof(SpsFamily), generatedFamily.Value) &&
            Enum.IsDefined(typeof(SpsFamily), requestedFamily) && generatedFamily.Value != requestedFamily;

        private void AddFamilyChoice(GenericMenu menu, SpsFamily family, VrcFuryCapabilities caps)
        {
            var label = SpsBackend.Name(family) + (caps.CanGenerate ? "" : " (" + L("利用不可") + ")");
            var content = new GUIContent(label, caps.BlockReason);
            if (!caps.CanGenerate) menu.AddDisabledItem(content, settings.family == family);
            else menu.AddItem(content, settings.family == family, () => {
                if (!SpsAuthoringCompatibility.ForSelection(family, descriptor).CanGenerate) return;
                if (family != settings.family) Change(() => { settings.family = family; VrcFuryCapabilities.Refresh(); });
            });
        }
        private void DrawPresets()
        {
            GUILayout.Space(10);
            GUILayout.Label(L("プリセット"), EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                var labels = PresetLabels;
                for (int i = 0; i < 3; i++)
                {
                    int value = i;
                    if (GUILayout.Button(labels[i], GUILayout.Height(28))) Change(() => { if (Sps3ControlsVisible) FullSetupCatalog.ApplySps3Preset(settings, value); else FullSetupCatalog.ApplyPreset(settings, value); });
                }
            }
        }

        private void DrawSocketParts()
        {
            for (int category = 0; category < FullSetupCatalog.Categories.Length; category++)
            {
                GUILayout.Space(20);
                GUILayout.Label(L(FullSetupCatalog.Categories[category]), EditorStyles.boldLabel);
                foreach (var part in settings.parts.Where(p => p.category == category).ToArray()) DrawPart(part);
                if (category == 5 && GUILayout.Button(L("＋ カスタム部位を追加"))) Change(() => settings.parts.Add(new SocketSettings
                {
                    id = Guid.NewGuid().ToString("N"), name = L("カスタム ") + (settings.parts.Count(p => p.custom) + 1),
                    custom = true, category = 5, included = true
                }));
            }
        }

        private bool Sps3ControlsVisible => TryReadSps3ControlsFresh(out _);
        private void Sps3Toggle(string label, bool value, Action<bool> setter)
        {
            Toggle(L(label), value, v => { if (!Sps3ControlsVisible) return; FullSetupCatalog.InitializeSps3Controls(settings); setter(v); });
        }
        private bool DrawSectionToggle(string title, bool value, bool available, Action<bool> setter)
        {
            GUILayout.Space(20);
            using(new EditorGUILayout.HorizontalScope()) {
                GUILayout.Label(title,EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                using(new EditorGUI.DisabledScope(!available)) {
                    bool next=EditorGUILayout.Toggle(value,GUILayout.Width(18));
                    if(next!=value) Change(()=>setter(next));
                    return next;
                }
            }
        }
        private void DrawAutoOptions()
        {
            bool native=Sps3ControlsForDrawing;
            var current=settings.sps3PolicyVersion>=1;
            bool enabled=native ? !current || settings.sps3AutoAvailable : settings.autoMode;
            if(!DrawSectionToggle("Auto",enabled,native || DepthCapabilitiesForDrawing.AutoMode,v=>{
                if(native) { if(!Sps3ControlsVisible)return; FullSetupCatalog.InitializeSps3Controls(settings); settings.sps3AutoAvailable=v; }
                else { settings.autoMode=v; legacyOrigin.autoFromScene=false; }
            }) || !native) return;
            Sps3Toggle("Auto 初期状態 ON", current && settings.sps3AutoDefaultOn, v => settings.sps3AutoDefaultOn = v);
            Sps3Toggle("Auto の状態を保存", current && settings.sps3AutoSaved, v => settings.sps3AutoSaved = v);
            DrawAutoParticipants();
        }
        private void DrawAutoParticipants()
        {
            var selected = settings.parts.Where(p => p != null && p.included && settings.Sps3IncludeInAuto(p)).ToArray();
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.PrefixLabel("Auto参加部位");
                var label=selected.Length==0 ? "未選択" : string.Join("、",selected.Select(p=>SocketDisplayNames.Resolve(p,settings.menuLanguage)));
                var content=new GUIContent(label,label);
                var button=GUILayoutUtility.GetRect(content,EditorStyles.popup);
                if(GUI.Button(button,content,EditorStyles.popup)) {
                    var selection=CreateAutoParticipantSelection();
                    if(selection!=null) PopupWindow.Show(button,new AutoParticipantPopup(selection,button.width,CreateAutoParticipantSelection));
                }
            }
            var capacity = GetAutoDisplaySnapshot().Capacity;
            EditorGUILayout.LabelField(new GUIContent(
                "Auto 候補: この選択 " + capacity.Draft + " / 現在全体 " +
                (capacity.Current.HasValue ? capacity.Current.Value.ToString() : "未確認") + " / 適用後見込み " +
                (capacity.Prospective.HasValue ? capacity.Prospective.Value.ToString() : "未確認"),
                "適用後見込みは、所有を確認できた現在の Setup 分を差し引き、今回の選択を加えた数です。外部・非アクティブの候補も枠を使います。生成成功や実際の優先順を保証する値ではありません。"), EditorStyles.miniLabel);
            if (capacity.ExceedsLimit)
                EditorGUILayout.HelpBox("Auto参加部位を減らしてください。アバター全体の先頭16個までがAuto対象です。対象外は手動メニューで使えます。", MessageType.Warning);
            else if (descriptor != null && capacity.PossibleOverflow)
                EditorGUILayout.HelpBox(capacity.Current.HasValue
                    ? "Auto参加部位を見直してください。適用後にアバター全体で16個を超える可能性があります。"
                    : "他のSetupや手動Socketも確認してください。Autoはアバター全体で16個までです。", MessageType.Warning);
        }
        internal AutoParticipantSelection CreateAutoParticipantSelection()
        {
            try { return OpenAutoParticipantSelection(); }
            catch (Exception e) { InvalidateAutoDisplayCache(); SetMessage(SetupFeedback.Error(e, "Auto 参加部位を開けませんでした。"), false); return null; }
        }
        private void DrawLegacyOptions()
        {
            bool native=Sps3ControlsForDrawing;
            var current=settings.sps3PolicyVersion>=1;
            bool tree=settings.sps3PolicyVersion==2;
            int nativeChildren=0;
            if(native && !tree) {
                try { nativeChildren=Sps3OwnedGlobalOptions.ObserveLegacyAnimationMask(descriptor); }
                catch(Exception) { EditorGUILayout.HelpBox(L("SPS Optionsの重複や参照を確認してください。Legacy設定は変更していません。"),MessageType.Warning);return; }
            }
            bool enabled=tree ? settings.sps3LegacyMaster : native && current ? settings.sps3LegacyAvailable || settings.legacy : settings.legacy;
            void Edit(Action<SetupSettings> change) {
                if(!Sps3ControlsVisible)return;
                int observed=settings.sps3PolicyVersion==2 ? 0 : Sps3OwnedGlobalOptions.ObserveLegacyAnimationMask(descriptor);
                FullSetupCatalog.InitializeSps3Controls(settings);
                settings.BeginLegacyTreeFromNative(observed); change(settings);
            }
            if(!DrawSectionToggle("Legacy",enabled,native || DepthCapabilitiesForDrawing.Legacy,
                v=>{if(native)Edit(s=>s.sps3LegacyMaster=v);else settings.legacy=v;}) || !native)return;
            bool plugs=tree?settings.sps3LegacyPlugs:current?settings.sps3LegacyAvailable:settings.legacy;
            bool sockets=tree?settings.sps3LegacySockets:settings.legacy;
            Sps3Toggle("旧Plug互換",plugs,v=>Edit(s=>s.sps3LegacyPlugs=v));
            using(new EditorGUILayout.HorizontalScope()) {
                GUILayout.Space(18);
                using(new EditorGUI.DisabledScope(!plugs))
                    Sps3Toggle("TPS Plug Animations",tree?settings.sps3LegacyTpsAnimations:(nativeChildren&1)!=0,v=>Edit(s=>s.sps3LegacyTpsAnimations=v));
            }
            Sps3Toggle("旧Socket互換",sockets,v=>Edit(s=>s.sps3LegacySockets=v));
            using(new EditorGUILayout.HorizontalScope()) {
                GUILayout.Space(18);
                using(new EditorGUI.DisabledScope(!sockets))
                    Sps3Toggle("DPS Socket Animations",tree?settings.sps3LegacyDpsAnimations:(nativeChildren&2)!=0,v=>Edit(s=>s.sps3LegacyDpsAnimations=v));
            }
            Sps3Toggle("Legacy 初期状態 ON",current?settings.sps3LegacyDefaultOn:settings.legacy,v=>Edit(s=>s.sps3LegacyDefaultOn=v));
            Sps3Toggle("Legacy の状態を保存",current&&settings.sps3LegacySaved,v=>Edit(s=>s.sps3LegacySaved=v));
        }

        private void DrawOptions()
        {
            GUILayout.Space(20);
            GUILayout.Label(L("その他の設定"), EditorStyles.boldLabel);
            if (settings.family == SpsFamily.VrcFury && AttachmentBackendRegistry.IsAvailable(AttachmentBackend.VrcFuryWithModularAvatar))
                Toggle("Modular Avatar で追従", settings.modularAvatar, v => settings.modularAvatar = v,
                    "Modular Avatar の Bone Proxy を使用して追従します。既存の生成物の方式を変更する場合は再生成が必要です。");
            CompatibleToggle(L("貫通"), settings.penetration, v => settings.penetration = v, DepthCapabilitiesForDrawing.Path,
                L("口-肛門の間にプラグが通る経路を生成します。非常に長いプラグの場合、体を貫通します。\n「首付近で太さを0にする」をオンにすると、口の入口では太さを保ち、首付近から体内の太さを0にします。出口の外では元の太さに戻します。"));
            if (!DepthCapabilitiesForDrawing.Path)
                EditorGUILayout.HelpBox("このFury版では貫通経路をまだ生成できません。貫通をオフにして生成してください。保存済みの経路設定は保持します。",MessageType.Warning);
            if (settings.penetration)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(18);
                    CompatibleToggle(L("首付近で太さを0にする"), !settings.showInternalThickness, v => settings.showInternalThickness = !v, DepthCapabilitiesForDrawing.Collapse);
                }
            }
            if (Sps3ControlsForDrawing)
                Sps3Toggle("ソケットの状態を保存", settings.sps3PolicyVersion >= 1 && settings.sps3SaveSockets, v => settings.sps3SaveSockets = v);
            GUILayout.Space(20);
        }

        internal static Sps3NewSetupPreflight InspectSps3Selection(VRCAvatarDescriptor selectedAvatar, SetupSettings selectedSettings)
        {
            bool exact = FuryMinimalSps3Probe.AvailableForPreview(out _);
            bool existing = selectedAvatar != null && (FullSetupGenerator.HasGeneratedRoot(selectedAvatar) ||
                selectedAvatar.transform.Find(Sps3Beta14SocketAdapter.RootName) != null ||
                SpsBackend.AllSockets(selectedAvatar.gameObject).Any(c => VrcFuryCompatibility.ReadIdentity(c) != null));
            bool live = selectedAvatar != null && !EditorUtility.IsPersistent(selectedAvatar) &&
                selectedAvatar.gameObject.scene.IsValid() && selectedAvatar.gameObject.scene.isLoaded &&
                !EditorApplication.isPlayingOrWillChangePlaymode && !PrefabUtility.IsPartOfPrefabAsset(selectedAvatar) &&
                !PrefabUtility.IsPartOfPrefabInstance(selectedAvatar);
            return Sps3NewSetupPreflight.Inspect(selectedSettings, exact, existing, live);
        }

        internal static string TitleForOutput(FuryFeatureOutputPlan.Mode mode, bool sps3) =>
            mode == FuryFeatureOutputPlan.Mode.NativeBasicSps3 ? "SPS3 Setup Assistant（基本 Socket）" :
            mode == FuryFeatureOutputPlan.Mode.PlacementOnly ? "SPS Setup Assistant (生成停止)" :
            sps3 ? "SPS3 Setup Assistant" : "SPS2 Setup Assistant";
        internal static bool IsLimitedOutput(FuryFeatureOutputPlan.Mode mode) =>
            mode == FuryFeatureOutputPlan.Mode.PlacementOnly || mode == FuryFeatureOutputPlan.Mode.NativeBasicSps3;
        private bool LimitedOutputForDrawing => descriptor != null && IsLimitedOutput(ReadGenerationPreview(Event.current?.type).Route.Mode);
        private bool BasicNativeForDrawing => descriptor != null && ReadGenerationPreview(Event.current?.type).Route.Mode == FuryFeatureOutputPlan.Mode.NativeBasicSps3;
        private void DrawGenerationControls()
        {
            var preview = ReadGenerationPreview(Event.current?.type);
            var sps3Blocker = preview.Blocker;
            var sps3Warning = preview.Warning;
            if (preview.Route.Mode == FuryFeatureOutputPlan.Mode.NativeBasicSps3)
                EditorGUILayout.HelpBox(VrcFuryCapabilities.BasicSocketNotice(), MessageType.Warning);
            if (preview.Route.Mode == FuryFeatureOutputPlan.Mode.PlacementOnly)
            {
                EditorGUILayout.HelpBox(FuryUnavailableNotice.Explain(preview.Route.Reason), MessageType.Warning);
                EditorGUILayout.HelpBox("既存の単一 SPS3 Socket は、設定変更がない場合に限り配置だけを更新できます。Socket 設定の変更・複数 Socket・経路付きは未対応です。", MessageType.Info);
                using (new EditorGUI.DisabledScope(descriptor == null || EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("既存 Socket の配置だけを更新"))
                    {
                        try {
                            if(!FinishPreviewForOperation()) return;
                            var plan = FullSetupGenerator.PrepareExistingPlacement(descriptor, settings);
                            if (EditorUtility.DisplayDialog("配置のみの更新", "既存 Socket と設定は変更せず、配置だけを更新します。シーンの変更は Undo できます。続行しますか？", "更新", "キャンセル")) {
                                FullSetupGenerator.CommitExistingPlacement(plan);
                                SetMessage("配置を更新しました。シーンを保存すると変更が保持されます。", true);
                            }
                        } catch (Exception e) { SetMessage(SetupFeedback.Error(e), false); }
                    }
            }
            if (!string.IsNullOrEmpty(sps3Warning)) EditorGUILayout.HelpBox(sps3Warning, MessageType.Warning);
            if (!string.IsNullOrEmpty(sps3Blocker)) EditorGUILayout.HelpBox(sps3Blocker, MessageType.Warning);
            if (!string.IsNullOrEmpty(draftReferenceWarning)) EditorGUILayout.HelpBox(draftReferenceWarning, MessageType.Warning);
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, messageType);
            bool generated = FullSetupGenerator.HasGeneratedRoot(descriptor);
            if (descriptor == null)
            {
                EditorGUILayout.HelpBox(L("上の「アバター」欄で対象を選択してください。"), MessageType.Info);
                if (GUILayout.Button(L("対象アバターを再検出"))) Change(() => DiscoverAvatarForExplicitRequest());
            }
            else if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox(L("再生を停止すると生成・再生成できます。"), MessageType.Info);
            using (new EditorGUI.DisabledScope(descriptor == null || preview.Route.Mode == FuryFeatureOutputPlan.Mode.PlacementOnly || (!generated && !string.IsNullOrEmpty(sps3Blocker)) || EditorApplication.isPlayingOrWillChangePlaymode || (preview.Route.Mode != FuryFeatureOutputPlan.Mode.NativeBasicSps3 && !DepthCapabilitiesForDrawing.CanGenerate) ||
                (settings.family == SpsFamily.VrcFury && !SpsAuthoringCompatibility.ProductionUiEnabled && SpsAuthoringCompatibility.ReviewedSps3Installed)))
            {
                if (!generated)
                {
                    if (GUILayout.Button(L("プレハブを生成"), GUILayout.Height(34))) Apply(true);
                }
                else
                {
                    using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(sps3Blocker)))
                    if (GUILayout.Button(L("プレハブを再生成"), GUILayout.Height(30))) Apply(true);
                    if (GUILayout.Button(L("置き換えずに変更を反映"), GUILayout.Height(30))) Apply(false);

                }
            }
            // Test plug creation is independent from the current generation draft.
            using (new EditorGUI.DisabledScope(descriptor == null || !generated || EditorApplication.isPlayingOrWillChangePlaymode || !DepthCapabilitiesForDrawing.TestPlug))
            {
                    using (new EditorGUI.DisabledScope(!DepthCapabilitiesForDrawing.TestPlug))
                    if (GUILayout.Button(L("貫通テストプラグ出現"), GUILayout.Height(30)))
                    {
                        if(!FinishPreviewForOperation()) return;
                        bool ok = FullSetupGenerator.ShowLongTestPlug(descriptor, out var error); SetMessage(error, ok);
                    }
                    using (new EditorGUI.DisabledScope(!DepthCapabilitiesForDrawing.TestPlug))
                    if (GUILayout.Button(L("テストプラグ出現"), GUILayout.Height(30)))
                    {
                        if(!FinishPreviewForOperation()) return;
                        bool ok = FullSetupGenerator.ShowTestPlug(descriptor, out var error); SetMessage(error, ok);
                    }
            }
        }

        private void DrawPlacementOnlyFallback()
        {
            EditorGUILayout.HelpBox(FuryUnavailableNotice.Explain(null), MessageType.Warning);
        }
        private void DrawPart(SocketSettings part)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string standard = SocketDisplayNames.Standard(part, UiLanguage);
                    string currentName = SocketDisplayNames.Resolve(part, settings.menuLanguage);
                    bool included = EditorGUILayout.ToggleLeft(new GUIContent(currentName, standard + " · " + currentName), part.included, GUILayout.MinWidth(60), GUILayout.ExpandWidth(true));
                    if (included != part.included) Change(() => part.included = included);
                    if (part.included)
                    {
                        using (new EditorGUI.DisabledScope(!BasicNativeForDrawing && (LimitedOutputForDrawing || !DepthCapabilitiesForDrawing.Depth)))
                        {
                        if (LimitedOutputForDrawing && !BasicNativeForDrawing) return;
                if (BasicNativeForDrawing || Sps3ControlsForDrawing || part.animationModelVersion != 0)
                        {
                            bool animation = EditorGUILayout.ToggleLeft(new GUIContent(L("アニメーション"), !DepthCapabilitiesForDrawing.Depth ? L("この構成では深度の保存先を確認できません。") : string.Empty), part.AnimationEnabled, GUILayout.Width(130));
                            if (animation != part.AnimationEnabled) Change(() => { part.InitializeAnimationGroups(); part.animationEnabled = animation; });
                        }
                        else
                        {
                        bool depth = EditorGUILayout.ToggleLeft(L("深度アクション"), part.depth && DepthCapabilitiesForDrawing.Depth, GUILayout.Width(EditorStyles.toggle.CalcSize(new GUIContent(L("深度アクション"))).x + 4));
                        if (DepthCapabilitiesForDrawing.Depth && depth != part.depth) Change(() => { part.depth = depth; if (depth && part.actions.Count == 0) part.actions.Add(new DepthActionSettings()); });
                        }
                        }
                    }
                    if (part.included)
                    {
                        bool editing = IsNameEditing(part);
                        bool nextEditing = EditorGUILayout.ToggleLeft(new GUIContent(L("表示名変更"), L("オフにするとメニュー言語の標準名へ戻ります。")), editing,
                            GUILayout.Width(EditorStyles.toggle.CalcSize(new GUIContent(L("表示名変更"))).x + 4));
                        if (nextEditing != editing) SetNameEditing(part, nextEditing);
                    }
                    if (part.custom && GUILayout.Button("−", GUILayout.Width(24))) Change(() => settings.parts.Remove(part));
                }
                if (part.custom)
                {
                    string name = EditorGUILayout.TextField(L("名前"), part.name);
                    if (name != part.name) Change(() => part.name = name);
                    var target = (Transform)EditorGUILayout.ObjectField(L("追従先"), part.target, typeof(Transform), true);
                    if (target != part.target) Change(() => part.target = target);
                }
                if (part.included && IsNameEditing(part))
                {
                    var customName = EditorGUILayout.TextField(new GUIContent(L("メニュー表示名"), L("空欄で標準名を使用します。個別名は言語変更後も保持されます。")), part.menuNameOverride ?? "");
                    if (customName != (part.menuNameOverride ?? "")) Change(() => part.menuNameOverride = customName);
                }
                if (LimitedOutputForDrawing && !BasicNativeForDrawing) return;
                if (BasicNativeForDrawing || Sps3ControlsForDrawing || part.animationModelVersion != 0)
                {
                    if (part.included && part.AnimationEnabled) DrawAnimationGroups(part, BasicNativeForDrawing);
                    return;
                }
                if (!part.included || !part.depth || !DepthCapabilitiesForDrawing.Depth) return;
                using (new EditorGUI.DisabledScope(!part.included))
                {
                    DrawRange(part);
                    foreach (var action in part.actions.ToArray()) DrawAction(part, action);
                    if (GUILayout.Button("＋", GUILayout.Width(28))) Change(() => part.actions.Add(new DepthActionSettings()));
                }
            }
        }
        private void DrawRange(SocketSettings part)
        {
            var projection = FullSetupCatalog.DepthRangeToSlider(part.range);
            float min = projection.x, max = projection.y;
            using (new EditorGUILayout.HorizontalScope()) { EditorGUILayout.LabelField(L("外側（遠）"), EditorStyles.miniLabel); GUILayout.FlexibleSpace(); EditorGUILayout.LabelField(L("内側（近）"), EditorStyles.miniLabel, GUILayout.Width(70)); }
            bool input = Event.current != null && AnimationRangeInputEvent(Event.current.type);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.MinMaxSlider(ref min, ref max, 0, 1);
            if (EditorGUI.EndChangeCheck() && input) Change(() => part.range = FullSetupCatalog.DepthRangeFromSlider(min, max));
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUIUtility.labelWidth = 50;
                float start = EditorGUILayout.FloatField(L("外側"), part.range.y), end = EditorGUILayout.FloatField(L("内側"), part.range.x);
                int units = EditorGUILayout.Popup((int)part.units, DepthUnitLabels, GUILayout.MinWidth(100));
                if (input && (start != part.range.y || end != part.range.x || units != (int)part.units))
                    if (!float.IsNaN(start) && !float.IsInfinity(start) && !float.IsNaN(end) && !float.IsInfinity(end))
                        Change(() => { start = Mathf.Clamp(start, -1, 3); end = Mathf.Clamp(end, -1, 3); part.range = new Vector2(Mathf.Min(start, end), Mathf.Max(start, end)); part.units = (DepthUnits)units; });
                EditorGUIUtility.labelWidth = 155;
            }
        }
        private bool HasUnresolvedRenderer(SocketSettings part, DepthActionSettings action)
        {
            if (part == null || action == null || draftUnresolvedKeys == null || action.renderer != null) return false;
            if (part.actions != null) {
                int index = part.actions.IndexOf(action);
                if (index >= 0 && draftUnresolvedKeys.Contains(part.id + ":" + index + ":renderer")) return true;
            }
            if (part.animationGroups != null) foreach (var group in part.animationGroups) {
                if (group?.actions == null) continue;
                int index = group.actions.IndexOf(action);
                if (index >= 0 && draftUnresolvedKeys.Contains(group.ReferenceKey(part.id, index) + ":renderer")) return true;
            }
            return false;
        }
        private void DrawAction(SocketSettings part, DepthActionSettings action, bool active = false)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (active) {
                        var caps = ActiveShapeForDrawing;
                        bool SupportedKind(DepthActionKind k) => k == DepthActionKind.BlendShape ? caps.BlendShape : k == DepthActionKind.AnimationClip ? caps.Clip : caps.ObjectOn || caps.ObjectOff;
                        var choices = new[] { DepthActionKind.BlendShape, DepthActionKind.AnimationClip, DepthActionKind.Object }.Where(k => caps.State && SupportedKind(k) || k == action.kind).ToArray();
                        int selected = Array.IndexOf(choices, action.kind);
                        var labels = choices.Select(k => ActionKindLabels[(int)k] + (caps.State && SupportedKind(k) ? "" : " (利用不可・設定保持)")).ToArray();
                        int picked = EditorGUILayout.Popup(selected, labels);
                        if (picked >= 0 && picked < choices.Length && choices[picked] != action.kind) Change(() => action.kind = choices[picked]);
                    } else {
                        int kind = EditorGUILayout.Popup((int)action.kind, ActionKindLabels);
                        if (kind != (int)action.kind) Change(() => action.kind = (DepthActionKind)kind);
                    }
                    if (GUILayout.Button("−", GUILayout.Width(24))) Change(() => RemoveAnimationAction(part, action));
                }
                if (active && !ActiveShapeForDrawing.Supports(action))
                    EditorGUILayout.HelpBox("この Active 操作は現在の Fury では利用できません。設定は保持します。", MessageType.Warning);
                if (action.kind == DepthActionKind.BlendShape)
                {
                    bool missingSavedRenderer = HasUnresolvedRenderer(part, action);
                    var previousContentColor = GUI.contentColor;
                    SkinnedMeshRenderer renderer;
                    try {
                        if (missingSavedRenderer) GUI.contentColor = new Color(1f, .32f, .32f, 1f);
                        renderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(L("メッシュ"), action.renderer, typeof(SkinnedMeshRenderer), true);
                    } finally { GUI.contentColor = previousContentColor; }
                    if (missingSavedRenderer) EditorGUILayout.HelpBox(L("保存済みのメッシュ参照を解決できません。対象を確認してください。"), MessageType.Warning);
                    if (renderer != action.renderer) SetDepthActionRenderer(part, action, renderer);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUIUtility.labelWidth = 62;
                        var rect = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), new GUIContent(L("シェイプ")));
                        string label = string.IsNullOrEmpty(action.shape) ? L("未設定") : action.shape;
                        if (EditorGUI.DropdownButton(rect, new GUIContent(label, label), FocusType.Keyboard))
                        {
                            var selection = BeginShapeSelection(part, action);
                            if (selection != null) PopupWindow.Show(rect, new BlendShapeSearchPopup(selection, rect.width));
                        }
                        EditorGUIUtility.labelWidth = 45;
                        float weight = (active || action.blendShapeMode == BlendShapeMode.Normal) ? EditorGUILayout.FloatField(L("値"), action.weight, GUILayout.Width(110)) : action.weight;
                        if (weight != action.weight) SetDepthActionWeight(part, action, weight);
                        EditorGUIUtility.labelWidth = 155;
                    }
                }
                if (action.kind == DepthActionKind.BlendShape) { if (!active) DrawBlendShapePoints(part, action); }
                else if (action.kind == DepthActionKind.AnimationClip)
                {
                    var clip = (AnimationClip)EditorGUILayout.ObjectField(action.clip, typeof(AnimationClip), false);
                    if (clip != action.clip) Change(() => action.clip = clip);
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var target = (GameObject)EditorGUILayout.ObjectField(action.target, typeof(GameObject), true);
                        var caps = active ? ActiveShapeForDrawing : default;
                        var states = active ? new[] { true, false }.Where(x => (x ? caps.ObjectOn : caps.ObjectOff) || x == action.objectOn).ToArray() : new[] { true, false };
                        var labels = states.Select(x => (x ? "ON" : "OFF") + (active && !(x ? caps.ObjectOn : caps.ObjectOff) ? " (利用不可)" : "")).ToArray();
                        int mode = EditorGUILayout.Popup(Array.IndexOf(states, action.objectOn), labels, GUILayout.Width(110));
                        bool nextOn = mode >= 0 && mode < states.Length ? states[mode] : action.objectOn;
                        if (target != action.target || nextOn != action.objectOn) Change(() => { action.target = target; action.objectOn = nextOn; });
                    }
                }
                GUILayout.Space(4);
            }
        }
        private bool OwnsDepthAction(SocketSettings part, DepthActionSettings action) =>
            settings != null && part != null && action != null && settings.parts.Contains(part) &&
            (part.animationModelVersion == 0 ? part.actions != null && part.actions.Contains(action) :
                part.animationGroups != null && part.animationGroups.Any(g => g?.actions != null && g.actions.Contains(action)));
        internal void SetDepthActionRenderer(SocketSettings part, DepthActionSettings action, SkinnedMeshRenderer renderer)
        {
            if (OwnsDepthAction(part, action) && action.renderer != renderer)
                Change(() => { action.renderer = renderer; action.shape = ""; });
        }
        internal void SetDepthActionWeight(SocketSettings part, DepthActionSettings action, float weight)
        {
            if (!OwnsDepthAction(part, action) || float.IsNaN(weight) || float.IsInfinity(weight)) return;
            weight = Mathf.Clamp(weight, 0, 100);
            if (weight != action.weight) Change(() => action.weight = weight);
        }
        internal BlendShapeSelection BeginShapeSelection(SocketSettings part, DepthActionSettings action)
        {
            InvalidateShapeSelection(); // Reopening also makes any queued callback from the previous popup stale.
            if (!OwnsDepthAction(part, action) || action.kind != DepthActionKind.BlendShape) return null;
            int revision = shapeSelectionRevision;
            var draft = settings;
            var avatar = descriptor;
            bool hadAvatar = avatar != null;
            var family = settings.family;
            var renderer = action.renderer;
            bool hadRenderer = renderer != null;
            var mesh = renderer != null ? renderer.sharedMesh : null;
            var savedShape = action.shape;
            var names = new string[mesh != null ? mesh.blendShapeCount : 0];
            for (int i = 0; i < names.Length; i++) names[i] = mesh.GetBlendShapeName(i);
            bool Current() => this != null && shapeSelectionRevision == revision &&
                ReferenceEquals(settings, draft) && descriptor == avatar && (avatar != null) == hadAvatar && settings.family == family &&
                OwnsDepthAction(part, action) && action.kind == DepthActionKind.BlendShape &&
                action.renderer == renderer && (renderer != null) == hadRenderer && action.shape == savedShape &&
                (renderer != null ? renderer.sharedMesh : null) == mesh &&
                (mesh != null ? mesh.blendShapeCount : 0) == names.Length;
            return new BlendShapeSelection(names, savedShape, Current, value => {
                if (!Current()) return false;
                // An imported/rebuilt mesh can retain its object identity and count. Validate the snapshot on commit only.
                for (int i = 0; i < names.Length; i++) if (mesh.GetBlendShapeName(i) != names[i]) return false;
                if (value != action.shape) Change(() => action.shape = value);
                return true;
            });
        }
        internal static bool CanProceedWithLimitedNative(VRCAvatarDescriptor avatar, Func<bool> confirm) =>
            !FullSetupGenerator.HasLimitedNativeOutput(avatar) || FullSetupGenerator.HasGeneratedRoot(avatar) || confirm();
        private SetupSettings DraftForOutput() => Sps2SetupStorage.DraftOutputForUnresolved(settings,draftUnresolvedKeys);
        internal SetupSettings UpgradeDraftForRegeneration(Sps2SetupContext old, out string[] unresolved)
        {
            unresolved = draftUnresolvedKeys;
            if (old == null || old.AuthoringContract != SpsAuthoringContract.LegacySps2 ||
                old.settings.family != SpsFamily.VrcFury || settings.family != SpsFamily.VrcFury) return null;
            return legacyOrigin.ForRegeneration(old.identity, settings, old.settings.autoMode, old.asset.stateJson, ref unresolved);
        }
        private void Apply(bool regenerate)
        {
            if(!FinishPreviewForOperation()) return;
            if (draftCannotLoad) { SetMessage(L("保存済みの下書きを読み込めません。元の下書きは保持されています。"),false); return; }
            if (!regenerate && descriptor != null) {
                try {
                    var existing = FullSetupGenerator.Find(descriptor);
                    if (existing != null && existing.settings.family != settings.family) {
                        SetMessage(L("基盤の変更には再生成が必要です。"), false);
                        return;
                    }
                    if (existing != null) {
                        // Project the draft for this operation only. A missing optional reference
                        // never authorizes clearing the native group or saving a filtered draft.
                        // The leaf writer matches the user's actual group, renderer and shape.
                        // Substituting an old group's weight here could overwrite a newer native value.
                        var draft = settings.Copy();
                        var deferred = draftSaveBlocked ? DescribeMissingDraftReferences() : null;
                        bool ok = FullSetupGenerator.ApplySceneFields(existing, draft, out var detail, out var changed);
                        if (changed) SaveWindowDraft();
                        SetMessage(SetupFeedback.ApplyText(ok, detail, deferred),
                            SetupFeedback.ApplyResult(ok && deferred == null, changed));
                        return;
                    }
                } catch (Exception e) { SetMessage(SetupFeedback.Error(e), false); return; }
            }
            var outputSettings = DraftForOutput()?.Copy();
            SetupSettings upgradeDraft = null;
            string[] upgradeMissing = null;
            // Decide before the fresh-regeneration transaction removes anything.
            try {
                var existing = FullSetupGenerator.Find(descriptor);
                if (regenerate && existing == null) { Apply(false); return; }
                var contract = SpsAuthoringCompatibility.SelectContract(outputSettings.family, regenerate ? null : existing, false);
                if (contract == SpsAuthoringContract.Sps3Beta14) {
                    if (regenerate) {
                        upgradeDraft = UpgradeDraftForRegeneration(existing, out upgradeMissing);
                        if (upgradeDraft != null)
                            outputSettings = Sps2SetupStorage.DraftOutputForUnresolved(upgradeDraft, upgradeMissing).Copy();
                    }
                    var snapshot = Sps3OwnedGlobalOptions.CaptureConflict(descriptor, existing);
                    if (snapshot.slots > 0 && !snapshot.onlyOwned) {
                        if (regenerate && snapshot.Inside(existing.transform)) {
                            // Both choices require keeping the confirmed host alive through commit.
                            SetMessage(L("対象の SPS Options が再生成するルート内にあります。元の場所での保持も安全な置換もできないため、再生成を開始しませんでした。") +
                                "\n" + string.Join("\n", snapshot.paths), false);
                            return;
                        }
                        var duplicate = snapshot.slots > 1 ? "\n" + L("複数の Options を残すと Fury の重複エラーが残ります。") : "";
                        var choice = SetupFeedback.ReadChoice(EditorUtility.DisplayDialogComplex(L("SPS Options の確認"),
                            L("対象の SPS Options") + "\n" + string.Join("\n", snapshot.paths) + duplicate + "\n\n" +
                            L("置き換えずに反映すると、既存の Options を保持して他の設定を反映します。置き換えると、一覧の Options を現在の設定に変更します。"),
                            L("置き換えずに反映"), L("キャンセル"), L("置き換える")));
                        if (choice == OptionsChoice.Cancel) return;
                        using (Sps3OwnedGlobalOptions.Keep(snapshot, replace: choice == OptionsChoice.Replace)) {
                            Sps3OwnedGlobalOptions.ValidateKeep(descriptor, regenerate ? existing.transform : null);
                            ApplyAfterOptionsDecision(regenerate, outputSettings, upgradeDraft, upgradeMissing);
                        }
                        return;
                    }
                }
                ApplyAfterOptionsDecision(regenerate, outputSettings, upgradeDraft, upgradeMissing);
            } catch (Exception e) { SetMessage(SetupFeedback.Error(e), false); }
        }
        private void ApplyAfterOptionsDecision(bool regenerate, SetupSettings outputSettings, SetupSettings upgradeDraft, string[] upgradeMissing) {
            if (regenerate) {
                try {
                    var existing=FullSetupGenerator.Find(descriptor);
                    if(existing==null || (upgradeDraft != null && existing.identity != legacyOrigin.identity)) { SetMessage("生成対象が変わりました。操作をやり直してください。", false); return; }
                    var additions=RegenerationImpact.Inspect(existing,null,additionsOnly:true);
                    RegenerationImpact.Report freshConfirmed=null;
                    if(additions.AdditionalObjects.Count!=0 || additions.AdditionalComponents.Count!=0) {
                        var details=additions.AdditionalObjects.Concat(additions.AdditionalComponents).Take(10);
                        if(!EditorUtility.DisplayDialog(L("再生成の確認"), L("置き換えると次の追加物が失われます。")+"\n"+string.Join("\n",details)+"\n\n"+L("キャンセルすると変更しません。"), L("置き換える"), L("キャンセル"))) return;
                        freshConfirmed=additions;
                    }
                    bool freshResult=FullSetupGenerator.ReplaceWithFresh(descriptor,outputSettings,existing,freshConfirmed,out var freshMessage);
                    if (freshResult && upgradeDraft != null) {
                        settings = upgradeDraft;
                        draftUnresolvedKeys = upgradeMissing;
                        legacyOrigin = default;
                        InvalidateShapeSelection();
                        SaveWindowDraft();
                    }
                    ShowGenerationResult(freshMessage, freshResult, freshResult);
                } catch(Exception e) { SetMessage(SetupFeedback.Error(e),false); }
                return;
            }
            var route = FuryNormalOutputRouter.Resolve(descriptor, outputSettings);
            if (route.Mode == FuryFeatureOutputPlan.Mode.NativeBasicSps3) {
                bool created = FullSetupGenerator.ApplyBasicSocketOnly(descriptor, outputSettings, out _, out var basicMessage);
                ShowGenerationResult(basicMessage, created, created);
                return;
            }
            if (route.Mode == FuryFeatureOutputPlan.Mode.PlacementOnly) {
                SetMessage(FuryUnavailableNotice.Explain(route.Reason), false);
                return;
            }
            // Basic native output is intentionally not owned by the full SPS generator.
            // Never silently add a second active Socket beside it.
            if (!CanProceedWithLimitedNative(descriptor, () => EditorUtility.DisplayDialog("基本 Socket と通常生成の確認",
                    "既存の基本 Socket を残したまま通常の SPS を作成すると、Socket が重複して動作する可能性があります。既存の Prefab と設定は削除しません。両方を残して生成しますか？",
                    "両方を残して生成", "キャンセル"))) return;            bool migrating = false;
            RegenerationImpact.Report confirmed = null;
            try {
                var current = FullSetupGenerator.Find(descriptor);
                migrating = current != null && current.settings.family != outputSettings.family;
                if (regenerate && current != null) {
                    if (FullSetupGenerator.HasUnconvertedLegacyOffset(current)) {
                        SetMessage(FullSetupGenerator.LegacyOffsetPreservedNoticeFor(current, outputSettings), false);
                        return;
                    }
                    // The Local scale plan enforces this proof after confirmation as well.
                    // Run the same read-only gate first so a confirmation never promises
                    // replacement when that replacement is certain to be blocked.
                    if (current.AuthoringContract == SpsAuthoringContract.Sps3Beta14 &&
                        SpsAuthoringCompatibility.HasRetainedActiveLocalDepth(outputSettings, current)) {
                        try { Sps3SingleDepthScaleProjection.RequireRetainedEquivalentGroups(outputSettings, current.settings); }
                        catch (InvalidOperationException e) {
                            SetMessage(SetupFeedback.Error(e, "既存の Local 深度を安全に再生成できませんでした。"), false);
                            return;
                        }
                    }
                    var impact = RegenerationImpact.Inspect(current, outputSettings);
                    // A confirmation about extra children must never authorize losing
                    // unrelated external references or unmatched native settings.
                    // Until their preservation is proven, report and stop without a modal.
                    if (impact.ExternalReferences.Count != 0 || impact.NativeEdits.Count != 0) {
                        SetMessage("外部参照または保存済み Socket 設定の保全を確認できないため、置き換えを行いません。既存の生成物は変更されていません。", false);
                        return;
                    }
                    if (impact.AdditionalObjects.Count != 0 || impact.AdditionalComponents.Count != 0) {
                        string Items(string title, System.Collections.Generic.List<string> values) => values.Count == 0 ? "" :
                            "\n" + title + " (" + values.Count + ")\n" + string.Join("\n", values.Take(5)) + (values.Count > 5 ? "\n…" : "") + "\n";
                        string detail = Items("生成物内の追加オブジェクト", impact.AdditionalObjects) +
                            Items("生成物内の追加コンポーネント", impact.AdditionalComponents);
                        if (!EditorUtility.DisplayDialog("再生成の確認", "生成物を置き換えると、次の追加物が失われる場合があります。\n" + detail +
                            "\nキャンセルすると変更しません。", "置き換える", "キャンセル")) return;
                        confirmed = impact;
                    }
                }
            }
            catch (Exception e) { SetMessage(SetupFeedback.Error(e, "再生成で影響を受ける対象を確認できませんでした。"), false); return; }
            bool ok = confirmed == null
                ? FullSetupGenerator.ApplyWithOutcome(descriptor, outputSettings, regenerate, out var root, out var result, out var changed)
                : FullSetupGenerator.ApplyWithConfirmedImpact(descriptor, outputSettings, confirmed, out root, out result, out changed);
            // Keep the user's window draft authoritative; generated settings are output, not a replacement draft.
            RefreshLegacySetupNotice();
            ShowGenerationResult(result, ok, changed);
        }
        private void ShowGenerationResult(string notes, bool ok, bool changed)
        {
            if (string.IsNullOrWhiteSpace(notes)) SetMessage(ok ? (changed ? L("生成しました。シーンを保存してください。") : L("変更はありません。")) : L("生成できませんでした。"), ok);
            else SetMessage(notes, ok && changed ? MessageType.Warning : MessageType.Error);
        }
        private void SetMessage(string text, bool success) =>
            SetMessage(text, success ? MessageType.Info : MessageType.Error);
        private void SetMessage(string text, MessageType nextType) {
            if (!FuryNativeFeatureShapes.UiValueChanged(message, (int)messageType, text, (int)nextType)) return;
            message = text; messageType = nextType; Repaint();
        }
        private void DrawCompatibility()
        {
            var caps = DepthCapabilitiesForDrawing;
            var route = ReadGenerationPreview(Event.current?.type).Route;
            var notes = caps.Notices(route.Mode == FuryFeatureOutputPlan.Mode.NativeBasicSps3);
            if (notes.Count == 0) return;
            if (settings.family == SpsFamily.SpsForNdmf && caps.CanGenerate)
                EditorGUILayout.LabelField(L("基本 Socket は生成できます。"));
            EditorGUILayout.HelpBox(SpsBackend.Name(settings.family) + " " + caps.Version + "\n" + string.Join("\n", notes) + (caps.CanGenerate ? "" : "\n" + SpsBackend.Guide(settings.family)), MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(L("選択した系列の導入案内"))) Application.OpenURL(SpsBackend.GuideUrl(settings.family));
                if (GUILayout.Button(L("互換性を再確認"))) { VrcFuryCapabilities.Refresh(); Repaint(); }
            }
        }

        private void CompatibleToggle(string label, bool value, Action<bool> set, bool available, string tooltip = null)
        {
            bool allowDisable = !available && value && settings.family == SpsFamily.VrcFury && FuryMinimalSps3Probe.AvailableForPreview(out _);
            using (new EditorGUI.DisabledScope(!available && !allowDisable))
                Toggle(label, value && (available || allowDisable), v => { if (available || (allowDisable && !v)) set(v); }, available ? tooltip : L("この版では利用できません。") + SpsBackend.Guide(settings.family));
        }

        private void Toggle(string label, bool value, Action<bool> set, string tooltip = null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var content = new GUIContent(label, tooltip);
                bool hasTooltip = !string.IsNullOrEmpty(tooltip);
                bool next = hasTooltip
                    ? EditorGUILayout.ToggleLeft(content, value, GUILayout.Width(EditorStyles.label.CalcSize(content).x + 16))
                    : EditorGUILayout.ToggleLeft(content, value);
                if (hasTooltip)
                {
                    DrawHelp(tooltip);
                    GUILayout.FlexibleSpace();
                }
                if (next != value) Change(() => set(next));
            }
        }
        private void DrawHelp(string tooltip)
        {
            var rect = GUILayoutUtility.GetRect(16, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(false));
            rect.y += 2; // Match the adjacent ToggleLeft text's vertical inset.
            if (helpStyle == null) helpStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold,
                padding = new RectOffset(), margin = new RectOffset()
            };
            if (Event.current.type == EventType.Repaint)
            {
                var points = helpCirclePoints;
                for (int i = 0; i < points.Length; i++)
                {
                    float angle = i * Mathf.PI * 2 / (points.Length - 1);
                    points[i] = rect.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 6;
                }
                var previous = Handles.color;
                try
                {
                    Handles.color = helpStyle.normal.textColor * new Color(1, 1, 1, GUI.enabled ? .8f : .4f);
                    Handles.DrawAAPolyLine(1.5f, points);
                }
                finally { Handles.color = previous; }
            }
            GUI.Label(rect, new GUIContent("?", tooltip), helpStyle);
        }
        private void Change(Action change)
        {
            var beforeSettings = settings;
            var beforeAvatar = descriptor;
            var beforeJson = settings == null ? null : JsonUtility.ToJson(settings);
            Undo.RecordObject(this, L("SPS2 設定"));
            try { change(); }
            catch { InvalidateShapeSelection(); externalWatch = null; assetWatch = null; assetDependencies = null; throw; }
            var afterJson = settings == null ? null : JsonUtility.ToJson(settings);
            if (!FuryNativeFeatureShapes.DraftChanged(beforeSettings, settings, beforeAvatar, descriptor, beforeJson, afterJson)) return;
            if(!draftCannotLoad && draftUnresolvedKeys!=null) {
                draftUnresolvedKeys=Sps2SetupStorage.StillMissingDraftReferences(settings,draftUnresolvedKeys);
                if(draftUnresolvedKeys.Length==0) { draftSaveBlocked=false; draftReferenceWarning=null; }
                else draftReferenceWarning=DescribeMissingDraftReferences();
            }
            EndBlendPreview(); InvalidateShapeSelection(); externalWatch = null; assetWatch = null; assetDependencies = null;
            EditorUtility.SetDirty(this); Repaint();
        }
    }
}
