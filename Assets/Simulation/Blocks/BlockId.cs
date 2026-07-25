using System;

namespace Aerow.Sim
{
    /// <summary>
    /// Lightweight handle to a <see cref="BlockDef"/> in a <see cref="BlockCatalogue"/>. Stored on
    /// every placed block; cheap to copy and serialize. Mirrors <see cref="ItemId"/>. Use the def's
    /// stable <see cref="BlockDef.StringId"/> for saves; the index is a runtime value.
    /// </summary>
    public readonly struct BlockId : IEquatable<BlockId>
    {
        public readonly int Index;

        public BlockId(int index)
        {
            Index = index;
        }

        public static readonly BlockId None = new BlockId(-1);
        public bool IsValid => Index >= 0;

        public bool Equals(BlockId other) => Index == other.Index;
        public override bool Equals(object obj) => obj is BlockId other && Equals(other);
        public override int GetHashCode() => Index;

        public static bool operator ==(BlockId a, BlockId b) => a.Index == b.Index;
        public static bool operator !=(BlockId a, BlockId b) => a.Index != b.Index;

        public override string ToString() => $"BlockId({Index})";
    }
}
