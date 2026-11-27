using System;
using EachOneCoin;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    [Serializable]
    public struct ParticipantHudSnapshot
    {
        public int SlotId;
        public string DisplayName;
        public bool IsParticipating;
        public bool IsLocal;
        public int Coins;
        public int FinishOrder;
        public int Health;
        public int MaxHealth;
        public int InitialMaxHealth;
        public ParticipantStatus Status;
        public Color Color;
        public AvatarAppearanceData Appearance;
    }
}
