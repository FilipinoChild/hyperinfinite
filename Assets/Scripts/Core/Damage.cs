using UnityEngine;

namespace HyperInfinite
{
    /// <summary>What kind of hit it was, so a target can react differently (a nest ignores bites, say).</summary>
    public enum DamageKind { Generic, Shotgun, Melee, Bite, Burst, Hazard }

    /// <summary>One hit. A shotgun blast arrives as one DamageInfo per target, with every bolt that landed added up.</summary>
    public struct DamageInfo
    {
        public float Amount;
        public DamageKind Kind;
        /// <summary>Where it landed (the first bolt, for a blast).</summary>
        public Vector3 Point;
        /// <summary>Which way it was traveling.</summary>
        public Vector3 Direction;
        /// <summary>How far the attacker was, in meters.</summary>
        public float Distance;
        /// <summary>Bolts that landed; 1 for single hits.</summary>
        public int Hits;
        /// <summary>Who did it. May be null.</summary>
        public GameObject Source;
    }

    /// <summary>
    /// Anything that can be hurt: target dummies, eggs, Scuttlers, the player, enemies.
    /// This is the shared contract, so the shotgun damages any enemies that implement it.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(DamageInfo damage);
    }

    /// <summary>Anything that can be shoved by a blast: knock-back, in meters per second.</summary>
    public interface IPushable
    {
        void Push(Vector3 velocityChange);
    }
}
