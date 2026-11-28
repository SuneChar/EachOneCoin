using EachOneCoin.Networking;
using kcp2k;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LanRoomUI : MonoBehaviour
{
    private BootMenu menu;
    private LanNetworkManager network;
    private int lastNavigationMask = -1;
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

        panel.SetActive(false);
        if ((network.HasRecoverySession && !NetworkClient.active && !NetworkServer.active) || (network.HasSnapshot && NetworkClient.isConnected)) Open();
    }

    public void Host() => network.HostRoom();
    public void Join() => network.JoinRoom(address.text);
    public void ToggleNpcFill() => network.SetNpcFill(!network.Snapshot.fillNpcs);
    public void StartMatch() => network.StartMovementTest();
    public void Reconnect() => network.Reconnect();

    public void Open()
    {
        menu.SetLanRoomOpen(true);
        panel.SetActive(true);
        lastNavigationMask = -1;
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
        int navigationMask = (hostButton.interactable ? 1 : 0) | (joinButton.interactable ? 2 : 0) | (fillButton.interactable ? 4 : 0) |
            (startButton.interactable ? 8 : 0) | (reconnectButton.interactable ? 16 : 0);
        if (lastNavigationMask != navigationMask) { lastNavigationMask = navigationMask; GamepadNavigation.Configure(panel); }
        fillLabel.text = network.HasSnapshot && network.Snapshot.fillNpcs ? "空位补 NPC：开" : "空位补 NPC：关";
        if (!network.HasSnapshot) { seats.text = "尚未进入房间"; return; }
        var snapshot = network.Snapshot;
        string value = "";
        for (int i = 0; i < RoomState.Capacity; i++)
        {
            bool occupied = (snapshot.occupiedMask & (1 << i)) != 0;
            value += "席位 " + (i + 1) + "：" + (occupied ? (i == 0 ? "房主" : "玩家") : (snapshot.fillNpcs ? "空位（将补 NPC）" : "空位"));
            if (snapshot.ownSlot == i) value += "（你）";
            value += "\n";
        }
        seats.text = value;
    }
}
