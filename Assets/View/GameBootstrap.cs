using UnityEngine;
using Aerow.Sim;
using Aerow.View.Input;

namespace Aerow.View
{
    /// <summary>
    /// Runtime entry point: loads game content and brings the input layer online at startup,
    /// and exposes content to the rest of the game. Put one on a GameObject in your startup
    /// scene and assign the ContentDatabase asset in the Inspector. It runs no simulation and
    /// no update loop.
    /// </summary>
    [DefaultExecutionOrder(-1000)] // build content before other scripts' Awake run
    public sealed class GameBootstrap : MonoBehaviour
    {
        private static GameBootstrap _instance;

        [SerializeField]
        [Tooltip("The master ContentDatabase asset listing every item SO.")]
        private ContentDatabase contentDatabase;

        [Header("Starting player state")]
        [Tooltip("Inventory slots the player starts with (grows via Inventory.Grow).")]
        [Min(1)]
        [SerializeField] private int startingInventorySlots = 20;

        [Tooltip("Hotbar slots — keys 1..9 then 0.")]
        [Min(1)]
        [SerializeField] private int hotbarSlots = 10;

        /// <summary>
        /// The loaded content: the sim's item catalogue plus the view-side visual lookup.
        /// Null until <see cref="Awake"/> succeeds — read it from Start onward, or guard with
        /// <see cref="IsReady"/>.
        /// </summary>
        public static GameContent Content { get; private set; }

        /// <summary>
        /// The one simulation instance — the whole game world. All game state lives here, never on
        /// a MonoBehaviour; the view reads it to render and calls into it to change things.
        /// </summary>
        public static Simulation Sim { get; private set; }

        /// <summary>True once content and the simulation are built and safe to read.</summary>
        public static bool IsReady => Content != null && Sim != null;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // A bootstrap already exists (e.g. an additively loaded scene). Keep the first.
                Destroy(gameObject);
                return;
            }
            _instance = this;

            Debug.Log("[GameBootstrap] Bootstrapping…", this); // TEMP DEBUG

            // Bring the input layer online. Independent of content, so it runs even if the
            // content build below fails — you still want the game controllable.
            GameInput.Initialize();
            Debug.Log("[GameBootstrap] Input layer online.", this); // TEMP DEBUG

            if (contentDatabase == null)
            {
                Debug.LogError("[GameBootstrap] No ContentDatabase assigned — drag the asset " +
                               "into the Content Database field in the Inspector.", this);
                enabled = false;
                return;
            }

            try
            {
                Content = contentDatabase.Build();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameBootstrap] Content build failed: {e.Message}", this);
                enabled = false;
                return;
            }

            Debug.Log($"[GameBootstrap] Content loaded — {Content.Items.Count} item(s), " +
                      $"{Content.Blocks.Count} block(s).", this); // TEMP DEBUG

            // The simulation shares the catalogues the content build produced.
            Sim = new Simulation(Content.Items, Content.Blocks, startingInventorySlots, hotbarSlots);
            Debug.Log($"[GameBootstrap] Simulation created — {Sim.Player.Inventory.SlotCount} inventory " +
                      $"slot(s), {Sim.Player.HotbarSize} hotbar slot(s).", this); // TEMP DEBUG

            DontDestroyOnLoad(gameObject);
            Debug.Log("[GameBootstrap] Ready.", this); // TEMP DEBUG
        }

        private void OnDestroy()
        {
            // Clear statics when the owning bootstrap is torn down, so the next play session
            // (with domain reload disabled) starts from a clean slate.
            if (_instance == this)
            {
                GameInput.Shutdown();
                _instance = null;
                Content = null;
                Sim = null;
            }
        }
    }
}
