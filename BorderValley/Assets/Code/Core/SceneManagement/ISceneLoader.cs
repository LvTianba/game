using System.Threading.Tasks;

namespace BorderValley.Core.SceneManagement
{
    public interface ISceneLoader
    {
        Task LoadAsync(string sceneName);
        string ActiveSceneName { get; }
    }
}
