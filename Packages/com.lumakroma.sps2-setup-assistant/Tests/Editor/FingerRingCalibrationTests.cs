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

        [UnityTearDown]
        public IEnumerator LeaveOwnedPlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
