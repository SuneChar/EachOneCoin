using System;
using System.Text;
using EachOneCoin.Gameplay;
using EachOneCoin.Networking;
using kcp2k;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class LanRoomUI : MonoBehaviour
{
    private static readonly string[] FallbackAppearanceNames = { "运动帽围巾", "遮阳帽", "运动耳机", "荣誉绶带", "无配饰" };
    private static readonly string[] FallbackAppearanceIds = { "slot_0", "slot_1", "slot_2", "slot_3", "none" };

    private BootMenu menu;
    private LanNetworkManager network;
    private int lastNavigationMask = -1;
    private readonly StringBuilder seatsBuilder = new StringBuilder(192);
    private string lastSeatsText;

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text status;
    [SerializeField] private TMP_Text seats;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button fillButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button reconnectButton;
    [SerializeField] private TMP_Text fillLabel;
    [SerializeField] private TMP_InputField address;
    [SerializeField] private CharacterAppearanceCatalog appearanceCatalog;
    [SerializeField] private Button wardrobeButton;
    [SerializeField] private WardrobeUI wardrobe;
    [SerializeField] private LanQrUI qrUI;

    public void Initialize(BootMenu owner)
    {
        menu = owner;

        network = NetworkManager.singleton as LanNetworkManager;
        if (network == null)
        {
            var networkObject = new GameObject("LAN Network Manager");
            networkObject.SetActive(false);
            var transport = networkObject.AddComponent<KcpTransport>();
            network = networkObject.AddComponent<LanNetworkManager>();
            network.authenticator = networkObject.AddComponent<LanReconnectAuthenticator>();
            network.transport = transport;
            network.maxConnections = RoomState.Capacity * 2;
            network.autoCreatePlayer = false;
            network.dontDestroyOnLoad = true;
            networkObject.SetActive(true);
        }

        SyncLocalAppearanceToNetwork();

        if (wardrobeButton != null)
            wardrobeButton.onClick.AddListener(OpenWardrobe);
        if (qrUI != null) qrUI.Initialize(network);

        panel.SetActive(false);
        if ((network.HasRecoverySession && !NetworkClient.active && !NetworkServer.active) || (network.HasSnapshot && NetworkClient.isConnected)) Open();
    }

    private void OnDestroy()
    {
        if (wardrobeButton != null)
            wardrobeButton.onClick.RemoveListener(OpenWardrobe);
    }

    public void Host() => network.HostRoom();
    public void Join() => network.JoinRoom(address.text);
    public void ToggleNpcFill() => network.SetNpcFill(!network.Snapshot.fillNpcs);
    public void StartMatch() => network.StartMovementTest();
    public void Reconnect() => network.Reconnect();

    public void OpenWardrobe()
    {
        if (wardrobe == null || (network != null && network.MatchStarted)) return;
        if (panel != null) panel.SetActive(false);
        wardrobe.Open(true);
    }

    public void ReturnFromWardrobe()
    {
        if (panel == null) return;
        panel.SetActive(true);
        lastNavigationMask = -1;
        lastSeatsText = null;
        GamepadNavigation.Configure(panel);
        bool usingGamepad = InputDeviceTracker.Instance != null && InputDeviceTracker.Instance.UsingGamepad;
        if (usingGamepad && wardrobeButton != null && wardrobeButton.gameObject.activeInHierarchy && wardrobeButton.IsInteractable() && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(wardrobeButton.gameObject);
        else
            GamepadNavigation.EnsureFocus(panel, usingGamepad);
    }

    public void Open()
    {
        SyncLocalAppearanceToNetwork();
        menu.SetLanRoomOpen(true);
        panel.SetActive(true);
        lastNavigationMask = -1;
        lastSeatsText = null;
        GamepadNavigation.Configure(panel);
        GamepadNavigation.EnsureFocus(panel, InputDeviceTracker.Instance != null && InputDeviceTracker.Instance.UsingGamepad);
    }

    public void Close() { network.LeaveRoom(); panel.SetActive(false); menu.SetLanRoomOpen(false); }

    private void LateUpdate()
    {
        if (panel != null && panel.activeInHierarchy)
            GamepadNavigation.EnsureFocus(panel, InputDeviceTracker.Instance != null && InputDeviceTracker.Instance.UsingGamepad);
    }

    private void Update()
    {
        if (panel == null || !panel.activeSelf) return;
        status.text = network.Status;
        bool active = NetworkClient.active || NetworkServer.active;
        hostButton.interactable = !active; joinButton.interactable = !active; address.interactable = !active;
        reconnectButton.interactable = !active && network.HasRecoverySession;
        fillButton.interactable = NetworkServer.active && network.HasSnapshot && !network.MatchStarted;
        startButton.interactable = NetworkServer.active && network.HasSnapshot && !network.MatchStarted;
        if (wardrobeButton != null) wardrobeButton.interactable = wardrobe != null && !network.MatchStarted;

        int navigationMask = (hostButton.interactable ? 1 : 0) | (joinButton.interactable ? 2 : 0) | (fillButton.interactable ? 4 : 0) |
            (startButton.interactable ? 8 : 0) | (reconnectButton.interactable ? 16 : 0) |
            (wardrobeButton != null && wardrobeButton.interactable ? 32 : 0);
        if (lastNavigationMask != navigationMask) { lastNavigationMask = navigationMask; GamepadNavigation.Configure(panel); }

        fillLabel.text = network.HasSnapshot && network.Snapshot.fillNpcs ? "空位补 NPC：开" : "空位补 NPC：关";
        if (!network.HasSnapshot)
        {
            SetSeatsText("尚未进入房间");
            return;
        }

        var snapshot = network.Snapshot;
        seatsBuilder.Length = 0;
        for (int i = 0; i < RoomState.Capacity; i++)
        {
            bool occupied = (snapshot.occupiedMask & (1 << i)) != 0;
            seatsBuilder.Append("席位 ").Append(i + 1).Append("：");
            if (occupied)
            {
                seatsBuilder.Append(i == 0 ? "房主" : "玩家");
                byte appearanceIndex = snapshot.AppearanceAt(i);
                if (appearanceIndex != RoomState.UnspecifiedAppearance)
                    seatsBuilder.Append(" · ").Append(ResolveAppearanceName(appearanceIndex));
                if (snapshot.ownSlot == i)
                    seatsBuilder.Append("（你）");
            }
            else
            {
                seatsBuilder.Append(snapshot.fillNpcs ? "空位（将补 NPC）" : "空位");
            }
            seatsBuilder.Append('\n');
        }
        SetSeatsText(seatsBuilder.ToString());
    }

    private void SetSeatsText(string value)
    {
        if (string.Equals(lastSeatsText, value, StringComparison.Ordinal)) return;
        lastSeatsText = value;
        if (seats != null) seats.text = value;
    }

    private void SyncLocalAppearanceToNetwork()
    {
        if (network == null) return;
        int catalogCount = appearanceCatalog != null && appearanceCatalog.Count > 0
            ? appearanceCatalog.Count
            : RoomState.DefaultAppearanceCount;
        network.ConfigureAppearanceCatalog(catalogCount);

        string savedId = CosmeticPreferenceStore.LoadSelectedAppearanceId();
        int preferredIndex = appearanceCatalog != null && appearanceCatalog.Count > 0
            ? appearanceCatalog.ResolveIndexOrDefault(savedId, 0)
            : ResolveFallbackIndex(savedId);
        network.SetLocalPreferredAppearance((byte)preferredIndex);
    }

    private string ResolveAppearanceName(byte index)
    {
        if (appearanceCatalog != null && appearanceCatalog.Count > 0)
            return appearanceCatalog.GetDisplayName(index);
        return index < FallbackAppearanceNames.Length ? FallbackAppearanceNames[index] : "装扮 " + (index + 1);
    }

    private static int ResolveFallbackIndex(string appearanceId)
    {
        for (int i = 0; i < FallbackAppearanceIds.Length; i++)
        {
            if (string.Equals(FallbackAppearanceIds[i], appearanceId, StringComparison.Ordinal))
                return i;
        }
        return 0;
    }
}
