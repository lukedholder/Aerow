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
        public void Initialize(Construct construct)
        {
            Construct = construct;
            Rebuild();
        }

        /// <summary>Regenerate the mesh + colliders from the current block layout.</summary>
        public void Rebuild()
        {
            if (Construct == null) return;

            if (_filter == null) _filter = GetComponent<MeshFilter>();
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            if (material != null) _renderer.sharedMaterial = material;

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "Construct" };
                _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // big ships exceed 65k verts
                _mesh.MarkDynamic();
            }

            ConstructMesher.BuildMesh(Construct, _mesh);
            _filter.sharedMesh = _mesh;
            ConstructMesher.RebuildColliders(Construct, gameObject);

            Debug.Log($"[ConstructView] Rebuilt construct #{Construct.Id} — " +
                      $"{Construct.BlockCount} block(s), {_mesh.vertexCount} verts."); // TEMP DEBUG
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
