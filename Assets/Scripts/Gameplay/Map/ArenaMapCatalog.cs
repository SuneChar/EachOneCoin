using UnityEngine;

namespace EachOneCoin.Gameplay
{
    [CreateAssetMenu(menuName = "EachOneCoin/Map/Map Catalog", fileName = "ArenaMapCatalog")]
    public sealed class ArenaMapCatalog : ScriptableObject
    {
        [SerializeField] private ArenaMapDefinition[] maps = System.Array.Empty<ArenaMapDefinition>();
        public ArenaMapDefinition[] Maps => maps;
    }
}
