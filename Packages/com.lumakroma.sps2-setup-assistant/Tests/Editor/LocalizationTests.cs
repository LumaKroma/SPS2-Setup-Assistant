using System;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using LumaKroma.Sps2SetupAssistant.Editor.Localization;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace LumaKroma.Sps2SetupAssistant.Editor.Tests
{
    public class LocalizationTests
    {
        [TestCase(SystemLanguage.Japanese, DisplayLanguage.Japanese)]
        [TestCase(SystemLanguage.English, DisplayLanguage.English)]
        [TestCase(SystemLanguage.Korean, DisplayLanguage.Korean)]
        [TestCase(SystemLanguage.Chinese, DisplayLanguage.ChineseSimplified)]
        [TestCase(SystemLanguage.ChineseSimplified, DisplayLanguage.ChineseSimplified)]
        [TestCase(SystemLanguage.ChineseTraditional, DisplayLanguage.ChineseTraditional)]
        [TestCase(SystemLanguage.German, DisplayLanguage.English)]
        [TestCase(SystemLanguage.Unknown, DisplayLanguage.English)]
        public void SystemLanguageMappingHasSupportedAndFallbackDefaults(SystemLanguage system, DisplayLanguage expected)
            => Assert.That(Sps2Localization.FromSystemLanguage(system), Is.EqualTo(expected));

        [TestCase(DisplayLanguage.Japanese, "指わっか", "手動")]
        [TestCase(DisplayLanguage.English, "Finger rings", "manually")]
        [TestCase(DisplayLanguage.Korean, "손가락 링", "수동")]
        [TestCase(DisplayLanguage.ChineseSimplified, "指环", "手动")]
        [TestCase(DisplayLanguage.ChineseTraditional, "指環", "手動")]
        public void ScaleWarningNamesFingerRingsInEveryLanguage(DisplayLanguage language, string subject, string adjustment)
        {
            const string message = "指わっかの階層に非一様または負のscaleがあります。生成は続行しますが、この指わっかの位置・向き・サイズは保証されません。生成後に確認し、手動で調整してください。";
            Assert.That(Sps2Localization.L(message, language), Does.StartWith(subject));
            Assert.That(Sps2Localization.L(message, language), Does.Contain(adjustment));
            if (language != DisplayLanguage.Japanese) Assert.That(Sps2Localization.L(message, language), Is.Not.EqualTo(message));
        }

        [Test]
        public void FirstUiPreferenceAndNewSetupUseSystemButSavedChoicesRemain()
        {
            const string key = "LumaKroma.Sps2SetupAssistant.UiLanguage";
            bool existed = UnityEditor.EditorPrefs.HasKey(key);
            int previous = UnityEditor.EditorPrefs.GetInt(key);
            try
            {
                UnityEditor.EditorPrefs.DeleteKey(key);
                Assert.That(Sps2Localization.UiLanguage, Is.EqualTo(Sps2Localization.DefaultLanguage));
                Assert.That(UnityEditor.EditorPrefs.HasKey(key), Is.False);
                Sps2Localization.UiLanguage = DisplayLanguage.ChineseTraditional;
                Assert.That(Sps2Localization.UiLanguage, Is.EqualTo(DisplayLanguage.ChineseTraditional));
                Assert.That(FullSetupCatalog.CreateDefault().menuLanguage, Is.EqualTo(Sps2Localization.DefaultLanguage));
                var saved = FullSetupCatalog.CreateDefault(); saved.menuLanguage = DisplayLanguage.Korean;
                var loaded = JsonUtility.FromJson<SetupSettings>(JsonUtility.ToJson(saved));
                FullSetupCatalog.UpgradeDisplayNames(loaded);
                Assert.That(loaded.menuLanguage, Is.EqualTo(DisplayLanguage.Korean));
                var old = JsonUtility.FromJson<SetupSettings>("{}");
                Assert.That(old.menuLanguage, Is.EqualTo(DisplayLanguage.Japanese));
            }
            finally { if(existed) UnityEditor.EditorPrefs.SetInt(key,previous); else UnityEditor.EditorPrefs.DeleteKey(key); }
        }

        [Test]
        public void RenameWindowUndoReloadAndOffRestoreTranslatedStandard()
        {
            var window = ScriptableObject.CreateInstance<Sps2SetupAssistantWindow>();
            UnityEditor.Undo.IncrementCurrentGroup(); int group = UnityEditor.Undo.GetCurrentGroup();
            try
            {
                window.DraftSettings.menuLanguage = DisplayLanguage.English;
                var mouth = window.DraftSettings.parts[0]; mouth.menuNameOverride = "Stored custom";
                Assert.That(window.IsNameEditing(mouth), Is.True);
                window.SetNameEditing(mouth,false); UnityEditor.Undo.FlushUndoRecordObjects();
                Assert.That(SocketDisplayNames.Resolve(mouth,DisplayLanguage.English), Is.EqualTo("Mouth"));
                UnityEditor.Undo.PerformUndo();
                mouth=window.DraftSettings.parts[0];
                Assert.That(mouth.menuNameOverride, Is.EqualTo("Stored custom"));
                Assert.That(window.IsNameEditing(mouth), Is.True);
                UnityEditor.Undo.PerformRedo(); mouth=window.DraftSettings.parts[0];
                Assert.That(window.IsNameEditing(mouth), Is.False);
                window.SetNameEditing(mouth,true);
                Assert.That(mouth.menuNameOverride, Is.EqualTo("Mouth"));
                mouth.menuNameOverride=""; // Empty field stays editable while typing.
                Assert.That(window.IsNameEditing(mouth), Is.True);
                string draft=JsonUtility.ToJson(window);
                Assert.That(draft, Does.Contain("nameEditors"));
                JsonUtility.FromJsonOverwrite(draft,window);
                mouth=window.DraftSettings.parts[0]; Assert.That(window.IsNameEditing(mouth),Is.True);
                mouth.menuNameOverride="User name";
                window.DraftSettings.menuLanguage=DisplayLanguage.Korean;
                Assert.That(SocketDisplayNames.Resolve(mouth,DisplayLanguage.Korean),Is.EqualTo("User name"));
                window.SetNameEditing(mouth,false);
                Assert.That(SocketDisplayNames.Resolve(mouth,DisplayLanguage.Korean),Is.EqualTo(Sps2Localization.L("口",DisplayLanguage.Korean)));
                Assert.That(JsonUtility.FromJson<SetupSettings>(JsonUtility.ToJson(window.DraftSettings.Copy())).parts[0].menuNameOverride,Is.Empty);
            }
            finally { UnityEditor.Undo.RevertAllDownToGroup(group); UnityEngine.Object.DestroyImmediate(window); }
        }

        [TestCase(DisplayLanguage.Japanese, "左手の指わっか", "右手の指わっか", "親指の重み")]
        [TestCase(DisplayLanguage.English, "Left finger loop", "Right finger loop", "Thumb weight")]
        [TestCase(DisplayLanguage.Korean, "왼손 손가락 고리", "오른손 손가락 고리", "엄지 가중치")]
        [TestCase(DisplayLanguage.ChineseSimplified, "左手指环", "右手指环", "拇指权重")]
        [TestCase(DisplayLanguage.ChineseTraditional, "左手指環", "右手指環", "拇指權重")]
        public void RingNamesUseSelectedLanguageAndPreserveCalibrationAndOverrides(DisplayLanguage language, string left, string right, string weight)
        {
            var setup = FullSetupCatalog.CreateDefault();
            var ring = setup.parts.Single(p => p.id == "fingerRingLeft");
            ring.fingerRing.center = new Vector3(.01f, .02f, .03f);
            ring.fingerRing.thumbWeight = .3f;
            string calibration = JsonUtility.ToJson(ring.fingerRing);
            // Preserve an earlier prototype's stored English name; standard labels resolve by stable ID.
            ring.name = "Left finger ring";
            Assert.That(SocketDisplayNames.Resolve(ring, language), Is.EqualTo(left));
            Assert.That(SocketDisplayNames.Resolve(setup.parts.Single(p => p.id == "fingerRingRight"), language), Is.EqualTo(right));
            Assert.That(Sps2Localization.L("親指の重み", language), Is.EqualTo(weight));
            ring.menuNameOverride = "My Ring";
            setup.menuLanguage = language;
            var restored = JsonUtility.FromJson<SetupSettings>(JsonUtility.ToJson(setup.Copy()));
            var saved = restored.parts.Single(p => p.id == ring.id);
            Assert.That(SocketDisplayNames.Resolve(saved, language), Is.EqualTo("My Ring"));
            Assert.That(JsonUtility.ToJson(saved.fingerRing), Is.EqualTo(calibration));
            Assert.That(saved.included, Is.False);
        }

        [Test]
        public void OldJsonKeepsJapaneseAndMigratesAuthoredNamesOnlyOnce()
        {
            var setup = JsonUtility.FromJson<SetupSettings>("{\"parts\":[{\"id\":\"mouth\",\"name\":\"口\"},{\"id\":\"chest\",\"name\":\"胸の間\"},{\"id\":\"handLeft\",\"name\":\"My Hand\"},{\"id\":\"custom\",\"name\":\"내 이름\",\"custom\":true}]}");
            FullSetupCatalog.UpgradeDisplayNames(setup);
            Assert.That(setup.menuLanguage, Is.EqualTo(DisplayLanguage.Japanese));
            Assert.That(SocketDisplayNames.Resolve(setup.parts[0], setup.menuLanguage), Is.EqualTo("口"));
            Assert.That(SocketDisplayNames.Resolve(setup.parts[1], setup.menuLanguage), Is.EqualTo("胸"));
            Assert.That(SocketDisplayNames.Resolve(setup.parts[2], DisplayLanguage.Korean), Is.EqualTo("My Hand"));
            Assert.That(SocketDisplayNames.Resolve(setup.parts[3], DisplayLanguage.English), Is.EqualTo("내 이름"));
            setup.parts[2].menuNameOverride = "";
            FullSetupCatalog.UpgradeDisplayNames(setup);
            Assert.That(SocketDisplayNames.Resolve(setup.parts[2], DisplayLanguage.English), Is.EqualTo("Left hand"));
        }

        [Test]
        public void CopySerializationPresetsAndLanguageSwitchPreserveOverrides()
        {
            var setup = FullSetupCatalog.CreateDefault();
            setup.menuLanguage = DisplayLanguage.ChineseTraditional;
            var mouth = setup.parts.Single(p => p.id == "mouth");
            mouth.menuNameOverride = "  My 口 입 嘴  ";
            setup.parts.Add(new SocketSettings { id = "custom", custom = true, name = "私の部位", menuNameOverride = "Custom 名" });
            var restored = JsonUtility.FromJson<SetupSettings>(JsonUtility.ToJson(setup.Copy()));
            Assert.That(restored.menuLanguage, Is.EqualTo(DisplayLanguage.ChineseTraditional));
            Assert.That(restored.displayNamesVersion, Is.EqualTo(1));
            foreach (DisplayLanguage language in Enum.GetValues(typeof(DisplayLanguage)))
            {
                restored.menuLanguage = language;
                for (int preset = 0; preset < 3; preset++) FullSetupCatalog.ApplyPreset(restored, preset);
                Assert.That(SocketDisplayNames.Resolve(restored.parts[0], language), Is.EqualTo(mouth.menuNameOverride));
                Assert.That(SocketDisplayNames.Resolve(restored.parts.Last(), language), Is.EqualTo("Custom 名"));
            }
            restored.parts[0].menuNameOverride = "\t  ";
            Assert.That(SocketDisplayNames.Resolve(restored.parts[0], DisplayLanguage.English), Is.EqualTo("Mouth"));
            Assert.That(mouth.menuNameOverride, Is.EqualTo("  My 口 입 嘴  "));
        }

        [Test]
        public void ExplicitMenuLanguageDoesNotDependOnUiPreference()
        {
            const string key = "LumaKroma.Sps2SetupAssistant.UiLanguage";
            bool existed = UnityEditor.EditorPrefs.HasKey(key);
            int previous = UnityEditor.EditorPrefs.GetInt(key);
            try
            {
                Sps2Localization.UiLanguage = DisplayLanguage.Korean;
                Assert.That(Sps2Localization.L("設定"), Is.EqualTo("설정"));
                var part = FullSetupCatalog.CreateDefault().parts[0];
                Assert.That(SocketDisplayNames.Resolve(part, DisplayLanguage.English), Is.EqualTo("Mouth"));
                Assert.That(SocketDisplayNames.Resolve(part, (DisplayLanguage)999), Is.EqualTo("口"));
                Sps2Localization.UiLanguage = DisplayLanguage.ChineseSimplified;
                Assert.That(Sps2Localization.L("設定"), Is.EqualTo("设置"));
                Assert.That(SocketDisplayNames.Resolve(part, DisplayLanguage.English), Is.EqualTo("Mouth"));
            }
            finally
            {
                if (existed) UnityEditor.EditorPrefs.SetInt(key, previous); else UnityEditor.EditorPrefs.DeleteKey(key);
                VrcFuryCapabilities.Refresh();
            }
        }

        [TestCase(DisplayLanguage.English, "Next")]
        [TestCase(DisplayLanguage.Korean, "다음")]
        [TestCase(DisplayLanguage.ChineseSimplified, "下一页")]
        [TestCase(DisplayLanguage.ChineseTraditional, "下一頁")]
        public void PaginationPreservesControlIdentityOrderAndParameters(DisplayLanguage language, string nextLabel)
        {
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            var controls = Enumerable.Range(0, 18).Select(i => new VRCExpressionsMenu.Control
            {
                name = "User " + i, type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "unchanged/" + i }, value = i
            }).ToArray();
            menu.controls = controls.ToList();
            try
            {
                Sps2BuildIntegration.Paginate(menu, language);
                var page = menu;
                int index = 0;
                while (page != null)
                {
                    Assert.That(page.controls.Count, Is.LessThanOrEqualTo(8));
                    VRCExpressionsMenu next = null;
                    foreach (var control in page.controls)
                    {
                        if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                        { Assert.That(control.name, Is.EqualTo(nextLabel)); next = control.subMenu; continue; }
                        Assert.That(control, Is.SameAs(controls[index]));
                        Assert.That(control.parameter.name, Is.EqualTo("unchanged/" + index));
                        Assert.That(control.value, Is.EqualTo(index++));
                    }
                    page = next;
                }
                Assert.That(index, Is.EqualTo(18));
            }
            finally
            {
                var page = menu;
                while (page != null)
                {
                    var next = page.controls.LastOrDefault()?.subMenu;
                    UnityEngine.Object.DestroyImmediate(page); page = next;
                }
            }
        }
    }
}
