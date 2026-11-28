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

    [SerializeField] private RawImage[] movementIcons;
    [SerializeField] private RawImage actionIcon;
    [SerializeField] private RawImage backIcon;
    [SerializeField] private TMP_Text movementLabel;
    [SerializeField] private TMP_Text actionLabel;
    [SerializeField] private TMP_Text backLabel;

    public void Refresh(bool visible, bool gamepad, bool playing, bool canGoBack)
    {
        gameObject.SetActive(visible);
        if (!visible) return;

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

}
