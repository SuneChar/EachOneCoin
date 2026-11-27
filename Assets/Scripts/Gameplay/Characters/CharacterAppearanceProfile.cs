using UnityEngine;

namespace EachOneCoin.Gameplay
{
    [CreateAssetMenu(menuName = "EachOneCoin/Characters/Appearance Profile", fileName = "CharacterAppearance")]
    public sealed class CharacterAppearanceProfile : ScriptableObject
    {
        [SerializeField] private string appearanceId;
        [SerializeField] private string accessorySlotName;

        public string AppearanceId => appearanceId;
        public string AccessorySlotName => accessorySlotName;
    }
}
