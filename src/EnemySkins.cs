using System;
using System.Collections.Generic;
using Assets.Scripts.Actors.Enemies;
using Actors.Enemies;
using UnityEngine;

namespace MegabonkSteve
{
    internal enum MobKind { Humanoid, Creeper, Slime, Spider, Illager, Wither, Guardian, Dragon, Pig, Cow, Bee, IronGolem, Warden }

    internal class MobSpec
    {
        public MobKind kind;
        public string tex;
        public float scale = 1f;        // extra size on top of matching the enemy's height
        public int armW = 4;            // humanoid arm/leg thickness in pixels
        public bool armsForward = true; // zombies and skeletons hold their arms out
        public bool ranged, explodes;   // skeleton-style archer; creeper that swells and blows up
        public bool ownLeft;            // drowned: separate left arm/leg regions on the texture
        public bool boss;               // bosses keep Megabonk's own special attacks
        public int armU = 40, armV = 46; // illager-type arm region
        public string held;             // item texture in the right hand
    }

    // Gives Megabonk's enemies Minecraft mob models, built from boxes with the real entity textures.
    internal static class EnemySkins
    {
        private static readonly HashSet<EEnemy> logged = new HashSet<EEnemy>();
        private static readonly HashSet<EEnemy> logged2 = new HashSet<EEnemy>();
        private static readonly Dictionary<Texture2D, Material> mats = new Dictionary<Texture2D, Material>();

        private static MobSpec Zombie(string tex, float s = 1f) { return new MobSpec { kind = MobKind.Humanoid, tex = tex, scale = s }; }
        private static MobSpec Skeleton(string tex, float s = 1f, bool bow = true) { return new MobSpec { kind = MobKind.Humanoid, tex = tex, scale = s, armW = 2, ranged = false, held = bow ? "item/bow" : "item/stone_sword" }; }

        internal static MobSpec Map(EEnemy t)
        {
            switch (t)
            {
                case EEnemy.Skeleton: case EEnemy.GoldenSkeleton: case EEnemy.XpSkeleton: case EEnemy.ArmoredSkeleton:
                case EEnemy.SkeletonMage: case EEnemy.Tumblebone:
                    return Skeleton("entity/skeleton/skeleton");
                case EEnemy.SkeletonDusty: case EEnemy.ArmoredSkeletonDusty:
                    return Skeleton("entity/skeleton/stray");
                case EEnemy.Pharaoh1: case EEnemy.Pharaoh2: case EEnemy.Pharaoh3:
                    return new MobSpec { kind = MobKind.IronGolem, tex = "entity/iron_golem/iron_golem", armsForward = false };
                case EEnemy.Bee:
                    return new MobSpec { kind = MobKind.Bee, tex = "entity/bee/bee", scale = 1.5f };
                case EEnemy.MinibossPig: case EEnemy.FrogGreen: case EEnemy.FrogRed:
                    return new MobSpec { kind = MobKind.Pig, tex = "entity/pig/pig_temperate", scale = 1.3f };
                case EEnemy.FrogBlue:
                    return new MobSpec { kind = MobKind.Cow, tex = "entity/cow/cow_temperate", scale = 1.2f };
                case EEnemy.Goblin: case EEnemy.Zombie:
                    return Zombie("entity/zombie/zombie");
                case EEnemy.GoblinStrong:
                    return Zombie("entity/zombie/husk");
                case EEnemy.GoblinTank:
                    { var d = Zombie("entity/zombie/drowned", 1.1f); d.ownLeft = true; return d; }
                case EEnemy.Ghoul:
                    return new MobSpec { kind = MobKind.Illager, tex = "entity/zombie_villager/zombie_villager", armsForward = true, armU = 44, armV = 22 };
                case EEnemy.Mummy: case EEnemy.MummyTank: case EEnemy.MummyAncient:
                    return Zombie("entity/zombie/husk");
                case EEnemy.Bandit:
                    return new MobSpec { kind = MobKind.Illager, tex = "entity/illager/pillager", armsForward = false, ranged = false, held = "item/crossbow_standby" };
                case EEnemy.BoomerSpider:
                    return new MobSpec { kind = MobKind.Creeper, tex = "entity/creeper/creeper", explodes = true };
                case EEnemy.Scorpion: case EEnemy.ScorpionMedium: case EEnemy.MinibossScorpion:
                    return new MobSpec { kind = MobKind.Spider, tex = "entity/spider/spider" };
                case EEnemy.Slime:
                    return new MobSpec { kind = MobKind.Slime, tex = "entity/slime/slime" };
            }
            return null;
        }

        public static void OnInit(Enemy e)
        {
            if (e == null) return;
            EEnemy type;
            try { type = e.enemyData.enemyName; } catch { return; }
            if (logged.Add(type)) Describe(e, type);
            Apply(e, type);
            // Zombies, husks, drowned, zombie villagers, mummies and pillagers walk a little slower.
            try
            {
                if (type == EEnemy.Zombie || type == EEnemy.Goblin || type == EEnemy.GoblinStrong || type == EEnemy.GoblinTank || type == EEnemy.Ghoul
                    || type == EEnemy.Mummy || type == EEnemy.MummyTank || type == EEnemy.MummyAncient || type == EEnemy.Bandit)
                    e.speedMultiplier = Mathf.Min(e.speedMultiplier, 0.82f);
            }
            catch { }
        }

