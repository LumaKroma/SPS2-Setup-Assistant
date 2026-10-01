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

        [TestCase(.5f)] [TestCase(2f)]
        public void UniformAvatarScaleIsStillRejected(float scale)
        {
            var root = new GameObject("Scaled avatar");
            try {
                var thumb = new GameObject("Thumb").transform; thumb.SetParent(root.transform, false);
                var index = new GameObject("Index").transform; index.SetParent(root.transform, false);
                root.transform.localScale = Vector3.one * scale;
                Assert.Throws<InvalidOperationException>(() => FingerRingCalibrationUtility.Capture(root.transform, thumb, index, Vector3.zero, Quaternion.identity, .5f));
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(float.NaN, 1f, 1f)] [TestCase(float.PositiveInfinity, 1f, 1f)]
        [TestCase(float.NegativeInfinity, 1f, 1f)] [TestCase(0f, 1f, 1f)]
        [TestCase(-1f, 1f, 1f)] [TestCase(2f, 1f, .5f)]
        public void UnsafeScaleValuesFailClosed(float x, float y, float z)
        {
            Assert.Throws<InvalidOperationException>(() => FingerRingCalibrationUtility.ValidateScale(new Vector3(x, y, z)));
        }

        [Test]
        public void CompensatedNonuniformBoneChainCannotHideShear()
        {
            var root = new GameObject("Sheared avatar");
            try {
                var parent = new GameObject("Nonuniform bone").transform; parent.SetParent(root.transform, false);
                parent.localScale = new Vector3(2, 1, 1);
                var thumb = new GameObject("Thumb").transform; thumb.SetParent(parent, false);
                thumb.localRotation = Quaternion.Euler(0, 0, 45);
                thumb.localScale = new Vector3(1 / Mathf.Sqrt(2.5f), 1 / Mathf.Sqrt(2.5f), 1);
                var index = new GameObject("Index").transform; index.SetParent(root.transform, false);
                Assert.Throws<InvalidOperationException>(() => FingerRingCalibrationUtility.Capture(root.transform, thumb, index, Vector3.zero, Quaternion.identity, .5f));
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

        [Test]
        public void LegacyAndPresetsDoNotEnableRingsOrChangeAuto()
        {
            var settings = JsonUtility.FromJson<SetupSettings>("{\"autoMode\":true,\"parts\":[{\"id\":\"handLeft\",\"included\":true}]}");
            FullSetupCatalog.UpgradeDisplayNames(settings);
            foreach (var preset in new[] { 0, 1, 2 }) {
                FullSetupCatalog.ApplyPreset(settings, preset);
                Assert.That(settings.parts.Where(p=>p.IsFingerRing).Count(), Is.EqualTo(2));
                Assert.That(settings.parts.Where(p=>p.IsFingerRing).All(p=>!p.included), Is.True);
                Assert.That(settings.autoMode, Is.True);
            }
            settings.parts.Single(p=>p.id=="fingerRingLeft").fingerRing.thumbOffset=Vector3.one;
            var copy=settings.Copy();copy.parts.Single(p=>p.id=="fingerRingLeft").fingerRing.thumbOffset=Vector3.zero;
            Assert.That(settings.parts.Single(p=>p.id=="fingerRingLeft").fingerRing.thumbOffset,Is.EqualTo(Vector3.one));
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
                    Assert.That(constraints[side].Sources[0].ParentPositionOffset, Is.EqualTo(values[side].thumbOffset));
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
                System.IO.File.WriteAllText("Library/Issue180Validation/gesture-refinement-converted-runtime-v8.json", JsonUtility.ToJson(new ConvertedRuntimeResult { passed = passed, sdkSimulation = true, vrcClientVerified = false, rows = rows }, true));
                UnityEngine.Object.DestroyImmediate(root);
            }
            yield return new ExitPlayMode();
        }

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
