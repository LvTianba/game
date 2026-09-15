using System.Reflection;
using BorderValley.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BorderValley.Presentation.Tests
{
    public sealed class PresentationBootstrapInstallerTests
    {
        [Test]
        public void Install_MissingCatalog_RegistersNullServiceWithoutThrowing()
        {
            var root = new GameObject("installer");
            try
            {
                var installer = root.AddComponent<PresentationBootstrapInstaller>();
                var context = new GameContext();

                Assert.DoesNotThrow(() => installer.Install(context, (PresentationCatalog)null));

                var service = context.Get<IPresentationService>();
                Assert.That(service, Is.TypeOf<NullPresentationService>());
                Assert.That(service.IsAvailable, Is.False);
                Assert.That(root.transform.Find("PresentationRuntime"), Is.Null);
            }
            finally
            {
                AudioListener.pause = false;
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OnApplicationPause_ForwardsToDirectorAndRetainsDesiredMusic()
        {
            var root = new GameObject("installer");
            try
            {
                var installer = root.AddComponent<PresentationBootstrapInstaller>();
                var context = new GameContext();
                var catalog = AssetDatabase.LoadAssetAtPath<PresentationCatalog>(
                    "Assets/Resources/PresentationCatalog.asset");
                Assert.That(catalog, Is.Not.Null);

                installer.Install(context, catalog);
                var service = context.Get<IPresentationService>();
                var output = root.GetComponentInChildren<UnityAudioOutput>();
                Assert.That(output, Is.Not.Null);

                service.PlayMusic("bgm.menu");
                Assert.That(output.CurrentMusicCueId, Is.EqualTo("bgm.menu"));
                Assert.That(AudioListener.pause, Is.False);

                InvokeApplicationPause(installer, true);
                service.PlayMusic("bgm.battle");

                Assert.That(AudioListener.pause, Is.True);
                Assert.That(output.CurrentMusicCueId, Is.EqualTo("bgm.menu"));

                InvokeApplicationPause(installer, false);

                Assert.That(AudioListener.pause, Is.False);
                Assert.That(output.CurrentMusicCueId, Is.EqualTo("bgm.battle"));
            }
            finally
            {
                AudioListener.pause = false;
                Object.DestroyImmediate(root);
            }
        }

        private static void InvokeApplicationPause(PresentationBootstrapInstaller installer, bool paused)
        {
            var method = typeof(PresentationBootstrapInstaller).GetMethod(
                "OnApplicationPause",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(installer, new object[] { paused });
        }
    }
}
