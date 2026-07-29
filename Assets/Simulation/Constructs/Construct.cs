using System;
using System.Collections.Generic;

namespace Aerow.Sim
{
    /// <summary>
    /// A grid of blocks (pure sim). Owns its local integer block layout, an occupancy index for
    /// O(1) cell lookups, and derived mass/bounds. Placement/removal keep occupancy in sync;
    /// removal can disconnect the construct, which <see cref="Split"/> resolves into new pieces.
    /// Deterministic and Unity-free — the view mirrors it (mesh, colliders, Rigidbody, transform).
    ///
    /// Storage uses stable slots: a removed block leaves a tombstone that is reused on the next
    /// add, so occupancy indices never dangle.
    /// </summary>
    public sealed class Construct
    {
        private readonly BlockCatalogue _blocks;

        private readonly List<BlockInstance> _slots = new List<BlockInstance>();
        private readonly List<int> _freeSlots = new List<int>();
        private readonly Dictionary<GridPos, int> _occupancy = new Dictionary<GridPos, int>();

        private bool _hasBounds;

        public int Id { get; internal set; }
        public bool IsAnchored;

        public int BlockCount { get; private set; }
        public float Mass { get; private set; }
        public GridPos BoundsMin { get; private set; }
        public GridPos BoundsMax { get; private set; }

        public Construct(BlockCatalogue blocks, int id = 0)
        {
            _blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
            Id = id;
        }

        /// <summary>Live blocks in slot order (tombstones skipped).</summary>
        public IEnumerable<BlockInstance> Blocks
        {
            get
            {
                for (int i = 0; i < _slots.Count; i++)
                    if (_slots[i].IsAlive) yield return _slots[i];
            }
        }

        public bool IsOccupied(GridPos cell) => _occupancy.ContainsKey(cell);

        /// <summary>All occupied cells (unordered). Used by the view mesher and collider-gen.</summary>
        public IEnumerable<GridPos> OccupiedCells => _occupancy.Keys;

        /// <summary>Slot index occupying a cell, or -1.</summary>
        public int SlotAt(GridPos cell) => _occupancy.TryGetValue(cell, out int i) ? i : -1;

        public bool TryGetBlock(GridPos cell, out BlockInstance block)
        {
            int i = SlotAt(cell);
            if (i >= 0) { block = _slots[i]; return true; }
            block = BlockInstance.Empty;
            return false;
        }

        // ── Placement ──

        /// <summary>
        /// True if <paramref name="def"/> can be placed at <paramref name="anchor"/> with
        /// <paramref name="orientation"/>: every covered cell is free, and it touches an existing
        /// block (unless the construct is empty — the first block seeds it).
        /// </summary>
        public bool CanPlace(BlockId def, GridPos anchor, BlockOrientation orientation)
        {
            if (!def.IsValid || !_blocks.TryGet(def, out BlockDef d)) return false;

            foreach (GridPos cell in Footprint(d.Size, anchor, orientation))
                if (_occupancy.ContainsKey(cell)) return false;

            if (BlockCount > 0 && !TouchesExisting(d.Size, anchor, orientation)) return false;
            return true;
        }

        public bool PlaceBlock(BlockId def, GridPos anchor, BlockOrientation orientation)
        {
            if (!CanPlace(def, anchor, orientation)) return false;
            BlockDef d = _blocks.Get(def);
            AddBlockDirect(new BlockInstance(def, anchor, orientation, d.MaxHealth));
            return true;
        }

        /// <summary>Remove the block occupying <paramref name="cell"/>. Returns false if none.</summary>
        public bool RemoveBlock(GridPos cell)
        {
            int slot = SlotAt(cell);
            if (slot < 0) return false;
            ClearSlot(slot);
            RecomputeBounds(); // removal can shrink the bounds
            return true;
        }

        // ── Split ──

        /// <summary>
        /// If the blocks form more than one face-connected component, keep the component containing
        /// the lowest-indexed block and move each other component into a new <see cref="Construct"/>
        /// (blocks keep their local coordinates). Returns the new pieces (empty if still one piece).
        /// Deterministic: components are numbered by ascending slot index.
        /// </summary>
        public List<Construct> Split()
        {
            var pieces = new List<Construct>();
            if (BlockCount <= 1) return pieces;

            var liveSlots = new List<int>();
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].IsAlive) liveSlots.Add(i);

            var component = new Dictionary<int, int>();
            int numComponents = 0;
            foreach (int start in liveSlots) // ascending → deterministic component numbering
            {
                if (component.ContainsKey(start)) continue;
                int id = numComponents++;
                var queue = new Queue<int>();
                queue.Enqueue(start);
                component[start] = id;
                while (queue.Count > 0)
                {
                    int s = queue.Dequeue();
                    foreach (int n in AdjacentSlots(s))
                        if (!component.ContainsKey(n)) { component[n] = id; queue.Enqueue(n); }
                }
            }

