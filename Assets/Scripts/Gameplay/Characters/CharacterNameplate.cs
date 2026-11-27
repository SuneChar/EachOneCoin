using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EachOneCoin.Gameplay
{
    // Runtime-only world-space label, kept outside the animated character visual.
    public sealed class CharacterNameplate : MonoBehaviour
    {
        private Canvas canvas;
        private TMP_Text label;
        private string currentName;

        public void Initialize(TMP_FontAsset font, float height)
        {
            var root = new GameObject("Character Nameplate", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.up * height;
            root.transform.localScale = Vector3.one * 0.0075f;
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(160f, 42f);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;

            var background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background.transform.SetParent(root.transform, false);
            var backgroundRect = (RectTransform)background.transform;
            backgroundRect.anchorMin = Vector2.zero; backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero; backgroundRect.offsetMax = Vector2.zero;
            var image = background.GetComponent<Image>();
            image.color = new Color(0.03f, 0.04f, 0.06f, 0.72f);
            image.raycastTarget = false;

            var text = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            text.transform.SetParent(root.transform, false);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(5f, 2f); textRect.offsetMax = new Vector2(-5f, -2f);
            label = text.GetComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.fontSize = 26f;
            label.enableAutoSizing = true; label.fontSizeMin = 14f; label.fontSizeMax = 26f;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            SetName(string.Empty);
        }

        public void SetName(string value)
        {
            value = value ?? string.Empty;
            if (currentName == value) return;
            currentName = value;
            if (label != null) label.text = value;
        }

        public void SetVisible(bool visible)
        {
            if (canvas != null) canvas.gameObject.SetActive(visible);
        }

        private void LateUpdate()
        {
            if (canvas == null || !canvas.gameObject.activeSelf) return;
            var camera = Camera.main;
            if (camera != null) canvas.transform.rotation = camera.transform.rotation;
        }
    }
}
