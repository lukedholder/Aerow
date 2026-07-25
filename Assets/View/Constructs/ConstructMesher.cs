using System.Collections.Generic;
using UnityEngine;
using Aerow.Sim;
using Aerow.View.Build; // GridSpace

namespace Aerow.View
{
    /// <summary>
    /// Minimal construct mesher: renders every occupied cell as a unit cube with hidden-face
    /// culling (a face is emitted only when the neighbouring cell is empty), and rebuilds a
    /// per-cell box-collider compound. Greedy meshing, chunking, authored-mesh blocks, and
    /// per-block / atlas materials are TODO.
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

        // Reused across rebuilds (main-thread only) to avoid per-build allocation.
        private static readonly List<Vector3> Verts = new List<Vector3>();
        private static readonly List<Vector3> Norms = new List<Vector3>();
        private static readonly List<int> Tris = new List<int>();

        public static void BuildMesh(Construct construct, Mesh mesh)
        {
            Verts.Clear();
            Norms.Clear();
            Tris.Clear();

            float h = 0.5f * GridSpace.CellSize;

            foreach (GridPos cell in construct.OccupiedCells)
            {
                Vector3 center = GridSpace.CellToLocal(cell);
                for (int f = 0; f < 6; f++)
                {
                    if (construct.IsOccupied(cell + Dirs[f])) continue; // face hidden between two blocks

                    var n = new Vector3(Dirs[f].X, Dirs[f].Y, Dirs[f].Z);
                    Vector3 basePt = center + n * h;
                    Vector3 u = U[f] * h;
                    Vector3 v = V[f] * h;

                    int b = Verts.Count;
                    Verts.Add(basePt - u - v);
                    Verts.Add(basePt + u - v);
                    Verts.Add(basePt + u + v);
                    Verts.Add(basePt - u + v);
                    Norms.Add(n); Norms.Add(n); Norms.Add(n); Norms.Add(n);
                    Tris.Add(b); Tris.Add(b + 1); Tris.Add(b + 2);
                    Tris.Add(b); Tris.Add(b + 2); Tris.Add(b + 3);
                }
            }

            mesh.Clear();
            mesh.SetVertices(Verts);
            mesh.SetNormals(Norms);
            mesh.SetTriangles(Tris, 0);
            mesh.RecalculateBounds();
        }

        public static void RebuildColliders(Construct construct, GameObject go)
        {
            // Minimal: one BoxCollider per cell. (Greedy-merged boxes later.)
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
    }
}
