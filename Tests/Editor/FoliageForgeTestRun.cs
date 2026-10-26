using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Meowdieval.FoliageForge.Tests
{
    /// <summary>Runs this package's tests after the next compile when Temp/FoliageForgeTests.trigger exists; results go to Library/FoliageForgeVerify/tests.txt.</summary>
    static class FoliageForgeTestRun
    {
        [InitializeOnLoadMethod]
        private static void AutoRun()
        {
            string trigger = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "FoliageForgeTests.trigger");
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(trigger)) return;
                File.Delete(trigger);
                var api = UnityEngine.ScriptableObject.CreateInstance<TestRunnerApi>();
                api.RegisterCallbacks(new Report());
                api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "Meowdieval.FoliageForge.Tests" } }));
            };
        }

        class Report : ICallbacks
        {
            readonly StringBuilder _log = new StringBuilder();
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor r)
            {
                if (!r.HasChildren) _log.AppendLine($"{r.TestStatus} | {r.Test.FullName} | {r.Message}");
            }
            public void RunFinished(ITestResultAdaptor r)
            {
                string dir = Path.Combine(Directory.GetCurrentDirectory(), "Library", "FoliageForgeVerify");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "tests.txt"), $"{r.PassCount} passed, {r.FailCount} failed{System.Environment.NewLine}{_log}");
            }
        }
    }
}
