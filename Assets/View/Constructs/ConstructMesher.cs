using System.Collections.Generic;
using UnityEngine;
using Aerow.Sim;
using Aerow.View.Build; // GridSpace

namespace Aerow.View
{
    /// <summary>
    /// Builds a construct's combined mesh. Two paths per block:
    ///   • authored mesh (<c>BlockDefSO.mesh</c>) → baked into the combined mesh at the block's
    ///     pose, so slopes/machines keep their real shape;
    ///   • no authored mesh → a whole cube per covered cell (procedural fallback).
    ///
    /// Pose convention: the mesh is authored centred on its origin in <b>cell units</b> (1 unit =
    /// 1 cell) and rotates about the <b>anchor cell's centre</b>, with the geometric centre offset
    /// by the rotated <c>(Size-1)/2</c> half-span — so geometry always lines up with the sim's
    /// occupancy for all 24 orientations.
    ///
    /// Face culling, greedy meshing, chunking, and per-block / atlas materials are TODO.
    /// </summary>
    public static class ConstructMesher
    {
        private static readonly GridPos[] Dirs = GridPos.FaceDirections; // +X,-X,+Y,-Y,+Z,-Z

        // In-plane axes per face, chosen so u × v == the face normal (outward CCW winding).
        private static readonly Vector3[] U =
        {
            new Vector3(0, 1, 0), new Vector3(0, 0, 1), new Vector3(0, 0, 1),
            new Vector3(1, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0),
        };
        private static readonly Vector3[] V =
        {
            new Vector3(0, 0, 1), new Vector3(0, 1, 0), new Vector3(1, 0, 0),
            new Vector3(0, 0, 1), new Vector3(0, 1, 0), new Vector3(1, 0, 0),
        };

        // TEMP DEBUG — which path each block took on the most recent build.
        public static int LastAuthoredBlocks { get; private set; }
        public static int LastProceduralBlocks { get; private set; }

        // Reused across rebuilds (main-thread only) to avoid per-build allocation.
        private static readonly List<Vector3> Verts = new List<Vector3>();
        private static readonly List<Vector3> Norms = new List<Vector3>();
        private static readonly List<Vector2> Uvs = new List<Vector2>();
        private static readonly List<int> Tris = new List<int>();

        /// <summary>
        /// Regenerate <paramref name="mesh"/> from the construct's blocks. <paramref name="content"/>
        /// supplies the block catalogue and authored meshes; when null every occupied cell falls
        /// back to a plain cube.
        /// </summary>
        public static void BuildMesh(Construct construct, GameContent content, Mesh mesh)
        {
            Verts.Clear();
            Norms.Clear();
            Uvs.Clear();
            Tris.Clear();
            LastAuthoredBlocks = 0;
            LastProceduralBlocks = 0;

            if (content == null)
            {
                foreach (GridPos cell in construct.OccupiedCells)
                    AppendCube(GridSpace.CellToLocal(cell));
            }
            else
            {
                BlockCatalogue blocks = content.Blocks;
                foreach (BlockInstance inst in construct.Blocks)
                {
                    BlockDef def = blocks.Get(inst.Def);
                    Mesh src = content.MeshOf(inst.Def);

                    if (IsUsable(src))
                    {
                        AppendAuthored(src, def, inst);
                        LastAuthoredBlocks++;
                    }
                    else
                    {
                        foreach (GridPos cell in Construct.Footprint(def.Size, inst.LocalPosition, inst.Orientation))
                            AppendCube(GridSpace.CellToLocal(cell));
                        LastProceduralBlocks++;
                    }
                }
            }

            mesh.Clear();
            mesh.SetVertices(Verts);
            mesh.SetNormals(Norms);
            mesh.SetUVs(0, Uvs);
            mesh.SetTriangles(Tris, 0);
            mesh.RecalculateBounds();
        }

        private static readonly HashSet<Mesh> Warned = new HashSet<Mesh>();

        /// <summary>
        /// True if an authored mesh can actually be read on the CPU. Imported models default to
        /// <b>Read/Write Enabled = off</b>, which discards the CPU copy — <c>mesh.vertices</c> then
        /// returns an empty array and the block would silently vanish. Warns once per mesh.
        /// </summary>
        private static bool IsUsable(Mesh src)
        {
            if (src == null) return false;

            if (!src.isReadable)
            {
                if (Warned.Add(src))
                    Debug.LogError($"[ConstructMesher] Mesh '{src.name}' is not readable — tick " +
                                   "'Read/Write Enabled' in its model import settings and Apply. " +
                                   "Falling back to a cube.", src);
                return false;
            }

            if (src.vertexCount == 0)
            {
                if (Warned.Add(src))
                    Debug.LogWarning($"[ConstructMesher] Mesh '{src.name}' has no vertices. " +
                                     "Falling back to a cube.", src);
                return false;
            }

            return true;
        }

