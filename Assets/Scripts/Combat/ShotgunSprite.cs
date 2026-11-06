using UnityEngine;
using UnityEngine.UI;

namespace HyperInfinite
{
    /// <summary>
    /// The Energy Blast shotgun gun drawn over the bottom right of the view, 2.5D. It bobs while you run,
    /// kicks down on every blast, dips while the lever cycles, slides toward the middle as you aim, and
    /// drops and tilts while venting or overheated. Its emitter cells glow with the gun's charge and heat
    /// (EnergyBlastShotgun.GlowColor), and the muzzle Transform is kept on the sprite's muzzle so the bolts
    /// start from it.
    ///
    /// Put it next to EnergyBlastShotgun, under the camera. It makes its own screen-space canvas, so the
    /// gun never pokes into walls. The texture needs Read/Write switched on for the emitter glow, and
    /// Point filtering to keep its pixels crisp; Hyper Infinite > Build Test Level sets both.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnergyBlastShotgun))]
    public class ShotgunSprite : MonoBehaviour
    {
        [Tooltip("The gun's pixel art: Assets/Textures/Sprites/EnergyBlastShotgun.png.")]
        [SerializeField] Texture2D sprite;
        [Tooltip("Moved every frame to the muzzle on screen. Put it in the shotgun's Emitters, with the Muzzle Flash light under it.")]
        [SerializeField] Transform muzzle;

        [Header("Placement (shares of the screen)")]
        [Tooltip("The gun's height. The prototype's is 40% of the screen.")]
        [SerializeField, Range(0.1f, 1f)] float height = 0.4f;
        [Tooltip("Gap between the gun's right edge and the screen's.")]
        [SerializeField, Range(-0.5f, 0.5f)] float rightMargin = 0.06f;
        [Tooltip("Where its bottom edge sits; a little below the screen.")]
        [SerializeField, Range(-0.5f, 0.5f)] float bottom = -0.01f;
        [Tooltip("How far it slides toward the middle while aiming.")]
        [SerializeField, Range(0f, 0.5f)] float aimShift = 0.14f;
        [Tooltip("The muzzle in the image, in pixels from its top-left corner.")]
        [SerializeField] Vector2 muzzlePixel = new Vector2(192f, 24f);
        [Tooltip("How far in front of the camera the muzzle sits in the world, in meters.")]
        [SerializeField, Min(0.05f)] float muzzleDepth = 0.5f;

        [Header("Motion (pixels on a 720-pixel-tall screen)")]
        [SerializeField, Min(0f)] float bob = 10f;
        [SerializeField, Min(0f)] float kick = 40f;

        // From the prototype: the gun rotates around a point 80% of the way across its bottom edge.
        static readonly Vector2 Pivot = new Vector2(0.8f, 0f);
        const float FullGlow = 2.4f;   // GlowColor's brightest channel when fully charged

        EnergyBlastShotgun shotgun;
        PlayerMotor motor;
        Camera view;
        Canvas canvas;
        RectTransform gun;
        RawImage body, glow;
        Texture2D bodyTexture, glowTexture;
        float bobTime, bobAmount, down;

        void Awake()
        {
            shotgun = GetComponent<EnergyBlastShotgun>();
            motor = GetComponentInParent<PlayerMotor>();
            view = GetComponentInParent<Camera>();
            if (!sprite)
            {
                Debug.LogError("ShotgunSprite on " + name + " has no sprite. Drag Assets/Textures/Sprites/EnergyBlastShotgun.png " +
                               "into it, or run Hyper Infinite > Build Test Level again.", this);
                enabled = false;
                return;
            }
            SplitGlow();
            BuildCanvas();
        }

        void OnEnable()
        {
            if (canvas) canvas.enabled = true;
        }

        void OnDisable()
        {
            if (canvas) canvas.enabled = false;
        }

        void OnDestroy()
        {
            if (canvas) Destroy(canvas.gameObject);
            if (bodyTexture && bodyTexture != sprite) Destroy(bodyTexture);
            if (glowTexture) Destroy(glowTexture);
        }

