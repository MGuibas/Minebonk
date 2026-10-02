using System;

namespace MegabonkSteve
{
    // Enchanted books read from chests. Each stacks up to level V and hooks into both the auto-weapons and the held items.
    internal static class Ench
    {
        public const int Sharpness = 0, Power = 1, Efficiency = 2, Multishot = 3, Protection = 4, Knockback = 5, Mending = 6, Luck = 7, Count = 8;
        public const int MaxLevel = 5;

        public static readonly string[] Names = { "Sharpness", "Power", "Efficiency", "Multishot", "Protection", "Knockback", "Mending", "Luck" };
        public static readonly string[] Blurbs =
        {
            "+15% melee and area damage per level",
            "+15% projectile damage per level",
            "-8% cooldowns and faster swings per level",
            "+1 projectile per two levels",
            "-7% damage taken per level",
            "+25% knockback per level",
            "Regenerates health over time",
            "+1 chest item per level, better finds, and more luck everywhere",
        };

        // The Megabonk tome each enchantment rides on: level-up cards for these tomes become the books.
        public static readonly Assets.Scripts._Data.Tomes.ETome[] Tomes =
        {
            Assets.Scripts._Data.Tomes.ETome.Damage, Assets.Scripts._Data.Tomes.ETome.Precision, Assets.Scripts._Data.Tomes.ETome.Cooldown,
            Assets.Scripts._Data.Tomes.ETome.Quantity, Assets.Scripts._Data.Tomes.ETome.Armor, Assets.Scripts._Data.Tomes.ETome.Knockback,
            Assets.Scripts._Data.Tomes.ETome.Regeneration, Assets.Scripts._Data.Tomes.ETome.Luck,
        };

        public static int IndexOf(Assets.Scripts._Data.Tomes.ETome t) { for (int i = 0; i < Tomes.Length; i++) if (Tomes[i] == t) return i; return -1; }

        public static readonly int[] Lvl = new int[Count];

        public static void Reset() { Array.Clear(Lvl, 0, Lvl.Length); }

        public static bool Add(int which)
        {
            if (Lvl[which] >= MaxLevel) return false;
            Lvl[which]++;
            return true;
        }

        public static string Roman(int n) { return n == 1 ? "I" : n == 2 ? "II" : n == 3 ? "III" : n == 4 ? "IV" : "V"; }

        public static float MeleeMul { get { return 1f + 0.15f * Lvl[Sharpness]; } }
        public static float ProjMul { get { return 1f + 0.15f * Lvl[Power]; } }
        public static float CooldownMul { get { return 1f - 0.08f * Lvl[Efficiency]; } }
        public static float SwingSpeedMul { get { return 1f + 0.08f * Lvl[Efficiency]; } }
        public static int ExtraProjectiles { get { return (Lvl[Multishot] + 1) / 2; } }
        public static float DamageTakenMul { get { return 1f - 0.07f * Lvl[Protection]; } }
        public static float KnockMul { get { return 1f + 0.25f * Lvl[Knockback]; } }
        public static float RegenPerSecond { get { return 0.012f * Lvl[Mending]; } }   // fraction of max health

        public static string Summary()
        {
            string s = "";
            for (int i = 0; i < Count; i++)
                if (Lvl[i] > 0) s += (s.Length > 0 ? "  " : "") + Names[i] + " " + Roman(Lvl[i]);
            return s;
        }
    }
}
