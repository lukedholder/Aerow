using UnityEngine;
using Aerow.Sim;          // Construct, BlockId, BlockOrientation, GridPos, GridSpace, Inventory (some not built yet)
using Aerow.View.Input;   // GameInput

namespace Aerow.View.Build
{
    /// <summary>
    /// SCAFFOLD — put on the player object. Drives block building: raycast at what the player
    /// looks at, place a block on the targeted construct's face, or seed a NEW construct when
    /// the ray hits terrain. Reads only <see cref="GameInput"/> semantic signals and calls the
    /// sim's <c>Construct</c> API (never touches the Input System directly).
    ///
    /// ⚠ Depends on the construct system that is still design-only
    /// (Construct, ConstructView, BlockId, BlockOrientation, GridSpace, block catalogue).
    /// Sections that reach into it are marked TODO and won't compile until those types exist.
    /// </summary>
    public sealed class BuildManager : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Camera the placement ray is cast from. Auto-found in children if empty.")]
        [SerializeField] private Camera playerCamera;

        [Header("Raycast")]
        [SerializeField] private float reach = 6f;
        [Tooltip("Layers the build ray can hit: constructs + terrain. Exclude the player and UI.")]
        [SerializeField] private LayerMask buildMask = ~0;
        [SerializeField] private string terrainTag = "Terrain";

        [Header("Placement")]
        [Tooltip("Constructs first built on terrain are anchored foundations; undock later to move.")]
        [SerializeField] private bool anchorNewConstructs = true;

        [Tooltip("Material for newly-seeded constructs (used when the block itself has no material).")]
        [SerializeField] private Material constructMaterial;

        // --- Held selection (for now driven by hotbar keys 1..N over GameContent.Blocks). ---
        private BlockDef currentBlockDef;             // the selected block; null = nothing in hand
        private BlockOrientation _currentOrientation; // cycled by rotate input

        [Header("Ghost")]
        [Tooltip("Material for the placement preview — a transparent material reads best.")]
        [SerializeField] private Material ghostMaterial;
        [SerializeField] private Color validTint = new Color(0.3f, 1f, 0.3f, 0.5f);
        [SerializeField] private Color invalidTint = new Color(1f, 0.3f, 0.3f, 0.5f);

        // --- Ghost preview instance (created at runtime). ---
        private GameObject _ghost;
        private Renderer _ghostRenderer;
        private Material _ghostMat;

        // --- Cached raycast target for the debug overlay. ---
        private bool _hasTarget;
        private BuildTarget _lastTarget;

        private bool HasBlockSelected => currentBlockDef != null;

        private void Awake()
        {
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        }

        private void Update()
        {
            if (!GameInput.IsInitialized) return;

            // Don't build while a menu owns input (OnFoot is suppressed anyway).
            if (GameInput.IsUiModal) { _hasTarget = false; HideGhost(); return; }

            UpdateSelection(); // hotbar keys → currentBlockDef

            // Always resolve a target so the debug overlay/ray show even before a block is picked.
            _hasTarget = TryGetTarget(out _lastTarget);
            if (!_hasTarget) { HideGhost(); return; }

            if (!HasBlockSelected) { HideGhost(); return; }

            CycleOrientationFromInput();
            UpdateGhost(_lastTarget);

            if (GameInput.OnFoot.PrimaryPressed) Place(_lastTarget);
            else if (GameInput.OnFoot.SecondaryPressed) Remove(_lastTarget);
            // TODO: hold-to-place — PrimaryHeld with a placement cooldown for dragging out walls.
        }

