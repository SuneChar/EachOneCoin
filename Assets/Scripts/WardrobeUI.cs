using System;
using EachOneCoin.Gameplay;
using EachOneCoin.Networking;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class WardrobeUI : MonoBehaviour
{
    private static readonly Color[] FactionPreviewColors =
    {
        new Color(0.94f, 0.27f, 0.28f, 1f),
        new Color(0.25f, 0.51f, 0.95f, 1f),
        new Color(0.26f, 0.79f, 0.43f, 1f),
        new Color(0.94f, 0.70f, 0.19f, 1f)
    };

    private static readonly string[] FallbackIds = { "slot_0", "slot_1", "slot_2", "slot_3", "none" };
    private static readonly string[] FallbackDisplayNames = { "运动帽围巾", "遮阳帽", "运动耳机", "荣誉绶带", "无配饰" };
    private static readonly string[] FallbackSlotNames = { "Slot 0", "Slot 1", "Slot 2", "Slot 3", "" };

    private static readonly Color UnselectedButtonColor = new Color(0.19f, 0.46f, 0.62f, 1f);
    private static readonly Color SelectedButtonColor = new Color(0.26f, 0.65f, 0.78f, 1f);
    private static readonly Color CircleBackdropBase = new Color(0.102f, 0.129f, 0.184f, 0.94f);
    private static readonly Color CircleFrameBase = new Color(0.95f, 0.97f, 1f, 1f);

    [SerializeField] private GameObject panel;
    [SerializeField] private CharacterAppearanceCatalog catalog;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text outfitNameText;
    [SerializeField] private TMP_Text indexIndicatorText;
    [SerializeField] private Image circleBackdropImage;
    [SerializeField] private Image circleFrameImage;
    [SerializeField] private Image bodyPreviewImage;
    [SerializeField] private RectTransform accessoryPreviewRoot;
    [SerializeField] private Image previewSportScarf;
    [SerializeField] private Image previewSportCap;
    [SerializeField] private Image previewSportVisor;
    [SerializeField] private Image previewHeadset;
    [SerializeField] private Image previewMedalSash;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button[] presetButtons;
    [SerializeField] private TMP_Text[] presetLabels;
    [SerializeField] private Image[] presetBackgrounds;
    [SerializeField] private Image[] presetIcons;
    [SerializeField] private Button[] colorPreviewButtons;
    [SerializeField] private Image[] colorPreviewRings;
    [SerializeField] private Button backButton;

    private BootMenu menu;
    private LanRoomUI lanRoom;
    private bool listenersBound;
    private bool returnToLanRoom;
    private int selectedIndex;
    private int previewColorIndex;
    private UnityAction[] presetCallbacks;
    private UnityAction[] colorCallbacks;

    public int SelectedIndex => selectedIndex;
    public int PreviewColorIndex => previewColorIndex;
    public bool IsOpen => panel != null && panel.activeSelf;

    private int OutfitCount => catalog != null && catalog.Count > 0 ? catalog.Count : FallbackIds.Length;
    private bool UsingGamepad => menu != null ? menu.UsingGamepad : (InputDeviceTracker.Instance != null && InputDeviceTracker.Instance.UsingGamepad);

    public void Initialize(BootMenu owner, LanRoomUI roomUi)
    {
        menu = owner;
        lanRoom = roomUi;
        BindListeners();
        selectedIndex = ResolveSavedIndex();
        if (panel != null) panel.SetActive(false);
        RefreshPreview();
    }

    private void OnDestroy()
    {
        UnbindListeners();
    }

    public void Open(bool fromLanRoom = false)
    {
        if (panel == null) return;
        BindListeners();
        returnToLanRoom = fromLanRoom;

        var network = NetworkManager.singleton as LanNetworkManager;
        if (fromLanRoom && network != null && network.HasSnapshot)
        {
            int ownSlot = network.Snapshot.ownSlot;
            if (ownSlot >= 0 && ownSlot < FactionPreviewColors.Length)
                previewColorIndex = ownSlot;
            byte netAppearance = network.Snapshot.AppearanceAt(ownSlot);
            if (netAppearance != RoomState.UnspecifiedAppearance && netAppearance < OutfitCount)
                selectedIndex = netAppearance;
            else
                selectedIndex = ResolveSavedIndex();
        }
        else
        {
            selectedIndex = ResolveSavedIndex();
        }

        if (menu != null) menu.SetWardrobeOpen(true);
        panel.SetActive(true);
        RefreshPreview();
        GamepadNavigation.Configure(panel);
        FocusCurrentSelection();
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
        bool backToLan = returnToLanRoom;
        returnToLanRoom = false;
        if (menu != null) menu.SetWardrobeOpen(false);
        if (backToLan && lanRoom != null) lanRoom.ReturnFromWardrobe();
    }

    public void SelectAppearance(int index)
    {
        int count = OutfitCount;
        if (count <= 0) return;

        int normalized = ((index % count) + count) % count;
        selectedIndex = normalized;

        string appearanceId = ResolveAppearanceId(normalized);
        CosmeticPreferenceStore.SaveSelectedAppearanceId(appearanceId);

        var network = NetworkManager.singleton as LanNetworkManager;
        if (network != null)
        {
            network.ConfigureAppearanceCatalog(count);
            network.SetLocalPreferredAppearance((byte)normalized);
        }

        RefreshPreview();
    }

    public void SelectPrevious()
    {
        SelectAppearance(selectedIndex - 1);
    }

    public void SelectNext()
    {
        SelectAppearance(selectedIndex + 1);
    }

    public void SelectPreviewColor(int colorIndex)
    {
        if (colorIndex < 0 || colorIndex >= FactionPreviewColors.Length) return;
        previewColorIndex = colorIndex;
        RefreshPreview();
    }

    public void RefreshPreview()
    {
        int count = OutfitCount;
        if (count <= 0) return;
        if (selectedIndex < 0 || selectedIndex >= count) selectedIndex = 0;

        Color factionColor = FactionPreviewColors[Mathf.Clamp(previewColorIndex, 0, FactionPreviewColors.Length - 1)];
        if (bodyPreviewImage != null)
            bodyPreviewImage.color = factionColor;

        if (circleBackdropImage != null)
        {
            Color mutedFaction = new Color(factionColor.r * 0.34f, factionColor.g * 0.34f, factionColor.b * 0.38f, CircleBackdropBase.a);
            Color bg = Color.Lerp(CircleBackdropBase, mutedFaction, 0.24f);
            bg.a = CircleBackdropBase.a;
            circleBackdropImage.color = bg;
        }

        if (circleFrameImage != null)
        {
            Color frame = Color.Lerp(CircleFrameBase, factionColor, 0.45f);
            frame.a = CircleFrameBase.a;
            circleFrameImage.color = frame;
        }

        string slotName = ResolveAccessorySlotName(selectedIndex);
        UpdateAccessoryLayers(slotName);

        if (outfitNameText != null)
            outfitNameText.text = ResolveDisplayName(selectedIndex);

        if (indexIndicatorText != null)
            indexIndicatorText.text = (selectedIndex + 1) + " / " + count;

        if (presetButtons != null)
        {
            for (int i = 0; i < presetButtons.Length; i++)
            {
                bool active = i < count;
                Button btn = presetButtons[i];
                if (btn != null && btn.gameObject.activeSelf != active)
                    btn.gameObject.SetActive(active);

                if (!active) continue;

                bool isSelected = i == selectedIndex;
                if (presetBackgrounds != null && i < presetBackgrounds.Length && presetBackgrounds[i] != null)
                    presetBackgrounds[i].color = isSelected ? SelectedButtonColor : UnselectedButtonColor;

                if (presetLabels != null && i < presetLabels.Length && presetLabels[i] != null)
                {
                    string name = ResolveDisplayName(i);
                    presetLabels[i].text = isSelected ? name + "（当前）" : name;
                }

                if (presetIcons != null && i < presetIcons.Length && presetIcons[i] != null)
                {
                    Sprite icon = ResolvePreviewIcon(i);
                    presetIcons[i].sprite = icon;
                    presetIcons[i].enabled = icon != null;
                }
            }
        }

        if (colorPreviewRings != null)
        {
            for (int i = 0; i < colorPreviewRings.Length; i++)
            {
                if (colorPreviewRings[i] != null)
                    colorPreviewRings[i].enabled = i == previewColorIndex;
            }
        }
    }

    private void LateUpdate()
    {
        if (panel != null && panel.activeInHierarchy)
            GamepadNavigation.EnsureFocus(panel, UsingGamepad);
    }

    private void UpdateAccessoryLayers(string slotName)
    {
        bool empty = string.IsNullOrWhiteSpace(slotName);
        bool slot0 = !empty && string.Equals(slotName, "Slot 0", StringComparison.Ordinal);
        bool slot1 = !empty && string.Equals(slotName, "Slot 1", StringComparison.Ordinal);
        bool slot2 = !empty && string.Equals(slotName, "Slot 2", StringComparison.Ordinal);
        bool slot3 = !empty && string.Equals(slotName, "Slot 3", StringComparison.Ordinal);

        SetImageActive(previewSportScarf, slot0);
        SetImageActive(previewSportCap, slot0);
        SetImageActive(previewSportVisor, slot1);
        SetImageActive(previewHeadset, slot2);
        SetImageActive(previewMedalSash, slot3);
    }

    private static void SetImageActive(Image image, bool active)
    {
        if (image == null) return;
        if (image.gameObject.activeSelf != active)
            image.gameObject.SetActive(active);
    }

    private void FocusCurrentSelection()
    {
        if (!UsingGamepad || EventSystem.current == null) return;
        if (presetButtons != null && selectedIndex >= 0 && selectedIndex < presetButtons.Length)
        {
            Button btn = presetButtons[selectedIndex];
            if (btn != null && btn.gameObject.activeInHierarchy && btn.IsInteractable())
            {
                EventSystem.current.SetSelectedGameObject(btn.gameObject);
                return;
            }
        }
        GamepadNavigation.EnsureFocus(panel, true);
    }

    private int ResolveSavedIndex()
    {
        string savedId = CosmeticPreferenceStore.LoadSelectedAppearanceId();
        if (catalog != null && catalog.Count > 0)
            return catalog.ResolveIndexOrDefault(savedId, 0);

        for (int i = 0; i < FallbackIds.Length; i++)
        {
            if (string.Equals(FallbackIds[i], savedId, StringComparison.Ordinal))
                return i;
        }
        return 0;
    }

    private string ResolveAppearanceId(int index)
    {
        if (catalog != null && catalog.Count > 0)
        {
            string id = catalog.GetId(index);
            if (!string.IsNullOrWhiteSpace(id)) return id;
        }
        return index >= 0 && index < FallbackIds.Length ? FallbackIds[index] : CosmeticPreferenceStore.DefaultAppearanceId;
    }

    private string ResolveDisplayName(int index)
    {
        if (catalog != null && catalog.Count > 0)
            return catalog.GetDisplayName(index);
        return index >= 0 && index < FallbackDisplayNames.Length ? FallbackDisplayNames[index] : "装扮 " + (index + 1);
    }

    private string ResolveAccessorySlotName(int index)
    {
        if (catalog != null && catalog.Count > 0)
        {
            CharacterAppearanceProfile profile = catalog.GetProfile(index);
            if (profile != null)
                return profile.IsEmptyOutfit ? string.Empty : profile.AccessorySlotName;
        }
        return index >= 0 && index < FallbackSlotNames.Length ? FallbackSlotNames[index] : string.Empty;
    }

    private Sprite ResolvePreviewIcon(int index)
    {
        if (catalog != null && catalog.Count > 0)
        {
            CharacterAppearanceProfile profile = catalog.GetProfile(index);
            if (profile != null) return profile.PreviewIcon;
        }
        return null;
    }

    private void BindListeners()
    {
        if (listenersBound) return;
        listenersBound = true;

        if (prevButton != null) prevButton.onClick.AddListener(SelectPrevious);
        if (nextButton != null) nextButton.onClick.AddListener(SelectNext);
        if (backButton != null) backButton.onClick.AddListener(Close);

        if (presetButtons != null)
        {
            presetCallbacks = new UnityAction[presetButtons.Length];
            for (int i = 0; i < presetButtons.Length; i++)
            {
                Button btn = presetButtons[i];
                if (btn == null) continue;
                int capturedIndex = i;
                UnityAction callback = () => SelectAppearance(capturedIndex);
                presetCallbacks[i] = callback;
                btn.onClick.AddListener(callback);
            }
        }

        if (colorPreviewButtons != null)
        {
            colorCallbacks = new UnityAction[colorPreviewButtons.Length];
            for (int i = 0; i < colorPreviewButtons.Length; i++)
            {
                Button btn = colorPreviewButtons[i];
                if (btn == null) continue;
                int capturedColor = i;
                UnityAction callback = () => SelectPreviewColor(capturedColor);
                colorCallbacks[i] = callback;
                btn.onClick.AddListener(callback);
            }
        }
    }

    private void UnbindListeners()
    {
        if (!listenersBound) return;
        listenersBound = false;

        if (prevButton != null) prevButton.onClick.RemoveListener(SelectPrevious);
        if (nextButton != null) nextButton.onClick.RemoveListener(SelectNext);
        if (backButton != null) backButton.onClick.RemoveListener(Close);

        if (presetButtons != null && presetCallbacks != null)
        {
            int len = Mathf.Min(presetButtons.Length, presetCallbacks.Length);
            for (int i = 0; i < len; i++)
            {
                if (presetButtons[i] != null && presetCallbacks[i] != null)
                    presetButtons[i].onClick.RemoveListener(presetCallbacks[i]);
            }
        }
        presetCallbacks = null;

        if (colorPreviewButtons != null && colorCallbacks != null)
        {
            int len = Mathf.Min(colorPreviewButtons.Length, colorCallbacks.Length);
            for (int i = 0; i < len; i++)
            {
                if (colorPreviewButtons[i] != null && colorCallbacks[i] != null)
                    colorPreviewButtons[i].onClick.RemoveListener(colorCallbacks[i]);
            }
        }
        colorCallbacks = null;
    }
}
