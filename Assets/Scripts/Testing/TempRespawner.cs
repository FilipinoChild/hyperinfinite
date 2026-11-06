using UnityEngine;

namespace HyperInfinite
{
    /// <summary>
    /// TEMPORARY. Brings the player back to the spawn point after falling out of the level, so testing
    /// never stops. Dying is PlayerHud's job now: its Respawn button brings you back where you fell.
    /// </summary>
    public class TempRespawner : MonoBehaviour
    {
        [SerializeField] Transform spawnPoint;
        [Tooltip("Falling below this height counts as falling out of the level.")]
        [SerializeField] float killHeight = -20f;

        PlayerMotor motor;

        void Update()
        {
            if (!motor)
            {
                motor = FindAnyObjectByType<PlayerMotor>();
                if (!motor) return;
            }

            if (motor.transform.position.y < killHeight) Respawn();
        }

        void Respawn()
        {
            TinyScuttler.ClearAll();
            Vector3 position = spawnPoint ? spawnPoint.position : Vector3.zero;
            float yaw = spawnPoint ? spawnPoint.eulerAngles.y : 0f;
            motor.Teleport(position, yaw);
        }
    }
}
