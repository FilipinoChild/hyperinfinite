using UnityEngine;

namespace HyperInfinite
{
    /// Sits in the level. If the player chose "Boss Fight" on the title screen,
    /// it skips the Scuttler waves and wakes the Hive Mother right away.
    public class LevelStart : MonoBehaviour
    {
        // "static" means this value belongs to the class, not to any one object,
        // so it survives loading a new scene. The title menu sets it; this reads it.
        public static bool StartAtBoss;

        [SerializeField] GameObject scuttlerSpawner;
        [SerializeField] GameObject hiveMother;

        void Awake()
        {
            if (!StartAtBoss) return;
            StartAtBoss = false;   // reset it so the next normal start isn't affected

            if (scuttlerSpawner) scuttlerSpawner.SetActive(false);   // no waves
            if (hiveMother) hiveMother.SetActive(true);              // boss appears now
        }
    }
}