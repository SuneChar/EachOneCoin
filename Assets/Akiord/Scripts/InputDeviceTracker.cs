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
    private Vector3 lastMousePosition;
    private float nextConnectionCheck;

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
        lastMousePosition = Input.mousePosition;
        CheckConnections();
        for (int i = 0; i < GamepadAxes.Length; i++) previousAxes[i] = Input.GetAxisRaw(GamepadAxes[i]);
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
        mouseOrKeyboard |= (mouse - lastMousePosition).sqrMagnitude >= 1f || Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f;
        lastMousePosition = mouse;

        bool gamepad = false;
        for (int i = 0; i < 20; i++) gamepad |= Input.GetKeyDown(KeyCode.JoystickButton0 + i);
        for (int i = 0; i < GamepadAxes.Length; i++)
        {
            float value = Input.GetAxisRaw(GamepadAxes[i]);
            gamepad |= Mathf.Abs(value) > 0.25f && (Mathf.Abs(previousAxes[i]) <= 0.25f || Mathf.Abs(value - previousAxes[i]) > 0.15f);
            previousAxes[i] = value;
        }
        // When both devices change in the same frame, explicit keyboard/mouse input wins.
        if (mouseOrKeyboard) SetDevice(false);
        else if (gamepad) SetDevice(true);
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
        if (!connected && UsingGamepad) SetDevice(false);
    }

    private void SetDevice(bool gamepad)
    {
        if (UsingGamepad == gamepad) return;
        UsingGamepad = gamepad;
        DeviceChanged?.Invoke(gamepad);
    }
}
