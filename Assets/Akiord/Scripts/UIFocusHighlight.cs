using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Selectable))]
public class UIFocusHighlight : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [SerializeField] private Color borderColor = new Color(1f, 0.8f, 0.25f, 1f);
    [SerializeField, Min(1f)] private float borderWidth = 4f;
    [SerializeField, Min(0f)] private float padding = 6f;

    private GameObject border;

    private void Awake()
    {
        border = new GameObject("Focus Border", typeof(RectTransform));
        border.transform.SetParent(transform, false);
        var rect = border.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-padding, -padding);
        rect.offsetMax = new Vector2(padding, padding);
        CreateEdge("Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -borderWidth), Vector2.zero);
        CreateEdge("Bottom", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, borderWidth));
        CreateEdge("Left", Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(borderWidth, 0));
        CreateEdge("Right", new Vector2(1, 0), Vector2.one, new Vector2(-borderWidth, 0), Vector2.zero);
        border.SetActive(false);
    }

    private void CreateEdge(string edgeName, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        var edge = new GameObject(edgeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        edge.transform.SetParent(border.transform, false);
        var rect = edge.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var image = edge.GetComponent<Image>();
        image.color = borderColor;
        image.raycastTarget = false;
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (border != null) border.SetActive(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (border != null) border.SetActive(false);
    }

    private void OnDisable()
    {
        if (border != null) border.SetActive(false);
    }
}
