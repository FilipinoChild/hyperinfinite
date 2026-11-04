using UnityEngine;

namespace HyperInfinite
{
    /// <summary>Ray tests that skip the shooter's own colliders, so no layer set-up is needed.</summary>
    public static class Raycasts
    {
        static readonly RaycastHit[] buffer = new RaycastHit[32];

        /// <summary>
        /// The nearest hit along the ray, ignoring anything under <paramref name="ignore"/>. Trigger colliders
        /// only count when <paramref name="damageableTriggers"/> is on and they belong to something
        /// IDamageable (larvae use trigger colliders so they never block the player).
        /// </summary>
        public static bool Nearest(Vector3 origin, Vector3 direction, float maxDistance, Transform ignore,
                                   out RaycastHit nearest, bool damageableTriggers = true)
        {
            nearest = default;
            var query = damageableTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
            int count = Physics.RaycastNonAlloc(origin, direction, buffer, maxDistance, Physics.DefaultRaycastLayers, query);
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = buffer[i];
                if (hit.distance >= best) continue;
                Collider c = hit.collider;
                if (ignore && c.transform.IsChildOf(ignore)) continue;
                if (c.isTrigger && c.GetComponentInParent<IDamageable>() == null) continue;
                best = hit.distance;
                nearest = hit;
            }
            return best < float.PositiveInfinity;
        }
    }
}
