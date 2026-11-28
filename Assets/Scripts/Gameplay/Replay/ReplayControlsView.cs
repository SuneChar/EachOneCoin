using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EachOneCoin.Gameplay.Replay
{
    public sealed class ReplayControlsView : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text pauseLabel;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button backButton;
        private ReplayPlayer player;

        public void Bind(ReplayPlayer replayPlayer)
        {
            player = replayPlayer;
            pauseButton.onClick.AddListener(player.TogglePause);
            backButton.onClick.AddListener(ReturnToList);
            gameObject.SetActive(true);
            GamepadNavigation.Configure(gameObject);
        }

        public void Refresh(float elapsed, bool paused, bool failed)
        {
            statusText.text = failed ? "录像播放出错" : $"录像 {elapsed:0.0} 秒";
            pauseLabel.text = paused ? "继续" : "暂停";
        }

        private void LateUpdate()
        {
            var input = InputDeviceTracker.Instance;
            GamepadNavigation.EnsureFocus(gameObject, input != null && input.UsingGamepad);
        }

        private void ReturnToList()
        {
            ReplaySelection.ReturnToList = true;
            SceneManager.LoadScene("BootScene");
        }

        private void OnDestroy()
        {
            if (pauseButton != null && player != null) pauseButton.onClick.RemoveListener(player.TogglePause);
            if (backButton != null) backButton.onClick.RemoveListener(ReturnToList);
        }
    }
}
