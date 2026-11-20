using System.Collections.Generic;
using UnityEngine;

namespace HyperInfinite
{
    [RequireComponent(typeof(Rigidbody))]
    public class AcidPool : MonoBehaviour
    {
        [SerializeField, Min(0f)] float damagePerTick = 6f;
        [Tooltip("Seconds between damage ticks.")]
        [SerializeField, Min(0.05f)] float tickInterval = 0.5f;
        [Tooltip("If on, the acid hurts enemies as well as the player.")]
        [SerializeField] bool hurtsEnemies = true;

        readonly HashSet<IDamageable> inside = new HashSet<IDamageable>();
        float tickTimer;

        void Awake()
        {
            Rigidbody body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        void OnTriggerStay(Collider other)
        {
            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target == null) return;   
            if (!hurtsEnemies && other.GetComponentInParent<PlayerControls>() == null) return;
            inside.Add(target);           
        }

        void FixedUpdate()
        {
            tickTimer -= Time.fixedDeltaTime;
            if (tickTimer <= 0f && inside.Count > 0)
            {
                tickTimer = tickInterval;
                foreach (IDamageable target in inside)
                {
                    if (target is Object obj && obj == null) continue;   
                    target.TakeDamage(new DamageInfo
                    {
                        Amount = damagePerTick,
                        Kind = DamageKind.Hazard,
                        Point = transform.position,
                        Direction = Vector3.up,
                        Distance = 0f,
                        Hits = 1,
                        Source = gameObject,
                    });
                }
            }
            inside.Clear(); 
        }
    }
}