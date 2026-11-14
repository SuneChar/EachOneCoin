using System;
using UnityEngine;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(-1000)]
public class InputDeviceTracker : MonoBehaviour
{
    public static InputDeviceTracker Instance { get; private set; }
    public bool UsingGamepad { get; private set; }
    public event Action<bool> DeviceChanged;

    private static readonly KeyCode[] KeyboardKeys = GetKeyboardKeys();
    private static readonly string[] GamepadAxes = { "GamepadHorizontal", "GamepadVertical", "GamepadRightHorizontal", "GamepadRightVertical", "GamepadAxis3", "GamepadAxis6", "GamepadAxis7", "GamepadAxis8", "GamepadAxis9", "GamepadAxis10" };
    private readonly float[] previousAxes = new float[GamepadAxes.Length];
    private Vector3 mouseOrigin;
    private float nextConnectionCheck;
    private float disconnectedSince = -1f;
    private bool hasSeenConnectedGamepad;
    private const float MouseMovementThreshold = 6f;
    private const float DisconnectConfirmSeconds = 2f;

    private static KeyCode[] GetKeyboardKeys()
    {
        var values = (KeyCode[])Enum.GetValues(typeof(KeyCode));
        var keys = new System.Collections.Generic.List<KeyCode>();
        foreach (var key in values)
            if (key > KeyCode.None && key < KeyCode.Mouse0) keys.Add(key);
        return keys.ToArray();
    }

    private void Awake()
    {
        Instance = this;
        mouseOrigin = Input.mousePosition;
        CheckConnections();
        for (int i = 0; i < GamepadAxes.Length; i++) previousAxes[i] = Input.GetAxisRaw(GamepadAxes[i]);
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) return;
        mouseOrigin = Input.mousePosition;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextConnectionCheck) CheckConnections();

        bool mouseOrKeyboard = false;
        if (Input.anyKeyDown)
        {
            for (int i = 0; i < KeyboardKeys.Length && !mouseOrKeyboard; i++)
                mouseOrKeyboard = Input.GetKeyDown(KeyboardKeys[i]);
        }
        for (int i = 0; i < 3; i++) mouseOrKeyboard |= Input.GetMouseButtonDown(i);
        Vector3 mouse = Input.mousePosition;
        bool mouseMoved = (mouse - mouseOrigin).sqrMagnitude >= MouseMovementThreshold * MouseMovementThreshold;
        mouseOrKeyboard |= Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f;

        bool gamepad = false;
        for (int i = 0; i < 20; i++) gamepad |= Input.GetKeyDown(KeyCode.JoystickButton0 + i);
        for (int i = 0; i < GamepadAxes.Length; i++)
        {
            float value = Input.GetAxisRaw(GamepadAxes[i]);
            gamepad |= Mathf.Abs(value) > 0.25f && (Mathf.Abs(previousAxes[i]) <= 0.25f || Mathf.Abs(value - previousAxes[i]) > 0.15f);
            previousAxes[i] = value;
        }
        // Explicit keyboard/mouse actions win; controller activity wins over mouse motion.
        if (mouseOrKeyboard)
        {
            mouseOrigin = mouse;
            SetDevice(false);
        }
        else if (gamepad)
        {
            mouseOrigin = mouse;
            disconnectedSince = -1f;
            SetDevice(true);
        }
        else if (mouseMoved)
        {
            mouseOrigin = mouse;
            SetDevice(false);
        }
    }

    private void LateUpdate()
    {
        if (!UsingGamepad && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void CheckConnections()
    {
        nextConnectionCheck = Time.unscaledTime + 0.5f;
        bool connected = false;
        foreach (string name in Input.GetJoystickNames())
            if (!string.IsNullOrEmpty(name)) { connected = true; break; }
        if (connected)
        {
            hasSeenConnectedGamepad = true;
            disconnectedSince = -1f;
        }
        else if (UsingGamepad && hasSeenConnectedGamepad)
        {
            if (disconnectedSince < 0f) disconnectedSince = Time.unscaledTime;
            else if (Time.unscaledTime - disconnectedSince >= DisconnectConfirmSeconds) SetDevice(false);
        }
        else disconnectedSince = -1f;
    }

    private void SetDevice(bool gamepad)
    {
        if (UsingGamepad == gamepad) return;
        UsingGamepad = gamepad;
        DeviceChanged?.Invoke(gamepad);
    }
}
