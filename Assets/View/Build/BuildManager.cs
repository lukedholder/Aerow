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

        [Tooltip("Degrees per scroll click when free-rotating a new construct on terrain.")]
        [SerializeField] private float terrainYawStep = 15f;

        // --- Held selection (for now driven by hotbar keys 1..N over GameContent.Blocks). ---
        private BlockDef currentBlockDef;             // the selected block; null = nothing in hand
        private BlockOrientation _currentOrientation; // 90° block orientation, on existing constructs
        private RotationAxis _rotationAxis = RotationAxis.Y; // axis the scroll wheel turns (middle-click cycles)
        private float _terrainYaw;                    // free yaw offset for a new construct on terrain

        [Header("Ghost")]
        [Tooltip("Material for the placement preview — a transparent material reads best.")]
        [SerializeField] private Material ghostMaterial;
        [SerializeField] private Color validTint = new Color(0.3f, 1f, 0.3f, 0.5f);
        [SerializeField] private Color invalidTint = new Color(1f, 0.3f, 0.3f, 0.5f);

        // --- Ghost preview instance (created at runtime). ---
        private GameObject _ghost;
        private Renderer _ghostRenderer;
        private MeshFilter _ghostFilter;
        private Mesh _cubeMesh; // fallback for blocks with no authored mesh
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

            UpdateRotationInput(_lastTarget.OnTerrain);
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

            BlockDef picked = blocks.Get(new BlockId(index));
            if (picked == currentBlockDef) return; // re-selecting the same block keeps its rotation

            currentBlockDef = picked;
            _currentOrientation = BlockOrientation.Identity; // a newly-picked block starts unrotated
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
            public GridPos FaceDir;         // outward normal of the hit face, in the construct's grid
            public Vector3 CellPoint;       // hit point in continuous cell coords (cell i centre == i)
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
                // Nudge just inside the surface so the hit resolves to the cell actually struck.
                // A small epsilon (rather than half a cell) also works for angled faces, where the
                // hit point lies inside the cell instead of on its boundary.
                GridPos hitCell = GridSpace.LocalToCell(localPoint - localNormal * (0.01f * GridSpace.CellSize));

                target = new BuildTarget
                {
                    OnTerrain = false,
                    Construct = view,
                    HitCell = hitCell,
                    FaceDir = faceDir,
                    CellPoint = GridSpace.LocalToCellPoint(localPoint),
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
                    Construct = null,           // first block sits at the new construct's local origin
                    WorldPoint = hit.point,
                    WorldNormal = hit.normal,
                };
                return true;
            }

            return false; // hit something that isn't buildable
        }

        /// <summary>
        /// Work out where a block of the current size/orientation should sit against the targeted
        /// face, and return its <b>anchor</b> cell.
        ///
        /// The block is positioned by its <b>centre</b>, not its anchor corner, so rotating spins it
        /// in place instead of swinging its bulk to another side of whatever it rests on. Across the
        /// face the centre snaps to the nearest cell vertex (even span) or cell centre (odd span) —
        /// i.e. the nearest corner of the face for a 2-cell-wide block. Along the face normal it
        /// sits flush against the surface. The anchor is then derived as
        /// <c>centre − rotate((Size−1)/2)</c>, which always lands on an integer cell.
        /// </summary>
        private GridPos ComputeAnchor(in BuildTarget target, BlockOrientation orient)
        {
            GridPos size = currentBlockDef.Size;

            // Footprint AABB after rotation, in cells (per-axis span).
            GridPos r = orient.Rotate(size);
            var rotSize = new Vector3Int(Mathf.Abs(r.X), Mathf.Abs(r.Y), Mathf.Abs(r.Z));

            var hitCell = new Vector3(target.HitCell.X, target.HitCell.Y, target.HitCell.Z);
            var faceDir = new Vector3(target.FaceDir.X, target.FaceDir.Y, target.FaceDir.Z);
            Vector3 p = target.CellPoint;

            Vector3 centre = Vector3.zero;
            for (int i = 0; i < 3; i++)
            {
                float half = rotSize[i] * 0.5f;

                if (Mathf.Abs(faceDir[i]) > 0.5f)
                {
                    // Along the normal: clear the hit cell's face (½ cell), then half our own span.
                    centre[i] = hitCell[i] + faceDir[i] * (0.5f + half);
                }
                else if (rotSize[i] % 2 == 0)
                {
                    centre[i] = Mathf.Floor(p[i]) + 0.5f; // even span → centre lands on a cell vertex
                }
                else
                {
                    centre[i] = Mathf.Round(p[i]);        // odd span → centre lands on a cell centre
                }
            }

            // Rotate is integer-only, so rotate (Size−1) and halve — Rotate is linear, so this
            // equals rotate((Size−1)/2).
            GridPos rs = orient.Rotate(new GridPos(size.X - 1, size.Y - 1, size.Z - 1));
            Vector3 anchorF = centre - new Vector3(rs.X, rs.Y, rs.Z) * 0.5f;

            return new GridPos(
                Mathf.RoundToInt(anchorF.x),
                Mathf.RoundToInt(anchorF.y),
                Mathf.RoundToInt(anchorF.z));
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
                // sits at the hit point with the player's chosen absolute yaw; the first block is
                // centred on the cursor.
                ComputeTerrainPose(target, out Vector3 pos, out Quaternion rot);
                Material mat = GameBootstrap.Content.MaterialOf(currentBlockDef.Id);
                if (mat == null) mat = constructMaterial;

                ConstructView view = ConstructFactory.Create(GameBootstrap.Content.Blocks, pos, rot, mat);
                Construct c = view.Construct;
                c.IsAnchored = anchorNewConstructs;
                c.PlaceBlock(currentBlockDef.Id, GridPos.Zero, OrientationFor(true));
                view.Rebuild();

                Debug.Log($"[BuildManager] Seeded construct #{c.Id} at {pos} with '{currentBlockDef.DisplayName}'."); // TEMP DEBUG
            }
            else
            {
                Construct c = target.Construct.Construct;
                GridPos anchor = ComputeAnchor(target, _currentOrientation);
                if (!c.CanPlace(currentBlockDef.Id, anchor, _currentOrientation)) return; // overlap / footprint / not connected
                c.PlaceBlock(currentBlockDef.Id, anchor, _currentOrientation);
                target.Construct.Rebuild(); // remesh + rebuild colliders for the dirty chunk
                Debug.Log($"[BuildManager] Placed '{currentBlockDef.DisplayName}' at {anchor} on construct #{c.Id}."); // TEMP DEBUG
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

            // Mirror ConstructMesher's pose maths exactly, so the preview is WYSIWYG: rotate about
            // the anchor cell's centre, offset to the geometric centre by the rotated half-span.
            BlockOrientation orient = OrientationFor(target.OnTerrain);
            Quaternion q = ConstructMesher.ToRotation(orient);
            GridPos size = currentBlockDef.Size;
            var halfSpan = new Vector3(size.X - 1, size.Y - 1, size.Z - 1) * (0.5f * GridSpace.CellSize);

            Vector3 worldPos;
            Quaternion worldRot;
            bool valid;

            if (target.OnTerrain)
            {
                ComputeTerrainPose(target, out Vector3 cpos, out Quaternion crot);
                worldPos = cpos + crot * (GridSpace.CellToLocal(GridPos.Zero) + q * halfSpan);
                worldRot = crot * q;
                valid = true; // a fresh construct always seeds
            }
            else
            {
                Transform t = target.Construct.transform;
                GridPos anchor = ComputeAnchor(target, orient);
                worldPos = t.TransformPoint(GridSpace.CellToLocal(anchor) + q * halfSpan);
                worldRot = t.rotation * q;
                valid = target.Construct.Construct.CanPlace(currentBlockDef.Id, anchor, orient);
            }

            // Authored meshes are already true world size; the cube fallback spans the footprint.
            Mesh src = GameBootstrap.IsReady ? GameBootstrap.Content.MeshOf(currentBlockDef.Id) : null;
            _ghostFilter.sharedMesh = src != null ? src : _cubeMesh;
            Vector3 scale = src != null
                ? Vector3.one * GridSpace.MeshScale
                : new Vector3(size.X, size.Y, size.Z) * GridSpace.CellSize;

            _ghost.transform.SetPositionAndRotation(worldPos, worldRot);
            _ghost.transform.localScale = scale;
            if (_ghostMat != null) _ghostMat.color = valid ? validTint : invalidTint;
            if (!_ghost.activeSelf) _ghost.SetActive(true);
        }

        private void EnsureGhost()
        {
            if (_ghost != null) return;

            _ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ghost.name = "BlockGhost";
            Destroy(_ghost.GetComponent<Collider>()); // preview must not block the build raycast

            _ghostFilter = _ghost.GetComponent<MeshFilter>();
            _cubeMesh = _ghostFilter.sharedMesh; // kept for blocks with no authored mesh

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

        /// <summary>
        /// Middle-click cycles the rotation axis; the scroll wheel turns the block about it.
        /// On terrain only yaw applies (free-form placement), in <see cref="terrainYawStep"/>
        /// increments; on a construct the block snaps through the 24 grid orientations.
        /// </summary>
        private void UpdateRotationInput(bool onTerrain)
        {
            if (GameInput.OnFoot.CycleAxisPressed)
            {
                _rotationAxis = (RotationAxis)(((int)_rotationAxis + 1) % 3);
                Debug.Log($"[BuildManager] Rotation axis → {_rotationAxis}."); // TEMP DEBUG
            }

            int steps = GameInput.OnFoot.ScrollSteps();
            if (steps == 0) return;

            if (onTerrain)
            {
                _terrainYaw = Mathf.Repeat(_terrainYaw + steps * terrainYawStep, 360f);
                Debug.Log($"[BuildManager] Terrain yaw → {_terrainYaw:0.#}°."); // TEMP DEBUG
            }
            else
            {
                _currentOrientation = _currentOrientation.RotatedAbout(_rotationAxis, steps);
                Debug.Log($"[BuildManager] Orientation → {_currentOrientation.Index} " +
                          $"(about {_rotationAxis})."); // TEMP DEBUG
            }
        }

        /// <summary>
        /// On terrain the construct's own yaw carries the rotation, so the seed block sits
        /// unrotated; on an existing construct the block uses the chosen grid orientation.
        /// </summary>
        private BlockOrientation OrientationFor(bool onTerrain)
            => onTerrain ? BlockOrientation.Identity : _currentOrientation;

        // TEMP DEBUG — on-screen raycast / selection overlay.
        private void OnGUI()
        {
            var rect = new Rect(10, 10, 340, 210);
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Label("BuildManager (debug)");
            GUILayout.Label(HasBlockSelected
                ? $"Block: {currentBlockDef.DisplayName}  (id {currentBlockDef.Id.Index})"
                : "Block: <none>   press 1 / 2 to select");

            bool terrain = _hasTarget && _lastTarget.OnTerrain;
            GUILayout.Label(terrain
                ? $"Axis: Y (locked on terrain)   [MMB cycles: {_rotationAxis}]"
                : $"Axis: {_rotationAxis}   [MMB to cycle, scroll to rotate]");
            GUILayout.Label(terrain
                ? $"Terrain yaw: {_terrainYaw:0.#}°  ({terrainYawStep:0.#}° / click)"
                : $"Orientation: {_currentOrientation.Index} / 24");
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
                    GUILayout.Label($"Hit cell: {_lastTarget.HitCell}   face {_lastTarget.FaceDir}");
                    GUILayout.Label(HasBlockSelected
                        ? $"Anchor: {ComputeAnchor(_lastTarget, OrientationFor(false))}"
                        : "Anchor: <no block>");
                }
                GUILayout.Label($"Face normal: {_lastTarget.WorldNormal}");
            }
            else
            {
                GUILayout.Label("Target: <none>");
            }

            GUILayout.EndArea();
        }

        // Free (un-snapped) pose for a construct seeded on terrain: sit at the hit point, take the
        // absolute scroll-wheel yaw, and centre the first block horizontally on the cursor.
        private void ComputeTerrainPose(in BuildTarget target, out Vector3 constructPos, out Quaternion rot)
        {
            rot = TerrainRotation();

            // Centre the whole block on the cursor (not just its anchor cell) — matters for
            // multi-cell blocks. Vertically the construct's origin is the block's base.
            GridPos size = currentBlockDef.Size;
            Vector3 blockCentre = GridSpace.CellToLocal(GridPos.Zero)
                                + new Vector3(size.X - 1, size.Y - 1, size.Z - 1) * (0.5f * GridSpace.CellSize);
            Vector3 offsetH = rot * new Vector3(blockCentre.x, 0f, blockCentre.z);
            constructPos = new Vector3(
                target.WorldPoint.x - offsetH.x,
                target.WorldPoint.y,
                target.WorldPoint.z - offsetH.z);
        }

        // Absolute world yaw for a construct seeded on terrain — independent of where the player is
        // looking, so builds stay put while you move and can be lined up with existing structures.
        private Quaternion TerrainRotation() => Quaternion.Euler(0f, _terrainYaw, 0f);
    }
}
