using UnityEngine;

namespace HyperInfinite
{
    /// How a creature's flat sprite is shown. It turns the sprite to face the camera every frame,
    /// so it never looks flat from the side; it only turns left and right, so it stays upright.
    ///
    /// It also gives the sprite a walk. The art is a single picture, so the walk is made by
    /// moving it: the sprite rocks from side to side and bounces with every step, as fast as its
    /// owner (the object above this one) actually moves, and settles when it stops. The pose
    /// changes 12 times a second, like frames of hand-drawn animation, to match the pixel art.
    ///
    /// AI scripts can add to the pose: Squash flattens or stretches the sprite (a crouch before a
    /// bite, rearing up before a slam) and Tint colors it (flashing red as a warning).
    public class FaceCamera : MonoBehaviour
    {
        [Header("Walking")]
        [Tooltip("Rock and bounce while the object above this one moves. Switch off for sprites that should hold still.")]
        [SerializeField] bool walk = true;
        [Tooltip("Steps taken per meter traveled. Higher makes smaller, quicker steps.")]
        [SerializeField, Min(0f)] float stepsPerMeter = 0.9f;
        [Tooltip("Degrees the sprite rocks to each side with every step.")]
        [SerializeField, Range(0f, 30f)] float rock = 6f;
        [Tooltip("How much it squashes and stretches with every step: 0.07 is 7%.")]
        [SerializeField, Range(0f, 0.5f)] float bounce = 0.07f;
        [Tooltip("How many times a second the pose changes. 12 looks hand-animated; 0 is smooth.")]
        [SerializeField, Range(0f, 60f)] float framesPerSecond = 12f;

        // One camera lookup shared by every sprite in the scene
        static Transform cameraTransform;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        Transform mover;                 // whatever carries this sprite around
        Vector3 lastPosition, restScale;
        float stride, moving, frameClock;
        float shownRock, shownBounce, shownStretch;
        SpriteRenderer flatSprite;       // a SpriteRenderer takes a color directly...
        Renderer meshSprite;             // ...a quad takes it through a property block
        MaterialPropertyBlock block;
        Color spriteColor = Color.white, shownTint = Color.white;

        /// Extra squash or stretch on top of the walk: -0.3 crouches the sprite 30% flatter
        /// (and a little wider), 0.2 stretches it 20% taller. 0 is its normal shape.
        public float Squash { get; set; }

        /// Colors the sprite: white leaves it as drawn, anything else multiplies its colors.
        public Color Tint { get; set; } = Color.white;

        void Awake()
        {
            mover = transform.parent ? transform.parent : transform;
            lastPosition = mover.position;
            restScale = transform.localScale;
            stride = Random.value * 2f;   // so a pack doesn't step in unison
            flatSprite = GetComponentInChildren<SpriteRenderer>();
            if (flatSprite) spriteColor = flatSprite.color;
            else meshSprite = GetComponentInChildren<Renderer>();
        }

        void LateUpdate()
        {
            // Find the camera once and remember it
            if (!cameraTransform)
            {
                Camera cam = Camera.main ? Camera.main : FindAnyObjectByType<Camera>();
                if (!cam) return;
                cameraTransform = cam.transform;
            }

            // Face the same way the camera faces, ignoring tilt up and down
            Vector3 forward = cameraTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) return;

            float dt = Time.deltaTime;
            if (walk && dt > 0f) Walk(dt);
            transform.rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(0f, 0f, shownRock);

            float stretch = shownBounce + Squash;
            if (stretch != shownStretch)
            {
                shownStretch = stretch;
                // Taller means narrower, and the other way around, so it keeps its bulk
                transform.localScale = new Vector3(restScale.x * (1f - stretch * 0.5f), restScale.y * (1f + stretch), restScale.z);
            }
            if (Tint != shownTint) ApplyTint();
        }

        /// Works out this frame's walking pose from how far the owner just moved.
        void Walk(float dt)
        {
            Vector3 position = mover.position;
            Vector3 step = position - lastPosition;
            step.y = 0f;
            lastPosition = position;
            float distance = step.magnitude;
            bool isMoving = distance > 0.3f * dt;   // faster than 0.3 meters a second
            moving = Mathf.MoveTowards(moving, isMoving ? 1f : 0f, dt * 6f);
            stride += distance * stepsPerMeter;

            // Hold each pose for a frame of animation, then take the next
            frameClock += dt;
            if (framesPerSecond > 0f && frameClock < 1f / framesPerSecond) return;
            frameClock = 0f;
            float swing = Mathf.Sin(stride * Mathf.PI);   // left on one step, right on the next
            shownRock = swing * rock * moving;
            shownBounce = Mathf.Abs(swing) * bounce * moving;
        }

        void ApplyTint()
        {
            shownTint = Tint;
            if (flatSprite)
            {
                flatSprite.color = spriteColor * shownTint;
                return;
            }
            if (!meshSprite) return;
            if (shownTint == Color.white)
            {
                meshSprite.SetPropertyBlock(null);   // back to the plain material
                return;
            }
            if (block == null) block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, shownTint);   // URP
            block.SetColor(ColorId, shownTint);       // Built-in
            meshSprite.SetPropertyBlock(block);
        }
    }
}
