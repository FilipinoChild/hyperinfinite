using System.Reflection;
using UnityEngine;

namespace HyperInfinite
{
    /// <summary>
    /// Rebuilds the level's NavMesh when the scene loads, from whatever geometry is in it now. Enemies
    /// find their way with the NavMesh, and one baked in the editor goes stale the moment a wall, step
    /// or rock moves: they'd refuse to follow the player onto new ground. With this, nobody has to
    /// remember to press Bake.
    ///
    /// Put it on the object that has the NavMeshSurface, such as Arena. It runs before everything
    /// else in the scene, so the NavMesh is ready before any enemy looks for it. For an arena this size
    /// the rebuild takes a moment at load and nothing after.
    ///
    /// It finds the surface by name and asks it to build, so this script compiles whether or not the
    /// AI Navigation package is where the project's scripts can see it.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public class NavMeshAtStart : MonoBehaviour
    {
        void Awake()
        {
            Component surface = GetComponent("NavMeshSurface");
            MethodInfo build = surface ? surface.GetType().GetMethod("BuildNavMesh", BindingFlags.Public | BindingFlags.Instance) : null;
            if (build == null)
            {
                Debug.LogWarning("NavMeshAtStart on " + name + " found no NavMeshSurface to rebuild, so the NavMesh baked in the editor is used as it is.", this);
                return;
            }
            build.Invoke(surface, null);
        }
    }
}
