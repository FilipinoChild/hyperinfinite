using UnityEngine;

namespace HyperInfinite
{
    /// The INFINITE power-up's effect. Added to the player when they grab the pickup.
    /// While active, the shotgun adds no heat, and shotgun hits set enemies on fire.
    public class InfiniteBuff : MonoBehaviour
    {
        // Static, so any script (like Burnable on an enemy) can check it without a reference
        public static bool IsActive { get; private set; }

        EnergyBlastShotgun shotgun;
        float timeLeft;

        /// Gives the power-up to the player, adding this component the first time.
        public static void Give(GameObject player, float duration)
        {
            InfiniteBuff buff = player.GetComponent<InfiniteBuff>();
            if (!buff) buff = player.AddComponent<InfiniteBuff>();
            buff.Activate(duration);
        }

        void Activate(float duration)
        {
            if (!shotgun) shotgun = GetComponentInChildren<EnergyBlastShotgun>();
            timeLeft = Mathf.Max(timeLeft, duration);   // a second pickup refreshes the timer
            IsActive = true;
            if (shotgun) shotgun.HeatMultiplier = 0f;
            if (shotgun) shotgun.Supercharged = true;
            Debug.Log("INFINITE active for " + duration + " seconds");
            PowerUpBanner.Show("INFINITE\nNo overheating. Shots set enemies on fire!", 3.5f);
        }

        void Update()
        {
            if (!IsActive) return;
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f) End();
        }

        void End()
        {
            IsActive = false;
            timeLeft = 0f;
            if (shotgun) shotgun.HeatMultiplier = 1f;
            if (shotgun) shotgun.Supercharged = false;
            Debug.Log("INFINITE ended");
            PowerUpBanner.Show("INFINITE wore off", 2f);
        }

        // If the player is removed or the scene reloads mid-power-up, switch it off cleanly
        void OnDisable()
        {
            if (IsActive) End();
        }
    }
}