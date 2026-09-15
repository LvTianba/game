using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BorderValley.Core;
using BorderValley.Core.Boot;
using BorderValley.Core.Persistence;
using UnityEngine;

[assembly: InternalsVisibleTo("BorderValley.EditModeTests")]

namespace BorderValley.Presentation
{
    public sealed class PresentationBootstrapInstaller : MonoBehaviour, IGameServiceInstaller
    {
        private AudioDirector audioDirector;

        public void Install(GameContext context, ICollection<ISaveParticipant> participants)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (participants == null) throw new ArgumentNullException(nameof(participants));

            Install(context, Resources.Load<PresentationCatalog>("PresentationCatalog"));
        }

        internal void Install(GameContext context, PresentationCatalog catalog)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            audioDirector = null;
            if (catalog == null)
            {
                Debug.LogWarning("PresentationCatalog is missing. Presentation services run in no-op mode.");
                context.Register<IPresentationService>(new NullPresentationService());
                return;
            }

            var runtimeTransform = transform.Find("PresentationRuntime");
            var output = runtimeTransform == null
                ? null
                : runtimeTransform.GetComponent<UnityAudioOutput>();
            if (output == null)
            {
                var runtime = new GameObject("PresentationRuntime");
                runtime.transform.SetParent(transform, false);
                output = runtime.AddComponent<UnityAudioOutput>();
            }

            output.Initialize();
            audioDirector = new AudioDirector(output);
            var service = new PresentationService(catalog, audioDirector);
            context.Register<IPresentationService>(service);
        }

        private void OnApplicationPause(bool paused)
        {
            audioDirector?.SetPaused(paused);
        }
    }
}