            if (numComponents <= 1) return pieces; // still connected

            var buckets = new List<List<int>>();
            for (int i = 0; i < numComponents; i++) buckets.Add(new List<int>());
            foreach (int s in liveSlots) buckets[component[s]].Add(s);

            // Component 0 contains the lowest slot → stays here. Move the rest.
            for (int id = 1; id < numComponents; id++)
            {
                var piece = new Construct(_blocks);
                foreach (int slot in buckets[id])
                {
                    piece.AddBlockDirect(_slots[slot]);
                    ClearSlot(slot);
                }
                pieces.Add(piece);
            }

            RecomputeBounds();
            return pieces;
        }

        // ── Internals ──

        /// <summary>Cells a block covers: its Size box, rotated by orientation, offset by anchor.</summary>
        public static IEnumerable<GridPos> Footprint(GridPos size, GridPos anchor, BlockOrientation orientation)
        {
            for (int x = 0; x < size.X; x++)
                for (int y = 0; y < size.Y; y++)
                    for (int z = 0; z < size.Z; z++)
                        yield return anchor + orientation.Rotate(new GridPos(x, y, z));
        }

        private bool TouchesExisting(GridPos size, GridPos anchor, BlockOrientation orientation)
        {
            foreach (GridPos cell in Footprint(size, anchor, orientation))
                foreach (GridPos dir in GridPos.FaceDirections)
                    if (_occupancy.ContainsKey(cell + dir)) return true;
            return false;
        }

        /// <summary>Distinct slots face-adjacent to the block in <paramref name="slot"/> (excluding itself).</summary>
        private IEnumerable<int> AdjacentSlots(int slot)
        {
            BlockInstance inst = _slots[slot];
            BlockDef d = _blocks.Get(inst.Def);
            var seen = new HashSet<int>();
            foreach (GridPos cell in Footprint(d.Size, inst.LocalPosition, inst.Orientation))
                foreach (GridPos dir in GridPos.FaceDirections)
                {
                    int n = SlotAt(cell + dir);
                    if (n >= 0 && n != slot && seen.Add(n)) yield return n;
                }
        }

        /// <summary>Add a block with no validation (caller guarantees it fits and connects).</summary>
        private void AddBlockDirect(BlockInstance inst)
        {
            BlockDef d = _blocks.Get(inst.Def);
            int slot = TakeSlot(inst);
            foreach (GridPos cell in Footprint(d.Size, inst.LocalPosition, inst.Orientation))
            {
                _occupancy[cell] = slot;
                ExpandBounds(cell);
            }
            BlockCount++;
            Mass += d.Mass;
        }

        /// <summary>Remove a slot's block (occupancy + tombstone + mass/count). Bounds handled by caller.</summary>
        private void ClearSlot(int slot)
        {
            BlockInstance inst = _slots[slot];
            BlockDef d = _blocks.Get(inst.Def);
            foreach (GridPos cell in Footprint(d.Size, inst.LocalPosition, inst.Orientation))
                _occupancy.Remove(cell);
            _slots[slot] = BlockInstance.Empty;
            _freeSlots.Add(slot);
            BlockCount--;
            Mass -= d.Mass;
        }

        private int TakeSlot(BlockInstance inst)
        {
            if (_freeSlots.Count > 0)
            {
                int s = _freeSlots[_freeSlots.Count - 1];
                _freeSlots.RemoveAt(_freeSlots.Count - 1);
                _slots[s] = inst;
                return s;
            }
            _slots.Add(inst);
            return _slots.Count - 1;
        }

        private void ExpandBounds(GridPos c)
        {
            if (!_hasBounds)
            {
                BoundsMin = c;
                BoundsMax = c;
                _hasBounds = true;
                return;
            }
            BoundsMin = new GridPos(Math.Min(BoundsMin.X, c.X), Math.Min(BoundsMin.Y, c.Y), Math.Min(BoundsMin.Z, c.Z));
            BoundsMax = new GridPos(Math.Max(BoundsMax.X, c.X), Math.Max(BoundsMax.Y, c.Y), Math.Max(BoundsMax.Z, c.Z));
        }

        private void RecomputeBounds()
        {
            _hasBounds = false;
            foreach (GridPos c in _occupancy.Keys) ExpandBounds(c);
            if (!_hasBounds) { BoundsMin = GridPos.Zero; BoundsMax = GridPos.Zero; }
        }
    }
}
