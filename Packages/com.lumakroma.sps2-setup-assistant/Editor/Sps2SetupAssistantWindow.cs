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
    public sealed class Sps2SetupAssistantWindow : EditorWindow
    {
        [SerializeField] private VRCAvatarDescriptor descriptor;
        [SerializeField] private SetupSettings settings;
        [SerializeField] private string avatarId;
        private Vector2 scroll;
        private string message;
        private MessageType messageType;
        private GUIStyle titleStyle;
        private GUIStyle helpStyle;
        private readonly Vector3[] helpCirclePoints = new Vector3[33];
        private static string[] PresetLabels => new[] { L("カジュアル"), L("デフォルト"), L("フル") };
        private static string[] DepthUnitLabels => new[] { L("メートル"), L("プラグ長"), L("ローカル") };
        private static string[] ActionKindLabels => new[] { "BlendShape", "Animation Clip", L("オブジェクト ON・OFF") };
        private static readonly string[] ObjectStateLabels = { "ON", "OFF" };

        [MenuItem("Tools/LumaKroma/SPS2 Setup Assistant")]
        public static void Open()
        {
            var window = GetWindow<Sps2SetupAssistantWindow>();
            window.titleContent = new GUIContent("SPS2 Setup");
            window.minSize = new Vector2(430, 520);
            window.Show();
        }
        private void OnEnable()
        {
            if (settings == null || settings.parts.Count == 0) settings = FullSetupCatalog.CreateDefault();
            FullSetupCatalog.UpgradeDisplayNames(settings);
            Undo.undoRedoPerformed += Repaint;
            EditorApplication.hierarchyChanged += RecoverAvatar;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            EditorApplication.delayCall += RecoverAvatar;
        }
        private void OnDisable() { Undo.undoRedoPerformed -= Repaint; EditorApplication.hierarchyChanged -= RecoverAvatar; EditorApplication.playModeStateChanged -= PlayModeChanged; EditorApplication.delayCall -= RecoverAvatar; }
        private void PlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) RememberAvatar();
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
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Repaint(); return; }
            if (descriptor == null && !string.IsNullOrEmpty(avatarId) && GlobalObjectId.TryParse(avatarId, out var id))
                descriptor = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as VRCAvatarDescriptor;
            if (descriptor == null && string.IsNullOrEmpty(avatarId))
            {
                var selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<VRCAvatarDescriptor>() : null;
                var owned = UnityEngine.Object.FindObjectsOfType<VRCAvatarDescriptor>(true)
                    .Where(a => a.gameObject.scene.IsValid() && FullSetupGenerator.HasGeneratedRoot(a)).ToArray();
                var candidate = selected != null ? selected : owned.Length == 1 ? owned[0] : null;
                if (candidate != null) BindAvatar(candidate);
            }
            RememberAvatar(); Repaint();
        }
        public void BindAvatar(VRCAvatarDescriptor next)
        {
            descriptor = next; avatarId = null;
            try { settings = FullSetupGenerator.Find(next)?.settings.Copy() ?? FullSetupCatalog.CreateDefault(); message = null; }
            catch (Exception e) { settings = FullSetupCatalog.CreateDefault(); SetMessage(e.Message, false); }
            FullSetupCatalog.UpgradeDisplayNames(settings);
            FullSetupCatalog.DetectMouth(settings, next); RememberAvatar();
        }
        private void OnGUI()
        {
            if (titleStyle == null) titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 20 };
            EditorGUIUtility.labelWidth = 155;
            using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorDefaultMargins))
            {
                GUILayout.Space(12);
                GUILayout.Label("SPS2 Setup Assistant", titleStyle);
                GUILayout.Space(10);
                var uiLanguage = (DisplayLanguage)EditorGUILayout.Popup(L("画面の言語"), (int)UiLanguage, LanguageNames);
                if (uiLanguage != UiLanguage) { UiLanguage = uiLanguage; VrcFuryCapabilities.Refresh(); message = null; Repaint(); }
                var menuLanguage = (DisplayLanguage)EditorGUILayout.Popup(L("メニューの言語"), (int)Normalize(settings.menuLanguage), LanguageNames);
                if (menuLanguage != settings.menuLanguage) Change(() => settings.menuLanguage = menuLanguage);
                var next = (VRCAvatarDescriptor)EditorGUILayout.ObjectField(L("アバター"), descriptor, typeof(VRCAvatarDescriptor), true);
                if (next != descriptor) Change(() => BindAvatar(next));
                scroll = EditorGUILayout.BeginScrollView(scroll);
                DrawCompatibility();
                DrawPresets();
                DrawSocketParts();
                DrawOptions();
                EditorGUILayout.EndScrollView();
                DrawGenerationControls();
                GUILayout.Space(8);
            }
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
                    if (GUILayout.Button(labels[i], GUILayout.Height(28))) Change(() => FullSetupCatalog.ApplyPreset(settings, value));
                }
            }
        }

        private void DrawSocketParts()
        {
            for (int category = 0; category < FullSetupCatalog.Categories.Length; category++)
            {
                GUILayout.Space(12);
                GUILayout.Label(L(FullSetupCatalog.Categories[category]), EditorStyles.boldLabel);
                foreach (var part in settings.parts.Where(p => p.category == category).ToArray()) DrawPart(part);
                if (category == 5 && GUILayout.Button(L("＋ カスタム部位を追加"))) Change(() => settings.parts.Add(new SocketSettings
                {
                    id = Guid.NewGuid().ToString("N"), name = L("カスタム ") + (settings.parts.Count(p => p.custom) + 1),
                    custom = true, category = 5, included = true
                }));
            }
        }

        private void DrawOptions()
        {
            GUILayout.Space(12);
            CompatibleToggle(L("貫通"), settings.penetration, v => settings.penetration = v, VrcFuryCapabilities.Current.Path,
                L("口-肛門の間にプラグが通る経路を生成します。非常に長いプラグの場合、体を貫通します。\n「首付近で太さを0にする」をオンにすると、口の入口では太さを保ち、首付近から体内の太さを0にします。出口の外では元の太さに戻します。"));
            if (settings.penetration)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(18);
                    CompatibleToggle(L("首付近で太さを0にする"), !settings.showInternalThickness, v => settings.showInternalThickness = !v, VrcFuryCapabilities.Current.Collapse);
                }
            }
            CompatibleToggle(L("Auto Mode"), settings.autoMode, v => settings.autoMode = v, VrcFuryCapabilities.Current.AutoMode,
                L("プラグに最も近い対象ソケットを自動で有効にするメニューを追加します。"));
            CompatibleToggle(L("後方互換性"), settings.legacy, v => settings.legacy = v, VrcFuryCapabilities.Current.Legacy,
                L("SPS1・DPS・TPSのプラグにも対応させ、互換機能の切り替えメニューを追加します。\nSPS2同士だけで使う場合はオフにできます。"));
            CompatibleToggle(L("Local Only"), settings.localOnly, v => settings.localOnly = v, VrcFuryCapabilities.Current.LocalOnly);
            GUILayout.Space(8);
            Toggle(L("Modular Avatarで追従"), settings.modularAvatar, v => settings.modularAvatar = v);
            GUILayout.Space(12);
        }

        private void DrawGenerationControls()
        {
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, messageType);
            bool generated = FullSetupGenerator.HasGeneratedRoot(descriptor);
            if (descriptor == null)
            {
                EditorGUILayout.HelpBox(L("対象アバターを選択してください。以前の対象が開かれている場合は再検出できます。"), MessageType.Info);
                if (GUILayout.Button(L("対象アバターを再検出"))) Change(() => { avatarId = null; RecoverAvatar(); });
            }
            else if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox(L("再生を停止すると生成・再生成できます。"), MessageType.Info);
            using (new EditorGUI.DisabledScope(descriptor == null || EditorApplication.isPlayingOrWillChangePlaymode || !VrcFuryCapabilities.Current.CanGenerate))
            {
                if (!generated)
                {
                    if (GUILayout.Button(L("プレハブを生成"), GUILayout.Height(34))) Apply(true);
                }
                else
                {
                    if (GUILayout.Button(L("プレハブを再生成"), GUILayout.Height(30))) Apply(true);
                    if (GUILayout.Button(L("置き換えずに変更を反映"), GUILayout.Height(30))) Apply(false);
                    using (new EditorGUI.DisabledScope(!VrcFuryCapabilities.Current.TestPlug))
                    if (GUILayout.Button(L("貫通テストプラグ出現"), GUILayout.Height(30)))
                    {
                        bool ok = FullSetupGenerator.ShowLongTestPlug(descriptor, out var error); SetMessage(error, ok);
                    }
                    using (new EditorGUI.DisabledScope(!VrcFuryCapabilities.Current.TestPlug))
                    if (GUILayout.Button(L("テストプラグ出現"), GUILayout.Height(30)))
                    {
                        bool ok = FullSetupGenerator.ShowTestPlug(descriptor, out var error); SetMessage(error, ok);
                    }
                }
            }
        }

        private void DrawPart(SocketSettings part)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool included = EditorGUILayout.ToggleLeft(part.custom ? L("使用") : SocketDisplayNames.Standard(part, UiLanguage), part.included, GUILayout.MinWidth(part.custom ? 55 : 140));
                    if (included != part.included) Change(() => part.included = included);
                    if (part.custom)
                    {
                        string name = EditorGUILayout.TextField(part.name);
                        if (name != part.name) Change(() => part.name = name);
                    }
                    if (part.included)
                    {
                        using (new EditorGUI.DisabledScope(!VrcFuryCapabilities.Current.Depth))
                        {
                        bool depth = EditorGUILayout.ToggleLeft(L("深度アクション"), part.depth && VrcFuryCapabilities.Current.Depth, GUILayout.MinWidth(114));
                        if (VrcFuryCapabilities.Current.Depth && depth != part.depth) Change(() => { part.depth = depth; if (depth && part.actions.Count == 0) part.actions.Add(new DepthActionSettings()); });
                        }
                    }
                    if (part.custom && GUILayout.Button("−", GUILayout.Width(24))) Change(() => settings.parts.Remove(part));
                }
                if (part.custom)
                {
                    var target = (Transform)EditorGUILayout.ObjectField(L("追従先"), part.target, typeof(Transform), true);
                    if (target != part.target) Change(() => part.target = target);
                }
                if (part.included)
                {
                    var customName = EditorGUILayout.TextField(new GUIContent(L("メニュー表示名"), L("空欄で標準名を使用します。個別名は言語変更後も保持されます。")), part.menuNameOverride ?? "");
                    if (customName != (part.menuNameOverride ?? "")) Change(() => part.menuNameOverride = customName);
                    EditorGUILayout.LabelField(L("表示名プレビュー"), SocketDisplayNames.Resolve(part, settings.menuLanguage));
                    if (!string.IsNullOrWhiteSpace(part.menuNameOverride) && GUILayout.Button(L("標準名に戻す"))) Change(() => part.menuNameOverride = "");
                }
                if (!part.included || !part.depth || !VrcFuryCapabilities.Current.Depth) return;
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
            float min = FullSetupCatalog.DistanceToSlider(part.range.x), max = FullSetupCatalog.DistanceToSlider(part.range.y);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.MinMaxSlider(ref min, ref max, 0, 1);
            if (EditorGUI.EndChangeCheck()) Change(() => part.range = new Vector2(FullSetupCatalog.SliderToDistance(min), FullSetupCatalog.SliderToDistance(max)));
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUIUtility.labelWidth = 50;
                float start = EditorGUILayout.FloatField(L("近"), part.range.x), end = EditorGUILayout.FloatField(L("遠"), part.range.y);
                int units = EditorGUILayout.Popup((int)part.units, DepthUnitLabels, GUILayout.MinWidth(100));
                if (start != part.range.x || end != part.range.y || units != (int)part.units)
                    if (!float.IsNaN(start) && !float.IsInfinity(start) && !float.IsNaN(end) && !float.IsInfinity(end))
                        Change(() => { start = Mathf.Clamp(start, -1, 3); end = Mathf.Clamp(end, -1, 3); part.range = new Vector2(Mathf.Min(start, end), Mathf.Max(start, end)); part.units = (DepthUnits)units; });
                EditorGUIUtility.labelWidth = 155;
            }
        }
        private void DrawAction(SocketSettings part, DepthActionSettings action)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    int kind = EditorGUILayout.Popup((int)action.kind, ActionKindLabels);
                    if (kind != (int)action.kind) Change(() => action.kind = (DepthActionKind)kind);
                    if (GUILayout.Button("−", GUILayout.Width(24))) Change(() => part.actions.Remove(action));
                }
                if (action.kind == DepthActionKind.BlendShape)
                {
                    var renderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(L("メッシュ"), action.renderer, typeof(SkinnedMeshRenderer), true);
                    if (renderer != action.renderer) Change(() => { action.renderer = renderer; action.shape = ""; });
                    var mesh = action.renderer != null ? action.renderer.sharedMesh : null;
                    var names = new[] { L("未設定") }.Concat(mesh == null ? Array.Empty<string>() : Enumerable.Range(0, mesh.blendShapeCount).Select(mesh.GetBlendShapeName)).ToArray();
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUIUtility.labelWidth = 62;
                        int index = Math.Max(0, Array.IndexOf(names, action.shape));
                        int next = EditorGUILayout.Popup(L("シェイプ"), index, names);
                        EditorGUIUtility.labelWidth = 45;
                        float weight = EditorGUILayout.FloatField(L("値"), action.weight, GUILayout.Width(110));
                        if (next != index || weight != action.weight) Change(() => { action.shape = next == 0 ? "" : names[next]; action.weight = Mathf.Clamp(weight, 0, 100); });
                        EditorGUIUtility.labelWidth = 155;
                    }
                }
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
                        int mode = EditorGUILayout.Popup(action.objectOn ? 0 : 1, ObjectStateLabels, GUILayout.Width(60));
                        if (target != action.target || (mode == 0) != action.objectOn) Change(() => { action.target = target; action.objectOn = mode == 0; });
                    }
                }
                GUILayout.Space(4);
            }
        }
        private void Apply(bool regenerate)
        {
            bool ok = FullSetupGenerator.Apply(descriptor, settings, regenerate, out _, out var result); SetMessage(result, ok);
        }
        private void SetMessage(string text, bool success) { message = text; messageType = success ? MessageType.Warning : MessageType.Error; Repaint(); }
        private void DrawCompatibility()
        {
            var caps = VrcFuryCapabilities.Current;
            var notes = caps.Notices();
            if (notes.Count == 0) return;
            EditorGUILayout.HelpBox("VRCFury " + caps.Version + "\n" + string.Join("\n", notes) + "\n" + VrcFuryCapabilities.UpdateGuide, MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(L("VRCFury 導入・更新の案内"))) Application.OpenURL(VrcFuryCapabilities.GuideUrl);
                if (GUILayout.Button(L("互換性を再確認"))) { VrcFuryCapabilities.Refresh(); Repaint(); }
            }
        }

        private void CompatibleToggle(string label, bool value, Action<bool> set, bool available, string tooltip = null)
        {
            using (new EditorGUI.DisabledScope(!available))
                Toggle(label, value && available, v => { if (available) set(v); }, available ? tooltip : L("この版では利用できません。") + VrcFuryCapabilities.UpdateGuide);
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
        private void Change(Action change) { Undo.RecordObject(this, L("SPS2 設定")); change(); EditorUtility.SetDirty(this); Repaint(); }
    }
}
