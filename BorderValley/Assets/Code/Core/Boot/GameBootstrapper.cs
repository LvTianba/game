using System.Collections.Generic;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.Boot;
using BorderValley.Core.Persistence;
using BorderValley.Core.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BorderValley.Core
{
    public sealed class GameBootstrapper : MonoBehaviour
    {
        public static GameContext Context { get; private set; }
        public static bool StartupBlocked { get; private set; }

        [SerializeField] private MonoBehaviour[] preflightChecks = System.Array.Empty<MonoBehaviour>();

        private void Awake()
        {
            StartupBlocked = false;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            DontDestroyOnLoad(gameObject);
            Context = new GameContext();
            Context.Register<ISceneLoader>(new UnitySceneLoader());
            Context.Register<IBattleFlow>(new BattleFlowService());
            Context.Register(new SaveService(Application.persistentDataPath, System.Array.Empty<ISaveParticipant>()));
        }

        private void Start()
        {
            var checks = new List<IPreflightCheck>();
            foreach (var candidate in preflightChecks)
            {
                if (candidate is IPreflightCheck check)
                {
                    checks.Add(check);
                }
            }

            if (!StartupPreflight.ShouldContinue(checks, Debug.isDebugBuild, out var errors))
            {
                StartupBlocked = true;
                Debug.LogError($"Startup blocked by preflight validation:\n{errors}");
                return;
            }

            if (!string.IsNullOrWhiteSpace(errors))
            {
                Debug.LogError($"Startup validation warnings:\n{errors}");
            }

            SceneManager.LoadScene("MainMenu");
        }
    }
}
