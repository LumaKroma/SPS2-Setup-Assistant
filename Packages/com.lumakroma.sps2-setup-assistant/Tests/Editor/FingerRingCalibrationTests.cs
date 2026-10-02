using System;
using System.Collections;
using System.Linq;
using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.TestTools;

namespace LumaKroma.Sps2SetupAssistant.Editor.Tests
{
    public class FingerRingCalibrationTests
    {
        [TestCase(false)] [TestCase(true)]
        public void SavedIndexFramePreservesIntermediateAndAcceptsAutomaticProximal(bool proximalFrame)
        {
            var root = new GameObject("Saved index frames");
            try {
                var thumb = new GameObject("Thumb").transform; thumb.SetParent(root.transform, false);
                var proximal = new GameObject("Index base").transform; proximal.SetParent(root.transform, false);
                var intermediate = new GameObject("Index middle").transform; intermediate.SetParent(proximal, false);
                var chosen = proximalFrame ? proximal : intermediate;
                var value = FingerRingCalibrationUtility.Capture(root.transform, thumb, chosen, Vector3.one * .01f, Quaternion.identity, .3f);
                value = JsonUtility.FromJson<FingerRingCalibration>(JsonUtility.ToJson(value));
                var resolved = FingerRingCalibrationUtility.ResolveIndexFrame(root.transform, intermediate, proximal, value);
                Assert.That(resolved, Is.SameAs(chosen));
                FingerRingCalibrationUtility.Validate(root.transform, thumb, resolved, value);
                Assert.That(FingerRingCalibrationUtility.ResolveIndexFrame(root.transform, intermediate, proximal, new FingerRingCalibration()), Is.SameAs(intermediate));
                value.indexPath = "Unknown saved path";
                resolved = FingerRingCalibrationUtility.ResolveIndexFrame(root.transform, intermediate, proximal, value);
                Assert.Throws<InvalidOperationException>(() => FingerRingCalibrationUtility.Validate(root.transform, thumb, resolved, value));
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(.5f, 1f, 1f)] [TestCase(2f, 1f, 1f)]
        [TestCase(1f, .5f, 1f)] [TestCase(1f, 2f, 1f)]
        [TestCase(1f, 1f, .5f)] [TestCase(1f, 1f, 2f)]
        [TestCase(2f, .5f, 1.3f)]
        public void UniformHierarchyScalePreservesSavedCenterAndFollowing(float rootScale, float parentScale, float jointScale)
        {
            var parent = new GameObject("Scaled ancestor");
            try {
                parent.transform.localScale = Vector3.one * parentScale;
                parent.transform.SetPositionAndRotation(new Vector3(2, 3, -4), Quaternion.Euler(20, 30, 40));
                var root = new GameObject("Avatar").transform; root.SetParent(parent.transform, false);
                root.localScale = Vector3.one * rootScale;
                var thumb = new GameObject("Thumb").transform; thumb.SetParent(root, false);
                var index = new GameObject("Index").transform; index.SetParent(root, false);
                thumb.localPosition = new Vector3(.1f, .02f, .04f); thumb.localRotation = Quaternion.Euler(170, -85, 120);
                index.localPosition = new Vector3(-.03f, .07f, -.06f); index.localRotation = Quaternion.Euler(-95, 35, 60);
                thumb.localScale = Vector3.one * jointScale;
                index.localScale = Vector3.one / jointScale;
                var localCenter = new Vector3(.025f, .03f, .015f);
                var center = root.TransformPoint(localCenter); var rotation = root.rotation * Quaternion.Euler(40, -25, 75);
                var value = FingerRingCalibrationUtility.Capture(root, thumb, index, center, rotation, .3f);
                value = JsonUtility.FromJson<FingerRingCalibration>(JsonUtility.ToJson(value));
                FingerRingCalibrationUtility.Validate(root, thumb, index, value);
                FingerRingCalibrationUtility.Evaluate(thumb, index, value, out var actual, out var facing);
                Assert.That(Vector3.Distance(actual, center), Is.LessThan(.00001f));
                Assert.That(Quaternion.Angle(facing, rotation), Is.LessThan(.05f));
                root.localScale *= 1.5f;
                FingerRingCalibrationUtility.Validate(root, thumb, index, value);
                FingerRingCalibrationUtility.Evaluate(thumb, index, value, out actual, out facing);
                Assert.That(Vector3.Distance(actual, root.TransformPoint(localCenter)), Is.LessThan(.00001f));
                Assert.That(Quaternion.Angle(facing, rotation), Is.LessThan(.05f));
            } finally { UnityEngine.Object.DestroyImmediate(parent); }
        }

        [Test]
        public void LegacyUnitScaleOffsetsRemainCompatibleAfterReloadAndRescale()
        {
            var root = new GameObject("Legacy saved avatar");
            try {
                var thumb = new GameObject("Thumb").transform; thumb.SetParent(root.transform, false);
                var index = new GameObject("Index").transform; index.SetParent(root.transform, false);
                thumb.localPosition = new Vector3(.1f, .02f, .04f); thumb.localRotation = Quaternion.Euler(40, 50, 60);
                index.localPosition = new Vector3(-.03f, .07f, -.06f); index.localRotation = Quaternion.Euler(-30, 60, 20);
                var center = new Vector3(.025f, .03f, .015f); var rotation = Quaternion.Euler(40, -25, 75);
                // Build the pre-1.1.1 representation independently of Capture.
                var old = new FingerRingCalibration { calibrated = true, thumbWeight = .3f,
                    thumbPath = "Thumb", indexPath = "Index",
                    thumbOffset = Quaternion.Inverse(thumb.rotation) * (center - thumb.position),
                    indexOffset = Quaternion.Inverse(index.rotation) * (center - index.position),
                    thumbRotation = Quaternion.Inverse(thumb.rotation) * rotation,
                    indexRotation = Quaternion.Inverse(index.rotation) * rotation,
                    center = center, euler = rotation.eulerAngles };
                old = JsonUtility.FromJson<FingerRingCalibration>(JsonUtility.ToJson(old));
                foreach (var scale in new[] { 1f, .5f, 2f }) {
                    root.transform.localScale = Vector3.one * scale;
                    FingerRingCalibrationUtility.Validate(root.transform, thumb, index, old);
                    FingerRingCalibrationUtility.Evaluate(thumb, index, old, out var actual, out var facing);
                    Assert.That(Vector3.Distance(actual, center * scale), Is.LessThan(.00001f));
                    Assert.That(Quaternion.Angle(facing, rotation), Is.LessThan(.05f));
                }
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(-1f, -1f, -1f)] [TestCase(2f, 1f, 1f)]
        public void CompensatedUnsafeAncestorScaleRequiresWarning(float x, float y, float z)
        {
            var parent = new GameObject("Unsafe ancestor");
            try {
                parent.transform.localScale = new Vector3(x, y, z);
                var root = new GameObject("Compensated avatar").transform; root.SetParent(parent.transform, false);
                root.localScale = new Vector3(1/x, 1/y, 1/z);
                var thumb = new GameObject("Thumb").transform; thumb.SetParent(root, false);
                var index = new GameObject("Index").transform; index.SetParent(root, false);
                Assert.That(FingerRingCalibrationUtility.CanUseFrame(thumb), Is.True);
                Assert.That(FingerRingCalibrationUtility.HasGuaranteedScale(thumb), Is.False);
                Assert.DoesNotThrow(() => FingerRingCalibrationUtility.Capture(root, thumb, index, Vector3.zero, Quaternion.identity, .5f));
            } finally { UnityEngine.Object.DestroyImmediate(parent); }
        }


        [TestCase(float.NaN, 1f, 1f)] [TestCase(float.PositiveInfinity, 1f, 1f)]
        [TestCase(float.NegativeInfinity, 1f, 1f)] [TestCase(0f, 1f, 1f)]
        [TestCase(0f, 0f, 0f)]
        public void UnsafeScaleValuesFailClosed(float x, float y, float z)
        {
            Assert.Throws<InvalidOperationException>(() => FingerRingCalibrationUtility.ValidateScale(new Vector3(x, y, z)));
        }

        [Test]
        public void ShearedFrameIsUsableButOutsideAccuracyGuarantee()
        {
            var root = new GameObject("Sheared avatar");
            try {
                var parent = new GameObject("Nonuniform bone").transform; parent.SetParent(root.transform, false);
                parent.localScale = new Vector3(2, 1, 1);
                var thumb = new GameObject("Thumb").transform; thumb.SetParent(parent, false);
                thumb.localRotation = Quaternion.Euler(0, 0, 45);
                thumb.localScale = new Vector3(1 / Mathf.Sqrt(2.5f), 1 / Mathf.Sqrt(2.5f), 1);
                var index = new GameObject("Index").transform; index.SetParent(root.transform, false);
                Assert.That(FingerRingCalibrationUtility.CanUseFrame(thumb), Is.True);
                Assert.That(FingerRingCalibrationUtility.HasGuaranteedScale(thumb), Is.False);
                Assert.DoesNotThrow(() => FingerRingCalibrationUtility.Capture(root.transform, thumb, index, Vector3.zero, Quaternion.identity, .5f));
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(2f, 1f, .5f)] [TestCase(-1f, 1f, 1f)] [TestCase(-1f, -1f, -1f)]
        public void UnguaranteedScaleCapturesFiniteCurrentPoseAndNativeDistances(float x, float y, float z)
        {
            var root = new GameObject("Best effort avatar");
            try {
                root.transform.localScale = new Vector3(x, y, z);
                root.transform.rotation = Quaternion.Euler(20, 30, 40);
                var thumb = new GameObject("Thumb").transform; thumb.SetParent(root.transform, false);
                var index = new GameObject("Index").transform; index.SetParent(root.transform, false);
                thumb.localPosition = new Vector3(.1f, .02f, .04f); thumb.localRotation = Quaternion.Euler(70, 25, 10);
                index.localPosition = new Vector3(-.03f, .07f, -.06f); index.localRotation = Quaternion.Euler(-40, 15, 60);
                var beforeThumb = thumb.localPosition; var beforeIndex = index.localPosition;
                Assert.That(FingerRingCalibrationUtility.HasGuaranteedScale(thumb), Is.False);
                var expected = thumb.position * .5f + index.position * .5f;
                var expectedFacing = Quaternion.Lerp(index.rotation, thumb.rotation, .3f);
                var value = FingerRingCalibrationUtility.CaptureProvisional(root.transform, thumb, index, .3f);
                value = JsonUtility.FromJson<FingerRingCalibration>(JsonUtility.ToJson(value));
                FingerRingCalibrationUtility.Validate(root.transform, thumb, index, value);
                FingerRingCalibrationUtility.Evaluate(thumb, index, value, out var actual, out var rotation);
                Assert.That(Vector3.Distance(actual, expected), Is.LessThan(.00001f));
                Assert.That(Quaternion.Angle(rotation, expectedFacing), Is.LessThan(.05f));
                var target = new GameObject("Target"); target.transform.SetParent(root.transform, false);
                FingerRingCalibrationUtility.Configure(target, thumb, index, value);
                var constraint = target.GetComponent<ParentConstraint>();
                var predictedThumb = thumb.position + thumb.rotation * constraint.GetTranslationOffset(0);
                var predictedIndex = index.position + index.rotation * constraint.GetTranslationOffset(1);
                Assert.That(Vector3.Distance(predictedThumb, expected), Is.LessThan(.00001f));
                Assert.That(Vector3.Distance(predictedIndex, expected), Is.LessThan(.00001f));
                Assert.That(thumb.localPosition, Is.EqualTo(beforeThumb));
                Assert.That(index.localPosition, Is.EqualTo(beforeIndex));
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(0f, 1f, 1f)] [TestCase(0f, 0f, 0f)]
        public void DegenerateFrameCannotCreateConstraintOrOverwriteCalibration(float x, float y, float z)
        {
            var root = new GameObject("Degenerate finger");
            try {
                var thumb = new GameObject("Thumb").transform; thumb.SetParent(root.transform, false);
                var index = new GameObject("Index").transform; index.SetParent(root.transform, false);
                var value = FingerRingCalibrationUtility.Capture(root.transform, thumb, index, Vector3.one * .01f, Quaternion.identity, .5f);
                string before = JsonUtility.ToJson(value);
                thumb.localScale = new Vector3(x, y, z);
                Assert.That(FingerRingCalibrationUtility.CanUseFrame(thumb), Is.False);
                var target = new GameObject("Target"); target.transform.SetParent(root.transform, false);
                Assert.Throws<FingerRingCalibrationUtility.UnusableFrameException>(() => FingerRingCalibrationUtility.Configure(target, thumb, index, value));
                Assert.That(target.GetComponent<ParentConstraint>(), Is.Null);
                Assert.That(JsonUtility.ToJson(value), Is.EqualTo(before));
                Assert.That(FingerRingCalibrationUtility.CanUseFrame(index), Is.True);
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void SavedCalibrationRejectsNonfiniteUiValuesAndWeight()
        {
            var root = new GameObject("Invalid saved calibration");
            try {
                var thumb = new GameObject("Thumb").transform; thumb.SetParent(root.transform, false);
                var index = new GameObject("Index").transform; index.SetParent(root.transform, false);
                var value = FingerRingCalibrationUtility.Capture(root.transform, thumb, index, Vector3.zero, Quaternion.identity, .5f);
                value.center.x = float.NaN;
                Assert.Throws<InvalidOperationException>(() => FingerRingCalibrationUtility.Validate(root.transform, thumb, index, value));
                value.center = Vector3.zero; value.euler.z = float.PositiveInfinity;
                Assert.Throws<InvalidOperationException>(() => FingerRingCalibrationUtility.Validate(root.transform, thumb, index, value));
                value.euler = Vector3.zero; value.thumbWeight = float.NaN;
                Assert.Throws<InvalidOperationException>(() => FingerRingCalibrationUtility.Validate(root.transform, thumb, index, value));
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void AuthoringCreatesMissingNativeConstraint()
        {
            var root = new GameObject("Ring creation regression");
            try {
                var thumb = new GameObject("Thumb").transform; thumb.SetParent(root.transform);
                var index = new GameObject("Index").transform; index.SetParent(root.transform);
                var target = new GameObject("Target"); target.transform.SetParent(root.transform);
                var calibration = FingerRingCalibrationUtility.Capture(root.transform, thumb, index, Vector3.one, Quaternion.identity, .3f);
                FingerRingCalibrationUtility.Configure(target, thumb, index, calibration);
                var constraint = target.GetComponent<ParentConstraint>();
                Assert.That(constraint != null, Is.True);
                Assert.That(constraint.sourceCount, Is.EqualTo(2));
                Assert.That(constraint.GetSource(0).sourceTransform, Is.SameAs(thumb));
                Assert.That(constraint.GetSource(0).weight, Is.EqualTo(.3f));
                Assert.That(constraint.constraintActive && constraint.locked, Is.True);
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(false)] [TestCase(true)]
        public void LegacyUpgradeAndReloadPreserveManualRingSelection(bool autoMode)
        {
            var settings = JsonUtility.FromJson<SetupSettings>("{\"autoMode\":true,\"parts\":[{\"id\":\"handLeft\",\"included\":true}]}");
            settings.autoMode = autoMode;
            FullSetupCatalog.UpgradeDisplayNames(settings);
            Assert.That(settings.parts.Count(p=>p.IsFingerRing), Is.EqualTo(2));
            Assert.That(settings.parts.Where(p=>p.IsFingerRing).All(p=>!p.included), Is.True);
            Assert.That(FullSetupCatalog.CreateDefault().parts.Where(p=>p.IsFingerRing).All(p=>!p.included), Is.True);
            FullSetupCatalog.ApplyPreset(settings, 2);
            settings.parts.Single(p=>p.id=="fingerRingLeft").included=false;
            settings.parts.Single(p=>p.id=="fingerRingLeft").fingerRing.thumbOffset=Vector3.one;
            var saved=JsonUtility.ToJson(settings);
            var restored=JsonUtility.FromJson<SetupSettings>(saved).Copy();
            FullSetupCatalog.UpgradeDisplayNames(restored);
            Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(saved));
            Assert.That(restored.autoMode, Is.EqualTo(autoMode));
            Assert.That(restored.parts.Single(p=>p.id=="fingerRingLeft").included, Is.False);
            Assert.That(restored.parts.Single(p=>p.id=="fingerRingRight").included, Is.True);
            FullSetupCatalog.ApplyPreset(restored, 0);
            restored.parts.Single(p=>p.id=="fingerRingLeft").included=true;
            saved=JsonUtility.ToJson(restored);
            var copy=JsonUtility.FromJson<SetupSettings>(saved).Copy();
            FullSetupCatalog.UpgradeDisplayNames(copy);
            Assert.That(JsonUtility.ToJson(copy), Is.EqualTo(saved));
            Assert.That(copy.parts.Single(p=>p.id=="fingerRingLeft").included, Is.True);
            Assert.That(copy.parts.Single(p=>p.id=="fingerRingRight").included, Is.False);
            copy.parts.Single(p=>p.id=="fingerRingLeft").fingerRing.thumbOffset=Vector3.zero;
            Assert.That(settings.parts.Single(p=>p.id=="fingerRingLeft").fingerRing.thumbOffset,Is.EqualTo(Vector3.one));
        }

        [TestCase(0, false)] [TestCase(0, true)]
        [TestCase(1, false)] [TestCase(1, true)]
        [TestCase(2, false)] [TestCase(2, true)]
        public void PresetsControlBothRingsAndPreserveSavedData(int preset, bool autoMode)
        {
            var settings=FullSetupCatalog.CreateDefault(); settings.autoMode=autoMode;
            settings.parts.Add(new SocketSettings { id="custom-on", custom=true, included=true, category=2 });
            settings.parts.Add(new SocketSettings { id="custom-off", custom=true, included=false, category=2 });
            int side=0;
            foreach(var ring in settings.parts.Where(p=>p.IsFingerRing)) {
                ring.included=side==0; ring.menuNameOverride="Saved ring "+side; ring.depth=true;
                ring.fingerRing=new FingerRingCalibration { calibrated=true, thumbWeight=side==0?.3f:.7f,
                    thumbPath="Hand/Thumb"+side, indexPath="Hand/Index"+side,
                    thumbOffset=new Vector3(.01f,.02f,.03f), indexOffset=new Vector3(-.02f,.04f,.01f),
                    thumbRotation=Quaternion.Euler(15,30,45), indexRotation=Quaternion.Euler(-20,60,10),
                    center=new Vector3(.3f,.4f,.5f), euler=new Vector3(10,20,30) };
                side++;
            }
            var before=settings.Copy();
            string[][] expected={
                new[]{"mouth","chest","handLeft","handRight","hands","vagina","anus"},
                new[]{"mouth","nippleLeft","nippleRight","chest","handLeft","handRight","hands","vagina","anus","footLeft","footRight","feet"},
                new[]{"mouth","earLeft","earRight","nippleLeft","nippleRight","chest","handLeft","handRight","hands","vagina","anus","thighs","footLeft","footRight","feet"}
            };
            foreach(var current in new[]{2,0,1,2,preset}) {
                FullSetupCatalog.ApplyPreset(settings,current);
                Assert.That(settings.parts.Where(p=>p.IsFingerRing).Select(p=>p.included), Is.EqualTo(new[]{current==2,current==2}));
                Assert.That(settings.parts.Where(p=>!p.custom&&!p.IsFingerRing&&p.included).Select(p=>p.id), Is.EquivalentTo(expected[current]));
                Assert.That(settings.autoMode, Is.EqualTo(autoMode));
                foreach(var part in settings.parts) {
                    var original=before.parts.Single(p=>p.id==part.id);
                    if(part.custom) Assert.That(part.included, Is.EqualTo(original.included));
                    var normalized=part.Copy(); normalized.included=original.included;
                    Assert.That(JsonUtility.ToJson(normalized), Is.EqualTo(JsonUtility.ToJson(original)), part.id);
                }
                var saved=JsonUtility.ToJson(settings);
                settings=JsonUtility.FromJson<SetupSettings>(saved).Copy();
                FullSetupCatalog.UpgradeDisplayNames(settings);
                Assert.That(JsonUtility.ToJson(settings), Is.EqualTo(saved));
            }
        }

        [TestCase(0f)] [TestCase(.3f)] [TestCase(.5f)] [TestCase(1f)]
        public void CalibratedFramesMeetAtUserCenterRegardlessOfWeightAndInitialThumbRotation(float weight)
        {
            var root=new GameObject("Avatar");
            try {
                root.transform.SetPositionAndRotation(new Vector3(2,3,-4),Quaternion.Euler(20,75,-50));
                var thumb=new GameObject("Thumb").transform;thumb.SetParent(root.transform);
                var index=new GameObject("Index").transform;index.SetParent(root.transform);
                thumb.localPosition=new Vector3(.1f,.02f,.04f);thumb.localRotation=Quaternion.Euler(170,-85,120);
                index.localPosition=new Vector3(-.03f,.07f,-.06f);index.localRotation=Quaternion.Euler(-95,35,60);
                var center=root.transform.TransformPoint(new Vector3(.025f,.03f,.015f));
                var rotation=root.transform.rotation*Quaternion.Euler(40,-25,75);
                var value=FingerRingCalibrationUtility.Capture(root.transform,thumb,index,center,rotation,weight);
                value=JsonUtility.FromJson<FingerRingCalibration>(JsonUtility.ToJson(value));
                FingerRingCalibrationUtility.Validate(root.transform,thumb,index,value);
                FingerRingCalibrationUtility.Evaluate(thumb,index,value,out var actual,out var facing);
                Assert.That(Vector3.Distance(actual,center),Is.LessThan(.00001f));
                Assert.That(Quaternion.Angle(facing,rotation),Is.LessThan(.05f));
                index.position=thumb.position;index.rotation=thumb.rotation;
                FingerRingCalibrationUtility.Evaluate(thumb,index,value,out actual,out facing);
                Assert.That(float.IsNaN(actual.x)||float.IsNaN(facing.w),Is.False,"Coincident joints must not create a cross-product singularity.");
                Assert.Throws<InvalidOperationException>(()=>FingerRingCalibrationUtility.Validate(root.transform,null,index,value));
                index.name="Renamed";
                Assert.Throws<InvalidOperationException>(()=>FingerRingCalibrationUtility.Validate(root.transform,thumb,index,value));
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [UnityTest, Explicit("Scale candidate gate: requires the existing persistent Play Mode result recorder.")]
        public IEnumerator UniformScaleNativeConstraintMatchesSavedCalibration() => CheckScaledConstraint(false);

        [UnityTest, Explicit("Requires persistent Play Mode recording; SDK simulation is not VRC-client evidence.")]
        public IEnumerator UniformScaleConvertedConstraintMatchesSavedCalibration() => CheckScaledConstraint(true);

        private IEnumerator CheckScaledConstraint(bool converted)
        {
            yield return new EnterPlayMode();
            foreach (var scales in new[] { new Vector3(.5f, 1, 1), new Vector3(2, 1, 1), new Vector3(2, .75f, 1.3f) }) {
                var parent = new GameObject("ScaleNativeAncestor");
                try {
                    parent.SetActive(false);
                    parent.transform.localScale = Vector3.one * scales.y;
                    var root = new GameObject("Avatar").transform; root.SetParent(parent.transform, false);
                    root.localScale = Vector3.one * scales.x;
                    var thumb = new GameObject("Thumb").transform; thumb.SetParent(root, false);
                    var index = new GameObject("Index").transform; index.SetParent(root, false);
                    thumb.localPosition = new Vector3(.04f, 1, .03f); thumb.localRotation = Quaternion.Euler(80, 125, -35);
                    index.localPosition = new Vector3(-.03f, 1.04f, .02f); index.localRotation = Quaternion.Euler(-45, 10, 65);
                    thumb.localScale = Vector3.one * scales.z; index.localScale = Vector3.one / scales.z;
                    var target = new GameObject("Socket"); target.transform.SetParent(root, false);
                    target.AddComponent<ParentConstraint>();
                    var center = root.TransformPoint(new Vector3(.01f, 1.025f, .065f));
                    var facing = root.rotation * Quaternion.Euler(20, 35, 80);
                    var saved = FingerRingCalibrationUtility.Capture(root, thumb, index, center, facing, .3f);
                    saved = JsonUtility.FromJson<FingerRingCalibration>(JsonUtility.ToJson(saved));
                    target.transform.SetPositionAndRotation(center, facing);
                    FingerRingCalibrationUtility.Configure(target, thumb, index, saved);
                    if (converted) {
                        var descriptor = root.gameObject.AddComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
                        Assert.That(VRC.SDK3.Avatars.AvatarDynamicsSetup.DoConvertUnityConstraints(new IConstraint[] { target.GetComponent<ParentConstraint>() }, descriptor, false), Is.False);
                        var constraint = target.GetComponent<VRC.SDK3.Dynamics.Constraint.Components.VRCParentConstraint>();
                        Assert.That(constraint != null && target.GetComponent<ParentConstraint>() == null, Is.True);
                        Assert.That(Vector3.Distance(constraint.Sources[0].ParentPositionOffset, saved.thumbOffset * thumb.lossyScale.x), Is.LessThan(.00001f));
                        constraint.ApplyConfigurationChanges();
                    }
                    parent.SetActive(true);
                    var initialThumb = thumb.localRotation; var initialIndex = index.localRotation;
                    for (int stage = 0; stage < (converted ? 3 : 5); stage++) {
                        if (stage == 1) { thumb.localRotation = Quaternion.Euler(110, -40, 170); index.localRotation = Quaternion.Euler(15, 100, -80); }
                        if (stage == 2) { thumb.localRotation = initialThumb; index.localRotation = initialIndex; }
                        if (stage == 3) {
                            root.localScale *= 1.5f;
                            FingerRingCalibrationUtility.Configure(target, thumb, index, saved);
                        }
                        if (stage == 4) {
                            thumb.localScale *= 1.2f; index.localScale *= .8f;
                            FingerRingCalibrationUtility.Configure(target, thumb, index, saved);
                        }
                        for (int frame = 0; frame < 4; frame++) yield return null;
                        FingerRingCalibrationUtility.Evaluate(thumb, index, saved, out var expected, out var rotation);
                        var rigid = Vector3.Lerp(index.position + index.rotation * saved.indexOffset,
                            thumb.position + thumb.rotation * saved.thumbOffset, saved.thumbWeight);
                        System.IO.File.AppendAllText("Library/Issue180Validation/scale-111-warning-native-v1.jsonl",
                            JsonUtility.ToJson(new ScaleOffsetObservation { converted = converted, stage = stage, rootScale = root.lossyScale.x,
                                thumbScale = thumb.lossyScale.x, indexScale = index.lossyScale.x,
                                scaledError = Vector3.Distance(target.transform.position, expected),
                                rigidError = Vector3.Distance(target.transform.position, rigid) }) + "\n");
                        Assert.That(Vector3.Distance(target.transform.position, expected), Is.LessThan(.0001f), "Position " + scales + " stage " + stage);
                        Assert.That(Quaternion.Angle(target.transform.rotation, rotation), Is.LessThan(.1f), "Facing " + scales + " stage " + stage);
                        Assert.That(target.transform.localScale, Is.EqualTo(Vector3.one), "Constraint must preserve authored socket size");
                        if (stage == 0 || stage == 2) Assert.That(Vector3.Distance(target.transform.position, center), Is.LessThan(.0001f));
                    }
                } finally { UnityEngine.Object.DestroyImmediate(parent); }
            }
            yield return new ExitPlayMode();
        }

        [UnityTest, Explicit("Warning-only scales: finite native/SDK output, not an accuracy guarantee.")]
        public IEnumerator WarningScaleNativeAndConvertedConstraintsStayFinite()
        {
            yield return new EnterPlayMode();
            foreach (var scale in new[] { new Vector3(2, 1, .5f), new Vector3(-1, 1, 1), new Vector3(-1, -1, -1) })
                foreach (bool converted in new[] { false, true }) {
                    var root = new GameObject("WarningScaleRuntime"); root.SetActive(false);
                    try {
                        root.transform.localScale = scale;
                        var thumb = new GameObject("Thumb").transform; thumb.SetParent(root.transform, false);
                        var index = new GameObject("Index").transform; index.SetParent(root.transform, false);
                        thumb.localPosition = new Vector3(.04f, 1, .03f); thumb.localRotation = Quaternion.Euler(80, 125, -35);
                        index.localPosition = new Vector3(-.03f, 1.04f, .02f); index.localRotation = Quaternion.Euler(-45, 10, 65);
                        var target = new GameObject("Ring"); target.transform.SetParent(root.transform, false);
                        target.AddComponent<ParentConstraint>();
                        var saved = FingerRingCalibrationUtility.CaptureProvisional(root.transform, thumb, index, .3f);
                        FingerRingCalibrationUtility.Evaluate(thumb, index, saved, out var position, out var rotation);
                        target.transform.SetPositionAndRotation(position, rotation);
                        FingerRingCalibrationUtility.Configure(target, thumb, index, saved);
                        if (converted) {
                            var descriptor = root.AddComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
                            Assert.That(VRC.SDK3.Avatars.AvatarDynamicsSetup.DoConvertUnityConstraints(new IConstraint[] { target.GetComponent<ParentConstraint>() }, descriptor, false), Is.False);
                            var constraint = target.GetComponent<VRC.SDK3.Dynamics.Constraint.Components.VRCParentConstraint>();
                            Assert.That(constraint != null, Is.True); constraint.ApplyConfigurationChanges();
                        }
                        root.SetActive(true);
                        for (int stage = 0; stage < 2; stage++) {
                            if (stage == 1) { thumb.localRotation = Quaternion.Euler(110, -40, 170); index.localRotation = Quaternion.Euler(15, 100, -80); }
                            for (int frame = 0; frame < 8; frame++) yield return null;
                            var p = target.transform.position; var q = target.transform.rotation;
                            Assert.That(new[] { p.x, p.y, p.z, q.x, q.y, q.z, q.w }.All(v => !float.IsNaN(v) && !float.IsInfinity(v)), Is.True, scale + " converted=" + converted + " stage=" + stage);
                            Assert.That(target.transform.localScale, Is.EqualTo(Vector3.one));
                        }
                    } finally { UnityEngine.Object.DestroyImmediate(root); }
                }
            yield return new ExitPlayMode();
        }

        [UnityTest, Explicit("Requires a persistent Play Mode evidence callback across domain reload; excluded from the transient MCP EditMode callback.")]
        public IEnumerator NativeParentConstraintMovesThenReturnsToCalibration()
        {
            yield return new EnterPlayMode();
            var root=new GameObject("RingRuntimeObservation");
            try {
                var a=new GameObject("Thumb").transform;a.SetParent(root.transform);
                var b=new GameObject("Index").transform;b.SetParent(root.transform);
                var target=new GameObject("Socket");target.transform.SetParent(root.transform);
                // Undo.AddComponent is an authoring operation; the runtime fixture owns this component.
                target.AddComponent<ParentConstraint>();
                a.position=new Vector3(.04f,1,.03f);a.rotation=Quaternion.Euler(80,125,-35);
                b.position=new Vector3(-.03f,1.04f,.02f);b.rotation=Quaternion.Euler(-45,10,65);
                var center=new Vector3(.01f,1.025f,.065f);var facing=Quaternion.Euler(20,35,80);
                var value=FingerRingCalibrationUtility.Capture(root.transform,a,b,center,facing,.3f);
                target.transform.SetPositionAndRotation(center,facing);
                FingerRingCalibrationUtility.Configure(target,a,b,value);
                for(int frame=0;frame<4;frame++) yield return null;
                Assert.That(Vector3.Distance(target.transform.position,center),Is.LessThan(.0001f));
                var originalA=a.rotation;var originalB=b.rotation;
                a.rotation=Quaternion.Euler(110,-40,170);b.rotation=Quaternion.Euler(15,100,-80);
                for(int frame=0;frame<4;frame++) yield return null;
                FingerRingCalibrationUtility.Evaluate(a,b,value,out var moved,out var movedFacing);
                Assert.That(Vector3.Distance(target.transform.position,moved),Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(target.transform.position,center),Is.GreaterThan(.001f));
                Assert.That(Quaternion.Angle(target.transform.rotation,movedFacing),Is.LessThan(1f));
                a.rotation=originalA;b.rotation=originalB;
                for(int frame=0;frame<4;frame++) yield return null;
                Assert.That(Vector3.Distance(target.transform.position,center),Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(target.transform.rotation,facing),Is.LessThan(.1f));
                var constraint=target.GetComponent<ParentConstraint>();constraint.constraintActive=false;
                target.transform.position=Vector3.one;
                for(int frame=0;frame<4;frame++) yield return null;
                Assert.That(target.transform.position,Is.EqualTo(Vector3.one),"Disabled constraint must release the Transform.");
            } finally { UnityEngine.Object.DestroyImmediate(root); }
            yield return new ExitPlayMode();
        }

        [UnityTest, Explicit("Requires persistent Play Mode result recording; SDK simulation is not VRC-client evidence.")]
        public IEnumerator ConvertedParentConstraintsFollowBothMirroredFrames()
        {
            yield return new EnterPlayMode();
            var root = new GameObject("ConvertedRingRuntimeObservation");
            root.SetActive(false); // Conversion/configuration precedes runtime OnEnable.
            var rows = new System.Collections.Generic.List<ConvertedRow>();
            bool passed = false;
            try {
                var descriptor = root.AddComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
                var thumbs = new Transform[2]; var indices = new Transform[2];
                var targets = new Transform[2]; var values = new FingerRingCalibration[2];
                var constraints = new VRC.SDK3.Dynamics.Constraint.Components.VRCParentConstraint[2];
                var thumbRotations = new Quaternion[2]; var indexRotations = new Quaternion[2];
                for (int side = 0; side < 2; side++) {
                    float sign = side == 0 ? 1 : -1;
                    thumbs[side] = new GameObject("Thumb" + side).transform; thumbs[side].SetParent(root.transform, false);
                    indices[side] = new GameObject("Index" + side).transform; indices[side].SetParent(root.transform, false);
                    targets[side] = new GameObject("Ring" + side).transform; targets[side].SetParent(root.transform, false);
                    thumbs[side].position = new Vector3(sign * .04f, 1, .03f);
                    indices[side].position = new Vector3(sign * -.03f, 1.04f, .02f);
                    thumbs[side].rotation = MirrorRotation(Quaternion.Euler(80, 125, -35), side);
                    indices[side].rotation = MirrorRotation(Quaternion.Euler(-45, 10, 65), side);
                    thumbRotations[side] = thumbs[side].rotation; indexRotations[side] = indices[side].rotation;
                    var center = new Vector3(sign * .01f, 1.025f, .065f);
                    var facing = MirrorRotation(Quaternion.Euler(20, 35, 80), side);
                    values[side] = FingerRingCalibrationUtility.Capture(root.transform, thumbs[side], indices[side], center, facing, side == 0 ? .3f : .7f);
                    targets[side].SetPositionAndRotation(center, facing);
                    var native = targets[side].gameObject.AddComponent<ParentConstraint>();
                    FingerRingCalibrationUtility.Configure(targets[side].gameObject, thumbs[side], indices[side], values[side]);
                    Assert.That(VRC.SDK3.Avatars.AvatarDynamicsSetup.DoConvertUnityConstraints(new IConstraint[] { native }, descriptor, false), Is.False, "SDK conversion reports issues as true.");
                    constraints[side] = targets[side].GetComponent<VRC.SDK3.Dynamics.Constraint.Components.VRCParentConstraint>();
                    Assert.That(constraints[side] != null && targets[side].GetComponent<ParentConstraint>() == null, Is.True);
                    Assert.That(constraints[side].Sources.Count, Is.EqualTo(2));
                    Assert.That(constraints[side].Sources[0].SourceTransform, Is.SameAs(thumbs[side]));
                    Assert.That(constraints[side].Sources[0].Weight, Is.EqualTo(values[side].thumbWeight));
                    Assert.That(Vector3.Distance(constraints[side].Sources[0].ParentPositionOffset, values[side].thumbOffset * thumbs[side].lossyScale.x), Is.LessThan(.000001f));
                    constraints[side].ApplyConfigurationChanges();
                }
                root.SetActive(true);
                for (int frame = 0; frame < 8; frame++) yield return null;
                for (int side = 0; side < 2; side++) ObserveConverted("closed", side, targets[side], thumbs[side], indices[side], values[side], rows);
                for (int side = 0; side < 2; side++) {
                    thumbs[side].rotation = MirrorRotation(Quaternion.Euler(110, -40, 170), side);
                    indices[side].rotation = MirrorRotation(Quaternion.Euler(15, 100, -80), side);
                    indices[side].position += new Vector3(side == 0 ? .03f : -.03f, .01f, -.025f);
                }
                for (int frame = 0; frame < 8; frame++) yield return null;
                for (int side = 0; side < 2; side++) ObserveConverted("open", side, targets[side], thumbs[side], indices[side], values[side], rows);
                for (int side = 0; side < 2; side++) {
                    thumbs[side].rotation = thumbRotations[side]; indices[side].rotation = indexRotations[side];
                    indices[side].position = new Vector3(side == 0 ? -.03f : .03f, 1.04f, .02f);
                }
                for (int frame = 0; frame < 8; frame++) yield return null;
                for (int side = 0; side < 2; side++) ObserveConverted("return", side, targets[side], thumbs[side], indices[side], values[side], rows);
                foreach (var constraint in constraints) { constraint.IsActive = false; constraint.ApplyConfigurationChanges(); }
                foreach (var target in targets) target.position = Vector3.one;
                for (int frame = 0; frame < 8; frame++) yield return null;
                foreach (var target in targets) Assert.That(target.position, Is.EqualTo(Vector3.one), "Inactive converted constraint releases the target.");
                foreach (var constraint in constraints) { constraint.IsActive = true; constraint.ApplyConfigurationChanges(); }
                for (int frame = 0; frame < 8; frame++) yield return null;
                for (int side = 0; side < 2; side++) ObserveConverted("reactivate", side, targets[side], thumbs[side], indices[side], values[side], rows);
                passed = true;
            } finally {
                System.IO.Directory.CreateDirectory("Library/Issue180Validation");
                System.IO.File.WriteAllText("Library/Issue180Validation/scale-111-warning-unit-converted-v1.json", JsonUtility.ToJson(new ConvertedRuntimeResult { passed = passed, sdkSimulation = true, vrcClientVerified = false, rows = rows }, true));
                UnityEngine.Object.DestroyImmediate(root);
            }
            yield return new ExitPlayMode();
        }

        [Serializable] private sealed class ScaleOffsetObservation { public bool converted; public int stage; public float rootScale, thumbScale, indexScale, scaledError, rigidError; }

        [Serializable] private sealed class ConvertedRow { public string stage; public int side; public float positionError, rotationError; }
        [Serializable] private sealed class ConvertedRuntimeResult { public bool passed, sdkSimulation, vrcClientVerified; public System.Collections.Generic.List<ConvertedRow> rows; }

        private static Quaternion MirrorRotation(Quaternion rotation, int side) => side == 0 ? rotation : new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w);

        private static void ObserveConverted(string stage, int side, Transform target, Transform thumb, Transform index, FingerRingCalibration value, System.Collections.Generic.List<ConvertedRow> rows)
        {
            FingerRingCalibrationUtility.Evaluate(thumb, index, value, out var expectedPosition, out var expectedRotation);
            float positionError = Vector3.Distance(target.position, expectedPosition);
            float rotationError = Quaternion.Angle(target.rotation, expectedRotation);
            rows.Add(new ConvertedRow { stage = stage, side = side, positionError = positionError, rotationError = rotationError });
            Assert.That(positionError, Is.LessThan(.0001f), stage + " position, side " + side);
            Assert.That(rotationError, Is.LessThan(1f), stage + " rotation, side " + side);
        }

        [UnityTearDown]
        public IEnumerator LeaveOwnedPlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
