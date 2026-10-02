using System;
using UnityEngine;

namespace MegabonkSteve
{
    // Pig, cow, bee, iron golem and warden, built from boxes with the vanilla layouts and entity textures.
    // Coordinates are in texture pixels, feet at y = 0, front at +Z.
    internal static class MobModels
    {
        private static Transform P(Transform parent, string name, Vector3 pivot, Vector3 center, Vector3 size, Vector3 uvDims, int u, int v,
                                   Texture2D tex, Material mat, float unit, bool mirror = false)
        {
            return EnemySkins.Part(parent, name, pivot, center, size, uvDims, u, v, tex, mat, unit, mirror);
        }

        private static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }

        // Four legs in the order the walking animation expects: front right, front left, hind right, hind left.
        private static Transform[] FourLegs(Transform root, float x, float height, float zFront, float zHind, Vector3 size, int u, int v, Texture2D tex, Material mat, float unit)
        {
            var legs = new Transform[4];
            legs[0] = P(root, "legFR", V(x, height, zFront), V(0, -height / 2f, 0), size, size, u, v, tex, mat, unit, false);
            legs[1] = P(root, "legFL", V(-x, height, zFront), V(0, -height / 2f, 0), size, size, u, v, tex, mat, unit, true);
            legs[2] = P(root, "legHR", V(x, height, zHind), V(0, -height / 2f, 0), size, size, u, v, tex, mat, unit, false);
            legs[3] = P(root, "legHL", V(-x, height, zHind), V(0, -height / 2f, 0), size, size, u, v, tex, mat, unit, true);
            return legs;
        }

        public static void BuildPig(MobRig rig, Transform root, Texture2D tex, Material mat, float scale)
        {
            float unit = 0.17f * scale;
            rig.unit = unit;
            var body = P(root, "body", V(0, 10, -1), Vector3.zero, V(10, 16, 8), V(10, 16, 8), 28, 8, tex, mat, unit);
            body.localRotation = Quaternion.Euler(90f, 0f, 0f);   // the 16-long axis lies along the animal
            rig.head = P(root, "head", V(0, 12, 9.5f), Vector3.zero, V(8, 8, 8), V(8, 8, 8), 0, 0, tex, mat, unit);
            P(rig.head, "snout", V(0, -1.5f, 4.5f), Vector3.zero, V(4, 3, 1), V(4, 3, 1), 16, 16, tex, mat, unit);
            rig.legs = FourLegs(root, 3f, 6f, 5f, -7f, V(4, 6, 4), 0, 16, tex, mat, unit);
        }

        public static void BuildCow(MobRig rig, Transform root, Texture2D tex, Material mat, float scale)
        {
            float unit = 0.17f * scale;
            rig.unit = unit;
            var body = P(root, "body", V(0, 17, 0), Vector3.zero, V(12, 18, 10), V(12, 18, 10), 18, 4, tex, mat, unit);
            body.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rig.head = P(root, "head", V(0, 20, 12f), Vector3.zero, V(8, 8, 6), V(8, 8, 6), 0, 0, tex, mat, unit);
            P(rig.head, "hornR", V(4.5f, 4.5f, 1.5f), Vector3.zero, V(1, 3, 1), V(1, 3, 1), 22, 0, tex, mat, unit);
            P(rig.head, "hornL", V(-4.5f, 4.5f, 1.5f), Vector3.zero, V(1, 3, 1), V(1, 3, 1), 22, 0, tex, mat, unit, true);
            rig.legs = FourLegs(root, 4f, 12f, 6f, -7f, V(4, 12, 4), 0, 16, tex, mat, unit);
        }

