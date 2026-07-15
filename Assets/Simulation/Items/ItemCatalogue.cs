using System;
using System.Collections.Generic;

namespace Aerow.Sim
{
    /// <summary>
    /// Runtime registry of all item types. Populated once at load (from code or a
    /// ScriptableObject bridge), then effectively read-only. Lookup by runtime
    /// <see cref="ItemId"/> is O(1); lookup by <see cref="ItemDef.StringId"/> (for saves
    /// and content) goes through a dictionary.
    /// </summary>
    public sealed class ItemCatalogue
    {
        private readonly List<ItemDef> _defs = new List<ItemDef>();
        private readonly Dictionary<string, ItemId> _byStringId = new Dictionary<string, ItemId>();

        public int Count => _defs.Count;

        /// <summary>All defs in registration (Id) order — deterministic iteration.</summary>
        public IReadOnlyList<ItemDef> All => _defs;

        public ItemId Register(ItemDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (_byStringId.ContainsKey(def.StringId))
                throw new InvalidOperationException($"Duplicate item StringId '{def.StringId}'.");

            var id = new ItemId(_defs.Count);
            def.Id = id;
            _defs.Add(def);
            _byStringId.Add(def.StringId, id);
            return id;
        }

        public ItemDef Get(ItemId id) => _defs[id.Index];

        public bool TryGet(ItemId id, out ItemDef def)
        {
            if (id.Index >= 0 && id.Index < _defs.Count)
            {
                def = _defs[id.Index];
                return true;
            }
            def = null;
            return false;
        }

        public bool TryGetId(string stringId, out ItemId id) => _byStringId.TryGetValue(stringId, out id);

        public ItemId GetId(string stringId)
        {
            if (_byStringId.TryGetValue(stringId, out var id)) return id;
            throw new KeyNotFoundException($"No item registered with StringId '{stringId}'.");
        }
    }
}
