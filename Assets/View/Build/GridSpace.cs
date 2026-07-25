using UnityEngine;
using Aerow.Sim;

namespace Aerow.View.Build
{
    /// <summary>
    /// The one cell ↔ local-space mapping shared by placement, the mesher, and collider-gen — so
    /// geometry, colliders, and building always agree. Convention: cell (i,j,k) occupies the local
    /// box <c>[i·cs, (i+1)·cs)</c> on each axis; its centre is <c>(i+0.5)·cs</c>.
    /// </summary>
    public static class GridSpace
    {
        public const float CellSize = 0.25f;

        /// <summary>Local point → the cell that contains it.</summary>
        public static GridPos LocalToCell(Vector3 local) => new GridPos(
            Mathf.FloorToInt(local.x / CellSize),
            Mathf.FloorToInt(local.y / CellSize),
            Mathf.FloorToInt(local.z / CellSize));

        /// <summary>Cell → its centre in local space (for ghosts, block placement).</summary>
        public static Vector3 CellToLocal(GridPos cell) => new Vector3(
            (cell.X + 0.5f) * CellSize,
            (cell.Y + 0.5f) * CellSize,
            (cell.Z + 0.5f) * CellSize);

        /// <summary>Snap a (roughly axis-aligned) local direction to the nearest grid face direction.</summary>
        public static GridPos NearestAxis(Vector3 v)
        {
            float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
            if (ax >= ay && ax >= az) return v.x >= 0f ? GridPos.Right : GridPos.Left;
            if (ay >= az) return v.y >= 0f ? GridPos.Up : GridPos.Down;
            return v.z >= 0f ? GridPos.Forward : GridPos.Back;
        }
    }
}
