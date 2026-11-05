using UnityEngine;

namespace HyperInfinite
{
    /// <summary>
    /// TEMPORARY. Brings the player back after dying or falling out of the level, so testing never
    /// stops. The real game's death and checkpoint flow will replace it.
    /// </summary>
    public class TempRespawner : MonoBehaviour
    {
        [SerializeField] Transform spawnPoint;
        [Tooltip("Seconds to wait after dying.")]
        [SerializeField, Min(0f)] float delay = 2f;
        [Tooltip("Falling below this height counts as falling out of the level.")]
        [SerializeField] float killHeight = -20f;

        PlayerControls controls;
        PlayerMotor motor;
        Health health;
        float deadFor = -1f;

        /// <summary>Seconds until the respawn while dead; negative while alive.</summary>
        public float RespawnIn => deadFor < 0f ? -1f : Mathf.Max(0f, delay - deadFor);

        void Update()
        {
            if (!motor)
            {
                motor = FindAnyObjectByType<PlayerMotor>();
                if (!motor) return;
                controls = motor.GetComponent<PlayerControls>();
                health = motor.GetComponent<Health>();
            }

            if (motor.transform.position.y < killHeight)
            {
                Respawn();
                return;
            }
            if (health && health.IsDead)
            {
                if (deadFor < 0f)
                {
                    deadFor = 0f;
                    if (controls) controls.GameplayEnabled = false;   // the dead don't walk
                }
                deadFor += Time.deltaTime;
                if (deadFor >= delay) Respawn();
            }
        }

        void Respawn()
        {
            TempScuttler.ClearAll();
            if (health) health.Revive();
            Vector3 position = spawnPoint ? spawnPoint.position : Vector3.zero;
            float yaw = spawnPoint ? spawnPoint.eulerAngles.y : 0f;
            motor.Teleport(position, yaw);
            if (controls) controls.GameplayEnabled = true;
            deadFor = -1f;
        }
    }
}
