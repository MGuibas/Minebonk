using System;
using System.Collections.Generic;
using Actors.Enemies;
using Assets.Scripts.Actors.Enemies;
using UnityEngine;

namespace MegabonkSteve
{
    // Minecraft bosses for Megabonk's boss enemies: the Ender Dragon (final boss), the Wither (stage bosses) and the
    // Elder Guardian (minibosses). Models are built from the vanilla entity boxes and textures.
    internal static class BossSkins
    {
        public static MobSpec ForFlags(Enemy e)
        {
            int f;
            try { f = (int)e.enemyFlag; } catch { return null; }
            if ((f & (int)EEnemyFlag.FinalBoss) != 0) return new MobSpec { kind = MobKind.Dragon, tex = "entity/enderdragon/dragon", boss = true };
            if ((f & (int)EEnemyFlag.StageBoss) != 0) return new MobSpec { kind = MobKind.Wither, tex = "entity/wither/wither", boss = true };
            if ((f & (int)EEnemyFlag.SummonerMiniboss) != 0) return new MobSpec { kind = MobKind.Warden, tex = "entity/warden/warden", boss = true, armsForward = false };
            if ((f & (int)EEnemyFlag.Boss) != 0)
                return new MobSpec { kind = MobKind.Guardian, tex = "entity/guardian/guardian_elder", boss = true };
            return null;
        }

        private static Transform P(Transform parent, string name, Vector3 pivot, Vector3 center, Vector3 size, Vector3 uvDims, int u, int v,
                                   Texture2D tex, Material mat, float unit, bool mirror = false)
        {
            return EnemySkins.Part(parent, name, pivot, center, size, uvDims, u, v, tex, mat, unit, mirror);
        }

        private static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }

        // ------------------------------------------------------------------ Wither (64x64)
        public static void BuildWither(MobRig rig, Transform root, Texture2D tex, Material mat, float height)
        {
            float unit = height / 27f;   // model height = collider height, so shots hit what you see
            rig.unit = unit;
            // Coordinates: pixels, feet at y=0, front is +Z.
            P(root, "shoulders", V(0, 18.5f, -1f), Vector3.zero, V(20, 3, 3), V(20, 3, 3), 0, 16, tex, mat, unit);
            P(root, "ribcage", V(-0.5f, 11.1f, -1f), Vector3.zero, V(3, 10, 3), V(3, 10, 3), 0, 22, tex, mat, unit);
            for (int i = 0; i < 3; i++)
                P(root, "rib" + i, V(-0.5f, 13.6f - i * 2.5f, -1f), Vector3.zero, V(11, 2, 2), V(11, 2, 2), 24, 22, tex, mat, unit);
            P(root, "tail", V(-0.5f, 3.1f, -1f), Vector3.zero, V(3, 6, 3), V(3, 6, 3), 12, 22, tex, mat, unit);
            rig.head = P(root, "headC", V(0, 23, 0), Vector3.zero, V(8, 8, 8), V(8, 8, 8), 0, 0, tex, mat, unit);
            rig.sideL = P(root, "headL", V(-9, 19, 0), Vector3.zero, V(6, 6, 6), V(6, 6, 6), 32, 0, tex, mat, unit);
            rig.sideR = P(root, "headR", V(9, 19, 0), Vector3.zero, V(6, 6, 6), V(6, 6, 6), 32, 0, tex, mat, unit);
        }

