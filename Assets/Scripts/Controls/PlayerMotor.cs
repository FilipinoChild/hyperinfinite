using UnityEngine;
using UnityEngine.Events;

namespace HyperInfinite
{
    /// <summary>
    /// MOVEMENT. Runs, jumps, double-jumps and thruster-dashes
    /// the player with Unity's CharacterController. It reads PlayerControls and moves the way this
    /// object faces; the camera turns it. The feel numbers are the web prototype's, so tune them
    /// in the Inspector.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-20)]   // after input and the camera's turn, before shooting
    [DisallowMultipleComponent]
    public class PlayerMotor : MonoBehaviour, IPushable
    {
        [SerializeField] PlayerControls controls;

        [Header("Running")]
        [SerializeField, Min(0f)] float runSpeed = 9f;
        [Tooltip("How quickly you reach running speed, or stop, on the ground. Higher is snappier.")]
        [SerializeField, Min(0f)] float groundAcceleration = 14f;
        [Tooltip("The same in the air. Lower lets momentum carry you.")]
        [SerializeField, Min(0f)] float airAcceleration = 5f;

        [Header("Jumping")]
        [SerializeField, Min(0f)] float gravity = 26f;
        [Tooltip("Upward speed of a ground jump. 9 with gravity 26 clears about 1.5 m.")]
        [SerializeField, Min(0f)] float jumpSpeed = 9f;
        [Tooltip("Upward speed of a midair jump.")]
        [SerializeField, Min(0f)] float doubleJumpSpeed = 8.5f;
        [Tooltip("Jumps allowed in midair before you touch the ground again.")]
        [SerializeField, Min(0)] int airJumps = 1;
        [Tooltip("Seconds after running off a ledge when a jump still counts as a ground jump.")]
        [SerializeField, Range(0f, 0.3f)] float coyoteTime = 0.1f;
        [Tooltip("Seconds a jump press is remembered when you press it just before landing.")]
        [SerializeField, Range(0f, 0.3f)] float jumpBuffer = 0.12f;

        [Header("Thruster dash")]
        [SerializeField, Min(0f)] float dashSpeed = 34f;
        [SerializeField, Min(0.01f)] float dashDuration = 0.16f;
        [SerializeField, Min(0f)] float dashCooldown = 1f;
        [Tooltip("Share of the dash speed you keep when it ends.")]
        [SerializeField, Range(0f, 1f)] float dashCarry = 0.3f;
        [Tooltip("A midair dash flies level instead of falling.")]
        [SerializeField] bool levelAirDash = true;

        [Header("Ground")]
        [Tooltip("How far down to look for ground when running off a slope or step, so you stay on it instead of skipping off.")]
        [SerializeField, Range(0f, 1f)] float groundSnap = 0.45f;

        [Header("Events (hook sounds and effects up here)")]
        [Tooltip("1 for a ground jump, 2 for a double jump.")]
        public UnityEvent<int> onJump = new UnityEvent<int>();
        public UnityEvent onDash = new UnityEvent();
        [Tooltip("The speed you hit the ground at, in meters per second.")]
        public UnityEvent<float> onLand = new UnityEvent<float>();

        const float GroundStick = 2f;   // a small downward push on the ground so contact never flickers

        CharacterController body;
        Vector3 velocity, dashDirection;
        float coyoteTimer, jumpBufferTimer, dashTimer, dashCooldownTimer, airTime;
        int airJumpsUsed;

        public Vector3 Velocity => velocity;
        /// <summary>Speed across the ground, meters per second.</summary>
        public float Speed => new Vector2(velocity.x, velocity.z).magnitude;
        public bool IsGrounded { get; private set; }
        public bool IsDashing => dashTimer > 0f;
        /// <summary>0 straight after a dash, 1 when the next one is ready.</summary>
        public float DashReadiness => dashCooldown <= 0f ? 1f : Mathf.Clamp01(1f - dashCooldownTimer / dashCooldown);
        /// <summary>Seconds until the next dash is ready; 0 when it is.</summary>
        public float DashCooldownLeft => Mathf.Max(0f, dashCooldownTimer);
        public int AirJumpsLeft => Mathf.Max(0, airJumps - airJumpsUsed);
        public float RunSpeed => runSpeed;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            if (!controls) controls = GetComponent<PlayerControls>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 wish = WishDirection();
            bool jumpPressed = controls && controls.JumpPressed;

            dashCooldownTimer -= dt;
            jumpBufferTimer = jumpPressed ? jumpBuffer : jumpBufferTimer - dt;
            coyoteTimer = IsGrounded ? coyoteTime : coyoteTimer - dt;

