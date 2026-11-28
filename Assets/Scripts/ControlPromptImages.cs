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
    [SerializeField] private Texture2D keyboardM;
    [SerializeField] private Texture2D xboxView;
    [SerializeField] private Texture2D keyboardSpace, keyboardShift, keyboardCapsLock;
    [SerializeField] private Texture2D xboxX, xboxLB, xboxRT;

    [SerializeField] private RawImage[] movementIcons;
    [SerializeField] private RawImage actionIcon;
    [SerializeField] private RawImage backIcon;
    [SerializeField] private RawImage minimapIcon;
    [SerializeField] private TMP_Text movementLabel;
    [SerializeField] private TMP_Text actionLabel;
    [SerializeField] private TMP_Text backLabel;
    [SerializeField] private TMP_Text minimapLabel;
    [SerializeField] private RawImage attackIcon, aimIcon, sprintIcon, minimapHoldIcon;
    [SerializeField] private TMP_Text attackLabel, sprintLabel, minimapHoldLabel;

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
        if (minimapIcon != null)
        {
            minimapIcon.gameObject.SetActive(playing);
            if (playing) minimapIcon.texture = gamepad ? xboxView : keyboardM;
        }
        if (minimapLabel != null)
        {
            minimapLabel.gameObject.SetActive(playing);
            if (playing) minimapLabel.text = "切换地图";
        }
        SetGameplayPrompt(attackIcon, playing, gamepad ? xboxX : keyboardSpace);
        SetGameplayPrompt(aimIcon, playing, gamepad ? xboxStick : mouseMove);
        SetGameplayPrompt(sprintIcon, playing, gamepad ? xboxLB : keyboardShift);
        SetGameplayPrompt(minimapHoldIcon, playing, gamepad ? xboxRT : keyboardCapsLock);
        if (attackLabel != null) { attackLabel.gameObject.SetActive(playing); attackLabel.text = "按住瞄准，松开攻击"; }
        if (sprintLabel != null) { sprintLabel.gameObject.SetActive(playing); sprintLabel.text = "按住冲刺"; }
        if (minimapHoldLabel != null) { minimapHoldLabel.gameObject.SetActive(playing); minimapHoldLabel.text = "按住展开"; }
        backIcon.gameObject.SetActive(canGoBack);
        backLabel.gameObject.SetActive(canGoBack);
        backIcon.texture = gamepad ? xboxB : keyboardEscape;
    }

    private static void SetGameplayPrompt(RawImage image, bool playing, Texture2D texture)
    {
        if (image == null) return;
        image.gameObject.SetActive(playing);
        if (playing) image.texture = texture;
    }

}
