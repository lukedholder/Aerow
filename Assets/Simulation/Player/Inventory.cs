using System;

namespace Aerow.Sim
{
    /// <summary>
    /// Slot-based inventory used by the player, chests, and machine buffers. Pure C#,
    /// deterministic, allocation-free on the hot path. Per-item stack limits are read from
    /// the <see cref="ItemCatalogue"/>. The view reads slots each frame to render; it never
    /// mutates them directly — it calls <see cref="Add"/> / <see cref="Remove"/>.
    /// </summary>
    public sealed class Inventory
    {
        private readonly ItemStack[] _slots;
        private readonly ItemCatalogue _items;

        public int SlotCount => _slots.Length;

        public Inventory(int slotCount, ItemCatalogue items)
        {
            if (slotCount < 1) slotCount = 1;
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _slots = new ItemStack[slotCount];
            for (int i = 0; i < _slots.Length; i++) _slots[i] = ItemStack.Empty;
        }

        public ItemStack GetSlot(int index) => _slots[index];

        /// <summary>
        /// Add up to <paramref name="count"/> of <paramref name="item"/>. Tops up existing
        /// stacks before using empty slots. Returns the amount that did NOT fit.
        /// </summary>
        public int Add(ItemId item, int count)
        {
            if (!item.IsValid) return count > 0 ? count : 0;
            if (count <= 0) return 0;

            int max = _items.Get(item).MaxStack;

            // Pass 1: top up existing stacks of the same item.
            for (int i = 0; i < _slots.Length && count > 0; i++)
            {
                ref ItemStack s = ref _slots[i];
                if (s.Count > 0 && s.Item == item && s.Count < max)
                {
                    int room = max - s.Count;
                    int moved = room < count ? room : count;
                    s.Count += moved;
                    count -= moved;
                }
            }

            // Pass 2: fill empty slots.
            for (int i = 0; i < _slots.Length && count > 0; i++)
            {
                ref ItemStack s = ref _slots[i];
                if (s.Count <= 0)
                {
                    int moved = count < max ? count : max;
                    s.Item = item;
                    s.Count = moved;
                    count -= moved;
                }
            }

            return count; // leftover that didn't fit
        }

        /// <summary>
        /// Remove up to <paramref name="count"/> of <paramref name="item"/>.
        /// Returns the amount actually removed.
        /// </summary>
        public int Remove(ItemId item, int count)
        {
            if (!item.IsValid || count <= 0) return 0;

            int removed = 0;
            for (int i = 0; i < _slots.Length && removed < count; i++)
            {
                ref ItemStack s = ref _slots[i];
                if (s.Count > 0 && s.Item == item)
                {
                    int take = Math.Min(s.Count, count - removed);
                    s.Count -= take;
                    removed += take;
                    if (s.Count == 0) s.Item = ItemId.None;
                }
            }
            return removed;
        }

        /// <summary>Total quantity of <paramref name="item"/> across all slots.</summary>
        public int Count(ItemId item)
        {
            if (!item.IsValid) return 0;
            int total = 0;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Item == item) total += _slots[i].Count;
            return total;
        }

        public bool Has(ItemId item, int count) => Count(item) >= count;

        /// <summary>
        /// True if <paramref name="count"/> of <paramref name="item"/> could be fully added
        /// right now without overflowing. Does not mutate.
        /// </summary>
        public bool CanAdd(ItemId item, int count)
        {
            if (!item.IsValid || count <= 0) return true;

            int max = _items.Get(item).MaxStack;
            int room = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                ItemStack s = _slots[i];
                if (s.Count <= 0) room += max;
                else if (s.Item == item && s.Count < max) room += max - s.Count;
                if (room >= count) return true;
            }
            return room >= count;
        }

        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++) _slots[i] = ItemStack.Empty;
        }
    }
}
