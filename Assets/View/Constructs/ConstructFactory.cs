using UnityEngine;
using Aerow.Sim;

namespace Aerow.View
{
    /// <summary>
    /// Creates a sim <see cref="Construct"/> together with its <see cref="ConstructView"/>
    /// GameObject (MeshFilter + MeshRenderer + colliders). Used to seed a new construct when the
    /// first block is placed on terrain. Minimal: a static runtime id counter, no world registry
    /// or Rigidbody yet (anchored foundations are static colliders).
    /// </summary>
    public static class ConstructFactory
    {
        private static int _nextId = 1;

        public static ConstructView Create(BlockCatalogue blocks, Vector3 position, Quaternion rotation, Material material)
        {
            var go = new GameObject($"Construct_{_nextId}");
            go.transform.SetPositionAndRotation(position, rotation);

            var construct = new Construct(blocks, _nextId++);
            var view = go.AddComponent<ConstructView>(); // RequireComponent adds MeshFilter/MeshRenderer
            view.Initialize(construct, material);
            return view;
        }
    }
}
