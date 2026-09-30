using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Tests
{
    // Disposable validation support, excluded from both distribution formats.
    // Unity's public runner owns Play Mode transitions; this only persists evidence.
    [InitializeOnLoad]
    public static class FingerRingRuntimeRecorder
    {
        private const string Pending = "LumaKroma.Issue180.RingRuntimePending";
        private const string Output = "Library/Issue180Validation/ring-runtime-test.json";
        private static TestRunnerApi api;
        static FingerRingRuntimeRecorder()
        {
            if (SessionState.GetBool(Pending, false)) Register();
        }
        private static void Register()
        {
            if (api != null) return;
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Recorder());
        }
        public static void Start()
        {
            if (Application.isPlaying || SessionState.GetBool(Pending, false) || File.Exists(Output))
                throw new InvalidOperationException("Runtime test already running or prior evidence exists.");
            for (int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save and audit every scene before this test.");
            SessionState.SetBool(Pending, true);
            Register();
            api.Execute(new ExecutionSettings(new Filter {
                testMode = TestMode.EditMode,
                testNames = new[] { "LumaKroma.Sps2SetupAssistant.Editor.Tests.FingerRingCalibrationTests.NativeParentConstraintMovesThenReturnsToCalibration" }
            }));
        }
        [Serializable] private sealed class Result
        {
            public string state, message, stackTrace;
            public int passed, failed, skipped;
        }
        private sealed class Recorder : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool(Pending, false)) return;
                Directory.CreateDirectory(Path.GetDirectoryName(Output));
                File.WriteAllText(Output,JsonUtility.ToJson(new Result {
                    state=result.ResultState, message=result.Message, stackTrace=result.StackTrace,
                    passed=result.PassCount, failed=result.FailCount, skipped=result.SkipCount
                },true));
                SessionState.SetBool(Pending,false);
            }
        }
    }
}
