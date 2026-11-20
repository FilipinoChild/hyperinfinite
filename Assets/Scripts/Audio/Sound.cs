namespace HyperInfinite
{
    /// <summary>Every sound effect in the game. Sfx plays them; SoundBank says how.</summary>
    public enum Sound
    {
        None,
        // The shotgun
        ShotgunFire, Lever, DryFire, Redline, Vent, Cooled, HitTick, KillTick,
        // Healing
        VentHeal, HealCut, HeartDrop, HeartCollect,
        // Melee
        Swing, Bash, Sear, Counter, HeavyCharge, HeavyBash, Overload, BlastJump, BrandBurst,
        // Moving, getting hurt, going down
        Dash, Jump, DoubleJump, Land, Hurt, PlayerDown, Respawn, Heartbeat,
        // HYPER
        CoreDrop, CoreHum, CoreFade, HyperStart, HyperExtend, HyperEnd, RamHit,
        // Scuttlers and hatchlings
        ScuttlerWindup, ScuttlerBite, ScuttlerDeath, ScuttlerChitter, PackSpawn, HatchlingPop,
        // The Hive Mother
        HiveWindup, HiveSlam, HiveSummon, HiveEnrage, HiveStagger, HiveDeath,
        // Hazards
        EggSwell, EggErupt, WebCaught, WebTorn,
        // Menus
        UiMove, UiSelect, PauseOpen, PauseClose,
    }

    /// <summary>How one sound plays.</summary>
    public struct SoundSpec
    {
        /// <summary>The clip's name in Resources/Audio/Sfx.</summary>
        public string file;
        /// <summary>Its loudness against the others, 0 to 1.</summary>
        public float volume;
        /// <summary>Each play is pitched up or down by up to this share, so repeats don't sound copied.</summary>
        public float jitter;
        /// <summary>At most this many play at once; the oldest makes way.</summary>
        public int voices;
        /// <summary>Seconds that must pass before it plays again, so a crowd doesn't stack it into noise.</summary>
        public float gap;
        /// <summary>How far away it can be heard, in meters. 0 for sounds that come from you or the menus, heard the same in both ears.</summary>
        public float range;
        /// <summary>When every source is busy, lower numbers win.</summary>
        public int priority;
        /// <summary>Keeps playing while the game is paused: the menus' own sounds.</summary>
        public bool ui;
    }

    /// <summary>
    /// The table of sound settings, in one place so the mix can be balanced from here. Clips live in
    /// Resources/Audio/Sfx under the names below; swap a file for another with the same name to change a sound.
    /// </summary>
    public static class SoundBank
    {
        static SoundSpec[] specs;

        public static SoundSpec Get(Sound sound)
        {
            if (specs == null) Build();
            int i = (int)sound;
            return i >= 0 && i < specs.Length ? specs[i] : default;
        }

        public static int Count => System.Enum.GetValues(typeof(Sound)).Length;

        static void Set(Sound s, string file, float volume, float jitter = 0.04f, int voices = 2, float gap = 0.04f, float range = 0f, int priority = 128, bool ui = false)
        {
            specs[(int)s] = new SoundSpec { file = file, volume = volume, jitter = jitter, voices = voices, gap = gap, range = range, priority = priority, ui = ui };
        }

        static void Build()
        {
            specs = new SoundSpec[Count];
            //   sound                  file                 vol   jitter voices gap    range  priority
            Set(Sound.ShotgunFire,     "gun_fire",          0.85f, 0.04f, 3, 0.05f,  0f,    10);
            Set(Sound.Lever,           "gun_lever",         0.45f, 0.03f, 2, 0.10f,  0f,    90);
            Set(Sound.DryFire,         "gun_dry",           0.55f, 0.05f, 1, 0.12f,  0f,    60);
            Set(Sound.Redline,         "gun_redline",       0.70f, 0f,    1, 0.50f,  0f,    20);
            Set(Sound.Vent,            "gun_vent",          0.65f, 0.03f, 1, 0.20f,  0f,    40);
            Set(Sound.Cooled,          "gun_cooled",        0.40f, 0.02f, 1, 0.30f,  0f,    90);
            Set(Sound.HitTick,         "hit_tick",          0.40f, 0.05f, 2, 0.04f,  0f,    70);
            Set(Sound.KillTick,        "kill_tick",         0.45f, 0.05f, 3, 0.04f,  0f,    60);

            Set(Sound.VentHeal,        "heal_vent",         0.65f, 0f,    1, 0.30f,  0f,    30);
            Set(Sound.HealCut,         "heal_cut",          0.75f, 0f,    1, 0.30f,  0f,    30);
            Set(Sound.HeartDrop,       "heart_drop",        0.60f, 0.10f, 3, 0.05f,  30f,   80);
            Set(Sound.HeartCollect,    "heart_collect",     0.50f, 0f,    4, 0.02f,  0f,    50);

            Set(Sound.Swing,           "melee_swing",       0.45f, 0.08f, 2, 0.05f,  0f,    70);
            Set(Sound.Bash,            "melee_bash",        0.75f, 0.06f, 2, 0.05f,  0f,    30);
            Set(Sound.Sear,            "melee_sear",        0.80f, 0.05f, 2, 0.05f,  0f,    30);
            Set(Sound.Counter,         "melee_counter",     0.85f, 0.03f, 1, 0.05f,  0f,    25);
            Set(Sound.HeavyCharge,     "melee_charge",      0.45f, 0f,    1, 0f,     0f,    40);
            Set(Sound.HeavyBash,       "melee_heavy",       1.00f, 0.02f, 2, 0.10f,  0f,    5);
            Set(Sound.Overload,        "melee_overload",    0.75f, 0.02f, 1, 0.10f,  0f,    15);
            Set(Sound.BlastJump,       "melee_blastjump",   0.80f, 0.03f, 1, 0.10f,  0f,    15);
            Set(Sound.BrandBurst,      "brand_burst",       0.55f, 0.10f, 4, 0.03f,  0f,    50);

            Set(Sound.Dash,            "move_dash",         0.55f, 0.05f, 1, 0.10f,  0f,    40);
            Set(Sound.Jump,            "move_jump",         0.35f, 0.05f, 1, 0.08f,  0f,    90);
            Set(Sound.DoubleJump,      "move_jump2",        0.45f, 0.04f, 1, 0.08f,  0f,    80);
            Set(Sound.Land,            "move_land",         0.45f, 0.08f, 1, 0.10f,  0f,    100);
            Set(Sound.Hurt,            "player_hurt",       0.75f, 0.06f, 2, 0.08f,  0f,    15);
            Set(Sound.PlayerDown,      "player_down",       0.85f, 0f,    1, 1f,     0f,    5);
            Set(Sound.Respawn,         "player_respawn",    0.75f, 0f,    1, 1f,     0f,    10);
            Set(Sound.Heartbeat,       "player_heartbeat",  0.50f, 0f,    1, 0f,     0f,    60);

            Set(Sound.CoreDrop,        "core_drop",         0.90f, 0f,    1, 0.50f,  90f,   10);
            Set(Sound.CoreHum,         "core_hum",          0.55f, 0f,    1, 0f,     28f,   40);
            Set(Sound.CoreFade,        "core_fade",         0.55f, 0f,    1, 0.30f,  40f,   60);
            Set(Sound.HyperStart,      "hyper_start",       0.90f, 0f,    1, 0.50f,  0f,    5);
            Set(Sound.HyperExtend,     "hyper_extend",      0.45f, 0f,    2, 0.05f,  0f,    60);
            Set(Sound.HyperEnd,        "hyper_end",         0.65f, 0f,    1, 0.50f,  0f,    20);
            Set(Sound.RamHit,          "hyper_ram",         0.65f, 0.08f, 3, 0.03f,  30f,   40);

            Set(Sound.ScuttlerWindup,  "scuttler_windup",   0.50f, 0.10f, 3, 0.08f,  22f,   50);
            Set(Sound.ScuttlerBite,    "scuttler_bite",     0.55f, 0.10f, 3, 0.06f,  22f,   45);
            Set(Sound.ScuttlerDeath,   "scuttler_death",    0.65f, 0.12f, 4, 0.03f,  40f,   55);
            Set(Sound.ScuttlerChitter, "scuttler_chitter",  0.35f, 0.15f, 3, 0.25f,  20f,   120);
            Set(Sound.PackSpawn,       "scuttler_pack",     0.55f, 0.05f, 2, 0.30f,  45f,   70);
            Set(Sound.HatchlingPop,    "hatchling_pop",     0.45f, 0.15f, 3, 0.03f,  20f,   110);

            Set(Sound.HiveWindup,      "hive_windup",       0.90f, 0.03f, 1, 0.50f,  70f,   10);
            Set(Sound.HiveSlam,        "hive_slam",         1.00f, 0.03f, 1, 0.30f,  80f,   5);
            Set(Sound.HiveSummon,      "hive_summon",       0.80f, 0.05f, 1, 1f,     70f,   20);
            Set(Sound.HiveEnrage,      "hive_enrage",       1.00f, 0f,    1, 1f,     90f,   5);
            Set(Sound.HiveStagger,     "hive_stagger",      0.85f, 0.05f, 1, 0.30f,  70f,   15);
            Set(Sound.HiveDeath,       "hive_death",        1.00f, 0f,    1, 1f,     120f,  5);

            Set(Sound.EggSwell,        "egg_swell",         0.45f, 0.08f, 2, 0.20f,  25f,   100);
            Set(Sound.EggErupt,        "egg_erupt",         0.65f, 0.08f, 2, 0.10f,  35f,   60);
            Set(Sound.WebCaught,       "web_caught",        0.65f, 0.05f, 1, 0.20f,  0f,    30);
            Set(Sound.WebTorn,         "web_torn",          0.55f, 0.08f, 2, 0.10f,  30f,   70);

            Set(Sound.UiMove,          "ui_move",           0.35f, 0f,    2, 0.03f,  0f,    100, true);
            Set(Sound.UiSelect,        "ui_select",         0.50f, 0f,    2, 0.05f,  0f,    90,  true);
            Set(Sound.PauseOpen,       "ui_pause",          0.55f, 0f,    1, 0.10f,  0f,    90,  true);
            Set(Sound.PauseClose,      "ui_resume",         0.55f, 0f,    1, 0.10f,  0f,    90,  true);
        }
    }
}
