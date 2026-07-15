using UnityEngine;
using Aerow.Sim;

namespace Aerow.View
{
    /// <summary>
    /// The runtime result of building content: the sim's item catalogue plus the view-side
    /// visual lookup. The visual array is <b>index-aligned</b> with the item catalogue, so
    /// resolving an <see cref="ItemId"/> to its mesh/material is a single array index —
    /// no dictionary, no string compare, safe to call every frame.
    /// Owned per session by the SimulationManager; rebuilt fresh each play.
    /// </summary>
    public sealed class GameContent
    {
        public readonly ItemCatalogue Items;

        private readonly ItemDefSO[] _itemVisuals; // indexed by ItemId.Index

        public GameContent(ItemCatalogue items, ItemDefSO[] itemVisuals)
        {
            Items = items;
            _itemVisuals = itemVisuals;
        }

        public ItemDefSO SOFor(ItemId id) => _itemVisuals[id.Index];
        public Sprite IconOf(ItemId id) => _itemVisuals[id.Index].icon;
        public Material MaterialOf(ItemId id) => _itemVisuals[id.Index].material;
        public Mesh MeshOf(ItemId id) => _itemVisuals[id.Index].mesh;
        public GameObject PrefabOf(ItemId id) => _itemVisuals[id.Index].prefab;
    }
}
