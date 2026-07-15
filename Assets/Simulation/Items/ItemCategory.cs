namespace Aerow.Sim
{
    /// <summary>
    /// Broad classification of an item, used for UI grouping and coarse gameplay rules
    /// (e.g. what a filter accepts, what a weapon consumes). Not a substitute for the
    /// item's own <see cref="ItemDef"/>.
    /// </summary>
    public enum ItemCategory
    {
        /// Mined or harvested straight from the world (ores, stone, wood).
        Raw,

        /// Smelted or processed base materials (plates, ingots).
        Refined,

        /// Intermediate crafted parts (gears, wire, circuits).
        Component,

        /// Consumed by weapons and turrets.
        Ammunition,

        /// Liquids and gases. Treated as discrete items for now; may split into a
        /// dedicated fluid system later.
        Fluid,
    }
}
