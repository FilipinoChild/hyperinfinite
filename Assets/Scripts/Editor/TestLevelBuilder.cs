using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace HyperInfinite
{
    /// <summary>
    /// Hyper Infinite > Build Test Level. Makes a grey-box scene for testing combat and controls, plus
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
        const string GunSpritePath = "Assets/Textures/Sprites/EnergyBlastShotgun.png";
        const string ScuttlerSpritePath = "Assets/Textures/Sprites/Scuttler.png";

        // The regular Scuttler's size, in meters, and the share of it a hatchling is.
        static readonly Vector2 ScuttlerSize = new Vector2(2.8f, 1.62f);
        const float TinyScuttlerScale = 0.3f;

        const string ControlsAssetPath = "Assets/Settings/HyperInfiniteControls.inputactions";

        // Layout, in meters. The player spawns facing north (+Z).
        static readonly Vector3 SpawnPosition = new Vector3(0f, 0f, -25f);
        const float FloorSize = 90f, FloorCenterZ = 10f, WallHeight = 4f;

        [MenuItem("Hyper Infinite/Build Test Level", priority = 1)]
        public static void Build()
        {
            var controlsAsset = ControlsAsset();
            if (!controlsAsset) return;   // checked first, so nothing has been replaced yet
            var gunSprite = PixelArt(GunSpritePath);
            if (!gunSprite) return;
            var scuttlerSprite = PixelArt(ScuttlerSpritePath);
            if (!scuttlerSprite) return;
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
            var playerPrefab = BuildPlayerPrefab(kit, controlsAsset, gunSprite);
            var dummyPrefab = BuildDummyPrefab(kit);
            var scuttlerPrefab = BuildTinyScuttlerPrefab(kit, scuttlerSprite);
            var eggsPrefab = BuildEggsPrefab(kit, scuttlerPrefab);

            var level = new GameObject("Level").transform;
            BuildShell(kit, level);
            BuildMovementYard(kit, level);
            BuildFiringRange(kit, level, dummyPrefab);
            BuildEggGarden(kit, level, eggsPrefab);

            var spawn = new GameObject("Spawn Point").transform;
            spawn.position = SpawnPosition;
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.SetPositionAndRotation(SpawnPosition, Quaternion.identity);

            new GameObject("Debug HUD (temporary)").AddComponent<TempDebugHud>();
            var respawner = new GameObject("Respawner (temporary)").AddComponent<TempRespawner>();
            Wire(respawner, "spawnPoint", spawn);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            Debug.Log("Hyper Infinite: built " + ScenePath + ". Press Play, then click the Game view to capture the mouse.");
        }

        // ---- The player ------------------------------------------------------------------

        /// <summary>
        /// The Input Actions asset with every binding. It's part of the Controls work, so it's made by hand, not
        /// built here. If it can't be loaded, this says why in a dialog and returns null.
        /// </summary>
        static InputActionAsset ControlsAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsAssetPath);
            if (asset) return asset;

            string problem;
            if (File.Exists(ControlsAssetPath))
            {
                problem = "The file is there, but Unity couldn't load it as Input Actions. That usually means part of the text " +
                          "didn't copy (the Console shows an import error for it). Copy the whole file again, then build.";
            }
            else
            {
                // Look for it in another folder or under a slightly different name, such as one ending in a hidden .txt.
                string[] found = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(ControlsAssetPath), new[] { "Assets" });
                for (int i = 0; i < found.Length; i++) found[i] = AssetDatabase.GUIDToAssetPath(found[i]);
                problem = found.Length == 0
                    ? "It isn't in the project yet. Add it, then build again."
                    : "Found " + string.Join(", ", found) + " instead. Move or rename it to match exactly. Windows can hide " +
                      "a .txt on the end of a file name; turn on File name extensions in File Explorer's View menu to see it.";
            }
            EditorUtility.DisplayDialog("Controls asset needed", "The Player gets its controls from " + ControlsAssetPath +
                                        ".\n\n" + problem + "\n\nThe build stopped before changing anything.", "OK");
            return null;
        }

        /// <summary>
        /// A piece of pixel art added by hand, with the import settings 2.5D sprites need: no resizing to a power
        /// of two, no blurring or compression, and Read/Write on so scripts can pick out parts of it. If it's
        /// missing, this says so in a dialog and returns null.
        /// </summary>
        static Texture2D PixelArt(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                bool wrong = importer.textureType != TextureImporterType.Default || importer.npotScale != TextureImporterNPOTScale.None
                             || importer.mipmapEnabled || importer.filterMode != FilterMode.Point || !importer.isReadable
                             || importer.textureCompression != TextureImporterCompression.Uncompressed
                             || !importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp;
                if (wrong)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.mipmapEnabled = false;
                    importer.filterMode = FilterMode.Point;
                    importer.isReadable = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.alphaIsTransparency = true;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                }
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture) return texture;
            }
            EditorUtility.DisplayDialog("Sprite needed", "The test level needs " + path + ", the pixel art from the 10 Pager. " +
                                        "Add it with exactly that name and folder, then build again.\n\nThe build stopped before changing anything.", "OK");
            return null;
        }

        static GameObject BuildPlayerPrefab(Kit kit, InputActionAsset controlsAsset, Texture2D gunSprite)
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
            Wire(controls, "actions", controlsAsset);
            root.AddComponent<PlayerMotor>();
            root.AddComponent<Health>();

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
            BuildShotgun(eyes.transform, gunSprite);

            return SavePrefab(root, "Testing", "Player");
        }

        /// <summary>
        /// The Energy Blast shotgun drawn over the view by ShotgunSprite,
        ///  and a muzzle that the sprite keeps on its emitters, with the flash on it.
        /// </summary>
        static EnergyBlastShotgun BuildShotgun(Transform eyes, Texture2D gunSprite)
        {
            var gun = new GameObject("Energy Blast Shotgun");
            gun.transform.SetParent(eyes, false);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(gun.transform, false);
            muzzle.localPosition = new Vector3(0.28f, -0.22f, 0.7f); 

            var flashObject = new GameObject("Muzzle Flash");
            flashObject.transform.SetParent(muzzle, false);
            var flash = flashObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.range = 8f;
            flash.color = new Color(0.45f, 0.9f, 1f);
            flash.intensity = 0f;
            flash.shadows = LightShadows.None;

            var shotgun = gun.AddComponent<EnergyBlastShotgun>();
            WireArray(shotgun, "emitters", new Object[] { muzzle });
            Wire(shotgun, "muzzleFlash", flash);
            var sprite = gun.AddComponent<ShotgunSprite>();
            Wire(sprite, "sprite", gunSprite);
            Wire(sprite, "muzzle", muzzle);
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

            // Floor tints marking where each of the combat test areas goes.
            Zone(level, "Zone: Movement Yard", new Vector3(-28.5f, 0f, 2f), new Vector2(31f, 66f), kit.ZoneMovement);
            Zone(level, "Zone: Firing Range", new Vector3(0f, 0f, 16f), new Vector2(18f, 62f), kit.ZoneRange);
            Zone(level, "Zone: Egg Garden", new Vector3(28.5f, 0f, 2f), new Vector2(31f, 66f), kit.ZoneEggs);

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

        /// <summary>
        /// The eggs' swarm: a tiny Scuttler, 30% of the regular one's size, drawn 2.5D like the prototype with
        /// its pixel art on a quad that faces the camera. It stands on the ground by its feet.
        /// </summary>
        static GameObject BuildTinyScuttlerPrefab(Kit kit, Texture2D scuttlerSprite)
        {
            Vector2 size = ScuttlerSize * TinyScuttlerScale;
            var root = new GameObject("Tiny Scuttler");
            var trigger = root.AddComponent<SphereCollider>();   // a trigger: shots hit it, the player walks through it
            trigger.isTrigger = true;
            trigger.radius = size.x * 0.4f;
            trigger.center = new Vector3(0f, size.y * 0.5f, 0f);

            var body = new GameObject("Body").transform;   // pivots at the feet, so it squashes onto the ground
            body.SetParent(root.transform, false);
            body.localScale = new Vector3(size.x, size.y, 1f);
            var sprite = Primitive(PrimitiveType.Quad, "Sprite", body, new Vector3(0f, 0.5f, 0f), Vector3.one,
                                   kit.SpriteMaterial("Scuttler Sprite", scuttlerSprite), collider: false);
            sprite.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var scuttler = root.AddComponent<TinyScuttler>();
            Wire(scuttler, "body", body);
            return SavePrefab(root, "Hazards", "Tiny Scuttler");
        }

        // ---- The hazard ------------------------------------------------------------------

        static GameObject BuildEggsPrefab(Kit kit, GameObject scuttlerPrefab)
        {
            var root = new GameObject("Pulsating Eggs");
            var hitbox = root.AddComponent<SphereCollider>();
            hitbox.radius = 0.85f;
            hitbox.center = new Vector3(0f, 0.5f, 0f);
            var goo = Primitive(PrimitiveType.Cylinder, "Goo", root.transform, new Vector3(0f, 0.012f, 0f), new Vector3(2.2f, 0.012f, 2.2f), kit.EggGoo, collider: false);
            goo.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // A tall egg in the middle and five around it, leaning outward.
            var cluster = new GameObject("Eggs").transform;
            cluster.SetParent(root.transform, false);
            var eggs = new Transform[6];
            eggs[0] = Primitive(PrimitiveType.Sphere, "Egg 1", cluster, new Vector3(0f, 0.45f, 0f), new Vector3(0.55f, 0.8f, 0.55f), kit.EggShell, collider: false).transform;
            float[] sizes = { 1f, 0.86f, 1.08f, 0.92f, 0.98f };
            for (int i = 0; i < 5; i++)
            {
                float angle = (i * 72f + 15f) * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var egg = Primitive(PrimitiveType.Sphere, "Egg " + (i + 2), cluster, outward * 0.42f + Vector3.up * 0.3f * sizes[i],
                                    new Vector3(0.42f, 0.58f, 0.42f) * sizes[i], kit.EggShell, collider: false).transform;
                egg.localRotation = Quaternion.AngleAxis(20f, Vector3.Cross(Vector3.up, outward));
                eggs[i + 1] = egg;
            }

            var pulsatingEggs = root.AddComponent<PulsatingEggs>();
            WireArray(pulsatingEggs, "eggs", eggs);
            Wire(pulsatingEggs, "hitbox", hitbox);
            Wire(pulsatingEggs, "scuttlerPrefab", scuttlerPrefab);
            return SavePrefab(root, "Hazards", "Pulsating Eggs");
        }

        /// <summary>
        /// East of the spawn: eggs in the open, behind a low wall, up on a ledge, and three clusters close
        /// together. They grow back after 12 seconds here, so they can be tested again and again.
        /// </summary>
        static void BuildEggGarden(Kit kit, Transform level, GameObject eggsPrefab)
        {
            var garden = new GameObject("Egg Garden").transform;
            garden.SetParent(level, false);

            Eggs(eggsPrefab, garden, "Eggs: Open Ground", new Vector3(22f, 0f, -16f));
            Block(kit, garden, "Low Wall (1.1 m)", new Vector3(30f, 0.55f, -2f), new Vector3(6f, 1.1f, 0.5f));
            Eggs(eggsPrefab, garden, "Eggs: Behind Cover", new Vector3(30f, 0f, 1.5f));
            Block(kit, garden, "Egg Ledge 1.5 m", new Vector3(38f, 0.75f, 14f), new Vector3(6f, 1.5f, 6f), kit.Movement);
            Eggs(eggsPrefab, garden, "Eggs: On the Ledge", new Vector3(38f, 1.5f, 14f));
            Eggs(eggsPrefab, garden, "Eggs: Cluster A", new Vector3(21f, 0f, 18f));
            Eggs(eggsPrefab, garden, "Eggs: Cluster B", new Vector3(24f, 0f, 20.5f));
            Eggs(eggsPrefab, garden, "Eggs: Cluster C", new Vector3(20.5f, 0f, 22f));
            Block(kit, garden, "Pillar", new Vector3(17f, 1.5f, 4f), new Vector3(1.2f, 3f, 1.2f));
            Block(kit, garden, "Pillar", new Vector3(28f, 1.5f, 28f), new Vector3(1.2f, 3f, 1.2f));
        }

        static void Eggs(GameObject prefab, Transform parent, string name, Vector3 position)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.localPosition = position;
            var eggs = go.GetComponent<PulsatingEggs>();
            WireFloat(eggs, "regrowTime", 12f);
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
            public readonly Material Floor, Wall, Block, Spawn, ZoneMovement, ZoneRange, ZoneEggs;
            public readonly Material Movement, Ramp;
            public readonly Material Dummy, DummyTarget, RangeLine, FiringLine;
            public readonly Material EggShell, EggGoo;

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
                ZoneEggs = Lit("Zone Eggs", Hex(0x5a4766), 0.1f);
                Movement = Lit("Movement Block", Hex(0x4f6fa3), 0.15f);
                Ramp = Lit("Ramp", Hex(0x6386c0), 0.15f);
                Dummy = Lit("Dummy", Hex(0xc9b28a), 0.2f);
                DummyTarget = Lit("Dummy Bullseye", Hex(0xd23a2e), 0.3f, Hex(0x3a0905));
                RangeLine = Lit("Range Line", Hex(0xe8d9a0), 0.1f, Hex(0x3b351c));
                FiringLine = Lit("Firing Line", Hex(0xe0473a), 0.1f, Hex(0x5a0e08));
                EggShell = Lit("Egg Shell", Hex(0x8a5a9c), 0.75f, new Color(0.04f, 0.01f, 0.06f));   // the eggs drive the glow
                EggGoo = Lit("Egg Goo", Hex(0x3a2244), 0.85f);
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

            /// <summary>
            /// Pixel art standing in the 3D world: unlit, so it keeps its colors, and
            /// cut out at the edges of its pixels rather than blended, so it sorts correctly with everything else.
            /// </summary>
            public Material SpriteMaterial(string name, Texture2D texture)
            {
                Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
                Shader shader = urpUnlit ? urpUnlit : Shader.Find("Unlit/Transparent Cutout");
                string path = MaterialFolder + "/" + name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, path);
                }
                else if (mat.shader != shader) mat.shader = shader;
                mat.mainTexture = texture;
                mat.color = Color.white;
                mat.SetFloat("_Cutoff", 0.5f);
                if (urpUnlit)
                {
                    mat.SetFloat("_AlphaClip", 1f);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.SetFloat("_Cull", (float)CullMode.Off);
                    mat.SetOverrideTag("RenderType", "TransparentCutout");
                }
                mat.renderQueue = (int)RenderQueue.AlphaTest;
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
