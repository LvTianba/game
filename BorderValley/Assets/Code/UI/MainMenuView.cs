using BorderValley.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BorderValley.UI
{
    public sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField] private Button newGameButton;
        private MainMenuPresenter presenter;

        private void Start()
        {
            var presentation = PresentationUiUtility.GetOrNull();
            presentation?.PlayMusic("bgm.menu");
            PresentationUiUtility.ApplyButton(
                newGameButton,
                PresentationUiUtility.ResolveButton(presentation),
                PresentationUiUtility.ResolvePressedButton(presentation));
            var loader = GameBootstrapper.Context.Get<Core.SceneManagement.ISceneLoader>();
            presenter = new MainMenuPresenter(loader, presentation);
            newGameButton.onClick.AddListener(presenter.StartNewGame);
        }

        private void OnDestroy() => newGameButton.onClick.RemoveAllListeners();
    }
}
