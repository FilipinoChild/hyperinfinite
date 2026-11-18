using UnityEngine;

namespace HyperInfinite
{
    /// <summary>
    /// The game's colors, taken from its pixel art: the Scuttler and Hive Mother sprites, the Energy Blast
    /// shotgun and the title screen. Effects and the HUD use these, so everything on screen looks like it
    /// belongs to the same game. Change a color here and it changes everywhere.
    /// </summary>
    public static class Palette
    {
        /// <summary>Outlines and the darkest shadows.</summary>
        public static readonly Color Ink = Hex(0x000000);
        /// <summary>The Arachturns' dark blue shell.</summary>
        public static readonly Color Navy = Hex(0x000435);
        public static readonly Color White = Hex(0xFFFFFF);
        /// <summary>Mid gray, from the shotgun's metal.</summary>
        public static readonly Color Steel = Hex(0x757575);
        /// <summary>The Arachturns' bright red markings; danger.</summary>
        public static readonly Color Red = Hex(0xFC0606);
        public static readonly Color DarkRed = Hex(0x9C0000);
        /// <summary>Their yellow eyes; warnings.</summary>
        public static readonly Color Yellow = Hex(0xFFE700);
        /// <summary>Between yellow and red: heat building up.</summary>
        public static readonly Color Amber = Hex(0xFF8A1C);
        /// <summary>The shotgun's energy cells and the menus' selection: the player's color.</summary>
        public static readonly Color Cyan = Hex(0x31C7FF);
        /// <summary>The paler cyan at the heart of an energy bolt.</summary>
        public static readonly Color PaleCyan = Hex(0xB8F1FF);
        public static readonly Color Green = Hex(0x5CE676);
        /// <summary>The Pulsating Eggs' shell, from the prototype.</summary>
        public static readonly Color EggPink = Hex(0xC9637A);

        /// <summary>The dark plate behind HUD panels and menu buttons, from the title screen's buttons.</summary>
        public static readonly Color Panel = new Color(0.055f, 0.031f, 0.067f, 0.82f);
        /// <summary>The empty part of a bar.</summary>
        public static readonly Color Track = Hex(0x29232F);
        /// <summary>Secondary text.</summary>
        public static readonly Color Dim = Hex(0xAAA5A4);

        static Color Hex(int rgb)
        {
            return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        }
    }
}
