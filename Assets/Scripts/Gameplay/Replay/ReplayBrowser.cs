using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EachOneCoin.Gameplay.Replay
{
    // Displays recordings using the preconfigured menu panel.
    public sealed class ReplayBrowser : MonoBehaviour
    {
        private const int PageSize = 6;
        private BootMenu menu;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button entryButton;
        [SerializeField] private TMP_Text title;
        [SerializeField] private Button[] rows = new Button[PageSize];
        [SerializeField] private Button previous, next, back;
        private string[] paths = Array.Empty<string>();
        private int page;
        public GameObject Panel => panel;
        public bool IsOpen => panel != null && panel.activeSelf;

        public void Initialize(BootMenu owner)
        {
            menu = owner;
            entryButton.onClick.AddListener(Open);
            for (int i = 0; i < rows.Length; i++)
            {
                int index = i;
                rows[i].onClick.AddListener(() => Select(index));
            }
            previous.onClick.AddListener(PreviousPage);
            next.onClick.AddListener(NextPage);
            back.onClick.AddListener(Close);
            panel.SetActive(false);
            if (ReplaySelection.ReturnToList) { ReplaySelection.ReturnToList = false; Open(); }
        }

        private void PreviousPage() { page--; Refresh(); }
        private void NextPage() { page++; Refresh(); }

        public void Open()
        {
            if (Mirror.NetworkClient.active || Mirror.NetworkServer.active) return;
            try
            {
                paths = Directory.Exists(ReplayStorage.DirectoryPath) ? Directory.GetFiles(ReplayStorage.DirectoryPath, "*.replay.json") : Array.Empty<string>();
                Array.Sort(paths, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { Debug.LogWarning("无法读取录像列表：" + e.Message); paths = Array.Empty<string>(); }
            page = 0;
            panel.SetActive(true);
            menu.SetReplayOpen(true);
            Refresh();
        }

        public void Close() { panel.SetActive(false); menu.SetReplayOpen(false); }

        private void Refresh()
        {
            int pages = Math.Max(1, (paths.Length + PageSize - 1) / PageSize);
            page = Mathf.Clamp(page, 0, pages - 1);
            title.text = paths.Length == 0 ? "录像列表（暂无录像）" : $"录像列表  {page + 1}/{pages}";
            previous.interactable = page > 0;
            next.interactable = page + 1 < pages;
            for (int i = 0; i < rows.Length; i++)
            {
                int index = page * PageSize + i;
                bool exists = index < paths.Length;
                rows[i].gameObject.SetActive(exists);
                if (!exists) continue;
                bool valid = ReplayStorage.TryLoad(paths[index], out var data, out var error);
                if (valid) valid = ReplayStorage.MatchesMap(data, menu.ReplayMap, out error);
                rows[i].interactable = valid;
                string recorded = valid && DateTime.TryParse(data.recordedUtc, out var time) ? time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "";
                rows[i].GetComponentInChildren<TMP_Text>().text = valid
                    ? $"{recorded}   {(data.networkMatch ? "局域网" : "单机")}"
                    : Path.GetFileName(paths[index]) + "  " + error;
            }
            GamepadNavigation.Configure(panel);
        }

        private void Select(int row)
        {
            int index = page * PageSize + row;
            if (index < 0 || index >= paths.Length || !ReplayStorage.TryLoad(paths[index], out var data, out _) || !ReplayStorage.MatchesMap(data, menu.ReplayMap, out _)) return;
            ReplaySelection.Pending = data;
            SceneManager.LoadScene("ArenaScene");
        }
    }
}
