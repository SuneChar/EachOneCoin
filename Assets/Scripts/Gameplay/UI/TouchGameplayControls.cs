using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EachOneCoin.Gameplay
{
    // How the move stick is driven: a stick that stays in the bottom-left corner, or one that
    // appears where the player first touches inside the lower-left area.
    public enum TouchJoystickMode { Fixed = 0, Floating = 1 }

    // Runtime-only overlay: the existing scene and its serialized references remain unchanged.
    public sealed class TouchGameplayControls : MonoBehaviour
    {
        private const float StickSize = 200f;
        private const float KnobSize = 90f;
        private const float StickOffsetX = 145f;
        private const float StickOffsetY = 145f;
        internal const float KnobRange = 45f;
        // The attack stick keeps the sensitivity of the old square button: full aim tilt at 100px.
        internal const float AimRange = 100f;

        private MatchCoordinator match;
        private ArenaGameUI ui;
        private LocalPlayerInput input;
        private GameObject gameplay, spectator;
        private TouchControl sprint;
        private VirtualJoystick movement, attack;
        private RectTransform moveStick;

        public TouchJoystickMode JoystickMode { get; private set; } = TouchJoystickMode.Fixed;

        public void Initialize(MatchCoordinator coordinator, ArenaGameUI gameUI, Transform parent, TMP_FontAsset font)
        {
            match = coordinator; ui = gameUI; input = match.LocalInput;
            JoystickMode = TouchSettings.JoystickMode;
            gameplay = new GameObject("Touch Gameplay", typeof(RectTransform));
            var root = (RectTransform)gameplay.transform;
            root.SetParent(parent, false); root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            movement = CreateJoystick(root, font, 0, new Vector2(0f, 0f), new Vector2(StickOffsetX, StickOffsetY), "移动");
            moveStick = movement.Stick;
            // The attack stick always stays in the bottom-right corner, whatever the move mode is.
            attack = CreateJoystick(root, font, 1, new Vector2(1f, 0f), new Vector2(-StickOffsetX, StickOffsetY), "攻击");
            sprint = CreateControl(root, "Sprint", new Vector2(1, 0), new Vector2(-320, 105), new Vector2(120, 120), font, "冲刺", 2);
            spectator = CreateControl(root, "Spectator", new Vector2(1, 0), new Vector2(-145, 145), new Vector2(200, 100), font, "切换观战", 3).gameObject;
            ApplyJoystickMode();
            gameplay.SetActive(false);
        }

        public void SetJoystickMode(TouchJoystickMode mode)
        {
            if (JoystickMode != mode)
            {
                JoystickMode = mode;
                TouchSettings.JoystickMode = mode;
            }
            Cancel();
            ApplyJoystickMode();
        }

        private VirtualJoystick CreateJoystick(Transform parent, TMP_FontAsset font, int kind, Vector2 anchor, Vector2 position, string labelText)
        {
            string prefix = kind == 0 ? "Move" : "Attack";
            // The visible stick never receives pointer events; the area below does.
            var stick = CreateRect(parent, prefix + " Stick", anchor, position, new Vector2(StickSize, StickSize));
            var baseImage = stick.gameObject.AddComponent<Image>();
            baseImage.sprite = CircleSprite();
            baseImage.color = new Color(0.12f, 0.18f, 0.25f, 0.55f);
            baseImage.raycastTarget = false;

            var knob = CreateRect(stick, "Knob", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(KnobSize, KnobSize));
            var knobImage = knob.gameObject.AddComponent<Image>();
            knobImage.sprite = CircleSprite();
            knobImage.color = new Color(0.9f, 0.95f, 1f, 0.8f);
            knobImage.raycastTarget = false;

            var label = CreateLabel(stick, labelText, font, 22f);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -6f);
            labelRect.sizeDelta = new Vector2(0f, 28f);

            var area = CreateRect(parent, prefix + " Area", anchor, position, new Vector2(StickSize, StickSize));
            var areaImage = area.gameObject.AddComponent<Image>();
            // Barely visible but still raycastable, so the touch area can be larger than the stick.
            areaImage.color = new Color(0f, 0f, 0f, 0.01f);
            var joystick = area.gameObject.AddComponent<VirtualJoystick>();
            joystick.Initialize(this, area, stick, knob, kind);
            return joystick;
        }

        private void ApplyJoystickMode()
        {
            if (movement == null || moveStick == null) return;
            var area = (RectTransform)movement.transform;
            bool floating = JoystickMode == TouchJoystickMode.Floating;
            if (floating)
            {
                area.anchorMin = new Vector2(0f, 0f);
                area.anchorMax = new Vector2(0.5f, 0.6f);
                area.offsetMin = Vector2.zero;
                area.offsetMax = Vector2.zero;
            }
            else
            {
                area.anchorMin = area.anchorMax = new Vector2(0f, 0f);
                area.anchoredPosition = new Vector2(StickOffsetX, StickOffsetY);
                area.sizeDelta = new Vector2(StickSize, StickSize);
                moveStick.anchoredPosition = new Vector2(StickOffsetX, StickOffsetY);
            }
            movement.Floating = floating;
            movement.ResetStick();
            moveStick.gameObject.SetActive(!floating);
        }

        // Built-in UI sprites are not available through Resources at runtime, so the stick uses
        // a generated circle texture instead of shipping an extra sprite asset.
        private static Sprite circleSprite;
        private static Sprite CircleSprite()
        {
            if (circleSprite != null) return circleSprite;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01((radius - distance) / 2f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            return circleSprite;
        }

        private static RectTransform CreateRect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static TMP_Text CreateLabel(Transform parent, string label, TMP_FontAsset font, float size)
        {
            var obj = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = obj.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = label;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private TouchControl CreateControl(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, TMP_FontAsset font, string label, int kind)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TouchControl));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
            obj.GetComponent<Image>().color = new Color(0.12f, 0.18f, 0.25f, 0.55f);
            CreateLabel(rect, label, font, 24f);
            var control = obj.GetComponent<TouchControl>();
            control.Initialize(this, kind);
            return control;
        }

        private void Update()
        {
            if (match == null || input == null || ui == null || match.Model == null) { SetVisible(false, false); return; }
            bool available = (Application.isFocused || Application.isEditor) && !match.IsPaused && !ui.NetworkMenuOpen;
            bool spectating = false;
            if (match.IsNetworkMovementTest)
            {
                var network = Mirror.NetworkManager.singleton as EachOneCoin.Networking.LanNetworkManager;
                var state = match.NetworkController;
                available = available && network != null && !network.ClientResuming && state != null && state.IsRunning && state.CurrentMatchStatus == MatchStatus.Playing;
                if (available)
                {
                    spectating = state.IsSpectating;
                    available = spectating || (network.LocalSlot >= 0 && network.LocalSlot < 4 && state.StatusAt(network.LocalSlot) == ParticipantStatus.Active);
                }
            }
            else available = available && match.Model.Status == MatchStatus.Playing && match.Model.Participants[0].Status == ParticipantStatus.Active;
            SetVisible(available, spectating);
        }

        private void SetVisible(bool visible, bool spectating)
        {
            if (gameplay == null) return;
            if (!visible || spectating) Cancel();
            if (gameplay.activeSelf != visible) gameplay.SetActive(visible);
            if (!visible) return;
            bool usable = !spectating;
            movement.gameObject.SetActive(usable);
            attack.gameObject.SetActive(usable);
            sprint.gameObject.SetActive(usable);
            if (usable && JoystickMode == TouchJoystickMode.Fixed) moveStick.gameObject.SetActive(true);
            spectator.SetActive(spectating);
        }

        public void Cancel()
        {
            if (input != null) input.CancelTouchInput();
            if (movement != null) movement.ResetStick();
            if (attack != null) attack.ResetStick();
            if (sprint != null) sprint.ResetPointer();
        }
        private void OnDisable() { Cancel(); }
        private void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
        private void OnApplicationPause(bool paused) { if (paused) Cancel(); }

        internal void Press(int kind) { if (kind == 1) input.BeginTouchAttack(); else if (kind == 2) input.TouchSprintHeld = true; else if (kind == 3 && match.NetworkController != null) match.NetworkController.NextSpectatorTarget(); }
        internal void Drag(int kind, Vector2 value) { if (kind == 0) input.TouchMovement = value; else if (kind == 1) input.TouchAim = value; }
        internal void Release(int kind, bool cancelled)
        {
            if (kind == 0) input.TouchMovement = Vector2.zero;
            else if (kind == 1) { if (cancelled) input.CancelTouchAttack(); else input.ReleaseTouchAttack(); }
            else if (kind == 2) input.TouchSprintHeld = false;
        }
    }

    // Drag-driven stick: the knob follows the pointer inside a fixed radius and the owner
    // receives the normalised direction. Floating mode moves the visible stick to the touch point.
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private TouchGameplayControls owner;
        private RectTransform area, stick, knob;
        private int pointer = int.MinValue;
        private int kind;
        private Vector2 origin;

        public bool Floating { get; set; }
        public RectTransform Stick => stick;

        public void Initialize(TouchGameplayControls controls, RectTransform areaRect, RectTransform stickRect, RectTransform knobRect, int channel)
        {
            owner = controls; area = areaRect; stick = stickRect; knob = knobRect; kind = channel;
        }

        // The movement stick reports full strength at the end of its visual travel; the attack
        // stick keeps the wider range the old square button used so aiming feels unchanged.
        private float InputRange => kind == 0 ? TouchGameplayControls.KnobRange : TouchGameplayControls.AimRange;

        public void ResetStick()
        {
            pointer = int.MinValue;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            if (Floating && stick != null) stick.gameObject.SetActive(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointer != int.MinValue || owner == null || area == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out Vector2 point)) return;
            pointer = eventData.pointerId;
            if (Floating)
            {
                // Clamp in the touch area's local space so the whole stick stays inside it, then
                // place the stick through world space: the clamp and the direction both live in
                // area space, so the two coordinate systems can no longer drift apart.
                float half = Mathf.Max(stick.rect.width, stick.rect.height) * 0.5f;
                origin = new Vector2(
                    Mathf.Clamp(point.x, area.rect.min.x + half, area.rect.max.x - half),
                    Mathf.Clamp(point.y, area.rect.min.y + half, area.rect.max.y - half));
                stick.position = area.TransformPoint(origin);
                stick.gameObject.SetActive(true);
            }
            else origin = Vector2.zero;
            knob.anchoredPosition = Vector2.zero;
            owner.Press(kind);
            ApplyDirection(point);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != pointer) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out Vector2 point)) ApplyDirection(point);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != pointer) return;
            bool cancelled = false;
            for (int i = 0; i < Input.touchCount; i++) if (Input.GetTouch(i).fingerId == pointer && Input.GetTouch(i).phase == TouchPhase.Canceled) cancelled = true;
            ResetStick();
            if (owner != null) owner.Release(kind, cancelled);
        }

        private void ApplyDirection(Vector2 point)
        {
            Vector2 delta = point - origin;
            knob.anchoredPosition = Vector2.ClampMagnitude(delta, TouchGameplayControls.KnobRange);
            if (owner != null) owner.Drag(kind, Vector2.ClampMagnitude(delta / InputRange, 1f));
        }
    }

    public sealed class TouchControl : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private TouchGameplayControls owner;
        private int kind, pointer = int.MinValue;
        public void Initialize(TouchGameplayControls controls, int controlKind) { owner = controls; kind = controlKind; }
        public void ResetPointer() { pointer = int.MinValue; }
        private void OnDisable() { ResetPointer(); }
        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointer != int.MinValue) return;
            pointer = eventData.pointerId; owner.Press(kind); UpdateDirection(eventData);
        }
        public void OnDrag(PointerEventData eventData) { if (eventData.pointerId == pointer) UpdateDirection(eventData); }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != pointer) return;
            bool cancelled = false;
            for (int i = 0; i < Input.touchCount; i++) if (Input.GetTouch(i).fingerId == pointer && Input.GetTouch(i).phase == TouchPhase.Canceled) cancelled = true;
            pointer = int.MinValue; owner.Release(kind, cancelled);
        }
        private void UpdateDirection(PointerEventData eventData)
        {
            if (kind > 1) return;
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 point)) return;
            owner.Drag(kind, Vector2.ClampMagnitude(new Vector2(point.x / (rect.rect.width * 0.5f), point.y / (rect.rect.height * 0.5f)), 1f));
        }
    }
}