        /// <summary>
        /// Splits the art in two: the gun with its emitter cells darkened, and a white mask of just those cells,
        /// which is tinted with the glow. Needs Read/Write on the texture; without it the gun is drawn as it is.
        /// </summary>
        void SplitGlow()
        {
            bodyTexture = sprite;
            if (!sprite.isReadable)
            {
                Debug.LogWarning("ShotgunSprite: " + sprite.name + " needs Read/Write switched on in its import settings for the " +
                                 "emitter glow. Hyper Infinite > Build Test Level sets it.", this);
                return;
            }
            Color32[] pixels = sprite.GetPixels32();
            var mask = new Color32[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                if (p.a > 128 && p.b > 150 && p.b - p.r > 60)   // the cyan cells
                {
                    mask[i] = new Color32(255, 255, 255, 255);
                    pixels[i] = new Color32(24, 34, 40, 255);   // an unlit cell
                }
                else mask[i] = new Color32(255, 255, 255, 0);
            }
            bodyTexture = Pixelated(pixels, sprite.name + " (body)");
            glowTexture = Pixelated(mask, sprite.name + " (glow)");
        }

        Texture2D Pixelated(Color32[] pixels, string textureName)
        {
            var texture = new Texture2D(sprite.width, sprite.height, TextureFormat.RGBA32, false)
            {
                name = textureName, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        void BuildCanvas()
        {
            var canvasObject = new GameObject("Shotgun Sprite (screen)");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -50;   // under any HUD

            gun = new GameObject("Gun", typeof(RectTransform)).GetComponent<RectTransform>();
            gun.SetParent(canvasObject.transform, false);
            gun.anchorMin = gun.anchorMax = new Vector2(1f, 0f);
            gun.pivot = Pivot;
            body = gun.gameObject.AddComponent<RawImage>();
            body.texture = bodyTexture;
            body.raycastTarget = false;

            if (!glowTexture) return;
            var glowRect = new GameObject("Emitter Glow", typeof(RectTransform)).GetComponent<RectTransform>();
            glowRect.SetParent(gun, false);
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = glowRect.offsetMax = Vector2.zero;
            glow = glowRect.gameObject.AddComponent<RawImage>();
            glow.texture = glowTexture;
            glow.raycastTarget = false;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            float w = Screen.width, h = Screen.height, px = h / 720f;

            bool moving = motor && motor.IsGrounded && motor.Speed > 0.5f;
            bobAmount = Mathf.MoveTowards(bobAmount, moving ? 1f : 0f, dt * 6f);
            if (moving) bobTime += dt * 11f;
            bool lowered = shotgun.IsOverheated || shotgun.IsVenting;
            down = Mathf.MoveTowards(down, lowered ? 1f : 0f, dt * 6f);
            float aim = Mathf.SmoothStep(0f, 1f, shotgun.AimBlend);
            float kickPixels = kick * shotgun.Kick * shotgun.Kick * px;
            float lever = shotgun.LeverSwing;

            float gunHeight = height * h, gunWidth = gunHeight * sprite.width / sprite.height;
            gun.sizeDelta = new Vector2(gunWidth, gunHeight);
            var rest = new Vector2(-(rightMargin * w + (1f - Pivot.x) * gunWidth), bottom * h);
            var offset = new Vector2(
                -aimShift * w * aim + Mathf.Cos(bobTime * 0.5f) * bob * px * bobAmount,
                -(lever * 0.03f + down * 0.08f) * h - Mathf.Abs(Mathf.Sin(bobTime * 0.5f)) * bob * px * bobAmount - kickPixels);
            gun.anchoredPosition = rest + offset;
            // The prototype tips it back (counterclockwise) for each of these: a positive angle in UI space.
            gun.localRotation = Quaternion.Euler(0f, 0f, kickPixels / px * 0.12f + lever * 10f + down * 14f);

            Color tint = shotgun.IsOverheated ? new Color(1f, 0.72f, 0.6f) : Color.Lerp(Color.white, new Color(1f, 0.93f, 0.86f), shotgun.Heat01);
            body.color = tint;
            if (glow)
            {
                Color g = shotgun.GlowColor;
                float peak = Mathf.Max(g.r, Mathf.Max(g.g, g.b));
                Color hue = peak > 0f ? Color.Lerp(new Color(g.r / peak, g.g / peak, g.b / peak), Color.white, 0.2f) : Color.black;
                hue.a = Mathf.Clamp01(peak / FullGlow);
                glow.color = hue;
            }

            if (muzzle && view)
            {
                Vector2 inImage = new Vector2(muzzlePixel.x / sprite.width - Pivot.x, 1f - muzzlePixel.y / sprite.height - Pivot.y);
                Vector3 onScreen = gun.TransformPoint(new Vector3(inImage.x * gunWidth, inImage.y * gunHeight, 0f));
                muzzle.position = view.ScreenToWorldPoint(new Vector3(onScreen.x, onScreen.y, muzzleDepth));
            }
        }
    }
}
