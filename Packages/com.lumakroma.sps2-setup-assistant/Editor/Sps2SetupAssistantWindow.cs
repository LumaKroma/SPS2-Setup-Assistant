using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    public sealed class Sps2SetupAssistantWindow : EditorWindow
    {
        [Serializable]
        private sealed class Part
        {
            public string id, name;
            public int category;
            public bool included, custom, depth;
            public HumanBodyBones bone = HumanBodyBones.Hips;
            public Vector3 offset, rotation;
            public SkinnedMeshRenderer renderer;
            public string shape = "";
            public float start = -2f, maximum, weight = 80f;
            public AnimationClip clip;
            public GameObject target;
            public bool objectOn = true;
            public void ResetPose() { offset = Vector3.zero; rotation = Vector3.zero; }
            public void ResetSettings()
            {
                depth = id == "mouth"; renderer = null; shape = "";
                start = -2; maximum = 0; weight = 80; clip = null; target = null; objectOn = true;
            }
        }

        [SerializeField] private List<Part> parts = new List<Part>();
        [SerializeField] private VRCAvatarDescriptor descriptor;
        [SerializeField] private int selected, tab, profile = 1;
        [SerializeField] private bool pathEnabled = true, showAuto = true, showLegacy = true, showInstant = true;
        [SerializeField] private bool omitLegacy, autoInitial, legacyInitial = true;
        [SerializeField] private Vector3 pathOffset;
        private Vector2 listScroll, detailScroll;
        private GUIStyle titleStyle, heading, paragraph;
        private static readonly string[] Categories = { "顔", "上半身", "手", "下半身", "足", "カスタム" };
        private static readonly Color Accent = new Color(.34f, .78f, .70f);

        [MenuItem("Tools/LumaKroma/SPS2 Setup Assistant")]
        public static void Open()
        {
            var window = GetWindow<Sps2SetupAssistantWindow>();
            window.titleContent = new GUIContent("SPS2 Setup");
            window.minSize = new Vector2(860, 700);
            window.Show();
        }

        private void OnEnable()
        {
            if (parts.Count == 0)
            {
                Add("mouth", "口", 0); Add("earLeft", "左耳", 0); Add("earRight", "右耳", 0);
                Add("nippleLeft", "左乳首", 1); Add("nippleRight", "右乳首", 1); Add("chest", "胸の間", 1);
                Add("handLeft", "左手", 2); Add("handRight", "右手", 2); Add("hands", "両手の間", 2);
                Add("vagina", "膣", 3); Add("anus", "肛門", 3); Add("thighs", "太ももの間", 3);
                Add("footLeft", "左足", 4); Add("footRight", "右足", 4); Add("feet", "両足の間", 4);
                ApplyProfile(1);
            }
            Undo.undoRedoPerformed += Repaint;
        }
        private void OnDisable() { Undo.undoRedoPerformed -= Repaint; }
        private void Add(string id, string name, int category)
        {
            var p = new Part { id = id, name = name, category = category };
            p.ResetSettings(); parts.Add(p);
        }
        private void ApplyProfile(int value)
        {
            profile = value;
            foreach (var p in parts)
            {
                if (p.custom) continue;
                bool casual = p.id == "mouth" || p.id == "chest" || p.category == 2 || p.id == "vagina" || p.id == "anus";
                bool extra = p.id == "earLeft" || p.id == "earRight" || p.id == "thighs";
                p.included = value == 2 || (value == 1 && !extra) || casual;
            }
        }
        private bool Includes(string id) { return parts.Exists(p => p.id == id && p.included); }

        private void OnGUI()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 23 };
                heading = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
                paragraph = new GUIStyle(EditorStyles.wordWrappedLabel);
            }
            EditorGUIUtility.labelWidth = 160;
            GUILayout.Space(16);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(18);
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label("SPS2 Setup Assistant", titleStyle);
                    GUILayout.Label("使う部位を選んで、アバターに合わせて調整", paragraph);
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label("画面確認版", EditorStyles.miniButton, GUILayout.Width(85), GUILayout.Height(24));
                GUILayout.Space(18);
            }
            GUILayout.Space(12);
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(1, 3, GUILayout.ExpandWidth(true)), Accent);
            using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorDefaultMargins))
            {
                GUILayout.Space(10);
                var next = (VRCAvatarDescriptor)EditorGUILayout.ObjectField("対象アバター", descriptor, typeof(VRCAvatarDescriptor), true);
                if (next != descriptor) Change("対象アバター", () =>
                {
                    descriptor = next;
                    foreach (var p in parts) { p.renderer = null; p.shape = ""; }
                    DetectMouth();
                });
                GUILayout.Space(8);
                using (new EditorGUILayout.HorizontalScope())
                {
                    var labels = new[] { "カジュアル  /  7部位", "デフォルト  /  12部位", "フル  /  15部位" };
                    for (int i = 0; i < 3; i++)
                    {
                        int value = i;
                        var color = GUI.backgroundColor;
                        if (profile == i) GUI.backgroundColor = Accent;
                        if (GUILayout.Button(labels[i], GUILayout.Height(38))) Change("プリセット", () => ApplyProfile(value));
                        GUI.backgroundColor = color;
                    }
                }
                GUILayout.Label("部位は個別に変更できます。プリセットを切り替えても位置・詳細設定は保持します。", EditorStyles.miniLabel);
                GUILayout.Space(10);
                tab = GUILayout.Toolbar(tab, new[] { "ソケット", "経路", "ゲーム内メニュー" }, GUILayout.Height(30));
                GUILayout.Space(8);
                if (tab == 0) DrawSockets();
                else
                {
                    detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                    if (tab == 1) DrawPath(); else DrawMenu();
                    EditorGUILayout.EndScrollView();
                }
                GUILayout.Space(8);
                DrawFooter();
            }
        }

        private void DrawSockets()
        {
            selected = Mathf.Clamp(selected, 0, parts.Count - 1);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(236)))
                {
                    GUILayout.Label("使用する部位", heading);
                    GUILayout.Label($"選択中 {parts.Count(p => p.included)} 部位", EditorStyles.miniLabel);
                    listScroll = EditorGUILayout.BeginScrollView(listScroll);
                    for (int category = 0; category < Categories.Length; category++)
                    {
                        GUILayout.Space(6);
                        GUILayout.Label(Categories[category], EditorStyles.boldLabel);
                        for (int i = 0; i < parts.Count; i++)
                        {
                            var p = parts[i];
                            if (p.category != category) continue;
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                bool included = EditorGUILayout.Toggle(p.included, GUILayout.Width(18));
                                if (included != p.included) Change("使用部位", () => { p.included = included; profile = -1; });
                                var color = GUI.backgroundColor;
                                if (selected == i) GUI.backgroundColor = Accent;
                                if (GUILayout.Button(p.name, EditorStyles.miniButton, GUILayout.Height(23))) selected = i;
                                GUI.backgroundColor = color;
                            }
                        }
                    }
                    if (GUILayout.Button("＋ カスタム部位を追加", GUILayout.Height(26))) Change("カスタム追加", () =>
                    {
                        parts.Add(new Part { id = Guid.NewGuid().ToString("N"), name = "カスタム " + (parts.Count(p => p.custom) + 1), category = 5, custom = true, included = true });
                        selected = parts.Count - 1;
                    });
                    EditorGUILayout.EndScrollView();
                }
                GUILayout.Space(10);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                    DrawPart(parts[selected]);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawPart(Part p)
        {
            GUILayout.Label(p.name, heading);
            GUILayout.Label(p.included ? "この部位をセットアップに含める" : "この部位はセットアップに含めない", EditorStyles.miniLabel);
            GUILayout.Space(10);
            if (p.custom)
            {
                string name = EditorGUILayout.TextField("表示名", p.name);
                if (name != p.name) Change("表示名", () => p.name = name);
                var bone = (HumanBodyBones)EditorGUILayout.EnumPopup("追従ボーン", p.bone);
                if (bone != p.bone && bone != HumanBodyBones.LastBone) Change("追従ボーン", () => p.bone = bone);
            }
            else GUILayout.Label(p.id == "chest" || p.id == "hands" || p.id == "thighs" || p.id == "feet"
                ? "追従：左右のボーンから位置・回転を決定" : "追従：アバターの対応ボーン", paragraph);
            GUILayout.Space(12);
            GUILayout.Label("位置と向き", EditorStyles.boldLabel);
            Vector3 offset = EditorGUILayout.Vector3Field("位置の調整（cm）", p.offset);
            if (offset != p.offset) Change("位置", () => p.offset = offset);
            Vector3 rotation = EditorGUILayout.Vector3Field("回転の調整（°）", p.rotation);
            if (rotation != p.rotation) Change("回転", () => p.rotation = rotation);
            GUILayout.Space(10);
            GUILayout.Label("深さに応じたアクション", EditorStyles.boldLabel);
            Toggle("深さアクションを使う", p.depth, v => p.depth = v);
            using (new EditorGUI.DisabledScope(!p.depth))
            {
                Float("開始位置（cm）", p.start, v => p.start = v);
                Float("最大に達する位置（cm）", p.maximum, v => p.maximum = v);
                if (p.maximum <= p.start) EditorGUILayout.HelpBox("最大に達する位置は開始位置より奥に設定してください。", MessageType.Warning);
                GUILayout.Space(6);
                GUILayout.Label("BlendShape", EditorStyles.boldLabel);
                var renderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("対象メッシュ", p.renderer, typeof(SkinnedMeshRenderer), true);
                if (renderer != p.renderer) Change("対象メッシュ", () => { p.renderer = renderer; p.shape = ""; });
                var mesh = p.renderer != null ? p.renderer.sharedMesh : null;
                var names = mesh == null ? new[] { "なし" } : new[] { "なし" }.Concat(Enumerable.Range(0, mesh.blendShapeCount).Select(mesh.GetBlendShapeName)).ToArray();
                int index = string.IsNullOrEmpty(p.shape) ? 0 : Math.Max(0, Array.IndexOf(names, p.shape));
                int next = EditorGUILayout.Popup("BlendShape", index, names);
                if (next != index) Change("BlendShape", () => p.shape = next == 0 ? "" : names[next]);
                float weight = EditorGUILayout.Slider("最大値", p.weight, 0, 100);
                if (weight != p.weight) Change("最大値", () => p.weight = weight);
                if (p.id == "mouth")
                    using (new EditorGUI.DisabledScope(descriptor == null))
                        if (GUILayout.Button("Viseme oh から対象を検出", GUILayout.Height(24))) Change("Viseme検出", DetectMouth);
                GUILayout.Label($"{p.start:0.##} cmで0 → {p.maximum:0.##} cmで{p.weight:0.#} → 奥では最大値を保持", EditorStyles.miniLabel);
                GUILayout.Space(8);
                GUILayout.Label("追加アクション", EditorStyles.boldLabel);
                var clip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", p.clip, typeof(AnimationClip), false);
                if (clip != p.clip) Change("Animation Clip", () => p.clip = clip);
                var target = (GameObject)EditorGUILayout.ObjectField("ON / OFF の対象", p.target, typeof(GameObject), true);
                if (target != p.target) Change("ON / OFF", () => p.target = target);
                Toggle("アクション時にON", p.objectOn, v => p.objectOn = v);
            }
            GUILayout.Space(14);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("この部位の位置をリセット", GUILayout.Height(26))) Change("位置リセット", p.ResetPose);
                if (GUILayout.Button("この部位の設定をリセット", GUILayout.Height(26))) Change("設定リセット", () => { p.ResetSettings(); if (p.id == "mouth") DetectMouth(); });
            }
            if (p.custom && GUILayout.Button("このカスタム部位を削除")) Change("カスタム削除", () => { parts.Remove(p); selected = 0; });
        }

        private void DetectMouth()
        {
            var mouth = parts.Find(p => p.id == "mouth");
            mouth.renderer = null; mouth.shape = "";
            if (descriptor == null || descriptor.VisemeSkinnedMesh == null || descriptor.VisemeBlendShapes == null) return;
            int oh = (int)VRC.SDKBase.VRC_AvatarDescriptor.Viseme.oh;
            if (oh >= descriptor.VisemeBlendShapes.Length) return;
            string name = descriptor.VisemeBlendShapes[oh];
            var mesh = descriptor.VisemeSkinnedMesh.sharedMesh;
            if (string.IsNullOrEmpty(name) || mesh == null || mesh.GetBlendShapeIndex(name) < 0) return;
            mouth.renderer = descriptor.VisemeSkinnedMesh; mouth.shape = name;
        }

        private void DrawPath()
        {
            GUILayout.Label("口と肛門をつなぐ経路", heading);
            GUILayout.Space(12);
            Toggle("経路を自動配置する", pathEnabled, v => pathEnabled = v);
            if (!pathEnabled || !Includes("mouth") || !Includes("anus")) return;
            GUILayout.Space(18);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("口", heading); GUILayout.FlexibleSpace();
                GUILayout.Label("──────── 経路 ────────", heading); GUILayout.FlexibleSpace(); GUILayout.Label("肛門", heading);
            }
            GUILayout.Space(18);
            Vector3 offset = EditorGUILayout.Vector3Field("経路の位置調整（cm）", pathOffset);
            if (offset != pathOffset) Change("経路の位置", () => pathOffset = offset);
            if (GUILayout.Button("経路の位置をリセット", GUILayout.Width(220), GUILayout.Height(28))) Change("経路リセット", () => pathOffset = Vector3.zero);
            GUILayout.Space(12);
            GUILayout.Label("配置した後で、アバターの体型に合わせて経路を調整できます。", paragraph);
            EditorGUILayout.HelpBox("画面確認用の設定です。経路の生成と双方向の動作は未検証です。", MessageType.Info);
        }

        private void DrawMenu()
        {
            GUILayout.Label("ゲーム内で使う操作", heading);
            GUILayout.Space(12);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label("共通メニュー", EditorStyles.boldLabel);
                    Toggle("Auto Modeを表示", showAuto, v => showAuto = v);
                    Toggle("後方互換性を完全に除外", omitLegacy, v => omitLegacy = v);
                    using (new EditorGUI.DisabledScope(omitLegacy)) Toggle("後方互換性を表示", showLegacy, v => showLegacy = v);
                    Toggle("インスタント起動を表示", showInstant, v => showInstant = v);
                    GUILayout.Space(16);
                    GUILayout.Label("初期状態と保存", EditorStyles.boldLabel);
                    Toggle("Auto Mode：初期ON", autoInitial, v => autoInitial = v);
                    using (new EditorGUI.DisabledScope(omitLegacy)) Toggle("後方互換性：初期ON", legacyInitial, v => legacyInitial = v);
                    GUILayout.Label("Auto Mode・後方互換性の状態を保存\n個別ソケットは初期OFF・状態を保存しない", paragraph);
                    GUILayout.Space(16);
                    EditorGUILayout.HelpBox("インスタント起動は口・膣をONにします。後方互換性がONの間は実行できません。", MessageType.Info);
                    GUILayout.Label("他の部位とAuto Modeの状態は変更しません。", paragraph);
                }
                GUILayout.Space(12);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(280)))
                {
                    GUILayout.Label("メニュー構成プレビュー", heading);
                    GUILayout.Space(12);
                    if (showAuto) MenuRow("Auto Mode", "保存");
                    if (showLegacy && !omitLegacy) MenuRow("後方互換性", "保存");
                    if (showInstant && (Includes("mouth") || Includes("vagina"))) MenuRow("インスタント起動", "一度だけ実行");
                    for (int i = 0; i < Categories.Length; i++)
                    {
                        int category = i;
                        var members = parts.Where(p => p.included && p.category == category).ToArray();
                        if (members.Length == 0) continue;
                        MenuRow(Categories[i] + "  ›", members.Length + " 部位");
                        GUILayout.Label(string.Join("・", members.Select(p => p.name)), paragraph);
                        GUILayout.Space(6);
                    }
                    GUILayout.Label("項目が多い場合はページ分割します。", EditorStyles.miniLabel);
                }
            }
        }
        private static void MenuRow(string name, string note)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(name, EditorStyles.boldLabel); GUILayout.FlexibleSpace(); GUILayout.Label(note, EditorStyles.miniLabel);
            }
            GUILayout.Space(5);
        }
        private void DrawFooter()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("全ての位置をリセット")) Change("全位置リセット", () => { foreach (var p in parts) p.ResetPose(); pathOffset = Vector3.zero; });
                if (GUILayout.Button("全ての部位設定をリセット")) Change("全設定リセット", () => { foreach (var p in parts) p.ResetSettings(); DetectMouth(); });
                GUILayout.FlexibleSpace(); GUILayout.Label("設定変更はUndoで戻せます", EditorStyles.miniLabel);
            }
            EditorGUILayout.HelpBox("画面確認版：設定はこのウィンドウ内だけで編集します。アバターへの適用は未接続です。", MessageType.None);
            using (new EditorGUI.DisabledScope(true))
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Button(new GUIContent("IcePop テスト用Plugを配置", "未実装：アバター前方に配置し、Gesture Managerで手動調整"), GUILayout.Height(34));
                GUILayout.Button(new GUIContent("選択した設定をアバターに適用", "未接続：この画面からアバターは変更されません"), GUILayout.Height(34));
            }
            GUILayout.Space(8);
        }
        private void Toggle(string label, bool value, Action<bool> set)
        {
            bool next = EditorGUILayout.ToggleLeft(label, value);
            if (next != value) Change(label, () => set(next));
        }
        private void Float(string label, float value, Action<float> set)
        {
            float next = EditorGUILayout.FloatField(label, value);
            if (next != value && !float.IsNaN(next) && !float.IsInfinity(next)) Change(label, () => set(next));
        }
        private void Change(string label, Action action)
        {
            Undo.RecordObject(this, "SPS2 " + label); action(); EditorUtility.SetDirty(this); Repaint();
        }
    }
}
