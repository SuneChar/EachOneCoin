using System;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public enum BannerPromptCategory
    {
        MineWarning,
        CoinsSpawned,
        NearVictory,
        PlayerFinished,
        Custom
    }

    [Serializable]
    public struct BannerPromptRequest
    {
        public BannerPromptCategory Category;
        public string Title;
        public string Subtitle;
        public Sprite Icon;
        public Color AccentColor;
        public float Duration;
        public int Priority;
        public bool EmphasizeIconPop;
    }
}
