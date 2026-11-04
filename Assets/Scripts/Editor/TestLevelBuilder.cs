using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace HyperInfinite
{
    /// <summary>
    /// Hyper Infinite > Builds Test Level. Makes a grey-box scene for testing controls, plus
    /// the prefabs and materials it uses, as ordinary assets in the project's own folders: Scenes,
    /// Prefabs, Materials/Greybox and Textures/Greybox. Open and edit them like anything else; run the
    /// command again to rebuild them from scratch.
    /// </summary>
    public static class TestLevelBuilder
    {
        const string ScenePath = "Assets/Scenes/TestLevel.unity";
        const string PrefabFolder = "Assets/Prefabs";              // in Testing/ and Hazards/ subfolders
        const string MaterialFolder = "Assets/Materials/Greybox";
        const string TextureFolder = "Assets/Textures/Greybox";
        const string ControlsAssetPath = "Assets/Settings/HyperInfiniteControls.inputactions";

        // Layout, in meters. The player spawns facing north (+Z).
        static readonly Vector3 SpawnPosition = new Vector3(0f, 0f, -25f);
        const float FloorSize = 90f, FloorCenterZ = 10f, WallHeight = 4f;

        [MenuItem("Hyper Infinite/Build Test Level", priority = 1)]
        public static void Build()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rebuild Test Level?",
                    "This rebuilds Scenes/TestLevel.unity and the prefabs and materials it made in Prefabs and Materials/Greybox. " +
                    "Changes you made to them will be lost.", "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Folder("Assets/Scenes");
            Folder(MaterialFolder);
            Folder(PrefabFolder);
            Folder(TextureFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var stockCamera = Object.FindAnyObjectByType<Camera>();
            if (stockCamera) Object.DestroyImmediate(stockCamera.gameObject);   // the Player brings its own
            SetUpSun();

            var kit = new Kit();
            var playerPrefab = BuildPlayerPrefab(kit);
            var dummyPrefab = BuildDummyPrefab(kit);

            var level = new GameObject("Level").transform;
            BuildShell(kit, level);
            BuildMovementYard(kit, level);
            BuildFiringRange(kit, level, dummyPrefab);

            var spawn = new GameObject("Spawn Point").transform;
            spawn.position = SpawnPosition;
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.SetPositionAndRotation(SpawnPosition, Quaternion.identity);

            new GameObject("Debug HUD (temporary)").AddComponent<TempDebugHud>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            Debug.Log("Hyper Infinite: built " + ScenePath + ". Press Play, then click the Game view to capture the mouse.");
        }

        // ---- The player ------------------------------------------------------------------

        /// <summary>The Input Actions asset with every binding. It's part of the Controls work, so it's written by hand, not built here.</summary>
        static InputActionAsset ControlsAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsAssetPath);
            if (!asset) Debug.LogError("Hyper Infinite: " + ControlsAssetPath + " is missing, so the Player has no controls. Add it, then build again.");
            return asset;
        }

        static GameObject BuildPlayerPrefab(Kit kit)
        {
            var root = new GameObject("Player") { tag = "Player" };
            var body = root.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.4f;
            body.center = new Vector3(0f, 0.9f, 0f);
            body.stepOffset = 0.35f;
            body.slopeLimit = 50f;
            body.skinWidth = 0.05f;
            body.minMoveDistance = 0f;
            var controls = root.AddComponent<PlayerControls>();
            Wire(controls, "actions", ControlsAsset());
            root.AddComponent<PlayerMotor>();

            // Casts the player's shadow without getting in the first-person view.
            var shadow = Primitive(PrimitiveType.Capsule, "Shadow Body", root.transform, new Vector3(0f, 0.9f, 0f),
                                   new Vector3(0.8f, 0.9f, 0.8f), kit.Block, collider: false);
            shadow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;

            var eyes = new GameObject("Eyes") { tag = "MainCamera" };
            eyes.transform.SetParent(root.transform, false);
            eyes.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var view = eyes.AddComponent<Camera>();
            view.fieldOfView = 75f;
            view.nearClipPlane = 0.03f;
            eyes.AddComponent<AudioListener>();
            eyes.AddComponent<TempFirstPersonCamera>();
            BuildShotgun(kit, eyes.transform);

            return SavePrefab(root, "Testing", "Player");
        }

        /// <summary>
        /// The 1887 viewmodel, built at roughly real size and then shrunk to 40% close to the eye.
        /// It looks the same as a full-size gun further away, and pokes into walls far less.
        /// </summary>
        static LeverShotgun BuildShotgun(Kit kit, Transform eyes)
        {
            var gun = new GameObject("Shotgun (1887)");
            gun.transform.SetParent(eyes, false);
            gun.transform.localPosition = new Vector3(0.13f, -0.12f, 0.2f);
            gun.transform.localScale = Vector3.one * 0.4f;
            Transform model = gun.transform;
            var alongZ = Quaternion.Euler(90f, 0f, 0f);   // cylinders stand along Y; lay them along the barrel

            Part(PrimitiveType.Cube, "Receiver", model, new Vector3(0f, 0f, 0f), new Vector3(0.07f, 0.11f, 0.28f), kit.GunMetal);
            Part(PrimitiveType.Cylinder, "Barrel", model, new Vector3(0f, 0.03f, 0.43f), new Vector3(0.045f, 0.32f, 0.045f), kit.GunMetal, alongZ);
            Part(PrimitiveType.Cylinder, "Magazine Tube", model, new Vector3(0f, -0.02f, 0.36f), new Vector3(0.04f, 0.25f, 0.04f), kit.GunMetal, alongZ);
            Part(PrimitiveType.Cube, "Forend", model, new Vector3(0f, -0.03f, 0.3f), new Vector3(0.062f, 0.055f, 0.2f), kit.GunWood);
            Part(PrimitiveType.Cube, "Stock", model, new Vector3(0f, -0.05f, -0.27f), new Vector3(0.06f, 0.12f, 0.3f), kit.GunWood, Quaternion.Euler(8f, 0f, 0f));

            // Three vents along the top of the barrel glow as heat builds.
            var vents = new Renderer[3];
            for (int i = 0; i < vents.Length; i++)
                vents[i] = Part(PrimitiveType.Cube, "Heat Vent " + (i + 1), model, new Vector3(0f, 0.058f, 0.2f + 0.1f * i),
                                new Vector3(0.05f, 0.012f, 0.045f), kit.HeatVent).GetComponent<Renderer>();

            var lever = new GameObject("Lever").transform;   // pivots at the front of the loop
            lever.SetParent(model, false);
            lever.localPosition = new Vector3(0f, -0.055f, 0.05f);
            Part(PrimitiveType.Cube, "Lever Loop", lever, new Vector3(0f, -0.045f, -0.06f), new Vector3(0.018f, 0.075f, 0.13f), kit.GunMetal);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(model, false);
            muzzle.localPosition = new Vector3(0f, 0.03f, 0.76f);
            var flashObject = new GameObject("Muzzle Flash");
            flashObject.transform.SetParent(muzzle, false);
            var flash = flashObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.range = 8f;
            flash.color = new Color(1f, 0.72f, 0.38f);
            flash.intensity = 0f;
            flash.shadows = LightShadows.None;

            var shotgun = gun.AddComponent<LeverShotgun>();
            Wire(shotgun, "muzzle", muzzle);
            Wire(shotgun, "lever", lever);
            Wire(shotgun, "muzzleFlash", flash);
            WireArray(shotgun, "heatVents", vents);
            return shotgun;
        }

        // ---- Targets ---------------------------------------------------------------------

        static GameObject BuildDummyPrefab(Kit kit)
        {
            var root = new GameObject("Target Dummy");
            var hitbox = root.AddComponent<CapsuleCollider>();
            hitbox.center = new Vector3(0f, 1f, 0f);
            hitbox.height = 2f;
            hitbox.radius = 0.35f;
            Primitive(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0f, 0.04f, 0f), new Vector3(0.9f, 0.04f, 0.9f), kit.Block, collider: false);

            var wobble = new GameObject("Wobble").transform;   // pivots at the feet
            wobble.SetParent(root.transform, false);
            Primitive(PrimitiveType.Cylinder, "Post", wobble, new Vector3(0f, 0.35f, 0f), new Vector3(0.12f, 0.3f, 0.12f), kit.Block, collider: false);
            Primitive(PrimitiveType.Capsule, "Body", wobble, new Vector3(0f, 1.15f, 0f), new Vector3(0.62f, 0.5f, 0.42f), kit.Dummy, collider: false);
            Primitive(PrimitiveType.Sphere, "Head", wobble, new Vector3(0f, 1.82f, 0f), new Vector3(0.36f, 0.36f, 0.36f), kit.Dummy, collider: false);
            var bullseye = Primitive(PrimitiveType.Cylinder, "Bullseye", wobble, new Vector3(0f, 1.2f, -0.21f),
                                     new Vector3(0.3f, 0.012f, 0.3f), kit.DummyTarget, collider: false);
            bullseye.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // a disc facing the player

            var dummy = root.AddComponent<TargetDummy>();
            Wire(dummy, "wobble", wobble);
            return SavePrefab(root, "Testing", "Target Dummy");
        }

        // ---- The level -------------------------------------------------------------------

        static void BuildShell(Kit kit, Transform level)
        {
            var floor = Primitive(PrimitiveType.Cube, "Floor", level, new Vector3(0f, -0.5f, FloorCenterZ),
                                  new Vector3(FloorSize, 1f, FloorSize), kit.Floor);
            floor.isStatic = true;

            float half = FloorSize / 2f, z = FloorCenterZ;
            Wall(kit, level, "Wall North", new Vector3(0f, WallHeight / 2f, z + half + 0.5f), new Vector3(FloorSize + 2f, WallHeight, 1f));
            Wall(kit, level, "Wall South", new Vector3(0f, WallHeight / 2f, z - half - 0.5f), new Vector3(FloorSize + 2f, WallHeight, 1f));
            Wall(kit, level, "Wall East", new Vector3(half + 0.5f, WallHeight / 2f, z), new Vector3(1f, WallHeight, FloorSize));
            Wall(kit, level, "Wall West", new Vector3(-half - 0.5f, WallHeight / 2f, z), new Vector3(1f, WallHeight, FloorSize));

            Primitive(PrimitiveType.Cylinder, "Spawn Pad", level, SpawnPosition + new Vector3(0f, 0.01f, 0f),
                      new Vector3(3f, 0.01f, 3f), kit.Spawn, collider: false);

            // Floor tints marking where each of the test areas are.
            Zone(level, "Zone: Movement Yard", new Vector3(-28.5f, 0f, 2f), new Vector2(31f, 66f), kit.ZoneMovement);
            Zone(level, "Zone: Firing Range", new Vector3(0f, 0f, 16f), new Vector2(18f, 62f), kit.ZoneRange);
            Zone(level, "Zone: Nest Garden", new Vector3(28.5f, 0f, 2f), new Vector2(31f, 66f), kit.ZoneNests);

            // A little cover around the spawn to walk around and hide behind.
            Block(kit, level, "Cover", new Vector3(-5f, 0.6f, -19f), new Vector3(3f, 1.2f, 1f));
            Block(kit, level, "Cover", new Vector3(5f, 0.6f, -19f), new Vector3(3f, 1.2f, 1f));
            Block(kit, level, "Pillar", new Vector3(-9f, 1.5f, -28f), new Vector3(1.5f, 3f, 1.5f));
            Block(kit, level, "Pillar", new Vector3(9f, 1.5f, -28f), new Vector3(1.5f, 3f, 1.5f));
        }

        /// <summary>West of the spawn: ledges, gaps, steps and ramps that test each movement number.</summary>
        static void BuildMovementYard(Kit kit, Transform level)
        {
            var yard = new GameObject("Movement Yard").transform;
            yard.SetParent(level, false);

            // A ground jump clears about 1.5 m and a double jump about 2.9 m, so the third ledge is out of reach.
            Block(kit, yard, "Ledge 1.2 m (jump)", new Vector3(-17f, 0.6f, -20f), new Vector3(4f, 1.2f, 4f), kit.Movement);
            Block(kit, yard, "Ledge 2.4 m (double jump)", new Vector3(-23f, 1.2f, -20f), new Vector3(4f, 2.4f, 4f), kit.Movement);
            Block(kit, yard, "Ledge 3.6 m (out of reach)", new Vector3(-29f, 1.8f, -20f), new Vector3(4f, 3.6f, 4f), kit.Movement);

            // Steps a little lower than the controller's step offset (0.35 m), so you walk straight up them.
            for (int i = 0; i < 5; i++)
            {
                float h = 0.3f * (i + 1);
                Block(kit, yard, "Step " + (i + 1) + " (0.3 m)", new Vector3(-38f, h / 2f, -26f + 0.6f * i), new Vector3(3f, h, 0.6f), kit.Movement);
            }
            Block(kit, yard, "Steps Top 1.5 m", new Vector3(-38f, 0.75f, -21.8f), new Vector3(3f, 1.5f, 3f), kit.Movement);

            // Gaps between 1.2 m platforms. A running jump covers about 6 m, a double jump about 10 m,
            // and a jump plus a dash about 12 m, so the 13 m gap needs a double jump and a dash.
            Block(kit, yard, "Gap 5 m Start", new Vector3(-22f, 0.6f, -4f), new Vector3(6f, 1.2f, 6f), kit.Movement);
            Block(kit, yard, "Gap 5 m End (running jump)", new Vector3(-22f, 0.6f, 7f), new Vector3(6f, 1.2f, 6f), kit.Movement);
            Block(kit, yard, "Gap 13 m Start", new Vector3(-32f, 0.6f, -4f), new Vector3(6f, 1.2f, 6f), kit.Movement);
            Block(kit, yard, "Gap 13 m End (double jump + dash)", new Vector3(-32f, 0.6f, 15f), new Vector3(6f, 1.2f, 6f), kit.Movement);

            // Ramps: one you can walk up and one steeper than the controller's slope limit (50 degrees).
            Ramp(yard, "Ramp 25 degrees", new Vector3(-40f, 0f, 0f), 4f, 3f, 25f, kit.Ramp);
            Block(kit, yard, "Ramp Top 3 m", new Vector3(-40f, 1.5f, 3f / Mathf.Tan(25f * Mathf.Deg2Rad) + 3f), new Vector3(4f, 3f, 6f), kit.Movement);
            Ramp(yard, "Ramp 55 degrees (too steep)", new Vector3(-40f, 0f, 20f), 4f, 3f, 55f, kit.Ramp);
            Block(kit, yard, "Steep Ramp Top 3 m", new Vector3(-40f, 1.5f, 20f + 3f / Mathf.Tan(55f * Mathf.Deg2Rad) + 3f), new Vector3(4f, 3f, 6f), kit.Movement);
        }

        /// <summary>A plank on the floor at <paramref name="start"/> rising toward +Z to <paramref name="height"/>.</summary>
        static void Ramp(Transform parent, string name, Vector3 start, float width, float height, float degrees, Material material)
        {
            const float thickness = 0.3f;
            float rad = degrees * Mathf.Deg2Rad, run = height / Mathf.Tan(rad), length = height / Mathf.Sin(rad);
            // The top face runs along the slope, so the center sits half a thickness below that line.
            var up = new Vector3(0f, Mathf.Cos(rad), -Mathf.Sin(rad));
            var center = start + new Vector3(0f, height / 2f, run / 2f) - up * (thickness / 2f);
            var go = Primitive(PrimitiveType.Cube, name, parent, center, new Vector3(width, thickness, length), material);
            go.transform.localRotation = Quaternion.Euler(-degrees, 0f, 0f);
            go.isStatic = true;
        }

        /// <summary>
        /// North of the spawn: a firing line, a stripe every 5 m and dummies from 5 to 40 m, staggered so none
        /// hides another, plus one sliding side to side at 12 m.
        /// </summary>
        static void BuildFiringRange(Kit kit, Transform level, GameObject dummyPrefab)
        {
            var range = new GameObject("Firing Range").transform;
            range.SetParent(level, false);
            const float line = -14f;
            Stripe(range, "Firing Line", new Vector3(0f, 0f, line), 16f, 0.15f, kit.FiringLine);
            for (int d = 5; d <= 40; d += 5)
                Stripe(range, "Range " + d + " m", new Vector3(0f, 0f, line + d), 16f, 0.06f, kit.RangeLine);

            float[] distances = { 5f, 10f, 15f, 20f, 30f, 40f };
            float[] sideways = { -4.5f, -1.5f, 1.5f, 4.5f, -3f, 3f };
            for (int i = 0; i < distances.Length; i++)
                Dummy(dummyPrefab, range, "Dummy " + distances[i] + " m", new Vector3(sideways[i], 0f, line + distances[i]));

            var moving = Dummy(dummyPrefab, range, "Moving Dummy 12 m", new Vector3(0f, 0f, line + 12f));
            WireFloat(moving, "strafeDistance", 4f);
            WireFloat(moving, "strafeSpeed", 3f);
        }

        static TargetDummy Dummy(GameObject prefab, Transform parent, string name, Vector3 position)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.localPosition = position;
            return go.GetComponent<TargetDummy>();
        }

        /// <summary>A thin glowing line across the floor with no collider.</summary>
        static void Stripe(Transform parent, string name, Vector3 center, float width, float depth, Material material)
        {
            var go = Primitive(PrimitiveType.Cube, name, parent, center + new Vector3(0f, 0.012f, 0f),
                               new Vector3(width, 0.02f, depth), material, collider: false);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            go.isStatic = true;
        }

        static void SetUpSun()
        {
            var sun = Object.FindAnyObjectByType<Light>();
            if (!sun) return;
            sun.name = "Sun";
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
        }

        static GameObject Wall(Kit kit, Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = Primitive(PrimitiveType.Cube, name, parent, center, size, kit.Wall);
            go.isStatic = true;
            return go;
        }

        static GameObject Block(Kit kit, Transform parent, string name, Vector3 center, Vector3 size, Material material = null)
        {
            var go = Primitive(PrimitiveType.Cube, name, parent, center, size, material ? material : kit.Block);
            go.isStatic = true;
            return go;
        }

        /// <summary>A colored floor patch with no collider, just above the floor so it doesn't flicker.</summary>
        static void Zone(Transform parent, string name, Vector3 center, Vector2 size, Material material)
        {
            var go = Primitive(PrimitiveType.Cube, name, parent, center + new Vector3(0f, 0.005f, 0f),
                               new Vector3(size.x, 0.01f, size.y), material, collider: false);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            go.isStatic = true;
        }

        // ---- Materials -------------------------------------------------------------------

        /// <summary>The test level's materials, saved in Materials/Greybox. Lit materials use the project's default shader, so they work in the Built-in pipeline and URP.</summary>
        class Kit
        {
            readonly Shader lit;
            public readonly Material Floor, Wall, Block, Spawn, ZoneMovement, ZoneRange, ZoneNests;
            public readonly Material Movement, Ramp;
            public readonly Material GunMetal, GunWood, HeatVent, Dummy, DummyTarget, RangeLine, FiringLine;

            public Kit()
            {
                lit = DefaultLitShader();
                Floor = Lit("Floor", Color.white, 0.1f);
                Floor.mainTexture = GridTexture();
                Floor.mainTextureScale = new Vector2(FloorSize / 2f, FloorSize / 2f);   // one grid tile is 2 m
                Wall = Lit("Wall", Hex(0x555b66), 0.1f);
                Block = Lit("Block", Hex(0x7c8392), 0.15f);
                Spawn = Lit("Spawn Pad", Hex(0x2aa7d6), 0.3f, Hex(0x0b4f66));
                ZoneMovement = Lit("Zone Movement", Hex(0x41536f), 0.1f);
                ZoneRange = Lit("Zone Range", Hex(0x6b6151), 0.1f);
                ZoneNests = Lit("Zone Nests", Hex(0x5a4766), 0.1f);
                Movement = Lit("Movement Block", Hex(0x4f6fa3), 0.15f);
                Ramp = Lit("Ramp", Hex(0x6386c0), 0.15f);
                GunMetal = Lit("Gun Metal", Hex(0x2f3238), 0.65f);
                GunWood = Lit("Gun Wood", Hex(0x6b4428), 0.35f);
                // Emission stays switched on (with a glow too faint to see) so the gun can light it up from code.
                HeatVent = Lit("Heat Vent", Hex(0x26272b), 0.5f, new Color(0.01f, 0.004f, 0.002f));
                Dummy = Lit("Dummy", Hex(0xc9b28a), 0.2f);
                DummyTarget = Lit("Dummy Bullseye", Hex(0xd23a2e), 0.3f, Hex(0x3a0905));
                RangeLine = Lit("Range Line", Hex(0xe8d9a0), 0.1f, Hex(0x3b351c));
                FiringLine = Lit("Firing Line", Hex(0xe0473a), 0.1f, Hex(0x5a0e08));
            }

            public Material Lit(string name, Color color, float smoothness, Color? glow = null)
            {
                string path = MaterialFolder + "/" + name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat)
                {
                    mat = new Material(lit);
                    AssetDatabase.CreateAsset(mat, path);
                }
                else if (mat.shader != lit) mat.shader = lit;
                mat.color = color;
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);   // Built-in Standard
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);   // URP Lit
                if (glow.HasValue)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", glow.Value);
                }
                EditorUtility.SetDirty(mat);
                return mat;
            }

            static Shader DefaultLitShader()
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var material = probe.GetComponent<Renderer>().sharedMaterial;
                Shader shader = material ? material.shader : null;
                Object.DestroyImmediate(probe);
                return shader ? shader : Shader.Find("Standard");
            }
        }

        /// <summary>A 2 m floor tile: a bold line on its edge and a faint one at 1 m.</summary>
        static Texture2D GridTexture()
        {
            string path = TextureFolder + "/Grid.png";
            if (!File.Exists(path))
            {
                const int size = 256;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                var fill = new Color32(58, 61, 68, 255);
                var minor = new Color32(71, 75, 83, 255);
                var major = new Color32(95, 100, 110, 255);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        bool onMajor = x < 3 || y < 3;
                        bool onMinor = x == 127 || x == 128 || y == 127 || y == 128;
                        pixels[y * size + x] = onMajor ? major : onMinor ? minor : fill;
                    }
                tex.SetPixels32(pixels);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    importer.anisoLevel = 8;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---- Helpers ---------------------------------------------------------------------

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
                                    Vector3 localScale, Material material, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>A viewmodel piece: no collider (it would block the player's own shots) and no shadow.</summary>
        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale,
                               Material material, Quaternion? localRotation = null)
        {
            var go = Primitive(type, name, parent, localPosition, localScale, material, collider: false);
            if (localRotation.HasValue) go.transform.localRotation = localRotation.Value;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Sets a private [SerializeField] reference, the way dragging it in the Inspector would.</summary>
        static void Wire(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError("Test level builder: " + target.GetType().Name + " has no serialized field called '" + field + "'.");
                return;
            }
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError("Test level builder: " + target.GetType().Name + " has no serialized field called '" + field + "'.");
                return;
            }
            property.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null || !property.isArray)
            {
                Debug.LogError("Test level builder: " + target.GetType().Name + " has no serialized array called '" + field + "'.");
                return;
            }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Saves into a subfolder of Prefabs, so it can never overwrite a teammate's prefab of the same name.</summary>
        static GameObject SavePrefab(GameObject template, string folder, string name)
        {
            Folder(PrefabFolder + "/" + folder);
            var asset = PrefabUtility.SaveAsPrefabAsset(template, PrefabFolder + "/" + folder + "/" + name + ".prefab");
            Object.DestroyImmediate(template);
            return asset;
        }

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);
    }
}
