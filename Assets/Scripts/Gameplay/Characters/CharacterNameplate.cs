using TMPro;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    // World-space label authored outside the animated character visual.
    public sealed class CharacterNameplate : MonoBehaviour
    {
        private const string HealthBarObjectName = "HealthBarRoot";

        [SerializeField] private Canvas canvas;
        [SerializeField] private TMP_Text label;
        [SerializeField] private SegmentedHealthBarView healthBar;
        [SerializeField, Min(4f)] private float fallbackHealthBarHeight = 16f;
        private string currentName;
        private bool healthBarVisible;

        public bool IsHealthBarVisible => healthBarVisible;
        public SegmentedHealthBarView HealthBar => healthBar;

        public void Initialize(TMP_FontAsset font, float height)
        {
            if (canvas != null) canvas.transform.localPosition = Vector3.up * height;
            if (font != null && label != null) label.font = font;
            SetName(string.Empty);
            EnsureHealthBar();
            SetHealthBarVisible(false);
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

        public void SetHealthBarVisible(bool visible)
        {
            healthBarVisible = visible;
            EnsureHealthBar();
            if (healthBar != null) healthBar.SetVisible(visible);
        }

        public void SetHealth(int health, int maxHealth, int initialMaxHealth, bool force = false)
        {
            EnsureHealthBar();
            if (healthBar != null) healthBar.SetHealth(health, maxHealth, initialMaxHealth, force);
        }

        private void EnsureHealthBar()
        {
            if (healthBar != null)
            {
                healthBar.EnsureInitialized();
                return;
            }

            if (canvas == null) return;

            Transform existing = canvas.transform.Find(HealthBarObjectName);
            GameObject barObj;
            if (existing != null)
            {
                barObj = existing.gameObject;
                healthBar = barObj.GetComponent<SegmentedHealthBarView>();
                if (healthBar == null)
                {
                    healthBar = barObj.AddComponent<SegmentedHealthBarView>();
                }
            }
            else
            {
                barObj = new GameObject(
                    HealthBarObjectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(UnityEngine.UI.Image),
                    typeof(SegmentedHealthBarView));
                barObj.layer = canvas.gameObject.layer;

                var rect = (RectTransform)barObj.transform;
                rect.SetParent(canvas.transform, false);
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(0f, Mathf.Max(4f, fallbackHealthBarHeight));

                var img = barObj.GetComponent<UnityEngine.UI.Image>();
                img.sprite = null;
                img.preserveAspect = false;
                img.raycastTarget = false;

                healthBar = barObj.GetComponent<SegmentedHealthBarView>();
            }

            healthBar.EnsureInitialized();
            healthBar.SetVisible(healthBarVisible);
        }

        private void LateUpdate()
        {
            if (canvas == null || !canvas.gameObject.activeSelf) return;
            var camera = Camera.main;
            if (camera != null) canvas.transform.rotation = camera.transform.rotation;
        }
    }
}
