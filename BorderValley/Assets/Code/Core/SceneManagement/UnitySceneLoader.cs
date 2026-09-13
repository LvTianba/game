using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace BorderValley.Core.SceneManagement
{
    public sealed class UnitySceneLoader : ISceneLoader
    {
        public string ActiveSceneName => SceneManager.GetActiveScene().name;

        public Task LoadAsync(string sceneName)
        {
            var completion = new TaskCompletionSource<bool>();
            var operation = SceneManager.LoadSceneAsync(sceneName);
            if (operation == null)
                throw new System.InvalidOperationException($"Scene is not in Build Settings: {sceneName}");
            operation.completed += _ => completion.SetResult(true);
            return completion.Task;
        }
    }
}
