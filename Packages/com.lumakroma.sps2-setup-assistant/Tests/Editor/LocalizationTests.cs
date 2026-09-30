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
        [TestCase(DisplayLanguage.Japanese, "左手の指輪", "右手の指輪", "親指の重み")]
        [TestCase(DisplayLanguage.English, "Left finger ring", "Right finger ring", "Thumb weight")]
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
