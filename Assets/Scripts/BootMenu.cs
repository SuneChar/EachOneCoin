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
    private bool lanRoomOpen;
    private bool wardrobeOpen;
    [SerializeField] private LanRoomUI lanRoom;
    [SerializeField] private WardrobeUI wardrobe;
    [SerializeField] private Button wardrobeButton;

    public bool UsingGamepad => usingGamepad;

    private void Awake()
    {
        showPrompts = PlayerPrefs.GetInt("GameUI.ShowPrompts", 1) != 0;
        promptsToggle.SetIsOnWithoutNotify(showPrompts);
        inputDevice = GetComponent<InputDeviceTracker>();
        usingGamepad = inputDevice.UsingGamepad;
        inputDevice.DeviceChanged += OnDeviceChanged;
        if (wardrobe == null || wardrobeButton == null)
            Debug.LogWarning("[BootMenu] 换装系统组件或入口按钮尚未绑定，请在 Unity 菜单执行 EachOneCoin/Setup/Apply All 4 Tasks Assets (1+4+2+3) 完成资产装配。");
        if (wardrobeButton != null) wardrobeButton.onClick.AddListener(OpenWardrobe);
        if (wardrobe != null) wardrobe.Initialize(this, lanRoom);
        if (lanRoom != null) lanRoom.Initialize(this);
        Refresh();
    }

    private void OnDestroy()
    {
        if (inputDevice != null) inputDevice.DeviceChanged -= OnDeviceChanged;
        if (wardrobeButton != null) wardrobeButton.onClick.RemoveListener(OpenWardrobe);
    }

    private void OnDeviceChanged(bool gamepad) { usingGamepad = gamepad; Refresh(); }
    private void Refresh()
    {
        startPanel.SetActive(!settingsOpen && !quitOpen && !lanRoomOpen && !wardrobeOpen);
        settingsPanel.SetActive(settingsOpen);
        quitConfirmPanel.SetActive(quitOpen);
        if (EventSystem.current != null)
        {
            EventSystem.current.sendNavigationEvents = usingGamepad && Application.isFocused;
            EventSystem.current.SetSelectedGameObject(null);
            var active = (lanRoomOpen || wardrobeOpen) ? null : quitOpen ? quitConfirmPanel : settingsOpen ? settingsPanel : startPanel;
            GamepadNavigation.Configure(active);
            GamepadNavigation.EnsureFocus(active, usingGamepad);
        }
        if (promptImages != null) promptImages.Refresh(showPrompts, usingGamepad, false, settingsOpen || quitOpen || wardrobeOpen);
    }
    private void LateUpdate()
    {
        if (lanRoomOpen || wardrobeOpen) return;
        GamepadNavigation.EnsureFocus(quitOpen ? quitConfirmPanel : settingsOpen ? settingsPanel : startPanel, usingGamepad);
    }
    private void Update()
    {
        if (!Application.isFocused) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7) || Input.GetKeyDown(KeyCode.JoystickButton1))
        {
            if (wardrobeOpen) { if (wardrobe != null) wardrobe.Close(); }
            else if (lanRoomOpen) { if (lanRoom != null) lanRoom.Close(); }
            else if (quitOpen) CancelQuit();
            else if (settingsOpen) CloseSettings();
        }
    }
    public void SetLanRoomOpen(bool open) { lanRoomOpen = open; Refresh(); }
    public void OpenWardrobe()
    {
        if (settingsOpen || quitOpen || lanRoomOpen || wardrobe == null) return;
        wardrobe.Open(false);
    }
    public void SetWardrobeOpen(bool open)
    {
        wardrobeOpen = open;
        Refresh();
        if (!open && !lanRoomOpen && !settingsOpen && !quitOpen && usingGamepad && wardrobeButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(wardrobeButton.gameObject);
    }
    public void StartGame() { SceneManager.LoadScene("ArenaScene"); }
    public void OpenSettings()
    {
        if (quitOpen || wardrobeOpen || lanRoomOpen) return;
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
    public void OpenQuitConfirmation() { if (!settingsOpen && !wardrobeOpen && !lanRoomOpen) { quitOpen = true; Refresh(); } }
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
