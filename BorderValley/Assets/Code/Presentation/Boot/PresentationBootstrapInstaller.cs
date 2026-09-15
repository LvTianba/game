using System;
using System.Collections.Generic;
using BorderValley.Core;
using BorderValley.Core.Boot;
using BorderValley.Core.Persistence;
using UnityEngine;

namespace BorderValley.Presentation
{
    public sealed class PresentationBootstrapInstaller : MonoBehaviour, IGameServiceInstaller
    {
        public void Install(GameContext context, ICollection<ISaveParticipant> participants)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (participants == null) throw new ArgumentNullException(nameof(participants));

            var catalog = Resources.Load<PresentationCatalog>("PresentationCatalog");
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
            var director = new AudioDirector(output);
            var service = new PresentationService(catalog, director);
            context.Register<IPresentationService>(service);
        }
    }
}
