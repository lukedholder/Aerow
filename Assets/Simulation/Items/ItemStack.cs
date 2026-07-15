namespace Aerow.Sim
{
    /// <summary>
    /// A quantity of one item type. Mutable value type — used directly inside inventory
    /// slot arrays for cache-friendly, allocation-free storage.
    /// </summary>
    public struct ItemStack
    {
        public ItemId Item;
        public int Count;

        public ItemStack(ItemId item, int count)
        {
            Item = item;
            Count = count;
        }

        public static readonly ItemStack Empty = new ItemStack(ItemId.None, 0);

        public bool IsEmpty => Count <= 0 || !Item.IsValid;

        public void Clear()
        {
            Item = ItemId.None;
            Count = 0;
        }

        public override string ToString() => IsEmpty ? "Empty" : $"{Count}x {Item}";
    }
}
