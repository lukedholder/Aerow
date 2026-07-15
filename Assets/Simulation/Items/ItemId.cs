using System;

namespace Aerow.Sim
{
    /// <summary>
    /// Lightweight handle to an <see cref="ItemDef"/> in an <see cref="ItemCatalogue"/>.
    /// Stored on belts, in inventory slots, in recipes — cheap to copy and cache-friendly.
    /// The integer index is a runtime value; for saves and content references use the
    /// def's stable <see cref="ItemDef.StringId"/> instead.
    /// </summary>
    public readonly struct ItemId : IEquatable<ItemId>
    {
        public readonly int Index;

        public ItemId(int index)
        {
            Index = index;
        }

        public static readonly ItemId None = new ItemId(-1);
        public bool IsValid => Index >= 0;

        public bool Equals(ItemId other) => Index == other.Index;
        public override bool Equals(object obj) => obj is ItemId other && Equals(other);
        public override int GetHashCode() => Index;

        public static bool operator ==(ItemId a, ItemId b) => a.Index == b.Index;
        public static bool operator !=(ItemId a, ItemId b) => a.Index != b.Index;

        public override string ToString() => $"ItemId({Index})";
    }
}
