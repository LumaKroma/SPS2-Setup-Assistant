using LumaKroma.Sps2SetupAssistant.Editor.Generation;
using LumaKroma.Sps2SetupAssistant.Editor.Model;
using NUnit.Framework;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Tests
{
    public sealed class OwnedRootTests
    {
        private GameObject avatar;

        [SetUp]
        public void SetUp()
        {
            avatar = new GameObject("Avatar");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(avatar);
        }

        [Test]
        public void FindOwnedRoot_LeavesUnrelatedChildrenAlone()
        {
            new GameObject("Unrelated Socket").transform.SetParent(avatar.transform);

            Assert.That(SocketSetupGenerator.TryFindOwnedRoot(
                avatar.transform,
                out var root,
                out var error), Is.True, error);
            Assert.That(root, Is.Null);
        }

        [Test]
        public void FindOwnedRoot_AcceptsOnlyDeterministicCatalogSignature()
        {
            var root = new GameObject(SocketSetupGenerator.OwnedRootName);
            root.transform.SetParent(avatar.transform);
            var anchor = new GameObject(SocketCatalog.Presets[0].NodeName);
            anchor.transform.SetParent(root.transform);
            new GameObject(SocketSetupGenerator.SocketPoseName).transform.SetParent(anchor.transform);

            Assert.That(SocketSetupGenerator.TryFindOwnedRoot(
                avatar.transform,
                out var found,
                out var error), Is.True, error);
            Assert.That(found, Is.EqualTo(root.transform));
        }

        [Test]
        public void FindOwnedRoot_FailsClosedForMalformedExactNameRoot()
        {
            var root = new GameObject(SocketSetupGenerator.OwnedRootName);
            root.transform.SetParent(avatar.transform);
            new GameObject("User Content").transform.SetParent(root.transform);

            Assert.That(SocketSetupGenerator.TryFindOwnedRoot(
                avatar.transform,
                out _,
                out var error), Is.False);
            Assert.That(error, Does.Contain("unexpected hierarchy"));
        }
    }
}
