using UnityEngine;
using UnityEngine.UI;

namespace HyperInfinite
{
    /// <summary>
    /// The Energy Blast shotgun gun drawn over the bottom right of the view, 2.5D. It bobs while you run,
    /// trails behind as you turn, leans as you strafe, lifts and drops as you jump and land,
    /// kicks down on every blast, dips while the lever cycles, slides toward the middle as you aim,
    /// drops and tilts while venting or redlined, and falls out of sight when you go down. Its emitter cells glow with the gun's charge and heat
    /// (EnergyBlastShotgun.GlowColor), a pixel burst flashes at the muzzle on every blast, and the muzzle
    /// Transform is kept on the sprite's muzzle so the bolts start from it.
    /// While the INFINITE power-up is active, a gold aura pulses behind the gun and the gun is tinted gold.
    ///
    /// It also swings the gun for ShotgunMelee. A bash drives the emitter end into whatever is in front, with
    /// an impact star where it lands: white for a plain hit, amber when it sears, a big yellow one for a
    /// counter. Charging a heavy bash draws the gun back and shakes it a pixel at a time, harder the more heat
    /// is waiting, while the cells strobe and embers drift off the muzzle. A pop and a white flash of the
    /// cells say it's ready. The heavy bash slams the gun forward: the cells flash white, a three-frame
    /// explosion goes off at the emitters with a shockwave ring growing in steps, and the screen flashes amber
    /// for two frames, all bigger the more heat went into it. An overload, from a redlined gun, is bigger still
    /// and flashes white first, for three frames. Under HYPER all of it is cyan and white instead, and the
    /// cells glow white and cyan for as long as HYPER lasts. While a vent heals you they step between green and
    /// pale green, and flash red if a hit cuts the heal short.
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
        [Tooltip("How far the gun trails behind when you turn, in pixels per degree turned in a frame.")]
        [SerializeField, Min(0f)] float sway = 3f;
        [Tooltip("Degrees the gun leans when strafing at full speed.")]
        [SerializeField, Range(0f, 15f)] float strafeLean = 3.5f;

        [Header("INFINITE power-up look")]   // INFINITE
        [Tooltip("Color of the aura and tint while INFINITE is active.")]
        [SerializeField] Color infiniteColor = new Color(1f, 0.75f, 0.15f);
        [Tooltip("How much bigger than the gun the aura is. 1.08 = 8% bigger.")]
        [SerializeField, Range(1f, 1.3f)] float auraSize = 1.08f;
        [Tooltip("How strongly the gun itself is tinted. 1 = fully gold.")]
        [SerializeField, Range(0f, 1f)] float tintStrength = 0.6f;
        [Header("Melee (ShotgunMelee)")]
        [Tooltip("Seconds a bash takes, out and back.")]
        [SerializeField, Min(0.05f)] float bashTime = 0.24f;
        [Tooltip("Seconds a heavy bash takes, out and back.")]
        [SerializeField, Min(0.05f)] float heavyTime = 0.42f;

        // From the prototype: the gun rotates around a point 80% of the way across its bottom edge.
        static readonly Vector2 Pivot = new Vector2(0.8f, 0f);
        const float FullGlow = 2.4f;   // GlowColor's brightest channel when fully charged
        const float FlashTime = 0.08f;  // seconds the muzzle flash shows
        const float ImpactTime = 0.1f, BlastTime = 0.16f, RingTime = 0.24f, ScreenFlashTime = 0.1f;   // the melee's effects, in seconds

        static readonly Color CoolCells = new Color(0.19f, 0.78f, 1f), HotCells = new Color(1f, 0.54f, 0.11f), RedlineCells = new Color(1f, 0.16f, 0.06f);

        EnergyBlastShotgun shotgun;
        ShotgunMelee melee;
        HyperPower hyper;
        PlayerMotor motor;
        PlayerControls controls;
        Health health;
        Camera view;
        Canvas canvas;
        RectTransform gun;
        RawImage body, glow, aura;                          // INFINITE: added aura
        Image flash;
        RectTransform flashRect;
        Texture2D bodyTexture, glowTexture, auraTexture;    // INFINITE: added auraTexture
        float bobTime, bobAmount, down, dropped, lean, lift;
        RawImage body, glow;
        Image flash, impact, blast, ring, screenFlash;
        RectTransform flashRect;
        Texture2D bodyTexture, glowTexture;
        float bobTime, bobAmount, down, dropped, lean, lift, charge, readyAt = -10f, emberDue;
        bool wasCharging;
        Vector2 trailing;

