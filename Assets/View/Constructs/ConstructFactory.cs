using UnityEngine;
using Aerow.Sim;

namespace Aerow.View
{
    /// <summary>
    /// Creates a sim <see cref="Construct"/> — registered in the <see cref="Simulation"/>, which
    /// owns it and allocates its id — together with its <see cref="ConstructView"/> GameObject
    /// (MeshFilter + MeshRenderer + colliders). Used to seed a new construct when the first block
    /// is placed on terrain. No Rigidbody yet (anchored foundations are static colliders).
    /// </summary>
    public static class ConstructFactory
    {
        /// <summary>Create a new empty construct in the simulation and a view for it.</summary>
        public static ConstructView Create(Simulation sim, Vector3 position, Quaternion rotation, Material material)
            => CreateViewFor(sim.CreateConstruct(), position, rotation, material);

        /// <summary>
        /// Build a view for a construct that already exists in the simulation — e.g. a piece handed
        /// back by <see cref="Construct.Split"/> after <see cref="Simulation.Adopt"/>.
        /// </summary>
        public static ConstructView CreateViewFor(Construct construct, Vector3 position, Quaternion rotation, Material material)
        {
            var go = new GameObject($"Construct_{construct.Id}");
            go.transform.SetPositionAndRotation(position, rotation);

            var view = go.AddComponent<ConstructView>(); // RequireComponent adds MeshFilter/MeshRenderer
            view.Initialize(construct, material);
            return view;
        }
    }
}
