using System;

namespace Aerow.Sim
{
    /// <summary>
    /// Immutable definition of a block type. Authored in the Editor as a BlockDefSO and registered
    /// into a <see cref="BlockCatalogue"/>, which assigns the runtime <see cref="Id"/>. Mirrors
    /// <see cref="ItemDef"/>. Never mutated after registration.
    /// </summary>
    public sealed class BlockDef
    {
        /// <summary>Stable key for saves and content references (e.g. "armor_cube").</summary>
        public readonly string StringId;
        public readonly string DisplayName;
        public readonly BlockCategory Category;

        /// <summary>Bounding footprint in the block's local frame (each axis &gt;= 1).</summary>
        public readonly GridPos Size;

        public readonly int MaxHealth;
        public readonly float Mass;

        /// <summary>Items consumed to build one (checked/deducted against an inventory by the view).</summary>
        public readonly ItemStack[] BuildCost;

        /// <summary>Reserved for future connectivity refinement; v1 connectivity uses face-adjacency.</summary>
        public readonly GridPos[] ConnectionPoints;

        /// <summary>Runtime handle, assigned by the catalogue on registration.</summary>
        public BlockId Id { get; internal set; }

        public BlockDef(string stringId, string displayName, BlockCategory category, GridPos size,
                        int maxHealth, float mass, ItemStack[] buildCost = null, GridPos[] connectionPoints = null)
        {
            StringId = stringId;
            DisplayName = displayName;
            Category = category;
            Size = new GridPos(Math.Max(1, size.X), Math.Max(1, size.Y), Math.Max(1, size.Z));
            MaxHealth = maxHealth < 1 ? 1 : maxHealth;
            Mass = mass;
            BuildCost = buildCost ?? Array.Empty<ItemStack>();
            ConnectionPoints = connectionPoints ?? Array.Empty<GridPos>();
            Id = BlockId.None;
        }
    }
}