        // ------------------------------------------------------------------ Elder Guardian (64x64)
        public static void BuildGuardian(MobRig rig, Transform root, Texture2D tex, Material mat, float height)
        {
            float unit = height / 16f;
            rig.unit = unit;
            var body = new GameObject("body").transform;
            body.SetParent(root, false);
            body.localPosition = V(0, 8f * unit, 0);
            rig.body = body;
            // Vanilla's head is a 12x12x16 core plus four 2-thick plates; the core's sides are blank on the texture.
            P(body, "head", Vector3.zero, Vector3.zero, V(12, 12, 16), V(12, 12, 16), 0, 0, tex, mat, unit);
            P(body, "plateL", V(-7, 0, 0), Vector3.zero, V(2, 12, 12), V(2, 12, 12), 0, 28, tex, mat, unit);
            P(body, "plateR", V(7, 0, 0), Vector3.zero, V(2, 12, 12), V(2, 12, 12), 0, 28, tex, mat, unit, true);
            P(body, "plateT", V(0, 7, 0), Vector3.zero, V(12, 2, 12), V(12, 2, 12), 16, 40, tex, mat, unit);
            P(body, "plateB", V(0, -7, 0), Vector3.zero, V(12, 2, 12), V(12, 2, 12), 16, 40, tex, mat, unit);
            rig.eye = P(body, "eye", V(0, 0, 8.4f), Vector3.zero, V(2, 2, 1), V(2, 2, 1), 8, 0, tex, mat, unit);
            // twelve spines: four up, four down, two on each side
            var spikes = new List<Transform>();
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? 3f : -3f), z = (i < 2 ? 4f : -4f);
                spikes.Add(Spine(body, V(x, 6, z), 0f, 0f, tex, mat, unit));
                spikes.Add(Spine(body, V(x, -6, z), 180f, 0f, tex, mat, unit));
            }
            spikes.Add(Spine(body, V(6, 0, 4), 0f, -90f, tex, mat, unit));
            spikes.Add(Spine(body, V(6, 0, -4), 0f, -90f, tex, mat, unit));
            spikes.Add(Spine(body, V(-6, 0, 4), 0f, 90f, tex, mat, unit));
            spikes.Add(Spine(body, V(-6, 0, -4), 0f, 90f, tex, mat, unit));
            rig.misc = spikes.ToArray();
            // tail
            rig.legs = new Transform[3];
            rig.legs[0] = P(body, "tail1", V(0, 0, -8), V(0, 0, -4), V(4, 4, 8), V(4, 4, 8), 40, 0, tex, mat, unit);
            rig.legs[1] = P(rig.legs[0], "tail2", V(0, 0, -8), V(0, 0, -3.5f), V(3, 3, 7), V(3, 3, 7), 0, 54, tex, mat, unit);
            rig.legs[2] = P(rig.legs[1], "tail3", V(0, 0, -7), V(0, 0, -3), V(2, 2, 6), V(2, 2, 6), 41, 32, tex, mat, unit);
        }

        private static Transform Spine(Transform parent, Vector3 pos, float pitch, float roll, Texture2D tex, Material mat, float unit)
        {
            var piv = P(parent, "spike", pos, V(0, 4.5f, 0), V(2, 9, 2), V(2, 9, 2), 0, 0, tex, mat, unit);
            piv.localRotation = Quaternion.Euler(pitch, 0f, roll);
            return piv;
        }

        // ------------------------------------------------------------------ Ender Dragon (256x256, simplified)
        public static void BuildDragon(MobRig rig, Transform root, Texture2D tex, Material mat, float height)
        {
            float unit = height / 50f;
            rig.unit = unit;
            var body = new GameObject("body").transform;
            body.SetParent(root, false);
            body.localPosition = V(0, 24f * unit, 0);
            rig.body = body;

            P(body, "torso", V(0, 0, -16), Vector3.zero, V(24, 24, 64), V(24, 24, 64), 0, 0, tex, mat, unit);
            // neck rising to the head, five segments
            Transform prev = body;
            var segs = new List<Transform>();
            for (int i = 0; i < 5; i++)
            {
                var n = P(body, "neck" + i, V(0, 4 + i * 3.4f, 18 + i * 8.5f), Vector3.zero, V(10, 10, 10), V(10, 10, 10), 192, 104, tex, mat, unit);
                segs.Add(n);
            }
            rig.head = P(body, "head", V(0, 22, 62), Vector3.zero, V(16, 16, 16), V(16, 16, 16), 112, 30, tex, mat, unit);
            P(rig.head, "snout", V(0, -2, 14), Vector3.zero, V(12, 5, 16), V(12, 5, 16), 176, 44, tex, mat, unit);
            rig.eye = P(rig.head, "jaw", V(0, -7, 12), Vector3.zero, V(12, 4, 16), V(12, 4, 16), 176, 65, tex, mat, unit);

            // tail: twelve segments trailing behind and slightly down
            var tail = new List<Transform>();
            for (int i = 0; i < 12; i++)
                tail.Add(P(body, "tail" + i, V(0, -i * 0.5f, -52 - i * 9f), Vector3.zero, V(10, 10, 10), V(10, 10, 10), 192, 104, tex, mat, unit));
            rig.misc = tail.ToArray();

            // wings: a bone and its membrane, flapping around the shoulder
            rig.wingR = new GameObject("wingR").transform; rig.wingR.SetParent(body, false); rig.wingR.localPosition = V(12, 10, 6) * unit;
            rig.wingL = new GameObject("wingL").transform; rig.wingL.SetParent(body, false); rig.wingL.localPosition = V(-12, 10, 6) * unit;
            for (int s = 0; s < 2; s++)
            {
                var w = s == 0 ? rig.wingR : rig.wingL;
                int side = s == 0 ? 1 : -1;
                bool mir = s == 1;
                P(w, "bone", Vector3.zero, V(side * 28, 0, 0), V(56, 8, 8), V(56, 8, 8), 112, 88, tex, mat, unit, mir);
                P(w, "tip", V(side * 56, 0, 0), V(side * 28, 0, 0), V(56, 4, 4), V(56, 4, 4), 112, 136, tex, mat, unit, mir);
                P(w, "membrane", Vector3.zero, V(side * 28, -3.5f, -30), V(56, 1, 56), V(56, 0, 56), -56, 88, tex, mat, unit, mir);
            }

            // legs
            P(body, "legFR", V(16, -8, 20), V(0, -10, 0), V(8, 24, 8), V(8, 24, 8), 112, 104, tex, mat, unit);
            P(body, "legFL", V(-16, -8, 20), V(0, -10, 0), V(8, 24, 8), V(8, 24, 8), 112, 104, tex, mat, unit, true);
            P(body, "legRR", V(16, -8, -22), V(0, -10, 0), V(8, 24, 8), V(8, 24, 8), 0, 0, tex, mat, unit);
            P(body, "legRL", V(-16, -8, -22), V(0, -10, 0), V(8, 24, 8), V(8, 24, 8), 0, 0, tex, mat, unit, true);
        }
    }
}
