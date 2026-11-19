using UnityEngine;

namespace HyperInfinite
{
    /// <summary>
    /// Life in the air of a level: specks of silk and spore drifting slowly past the player. They're
    /// PixelFx squares in the game's palette, so they match every other effect, and they shrink away in
    /// steps as they age. Only a couple of dozen exist at a time, always near the camera, so the cost
    /// is next to nothing however big the level is.
    ///
    /// Put it on any object in the scene. It needs nothing else.
    /// </summary>
    public class ArenaAmbience : MonoBehaviour
    {
        [Tooltip("How many specks appear each second. Each lives a few seconds, so about four times this many are in the air.")]
        [SerializeField, Range(0f, 30f)] float specksPerSecond = 6f;
        [Tooltip("Specks appear within this distance of the camera, in meters.")]
        [SerializeField, Min(2f)] float range = 16f;
        [Tooltip("The highest a speck appears above the camera's feet, in meters.")]
        [SerializeField, Min(0.5f)] float ceiling = 5f;
        [Tooltip("Which way the air is moving, in meters per second.")]
        [SerializeField] Vector3 drift = new Vector3(0.4f, 0.22f, 0.12f);

        Transform view;
        float owed;

        void Update()
        {
            if (!view)
            {
                Camera main = Camera.main;
                if (!main) return;
                view = main.transform;
            }
            owed += specksPerSecond * Time.deltaTime;
            while (owed >= 1f)
            {
                owed -= 1f;
                Vector2 spot = Random.insideUnitCircle * range;
                Vector3 eye = view.position;
                var position = new Vector3(eye.x + spot.x, eye.y - 1.4f + Random.value * ceiling, eye.z + spot.y);
                Vector3 velocity = drift + Random.insideUnitSphere * 0.2f;
                // Mostly pale silk, with the odd pink spore from the eggs.
                Color color = Random.value < 0.75f ? Palette.Dim : Palette.EggPink;
                PixelFx.Mote(position, velocity, color, Random.Range(0.035f, 0.06f), Random.Range(3f, 5f));
            }
        }
    }
}
