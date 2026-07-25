namespace Aerow.Sim
{
    /// <summary>
    /// Broad classification of a block, used for UI grouping and coarse rules (which systems
    /// iterate it, what a tool filters). Not a substitute for the block's own <see cref="BlockDef"/>.
    /// </summary>
    public enum BlockCategory
    {
        Structural,
        Armor,
        Thruster,
        Weapon,
        Cockpit,
        Container,
    }
}
