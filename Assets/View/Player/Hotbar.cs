using UnityEngine;
using Aerow.Sim;

namespace Aerow.View
{
    /// <summary>
    /// View-side access point for the player's block hotbar. It holds <b>no state</b> — the bindings
    /// and the selected slot live in <c>GameBootstrap.Sim.Player</c>, so they're saved and can be
    /// rearranged at runtime.
    ///
    /// This component's job is the authoring bridge: the Inspector list of <see cref="BlockDefSO"/>
    /// assets is the *default* layout, written into the simulation once at startup (resolved by each
    /// asset's stable <c>stringId</c>, never list order).
    /// </summary>
    public class Hotbar : MonoBehaviour
    {
        private static Hotbar _instance;

        /// <summary>True when a Hotbar exists — check this to avoid <see cref="Instance"/>'s error log.</summary>
        public static bool Exists => _instance != null;

        public static Hotbar Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.LogError("Hotbar instance is null. Make sure there is a Hotbar component in the scene.");
                }

                return _instance;
            }
        }

        [Tooltip("Default blocks for hotbar keys 1..9 then 0 (the 10th slot), in order. " +
                 "Written into the simulation at startup; empty entries are left unbound.")]
        [SerializeField] private BlockDefSO[] defaultSlots = new BlockDefSO[10];

        /// <summary>Hotbar capacity, as configured on GameBootstrap.</summary>
        public int SlotCount => GameBootstrap.IsReady ? GameBootstrap.Sim.Player.HotbarSize : 0;

        /// <summary>Zero-based selected slot (key 1 → 0, key 0 → 9).</summary>
        public int SelectedSlot => GameBootstrap.IsReady ? GameBootstrap.Sim.Player.SelectedSlot : 0;

        public BlockDef SelectedBlock => Slot(SelectedSlot);

        public void Awake()
        {
            _instance = this;
        }

        // Start, not Awake: GameBootstrap builds the simulation in its own Awake.
        private void Start()
        {
            ApplyDefaults();
        }

        /// <summary>
        /// The block bound to a zero-based slot (key 1 → index 0, key 0 → index 9), or null if the
        /// slot is empty or out of range.
        /// </summary>
        public BlockDef Slot(int index)
        {
            if (!GameBootstrap.IsReady) return null;

            BlockId id = GameBootstrap.Sim.Player.GetHotbar(index);
            return id.IsValid ? GameBootstrap.Sim.Blocks.Get(id) : null;
        }

        /// <summary>
        /// Select a zero-based slot and return the block now held (null if the slot is empty).
        /// The selection is stored on the simulation's player state.
        /// </summary>
        public BlockDef Select(int index)
        {
            if (!GameBootstrap.IsReady) return null;

            GameBootstrap.Sim.Player.SelectedSlot = index;
            return Slot(index);
        }

        /// <summary>Bind a block to a slot at runtime (drag-and-drop later); null clears it.</summary>
        public void Bind(int index, BlockDef block)
        {
            if (!GameBootstrap.IsReady) return;
            GameBootstrap.Sim.Player.SetHotbar(index, block != null ? block.Id : BlockId.None);
        }

        /// <summary>Write the Inspector-authored default layout into the simulation.</summary>
        private void ApplyDefaults()
        {
            if (!GameBootstrap.IsReady)
            {
                Debug.LogError("[Hotbar] Simulation not ready — is a GameBootstrap in the scene?", this);
                return;
            }

            PlayerState player = GameBootstrap.Sim.Player;
            BlockCatalogue blocks = GameBootstrap.Sim.Blocks;
            int filled = 0;

            int count = Mathf.Min(defaultSlots.Length, player.HotbarSize);
            for (int i = 0; i < count; i++)
            {
                BlockDefSO so = defaultSlots[i];
                if (so == null) continue;

                if (blocks.TryGetId(so.stringId, out BlockId id))
                {
                    player.SetHotbar(i, id);
                    filled++;
                }
                else
                {
                    Debug.LogWarning($"[Hotbar] Slot {i + 1}: block '{so.stringId}' isn't registered — " +
                                     "is it listed in the ContentDatabase?", this);
                }
            }

            if (defaultSlots.Length > player.HotbarSize)
                Debug.LogWarning($"[Hotbar] {defaultSlots.Length} default slot(s) authored but the " +
                                 $"simulation has {player.HotbarSize} — extras ignored. " +
                                 "Raise 'Hotbar Slots' on GameBootstrap.", this);

            Debug.Log($"[Hotbar] Bound {filled}/{player.HotbarSize} slot(s)."); // TEMP DEBUG
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
