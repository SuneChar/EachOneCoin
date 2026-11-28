using TMPro;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    // World-space label authored outside the animated character visual.
    public sealed class CharacterNameplate : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private TMP_Text label;
        private string currentName;

        public void Initialize(TMP_FontAsset font, float height)
        {
            if (canvas != null) canvas.transform.localPosition = Vector3.up * height;
            if (font != null && label != null) label.font = font;
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
