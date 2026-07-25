namespace Aerow.View.UI
{
    /// <summary>
    /// Stable identifier for a managed panel. Grows as new interfaces are added
    /// (e.g. Machine, Research, Map). Machine interfaces will likely reuse a single
    /// <c>Machine</c> panel bound to whichever machine was opened.
    /// </summary>
    public enum MenuId
    {
        Pause,
        Build,
        Inventory,
    }
}
