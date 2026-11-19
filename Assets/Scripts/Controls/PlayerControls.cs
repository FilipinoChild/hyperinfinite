using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

#if !ENABLE_INPUT_SYSTEM
#error PlayerControls uses Unity's Input System. Set Edit > Project Settings > Player > Active Input Handling to "Input System Package (New)" or "Both".
#endif

namespace HyperInfinite
{
    /// <summary>
    /// CONTROLS. The one place gameplay reads input from. It uses Unity's Input System
    /// with the HyperInfiniteControls actions asset (Assets/Settings), which holds every binding.
    /// To rebind, double-click that asset and edit it in the Input Actions editor; no code changes.
    /// Every other script asks this component what the player wants to do this frame (move, look,
    /// jump, fire...).
    ///
    /// Menus switch gameplay input off with Block and back on with Unblock. While anything blocks, the
    /// cursor is free; when the last block lifts, the cursor is captured again. Fire only counts once
    /// the button has been seen released, so a button that's already down when play starts or resumes
    /// (the click that started the level, closed a menu or captured the cursor) never fires a blast.
    /// </summary>
    [DefaultExecutionOrder(-100)]   // read input before anything uses it
    [DisallowMultipleComponent]
    public class PlayerControls : MonoBehaviour
    {
        [Tooltip("The actions asset that holds every binding: Assets/Settings/HyperInfiniteControls.")]
        [SerializeField] InputActionAsset actions;

        [Header("Look")]
        [Tooltip("Degrees turned per pixel of mouse travel.")]
        [SerializeField, Range(0.01f, 0.6f)] float mouseSensitivity = 0.12f;
        [Tooltip("Degrees per second with the right stick fully tilted.")]
        [SerializeField, Range(30f, 600f)] float stickLookSpeed = 200f;
        [Tooltip("Look speed is multiplied by this while Aim is held, for finer aiming.")]
        [SerializeField, Range(0.2f, 1f)] float aimLookScale = 0.7f;
        [SerializeField] bool invertY;

        [Header("Melee")]
        [Tooltip("Holding Melee at least this long, in seconds, makes a heavy bash instead of a quick one.")]
        [SerializeField, Range(0.1f, 1f)] float heavyMeleeHoldTime = 0.3f;

        [Header("Cursor")]
        [Tooltip("Hide and lock the cursor while playing. Clicking the game captures it; Pause frees it.")]
        [SerializeField] bool lockCursor = true;

