using UnityEngine;
using Aerow.Sim;

namespace Aerow.View
{
    /// <summary>
    /// The runtime result of building content: the sim's item and block catalogues plus the
    /// view-side visual lookups. Each visual array is <b>index-aligned</b> with its catalogue, so
    /// resolving an <see cref="ItemId"/>/<see cref="BlockId"/> to its mesh/material is a single
    /// array index — no dictionary, no string compare, safe to call every frame.
    /// Owned per session by the bootstrap; rebuilt fresh each play.
    /// </summary>
    public sealed class GameContent
    {
        public readonly ItemCatalogue Items;
        public readonly BlockCatalogue Blocks;

        private readonly ItemDefSO[] _itemVisuals;   // indexed by ItemId.Index
        private readonly BlockDefSO[] _blockVisuals; // indexed by BlockId.Index

        public GameContent(ItemCatalogue items, ItemDefSO[] itemVisuals,
                           BlockCatalogue blocks, BlockDefSO[] blockVisuals)
        {
            Items = items;
            _itemVisuals = itemVisuals;
            Blocks = blocks;
            _blockVisuals = blockVisuals;
        }

        // --- Items ---
        public ItemDefSO SOFor(ItemId id) => _itemVisuals[id.Index];
        public Sprite IconOf(ItemId id) => _itemVisuals[id.Index].icon;
        public Material MaterialOf(ItemId id) => _itemVisuals[id.Index].material;
        public Mesh MeshOf(ItemId id) => _itemVisuals[id.Index].mesh;
        public GameObject PrefabOf(ItemId id) => _itemVisuals[id.Index].prefab;

        // --- Blocks ---
        public BlockDefSO SOFor(BlockId id) => _blockVisuals[id.Index];
        public Mesh MeshOf(BlockId id) => _blockVisuals[id.Index].mesh;
        public Material MaterialOf(BlockId id) => _blockVisuals[id.Index].material;
        public Sprite IconOf(BlockId id) => _blockVisuals[id.Index].icon;
    }
}
