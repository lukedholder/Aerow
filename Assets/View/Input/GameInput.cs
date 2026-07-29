using UnityEngine;
using UnityEngine.InputSystem;

namespace Aerow.View.Input
{
    /// <summary>
    /// The single semantic input layer. This is the ONLY code that touches
    /// <see cref="UnityEngine.InputSystem"/>; everything else reads the polled signals
    /// exposed here (e.g. <c>GameInput.OnFoot.Move</c>). Wraps the generated
    /// <see cref="InputSystem_Actions"/>.
    ///
    /// Exposes every action in the Global, OnFoot, and UI maps as polled semantic signals.
    /// </summary>
    public static class GameInput
    {
        private static InputSystem_Actions _actions; // generated wrapper
        private static int _uiModalCount;

        public static InputContext Active { get; private set; }
        public static bool IsUiModal => _uiModalCount > 0;
        public static bool IsInitialized => _actions != null;

        // ── Lifecycle (drive from GameBootstrap: Initialize in Awake, Shutdown in OnDestroy) ──

        public static void Initialize()
        {
            if (_actions != null) return;
            _actions = new InputSystem_Actions();

            _actions.Global.Enable();        // always-on map
            SetContext(InputContext.OnFoot); // default gameplay map

            Debug.Log($"[GameInput] Initialized — context {Active}."); // TEMP DEBUG
        }

        public static void Shutdown()
        {
            if (_actions == null) return;
            _actions.Global.Disable();
            _actions.OnFoot.Disable();
            _actions.UI.Disable();
            _actions.Dispose();
            _actions = null;
            _uiModalCount = 0;

            Debug.Log("[GameInput] Shutdown."); // TEMP DEBUG
        }

        // ── Context switching ──

        public static void SetContext(InputContext context)
        {
            Active = context;
            Debug.Log($"[GameInput] Context → {context}."); // TEMP DEBUG
            if (IsUiModal) return; // a panel owns input; restored on PopUiModal
            EnableActiveGameplayMap();
        }

        private static void EnableActiveGameplayMap()
        {
            DisableAllGameplayMaps();
            switch (Active)
            {
                case InputContext.OnFoot: _actions.OnFoot.Enable(); break;
                // case InputContext.Build: _actions.Build.Enable(); break; // future
            }
        }

        private static void DisableAllGameplayMaps()
        {
            _actions.OnFoot.Disable();
            // future: Build/Vehicle/Ship
        }

        // ── UI overlay (counter-based, so stacked panels work) ──

        public static void PushUiModal()
        {
            if (_uiModalCount++ == 0)
            {
                DisableAllGameplayMaps();
                _actions.UI.Enable();
            }
            Debug.Log($"[GameInput] UI modal pushed (count {_uiModalCount})."); // TEMP DEBUG
        }

        public static void PopUiModal()
        {
            if (_uiModalCount == 0) return;
            if (--_uiModalCount == 0)
            {
                _actions.UI.Disable();
                EnableActiveGameplayMap(); // restore remembered gameplay context
            }
            Debug.Log($"[GameInput] UI modal popped (count {_uiModalCount})."); // TEMP DEBUG
        }

        // ── Semantic signals (polling — no Update pump, no flag clearing) ──

        public static class Global
        {
            // Escape gating: while a panel is open, UI/Cancel owns Escape, so this reports
            // false to avoid double-handling the same key press.
            public static bool EscapePressed =>
                !IsUiModal && _actions.Global.Escape.WasPressedThisFrame();

            public static bool ScreenshotPressed =>
                _actions.Global.Screenshot.WasPressedThisFrame();
        }

        public static class OnFoot
        {
            public static Vector2 Move => _actions.OnFoot.Move.ReadValue<Vector2>();
            public static Vector2 Look => _actions.OnFoot.Look.ReadValue<Vector2>();

            public static bool JumpPressed => _actions.OnFoot.Jump.WasPressedThisFrame();
            public static bool SprintHeld => _actions.OnFoot.Sprint.IsPressed();
            public static bool InteractPressed => _actions.OnFoot.Interact.WasPressedThisFrame();

