using UnityEngine;
using Aerow.Sim;

namespace Aerow.View
{
    /// <summary>
    /// View-side access point for the player's inventory. It holds <b>no state</b> — the stacks live
    /// in <c>GameBootstrap.Sim.Player.Inventory</c>, which is what gets saved. This is a scene
    /// presence for UI to bind to, plus a convenient handle for gameplay code.
    ///
    /// The starting slot count is configured on <see cref="GameBootstrap"/>, since that's what
    /// constructs the simulation.
    ///
    /// (The class name matches the file, which collides with the sim type — so the sim one is
    /// referred to as <c>Sim.Inventory</c> throughout.)
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        private static Inventory _instance;

        /// <summary>True when a player Inventory exists — check before using <see cref="Instance"/>.</summary>
        public static bool Exists => _instance != null;

        public static Inventory Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.LogError("Inventory instance is null. Make sure there is an Inventory component in the scene.");
                }

                return _instance;
            }
        }

        /// <summary>The simulation inventory holding the actual stacks (null before bootstrap).</summary>
        public Sim.Inventory Items => GameBootstrap.IsReady ? GameBootstrap.Sim.Player.Inventory : null;

        /// <summary>Current capacity — grows past the starting size as upgrades are unlocked.</summary>
        public int SlotCount
        {
            get
            {
                Sim.Inventory inv = Items;
                return inv != null ? inv.SlotCount : 0;
            }
        }

        private void Awake()
        {
            _instance = this;
        }

        /// <summary>
        /// Add capacity — call when the player unlocks an inventory upgrade. Existing stacks keep
        /// their slots. Returns the new slot count.
        /// </summary>
        public int Grow(int additionalSlots)
        {
            Sim.Inventory inv = Items;
            if (inv == null) return 0;

            int count = inv.Grow(additionalSlots);
            Debug.Log($"[Inventory] Grew by {additionalSlots} → {count} slot(s)."); // TEMP DEBUG
            return count;
        }

        /// <summary>Convenience passthrough: the stack in a slot (Empty if out of range).</summary>
        public ItemStack GetSlot(int index)
        {
            Sim.Inventory inv = Items;
            return inv != null && index >= 0 && index < inv.SlotCount ? inv.GetSlot(index) : ItemStack.Empty;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
