using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using EachOneCoin.Gameplay;

public class BootMenu : MonoBehaviour
{
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject quitConfirmPanel;
    [SerializeField] private Button quitButton;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeValueText;
    [SerializeField] private Toggle promptsToggle;
    [SerializeField] private Toggle joystickModeToggle;
    [SerializeField] private ControlPromptImages promptImages;
    [SerializeField] private GameAudio gameAudio;
    private InputDeviceTracker inputDevice;
    private bool usingGamepad;
    private bool showPrompts;
    private bool settingsOpen;
    private bool quitOpen;
    private bool lanRoomOpen;
    private bool wardrobeOpen;
    private bool replayOpen;
    [SerializeField] private EachOneCoin.Gameplay.Replay.ReplayBrowser replayBrowser;
    private TMP_Text joystickModeLabel;
    [SerializeField] private LanRoomUI lanRoom;
    [SerializeField] private WardrobeUI wardrobe;
    [SerializeField] private Button wardrobeButton;
    public bool UsingGamepad => usingGamepad;
    [SerializeField] private EachOneCoin.Gameplay.ArenaMapCatalog mapCatalog;
    [SerializeField] private EachOneCoin.Gameplay.ArenaMapDefinition replayMap;
    public EachOneCoin.Gameplay.ArenaMapDefinition ReplayMap => replayMap;
    private EachOneCoin.Gameplay.ArenaMapDefinition[] singlePlayerMaps => mapCatalog != null ? mapCatalog.Maps : null;
    [SerializeField] private TMP_Text mapNameText;
    [SerializeField] private Button previousMapButton;
    [SerializeField] private Button nextMapButton;
    private int selectedMap;

    private void Awake()
    {
        showPrompts = PlayerPrefs.GetInt("GameUI.ShowPrompts", 1) != 0;
        promptsToggle.SetIsOnWithoutNotify(showPrompts);
        InitializeJoystickModeOption();
        inputDevice = GetComponent<InputDeviceTracker>();
        usingGamepad = inputDevice.UsingGamepad;
        inputDevice.DeviceChanged += OnDeviceChanged;
        if (wardrobe == null || wardrobeButton == null)
            Debug.LogWarning("[BootMenu] 换装系统组件或入口按钮尚未绑定，请在 Unity 菜单执行 EachOneCoin/Setup/Apply All 4 Tasks Assets (1+4+2+3) 完成资产装配。");
        if (wardrobeButton != null) wardrobeButton.onClick.AddListener(OpenWardrobe);
        if (wardrobe != null) wardrobe.Initialize(this, lanRoom);
        if (lanRoom != null) lanRoom.Initialize(this);
        if (previousMapButton != null) previousMapButton.onClick.AddListener(PreviousMap);
        if (nextMapButton != null) nextMapButton.onClick.AddListener(NextMap);
        RefreshMap();
        Refresh();
        if (replayBrowser != null) replayBrowser.Initialize(this);
    }

    private void OnDestroy()
    {
        if (inputDevice != null) inputDevice.DeviceChanged -= OnDeviceChanged;
        if (wardrobeButton != null) wardrobeButton.onClick.RemoveListener(OpenWardrobe);
        if (previousMapButton != null) previousMapButton.onClick.RemoveListener(PreviousMap);
        if (nextMapButton != null) nextMapButton.onClick.RemoveListener(NextMap);
    }

    // The row is authored in the settings panel; only its initial state and label are handled here.
    private void InitializeJoystickModeOption()
    {
        if (joystickModeToggle == null) return;
        joystickModeToggle.gameObject.SetActive(TouchSettings.TouchControlsAvailable);
        joystickModeLabel = joystickModeToggle.GetComponentInChildren<TMP_Text>(true);
        joystickModeToggle.SetIsOnWithoutNotify(TouchSettings.JoystickMode == TouchJoystickMode.Floating);
        RefreshJoystickModeLabel();
    }

    public void OnJoystickModeChanged(bool floating)
    {
        TouchSettings.JoystickMode = floating ? TouchJoystickMode.Floating : TouchJoystickMode.Fixed;
        RefreshJoystickModeLabel();
    }

    private void RefreshJoystickModeLabel()
    {
        if (joystickModeLabel != null) joystickModeLabel.text = TouchSettings.JoystickModeLabel(TouchSettings.JoystickMode);
    }

