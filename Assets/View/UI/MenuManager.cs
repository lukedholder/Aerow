using System.Collections.Generic;
using UnityEngine;
using Aerow.View.Input;

namespace Aerow.View.UI
{
    /// <summary>
    /// Owns every <see cref="UIPanel"/> and the rules around them: input capture (via
    /// <see cref="GameInput"/>), the cursor, pause (Time.timeScale), and a modal stack so
    /// Escape/Cancel closes the top panel. Put this on your root UI Canvas; panels live as
    /// children (or drag them into the list).
    /// </summary>
    public sealed class MenuManager : MonoBehaviour
    {
        public static MenuManager Instance { get; private set; }

        [Tooltip("Panels managed here. Auto-collected from children (including inactive) if left empty.")]
        [SerializeField] private List<UIPanel> panels = new List<UIPanel>();

        private readonly Dictionary<MenuId, UIPanel> _byId = new Dictionary<MenuId, UIPanel>();
        private readonly List<UIPanel> _stack = new List<UIPanel>(); // open panels; top == last
        private int _pauseCount;

        public bool AnyOpen => _stack.Count > 0;
        private UIPanel Top => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Reset in case a previous play session (domain reload off) left time frozen.
            Time.timeScale = 1f;
            _pauseCount = 0;

            if (panels == null || panels.Count == 0)
                panels = new List<UIPanel>(GetComponentsInChildren<UIPanel>(includeInactive: true));

            _byId.Clear();
            foreach (UIPanel p in panels)
            {
                if (p == null) continue;
                if (_byId.ContainsKey(p.Id))
                    Debug.LogWarning($"[MenuManager] Duplicate panel id '{p.Id}' on '{p.name}'.", p);
                else
                    _byId.Add(p.Id, p);

                p.SetOpen(false); // everything starts closed
            }

            UpdateCursor();
            Debug.Log($"[MenuManager] Registered {_byId.Count} panel(s)."); // TEMP DEBUG
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!GameInput.IsInitialized) return;

            if (AnyOpen)
            {
                // Escape (UI.Cancel) or Tab (UI.Close) closes the top panel. Global.Escape is
                // gated off while modal, so this never double-fires with the open path below.
                if ((GameInput.UI.CancelPressed || GameInput.UI.ClosePressed) &&
                    Top != null && Top.CloseOnCancel)
                {
                    Close(Top.Id);
                }
            }
            else
            {
                // Nothing open: map gameplay shortcuts to their menus.
                if (GameInput.Global.EscapePressed) Open(MenuId.Pause);
                else if (GameInput.OnFoot.ToggleInventoryPressed) Open(MenuId.Inventory);
                else if (GameInput.OnFoot.OpenBuildMenuPressed) Open(MenuId.Build);
            }
        }

        public bool IsOpen(MenuId id) => _byId.TryGetValue(id, out UIPanel p) && p.IsOpen;

        public void Toggle(MenuId id)
        {
            if (IsOpen(id)) Close(id); else Open(id);
        }

        public void Open(MenuId id)
        {
            if (!_byId.TryGetValue(id, out UIPanel panel))
            {
                Debug.LogWarning($"[MenuManager] No panel registered for '{id}'.");
                return;
            }
            if (panel.IsOpen) return;

            panel.SetOpen(true);
            _stack.Add(panel);

            if (panel.CaptureInput) GameInput.PushUiModal();
            if (panel.PausesGame && _pauseCount++ == 0) Time.timeScale = 0f;

            UpdateCursor();
            Debug.Log($"[MenuManager] Open {id} (stack {_stack.Count})."); // TEMP DEBUG
        }

        public void Close(MenuId id)
        {
            if (!_byId.TryGetValue(id, out UIPanel panel) || !panel.IsOpen) return;

            panel.SetOpen(false);
            _stack.Remove(panel);

            if (panel.CaptureInput) GameInput.PopUiModal();
            if (panel.PausesGame && --_pauseCount == 0) Time.timeScale = 1f;

            UpdateCursor();
            Debug.Log($"[MenuManager] Close {id} (stack {_stack.Count})."); // TEMP DEBUG
        }

        public void CloseAll()
        {
            while (_stack.Count > 0) Close(_stack[_stack.Count - 1].Id);
        }

        private void UpdateCursor()
        {
            // Any open modal frees the cursor; otherwise gameplay recaptures it.
            bool free = AnyOpen;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }
    }
}
