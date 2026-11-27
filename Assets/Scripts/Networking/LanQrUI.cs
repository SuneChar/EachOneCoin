using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using EachOneCoin.Networking;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

public sealed class LanQrUI : MonoBehaviour
{
    [SerializeField] private RawImage image;
    [SerializeField] private TMP_Text message;
    [SerializeField] private Button scanButton;
    [SerializeField] private Button addressButton;
    [SerializeField] private Button cameraButton;
    [SerializeField] private Button cancelButton;
    private LanNetworkManager network;
    private readonly List<IPAddress> addresses = new List<IPAddress>();
    private WebCamTexture cameraTexture;
    private Texture2D qrTexture;
    private Coroutine scanner;
    private int addressIndex;
    private int cameraIndex;
    private bool displayingHost;
    private bool selectedCamera;
    private bool requestingPermission;
    private readonly BarcodeReaderGeneric reader = new BarcodeReaderGeneric { AutoRotate = true, Options = new DecodingOptions { PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE }, TryHarder = true } };

    public void Initialize(LanNetworkManager manager) { network = manager; }
    private void OnDisable() { StopScan(); ClearQr(); displayingHost = false; }
    private void Update()
    {
        if (network == null) return;
        bool hosting = NetworkServer.active && network.HasSnapshot && !network.MatchStarted;
        bool idle = !NetworkClient.active && !NetworkServer.active;
        if (hosting && !displayingHost) { displayingHost = true; RefreshAddresses(); }
        if (!hosting && displayingHost) { displayingHost = false; ClearQr(); message.text = ""; }
        scanButton.gameObject.SetActive(idle && scanner == null);
        scanButton.interactable = idle && scanner == null;
        addressButton.gameObject.SetActive(hosting);
        cameraButton.gameObject.SetActive(scanner != null && WebCamTexture.devices.Length > 1);
        cancelButton.gameObject.SetActive(scanner != null);
    }
    public void NextAddress() { if (addresses.Count == 0) { RefreshAddresses(); return; } addressIndex = (addressIndex + 1) % addresses.Count; ShowQr(); }
    private void RefreshAddresses()
    {
        addresses.Clear(); addressIndex = 0;
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                if (nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    foreach (var item in nic.GetIPProperties().UnicastAddresses)
                    {
                        var ip = item.Address;
                        if (ip.AddressFamily == AddressFamily.InterNetwork && !addresses.Contains(ip))
                            try { LanInvitation.Encode(ip, 1); addresses.Add(ip); } catch (ArgumentException) { }
                    }
        }
        catch (Exception e) { Debug.LogWarning("无法枚举网络地址：" + e.GetType().Name); }
        addresses.Sort((a, b) => AddressRank(a).CompareTo(AddressRank(b)));
        ShowQr();
    }
    private static int AddressRank(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return bytes[0] == 10 || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || (bytes[0] == 192 && bytes[1] == 168) ? 0 : 1;
    }
    private void ShowQr()
    {
        ClearQr();
        if (addresses.Count == 0) { message.text = "未找到可用 IPv4 地址"; return; }
        var kcp = network.transport as kcp2k.KcpTransport;
        if (kcp == null || kcp.port == 0) { message.text = "无法获取房间端口"; return; }
        string payload = LanInvitation.Encode(addresses[addressIndex], kcp.port);
        var matrix = new QRCodeWriter().encode(payload, BarcodeFormat.QR_CODE, 256, 256);
        var pixels = new Color32[matrix.Width * matrix.Height];
        for (int y = 0; y < matrix.Height; y++) for (int x = 0; x < matrix.Width; x++)
            pixels[(matrix.Height - 1 - y) * matrix.Width + x] = matrix[x, y] ? new Color32(0, 0, 0, 255) : new Color32(255, 255, 255, 255);
        qrTexture = new Texture2D(matrix.Width, matrix.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        qrTexture.SetPixels32(pixels); qrTexture.Apply(); image.texture = qrTexture; image.gameObject.SetActive(true);
        message.text = addresses[addressIndex] + ":" + kcp.port + "（" + (addressIndex + 1) + "/" + addresses.Count + "）";
    }
    private void ClearQr() { if (qrTexture != null) Destroy(qrTexture); qrTexture = null; if (image != null) { image.texture = null; image.gameObject.SetActive(false); } }
    public void BeginScan() { if (scanner == null && !NetworkClient.active && !NetworkServer.active) scanner = StartCoroutine(Scan()); }
    public void CancelScan() { StopScan(); message.text = "已取消扫码"; }
    public void NextCamera() { cameraIndex++; selectedCamera = true; StopScan(); scanner = StartCoroutine(Scan()); }
    private IEnumerator Scan()
    {
        message.text = "正在请求摄像头权限…";
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
        {
            bool completed = false;
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += _ => completed = true;
            callbacks.PermissionDenied += _ => completed = true;
            callbacks.PermissionDeniedAndDontAskAgain += _ => completed = true;
            requestingPermission = true;
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera, callbacks);
            while (!completed) yield return null;
            requestingPermission = false;
        }
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera)) { message.text = "摄像头权限被拒绝，请在系统设置中允许"; scanner = null; yield break; }
#elif UNITY_IOS || UNITY_WEBGL
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam)) { message.text = "摄像头权限被拒绝，请在系统设置中允许"; scanner = null; yield break; }
#else
        yield return null;
