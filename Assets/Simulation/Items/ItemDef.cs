namespace Aerow.Sim
{
    /// <summary>
    /// Immutable definition of an item type. Authored in the Editor as an ItemDefSO and
    /// registered into an <see cref="ItemCatalogue"/>, which assigns the runtime
    /// <see cref="Id"/>. Never mutated after registration.
    /// </summary>
    public sealed class ItemDef
    {
        /// <summary>
        /// Stable, human-readable key used for saves and content references
        /// (e.g. "iron_plate"). The runtime <see cref="Id"/> is not stable across content
        /// changes; this string is the durable identity.
        /// </summary>
        public readonly string StringId;

        public readonly string DisplayName;
        public readonly ItemCategory Category;

        /// <summary>Maximum quantity of this item in a single inventory slot.</summary>
        public readonly int MaxStack;

        public readonly string Description;

        /// <summary>Runtime handle, assigned by the catalogue on registration.</summary>
        public ItemId Id { get; internal set; }

        public ItemDef(string stringId, string displayName, ItemCategory category, int maxStack, string description = "")
        {
            StringId = stringId;
            DisplayName = displayName;
            Category = category;
            MaxStack = maxStack < 1 ? 1 : maxStack;
            Description = description;
            Id = ItemId.None;
        }
    }
}
