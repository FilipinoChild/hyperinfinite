using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace HyperInfinite
{
    /// <summary>
    /// Decides when fallen Arachturns drop a HYPER core, and drops it.
    ///  - Earned: a heavy bash that kills 5 or more at once, brand bursts included, drops a core where they fell.
    ///  - Safety net: a core is guaranteed on the 8th kill in a row without one, so every player gets to feel
    ///    HYPER, and the Hive Mother's summons drop them too in the boss fight.
    /// Only one core waits on the ground at a time, and none drop while HYPER is on, so it can't chain forever.
    ///
    /// Put one in any scene with the Player and give it the Hyper Core prefab.
    /// </summary>
    public class PowerUpDrops : MonoBehaviour
    {
        [Tooltip("The Hyper Core prefab.")]
        [SerializeField] HyperCore hyperCore;
        [Tooltip("A heavy bash that kills at least this many at once earns a core.")]
        [SerializeField, Min(1)] int multiKill = 5;
        [Tooltip("A core is guaranteed on this many kills in a row without one.")]
        [SerializeField, Min(1)] int guaranteedAfter = 8;

        [Tooltip("A core was dropped.")]
        public UnityEvent onCoreDropped = new UnityEvent();

        ShotgunMelee melee;
        HyperPower hyper;
        Health playerHealth;
        HyperCore waiting;
        int sinceLast;

        /// <summary>Kills since the last core, toward the guaranteed one.</summary>
        public int KillsSinceLast => sinceLast;

        void Start()
        {
            var controls = FindAnyObjectByType<PlayerControls>();
            if (controls)
            {
                hyper = controls.GetComponent<HyperPower>();
                playerHealth = controls.GetComponent<Health>();
                melee = controls.GetComponentInChildren<ShotgunMelee>();
            }
            if (melee) melee.onHeavyKills.AddListener(OnHeavyKills);
            Health.AnyDied += OnDied;
            if (!hyperCore) Debug.LogWarning("PowerUpDrops has no Hyper Core prefab, so nothing will drop.", this);
        }

        void OnDestroy()
        {
            if (melee) melee.onHeavyKills.RemoveListener(OnHeavyKills);
            Health.AnyDied -= OnDied;
        }

        bool CanDrop => hyperCore && !waiting && !(hyper && hyper.IsActive);

        void OnDied(Health died)
        {
            if (died == playerHealth || !CanDrop) return;
            sinceLast++;
            if (sinceLast >= guaranteedAfter) Drop(died.transform.position);
        }

        void OnHeavyKills(int kills)
        {
            // The deaths have each been counted already, so a guaranteed core may have just dropped.
            if (kills >= multiKill && CanDrop) Drop(melee.LastHeavyKillPoint);
        }

        void Drop(Vector3 where)
        {
            // On walkable ground near where they fell, so it never sits inside a rock or floats over a ledge.
            if (NavMesh.SamplePosition(where, out NavMeshHit hit, 3f, NavMesh.AllAreas)) where = hit.position;
            waiting = Instantiate(hyperCore, where, Quaternion.identity);
            sinceLast = 0;
            onCoreDropped.Invoke();
        }
    }
}
