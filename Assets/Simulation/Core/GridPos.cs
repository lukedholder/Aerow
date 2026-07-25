using System;

namespace Aerow.Sim
{
    /// <summary>
    /// Integer grid coordinate — the canonical position type in the simulation. The sim never
    /// uses <c>Vector3</c>; everything spatial is whole cells. Value type, deterministic hashing.
    /// </summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public GridPos(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly GridPos Zero = new GridPos(0, 0, 0);
        public static readonly GridPos Right = new GridPos(1, 0, 0);
        public static readonly GridPos Left = new GridPos(-1, 0, 0);
        public static readonly GridPos Up = new GridPos(0, 1, 0);
        public static readonly GridPos Down = new GridPos(0, -1, 0);
        public static readonly GridPos Forward = new GridPos(0, 0, 1);
        public static readonly GridPos Back = new GridPos(0, 0, -1);

        /// <summary>The six face-neighbour directions, in a fixed order.</summary>
        public static readonly GridPos[] FaceDirections = { Right, Left, Up, Down, Forward, Back };

        public static GridPos operator +(GridPos a, GridPos b) => new GridPos(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static GridPos operator -(GridPos a, GridPos b) => new GridPos(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static GridPos operator *(GridPos a, int s) => new GridPos(a.X * s, a.Y * s, a.Z * s);
        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);
        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);

        public int ManhattanTo(GridPos other) =>
            Math.Abs(X - other.X) + Math.Abs(Y - other.Y) + Math.Abs(Z - other.Z);

        public bool Equals(GridPos other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is GridPos other && Equals(other);

        public override int GetHashCode()
        {
            // Deterministic hash — never rely on runtime-varying hashing in the sim.
            unchecked
            {
                int h = 17;
                h = h * 31 + X;
                h = h * 31 + Y;
                h = h * 31 + Z;
                return h;
            }
        }

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