        internal static Material MatFor(Texture2D tex)
        {
            Material m;
            if (mats.TryGetValue(tex, out m) && m != null) return m;
            var inst = MinecraftMode.Instance;
            var baseMat = inst != null && inst.rig != null ? inst.rig.itemMat : null;
            if (baseMat == null) return null;
            m = Keep.It(new Material(baseMat));
            m.mainTexture = tex;
            foreach (var n in new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo" })
                if (m.HasProperty(n)) m.SetTexture(n, tex);
            mats[tex] = m;
            return m;
        }

        private static void Apply(Enemy e, EEnemy type)
        {
            var existing = e.transform.Find("MC_Skin");
            var spec = BossSkins.ForFlags(e) ?? Map(type);
            if (spec != null && spec.boss) { try { if (e.enemyData.teleportCooldown > 0f) { Plugin.Logger.LogInfo("Boss " + type + " teleport cooldown " + e.enemyData.teleportCooldown + " switched off"); e.enemyData.teleportCooldown = 0f; } } catch { } }
            if (spec != null && spec.boss) Plugin.Dbg("BOSS init: " + type + " flag=" + e.enemyFlag + " as " + spec.kind + " at " + Time.time.ToString("0.0") + "s");
            if (spec == null)
            {
                if (existing != null) existing.gameObject.SetActive(false);
                if (e.renderer != null) e.renderer.enabled = true;
                return;
            }
            var tex = McAssets.Tex(spec.tex);
            if (tex == null) { Plugin.Logger.LogWarning("missing mob texture " + spec.tex); return; }
            var mat = MatFor(tex);
            if (mat == null) return;

            if (existing != null)
            {
                var old = existing.GetComponent<MobRig>();
                if (old != null && old.texPath == spec.tex)
                {
                    existing.gameObject.SetActive(true);
                    old.ResetState();
                    if (!spec.boss) { try { e.DisableSpecialAttacks(); } catch { } }
                    if (e.renderer != null) e.renderer.enabled = false;
                    return;
                }
                UnityEngine.Object.Destroy(existing.gameObject);
            }

            Renderer r = e.renderer;
            float height = Mathf.Max(0.5f, e.GetHeight());
            // Pooled enemies keep stale renderer bounds until the next frame, so take the feet from the enemy itself.
            float feetLocal = e.GetFeetPosition().y - e.transform.position.y;
            // Bosses: the game's "feet" can sit far below their body, so fit the model to the real collider instead.
            if (spec.boss)
            {
                try
                {
                    var cb = e.collider.bounds;
                    if (cb.size.y > 1f)
                    {
                        height = cb.size.y;
                        feetLocal = cb.min.y - e.transform.position.y;
                    }
                    Plugin.Dbg("BOSS " + type + " collider center=" + (cb.center - e.transform.position) + " size=" + cb.size
                        + " radius=" + e.collider.radius + " height=" + e.collider.height + " feetLocal(used)=" + feetLocal);
                }
                catch (Exception ex) { Plugin.Logger.LogWarning("boss collider: " + ex.Message); }
            }
            if (logged2.Add(type))
                Plugin.Dbg("SKIN " + type + " feetLocal=" + feetLocal + " height=" + height + " rootY=" + e.transform.position.y
                    + " feet=" + e.GetFeetPosition() + " bottom=" + e.GetBottomPosition() + " center=" + e.GetCenterPosition()
                    + " boundsMin=" + (r != null ? r.bounds.min.y : 0f) + " layer=" + e.gameObject.layer);
            var go = new GameObject("MC_Skin");
            go.transform.SetParent(e.transform, false);
            go.transform.localPosition = new Vector3(0f, feetLocal, 0f);
            go.transform.localRotation = Quaternion.identity;
            var rig = go.AddComponent<MobRig>();
            rig.enemy = e;
            rig.texPath = spec.tex;
            rig.kind = (int)spec.kind;
            rig.armsForward = spec.armsForward;
            rig.ranged = spec.ranged; rig.explodes = spec.explodes;
            Build(rig, spec, tex, mat, height);
            // Plain Minecraft mobs only walk, shoot or explode; the game's lasers and other specials are for bosses.
            if (!spec.boss) { try { e.DisableSpecialAttacks(); } catch { } }
            rig.bossKind = spec.boss;
            if (spec.boss) FitCollider(e, spec, rig.unit, feetLocal);
            AttachHeld(rig, spec);
            rig.CollectRenderers();
            if (spec.ranged) KeepDistance(e);
            if (r != null) r.enabled = false;
        }

        // The boss's hitbox is resized to cover the whole Minecraft model, so shots and swings land where it looks.
        private static void FitCollider(Enemy e, MobSpec spec, float unit, float feetLocal)
        {
            try
            {
                float radius, h;
                switch (spec.kind)
                {
                    case MobKind.Guardian: radius = 8f; h = 16f; break;     // head + plates + spines
                    case MobKind.Wither: radius = 10f; h = 27f; break;      // shoulders are 20 wide
                    case MobKind.Warden: radius = 11f; h = 50f; break;      // 2.9 blocks tall, broad shoulders
                    default: radius = 26f; h = 50f; break;                  // dragon: body, neck and wings
                }
                var c = e.collider;
                // Only a little wider, if the model's shoulders need it: the capsule's height and centre are the game's own, because its
                // ground contact, teleports and attacks are built around them.
                c.radius = Mathf.Max(c.radius, radius * unit * 0.8f);
                Plugin.Logger.LogInfo("Boss collider: r=" + c.radius + " h=" + c.height + " centre=" + c.center);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning("FitCollider: " + ex.Message); }
        }

        // Archers keep their distance instead of walking up to melee range.
        private static readonly Dictionary<EnemyData, Vector2> savedStay = new Dictionary<EnemyData, Vector2>();
        private static void KeepDistance(Enemy e)
        {
            try
            {
                var d = e.enemyData;
                if (d.maxStayAtDistance > 0.01f || savedStay.ContainsKey(d)) return;
                savedStay[d] = new Vector2(d.minStayAtDistance, d.maxStayAtDistance);
                d.minStayAtDistance = 20f; d.maxStayAtDistance = 34f;
            }
            catch { }
        }

        public static void RestoreAll()
        {
            foreach (var kv in savedStay) { try { kv.Key.minStayAtDistance = kv.Value.x; kv.Key.maxStayAtDistance = kv.Value.y; } catch { } }
            savedStay.Clear();
        }

        private static Material whiteMat;
        public static Material WhiteMat()
        {
            if (whiteMat != null) return whiteMat;
            var inst = MinecraftMode.Instance;
            var baseMat = inst != null && inst.rig != null ? inst.rig.itemMat : null;
            if (baseMat == null) return null;
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, Color.white); t.Apply(); t.filterMode = FilterMode.Point;
            Keep.It(t);
            whiteMat = Keep.It(new Material(baseMat));
            whiteMat.mainTexture = t;
            foreach (var n in new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo" })
                if (whiteMat.HasProperty(n)) whiteMat.SetTexture(n, t);
            return whiteMat;
        }

        // The skeleton's bow (or the wither skeleton's sword) in its right hand.
        private static void AttachHeld(MobRig rig, MobSpec spec)
        {
            if (spec.held == null || rig.armR == null) return;
            var icon = McAssets.Tex(spec.held);
            if (icon == null) return;
            var mat = MatFor(icon);
            if (mat == null) return;
            var go = new GameObject("held");
            go.transform.SetParent(rig.armR, false);
            go.transform.localPosition = new Vector3(0f, -10f * rig.unit, 1.5f * rig.unit);
            go.transform.localRotation = Quaternion.Euler(0f, -90f, -45f) * (spec.held.Contains("bow") && !spec.held.Contains("crossbow") ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity);   // blade forward, handle in the hand; the bow is turned round so it bulges away from the skeleton
            go.transform.localScale = Vector3.one * rig.unit * 1.15f;
            // The hand closes on the handle, not the middle of the sprite: shift the mesh so the grip point sits at the hand.
            Vector3 grip = spec.held.Contains("sword") || spec.held.Contains("axe") ? new Vector3(-4.5f, -4.5f, 0f)
                         : spec.held.Contains("crossbow") ? new Vector3(-1f, -3f, 0f) : new Vector3(-1f, -1f, 0f);
            var m = new GameObject("held_m");
            m.transform.SetParent(go.transform, false);
            m.transform.localPosition = -grip;
            m.AddComponent<MeshFilter>().sharedMesh = ItemMesh.Build(icon);
            m.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ------------------------------------------------------------------ model builders

        internal static Transform Part(Transform parent, string name, Vector3 pivotPx, Vector3 centerPx, Vector3 sizePx,
                                      Vector3 uvDims, int u, int v, Texture2D tex, Material mat, float unit, bool mirror)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = pivotPx * unit;
            var m = new GameObject(name + "_m");
            m.transform.SetParent(pivot, false);
            m.transform.localPosition = centerPx * unit;
            m.AddComponent<MeshFilter>().sharedMesh = SteveRig.CutoutBox(tex, sizePx.x * unit, sizePx.y * unit, sizePx.z * unit,
                (int)uvDims.x, (int)uvDims.y, (int)uvDims.z, u, v, mirror);
            m.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return pivot;
        }

        private static void Build(MobRig rig, MobSpec spec, Texture2D tex, Material mat, float height)
        {
            var root = rig.transform;
            switch (spec.kind)
            {
                case MobKind.Humanoid: BuildHumanoid(rig, root, spec, tex, mat, height); break;
                case MobKind.Creeper: BuildCreeper(rig, root, tex, mat, height); break;
                case MobKind.Slime: BuildSlime(rig, root, tex, mat, height); break;
                case MobKind.Spider: BuildSpider(rig, root, tex, mat, height); break;
                case MobKind.Illager: BuildIllager(rig, root, tex, mat, height, spec.armU, spec.armV); break;
                case MobKind.Pig: MobModels.BuildPig(rig, root, tex, mat, spec.scale); break;
                case MobKind.Cow: MobModels.BuildCow(rig, root, tex, mat, spec.scale); break;
                case MobKind.Bee: MobModels.BuildBee(rig, root, tex, mat, spec.scale); break;
                case MobKind.IronGolem: MobModels.BuildIronGolem(rig, root, tex, mat, spec.scale); break;
                case MobKind.Warden: MobModels.BuildWarden(rig, root, tex, mat, spec.scale); break;
                case MobKind.Wither: BossSkins.BuildWither(rig, root, tex, mat, height); break;
                case MobKind.Guardian: BossSkins.BuildGuardian(rig, root, tex, mat, height); break;
                case MobKind.Dragon: BossSkins.BuildDragon(rig, root, tex, mat, height); break;
            }
        }

        private static void BuildHumanoid(MobRig rig, Transform root, MobSpec spec, Texture2D tex, Material mat, float height)
        {
            // Never bigger than vanilla (a block is 2.5 world units): big Megabonk enemies must not become giants.
            float unit = Mathf.Min(height * spec.scale / 32f, 0.19f * spec.scale);
            rig.unit = unit;
            int aw = spec.armW;
            // Most vanilla mobs draw the left limbs as the mirrored right ones; the drowned has its own regions.
            bool tall = spec.ownLeft;
            Vector3 bodyDims = new Vector3(8, 12, 4);
            Vector3 limb = new Vector3(aw, 12, aw);

            Part(root, "body", new Vector3(0, 18, 0), Vector3.zero, bodyDims, bodyDims, 16, 16, tex, mat, unit, false);
            rig.head = Part(root, "head", new Vector3(0, 28, 0), Vector3.zero, new Vector3(8, 8, 8), new Vector3(8, 8, 8), 0, 0, tex, mat, unit, false);
            float ax = 4 + aw / 2f;
            rig.armR = Part(root, "armR", new Vector3(ax, 24, 0), new Vector3(0, -6, 0), limb, limb, 40, 16, tex, mat, unit, false);
            rig.armL = tall
                ? Part(root, "armL", new Vector3(-ax, 24, 0), new Vector3(0, -6, 0), limb, limb, 32, 48, tex, mat, unit, false)
                : Part(root, "armL", new Vector3(-ax, 24, 0), new Vector3(0, -6, 0), limb, limb, 40, 16, tex, mat, unit, true);
            float lx = aw == 2 ? 2f : 2f;
            rig.legR = Part(root, "legR", new Vector3(lx, 12, 0), new Vector3(0, -6, 0), limb, limb, 0, 16, tex, mat, unit, false);
            rig.legL = tall
                ? Part(root, "legL", new Vector3(-lx, 12, 0), new Vector3(0, -6, 0), limb, limb, 16, 48, tex, mat, unit, false)
                : Part(root, "legL", new Vector3(-lx, 12, 0), new Vector3(0, -6, 0), limb, limb, 0, 16, tex, mat, unit, true);
            // second skin layer where the texture has one (hat, jacket, sleeves, pants)
            if (tall) AddLayers(rig, tex, mat, unit, aw);
        }

        private static void AddLayers(MobRig rig, Texture2D tex, Material mat, float unit, int aw)
        {
            Vector3 b = new Vector3(8, 12, 4), l = new Vector3(aw, 12, aw);
            Layer(rig.head, Vector3.zero, new Vector3(8, 8, 8), 32, 0, tex, mat, unit);
            Layer(rig.armR, new Vector3(0, -6, 0), l, 40, 32, tex, mat, unit);
            Layer(rig.armL, new Vector3(0, -6, 0), l, 48, 48, tex, mat, unit);
            Layer(rig.legR, new Vector3(0, -6, 0), l, 0, 32, tex, mat, unit);
            Layer(rig.legL, new Vector3(0, -6, 0), l, 0, 48, tex, mat, unit);
        }

        private static void Layer(Transform pivot, Vector3 centerPx, Vector3 size, int u, int v, Texture2D tex, Material mat, float unit)
        {
            var go = new GameObject("layer");
            go.transform.SetParent(pivot, false);
            go.transform.localPosition = centerPx * unit;
            go.AddComponent<MeshFilter>().sharedMesh = SteveRig.CutoutBox(tex, (size.x + 1f) * unit, (size.y + 1f) * unit, (size.z + 1f) * unit,
                (int)size.x, (int)size.y, (int)size.z, u, v, false);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // Vanilla illager (pillager) model: tall 8x10x8 head with a big nose, 8x12x6 body, jacket, plain arms and legs.
        private static void BuildIllager(MobRig rig, Transform root, Texture2D tex, Material mat, float height, int armU, int armV)
        {
            float unit = Mathf.Min(height / 34f, 0.19f);
            rig.unit = unit;
            Vector3 body = new Vector3(8, 12, 6), leg = new Vector3(4, 12, 4), arm = new Vector3(4, 12, 4);
            Part(root, "body", new Vector3(0, 18, 0), Vector3.zero, body, body, 16, 20, tex, mat, unit, false);
            // jacket over the body down to the knees
            Part(root, "jacket", new Vector3(0, 15, 0), Vector3.zero, new Vector3(9, 19, 7), new Vector3(8, 18, 6), 0, 38, tex, mat, unit, false);
            rig.head = Part(root, "head", new Vector3(0, 29, 0), Vector3.zero, new Vector3(8, 10, 8), new Vector3(8, 10, 8), 0, 0, tex, mat, unit, false);
            Part(rig.head, "nose", new Vector3(0, -4f, 5f), Vector3.zero, new Vector3(2, 4, 2), new Vector3(2, 4, 2), 24, 0, tex, mat, unit, false);
            rig.armR = Part(root, "armR", new Vector3(6, 24, 0), new Vector3(0, -6, 0), arm, arm, armU, armV, tex, mat, unit, false);
            rig.armL = Part(root, "armL", new Vector3(-6, 24, 0), new Vector3(0, -6, 0), arm, arm, armU, armV, tex, mat, unit, true);
            rig.legR = Part(root, "legR", new Vector3(2, 12, 0), new Vector3(0, -6, 0), leg, leg, 0, 22, tex, mat, unit, false);
            rig.legL = Part(root, "legL", new Vector3(-2, 12, 0), new Vector3(0, -6, 0), leg, leg, 0, 22, tex, mat, unit, true);
        }
        private static void BuildCreeper(MobRig rig, Transform root, Texture2D tex, Material mat, float height)
        {
            float unit = Mathf.Min(height / 26f, 0.19f);
            rig.unit = unit;
            Vector3 body = new Vector3(8, 12, 4), leg = new Vector3(4, 6, 4);
            Part(root, "body", new Vector3(0, 12, 0), Vector3.zero, body, body, 16, 16, tex, mat, unit, false);
            rig.head = Part(root, "head", new Vector3(0, 22, 0), Vector3.zero, new Vector3(8, 8, 8), new Vector3(8, 8, 8), 0, 0, tex, mat, unit, false);
            rig.legs = new Transform[4];
            int i = 0;
            foreach (var z in new[] { 4f, -4f })
                foreach (var x in new[] { 2f, -2f })
                    rig.legs[i++] = Part(root, "leg" + i, new Vector3(x, 6, z), new Vector3(0, -3, 0), leg, leg, 0, 16, tex, mat, unit, false);
        }

        // Translucent vertex-coloured material (depth-tested, drawn after the opaque model).
        private static Material glassMat;
        private static Material GlassMat()
        {
            if (glassMat != null) return glassMat;
            Shader sh = Shader.Find("Hidden/Internal-Colored");
            if (sh == null) return null;
            glassMat = Keep.It(new Material(sh));
            glassMat.SetInt("_SrcBlend", 5);    // SrcAlpha
            glassMat.SetInt("_DstBlend", 10);   // OneMinusSrcAlpha
            glassMat.SetInt("_Cull", 0);
            glassMat.SetInt("_ZWrite", 0);
            glassMat.SetInt("_ZTest", 4);       // LessEqual
            glassMat.renderQueue = 3000;
            return glassMat;
        }

        // Vanilla slime model, in pixels with the feet at y=0: opaque 6x6x6 core with two 2x2x2 eyes and a 1x1x1 mouth,
        // inside a translucent 8x8x8 shell.
        private static void BuildSlime(MobRig rig, Transform root, Texture2D tex, Material mat, float height)
        {
            float unit = height / 8f;
            rig.unit = unit;
            var body = new GameObject("slime").transform;
            body.SetParent(root, false);
            body.localPosition = Vector3.zero;
            rig.body = body;

            Part(body, "core", new Vector3(0, 4, 0), Vector3.zero, new Vector3(6, 6, 6), new Vector3(6, 6, 6), 0, 16, tex, mat, unit, false);
            var eye = new Vector3(2, 2, 2);
            Part(body, "eyeL", new Vector3(-2.3f, 5, 2.5f), Vector3.zero, eye, eye, 32, 4, tex, mat, unit, false);
            Part(body, "eyeR", new Vector3(2.3f, 5, 2.5f), Vector3.zero, eye, eye, 32, 0, tex, mat, unit, false);
            Part(body, "mouth", new Vector3(-0.5f, 2.5f, 2.5f), Vector3.zero, new Vector3(1, 1, 1), new Vector3(1, 1, 1), 32, 8, tex, mat, unit, false);

            // The shell: only the texels the texture actually paints, keeping their own alpha.
            var gm = GlassMat();
            var shell = new GameObject("shell");
            shell.transform.SetParent(body, false);
            shell.transform.localPosition = new Vector3(0f, 4f * unit, 0f);
            shell.AddComponent<MeshFilter>().sharedMesh = SteveRig.CutoutBox(tex, 8f * unit, 8f * unit, 8f * unit, 8, 8, 8, 0, 0, false, true, 0.6f, 0.85f);
            if (gm != null) shell.AddComponent<MeshRenderer>().sharedMaterial = gm;
            else shell.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
        private static void BuildSpider(MobRig rig, Transform root, Texture2D tex, Material mat, float height)
        {
            float unit = 0.17f;   // vanilla size: a spider is 1.4 blocks wide, however big the Megabonk enemy is
            rig.unit = unit;
            // Vanilla spider: head, neck and body in a row along the facing direction, eight legs.
            Part(root, "body", new Vector3(0, 6, -9), Vector3.zero, new Vector3(10, 8, 12), new Vector3(10, 8, 12), 0, 12, tex, mat, unit, false);
            Part(root, "neck", new Vector3(0, 6, 0), Vector3.zero, new Vector3(6, 6, 6), new Vector3(6, 6, 6), 0, 0, tex, mat, unit, false);
            rig.head = Part(root, "head", new Vector3(0, 6, 7), Vector3.zero, new Vector3(8, 8, 8), new Vector3(8, 8, 8), 32, 4, tex, mat, unit, false);
            rig.legs = new Transform[8];
            float[] zs = { 2.5f, 0.8f, -0.8f, -2.5f };
            float[] yaw = { 40f, 14f, -14f, -40f };
            for (int s = 0; s < 2; s++)
                for (int k = 0; k < 4; k++)
                {
                    int side = s == 0 ? 1 : -1;
                    var pivot = Part(root, "leg" + (s * 4 + k), new Vector3(side * 4f, 6, zs[k] - 3f), new Vector3(side * 7.5f, 0, 0),
                        new Vector3(16, 2, 2), new Vector3(16, 2, 2), 18, 0, tex, mat, unit, s == 1);
                    pivot.localRotation = Quaternion.Euler(0f, side * (-yaw[k]), side * -18f);
                    rig.legs[s * 4 + k] = pivot;
                }
        }

        // ------------------------------------------------------------------ debugging

        // One-off dump of how an enemy is built, to line the Minecraft model up with it.
        private static void Describe(Enemy e, EEnemy type)
        {
            try
            {
                var d = e.enemyData;
                Plugin.Dbg("ENEMY " + type + " hp=" + d.hp + " speed=" + d.speed + " scale=" + d.rendererScale + " radius=" + d.colliderRadius
                    + " flying=" + d.isFlying + " height=" + e.GetHeight() + " -> " + (Map(type) != null ? Map(type).tex : "unchanged"));
            }
            catch (Exception ex) { Plugin.Logger.LogWarning("Describe " + type + ": " + ex.Message); }
        }
    }

    // Animates a Minecraft mob model from the enemy's real movement.
    public class MobRig : MonoBehaviour
    {
        public MobRig(IntPtr ptr) : base(ptr) { }

        public Enemy enemy;
        public string texPath;
        public int kind;
        public bool armsForward = true;
        public float unit;
        public Transform head, armR, armL, legR, legL, body, sideL, sideR, eye, wingL, wingR;
        public Transform[] misc;
        public bool bossKind;
        public Transform[] legs;
        public bool ranged, explodes;

        // ---- behaviour state
        private Renderer[] rends;
        private Material[] origMats;
        private bool isWhite;
        private float fuse;
        private bool swelling;
        private int shootState;      // 0 idle, 1 drawing the bow
        private float shootCd = 1.5f, drawT;

        public void CollectRenderers()
        {
            var arr = GetComponentsInChildren<MeshRenderer>();
            rends = new Renderer[arr.Length];
            origMats = new Material[arr.Length];
            int big = -1; float bigV = -1f;
            for (int i = 0; i < arr.Length; i++)
            {
                rends[i] = arr[i]; origMats[i] = arr[i].sharedMaterial;
                var s = arr[i].bounds.size; float v = s.x * s.y * s.z;
                if (v > bigV) { bigV = v; big = i; }
            }
            // Every box of every mob drawn into every shadow cascade is what hurts with a crowd: only the biggest box casts a shadow.
            for (int i = 0; i < arr.Length; i++)
                if (i != big) arr[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void SetWhite(bool on)
        {
            if (rends == null || on == isWhite) return;
            var w = EnemySkins.WhiteMat();
            if (on && w == null) return;
            isWhite = on;
            for (int i = 0; i < rends.Length; i++)
                if (rends[i] != null) rends[i].sharedMaterial = on ? w : origMats[i];
        }

        // Creeper: swells within 3 blocks of the player, flashes white and explodes after 1.5 s; backs off past 7 blocks.
        // Skeleton: draws and fires arrows at the player from a distance.
        private void Behave(float dt)
        {
            var pl = Assets.Scripts.Actors.Player.MyPlayer.Instance;
            if (pl == null) return;
            Vector3 to = pl.transform.position - enemy.transform.position;
            float dist = new Vector2(to.x, to.z).magnitude;

            if (explodes)
            {
                if (dist < 7.5f) swelling = true; else if (dist > 17.5f) swelling = false;
                if (swelling) { if (fuse <= 0f) McSound.PlayAt("entity.creeper.primed", enemy.transform.position, 1f); fuse += dt; }
                else fuse = Mathf.Max(0f, fuse - dt);
                float swell = Mathf.Clamp01(fuse / 1.5f);
                float f1 = 1f + Mathf.Sin(swell * 100f) * swell * 0.01f;
                float g = swell * swell; g *= g;
                transform.localScale = new Vector3((1f + g * 0.4f) * f1, (1f + g * 0.1f) / f1, (1f + g * 0.4f) * f1);
                SetWhite(swell > 0f && ((int)(swell * 10f)) % 2 == 0);
                if (fuse >= 1.5f) Blow(pl);
            }

            if (bossKind && (kind == (int)MobKind.Wither || kind == (int)MobKind.Dragon))
            {
                shootCd -= dt;
                if (shootCd <= 0f && dist < 90f && dist > 6f)
                {
                    shootCd = kind == (int)MobKind.Wither ? UnityEngine.Random.Range(2.4f, 3.6f) : UnityEngine.Random.Range(3.5f, 5.5f);
                    FireBalls(pl);
                }
            }
            if (bossKind && kind == (int)MobKind.Warden)
            {
                // The Warden's sonic boom: a shriek that hurts through everything between it and you.
                shootCd -= dt;
                if (shootCd <= 0f && dist < 70f && dist > 4f)
                {
                    shootCd = UnityEngine.Random.Range(4.5f, 7f);
                    SonicBoom(pl);
                }
            }
            if (ranged)
            {
                shootCd -= dt;
                if (shootState == 0)
                {
                    if (shootCd <= 0f && dist < 45f && dist > 5f && HasLineOfSight(pl)) { shootState = 1; drawT = 0f; }
                }
                else
                {
                    drawT += dt;
                    if (drawT >= 0.9f) { Shoot(pl); shootState = 0; shootCd = UnityEngine.Random.Range(1.6f, 3.2f); }
                }
            }
        }

        private bool HasLineOfSight(Assets.Scripts.Actors.Player.MyPlayer pl)
        {
            Vector3 eye = enemy.GetHeadPosition();
            Vector3 tgt = pl.transform.position + Vector3.up * (pl.height * 0.6f);
            Vector3 d = tgt - eye;
            float best = d.magnitude;
            foreach (var h in Physics.RaycastAll(eye, d.normalized, best))
            {
                if (h.collider == null || h.collider.isTrigger) continue;
                if (h.collider.GetComponentInParent<Enemy>() != null) continue;
                if (h.collider.GetComponentInParent<Assets.Scripts.Actors.Player.MyPlayer>() != null) continue;
                if (h.distance < best - 0.5f) return false;
            }
            return true;
        }

        private void Shoot(Assets.Scripts.Actors.Player.MyPlayer pl)
        {
            Vector3 eye = enemy.GetHeadPosition();
            Vector3 tgt = pl.transform.position + Vector3.up * (pl.height * 0.6f);
            Vector3 to = tgt - eye;
            float speed = 85f;
            float t = to.magnitude / speed;
            Vector3 aim = (to + Vector3.up * (0.5f * 50f * t * t)).normalized;
            aim = Quaternion.Euler(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-3f, 3f), 0f) * aim;
            float dmg = Mathf.Max(3f, enemy.enemyData.damage);
            AutoWeapons.SpawnEnemyArrow(eye + aim * 1.5f, aim * speed, dmg, enemy);
            McSound.PlayAt("entity.skeleton.shoot", eye, 1f, 1f / (UnityEngine.Random.value * 0.4f + 0.8f));
        }

        // Wither: a fan of three dark skulls. Dragon: one big purple fireball. Both home in on the player.
        private void FireBalls(Assets.Scripts.Actors.Player.MyPlayer pl)
        {
            Vector3 from = enemy.GetHeadPosition();
            Vector3 tgt = pl.transform.position + Vector3.up * (pl.height * 0.5f);
            Vector3 dir = (tgt - from).normalized;
            float dmg = Mathf.Max(20f, enemy.enemyData.damage * 1.5f);
            if (kind == (int)MobKind.Wither)
            {
                for (int i = -1; i <= 1; i++)
                {
                    Vector3 d = Quaternion.AngleAxis(i * 14f, Vector3.up) * dir;
                    AutoWeapons.SpawnEnemyBall(from + d * 3f, d * 32f, dmg, enemy, new Color(0.08f, 0.08f, 0.2f), 11f, 2.2f);
                }
                McSound.PlayAt("entity.wither.shoot", from, 1.2f);
            }
            else
            {
                AutoWeapons.SpawnEnemyBall(from + dir * 4f, dir * 26f, dmg * 1.3f, enemy, new Color(0.75f, 0.25f, 0.95f), 18f, 4.5f);
                McSound.PlayAt("entity.ender_dragon.shoot", from, 1.4f);
            }
        }
        private void SonicBoom(Assets.Scripts.Actors.Player.MyPlayer pl)
        {
            Vector3 from = enemy.GetHeadPosition();
            Vector3 tgt = pl.transform.position + Vector3.up * (pl.height * 0.5f);
            Vector3 dir = (tgt - from).normalized;
            float len = (tgt - from).magnitude;
            McSound.PlayAt("entity.warden.sonic_boom", from, 1.4f);
            for (float d = 4f; d < len; d += 5f) Fx.SonicBoom(from + dir * d, 7f);
            float dmg = Mathf.Max(25f, enemy.enemyData.damage * 1.5f);
            try
            {
                var mm = MinecraftMode.Instance;
                if (mm != null && mm.Blocking) { McSound.Play("item.shield.block", 1f); return; }   // a raised shield takes the boom
                var ph = pl.inventory != null ? pl.inventory.playerHealth : null;
                if (ph != null) ph.DamagePlayerExternal(dmg, 8f, dir, false, "Warden", enemy: enemy);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning("sonic boom: " + ex.Message); }
        }

        public float lastPlayerDmg = -10f;
        private bool blown;
        // The game kills a creeper by itself when you brush past it (its own contact explosion): that one must still go off.
        public void BlowIfSelfDestructed()
        {
            if (!explodes || blown || Time.time - lastPlayerDmg < 0.4f) return;
            var pl = Assets.Scripts.Actors.Player.MyPlayer.Instance;
            if (pl != null) Blow(pl, true);
        }

        private void Blow(Assets.Scripts.Actors.Player.MyPlayer pl, bool force = false)
        {
            if (blown) return;
            try { if (!force && enemy.IsDead()) return; } catch { }
            blown = true;
            Vector3 pos = enemy.GetCenterPosition();
            float dmg = Mathf.Max(25f, enemy.enemyData.damage * 4f);
            float radius = 17.5f;
            AutoWeapons.ExplodeAt(pos, radius, dmg, "Creeper", enemy);
            Vector3 d = pl.transform.position + Vector3.up * (pl.height * 0.5f) - pos;
            if (d.magnitude < radius)
            {
                try
                {
                    var ph = pl.inventory != null ? pl.inventory.playerHealth : null;
                    if (ph != null) ph.DamagePlayerExternal(dmg * (1f - d.magnitude / radius), 8f, d.normalized, false, "Creeper", enemy: enemy);
                }
                catch (Exception e) { Plugin.Logger.LogWarning("creeper blast: " + e.Message); }
            }
            fuse = 0f; swelling = false;
            SetWhite(false);
            try { enemy.Kill("Creeper"); } catch { }
        }

        private float nextGround, nextTelemetry, groundLocal;
        private Vector3 lastPos;
        private float limbSwing, limbAmount, deathT = -1f, age, hopT;
        private bool inited;

        public void ResetState()
        {
            deathT = -1f; limbAmount = 0f; inited = false; blown = false; lastPlayerDmg = -10f;
            fuse = 0f; swelling = false; shootState = 0; shootCd = UnityEngine.Random.Range(1f, 2.5f);
            SetWhite(false);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private float pendingDt;
        private static int posFrame = -1;
        private static Vector3 playerPosCache;
        private static float DistToPlayer(Vector3 p)
        {
            if (posFrame != Time.frameCount)
            {
                posFrame = Time.frameCount;
                var pl = Assets.Scripts.Actors.Player.MyPlayer.Instance;
                if (pl != null) playerPosCache = pl.transform.position;
            }
            return (p - playerPosCache).magnitude;
        }

        private void LateUpdate()
        {
            if (enemy == null) return;
            // With many enemies on screen the per-mob animation is the main cost: far mobs animate every 3rd/6th frame.
            float dtAcc = Time.deltaTime;
            if (!bossKind)
            {
                float dd = DistToPlayer(enemy.transform.position);
                int every = dd > 140f ? 6 : (dd > 70f ? 3 : 1);
                if (every > 1)
                {
                    pendingDt += dtAcc;
                    if (age >= 1f && deathT < 0f && (Time.frameCount + GetInstanceID() % every + every) % every != 0) return;
                    dtAcc = pendingDt;
                }
                pendingDt = 0f;
            }
            // Keep the feet pinned to the ground for the first moments after spawning (the pool re-places the enemy).
            if (bossKind || kind == (int)MobKind.IronGolem)
            {
                try
                {
                    bool walker = kind == (int)MobKind.IronGolem || kind == (int)MobKind.Warden;
                    float ry = enemy.transform.position.y;
                    if (walker)
                    {
                        // Walking bosses stand on the real ground, found with a ray, not on a guess from the game's "feet".
                        if (Time.time >= nextGround)
                        {
                            nextGround = Time.time + 0.2f;
                            var pl2 = Assets.Scripts.Actors.Player.MyPlayer.Instance;
                            int mask = pl2 != null && pl2.playerMovement != null ? pl2.playerMovement.whatIsGround : ~0;
                            RaycastHit gh;
                            if (Physics.Raycast(enemy.transform.position + Vector3.up * 2f, Vector3.down, out gh, 200f, mask, QueryTriggerInteraction.Ignore))
                                groundLocal = gh.point.y - ry;
                        }
                        if (groundLocal < -0.01f) { var lp0 = transform.localPosition; transform.localPosition = new Vector3(lp0.x, groundLocal, lp0.z); }
                    }
                    else
                    {
                        // Floating bosses (wither, guardian, dragon): the model sits on the bottom of the game's own capsule.
                        var lp = transform.localPosition;
                        transform.localPosition = new Vector3(lp.x, enemy.collider.bounds.min.y - ry, lp.z);
                    }
                    if (age < 40f && Time.time >= nextTelemetry)
                    {
                        nextTelemetry = Time.time + 2f;
                        var cb = enemy.collider.bounds;
                        Plugin.Dbg("BOSS telemetry " + enemy.enemyData.enemyName + " rootY=" + ry.ToString("0.0") + " groundLocal=" + groundLocal.ToString("0.0")
                            + " capsule=" + (cb.min.y - ry).ToString("0.0") + ".." + (cb.max.y - ry).ToString("0.0") + " teleportCd=" + enemy.enemyData.teleportCooldown + " speed=" + enemy.enemyData.speed);
                    }
                }
                catch { }
            }
            else if (age < 1f)
            {
                var lp = transform.localPosition;
                transform.localPosition = new Vector3(lp.x, enemy.GetFeetPosition().y - enemy.transform.position.y, lp.z);
            }
            // The game may switch its own mesh back on (damage flash, status effects); keep it hidden.
            try { if (enemy.renderer != null && enemy.renderer.enabled) enemy.renderer.enabled = false; } catch { }
            float dt = dtAcc;
            age += dt;

            Vector3 p = enemy.transform.position;
            if (!inited) { lastPos = p; inited = true; }
            Vector3 d = p - lastPos; lastPos = p;
            float speed = dt > 0.0001f ? new Vector2(d.x, d.z).magnitude / dt : 0f;      // units per second
            float ticks = dt * 20f;
            float amt = Mathf.Clamp01(speed / 9f);
            limbAmount += (amt - limbAmount) * Mathf.Min(1f, 0.4f * ticks);
            limbSwing += limbAmount * ticks * 0.9f;
            float ph = limbSwing * 0.6662f;
            float rad = Mathf.Rad2Deg;

            bool dead = false;
            try { dead = enemy.IsDead(); } catch { }
            if (dead && deathT < 0f) { deathT = 0f; SetWhite(false); }
            if (!dead) { try { Behave(dt); } catch (Exception ex) { if (age < 3f) Plugin.Logger.LogWarning("mob behaviour: " + ex.Message); } }
            if (deathT >= 0f)
            {
                deathT += dt;
                float k = Mathf.Clamp01(deathT / 0.35f);
                transform.localRotation = Quaternion.Euler(0f, 0f, -90f * k);   // falls over like vanilla
            }

            if (kind == (int)MobKind.Humanoid || kind == (int)MobKind.Illager || kind == (int)MobKind.IronGolem || kind == (int)MobKind.Warden)
            {
                float legA = Mathf.Cos(ph) * 1.4f * limbAmount * rad;
                float armA = Mathf.Cos(ph + Mathf.PI) * 2f * limbAmount * 0.5f * rad;
                float sway = Mathf.Sin(age * 1.34f) * 3f;
                if (legR) legR.localRotation = Quaternion.Euler(-legA, 0f, 0f);
                if (legL) legL.localRotation = Quaternion.Euler(legA, 0f, 0f);
                if (ranged && shootState == 1)
                {
                    // Bow drawn: both arms out in front, pulled back a little as the shot loads.
                    float pull = Mathf.Clamp01(drawT / 0.9f);
                    if (armR) armR.localRotation = Quaternion.Euler(-90f, -4f, 0f);
                    if (armL) armL.localRotation = Quaternion.Euler(-88f + pull * 8f, 16f, 0f);
                }
                else if (armsForward)
                {
                    if (armR) armR.localRotation = Quaternion.Euler(-82f + armA * 0.15f, 0f, sway);
                    if (armL) armL.localRotation = Quaternion.Euler(-82f - armA * 0.15f, 0f, -sway);
                }
                else
                {
                    if (armR) armR.localRotation = Quaternion.Euler(armA, 0f, sway * 0.4f);
                    if (armL) armL.localRotation = Quaternion.Euler(-armA, 0f, -sway * 0.4f);
                }
                if (head && enemy.target != null)
                {
                    Vector3 to = enemy.target.position - enemy.transform.position;
                    float yawTo = Mathf.DeltaAngle(enemy.transform.eulerAngles.y, Mathf.Atan2(to.x, to.z) * rad);
                    head.localRotation = Quaternion.Euler(0f, Mathf.Clamp(yawTo, -50f, 50f), 0f);
                }
            }
            else if ((kind == (int)MobKind.Creeper || kind == (int)MobKind.Pig || kind == (int)MobKind.Cow) && legs != null)
            {
                float a = Mathf.Cos(ph) * 1.4f * limbAmount * rad;
                legs[0].localRotation = Quaternion.Euler(-a, 0f, 0f);
                legs[1].localRotation = Quaternion.Euler(a, 0f, 0f);
                legs[2].localRotation = Quaternion.Euler(a, 0f, 0f);
                legs[3].localRotation = Quaternion.Euler(-a, 0f, 0f);
            }
            else if (kind == (int)MobKind.Bee && body != null)
            {
                // hovers and bobs, wings buzzing
                float flapB = Mathf.Sin(age * 38f);
                body.localPosition = new Vector3(0f, 9f * unit + Mathf.Sin(age * 3f) * 1.2f * unit, 0f);
                if (wingR) wingR.localRotation = Quaternion.Euler(0f, 0f, 20f + flapB * 38f);
                if (wingL) wingL.localRotation = Quaternion.Euler(0f, 0f, -20f - flapB * 38f);
            }
            else if (kind == (int)MobKind.Slime && body != null)
            {
                // Hops like vanilla: an arc while it travels, squashed on landing and stretched in the air.
                bool moving = speed > 0.8f;
                hopT = moving ? (hopT + dt * 1.6f) % 1f : Mathf.MoveTowards(hopT, 0f, dt * 3f);
                float arc = 4f * hopT * (1f - hopT);
                body.localPosition = new Vector3(0f, moving ? arc * unit * 4.5f : 0f, 0f);
                float sy = moving ? (hopT < 0.14f ? Mathf.Lerp(0.7f, 1f, hopT / 0.14f) : 1f + 0.2f * arc)
                                  : 1f + 0.04f * Mathf.Sin(age * 3f);
                float sxz = 1f / Mathf.Sqrt(sy);
                body.localScale = new Vector3(sxz, sy, sxz);
            }            else if (kind == (int)MobKind.Wither)
            {
                // floats and bobs; the side heads sway independently
                transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, transform.localPosition.z);
                if (sideL) sideL.localRotation = Quaternion.Euler(Mathf.Sin(age * 1.7f) * 8f, 18f + Mathf.Sin(age * 1.3f) * 14f, 0f);
                if (sideR) sideR.localRotation = Quaternion.Euler(Mathf.Sin(age * 1.5f + 1f) * 8f, -18f + Mathf.Sin(age * 1.1f) * 14f, 0f);
                if (head) head.localRotation = Quaternion.Euler(Mathf.Sin(age * 2f) * 4f, 0f, 0f);
            }
            else if (kind == (int)MobKind.Guardian)
            {
                if (body) body.localPosition = new Vector3(0f, 8f * unit + Mathf.Sin(age * 1.6f) * 1.2f * unit, 0f);
                if (misc != null)
                    for (int i = 0; i < misc.Length; i++)
                        if (misc[i]) { float s = 1f + Mathf.Sin(age * 3f + i) * 0.22f; misc[i].localScale = new Vector3(1f, s, 1f); }
                if (legs != null)
                    for (int i = 0; i < legs.Length; i++) if (legs[i]) legs[i].localRotation = Quaternion.Euler(0f, Mathf.Sin(age * 3.2f - i * 0.8f) * 22f, 0f);
            }
            else if (kind == (int)MobKind.Dragon)
            {
                float flap = Mathf.Sin(age * 2.3f);
                if (wingR) wingR.localRotation = Quaternion.Euler(0f, 0f, flap * 32f);
                if (wingL) wingL.localRotation = Quaternion.Euler(0f, 0f, -flap * 32f);
                if (body) body.localPosition = new Vector3(0f, 24f * unit + Mathf.Sin(age * 2.3f + 1.2f) * 1.6f * unit, 0f);
                if (head) head.localRotation = Quaternion.Euler(Mathf.Sin(age * 1.1f) * 6f, Mathf.Sin(age * 0.8f) * 10f, 0f);
                if (eye) eye.localRotation = Quaternion.Euler(8f + Mathf.Sin(age * 1.4f) * 6f, 0f, 0f);
                if (misc != null)
                    for (int i = 0; i < misc.Length; i++)
                        if (misc[i]) { var lp = misc[i].localPosition; misc[i].localPosition = new Vector3(Mathf.Sin(age * 1.8f - i * 0.45f) * (2f + i * 0.6f) * unit, lp.y, lp.z); }
            }
            else if (kind == (int)MobKind.Spider && legs != null)
            {
                for (int i = 0; i < legs.Length; i++)
                {
                    float off = (i % 2 == 0 ? 0f : Mathf.PI) + (i / 2) * 0.7f;
                    float lift = Mathf.Max(0f, Mathf.Sin(ph * 2f + off)) * 18f * limbAmount;
                    var e = legs[i].localEulerAngles;
                    legs[i].localRotation = Quaternion.Euler(0f, e.y, (i < 4 ? -18f - lift : 18f + lift));
                }
            }
        }
    }
}









