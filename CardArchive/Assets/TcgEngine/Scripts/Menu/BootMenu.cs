using UnityEngine;
using UnityEngine.Events;

namespace TcgEngine.UI
{
    /// <summary>
    /// First scene of the client build: black screen that checks the version before the login menu
    /// </summary>

    public class BootMenu : MonoBehaviour
    {
        public string next_scene = "LoginMenu";

        public UnityAction<VersionCheckResult> onCheckFailed; //Mismatch or network error, the scene stays on the black screen

        private bool checking = false;

        public VersionCheckResult Result { get; private set; }

        void Start()
        {
            CheckVersion();
        }

        public async void CheckVersion()
        {
            if (checking)
                return;

            checking = true;
            VersionCheckResult result = await ApiClient.Get().CheckVersion();
            checking = false;

            // The request can finish after leaving this scene or stopping Play Mode.
            if (!Application.isPlaying || this == null)
                return;

            Result = result;
            if (result.Passed)
            {
                SceneNav.GoTo(next_scene);
                return;
            }

            Debug.LogWarning("Version check failed: " + result.status + " (client " + result.client_version
                + ", server " + result.server_version + ") " + result.error);
            onCheckFailed?.Invoke(result);
        }
    }
}
