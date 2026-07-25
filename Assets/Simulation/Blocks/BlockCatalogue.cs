using System;
using System.Collections.Generic;

namespace Aerow.Sim
{
    /// <summary>
    /// Runtime registry of all block types. Same shape as <see cref="ItemCatalogue"/>: O(1) lookup
    /// by <see cref="BlockId"/>, dictionary lookup by stable <see cref="BlockDef.StringId"/>.
    /// </summary>
    public sealed class BlockCatalogue
    {
        private readonly List<BlockDef> _defs = new List<BlockDef>();
        private readonly Dictionary<string, BlockId> _byStringId = new Dictionary<string, BlockId>();

        public int Count => _defs.Count;
        public IReadOnlyList<BlockDef> All => _defs;

        public BlockId Register(BlockDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (_byStringId.ContainsKey(def.StringId))
                throw new InvalidOperationException($"Duplicate block StringId '{def.StringId}'.");

            var id = new BlockId(_defs.Count);
            def.Id = id;
            _defs.Add(def);
            _byStringId.Add(def.StringId, id);
            return id;
        }

        public BlockDef Get(BlockId id) => _defs[id.Index];

        public bool TryGet(BlockId id, out BlockDef def)
        {
            if (id.Index >= 0 && id.Index < _defs.Count)
            {
                def = _defs[id.Index];
                return true;
            }
            def = null;
            return false;
        }

        public bool TryGetId(string stringId, out BlockId id) => _byStringId.TryGetValue(stringId, out id);

        public BlockId GetId(string stringId)
        {
            if (_byStringId.TryGetValue(stringId, out var id)) return id;
            throw new KeyNotFoundException($"No block registered with StringId '{stringId}'.");
        }
    }
}
