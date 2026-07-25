using UnityEngine;

namespace Aerow.View.UI
{
    /// <summary>
    /// Base class for every menu / interface panel (pause, inventory, build, and later machine
    /// UIs, research, etc.). Author the panel visually in Unity and put this — or a subclass —
    /// on its root object. The <see cref="MenuManager"/> owns Open/Close; override
    /// <see cref="OnOpen"/>/<see cref="OnClose"/> for panel-specific logic (bind data, reset
    /// state, play a transition).
    ///
    /// Per-panel behaviour is authored in the Inspector via the flags below, so policy lives
    /// with the content rather than being hard-coded in the manager.
    /// </summary>
    public class UIPanel : MonoBehaviour
    {
        [SerializeField] private MenuId id;

        [Header("Behaviour")]
        [Tooltip("Modal: capture input while open (disable gameplay maps, free the cursor).")]
        [SerializeField] private bool captureInput = true;

        [Tooltip("Freeze game time (Time.timeScale = 0) while open. Typically true only for Pause.")]
        [SerializeField] private bool pausesGame = false;

        [Tooltip("Cancel (Escape) / Close (Tab) will close this panel.")]
        [SerializeField] private bool closeOnCancel = true;

        public MenuId Id => id;
        public bool CaptureInput => captureInput;
        public bool PausesGame => pausesGame;
        public bool CloseOnCancel => closeOnCancel;
        public bool IsOpen { get; private set; }

        /// <summary>
        /// Driven by the <see cref="MenuManager"/> only. Syncs the GameObject's active state and
        /// fires <see cref="OnOpen"/>/<see cref="OnClose"/> on a change.
        /// </summary>
        public void SetOpen(bool open)
        {
            bool changed = IsOpen != open;
            IsOpen = open;
            if (gameObject.activeSelf != open) gameObject.SetActive(open);
            if (!changed) return;
            if (open) OnOpen(); else OnClose();
        }

        /// <summary>Called after the panel becomes visible. Bind data / reset here.</summary>
        protected virtual void OnOpen() { }

        /// <summary>Called after the panel is hidden. Unsubscribe / clean up here.</summary>
        protected virtual void OnClose() { }
    }
}
