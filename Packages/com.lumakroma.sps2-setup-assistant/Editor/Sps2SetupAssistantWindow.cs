using System;
using System.Linq;
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
        private GUIStyle title;
        private GUIStyle helpStyle;

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
            if (descriptor == null && GlobalObjectId.TryParse(avatarId, out var id))
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
            if (title == null) title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 20 };
            EditorGUIUtility.labelWidth = 100;
            using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorDefaultMargins))
            {
                GUILayout.Space(12);
                GUILayout.Label("SPS2 Setup Assistant", title);
                GUILayout.Space(10);
                var next = (VRCAvatarDescriptor)EditorGUILayout.ObjectField("アバター", descriptor, typeof(VRCAvatarDescriptor), true);
                if (next != descriptor) Change(() => BindAvatar(next));
                scroll = EditorGUILayout.BeginScrollView(scroll);
                GUILayout.Space(10);
                GUILayout.Label("プリセット", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    var labels = new[] { "カジュアル", "デフォルト", "フル" };
                    for (int i = 0; i < 3; i++)
                    {
                        int value = i;
                        if (GUILayout.Button(labels[i], GUILayout.Height(28))) Change(() => FullSetupCatalog.ApplyPreset(settings, value));
                    }
                }
                for (int category = 0; category < FullSetupCatalog.Categories.Length; category++)
                {
                    GUILayout.Space(12);
                    GUILayout.Label(FullSetupCatalog.Categories[category], EditorStyles.boldLabel);
                    foreach (var part in settings.parts.Where(p => p.category == category).ToArray()) DrawPart(part);
                    if (category == 5 && GUILayout.Button("＋ カスタム部位を追加")) Change(() => settings.parts.Add(new SocketSettings
                    {
                        id = Guid.NewGuid().ToString("N"), name = "カスタム " + (settings.parts.Count(p => p.custom) + 1),
                        custom = true, category = 5, included = true
                    }));
                }
                GUILayout.Space(12);
                Toggle("貫通", settings.penetration, v => settings.penetration = v,
                    "口と肛門をつなぐ体内の経路を生成します。片方だけを使用する場合も、もう片方の位置を出口として自動配置します。\n使用しない側のソケットやメニューは追加しません。「体内で太さを0にする」をオンにすると、体内の太さを0にし、出口の外では元の太さに戻します。");
                if (settings.penetration)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(18);
                        Toggle("体内で太さを0にする", !settings.showInternalThickness, v => settings.showInternalThickness = !v);
                    }
                }
                Toggle("Auto Mode", settings.autoMode, v => settings.autoMode = v,
                    "プラグに最も近い対象ソケットを自動で有効にするメニューを追加します。\n使用時にメニューのAuto Modeをオンにしてください。");
                Toggle("後方互換性", settings.legacy, v => settings.legacy = v,
                    "SPS1・DPS・TPSのプラグにも対応させ、互換機能の切り替えメニューを追加します。\nSPS2同士だけで使う場合はオフにできます。");
                Toggle("インスタント起動", settings.instant, v => settings.instant = v,
                    "生成済みの口と膣のソケットをまとめてオンにするボタンをメニューに追加します。\n後方互換性がオフのときに動作します。押した後もソケットはオンのままで、各メニューからオフにできます。");
                Toggle("Local Only", settings.localOnly, v => settings.localOnly = v);
                GUILayout.Space(8);
                Toggle("Modular Avatarで追従", settings.modularAvatar, v => settings.modularAvatar = v);
                GUILayout.Space(12);
                EditorGUILayout.EndScrollView();
                if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, messageType);
                bool generated = FullSetupGenerator.HasGeneratedRoot(descriptor);
                if (descriptor == null)
                {
                    EditorGUILayout.HelpBox("対象アバターを選択してください。以前の対象が開かれている場合は再検出できます。", MessageType.Info);
                    if (GUILayout.Button("対象アバターを再検出")) Change(() => { avatarId = null; RecoverAvatar(); });
                }
                else if (EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorGUILayout.HelpBox("再生を停止すると生成・再生成できます。", MessageType.Info);
                using (new EditorGUI.DisabledScope(descriptor == null || EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (!generated)
                    {
                        if (GUILayout.Button("プレハブを生成", GUILayout.Height(34))) Apply(true);
                    }
                    else
                    {
                        if (GUILayout.Button("プレハブを再生成", GUILayout.Height(30))) Apply(true);
                        if (GUILayout.Button("置き換えずに変更を反映", GUILayout.Height(30))) Apply(false);
                        if (GUILayout.Button("貫通テストプラグ出現", GUILayout.Height(30)))
                        {
                            bool ok = FullSetupGenerator.ShowLongTestPlug(descriptor, out var error); SetMessage(error, ok);
                        }
                        if (GUILayout.Button("テストプラグ出現", GUILayout.Height(30)))
                        {
                            bool ok = FullSetupGenerator.ShowTestPlug(descriptor, out var error); SetMessage(error, ok);
                        }
                    }
                }
                GUILayout.Space(8);
            }
        }
        private void DrawPart(SocketSettings part)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool included = EditorGUILayout.ToggleLeft(part.custom ? "使用" : part.name, part.included, GUILayout.Width(part.custom ? 48 : 130));
                    if (included != part.included) Change(() => part.included = included);
                    if (part.custom)
                    {
                        string name = EditorGUILayout.TextField(part.name);
                        if (name != part.name) Change(() => part.name = name);
                    }
                    if (part.included)
                    {
                    bool depth = EditorGUILayout.ToggleLeft("深度アクション", part.depth, GUILayout.MinWidth(114));
                    if (depth != part.depth) Change(() => { part.depth = depth; if (depth && part.actions.Count == 0) part.actions.Add(new DepthActionSettings()); });
                    }
                    if (part.custom && GUILayout.Button("−", GUILayout.Width(24))) Change(() => settings.parts.Remove(part));
                }
                if (part.custom)
                {
                    var target = (Transform)EditorGUILayout.ObjectField("追従先", part.target, typeof(Transform), true);
                    if (target != part.target) Change(() => part.target = target);
                }
                if (!part.included || !part.depth) return;
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
                EditorGUIUtility.labelWidth = 28;
                float start = EditorGUILayout.FloatField("近", part.range.x), end = EditorGUILayout.FloatField("遠", part.range.y);
                int units = EditorGUILayout.Popup((int)part.units, new[] { "メートル", "プラグ長", "ローカル" }, GUILayout.Width(90));
                if (start != part.range.x || end != part.range.y || units != (int)part.units)
                    if (!float.IsNaN(start) && !float.IsInfinity(start) && !float.IsNaN(end) && !float.IsInfinity(end))
                        Change(() => { start = Mathf.Clamp(start, -1, 3); end = Mathf.Clamp(end, -1, 3); part.range = new Vector2(Mathf.Min(start, end), Mathf.Max(start, end)); part.units = (DepthUnits)units; });
                EditorGUIUtility.labelWidth = 100;
            }
        }
        private void DrawAction(SocketSettings part, DepthActionSettings action)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
            using (new EditorGUILayout.HorizontalScope())
            {
                int kind = EditorGUILayout.Popup((int)action.kind, new[] { "BlendShape", "Animation Clip", "オブジェクト ON・OFF" });
                if (kind != (int)action.kind) Change(() => action.kind = (DepthActionKind)kind);
                if (GUILayout.Button("−", GUILayout.Width(24))) Change(() => part.actions.Remove(action));
            }
            if (action.kind == DepthActionKind.BlendShape)
            {
                var renderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("メッシュ", action.renderer, typeof(SkinnedMeshRenderer), true);
                if (renderer != action.renderer) Change(() => { action.renderer = renderer; action.shape = ""; });
                var mesh = action.renderer != null ? action.renderer.sharedMesh : null;
                var names = new[] { "未設定" }.Concat(mesh == null ? Array.Empty<string>() : Enumerable.Range(0, mesh.blendShapeCount).Select(mesh.GetBlendShapeName)).ToArray();
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUIUtility.labelWidth = 62;
                    int index = Math.Max(0, Array.IndexOf(names, action.shape));
                    int next = EditorGUILayout.Popup("シェイプ", index, names);
                    EditorGUIUtility.labelWidth = 20;
                    float weight = EditorGUILayout.FloatField("値", action.weight, GUILayout.Width(78));
                    if (next != index || weight != action.weight) Change(() => { action.shape = next == 0 ? "" : names[next]; action.weight = Mathf.Clamp(weight, 0, 100); });
                    EditorGUIUtility.labelWidth = 100;
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
                    int mode = EditorGUILayout.Popup(action.objectOn ? 0 : 1, new[] { "ON", "OFF" }, GUILayout.Width(60));
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
        private void Toggle(string label, bool value, Action<bool> set, string tooltip = null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var content = new GUIContent(label, tooltip);
                bool hasTooltip = !string.IsNullOrEmpty(tooltip);
                bool next = hasTooltip
                    ? EditorGUILayout.ToggleLeft(content, value, GUILayout.Width(EditorStyles.label.CalcSize(content).x + 16))
                    : EditorGUILayout.ToggleLeft(content, value);
                if (!string.IsNullOrEmpty(tooltip))
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
            if (helpStyle == null) helpStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold,
                padding = new RectOffset(), margin = new RectOffset()
            };
            if (Event.current.type == EventType.Repaint)
            {
                var points = new Vector3[33];
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
        private void Change(Action change) { Undo.RecordObject(this, "SPS2 設定"); change(); EditorUtility.SetDirty(this); Repaint(); }
    }
}
