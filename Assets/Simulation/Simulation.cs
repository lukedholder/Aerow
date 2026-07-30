using System;
using System.Collections.Generic;

namespace Aerow.Sim
{
    /// <summary>
    /// The root of the simulation — the whole savable game world in one pure-C# object: content
    /// catalogues, player state, and every construct. The view owns exactly one instance of this
    /// and never keeps game state of its own.
    ///
    /// Construct ids are allocated here, so constructs are always reachable by id (needed for
    /// save/load, rotor joints, and splitting).
    /// </summary>
    public sealed class Simulation
    {
        public readonly ItemCatalogue Items;
        public readonly BlockCatalogue Blocks;
        public readonly PlayerState Player;

        private readonly Dictionary<int, Construct> _constructs = new Dictionary<int, Construct>();
        private int _nextConstructId = 1;

        public Simulation(ItemCatalogue items, BlockCatalogue blocks, int inventorySlots, int hotbarSlots)
        {
            Items = items ?? throw new ArgumentNullException(nameof(items));
            Blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
            Player = new PlayerState(inventorySlots, hotbarSlots, items);
        }

        // ── Constructs ──

        public int ConstructCount => _constructs.Count;

        /// <summary>All live constructs. Order is unspecified; look up by id for stable access.</summary>
        public IEnumerable<Construct> Constructs => _constructs.Values;

        /// <summary>Create an empty construct, registered under a fresh id.</summary>
        public Construct CreateConstruct()
        {
            var c = new Construct(Blocks, _nextConstructId++);
            _constructs.Add(c.Id, c);
            return c;
        }

        /// <summary>
        /// Register a construct that was built outside the simulation — notably the pieces returned
        /// by <see cref="Construct.Split"/>, which arrive without an id. Assigns a fresh id.
        /// </summary>
        public Construct Adopt(Construct construct)
        {
            if (construct == null) throw new ArgumentNullException(nameof(construct));

            construct.Id = _nextConstructId++;
            _constructs.Add(construct.Id, construct);
            return construct;
        }

        public Construct GetConstruct(int id) => _constructs[id];

        public bool TryGetConstruct(int id, out Construct construct) =>
            _constructs.TryGetValue(id, out construct);

        /// <summary>Forget a construct (e.g. its last block was removed). True if it existed.</summary>
        public bool RemoveConstruct(int id) => _constructs.Remove(id);
    }
}