        public static void BuildBee(MobRig rig, Transform root, Texture2D tex, Material mat, float scale)
        {
            float unit = 0.17f * scale;
            rig.unit = unit;
            rig.body = P(root, "body", V(0, 9, 0), Vector3.zero, V(7, 7, 10), V(7, 7, 10), 0, 0, tex, mat, unit);
            P(rig.body, "antennaR", V(1.5f, 3f, 6f), Vector3.zero, V(1, 2, 3), V(1, 2, 3), 2, 3, tex, mat, unit);
            P(rig.body, "antennaL", V(-1.5f, 3f, 6f), Vector3.zero, V(1, 2, 3), V(1, 2, 3), 2, 0, tex, mat, unit);
            P(rig.body, "stinger", V(0, 0, -6f), Vector3.zero, V(1, 1, 2), V(1, 1, 2), 26, 7, tex, mat, unit);
            rig.wingR = P(rig.body, "wingR", V(1.5f, 3.5f, 1.5f), V(4.5f, 0, 3), V(9, 0.4f, 6), V(9, 0, 6), 0, 18, tex, mat, unit);
            rig.wingL = P(rig.body, "wingL", V(-1.5f, 3.5f, 1.5f), V(-4.5f, 0, 3), V(9, 0.4f, 6), V(9, 0, 6), 0, 18, tex, mat, unit, true);
        }

        public static void BuildIronGolem(MobRig rig, Transform root, Texture2D tex, Material mat, float scale)
        {
            float unit = 0.17f * scale;
            rig.unit = unit;
            P(root, "body", V(0, 27, 0.5f), Vector3.zero, V(18, 12, 11), V(18, 12, 11), 0, 40, tex, mat, unit);
            P(root, "waist", V(0, 18.5f, 0f), Vector3.zero, V(10, 6, 7), V(9, 5, 6), 0, 70, tex, mat, unit);
            rig.head = P(root, "head", V(0, 38, 3.5f), Vector3.zero, V(8, 10, 8), V(8, 10, 8), 0, 0, tex, mat, unit);
            P(rig.head, "nose", V(0, -1f, 5f), Vector3.zero, V(2, 4, 2), V(2, 4, 2), 24, 0, tex, mat, unit);
            rig.armR = P(root, "armR", V(11, 31, 0), V(0, -12.5f, 0), V(4, 30, 6), V(4, 30, 6), 60, 21, tex, mat, unit);
            rig.armL = P(root, "armL", V(-11, 31, 0), V(0, -12.5f, 0), V(4, 30, 6), V(4, 30, 6), 60, 58, tex, mat, unit);
            rig.legR = P(root, "legR", V(4, 13, 0), V(0.5f, -5f, 0.5f), V(6, 16, 5), V(6, 16, 5), 37, 0, tex, mat, unit);
            rig.legL = P(root, "legL", V(-5, 13, 0), V(-0.5f, -5f, 0.5f), V(6, 16, 5), V(6, 16, 5), 60, 0, tex, mat, unit, true);
        }

        public static void BuildWarden(MobRig rig, Transform root, Texture2D tex, Material mat, float scale)
        {
            float unit = 0.15f * scale;
            rig.unit = unit;
            P(root, "body", V(0, 23.5f, 0), Vector3.zero, V(18, 21, 11), V(18, 21, 11), 0, 0, tex, mat, unit);
            P(root, "ribR", V(4.5f, 23.5f, 4.2f), Vector3.zero, V(9, 21, 0.4f), V(9, 21, 0), 90, 11, tex, mat, unit);
            P(root, "ribL", V(-4.5f, 23.5f, 4.2f), Vector3.zero, V(9, 21, 0.4f), V(9, 21, 0), 90, 11, tex, mat, unit, true);
            rig.head = P(root, "head", V(0, 42, 0), Vector3.zero, V(16, 16, 10), V(16, 16, 10), 0, 32, tex, mat, unit);
            rig.armR = P(root, "armR", V(13, 34, -1), V(0, -14, 0), V(8, 28, 8), V(8, 28, 8), 44, 50, tex, mat, unit);
            rig.armL = P(root, "armL", V(-13, 34, -1), V(0, -14, 0), V(8, 28, 8), V(8, 28, 8), 0, 58, tex, mat, unit);
            rig.legR = P(root, "legR", V(5.9f, 13, 0), V(0, -6.5f, 0), V(6, 13, 6), V(6, 13, 6), 76, 48, tex, mat, unit);
            rig.legL = P(root, "legL", V(-5.9f, 13, 0), V(0, -6.5f, 0), V(6, 13, 6), V(6, 13, 6), 76, 76, tex, mat, unit);
        }
    }
}
