using UnityEngine;
using Aerow.Sim;

namespace Aerow.View
{
    /// <summary>
    /// The view of one sim <see cref="Sim.Construct"/>: owns the mesh + colliders and rebuilds them
    /// from the block data via <see cref="ConstructMesher"/>. Minimal for now — a single material,
    /// per-cell cube meshing, per-cell box colliders. A Rigidbody, rotor joints, and per-block
    /// authored visuals come later.
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
            ConstructMesher.RebuildColliders(Construct, gameObject);

            // TEMP DEBUG — everything needed to diagnose an invisible construct in one line.
            Debug.Log($"[ConstructView] #{Construct.Id}: {Construct.BlockCount} block(s) " +
                      $"({ConstructMesher.LastAuthoredBlocks} authored / {ConstructMesher.LastProceduralBlocks} cube) → " +
                      $"{_mesh.vertexCount} verts, {_mesh.triangles.Length / 3} tris, " +
                      $"bounds {_mesh.bounds.size}, mat '{(_renderer.sharedMaterial != null ? _renderer.sharedMaterial.name : "NULL")}', " +
                      $"content {(GameBootstrap.IsReady ? "ready" : "NULL")}", this);
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
