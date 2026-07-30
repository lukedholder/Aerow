using System;

namespace Aerow.Sim
{
    /// <summary>
    /// Everything about the player that belongs to the simulation and must survive a save:
    /// their inventory, hotbar bindings, and which slot is selected. Pure data + rules — the view
    /// renders it and calls in to change it.
    /// </summary>
    public sealed class PlayerState
    {
        public readonly Inventory Inventory;

        private readonly BlockId[] _hotbar;
        private int _selectedSlot;

        public PlayerState(int inventorySlots, int hotbarSlots, ItemCatalogue items)
        {
            if (hotbarSlots < 1) hotbarSlots = 1;

            Inventory = new Inventory(inventorySlots, items);

            // default(BlockId) is BlockId(0) — a *valid* id — so fill with None explicitly.
            _hotbar = new BlockId[hotbarSlots];
            for (int i = 0; i < _hotbar.Length; i++) _hotbar[i] = BlockId.None;
        }

        public int HotbarSize => _hotbar.Length;

        /// <summary>Which hotbar slot the player has selected. Assignment is clamped in range.</summary>
        public int SelectedSlot
        {
            get => _selectedSlot;
            set => _selectedSlot = value < 0 ? 0 : (value >= _hotbar.Length ? _hotbar.Length - 1 : value);
        }

        /// <summary>Block bound to a hotbar slot, or <see cref="BlockId.None"/> if empty/out of range.</summary>
        public BlockId GetHotbar(int index) =>
            index >= 0 && index < _hotbar.Length ? _hotbar[index] : BlockId.None;

        /// <summary>Bind a block to a hotbar slot (pass <see cref="BlockId.None"/> to clear it).</summary>
        public void SetHotbar(int index, BlockId block)
        {
            if (index < 0 || index >= _hotbar.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            _hotbar[index] = block;
        }

        /// <summary>The block in the selected slot, or <see cref="BlockId.None"/>.</summary>
        public BlockId SelectedBlock => GetHotbar(_selectedSlot);
    }
}
