using UnityEngine;
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

        /// <summary>
        /// The loaded content: the sim's item catalogue plus the view-side visual lookup.
        /// Null until <see cref="Awake"/> succeeds — read it from Start onward, or guard with
        /// <see cref="IsReady"/>.
        /// </summary>
        public static GameContent Content { get; private set; }

        /// <summary>True once content is loaded and safe to read.</summary>
        public static bool IsReady => Content != null;

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
            }
        }
    }
}
