using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ControlPromptImages : MonoBehaviour
{
    [SerializeField] private Texture2D keyboardW;
    [SerializeField] private Texture2D keyboardA;
    [SerializeField] private Texture2D keyboardS;
    [SerializeField] private Texture2D keyboardD;
    [SerializeField] private Texture2D keyboardArrows;
    [SerializeField] private Texture2D keyboardEnter;
    [SerializeField] private Texture2D keyboardEscape;
    [SerializeField] private Texture2D mouseMove;
    [SerializeField] private Texture2D mouseLeft;
    [SerializeField] private Texture2D xboxStick;
    [SerializeField] private Texture2D xboxA;
    [SerializeField] private Texture2D xboxB;
    [SerializeField] private Texture2D xboxMenu;

    private GameObject row;
    private RawImage[] movementIcons;
    private RawImage actionIcon;
    private RawImage backIcon;
    private TMP_Text movementLabel;
    private TMP_Text actionLabel;
    private TMP_Text backLabel;

    public void Refresh(bool visible, bool gamepad, bool playing, bool canGoBack)
    {
        gameObject.SetActive(visible);
        if (!visible) return;
        if (row == null) Build();

        Texture2D[] keyboardMovement = { keyboardW, keyboardA, keyboardS, keyboardD, keyboardArrows };
        for (int i = 0; i < movementIcons.Length; i++)
        {
            bool active = gamepad || !playing ? i == 0 : true;
            movementIcons[i].gameObject.SetActive(active);
            if (active) movementIcons[i].texture = gamepad ? xboxStick : playing ? keyboardMovement[i] : mouseMove;
        }
        movementLabel.text = playing ? "移动" : "选择";
        actionIcon.texture = gamepad ? (playing ? xboxMenu : xboxA) : (playing ? keyboardEscape : mouseLeft);
        actionLabel.text = playing ? "暂停" : "确认";
        backIcon.gameObject.SetActive(canGoBack);
        backLabel.gameObject.SetActive(canGoBack);
        backIcon.texture = gamepad ? xboxB : keyboardEscape;
    }

    private void Build()
    {
        var template = GetComponent<TMP_Text>();
        template.text = string.Empty;
        row = new GameObject("Prompt Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        row.transform.SetParent(transform, false);
        var rect = row.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        var fitter = row.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        movementIcons = new RawImage[5];
        for (int i = 0; i < movementIcons.Length; i++) movementIcons[i] = CreateIcon();
        movementLabel = CreateLabel(template);
        actionIcon = CreateIcon();
        actionLabel = CreateLabel(template);
        backIcon = CreateIcon();
        backLabel = CreateLabel(template);
        backLabel.text = "返回";
    }

    private RawImage CreateIcon()
    {
        var obj = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(LayoutElement));
        obj.transform.SetParent(row.transform, false);
        var image = obj.GetComponent<RawImage>();
        image.raycastTarget = false;
        var size = obj.GetComponent<LayoutElement>();
        size.preferredWidth = 38f;
        size.preferredHeight = 38f;
        return image;
    }

    private TMP_Text CreateLabel(TMP_Text template)
    {
        var obj = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        obj.transform.SetParent(row.transform, false);
        var label = obj.GetComponent<TextMeshProUGUI>();
        label.font = template.font;
        label.fontSharedMaterial = template.fontSharedMaterial;
        label.fontSize = template.fontSize;
        label.color = template.color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        var size = obj.GetComponent<LayoutElement>();
        size.preferredWidth = 68f;
        size.preferredHeight = 38f;
        return label;
    }
}