            if (controls && controls.DashPressed && dashCooldownTimer <= 0f) StartDash(wish);
            bool jumped = (jumpPressed || jumpBufferTimer > 0f) && TryJump(jumpPressed);

            // Across the ground: the dash overrides running while it lasts.
            if (dashTimer > 0f)
            {
                dashTimer -= dt;
                velocity.x = dashDirection.x * dashSpeed;
                velocity.z = dashDirection.z * dashSpeed;
                if (dashTimer <= 0f)
                {
                    velocity.x *= dashCarry;
                    velocity.z *= dashCarry;
                }
            }
            else
            {
                float k = 1f - Mathf.Exp(-(IsGrounded ? groundAcceleration : airAcceleration) * dt);
                velocity.x += (wish.x * runSpeed - velocity.x) * k;
                velocity.z += (wish.z * runSpeed - velocity.z) * k;
            }

            // Up and down.
            if (IsDashing && levelAirDash && !IsGrounded) velocity.y = 0f;
            else velocity.y -= gravity * dt;

            float fallSpeed = -velocity.y;
            CollisionFlags hits = body.Move(velocity * dt);
            Vector3 moved = body.velocity;
            bool wasGrounded = IsGrounded;
            IsGrounded = body.isGrounded;
            if (!IsGrounded && wasGrounded && !jumped && velocity.y <= 0f) SnapToGround();

            if ((hits & CollisionFlags.Sides) != 0)
            {
                velocity.x = moved.x;   // walls soak up speed instead of storing it
                velocity.z = moved.z;
            }
            if ((hits & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;   // bumped your head

            if (IsGrounded)
            {
                if (!wasGrounded) Land(fallSpeed);
                if (velocity.y < 0f) velocity.y = -GroundStick;
                airTime = 0f;
            }
            else airTime += dt;
        }

        /// <summary>Moves the player instantly, for respawns and teleporters. Clears speed and the dash.</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            body.enabled = false;   // the controller would otherwise put the old position back
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            body.enabled = true;
            velocity = Vector3.zero;
            dashTimer = dashCooldownTimer = coyoteTimer = jumpBufferTimer = airTime = 0f;
            airJumpsUsed = 0;
            IsGrounded = false;
        }

        /// <summary>Knock-back from blasts, in meters per second. An upward push lifts you off the ground.</summary>
        public void Push(Vector3 velocityChange)
        {
            velocity += velocityChange;
            if (velocityChange.y > 0f)
            {
                IsGrounded = false;
                coyoteTimer = 0f;
            }
        }

        bool TryJump(bool freshPress)
        {
            if (IsGrounded || coyoteTimer > 0f)
            {
                velocity.y = jumpSpeed;
                IsGrounded = false;
                coyoteTimer = jumpBufferTimer = 0f;
                onJump.Invoke(1);
                return true;
            }
            if (freshPress && airJumpsUsed < airJumps)
            {
                airJumpsUsed++;
                velocity.y = doubleJumpSpeed;
                coyoteTimer = jumpBufferTimer = 0f;
                onJump.Invoke(1 + airJumpsUsed);
                return true;
            }
            return false;   // stays buffered in case you land in time
        }

        void StartDash(Vector3 wish)
        {
            dashDirection = wish.sqrMagnitude > 0.01f ? wish.normalized : Flat(transform.forward);
            dashTimer = dashDuration;
            dashCooldownTimer = dashCooldown;
            onDash.Invoke();
        }

        void Land(float impactSpeed)
        {
            airJumpsUsed = 0;
            if (airTime > 0.1f) onLand.Invoke(impactSpeed);
        }

        /// <summary>Running down a slope or off a low step: pull back onto the ground if it's close.</summary>
        void SnapToGround()
        {
            if (groundSnap <= 0f) return;
            Vector3 feet = transform.TransformPoint(body.center) + Vector3.down * (body.height * 0.5f - body.radius);
            if (!Physics.SphereCast(feet, body.radius * 0.95f, Vector3.down, out RaycastHit hit, groundSnap + body.skinWidth,
                                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return;
            if (Vector3.Angle(hit.normal, Vector3.up) > body.slopeLimit) return;
            body.Move(Vector3.down * (hit.distance + 0.02f));
            IsGrounded = body.isGrounded;
        }

        Vector3 WishDirection()
        {
            if (!controls) return Vector3.zero;
            Vector2 m = controls.Move;
            Vector3 wish = Flat(transform.right) * m.x + Flat(transform.forward) * m.y;
            return wish.sqrMagnitude > 1f ? wish.normalized : wish;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }
    }
}
