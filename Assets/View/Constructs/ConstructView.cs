using System.Collections.Generic;
using UnityEngine;
using Aerow.Sim;
using Aerow.View.Build; // GridSpace

namespace Aerow.View
{
    /// <summary>
    /// The view of one sim <see cref="Sim.Construct"/>: owns the combined render mesh (built by
    /// <see cref="ConstructMesher"/>) and a <b>per-block collider</b> — one child object per block,
    /// carrying a convex MeshCollider shaped like that block's authored mesh, or a box spanning its
    /// footprint when it has none. A Rigidbody and rotor joints come later.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ConstructView : MonoBehaviour
    {
        [Tooltip("Material for the whole construct hull (single-material minimal path).")]
        [SerializeField] private Material material;

        /// <summary>The sim construct this view mirrors.</summary>
        public Construct Construct { get; private set; }

        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Mesh _mesh;

        // One child object per block, reused across rebuilds (surplus is deactivated, not destroyed,
        // so a rebuild never leaves stale colliders around for the rest of the frame).
        private readonly List<GameObject> _blockColliders = new List<GameObject>();

        /// <summary>Bind this view to a sim construct and build its mesh/colliders.</summary>
        public void Initialize(Construct construct, Material overrideMaterial = null)
        {
            Construct = construct;
            if (overrideMaterial != null) material = overrideMaterial;
            Rebuild();
        }

        /// <summary>Regenerate the mesh + colliders from the current block layout.</summary>
        public void Rebuild()
        {
            if (Construct == null) return;

            if (_filter == null) _filter = GetComponent<MeshFilter>();
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            if (material != null) _renderer.sharedMaterial = material;

            // A MeshRenderer added at runtime has no material — geometry would be invisible.
            if (_renderer.sharedMaterial == null)
            {
                _renderer.sharedMaterial = FallbackMaterial();
                Debug.LogWarning("[ConstructView] No material assigned — using a fallback. Set " +
                                 "BlockDefSO.material or BuildManager.constructMaterial.", this);
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "Construct" };
                _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // big ships exceed 65k verts
                _mesh.MarkDynamic();
            }

            // Content supplies the block catalogue + authored meshes; null falls back to cubes.
            GameContent content = GameBootstrap.IsReady ? GameBootstrap.Content : null;

            ConstructMesher.BuildMesh(Construct, content, _mesh);
            _filter.sharedMesh = _mesh;
            RebuildColliders(content);

            // TEMP DEBUG — everything needed to diagnose an invisible construct in one line.
            Debug.Log($"[ConstructView] #{Construct.Id}: {Construct.BlockCount} block(s) " +
                      $"({ConstructMesher.LastAuthoredBlocks} authored / {ConstructMesher.LastProceduralBlocks} cube) → " +
                      $"{_mesh.vertexCount} verts, {_mesh.triangles.Length / 3} tris, " +
                      $"bounds {_mesh.bounds.size}, mat '{(_renderer.sharedMaterial != null ? _renderer.sharedMaterial.name : "NULL")}', " +
                      $"content {(GameBootstrap.IsReady ? "ready" : "NULL")}", this);
        }

        /// <summary>
        /// Rebuild one collider per block, posed exactly like its mesh (same anchor-cell pivot and
        /// rotated half-span the mesher uses). Blocks with a readable authored mesh get a convex
        /// MeshCollider of that shape; the rest get a box spanning their footprint.
        /// </summary>
        private void RebuildColliders(GameContent content)
        {
            int used = 0;

            if (content == null)
            {
                // No catalogue — mirror the mesher's fallback: a unit box per occupied cell.
                foreach (GridPos cell in Construct.OccupiedCells)
                {
                    GameObject child = ColliderChild(used++);
                    child.transform.localPosition = GridSpace.CellToLocal(cell);
                    child.transform.localRotation = Quaternion.identity;
                    ConfigureCollider(child, null, new GridPos(1, 1, 1));
                }
            }
            else
            {
                foreach (BlockInstance inst in Construct.Blocks)
                {
                    BlockDef def = content.Blocks.Get(inst.Def);
                    Quaternion q = ConstructMesher.ToRotation(inst.Orientation);
                    GridPos size = def.Size;
                    var halfSpan = new Vector3(size.X - 1, size.Y - 1, size.Z - 1) * (0.5f * GridSpace.CellSize);

                    GameObject child = ColliderChild(used++);
                    child.transform.localPosition = GridSpace.CellToLocal(inst.LocalPosition) + q * halfSpan;
                    child.transform.localRotation = q;
                    ConfigureCollider(child, content.MeshOf(inst.Def), size);
                }
            }

            // Park any children left over from a larger previous layout.
            for (int i = used; i < _blockColliders.Count; i++)
                if (_blockColliders[i] != null && _blockColliders[i].activeSelf)
                    _blockColliders[i].SetActive(false);
        }

        private GameObject ColliderChild(int index)
        {
            while (_blockColliders.Count <= index)
            {
                var created = new GameObject($"BlockCollider_{_blockColliders.Count}");
                created.transform.SetParent(transform, false);
                _blockColliders.Add(created);
            }

            GameObject child = _blockColliders[index];
            if (!child.activeSelf) child.SetActive(true);
            return child;
        }

        /// <summary>
        /// Give a child the right collider. Both component types are kept and toggled rather than
        /// destroyed — Destroy is deferred to end-of-frame, so removing them would leave stale
        /// colliders live for the rest of the frame.
        /// </summary>
        private static void ConfigureCollider(GameObject go, Mesh src, GridPos size)
        {
            // MeshCollider needs CPU-side mesh data, same as the mesher.
            bool useMesh = src != null && src.isReadable && src.vertexCount > 0;

            var mc = go.GetComponent<MeshCollider>();
            var bc = go.GetComponent<BoxCollider>();

            if (useMesh)
            {
                if (mc == null) mc = go.AddComponent<MeshCollider>();
                mc.convex = true; // required once constructs become dynamic (vehicles, subgrids)
                if (mc.sharedMesh != src) mc.sharedMesh = src;
                mc.enabled = true;
                if (bc != null) bc.enabled = false;
            }
            else
            {
                if (bc == null) bc = go.AddComponent<BoxCollider>();
                bc.center = Vector3.zero; // the child already sits at the block's centre
                bc.size = new Vector3(size.X, size.Y, size.Z) * GridSpace.CellSize;
                bc.enabled = true;
                if (mc != null) mc.enabled = false;
            }
        }

        private static Material _fallback;

        private static Material FallbackMaterial()
        {
            if (_fallback != null) return _fallback;

            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            _fallback = new Material(sh) { name = "ConstructFallback" };
            return _fallback;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
