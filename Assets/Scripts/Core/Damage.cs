using UnityEngine;

namespace HyperInfinite
{
    /// <summary>What kind of hit it was, so a target can react differently (a nest ignores bites, say).</summary>
    public enum DamageKind { Generic, Shotgun, Melee, Bite, Burst, Hazard }

    /// <summary>One hit. A shotgun blast arrives as one DamageInfo per target, with every pellet that landed added up.</summary>
    public struct DamageInfo
    {
        public float Amount;
        public DamageKind Kind;
        /// <summary>Where it landed (the first pellet, for a blast).</summary>
        public Vector3 Point;
        /// <summary>Which way it was traveling.</summary>
        public Vector3 Direction;
        /// <summary>How far the attacker was, in meters.</summary>
        public float Distance;
        /// <summary>Pellets that landed; 1 for single hits.</summary>
        public int Hits;
        /// <summary>Who did it. May be null.</summary>
        public GameObject Source;
    }

    /// <summary>
    /// Anything that can be hurt: target dummies, egg nests, larvae, the player, enemies.
    /// This is the shared contract, so the shotgun damages Branden's enemies the moment they implement it.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(DamageInfo damage);
    }
}