        /// <summary>Where to move: x strafes right (+), y goes forward (+). Never longer than 1.</summary>
        public Vector2 Move { get; private set; }
        /// <summary>Degrees to turn this frame: x turns right (+), y looks up (+). Sensitivity is already applied.</summary>
        public Vector2 Look { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool DashPressed { get; private set; }
        public bool FirePressed { get; private set; }
        public bool FireHeld { get; private set; }
        public bool AimHeld { get; private set; }
        public bool VentPressed { get; private set; }
        /// <summary>True on the frame Melee is let go after a tap: a quick hit.</summary>
        public bool QuickMeleePressed { get; private set; }
        /// <summary>True on the frame Melee is let go after a hold: the heavy bash lands.</summary>
        public bool HeavyMeleeReleased { get; private set; }
        /// <summary>Seconds Melee has been held so far, while it's down; 0 otherwise. A heavy bash charges up with it.</summary>
        public float MeleeHoldTime { get; private set; }
        /// <summary>True while Melee has been held long enough that letting go throws a heavy bash.</summary>
        public bool HeavyMeleeCharging => meleeDown && MeleeHoldTime >= heavyMeleeHoldTime;
        /// <summary>True on the frame Pause was pressed. It still reports while gameplay input is off, so a menu can close itself.</summary>
        public bool PausePressed { get; private set; }
        /// <summary>True when the gamepad was the last device used, for showing the right button prompts.</summary>
        public bool UsingGamepad { get; private set; }

        /// <summary>False while a menu has gameplay input switched off (see Block). Pause still reports.</summary>
        public bool GameplayEnabled => blockers.Count == 0;
        /// <summary>True when gameplay input is getting through this frame.</summary>
        public bool IsLive { get; private set; }
        public bool CursorCaptured => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>Degrees turned per pixel of mouse travel. A settings menu can change it.</summary>
        public float MouseSensitivity
        {
            get => mouseSensitivity;
            set => mouseSensitivity = Mathf.Clamp(value, 0.01f, 0.6f);
        }

        /// <summary>The actions asset in use. A settings menu can rebind keys on it, and the change applies at once.</summary>
        public InputActionAsset Actions => actions;

        InputActionMap player;
        InputAction move, look, jump, dash, fire, aim, vent, melee, pause;
        InputAction[] all;
        readonly List<object> blockers = new List<object>(4);
        bool meleeDown;
        bool fireLatched = true;   // Fire is ignored until it has been seen released

        void Awake()
        {
            if (!actions)
            {
                Debug.LogError("PlayerControls on " + name + " has no Input Actions asset, so the player can't be controlled. " +
                               "Drag Assets/Settings/HyperInfiniteControls into its Actions field. In the test level, " +
                               "Hyper Infinite > Build Test Level does that for you.", this);
                enabled = false;
                return;
            }
            player = actions.FindActionMap("Player", throwIfNotFound: true);
            move = player.FindAction("Move", throwIfNotFound: true);
            look = player.FindAction("Look", throwIfNotFound: true);
            jump = player.FindAction("Jump", throwIfNotFound: true);
            dash = player.FindAction("Dash", throwIfNotFound: true);
            fire = player.FindAction("Fire", throwIfNotFound: true);
            aim = player.FindAction("Aim", throwIfNotFound: true);
            vent = player.FindAction("Vent", throwIfNotFound: true);
            melee = player.FindAction("Melee", throwIfNotFound: true);
            pause = player.FindAction("Pause", throwIfNotFound: true);
            all = new[] { move, look, jump, dash, fire, aim, vent, melee, pause };
        }

        void OnEnable()
        {
            if (player != null) player.Enable();
        }

        void OnDisable()
        {
            if (player != null) player.Disable();
            if (lockCursor && CursorCaptured) SetCursorCaptured(false);
            Clear();
            PausePressed = false;
        }

        void Update()
        {
            PausePressed = pause.WasPressedThisFrame();
            InputControl last = ControlInUse();
            if (last != null) UsingGamepad = last.device is Gamepad;

            // Esc frees the cursor; clicking the game captures it again. That click only captures:
            // input stays off for its frame, and Fire has to be let go before it counts.
            bool justCaptured = false;
            if (lockCursor)
            {
                Mouse mouse = Mouse.current;
                if (CursorCaptured && PausePressed) SetCursorCaptured(false);
                else if (!CursorCaptured && GameplayEnabled && mouse != null
                         && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
                {
                    SetCursorCaptured(true);
                    justCaptured = true;
                }
            }

            // The keyboard and mouse only play while the cursor is captured, so clicks on the editor
            // or a menu don't reach the game. A gamepad doesn't need the cursor.
            IsLive = GameplayEnabled && !justCaptured && (!lockCursor || CursorCaptured || UsingGamepad);
            if (!IsLive)
            {
                Clear();
                return;
            }

            Vector2 m = move.ReadValue<Vector2>();
            Move = m.sqrMagnitude > 1f ? m.normalized : m;
            AimHeld = aim.IsPressed();
            Look = ReadLook();
            JumpPressed = jump.WasPressedThisFrame();
            JumpHeld = jump.IsPressed();
            DashPressed = dash.WasPressedThisFrame();
            bool fireDown = fire.IsPressed();
            if (!fireDown) fireLatched = false;   // let go: the next press is a real one
            FirePressed = !fireLatched && fire.WasPressedThisFrame();
            FireHeld = !fireLatched && fireDown;
            VentPressed = vent.WasPressedThisFrame();
            ReadMelee();
        }

        /// <summary>
        /// Switches gameplay input off for as long as <paramref name="owner"/> keeps its block, and frees the
        /// cursor for it. Menus call this when they open. Blocking twice with the same owner counts once.
        /// </summary>
        public void Block(object owner)
        {
            if (owner == null || blockers.Contains(owner)) return;
            blockers.Add(owner);
            if (lockCursor) SetCursorCaptured(false);
        }

        /// <summary>Lifts <paramref name="owner"/>'s block. When the last one lifts, play resumes with the cursor captured.</summary>
        public void Unblock(object owner)
        {
            if (!blockers.Remove(owner) || blockers.Count > 0) return;
            if (lockCursor && isActiveAndEnabled) SetCursorCaptured(true);
        }

        /// <summary>Locks and hides the cursor for play, or frees it for menus.</summary>
        public void SetCursorCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }

        /// <summary>
        /// The mouse reports how far it moved this frame, in pixels. A stick reports how far it's tilted,
        /// which becomes a turning speed, with a squared curve for fine control near the center. The
        /// stick's dead zone comes from Project Settings > Input System Package.
        /// </summary>
        Vector2 ReadLook()
        {
            Vector2 value = look.ReadValue<Vector2>();
            InputControl source = look.activeControl;
            bool stick = source != null && source.device is Gamepad;
            Vector2 degrees = stick ? value * (value.magnitude * stickLookSpeed * Time.unscaledDeltaTime)
                                    : value * mouseSensitivity;
            if (invertY) degrees.y = -degrees.y;
            if (AimHeld) degrees *= aimLookScale;
            return degrees;
        }

        /// <summary>
        /// The melee: tap Q for a quick bash, or hold it to charge a heavy bash that lands when you let
        /// go. Both are decided on release, so one press never gives both.
        /// </summary>
        void ReadMelee()
        {
            QuickMeleePressed = HeavyMeleeReleased = false;
            if (melee.WasPressedThisFrame())
            {
                meleeDown = true;
                MeleeHoldTime = 0f;
            }
            if (!meleeDown) return;
            MeleeHoldTime += Time.unscaledDeltaTime;
            if (melee.IsPressed()) return;
            // Let go, perhaps in the same frame it went down.
            if (MeleeHoldTime >= heavyMeleeHoldTime) HeavyMeleeReleased = true;
            else QuickMeleePressed = true;
            meleeDown = false;
            MeleeHoldTime = 0f;
        }

        /// <summary>The control behind whichever action is in use right now, or null when nothing is.</summary>
        InputControl ControlInUse()
        {
            foreach (InputAction action in all)
                if (action.IsInProgress() && action.activeControl != null) return action.activeControl;
            return null;
        }

        void Clear()
        {
            Move = Look = Vector2.zero;
            JumpPressed = JumpHeld = DashPressed = FirePressed = FireHeld = AimHeld = false;
            VentPressed = QuickMeleePressed = HeavyMeleeReleased = false;
            MeleeHoldTime = 0f;
            meleeDown = false;
            fireLatched = true;
            IsLive = false;
        }
    }
}
