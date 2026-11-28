using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EachOneCoin.Gameplay
{
    // How the move stick is driven: a stick that stays in the bottom-left corner, or one that
    // appears where the player first touches inside the lower-left area.
    public enum TouchJoystickMode { Fixed = 0, Floating = 1 }

    // Statically authored touch overlay; pointer handlers bind to prefab objects at initialization.
    public sealed class TouchGameplayControls : MonoBehaviour
    {
        private const float StickSize = 200f;
        private const float StickOffsetX = 145f;
        private const float StickOffsetY = 145f;
        internal const float KnobRange = 45f;
        // The attack stick keeps the sensitivity of the old square button: full aim tilt at 100px.
        internal const float AimRange = 100f;

        private MatchCoordinator match;
        private ArenaGameUI ui;
        private LocalPlayerInput input;
        [SerializeField] private GameObject gameplay, spectator;
        [SerializeField] private RectTransform movementArea, attackArea, moveStick, attackStick, movementKnob, attackKnob;
        [SerializeField] private GameObject sprintObject;

        private TouchControl sprint;
        private VirtualJoystick movement, attack;

        public TouchJoystickMode JoystickMode { get; private set; } = TouchJoystickMode.Fixed;

        public void Initialize(MatchCoordinator coordinator, ArenaGameUI gameUI, Transform parent, TMP_FontAsset font)
        {
            match = coordinator; ui = gameUI; input = match.LocalInput;
            JoystickMode = TouchSettings.JoystickMode;
            movement = movementArea.gameObject.AddComponent<VirtualJoystick>();
            movement.Initialize(this, movementArea, moveStick, movementKnob, 0);
            attack = attackArea.gameObject.AddComponent<VirtualJoystick>();
            attack.Initialize(this, attackArea, attackStick, attackKnob, 1);
            sprint = sprintObject.AddComponent<TouchControl>();
            sprint.Initialize(this, 2);
            spectator.AddComponent<TouchControl>().Initialize(this, 3);
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
            attackStick.gameObject.SetActive(usable);
            if (!usable) moveStick.gameObject.SetActive(false);
            else if (JoystickMode == TouchJoystickMode.Fixed) moveStick.gameObject.SetActive(true);
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
