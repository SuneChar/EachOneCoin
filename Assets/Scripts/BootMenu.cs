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
    private TMP_Text joystickModeLabel;
    [SerializeField] private LanRoomUI lanRoom;

    private void Awake()
    {
        showPrompts = PlayerPrefs.GetInt("GameUI.ShowPrompts", 1) != 0;
        promptsToggle.SetIsOnWithoutNotify(showPrompts);
        InitializeJoystickModeOption();
        inputDevice = GetComponent<InputDeviceTracker>();
        usingGamepad = inputDevice.UsingGamepad;
        inputDevice.DeviceChanged += OnDeviceChanged;
        lanRoom.Initialize(this);
        Refresh();
    }

    private void OnDestroy() { if (inputDevice != null) inputDevice.DeviceChanged -= OnDeviceChanged; }

    // The row is authored in the settings panel; only its initial state and label are handled here.
    private void InitializeJoystickModeOption()
    {
        if (joystickModeToggle == null) return;
        joystickModeToggle.gameObject.SetActive(TouchSettings.TouchControlsAvailable);
        joystickModeLabel = joystickModeToggle.GetComponentInChildren<TMP_Text>(true);
        joystickModeToggle.SetIsOnWithoutNotify(TouchSettings.JoystickMode == TouchJoystickMode.Floating);
        RefreshJoystickModeLabel();
    }

    // Bound as a persistent scene listener, matching the prompts toggle next to it.
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
        startPanel.SetActive(!settingsOpen && !quitOpen && !lanRoomOpen);
        settingsPanel.SetActive(settingsOpen);
        quitConfirmPanel.SetActive(quitOpen);
        if (EventSystem.current != null)
        {
            EventSystem.current.sendNavigationEvents = usingGamepad && Application.isFocused;
            EventSystem.current.SetSelectedGameObject(null);
            var active = lanRoomOpen ? null : quitOpen ? quitConfirmPanel : settingsOpen ? settingsPanel : startPanel;
            GamepadNavigation.Configure(active);
            GamepadNavigation.EnsureFocus(active, usingGamepad);
        }
        if (promptImages != null) promptImages.Refresh(showPrompts, usingGamepad, false, settingsOpen || quitOpen);
    }
    private void LateUpdate()
    {
        if (lanRoomOpen) return;
        GamepadNavigation.EnsureFocus(quitOpen ? quitConfirmPanel : settingsOpen ? settingsPanel : startPanel, usingGamepad);
    }
    private void Update()
    {
        if (!Application.isFocused) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7) || Input.GetKeyDown(KeyCode.JoystickButton1))
        {
            if (lanRoomOpen) lanRoom.Close();
            else if (quitOpen) CancelQuit();
            else if (settingsOpen) CloseSettings();
        }
    }
    public void SetLanRoomOpen(bool open) { lanRoomOpen = open; Refresh(); }
    public void StartGame() { SceneManager.LoadScene("ArenaScene"); }
    public void OpenSettings()
    {
        if (quitOpen) return;
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
    public void OpenQuitConfirmation() { if (!settingsOpen) { quitOpen = true; Refresh(); } }
    public void CancelQuit()
    {
        quitOpen = false;
        Refresh();
        if (usingGamepad && quitButton != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(quitButton.gameObject);
    }
    public void ConfirmQuit() { if (quitOpen) Application.Quit(); }
}
