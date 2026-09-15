using System;
using System.Threading.Tasks;
using BorderValley.Core.SceneManagement;
using BorderValley.Presentation;

namespace BorderValley.UI
{
    public sealed class MainMenuPresenter
    {
        private readonly ISceneLoader sceneLoader;
        private readonly IPresentationService presentation;

        public MainMenuPresenter(
            ISceneLoader sceneLoader,
            IPresentationService presentation = null)
        {
            this.sceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
            this.presentation = presentation ?? new NullPresentationService();
        }

        public void StartNewGame()
        {
            presentation.PlaySfx("sfx.ui.confirm");
            _ = LoadWorld();
        }

        private async Task LoadWorld()
        {
            try
            {
                await sceneLoader.LoadAsync("World");
            }
            catch
            {
                presentation.PlaySfx("sfx.ui.error");
            }
        }
    }
}
