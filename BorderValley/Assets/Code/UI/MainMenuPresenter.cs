using BorderValley.Core.SceneManagement;

namespace BorderValley.UI
{
    public sealed class MainMenuPresenter
    {
        private readonly ISceneLoader sceneLoader;
        public MainMenuPresenter(ISceneLoader sceneLoader) => this.sceneLoader = sceneLoader;
        public void StartNewGame() => _ = sceneLoader.LoadAsync("World");
    }
}
