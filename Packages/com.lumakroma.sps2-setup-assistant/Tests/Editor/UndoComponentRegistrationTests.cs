using LumaKroma.Sps2SetupAssistant.Editor.Model;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Tests
{
    public sealed class UndoComponentRegistrationTests
    {
        [Test]
        public void Invoke_RegistersComponentsCreatedByExternalFactories()
        {
            var target = new GameObject("Undo Component Target");
            try
            {
                Undo.IncrementCurrentGroup();
                var undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Add External Component");

                var component = UndoComponentRegistration.Invoke(
                    target,
                    "Add External Component",
                    () => target.AddComponent<BoxCollider>());
                Undo.CollapseUndoOperations(undoGroup);

                Assert.That(component, Is.Not.Null);
                Undo.PerformUndo();
                Assert.That(target.GetComponent<BoxCollider>(), Is.Null);

                Undo.PerformRedo();
                Assert.That(target.GetComponent<BoxCollider>(), Is.Not.Null);
            }
            finally
            {
                Undo.ClearAll();
                Object.DestroyImmediate(target);
            }
        }
    }
}