    private void OnDeviceChanged(bool gamepad) { usingGamepad = gamepad; Refresh(); }
    private void Refresh()
    {
        startPanel.SetActive(!settingsOpen && !quitOpen && !lanRoomOpen && !wardrobeOpen && !replayOpen);
        settingsPanel.SetActive(settingsOpen);
        quitConfirmPanel.SetActive(quitOpen);
        if (EventSystem.current != null)
        {
            EventSystem.current.sendNavigationEvents = usingGamepad && Application.isFocused;
            EventSystem.current.SetSelectedGameObject(null);
            var active = replayOpen && replayBrowser != null ? replayBrowser.Panel : (lanRoomOpen || wardrobeOpen) ? null : quitOpen ? quitConfirmPanel : settingsOpen ? settingsPanel : startPanel;
            GamepadNavigation.Configure(active);
            GamepadNavigation.EnsureFocus(active, usingGamepad);
        }
        if (promptImages != null) promptImages.Refresh(showPrompts, usingGamepad, false, settingsOpen || quitOpen || wardrobeOpen);
    }
    private void LateUpdate()
    {
        if (lanRoomOpen || wardrobeOpen) return;
        GamepadNavigation.EnsureFocus(replayOpen && replayBrowser != null ? replayBrowser.Panel : quitOpen ? quitConfirmPanel : settingsOpen ? settingsPanel : startPanel, usingGamepad);
    }
    private void Update()
    {
        if (!Application.isFocused) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7) || Input.GetKeyDown(KeyCode.JoystickButton1))
        {
            if (replayOpen && replayBrowser != null) replayBrowser.Close();
            else if (wardrobeOpen) { if (wardrobe != null) wardrobe.Close(); }
            else if (lanRoomOpen) { if (lanRoom != null) lanRoom.Close(); }
            else if (quitOpen) CancelQuit();
            else if (settingsOpen) CloseSettings();
        }
    }
    public void SetReplayOpen(bool open) { replayOpen = open; Refresh(); }
    public void SetLanRoomOpen(bool open) { lanRoomOpen = open; Refresh(); }
    public void OpenWardrobe()
    {
        if (settingsOpen || quitOpen || lanRoomOpen || replayOpen || wardrobe == null) return;
        wardrobe.Open(false);
    }
    public void SetWardrobeOpen(bool open)
    {
        wardrobeOpen = open;
        Refresh();
        if (!open && !lanRoomOpen && !settingsOpen && !quitOpen && !replayOpen && usingGamepad && wardrobeButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(wardrobeButton.gameObject);
    }
    public void StartGame()
    {
        var selected = singlePlayerMaps != null && selectedMap < singlePlayerMaps.Length ? singlePlayerMaps[selectedMap] : null;
        if (selected != null && !EachOneCoin.Gameplay.ArenaMapValidator.TryValidate(selected, out string error))
        {
            if (mapNameText != null) mapNameText.text = "地图无效";
            Debug.LogWarning("无法开始：" + error, selected);
            return;
        }
        EachOneCoin.Gameplay.ArenaMapSelection.Selected = selected;
        SceneManager.LoadScene("ArenaScene");
    }
    public void PreviousMap() { ChangeMap(-1); }
    public void NextMap() { ChangeMap(1); }
    private void ChangeMap(int step)
    {
        if (singlePlayerMaps == null || singlePlayerMaps.Length == 0) return;
        selectedMap = (selectedMap + step + singlePlayerMaps.Length) % singlePlayerMaps.Length;
        RefreshMap();
    }
    private void RefreshMap()
    {
        int count = singlePlayerMaps != null ? singlePlayerMaps.Length : 0;
        if (selectedMap >= count) selectedMap = 0;
        var selected = count > 0 ? singlePlayerMaps[selectedMap] : null;
        if (mapNameText != null) mapNameText.text = selected != null ? selected.DisplayName : "默认竞技场";
        if (previousMapButton != null) previousMapButton.interactable = count > 1;
        if (nextMapButton != null) nextMapButton.interactable = count > 1;
    }
    public void OpenSettings()
    {
        if (quitOpen || wardrobeOpen || lanRoomOpen || replayOpen) return;
        settingsOpen = true;
        volumeSlider.SetValueWithoutNotify(gameAudio.Volume);
        UpdateVolumeLabel(gameAudio.Volume);
        Refresh();
    }
    public void CloseSettings() { settingsOpen = false; Refresh(); }
    public void SetVolume(float value) { gameAudio.SetVolume(value); UpdateVolumeLabel(gameAudio.Volume); }
    private void UpdateVolumeLabel(float value) { volumeValueText.text = $"{Mathf.RoundToInt(value * 100f)}%"; }
    public void SetPromptsVisible(bool visible)
    {
        showPrompts = visible;
        PlayerPrefs.SetInt("GameUI.ShowPrompts", visible ? 1 : 0);
        PlayerPrefs.Save();
        Refresh();
    }
    public void OpenQuitConfirmation() { if (!settingsOpen && !wardrobeOpen && !lanRoomOpen && !replayOpen) { quitOpen = true; Refresh(); } }
    public void CancelQuit()
    {
        quitOpen = false;
        Refresh();
        if (usingGamepad && quitButton != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(quitButton.gameObject);
    }
    public void ConfirmQuit()
    {
        if (!quitOpen) return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
