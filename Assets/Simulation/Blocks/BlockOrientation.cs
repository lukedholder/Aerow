using System;
using System.Collections.Generic;

namespace Aerow.Sim
{
    /// <summary>
    /// One of the 24 axis-aligned cube orientations (90° steps). Rotates a block's local grid
    /// offsets into the construct frame — blocks stay grid-snapped; only the construct's transform
    /// rotates continuously (that lives on the view/joint, not here).
    ///
    /// The 24 rotations are generated once in a fixed order (Identity = index 0), so indices are
    /// deterministic and stable for saves.
    /// </summary>
    public readonly struct BlockOrientation : IEquatable<BlockOrientation>
    {
        public const int Count = 24;

        public readonly byte Index;
        private BlockOrientation(int index) { Index = (byte)index; }

        public static readonly BlockOrientation Identity = new BlockOrientation(0);

        public static BlockOrientation FromIndex(int index) =>
            new BlockOrientation(((index % Count) + Count) % Count);

        /// <summary>Next orientation, cycling 0..23 — handy for a "rotate block" key.</summary>
        public BlockOrientation Next() => new BlockOrientation((Index + 1) % Count);

        /// <summary>Rotate a local offset into this orientation's frame.</summary>
        public GridPos Rotate(GridPos v)
        {
            Rot r = Rots[Index];
            return r.ColX * v.X + r.ColY * v.Y + r.ColZ * v.Z;
        }

        public bool Equals(BlockOrientation other) => Index == other.Index;
        public override bool Equals(object obj) => obj is BlockOrientation other && Equals(other);
        public override int GetHashCode() => Index;
        public override string ToString() => $"Orientation({Index})";

        // ── Rotation table, generated from two 90° generators ──

        /// <summary>A proper cube rotation as the images of the x/y/z basis vectors (its columns).</summary>
        private readonly struct Rot
        {
            public readonly GridPos ColX, ColY, ColZ;
            public Rot(GridPos x, GridPos y, GridPos z) { ColX = x; ColY = y; ColZ = z; }

            public GridPos Apply(GridPos v) => ColX * v.X + ColY * v.Y + ColZ * v.Z;

            /// <summary>this ∘ other (apply <paramref name="other"/> first, then this).</summary>
            public Rot Compose(Rot other) => new Rot(Apply(other.ColX), Apply(other.ColY), Apply(other.ColZ));

            /// <summary>Unique key for dedup (each column is a signed unit axis → 0..26).</summary>
            public long Key() => Enc(ColX) * 1000000L + Enc(ColY) * 1000L + Enc(ColZ);
            private static long Enc(GridPos p) => (p.X + 1) * 9 + (p.Y + 1) * 3 + (p.Z + 1);
        }

        private static readonly Rot[] Rots = Generate();

        private static Rot[] Generate()
        {
            var identity = new Rot(GridPos.Right, GridPos.Up, GridPos.Forward);
            var rx = new Rot(GridPos.Right, GridPos.Forward, GridPos.Down); // 90° about X: y→z, z→-y
            var ry = new Rot(GridPos.Back, GridPos.Up, GridPos.Right);      // 90° about Y: x→-z, z→x
            var generators = new[] { rx, ry };

            var list = new List<Rot> { identity };
            var seen = new HashSet<long> { identity.Key() };

            // Breadth-first closure over the generators → all 24, Identity first.
            for (int i = 0; i < list.Count; i++)
                foreach (Rot g in generators)
                {
                    Rot n = g.Compose(list[i]);
                    if (seen.Add(n.Key())) list.Add(n);
                }

            return list.ToArray();
        }
    }
}
