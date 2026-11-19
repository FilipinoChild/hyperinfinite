using System.Collections.Generic;
using UnityEngine;

namespace HyperInfinite
{
    /// <summary>
    /// Marks an enemy as a boss, so the HUD shows its name and health in the boss bar at the top of the
    /// screen. The bar comes up when the boss is switched on and goes once it dies, so a boss that waits
    /// switched off until its cue (as the Hive Mother does, until the last wave is cleared) works as it is.
    ///
    /// Put it on the boss, next to its Health. Bosses add themselves to a short list when switched on, so
    /// the HUD never has to search the scene for one.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public class Boss : MonoBehaviour
    {
        [Tooltip("The name over the boss bar. Capitals suit the pixel font.")]
        [SerializeField] string displayName = "HIVE MOTHER";

        static readonly List<Boss> active = new List<Boss>();

        /// <summary>The boss the bar shows: the first one switched on that's still around. Null if there's none.</summary>
        public static Boss Current => active.Count > 0 ? active[0] : null;

        public string DisplayName => displayName;
        public Health Health { get; private set; }

        void Awake()
        {
            Health = GetComponent<Health>();
        }

        void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
        }

        void OnDisable()
        {
            active.Remove(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            active.Clear();   // with domain reload off, nothing from the last Play carries over
        }
    }
}
