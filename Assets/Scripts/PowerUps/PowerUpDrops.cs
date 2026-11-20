using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace HyperInfinite
{
    /// <summary>
    /// Decides when fallen Arachturns drop a HYPER core, and drops it.
    ///  - Earned: every heavy bash that kills 5 or more at once, brand bursts included, drops a core where the
    ///    most of them fell. It rewards the play the game is built around: soften a pack, brand it, wipe it.
    ///  - Safety net: every wave guarantees one, so it isn't only for players who have mastered branding. When a
    ///    pack arrives, one of the kills in its second half is picked at random, and that kill drops a core,
    ///    unless one has already dropped since the pack arrived or HYPER is on.
    ///  - Outside the waves (the Hive Mother's summons), a core comes every so many kills instead.
    /// Only one core waits at a time: earning another while one waits starts its wait over instead. None drop
    /// while HYPER is on, so it can't chain forever.
    ///
    /// Put one in any scene with the Player and give it the Hyper Core prefab. It finds the ScuttlerSpawner itself.
    /// </summary>
    public class PowerUpDrops : MonoBehaviour
    {
        [Tooltip("The Hyper Core prefab.")]
        [SerializeField] HyperCore hyperCore;
        [Tooltip("A heavy bash that kills at least this many at once earns a core.")]
        [SerializeField, Min(1)] int multiKill = 5;
        [Tooltip("Kills that aren't part of a wave, such as the Hive Mother's summons: a core comes every this many.")]
        [SerializeField, Min(1)] int guaranteedAfter = 8;

        [Tooltip("A core was dropped.")]
        public UnityEvent onCoreDropped = new UnityEvent();

        /// <summary>One wave's pack, and which of its kills drops its core.</summary>
        class Pack
        {
            public readonly List<GameObject> members = new List<GameObject>(24);
            public int wave, dead, half, dropOn;
            public bool covered;   // a core has dropped since it arrived, or HYPER was on when its turn came
        }

        readonly List<Pack> packs = new List<Pack>(8);
        readonly Stack<Pack> spare = new Stack<Pack>(8);
        ShotgunMelee melee;
        HyperPower hyper;
        Health playerHealth;
        ScuttlerSpawner spawner;
        HyperCore waiting;
        int sinceLast;

        bool HyperOn => hyper && hyper.IsActive;
        bool CanDrop => hyperCore && !waiting && !HyperOn;

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
            spawner = FindAnyObjectByType<ScuttlerSpawner>();
            if (spawner) spawner.onPackSpawned.AddListener(OnPackSpawned);
            Health.AnyDied += OnDied;
            if (!hyperCore) Debug.LogWarning("PowerUpDrops has no Hyper Core prefab, so nothing will drop.", this);
        }

        void OnDestroy()
        {
            if (melee) melee.onHeavyKills.RemoveListener(OnHeavyKills);
            if (spawner) spawner.onPackSpawned.RemoveListener(OnPackSpawned);
            Health.AnyDied -= OnDied;
        }

        /// <summary>A wave arrives: note its Scuttlers, and pick which kill in its second half drops its core.</summary>
        void OnPackSpawned(int wave)
        {
            IReadOnlyList<GameObject> sent = spawner.LastPack;
            if (sent.Count == 0) return;
            Pack pack = spare.Count > 0 ? spare.Pop() : new Pack();
            pack.members.Clear();
            for (int i = 0; i < sent.Count; i++) pack.members.Add(sent[i]);
            pack.wave = wave;
            pack.dead = 0;
            pack.covered = false;
            pack.half = (sent.Count + 1) / 2;
            pack.dropOn = Random.Range(pack.half, sent.Count + 1);   // counted from 1: a kill from halfway to the last
            packs.Add(pack);
        }

        void OnDied(Health died)
        {
            if (!died || died == playerHealth) return;
            Pack pack = PackOf(died.gameObject);
            if (pack == null)
            {
                Loose(died);
                return;
            }
            pack.dead++;
            if (!pack.covered && pack.dead >= pack.dropOn)
            {
                // This pack's core: dropped now if it can be. If HYPER is on, or a core is already waiting, the
                // player has one, which counts.
                pack.covered = true;
                if (CanDrop) Drop(died.transform.position);
            }
            if (pack.dead >= pack.members.Count)
            {
                packs.Remove(pack);
                spare.Push(pack);
            }
        }

        /// <summary>A kill from outside the waves, such as one of the Hive Mother's summons.</summary>
        void Loose(Health died)
        {
            if (!CanDrop) return;
            sinceLast++;
            if (sinceLast >= guaranteedAfter) Drop(died.transform.position);
        }

        void OnHeavyKills(int kills)
        {
            if (kills < multiKill || !hyperCore || HyperOn) return;
            if (waiting) waiting.Renew();   // one waits already, perhaps this pack's own: its wait starts over
            else Drop(melee.LastHeavyKillPoint);
        }

        Pack PackOf(GameObject scuttler)
        {
            for (int p = 0; p < packs.Count; p++)
            {
                List<GameObject> members = packs[p].members;
                for (int i = 0; i < members.Count; i++)
                    if (members[i] == scuttler) return packs[p];
            }
            return null;
        }

        void Drop(Vector3 where)
        {
            // On walkable ground near where they fell, so it never sits inside a rock or floats over a ledge.
            if (NavMesh.SamplePosition(where, out NavMeshHit hit, 3f, NavMesh.AllAreas)) where = hit.position;
            waiting = Instantiate(hyperCore, where, Quaternion.identity);
            sinceLast = 0;
            // A wave that's into its second half has had its core. One that has only just arrived hasn't: it still gets its own.
            for (int p = 0; p < packs.Count; p++)
                if (packs[p].dead >= packs[p].half) packs[p].covered = true;
            onCoreDropped.Invoke();
        }

        /// <summary>For the debug panel: where the next core stands.</summary>
        public string Describe()
        {
            if (!hyperCore) return "no Hyper Core prefab: nothing drops";
            string state = HyperOn ? "HYPER is on, so none drop" : waiting ? "a core is waiting (" + waiting.TimeLeft.ToString("0.0") + " s)" : "ready";
            string wave = "";
            for (int p = 0; p < packs.Count; p++)
            {
                Pack pack = packs[p];
                wave += "  ·  wave " + pack.wave + ": " + pack.dead + " of " + pack.members.Count + " down, "
                        + (pack.covered ? "core given" : "core on kill " + pack.dropOn);
            }
            return "HYPER cores: " + state + "  ·  " + multiKill + "+ in one heavy bash earns one" + wave;
        }
    }
}
