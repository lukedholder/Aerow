using System;
using UnityEngine;
using Aerow.Sim;

namespace Aerow.View
{
    /// <summary>
    /// Authoring asset for one block — the single source of truth. Holds the gameplay data (copied
    /// into a pure-C# <see cref="BlockDef"/> via <see cref="ToDefinition"/>) and the visuals (which
    /// never enter the sim). Mirrors <see cref="ItemDefSO"/>.
    /// Create via <c>Assets ▸ Create ▸ Aerow ▸ Block Definition</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Aerow/Block Definition", fileName = "Block_")]
    public sealed class BlockDefSO : ScriptableObject
    {
        [Serializable]
        public struct CostEntry
        {
            public ItemDefSO item;
            [Min(1)] public int count;
        }

        [Header("Identity — shared with the simulation")]
        [Tooltip("Stable unique key used by the sim and saves, e.g. \"armor_cube\".")]
        public string stringId;
        public string displayName;
        public BlockCategory category = BlockCategory.Structural;

        [Header("Shape / stats")]
        [Tooltip("Footprint in cells (each axis >= 1).")]
        public Vector3Int size = new Vector3Int(1, 1, 1);
        [Min(1)] public int maxHealth = 100;
        public float mass = 10f;
        [Tooltip("Items consumed to build one.")]
        public CostEntry[] buildCost;

        [Header("Visuals — View only, never cross into the sim")]
        public Mesh mesh;
        public Material material;
        public Sprite icon;

        public BlockDef ToDefinition(ItemCatalogue items)
            => new BlockDef(
                stringId, displayName, category,
                new GridPos(Mathf.Max(1, size.x), Mathf.Max(1, size.y), Mathf.Max(1, size.z)),
                maxHealth, mass, ResolveCost(items));

        private ItemStack[] ResolveCost(ItemCatalogue items)
        {
            if (buildCost == null || buildCost.Length == 0) return Array.Empty<ItemStack>();
            var arr = new ItemStack[buildCost.Length];
            for (int i = 0; i < buildCost.Length; i++)
            {
                CostEntry e = buildCost[i];
                if (e.item == null)
                    throw new InvalidOperationException($"Block '{stringId}' has a build-cost entry with no item assigned.");
                arr[i] = new ItemStack(items.GetId(e.item.stringId), e.count);
            }
            return arr;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName)) displayName = name;
            size = new Vector3Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y), Mathf.Max(1, size.z));
        }
    }
}
