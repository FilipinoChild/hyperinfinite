using UnityEngine;

namespace HyperInfinite
{
    /// The Spitter's acid glob. It flies in a straight line and damages the player on contact.
    /// It has its own Health, so the player can shoot it out of the air.
    public class SpitterGlob : MonoBehaviour
    {
        [SerializeField, Min(1f)] float speed = 14f;
        [SerializeField, Min(0f)] float damage = 15f;
        [Tooltip("Seconds before it disappears if it hits nothing.")]
        [SerializeField, Min(0.5f)] float lifetime = 4f;

        Vector3 direction;
        GameObject owner;   // the Spitter that fired it

        /// Called by the Spitter right after creating the glob.
        public void Launch(Vector3 aimDirection, GameObject from)
        {
            direction = aimDirection.normalized;
            owner = from;
        }

        void Start()
        {
            Destroy(gameObject, lifetime);   // clean up if it never hits anything
        }

        void Update()
        {
            Vector3 start = transform.position;
            Vector3 end = start + direction * speed * Time.deltaTime;

            // Check the path it travels this frame, so it can't skip through thin things
            if (Physics.Linecast(start, end, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            {
                PlayerControls player = hit.collider.GetComponentInParent<PlayerControls>();
                if (player)
                {
                    IDamageable target = player.GetComponentInChildren<IDamageable>();
                    if (target != null)
                    {
                        target.TakeDamage(new DamageInfo
                        {
                            Amount = damage,
                            Kind = DamageKind.Generic,
                            Point = hit.point,
                            Direction = direction,
                            Distance = 0f,
                            Hits = 1,
                            Source = owner,
                        });
                    }
                }
                Destroy(gameObject);   // splat on whatever it hit
                return;
            }

            transform.position = end;
        }
    }
}