        private void UpdateSelection()
        {
            if (!GameBootstrap.IsReady) return;
            BlockCatalogue blocks = GameBootstrap.Content.Blocks;

            int slot = GameInput.OnFoot.HotbarDigitPressed(); // 1..9, 0 = none this frame
            if (slot < 1) return;

            int index = slot - 1; // slot 1 → block 0, slot 2 → block 1, …
            if (index >= blocks.Count) return;

            currentBlockDef = blocks.Get(new BlockId(index));
            Debug.Log($"[BuildManager] Selected block '{currentBlockDef.DisplayName}' (slot {slot})."); // TEMP DEBUG
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Targeting
        // ─────────────────────────────────────────────────────────────────────────────

        private struct BuildTarget
        {
            public bool OnTerrain;          // true → seed a new construct
            public ConstructView Construct; // null when OnTerrain
            public GridPos HitCell;         // existing block cell (for removal)
            public GridPos PlaceCell;       // empty neighbour cell (for placement)
            public Vector3 WorldPoint;
            public Vector3 WorldNormal;
        }

        private bool TryGetTarget(out BuildTarget target)
        {
            target = default;

            var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            Debug.DrawRay(ray.origin, ray.direction * reach, Color.cyan); // TEMP DEBUG (Scene view)
            if (!Physics.Raycast(ray, out RaycastHit hit, reach, buildMask, QueryTriggerInteraction.Ignore))
                return false;
            Debug.DrawLine(hit.point, hit.point + hit.normal * 0.25f, Color.yellow); // TEMP DEBUG (Scene view)

            // Hit an existing construct (or one of its subgrids — GetComponentInParent picks the right one).
            ConstructView view = hit.collider.GetComponentInParent<ConstructView>();
            if (view != null)
            {
                // Convert the world hit into the construct's LOCAL grid so any rotor/ship rotation
                // is handled automatically (the local grid is always axis-aligned).
                Transform t = view.transform;
                Vector3 localPoint = t.InverseTransformPoint(hit.point);
                Vector3 localNormal = t.InverseTransformDirection(hit.normal);
                GridPos faceDir = GridSpace.NearestAxis(localNormal);
                GridPos hitCell = GridSpace.LocalToCell(localPoint - localNormal * (0.5f * GridSpace.CellSize));

                target = new BuildTarget
                {
                    OnTerrain = false,
                    Construct = view,
                    HitCell = hitCell,
                    PlaceCell = hitCell + faceDir,
                    WorldPoint = hit.point,
                    WorldNormal = hit.normal,
                };
                return true;
            }

            // Hit terrain → the placement will seed a brand-new construct here.
            if (hit.collider.CompareTag(terrainTag))
            {
                target = new BuildTarget
                {
                    OnTerrain = true,
                    Construct = null,
                    PlaceCell = GridPos.Zero,   // first block sits at the new construct's local origin
                    WorldPoint = hit.point,
                    WorldNormal = hit.normal,
                };
                return true;
            }

            return false; // hit something that isn't buildable
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Place / Remove
        // ─────────────────────────────────────────────────────────────────────────────

        private void Place(in BuildTarget target)
        {
            // TODO: cost gate — read the block's BuildCost from the catalogue and check the
            //       player inventory before committing:
            //   ItemStack[] cost = currentBlockDef.BuildCost;
            //   if (!playerInventory.HasAll(cost)) { /* flash "insufficient" */ return; }

            if (target.OnTerrain)
            {
                // Free placement — no grid or rotation snap (Satisfactory-style). The construct
                // sits at the hit point, yawed to the player's facing; the first block is centred
                // on the cursor.
                ComputeTerrainPose(target, out Vector3 pos, out Quaternion rot);
                Material mat = GameBootstrap.Content.MaterialOf(currentBlockDef.Id);
                if (mat == null) mat = constructMaterial;

                ConstructView view = ConstructFactory.Create(GameBootstrap.Content.Blocks, pos, rot, mat);
                Construct c = view.Construct;
                c.IsAnchored = anchorNewConstructs;
                c.PlaceBlock(currentBlockDef.Id, GridPos.Zero, _currentOrientation);
                view.Rebuild();

                Debug.Log($"[BuildManager] Seeded construct #{c.Id} at {pos} with '{currentBlockDef.DisplayName}'."); // TEMP DEBUG
            }
            else
            {
                Construct c = target.Construct.Construct;
                if (!c.CanPlace(currentBlockDef.Id, target.PlaceCell, _currentOrientation)) return; // overlap / footprint / not connected
                c.PlaceBlock(currentBlockDef.Id, target.PlaceCell, _currentOrientation);
                target.Construct.Rebuild(); // remesh + rebuild colliders for the dirty chunk
                Debug.Log($"[BuildManager] Placed '{currentBlockDef.DisplayName}' at {target.PlaceCell} on construct #{c.Id}."); // TEMP DEBUG
            }

            // TODO: playerInventory.RemoveAll(cost);
        }

        private void Remove(in BuildTarget target)
        {
            if (target.OnTerrain || target.Construct == null) return;

            Construct c = target.Construct.Construct;
            if (!c.RemoveBlock(target.HitCell)) return;
            target.Construct.Rebuild();
            Debug.Log($"[BuildManager] Removed block at {target.HitCell} on construct #{c.Id}."); // TEMP DEBUG

            // TODO: removal can empty or SPLIT the construct:
            //   - if the construct is now empty → destroy its ConstructView/Rigidbody.
            //   - if RemoveBlock disconnected it → Construct.Split() returns the new pieces;
            //     spawn a ConstructView per piece (each becomes its own physics body).
            //   - refund a fraction of the block's build cost to the player inventory.
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Ghost preview & orientation
        // ─────────────────────────────────────────────────────────────────────────────

        private void UpdateGhost(in BuildTarget target)
        {
            EnsureGhost();

            Vector3 centre;
            Quaternion rot;
            bool valid;

            if (target.OnTerrain)
            {
                ComputeTerrainPose(target, out Vector3 cpos, out rot);
                centre = cpos + rot * GridSpace.CellToLocal(GridPos.Zero);
                valid = true; // a fresh construct always seeds
            }
            else
            {
                Transform t = target.Construct.transform;
                rot = t.rotation;
                centre = t.TransformPoint(GridSpace.CellToLocal(target.PlaceCell));
                valid = target.Construct.Construct.CanPlace(currentBlockDef.Id, target.PlaceCell, _currentOrientation);
            }

            _ghost.transform.SetPositionAndRotation(centre, rot);
            _ghost.transform.localScale = Vector3.one * GridSpace.CellSize;
            if (_ghostMat != null) _ghostMat.color = valid ? validTint : invalidTint;
            if (!_ghost.activeSelf) _ghost.SetActive(true);
        }

        private void EnsureGhost()
        {
            if (_ghost != null) return;

            _ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ghost.name = "BlockGhost";
            Destroy(_ghost.GetComponent<Collider>()); // preview must not block the build raycast

            _ghostRenderer = _ghost.GetComponent<Renderer>();
            _ghostMat = ghostMaterial != null
                ? new Material(ghostMaterial)
                : new Material(_ghostRenderer.sharedMaterial);
            _ghostRenderer.sharedMaterial = _ghostMat;
            _ghost.SetActive(false);
        }

        private void HideGhost()
        {
            if (_ghost != null && _ghost.activeSelf) _ghost.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_ghost != null) Destroy(_ghost);
            if (_ghostMat != null) Destroy(_ghostMat);
        }

        private void CycleOrientationFromInput()
        {
            // TODO: rotate the held block. Needs a rotate input — either bind Scroll (GameInput.UI
            //       is UI-only, so add a "Rotate" action to the OnFoot/Build map) or an R key.
            //   if (rotatePressed) _currentOrientation = _currentOrientation.Next();
        }

        // TEMP DEBUG — on-screen raycast / selection overlay.
        private void OnGUI()
        {
            var rect = new Rect(10, 10, 340, 176);
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Label("BuildManager (debug)");
            GUILayout.Label(HasBlockSelected
                ? $"Block: {currentBlockDef.DisplayName}  (id {currentBlockDef.Id.Index})"
                : "Block: <none>   press 1 / 2 to select");
            GUILayout.Label($"Orientation: {_currentOrientation.Index}");
            GUILayout.Space(4);

            if (_hasTarget)
            {
                if (_lastTarget.OnTerrain)
                {
                    GUILayout.Label("Target: TERRAIN (would seed a new construct)");
                    GUILayout.Label($"Hit point: {_lastTarget.WorldPoint}");
                }
                else
                {
                    GUILayout.Label($"Target: construct #{_lastTarget.Construct.Construct.Id}");
                    GUILayout.Label($"Hit cell:   {_lastTarget.HitCell}");
                    GUILayout.Label($"Place cell: {_lastTarget.PlaceCell}");
                }
                GUILayout.Label($"Face normal: {_lastTarget.WorldNormal}");
            }
            else
            {
                GUILayout.Label("Target: <none>");
            }

            GUILayout.EndArea();
        }

        // Free (un-snapped) pose for a construct seeded on terrain: sit at the hit point, yaw to
        // the player's facing, and centre the first block horizontally on the cursor.
        private void ComputeTerrainPose(in BuildTarget target, out Vector3 constructPos, out Quaternion rot)
        {
            rot = FacingYaw();
            Vector3 firstCellCentre = GridSpace.CellToLocal(GridPos.Zero); // (½cs, ½cs, ½cs)
            Vector3 offsetH = rot * new Vector3(firstCellCentre.x, 0f, firstCellCentre.z);
            constructPos = new Vector3(
                target.WorldPoint.x - offsetH.x,
                target.WorldPoint.y,
                target.WorldPoint.z - offsetH.z);
        }

        // Upright rotation facing the camera's horizontal direction — free yaw, no snapping.
        private Quaternion FacingYaw()
        {
            Vector3 fwd = playerCamera.transform.forward;
            fwd.y = 0f;
            return fwd.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(fwd, Vector3.up) : Quaternion.identity;
        }
    }
}
