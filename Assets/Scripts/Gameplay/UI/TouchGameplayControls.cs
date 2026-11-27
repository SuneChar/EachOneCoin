using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EachOneCoin.Gameplay
{
    // Runtime-only overlay: the existing scene and its serialized references remain unchanged.
    public sealed class TouchGameplayControls : MonoBehaviour
    {
        private MatchCoordinator match;
        private ArenaGameUI ui;
        private LocalPlayerInput input;
        private GameObject gameplay, spectator;
        private TouchControl movement, attack, sprint;

        public void Initialize(MatchCoordinator coordinator, ArenaGameUI gameUI, Transform parent, TMP_FontAsset font)
        {
            match = coordinator; ui = gameUI; input = match.LocalInput;
            gameplay = new GameObject("Touch Gameplay", typeof(RectTransform));
            var root = (RectTransform)gameplay.transform;
            root.SetParent(parent, false); root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            movement = CreateControl(root, "Move", new Vector2(0, 0), new Vector2(145, 145), new Vector2(200, 200), font, "移动", 0);
            attack = CreateControl(root, "Attack", new Vector2(1, 0), new Vector2(-145, 145), new Vector2(200, 200), font, "攻击", 1);
            sprint = CreateControl(root, "Sprint", new Vector2(1, 0), new Vector2(-320, 105), new Vector2(120, 120), font, "冲刺", 2);
            spectator = CreateControl(root, "Spectator", new Vector2(1, 0), new Vector2(-145, 145), new Vector2(200, 100), font, "切换观战", 3).gameObject;
            gameplay.SetActive(false);
        }

        private TouchControl CreateControl(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, TMP_FontAsset font, string label, int kind)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TouchControl));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
            obj.GetComponent<Image>().color = new Color(0.12f, 0.18f, 0.25f, 0.55f);
            var textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var textRect = (RectTransform)textObject.transform;
            textRect.SetParent(rect, false); textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            var text = textObject.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = label; text.fontSize = 24; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            var control = obj.GetComponent<TouchControl>();
            control.Initialize(this, kind);
            return control;
        }

        private void Update()
        {
            if (match == null || input == null || ui == null || match.Model == null) { SetVisible(false, false); return; }
            bool available = Application.isFocused && !match.IsPaused && !ui.NetworkMenuOpen;
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
            movement.gameObject.SetActive(!spectating); attack.gameObject.SetActive(!spectating); sprint.gameObject.SetActive(!spectating);
            spectator.SetActive(spectating);
        }

        public void Cancel()
        {
            if (input != null) input.CancelTouchInput();
            if (movement != null) movement.ResetPointer();
            if (attack != null) attack.ResetPointer();
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
