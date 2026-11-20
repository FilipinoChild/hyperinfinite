using UnityEngine;

namespace HyperInfinite
{
    /// The glowing pickup. It bobs and spins, and gives the player INFINITE on touch.
    [RequireComponent(typeof(Rigidbody))]
    public class InfinitePickup : MonoBehaviour
    {
        [Tooltip("Seconds the power-up lasts.")]
        [SerializeField, Min(1f)] float duration = 10f;
        [Tooltip("Seconds before the pickup disappears if nobody grabs it.")]
        [SerializeField, Min(1f)] float lifetime = 15f;
        [Tooltip("The visible part, which bobs and spins.")]
        [SerializeField] Transform visual;

        Vector3 visualRest;

        void Awake()
        {
            // Trigger events need a Rigidbody; kinematic means physics never moves it
            Rigidbody body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            if (visual) visualRest = visual.localPosition;
        }

        void Start()
        {
            Destroy(gameObject, lifetime);
        }

        void Update()
        {
            if (!visual) return;
            visual.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
            visual.localPosition = visualRest + Vector3.up * (Mathf.Sin(Time.time * 3f) * 0.15f);
        }

        void OnTriggerEnter(Collider other)
        {
            PlayerControls player = other.GetComponentInParent<PlayerControls>();
            if (!player) return;   // only the player can collect it

            InfiniteBuff.Give(player.gameObject, duration);
            Destroy(gameObject);
        }
    }
}