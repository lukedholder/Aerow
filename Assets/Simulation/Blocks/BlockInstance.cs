namespace Aerow.Sim
{
    /// <summary>
    /// A placed block: which type (<see cref="Def"/>), its anchor cell, orientation, and runtime
    /// health. Value type stored in the construct's slot array; carries a <see cref="BlockId"/>
    /// handle, not a def reference. The anchor cell is always one of the block's occupied cells.
    /// </summary>
    public struct BlockInstance
    {
        public BlockId Def;
        public GridPos LocalPosition;
        public BlockOrientation Orientation;
        public int Health;

        public BlockInstance(BlockId def, GridPos localPosition, BlockOrientation orientation, int health)
        {
            Def = def;
            LocalPosition = localPosition;
            Orientation = orientation;
            Health = health;
        }

        /// <summary>False marks a tombstoned (removed) slot.</summary>
        public bool IsAlive => Def.IsValid;

        public static readonly BlockInstance Empty =
            new BlockInstance(BlockId.None, GridPos.Zero, BlockOrientation.Identity, 0);
    }
}
