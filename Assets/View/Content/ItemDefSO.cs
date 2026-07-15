using UnityEngine;
using Aerow.Sim;

namespace Aerow.View
{
    /// <summary>
    /// Authoring asset for one item — the single source of truth. Holds both the gameplay
    /// data (copied into a pure-C# <see cref="ItemDef"/> for the sim via
    /// <see cref="ToDefinition"/>) and the visuals (which never enter the sim).
    /// Create via <c>Assets ▸ Create ▸ Aerow ▸ Item Definition</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Aerow/Item Definition", fileName = "Item_")]
    public sealed class ItemDefSO : ScriptableObject
    {
        [Header("Identity — shared with the simulation")]
        [Tooltip("Stable unique key used by the sim, saves, and recipe references. " +
                 "e.g. \"iron_plate\". Lowercase, no spaces. Changing it after saves exist breaks those saves.")]
        public string stringId;

        public string displayName;
        public ItemCategory category = ItemCategory.Raw;

        [Min(1)]
        [Tooltip("Maximum quantity of this item in a single inventory slot.")]
        public int maxStack = 100;

        [TextArea]
        public string description;

        [Header("Visuals — View only, never cross into the sim")]
        public Sprite icon;
        public Material material;
        public Mesh mesh;
        public GameObject prefab;

        /// <summary>Copy the gameplay half into a pure-C# def for the simulation.</summary>
        public ItemDef ToDefinition()
            => new ItemDef(stringId, displayName, category, maxStack, description);

        private void OnValidate()
        {
            // Convenience: default the display name to the asset name when left blank.
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name;
        }
    }
}