            // Main action buttons expose both edge and hold (e.g. tap to fire vs. hold to mine).
            public static bool PrimaryPressed => _actions.OnFoot.Primary.WasPressedThisFrame();
            public static bool PrimaryHeld => _actions.OnFoot.Primary.IsPressed();
            public static bool SecondaryPressed => _actions.OnFoot.Secondary.WasPressedThisFrame();
            public static bool SecondaryHeld => _actions.OnFoot.Secondary.IsPressed();

            public static bool ToggleInventoryPressed => _actions.OnFoot.ToggleInventory.WasPressedThisFrame();

            // Q — opens the build menu (handled by MenuManager).
            public static bool OpenBuildMenuPressed => _actions.OnFoot.OpenBuildMenu.WasPressedThisFrame();

            // B — toggles the hotbar between the item/build layout (0-9, Minecraft/Satisfactory)
            // and the combat layout (number keys map to weapons, Helldivers-style). The underlying
            // action is still named "EnterBuild" in the .inputactions asset (a rename candidate).
            // No hotbar system consumes this yet.
            public static bool ToggleHotbarModePressed => _actions.OnFoot.EnterBuild.WasPressedThisFrame();

            // TEMP: middle-click cycles the build rotation axis. Reads the mouse directly instead
            // of through the .inputactions asset — add a real "CycleAxis" action to OnFoot later.
            public static bool CycleAxisPressed =>
                Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame;

            // TEMP: scroll wheel as discrete detents (+1 up / -1 down / 0 none). The OnFoot map has
            // no Scroll action yet — add one and route this through it later.
            public static int ScrollSteps()
            {
                Mouse m = Mouse.current;
                if (m == null) return 0;
                float y = m.scroll.ReadValue().y;
                if (y > 0.01f) return 1;
                if (y < -0.01f) return -1;
                return 0;
            }

            // Hotbar slot pressed this frame (1..9), or 0 for none. TEMP: reads the number-row keys
            // directly instead of through the .inputactions asset — swap to real Hotbar actions later.
            public static int HotbarDigitPressed()
            {
                Keyboard kb = Keyboard.current;
                if (kb == null) return 0;
                if (kb.digit1Key.wasPressedThisFrame) return 1;
                if (kb.digit2Key.wasPressedThisFrame) return 2;
                if (kb.digit3Key.wasPressedThisFrame) return 3;
                if (kb.digit4Key.wasPressedThisFrame) return 4;
                if (kb.digit5Key.wasPressedThisFrame) return 5;
                if (kb.digit6Key.wasPressedThisFrame) return 6;
                if (kb.digit7Key.wasPressedThisFrame) return 7;
                if (kb.digit8Key.wasPressedThisFrame) return 8;
                if (kb.digit9Key.wasPressedThisFrame) return 9;
                return 0;
            }
        }

        public static class UI
        {
            public static Vector2 Navigate => _actions.UI.Navigate.ReadValue<Vector2>();
            public static Vector2 Point => _actions.UI.Point.ReadValue<Vector2>(); // mouse screen position

            public static bool SubmitPressed => _actions.UI.Submit.WasPressedThisFrame();
            public static bool CancelPressed => _actions.UI.Cancel.WasPressedThisFrame();
            public static bool ClosePressed => _actions.UI.Close.WasPressedThisFrame();

            // Full press/hold/release lifecycle for drag-and-drop (e.g. moving items between slots).
            public static bool ClickPressed => _actions.UI.Click.WasPressedThisFrame();
            public static bool ClickHeld => _actions.UI.Click.IsPressed();
            public static bool ClickReleased => _actions.UI.Click.WasReleasedThisFrame();

            // Single Value action bound to <Mouse>/scroll (a Vector2); the wheel is the
            // vertical component (positive = up).
            public static float Scroll => _actions.UI.Scroll.ReadValue<Vector2>().y;
        }
    }
}
