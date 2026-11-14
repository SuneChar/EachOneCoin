using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BootMenu : MonoBehaviour
{
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject quitConfirmPanel;
    [SerializeField] private Button quitButton;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeValueText;
    [SerializeField] private Toggle promptsToggle;
    [SerializeField] private ControlPromptImages promptImages;
    [SerializeField] private GameAudio gameAudio;
    private InputDeviceTracker inputDevice;
    private bool usingGamepad;
    private bool showPrompts;
    private bool settingsOpen;
    private bool quitOpen;

    private void Awake()
    {
        showPrompts = PlayerPrefs.GetInt("GameUI.ShowPrompts", 1) != 0;
        promptsToggle.SetIsOnWithoutNotify(showPrompts);
        inputDevice = GetComponent<InputDeviceTracker>();
        usingGamepad = inputDevice.UsingGamepad;
        inputDevice.DeviceChanged += OnDeviceChanged;
        Refresh();
    }

    private void OnDestroy() { if (inputDevice != null) inputDevice.DeviceChanged -= OnDeviceChanged; }
    private void OnDeviceChanged(bool gamepad) { usingGamepad = gamepad; Refresh(); }
    private void Refresh()
    {
        startPanel.SetActive(!settingsOpen && !quitOpen);
        settingsPanel.SetActive(settingsOpen);
        quitConfirmPanel.SetActive(quitOpen);
        if (EventSystem.current != null)
        {
            EventSystem.current.sendNavigationEvents = usingGamepad;
            EventSystem.current.SetSelectedGameObject(null);
            var active = quitOpen ? quitConfirmPanel : settingsOpen ? settingsPanel : startPanel;
            var first = active.GetComponentInChildren<Selectable>();
            if (usingGamepad && first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
        if (promptImages != null) promptImages.Refresh(showPrompts, usingGamepad, false, settingsOpen || quitOpen);
    }
    private void LateUpdate()
    {
        if (!usingGamepad || EventSystem.current == null || EventSystem.current.currentSelectedGameObject != null) return;
        var first = (quitOpen ? quitConfirmPanel : settingsOpen ? settingsPanel : startPanel).GetComponentInChildren<Selectable>();
        if (first != null && first.IsInteractable()) EventSystem.current.SetSelectedGameObject(first.gameObject);
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7) || Input.GetKeyDown(KeyCode.JoystickButton1))
        {
            if (quitOpen) CancelQuit();
            else if (settingsOpen) CloseSettings();
        }
    }
    public void StartGame() { SceneManager.LoadScene("SampleScene"); }
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