        void Awake()
        {
            shotgun = GetComponent<EnergyBlastShotgun>();
            melee = GetComponent<ShotgunMelee>();
            hyper = GetComponentInParent<HyperPower>();
            motor = GetComponentInParent<PlayerMotor>();
            controls = GetComponentInParent<PlayerControls>();
            health = GetComponentInParent<Health>();
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
            if (auraTexture) Destroy(auraTexture);   // INFINITE
        }

        /// <summary>
        /// Splits the art in two: the gun with its emitter cells darkened, and a white mask of just those cells,
        /// which is tinted with the glow. Also makes a white silhouette of the whole gun for the INFINITE aura.
        /// Needs Read/Write on the texture; without it the gun is drawn as it is.
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
            var silhouette = new Color32[pixels.Length];   // INFINITE: the gun's outline, all white
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                silhouette[i] = new Color32(255, 255, 255, p.a > 128 ? (byte)255 : (byte)0);   // INFINITE
                if (p.a > 128 && p.b > 150 && p.b - p.r > 60)   // the cyan cells
                {
                    mask[i] = new Color32(255, 255, 255, 255);
                    pixels[i] = new Color32(24, 34, 40, 255);   // an unlit cell
                }
                else mask[i] = new Color32(255, 255, 255, 0);
            }
            bodyTexture = Pixelated(pixels, sprite.name + " (body)");
            glowTexture = Pixelated(mask, sprite.name + " (glow)");
            auraTexture = Pixelated(silhouette, sprite.name + " (aura)");   // INFINITE
        }

        Texture2D Pixelated(Color32[] pixels, string textureName)
        {
            var texture = new Texture2D(sprite.width, sprite.height, TextureFormat.RGBA32, false)
            {
                name = textureName,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
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

            // INFINITE: the aura is made first so it's drawn behind the gun. It copies the gun's
            // position and rotation every frame, and is only switched on during INFINITE.
            if (auraTexture)
            {
                var auraRect = new GameObject("INFINITE Aura", typeof(RectTransform)).GetComponent<RectTransform>();
                auraRect.SetParent(canvasObject.transform, false);
                auraRect.anchorMin = auraRect.anchorMax = new Vector2(1f, 0f);
                auraRect.pivot = Pivot;
                aura = auraRect.gameObject.AddComponent<RawImage>();
                aura.texture = auraTexture;
                aura.raycastTarget = false;
                aura.enabled = false;
            }
            // The heavy bash's flash over the whole view: behind the gun, so the gun stays crisp in front of it.
            var cover = new GameObject("Bash Flash", typeof(RectTransform)).GetComponent<RectTransform>();
            cover.SetParent(canvasObject.transform, false);
            cover.anchorMin = Vector2.zero;
            cover.anchorMax = Vector2.one;
            cover.offsetMin = cover.offsetMax = Vector2.zero;
            screenFlash = cover.gameObject.AddComponent<Image>();
            screenFlash.raycastTarget = false;
            screenFlash.enabled = false;

            gun = new GameObject("Gun", typeof(RectTransform)).GetComponent<RectTransform>();
            gun.SetParent(canvasObject.transform, false);
            gun.anchorMin = gun.anchorMax = new Vector2(1f, 0f);
            gun.pivot = Pivot;
            body = gun.gameObject.AddComponent<RawImage>();
            body.texture = bodyTexture;
            body.raycastTarget = false;

            if (glowTexture)
            {
                var glowRect = new GameObject("Emitter Glow", typeof(RectTransform)).GetComponent<RectTransform>();
                glowRect.SetParent(gun, false);
                glowRect.anchorMin = Vector2.zero;
                glowRect.anchorMax = Vector2.one;
                glowRect.offsetMin = glowRect.offsetMax = Vector2.zero;
                glow = glowRect.gameObject.AddComponent<RawImage>();
                glow.texture = glowTexture;
                glow.raycastTarget = false;
            }

            // The muzzle flash is pinned to the muzzle in the image, so it follows the gun wherever it moves.
            flashRect = new GameObject("Muzzle Flash", typeof(RectTransform)).GetComponent<RectTransform>();
            flashRect.SetParent(gun, false);
            flashRect.anchorMin = flashRect.anchorMax = new Vector2(muzzlePixel.x / sprite.width, 1f - muzzlePixel.y / sprite.height);
            flashRect.anchoredPosition = Vector2.zero;
            flash = flashRect.gameObject.AddComponent<Image>();
            flash.sprite = PixelArt.MuzzleFlash;
            flash.raycastTarget = false;
            flash.enabled = false;

            // The melee's effects, pinned to the emitters like the muzzle flash. The ring goes under the explosion.
            ring = AtMuzzle("Shockwave", PixelArt.BlastRing);
            blast = AtMuzzle("Bash Blast", PixelArt.Blast);
            impact = AtMuzzle("Bash Impact", PixelArt.Impact);
        }

        Image AtMuzzle(string objectName, Sprite art)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(gun, false);
            rect.anchorMin = rect.anchorMax = flashRect.anchorMin;
            rect.anchoredPosition = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = art;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime, now = Time.time;
            float w = Screen.width, h = Screen.height, px = h / 720f;

            // The melee: how far through a bash or a heavy bash the gun is (0 at rest, 1 at full reach), and the charge.
            float bash = 0f, heavy = 0f, power = 0f;
            bool charging = false;
            if (melee)
            {
                bash = Swing((now - melee.LastBashTime) / bashTime);
                heavy = Swing((now - melee.LastHeavyTime) / heavyTime);
                power = melee.LastHeavyPower;
                charging = melee.IsCharging || melee.IsLunging;   // held back through the lunge, then slammed forward as it lands
                if (charging && !wasCharging) readyAt = now;   // held long enough: letting go now throws it
                wasCharging = charging;
            }
            charge = Mathf.MoveTowards(charge, charging ? 1f : 0f, dt * 8f);
            float heat = shotgun.Heat01;

            bool moving = motor && motor.IsGrounded && motor.Speed > 0.5f;
            bobAmount = Mathf.MoveTowards(bobAmount, moving ? 1f : 0f, dt * 6f);
            if (moving) bobTime += dt * 11f;
            bool lowered = shotgun.IsRedlined || shotgun.IsVenting;
            down = Mathf.MoveTowards(down, lowered ? 1f : 0f, dt * 6f);
            dropped = Mathf.MoveTowards(dropped, health && health.IsDead ? 1f : 0f, dt * 3f);   // out of sight while you're down
            float aim = Mathf.SmoothStep(0f, 1f, shotgun.AimBlend) * (1f - Mathf.Max(bash, heavy));   // a swing takes it off aim
            float kickPixels = kick * shotgun.Kick * shotgun.Kick * px;
            float lever = shotgun.LeverSwing;

            // The gun has weight: it trails behind a turn, leans into a strafe, and lags a jump or a fall.
            float follow = 1f - Mathf.Exp(-10f * dt);
            Vector2 look = controls ? controls.Look : Vector2.zero;
            trailing = Vector2.Lerp(trailing, new Vector2(Mathf.Clamp(-look.x * sway, -28f, 28f), Mathf.Clamp(-look.y * sway, -18f, 18f)), follow);
            float sideways = 0f, rising = 0f;
            if (motor)
            {
                Vector3 velocity = motor.Velocity;
                sideways = motor.RunSpeed > 0f ? Mathf.Clamp(Vector3.Dot(velocity, transform.right) / motor.RunSpeed, -1f, 1f) : 0f;
                rising = motor.IsGrounded ? 0f : Mathf.Clamp(-velocity.y * 2.2f, -26f, 26f);
            }
            lean = Mathf.Lerp(lean, sideways * strafeLean, follow);
            lift = Mathf.Lerp(lift, rising, follow);

            float gunHeight = height * h, gunWidth = gunHeight * sprite.width / sprite.height;
            gun.sizeDelta = new Vector2(gunWidth, gunHeight);
            var rest = new Vector2(-(rightMargin * w + (1f - Pivot.x) * gunWidth), bottom * h);
            var offset = new Vector2(
                -aimShift * w * aim + (Mathf.Cos(bobTime * 0.5f) * bob * bobAmount + trailing.x) * px,
                -(lever * 0.03f + down * 0.08f + dropped * dropped * 0.6f) * h
                - (Mathf.Abs(Mathf.Sin(bobTime * 0.5f)) * bob * bobAmount - trailing.y - lift) * px - kickPixels);
            // A bash drives the emitter end in toward the crosshair; a heavy bash harder, further and bigger. Charging
            // draws the gun back and shakes it, harder the more heat is waiting to go, a whole pixel at a time.
            var swing = new Vector2((-0.17f * bash - (0.22f + 0.05f * power) * heavy + 0.04f * charge) * w,
                                    (0.08f * bash + (0.13f + 0.04f * power) * heavy - 0.07f * charge) * h);
            float shake = charge * (1.5f + 5f * heat);
            Vector2 tremble = shake > 0f ? new Vector2(Mathf.Round(Random.Range(-shake, shake)), Mathf.Round(Random.Range(-shake, shake))) * px : Vector2.zero;
            gun.anchoredPosition = rest + offset + swing + tremble;
            // The prototype tips it back (counterclockwise) for each of these: a positive angle in UI space.
            gun.localRotation = Quaternion.Euler(0f, 0f, kickPixels / px * 0.12f + lever * 10f + down * 14f - lean
                                                         - 10f * bash - (20f + 8f * power) * heavy + 12f * charge);
            float grow = 1f + 0.1f * bash + (0.18f + 0.1f * power) * heavy + (now - readyAt < 0.08f ? 0.04f : 0f);   // pops when the charge is ready
            gun.localScale = new Vector3(grow, grow, 1f);

            // INFINITE: one pulse value (0 to 1) shared by the aura, the tint, and the flash
            bool supercharged = shotgun.Supercharged;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 12f);

            Color tint = shotgun.IsOverheated ? new Color(1f, 0.72f, 0.6f) : Color.Lerp(Color.white, new Color(1f, 0.93f, 0.86f), shotgun.Heat01);
            if (supercharged) tint = Color.Lerp(Color.white, infiniteColor, tintStrength * (0.7f + 0.3f * pulse));   // INFINITE
            Color tint = shotgun.IsRedlined ? new Color(1f, 0.72f, 0.6f) : Color.Lerp(Color.white, new Color(1f, 0.93f, 0.86f), shotgun.Heat01);
            body.color = tint;
            if (glow)
            {
                Color g = shotgun.GlowColor;
                float peak = Mathf.Max(g.r, Mathf.Max(g.g, g.b));
                Color hue = peak > 0f ? Color.Lerp(new Color(g.r / peak, g.g / peak, g.b / peak), Color.white, 0.2f) : Color.black;
                hue.a = Mathf.Clamp01(peak / FullGlow);
                if (melee) hue = MeleeGlow(hue, now, heat);
                hue = HealGlow(hue, now);
                glow.color = hue;
            }
            if (melee) MeleeEffects(now, gunHeight, power, heat);

            // INFINITE: the aura follows the gun, slightly bigger, pulsing in size and brightness
            if (aura)
            {
                aura.enabled = supercharged && dropped < 1f;
                if (aura.enabled)
                {
                    RectTransform a = aura.rectTransform;
                    a.sizeDelta = gun.sizeDelta * (auraSize + 0.03f * pulse);
                    a.anchoredPosition = gun.anchoredPosition;
                    a.localRotation = gun.localRotation;
                    Color c = infiniteColor;
                    c.a = 0.45f + 0.35f * pulse;
                    aura.color = c;
                }
            }

            // Two frames of flash: a big white burst, then a smaller cyan one (gold during INFINITE).
            ShotReport shot = shotgun.LastShot;
            float sinceShot = Time.time - shot.Time;
            bool flashing = shot.Bolts > 0 && sinceShot < FlashTime;
            flash.enabled = flashing;
            if (flashing)
            {
                bool first = sinceShot < FlashTime * 0.5f;
                flashRect.sizeDelta = Vector2.one * (gunHeight * (first ? 0.5f : 0.32f));
                flash.color = first ? Palette.White : (supercharged ? Palette.Yellow : Palette.Cyan);   // INFINITE
            }

            if (muzzle && view)
            {
                Vector2 inImage = new Vector2(muzzlePixel.x / sprite.width - Pivot.x, 1f - muzzlePixel.y / sprite.height - Pivot.y);
                Vector3 onScreen = gun.TransformPoint(new Vector3(inImage.x * gunWidth, inImage.y * gunHeight, 0f));
                muzzle.position = view.ScreenToWorldPoint(new Vector3(onScreen.x, onScreen.y, muzzleDepth));
            }
        }
        /// <summary>
        /// The cells for the melee: white as a heavy bash fires or the charge comes ready, strobing while charging,
        /// amber after a sear, and stepping between white and cyan all through HYPER.
        /// </summary>
        Color MeleeGlow(Color hue, float now, float heat)
        {
            float sinceHeavy = now - melee.LastHeavyTime, sinceBash = now - melee.LastBashTime;
            if (sinceHeavy < 0.1f || now - readyAt < 0.08f) return Color.white;
            if (charge > 0f)
            {
                bool boosted = hyper && hyper.IsActive;
                Color hot = boosted ? CoolCells : shotgun.IsRedlined ? RedlineCells : Color.Lerp(CoolCells, HotCells, heat);
                bool lit = Mathf.Repeat(now * (6f + 10f * heat), 1f) < 0.5f;   // strobes faster with more heat behind it
                Color strobe = lit ? Color.white : hot;
                strobe.a = 1f;
                return Color.Lerp(hue, strobe, charge);
            }
            if (sinceBash < 0.1f && melee.LastBash != ShotgunMelee.BashKind.Plain && melee.LastBashLanded) return new Color(1f, 0.8f, 0.35f, 1f);
            if (hyper && hyper.IsActive)
            {
                bool lit = Mathf.Repeat(now * 3f, 1f) < 0.5f;   // a steady step between white and cyan
                return lit ? Color.white : new Color(CoolCells.r, CoolCells.g, CoolCells.b, 1f);
            }
            return hue;
        }

        /// <summary>The cells step between green and pale green while a vent heals you, and flash red when a hit cuts it short.</summary>
        Color HealGlow(Color hue, float now)
        {
            if (now - shotgun.LastHealCutTime < 0.15f) return Palette.Red;
            if (!shotgun.IsVentHealing) return hue;
            return Mathf.Repeat(now * 6f, 1f) < 0.5f ? Palette.Green : Palette.PaleGreen;
        }

        /// <summary>The impact star, the heavy bash's explosion, shockwave and screen flash, and embers while charging. Two to four frames each.</summary>
        void MeleeEffects(float now, float gunHeight, float power, float heat)
        {
            // A bash that lands: a big white star, then a smaller one in the bash's color.
            float sinceBash = now - melee.LastBashTime;
            bool showImpact = sinceBash < ImpactTime && melee.LastBashLanded;
            impact.enabled = showImpact;
            if (showImpact)
            {
                bool first = sinceBash < ImpactTime * 0.5f;
                ShotgunMelee.BashKind kind = melee.LastBash;
                float size = kind == ShotgunMelee.BashKind.Counter ? 0.5f : kind == ShotgunMelee.BashKind.Seared ? 0.42f : 0.32f;
                impact.rectTransform.sizeDelta = Vector2.one * gunHeight * size * (first ? 1f : 0.7f);
                impact.color = kind == ShotgunMelee.BashKind.Counter ? (first ? Palette.White : Palette.Yellow)
                             : kind == ShotgunMelee.BashKind.Seared ? (first ? Palette.Yellow : Palette.Amber)
                             : (first ? Palette.White : Palette.PaleCyan);
            }

            // A heavy bash: three frames of explosion, white, yellow, then red or amber, each bigger the hotter it was.
            float sinceHeavy = now - melee.LastHeavyTime;
            bool overload = melee.LastHeavyOverload;
            float scale = gunHeight * (0.45f + 0.5f * power) * (overload ? 1.2f : 1f);
            bool showBlast = sinceHeavy < BlastTime;
            blast.enabled = showBlast;
            if (showBlast)
            {
                int frame = Mathf.Min(2, (int)(sinceHeavy / (BlastTime / 3f)));
                blast.rectTransform.sizeDelta = Vector2.one * scale * (frame == 0 ? 1f : frame == 1 ? 1.25f : 1.1f);
                blast.color = frame == 0 ? Palette.White
                            : melee.LastHeavyHyper ? (frame == 1 ? Palette.PaleCyan : Palette.Cyan)
                            : frame == 1 ? Palette.Yellow : power > 0.5f ? Palette.Red : Palette.Amber;
            }

            // Its shockwave: a broken ring growing in four steps and cooling from yellow to red.
            bool showRing = sinceHeavy < RingTime;
            ring.enabled = showRing;
            if (showRing)
            {
                int step = Mathf.Min(3, (int)(sinceHeavy / (RingTime / 4f)));
                ring.rectTransform.sizeDelta = Vector2.one * scale * (0.7f + 0.45f * step);
                ring.color = melee.LastHeavyHyper ? (step == 0 ? Palette.White : step == 3 ? Palette.Cyan : Palette.PaleCyan)
                                                  : step == 0 ? Palette.Yellow : step == 3 ? Palette.Red : Palette.Amber;
            }

            // The whole view flashes for two frames: amber, then red. An overload flashes white first, for three.
            // A counter gets one faint yellow frame.
            bool counterFlash = melee.LastBash == ShotgunMelee.BashKind.Counter && melee.LastBashLanded && sinceBash < ScreenFlashTime * 0.5f;
            int frames = overload ? 3 : 2;
            bool heavyFlash = sinceHeavy < ScreenFlashTime * 0.5f * frames;
            screenFlash.enabled = heavyFlash || counterFlash;
            if (heavyFlash)
            {
                int frame = (int)(sinceHeavy / (ScreenFlashTime * 0.5f)) + (overload ? 0 : 1);   // 0 white, 1 amber, 2 red
                Color c = frame == 0 ? Palette.White
                        : melee.LastHeavyHyper ? (frame == 1 ? Palette.PaleCyan : Palette.Cyan)
                        : frame == 1 ? Palette.Amber : Palette.Red;
                c.a = frame == 0 ? 0.35f : frame == 1 ? 0.14f + 0.18f * power : 0.08f + 0.1f * power;
                screenFlash.color = c;
            }
            else if (counterFlash)
            {
                Color c = Palette.Yellow;
                c.a = 0.08f;
                screenFlash.color = c;
            }

            // Charging: embers drift up off the emitters, thicker the more heat is waiting.
            if (charge >= 1f && heat > 0.05f && muzzle && now >= emberDue)
            {
                emberDue = now + Mathf.Lerp(0.09f, 0.03f, heat);
                Color ember = shotgun.IsRedlined || Random.value < heat * 0.5f ? Palette.Red : Random.value < 0.5f ? Palette.Amber : Palette.Yellow;
                Vector3 drift = view ? view.transform.up * Random.Range(0.18f, 0.32f) + view.transform.right * Random.Range(-0.06f, 0.06f) : Vector3.up * 0.25f;
                PixelFx.Mote(muzzle.position + Random.insideUnitSphere * 0.02f, drift, ember, Random.Range(0.007f, 0.011f), Random.Range(0.3f, 0.5f));
            }
        }

        /// <summary>0 at rest; out to 1 over the first quarter, then back to 0 by the end. 0 outside 0..1.</summary>
        static float Swing(float t)
        {
            if (t <= 0f || t >= 1f) return 0f;
            if (t < 0.25f) return Mathf.Sin(t / 0.25f * Mathf.PI * 0.5f);
            float back = (t - 0.25f) / 0.75f;
            return 1f - back * back;
        }
    }
}