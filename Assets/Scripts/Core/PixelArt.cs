using System.Collections.Generic;
using UnityEngine;

namespace HyperInfinite
{
    /// <summary>
    /// Small pixel-art images drawn in code, for the HUD and effects: each is a few rows of text, where
    /// '#' is a white pixel, '+' a half-bright one and anything else is clear. They're white so the UI
    /// can tint them with any color from the Palette. Every image is made once, the first time it's
    /// asked for, and kept; point filtering keeps the pixels square at any size.
    /// </summary>
    public static class PixelArt
    {
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        /// <summary>How big one pixel of this art is on the HUD's 1080-pixel-tall canvas.</summary>
        public const float Pixel = 3f;

        /// <summary>A one-pixel outline with a clear middle: stretched (9-sliced) around panels, bars and buttons.</summary>
        public static Sprite Outline => Get("Outline", 1, OutlineRows);
        /// <summary>The burst at the muzzle when the shotgun fires.</summary>
        public static Sprite MuzzleFlash => Get("Muzzle Flash", 0, MuzzleRows);
        /// <summary>An arrowhead pointing up: turned to point at whatever just hurt you.</summary>
        public static Sprite Arrow => Get("Arrow", 0, ArrowRows);
        /// <summary>A corner of the hurt and shield borders; flipped for the other three corners.</summary>
        public static Sprite Corner => Get("Corner", 0, CornerRows);
        /// <summary>The star where a melee bash lands.</summary>
        public static Sprite Impact => Get("Impact", 0, ImpactRows);
        /// <summary>The explosion out of the emitters when a heavy bash dumps the gun's heat.</summary>
        public static Sprite Blast => Get("Blast", 0, BlastRows);
        /// <summary>A broken ring, grown outward in steps for a heavy bash's shockwave.</summary>
        public static Sprite BlastRing => Get("Blast Ring", 0, BlastRingRows);
        /// <summary>The ">" that marks the selected menu button.</summary>
        public static Sprite Chevron => Get("Chevron", 0, ChevronRows);
        /// <summary>The mark beside a boss's name: a spider with its legs arched like the Hive Mother's, and holes for its eyes.</summary>
        public static Sprite BossMark => Get("Boss Mark", 0, BossMarkRows);
        /// <summary>The two eyes that fill the holes in BossMark, so they can be tinted their own color.</summary>
        public static Sprite BossEyes => Get("Boss Eyes", 0, BossEyesRows);

        static readonly string[] OutlineRows =
        {
            "###",
            "#.#",
            "###",
        };

        static readonly string[] MuzzleRows =
        {
            ".....#.....",
            ".#...#...#.",
            "..#.+#+.#..",
            "...+###+...",
            "..+#####+..",
            "###########",
            "..+#####+..",
            "...+###+...",
            "..#.+#+.#..",
            ".#...#...#.",
            ".....#.....",
        };

        static readonly string[] ImpactRows =
        {
            "....#....",
            ".#..#..#.",
            "..#.#.#..",
            "...###...",
            "####+####",
            "...###...",
            "..#.#.#..",
            ".#..#..#.",
            "....#....",
        };

        static readonly string[] BlastRows =
        {
            ".......#.......",
            "..#....#....#..",
            "...#..###..#...",
            "....+#####+....",
            ".#.#########.#.",
            "..###########..",
            "..###########..",
            "###############",
            "..###########..",
            "..###########..",
            ".#.#########.#.",
            "....+#####+....",
            "...#..###..#...",
            "..#....#....#..",
            ".......#.......",
        };

        static readonly string[] BlastRingRows =
        {
            ".....##+##.....",
            "...##.....#+...",
            "..+.........#..",
            ".#...........#.",
            ".#.............",
            "#.............#",
            "#.............+",
            "+.............#",
            "#.............#",
            "..............#",
            ".#...........#.",
            ".#...........+.",
            "..#.........#..",
            "...+#.....##...",
            ".....#+###.....",
        };

        static readonly string[] ArrowRows =
        {
            "....#....",
            "...###...",
            "..#####..",
            ".###.###.",
            "###...###",
            "##.....##",
        };

        // The top-left corner: solid along the two edges, breaking up into scattered pixels toward the middle.
        static readonly string[] CornerRows =
        {
            "################",
            "###############.",
            "#############.#.",
            "###########.#...",
            "#########.#.....",
            "########.#..#...",
            "#######.#.......",
            "######.#..#.....",
            "#####.#.........",
            "####.#..#.......",
            "####............",
            "###.#...........",
            "###.............",
            "##.#............",
            "##..............",
            "#...............",
        };

        static readonly string[] ChevronRows =
        {
            "##...",
            ".##..",
            "..##.",
            "...##",
            "..##.",
            ".##..",
            "##...",
        };

        static readonly string[] BossMarkRows =
        {
            "..##.......##..",
            ".#..#.....#..#.",
            "#..#..###..#..#",
            "#.#..#####..#.#",
            "#.#.#######.#.#",
            "#.#.#.###.#.#.#",
            "..#.#######.#..",
            "..#..#.#.#..#..",
            "..#.........#..",
        };

        static readonly string[] BossEyesRows =
        {
            "...............",
            "...............",
            "...............",
            "...............",
            "...............",
            ".....#...#.....",
            "...............",
            "...............",
            "...............",
        };

        /// <summary>
        /// Builds a sprite from rows of text, top row first. <paramref name="border"/> is how many pixels
        /// at each edge stay unstretched when the sprite is 9-sliced; 0 for a plain image.
        /// </summary>
        static Sprite Get(string name, int border, string[] rows)
        {
            if (sprites.TryGetValue(name, out Sprite sprite) && sprite) return sprite;
            int width = rows[0].Length, height = rows.Length;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                string row = rows[height - 1 - y];   // textures start at the bottom row
                for (int x = 0; x < width; x++)
                {
                    char c = x < row.Length ? row[x] : '.';
                    byte alpha = c == '#' ? (byte)255 : c == '+' ? (byte)140 : (byte)0;
                    pixels[y * width + x] = new Color32(255, 255, 255, alpha);
                }
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Pixel Art: " + name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            // 100 / Pixel sprite pixels per unit makes each one Pixel canvas units wide, borders included.
            sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f / Pixel, 0,
                                   SpriteMeshType.FullRect, Vector4.one * border);
            sprite.name = name;
            sprites[name] = sprite;
            return sprite;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            sprites.Clear();
        }
    }
}