        /// <summary>Bake an authored block mesh into the combined mesh at the instance's pose.</summary>
        private static void AppendAuthored(Mesh src, BlockDef def, BlockInstance inst)
        {
            Quaternion q = ToRotation(inst.Orientation);

            // Anchor cell centre → geometric centre, rotated with the block.
            var halfSpan = new Vector3(def.Size.X - 1, def.Size.Y - 1, def.Size.Z - 1) * (0.5f * GridSpace.CellSize);
            Vector3 pivot = GridSpace.CellToLocal(inst.LocalPosition) + q * halfSpan;
            Matrix4x4 m = Matrix4x4.TRS(pivot, q, Vector3.one * GridSpace.MeshScale);

            SrcMesh s = GetSource(src);
            int b = Verts.Count;

            for (int i = 0; i < s.Vertices.Length; i++)
            {
                Verts.Add(m.MultiplyPoint3x4(s.Vertices[i]));
                // Rotate normals directly — MultiplyVector would also apply the CellSize scale.
                Norms.Add(s.Normals != null && i < s.Normals.Length ? q * s.Normals[i] : Vector3.up);
                Uvs.Add(s.Uvs != null && i < s.Uvs.Length ? s.Uvs[i] : Vector2.zero);
            }
            for (int i = 0; i < s.Triangles.Length; i++)
                Tris.Add(b + s.Triangles[i]);
        }

        /// <summary>Emit a whole cube (all six faces) centred on <paramref name="centre"/>.</summary>
        private static void AppendCube(Vector3 centre)
        {
            float h = 0.5f * GridSpace.CellSize;

            for (int f = 0; f < 6; f++)
            {
                // TODO: skip faces shared with an occupied neighbour once culling comes back.
                var n = new Vector3(Dirs[f].X, Dirs[f].Y, Dirs[f].Z);
                Vector3 basePt = centre + n * h;
                Vector3 u = U[f] * h;
                Vector3 v = V[f] * h;

                int b = Verts.Count;
                Verts.Add(basePt - u - v);
                Verts.Add(basePt + u - v);
                Verts.Add(basePt + u + v);
                Verts.Add(basePt - u + v);
                Norms.Add(n); Norms.Add(n); Norms.Add(n); Norms.Add(n);
                Uvs.Add(new Vector2(0, 0)); Uvs.Add(new Vector2(1, 0));
                Uvs.Add(new Vector2(1, 1)); Uvs.Add(new Vector2(0, 1));
                Tris.Add(b); Tris.Add(b + 1); Tris.Add(b + 2);
                Tris.Add(b); Tris.Add(b + 2); Tris.Add(b + 3);
            }
        }

        /// <summary>
        /// Exact 90°-step rotation for one of the 24 orientations, rebuilt from the integer basis
        /// (<see cref="BlockOrientation.Rotate"/> moves grid cells; meshes need a quaternion).
        /// </summary>
        private static Quaternion ToRotation(BlockOrientation o)
        {
            Vector3 fwd = ToV3(o.Rotate(GridPos.Forward));
            Vector3 up = ToV3(o.Rotate(GridPos.Up));
            return Quaternion.LookRotation(fwd, up);
        }

        private static Vector3 ToV3(GridPos p) => new Vector3(p.X, p.Y, p.Z);

        public static void RebuildColliders(Construct construct, GameObject go)
        {
            // Minimal: one BoxCollider per cell — a slope still collides as a full cube for now.
            BoxCollider[] existing = go.GetComponents<BoxCollider>();
            for (int i = 0; i < existing.Length; i++) Object.Destroy(existing[i]);

            float s = GridSpace.CellSize;
            foreach (GridPos cell in construct.OccupiedCells)
            {
                BoxCollider bc = go.AddComponent<BoxCollider>();
                bc.center = GridSpace.CellToLocal(cell);
                bc.size = new Vector3(s, s, s);
            }
        }

        // ── Source-mesh cache ──
        // Mesh.vertices/normals/uv/triangles allocate a fresh array on every access, so read each
        // authored mesh once. Cleared by ClearCache() if a mesh asset changes at runtime.

        private sealed class SrcMesh
        {
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public Vector2[] Uvs;
            public int[] Triangles;
        }

        private static readonly Dictionary<Mesh, SrcMesh> SrcCache = new Dictionary<Mesh, SrcMesh>();

        private static SrcMesh GetSource(Mesh src)
        {
            if (SrcCache.TryGetValue(src, out SrcMesh cached)) return cached;

            cached = new SrcMesh
            {
                Vertices = src.vertices,
                Normals = src.normals,
                Uvs = src.uv,
                Triangles = src.triangles,
            };
            SrcCache[src] = cached;

            // TEMP DEBUG — authored size, to verify the mesh-units convention (a 1-cell block
            // should read ~0.25 on each axis).
            Debug.Log($"[ConstructMesher] Cached mesh '{src.name}' — {cached.Vertices.Length} verts, " +
                      $"authored bounds {src.bounds.size}.", src);

            return cached;
        }

        public static void ClearCache()
        {
            SrcCache.Clear();
            Warned.Clear();
        }
    }
}
