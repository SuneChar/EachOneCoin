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
    private GameObject panel;
    private TMP_Text status;
    private TMP_Text seats;
    private Button hostButton;
    private Button joinButton;
    private Button fillButton;
    private Button startButton;
    private Button reconnectButton;
    private TMP_Text fillLabel;
    private InputField address;

    public void Initialize(BootMenu owner, GameObject startPanel)
    {
        menu = owner;
        var template = startPanel.transform.Find("StartButton").GetComponent<Button>();
        var open = Instantiate(template, template.transform.parent);
        open.name = "LanRoomButton";
        open.GetComponent<RectTransform>().anchoredPosition += new Vector2(300, 0);
        open.GetComponentInChildren<TMP_Text>().text = "局域网联机";
        open.onClick = new Button.ButtonClickedEvent();
        open.onClick.AddListener(Open);

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

        panel = new GameObject("LanRoomPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(startPanel.transform.parent, false);
        var root = panel.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.02f, 0.05f, 0.09f, 0.96f);
        var textTemplate = template.GetComponentInChildren<TMP_Text>();
        Label(textTemplate, "局域网房间（Windows）", 0, 300, 700, 60, 36);
        status = Label(textTemplate, "未连接", 0, 240, 900, 45, 24);
        seats = Label(textTemplate, "", 0, 90, 700, 190, 26);
        hostButton = Button(template, "创建房间", -400, -60, () => network.HostRoom());
        joinButton = Button(template, "加入房间", 400, -60, () => network.JoinRoom(address.text));
        fillButton = Button(template, "", 0, -160, () => network.SetNpcFill(!network.Snapshot.fillNpcs));
        fillLabel = fillButton.GetComponentInChildren<TMP_Text>();
        startButton = Button(template, "开始移动测试", -160, -250, () => network.StartMovementTest());
        Button(template, "退出 / 返回", 160, -250, Close);
        reconnectButton = Button(template, "恢复上局", 0, -330, () => network.Reconnect());
        Label(textTemplate, "房主 IP：", -150, -60, 150, 45, 22);
        address = CreateAddressField(panel.transform);
        panel.SetActive(false);
        if (network.HasRecoverySession && !NetworkClient.active && !NetworkServer.active) Open();
    }

    private TMP_Text Label(TMP_Text template, string value, float x, float y, float width, float height, float size)
    {
        var label = Instantiate(template, panel.transform);
        label.name = "RoomLabel";
        label.text = value; label.fontSize = size; label.enableAutoSizing = false;
        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
        return label;
    }

    private Button Button(Button template, string title, float x, float y, UnityEngine.Events.UnityAction action)
    {
        var button = Instantiate(template, panel.transform);
        button.name = title;
        button.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
        button.GetComponentInChildren<TMP_Text>().text = title;
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
        return button;
    }

    private static InputField CreateAddressField(Transform parent)
    {
        var obj = new GameObject("Address", typeof(RectTransform), typeof(Image), typeof(InputField));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(20, -60); rect.sizeDelta = new Vector2(200, 55);
        obj.GetComponent<Image>().color = Color.white;
        var child = new GameObject("Text", typeof(RectTransform), typeof(Text));
        child.transform.SetParent(obj.transform, false);
        var textRect = child.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8, 2); textRect.offsetMax = new Vector2(-8, -2);
        var text = child.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 22;
        text.color = Color.black; text.alignment = TextAnchor.MiddleLeft;
        var field = obj.GetComponent<InputField>();
        field.textComponent = text; field.text = "127.0.0.1"; field.characterLimit = 253;
        return field;
    }

    private void Open()
    {
        menu.SetLanRoomOpen(true);
        panel.SetActive(true);
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.sendNavigationEvents)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(hostButton.gameObject);
    }
    public void Close() { network.LeaveRoom(); panel.SetActive(false); menu.SetLanRoomOpen(false); }

    private void Update()
    {
        if (panel == null || !panel.activeSelf) return;
        status.text = network.Status;
        bool active = NetworkClient.active || NetworkServer.active;
        hostButton.interactable = !active; joinButton.interactable = !active; address.interactable = !active;
        reconnectButton.interactable = !active && network.HasRecoverySession;
        fillButton.interactable = NetworkServer.active && network.HasSnapshot && !network.MatchStarted;
        startButton.interactable = NetworkServer.active && network.HasSnapshot && !network.MatchStarted;
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