#endif
        var devices = WebCamTexture.devices;
        if (devices.Length == 0) { message.text = "未找到摄像头"; scanner = null; yield break; }
        if (!selectedCamera) { for (int i = 0; i < devices.Length; i++) if (!devices[i].isFrontFacing) { cameraIndex = i; break; } selectedCamera = true; }
        cameraIndex %= devices.Length;
        cameraTexture = new WebCamTexture(devices[cameraIndex].name, 640, 480, 15);
        image.texture = cameraTexture; image.gameObject.SetActive(true); cameraTexture.Play();
        message.text = "请将二维码对准摄像头";
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!cameraTexture.didUpdateThisFrame && Time.realtimeSinceStartup < deadline) yield return null;
        if (!cameraTexture.didUpdateThisFrame) { message.text = "摄像头启动失败"; scanner = null; StopScan(); yield break; }
        while (cameraTexture != null)
        {
            if (cameraTexture.didUpdateThisFrame && cameraTexture.width > 16 && cameraTexture.height > 16)
            {
                image.rectTransform.localEulerAngles = new Vector3(0, 0, -cameraTexture.videoRotationAngle);
                image.rectTransform.localScale = new Vector3(1, cameraTexture.videoVerticallyMirrored ? -1 : 1, 1);
                var pixels = cameraTexture.GetPixels32();
                var bytes = new byte[pixels.Length * 4];
                for (int i = 0; i < pixels.Length; i++) { int n = i * 4; bytes[n] = pixels[i].r; bytes[n + 1] = pixels[i].g; bytes[n + 2] = pixels[i].b; bytes[n + 3] = pixels[i].a; }
                var result = reader.Decode(new RGBLuminanceSource(bytes, cameraTexture.width, cameraTexture.height, RGBLuminanceSource.BitmapFormat.RGBA32));
                if (result != null)
                {
                    if (LanInvitation.TryDecode(result.Text, out var ip, out var port)) { scanner = null; StopScan(); message.text = "已识别，正在连接…"; network.JoinRoom(ip.ToString(), port); yield break; }
                    message.text = "二维码不是有效的房间邀请";
                }
            }
            yield return new WaitForSecondsRealtime(.3f);
        }
    }
    private void OnApplicationPause(bool paused) { if (paused && scanner != null && !requestingPermission) { StopScan(); message.text = "扫码已暂停，请重新打开"; } }
    private void StopScan()
    {
        requestingPermission = false;
        if (scanner != null) { StopCoroutine(scanner); scanner = null; }
        if (cameraTexture != null) { cameraTexture.Stop(); Destroy(cameraTexture); cameraTexture = null; }
        if (image != null) { image.rectTransform.localEulerAngles = Vector3.zero; image.rectTransform.localScale = Vector3.one; }
        if (image != null && qrTexture == null) { image.texture = null; image.gameObject.SetActive(false); }
    }
}
