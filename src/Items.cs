using System;
using System.Collections.Generic;
using UnityEngine;

namespace MegabonkSteve
{
    public enum ItemKind { Sword, Axe, Mace, Food, Pearl, Potion, Totem, Shield, Bow, Armor, Elytra, Rocket, Arrow, Tome }

    // Minecraft 1.21 numbers: damage in half-hearts, attackSpeed in attacks/second.
    public class ItemDef
    {
        public string name;
        public ItemKind kind;
        public float damage;
        public float attackSpeed;
        public float heal;          // food: hp healed (half-hearts)
        public float absorption;    // food: bonus hp
        public float eatTime;       // seconds
        public bool disablesShield; // axes
        public int maxStack = 1;
        public int armorSlot;       // 0 helmet, 1 chestplate, 2 leggings, 3 boots
        public float armorPoints;
        public float toughness;
        public Texture2D icon;
        public int ench = -1;   // Tome: which enchantment reading it grants
        public string tier;           // armor material (texture name), e.g. iron, gold, chainmail
        public string iconFile;
        public float hunger, sat;     // food: hunger points and saturation restored
        public Texture2D[] pullIcons; // bow draw stages (pulling_0..2)
    }

    internal static class ItemLibrary
    {
        public static ItemDef NetheriteSword, DiamondSword, NetheriteAxe, Mace, GoldenApple, EnchGoldenApple,
            Pearl, StrengthPotion, HealPotion, SpeedPotion, Totem, Shield, Bow, Steak, Elytra, Rocket, Arrow;
        public static ItemDef[] Books;
        // Bare hands: one point of damage, vanilla fist speed.
        public static readonly ItemDef Fist = new ItemDef { name = "Hand", kind = ItemKind.Axe, damage = 1f, attackSpeed = 4f };
        public static readonly List<ItemDef> Armors = new List<ItemDef>(), Swords = new List<ItemDef>(), Axes = new List<ItemDef>(), Foods = new List<ItemDef>();
        public static ItemDef[] NetheriteArmor = new ItemDef[4];
        public static ItemDef[] DiamondArmor = new ItemDef[4];

        private static bool built;

        public static void Build()
        {
            if (built) return;
            built = true;
            NetheriteSword = new ItemDef { name = "Netherite Sword", kind = ItemKind.Sword, damage = 13f, attackSpeed = 1.6f, icon = Icons.Sword(new Color32(70, 62, 70, 255), new Color32(120, 110, 120, 255)) };
            DiamondSword = new ItemDef { name = "Diamond Sword", kind = ItemKind.Sword, damage = 9f, attackSpeed = 1.6f, icon = Icons.Sword(new Color32(60, 220, 210, 255), new Color32(160, 250, 245, 255)) };
            NetheriteAxe = new ItemDef { name = "Netherite Axe", kind = ItemKind.Axe, damage = 16f, attackSpeed = 1.0f, disablesShield = true, icon = Icons.Axe(new Color32(70, 62, 70, 255), new Color32(120, 110, 120, 255)) };
            Mace = new ItemDef { name = "Mace", kind = ItemKind.Mace, damage = 6f, attackSpeed = 0.6f, icon = Icons.Mace() };
            GoldenApple = new ItemDef { name = "Golden Apple", kind = ItemKind.Food, heal = 4f, absorption = 4f, eatTime = 1.6f, icon = Icons.Apple(new Color32(250, 210, 60, 255), new Color32(255, 245, 160, 255)) };
            EnchGoldenApple = new ItemDef { name = "Enchanted Golden Apple", kind = ItemKind.Food, heal = 8f, absorption = 16f, eatTime = 1.6f, icon = Icons.Apple(new Color32(250, 170, 230, 255), new Color32(255, 235, 255, 255)) };
            Steak = new ItemDef { name = "Steak", kind = ItemKind.Food, heal = 0f, eatTime = 1.6f, icon = Icons.Steak() };
            Pearl = new ItemDef { name = "Ender Pearl", kind = ItemKind.Pearl, icon = Icons.Pearl() };
            StrengthPotion = new ItemDef { name = "Potion of Strength", kind = ItemKind.Potion, icon = Icons.Potion(new Color32(150, 40, 40, 255)) };
            HealPotion = new ItemDef { name = "Potion of Healing", kind = ItemKind.Potion, heal = 8f, icon = Icons.Potion(new Color32(235, 60, 100, 255)) };
            SpeedPotion = new ItemDef { name = "Potion of Swiftness", kind = ItemKind.Potion, icon = Icons.Potion(new Color32(110, 210, 240, 255)) };
            Totem = new ItemDef { name = "Totem of Undying", kind = ItemKind.Totem, icon = Icons.Totem() };
            Shield = new ItemDef { name = "Shield", kind = ItemKind.Shield, icon = Icons.Shield() };
            Bow = new ItemDef { name = "Bow", kind = ItemKind.Bow, damage = 9f, icon = Icons.Bow() };
            GoldenApple.maxStack = 64; EnchGoldenApple.maxStack = 64; Steak.maxStack = 64; Pearl.maxStack = 16;
            Elytra = new ItemDef { name = "Elytra", kind = ItemKind.Elytra, armorSlot = 1, icon = Icons.Generic(new Color32(150, 140, 170, 255)) };
            Rocket = new ItemDef { name = "Firework Rocket", kind = ItemKind.Rocket, maxStack = 64, icon = Icons.Generic(new Color32(200, 60, 60, 255)) };
            Arrow = new ItemDef { name = "Arrow", kind = ItemKind.Arrow, maxStack = 64, icon = Icons.Generic(new Color32(190, 190, 190, 255)) };
            string[] names = { "Helmet", "Chestplate", "Leggings", "Boots" };
            float[] pts = { 3f, 8f, 6f, 3f };
            for (int i = 0; i < 4; i++)
            {
                NetheriteArmor[i] = new ItemDef { name = "Netherite " + names[i], kind = ItemKind.Armor, armorSlot = i, armorPoints = pts[i], toughness = 3f, icon = Icons.Generic(new Color32(70, 62, 70, 255)) };
                DiamondArmor[i] = new ItemDef { name = "Diamond " + names[i], kind = ItemKind.Armor, armorSlot = i, armorPoints = pts[i], toughness = 2f, icon = Icons.Generic(new Color32(60, 220, 210, 255)) };
            }
            Books = new ItemDef[Ench.Count];
            for (int i = 0; i < Ench.Count; i++)
                Books[i] = new ItemDef { name = "Enchanted Book: " + Ench.Names[i], kind = ItemKind.Tome, ench = i, maxStack = 16, icon = Icons.Generic(new Color32(140, 90, 200, 255)) };
            BuildExtras();
            ApplyRealIcons();
        }

        // Every armor material, sword and axe tier, and more food: what the chests can hold.
        private static void BuildExtras()
        {
            string[] pieces = { "Helmet", "Chestplate", "Leggings", "Boots" };
            string[] files = { "helmet", "chestplate", "leggings", "boots" };
            // material, display name, icon prefix, points per piece, toughness
            var mats = new (string, string, string, float[], float)[] {
                ("chainmail", "Chainmail", "chainmail", new[] { 2f, 5f, 4f, 1f }, 0f),
                ("iron", "Iron", "iron", new[] { 2f, 6f, 5f, 2f }, 0f),
                ("gold", "Golden", "golden", new[] { 2f, 5f, 3f, 1f }, 0f),
            };
            foreach (var m in mats)
                for (int i = 0; i < 4; i++)
                    Armors.Add(new ItemDef { name = m.Item2 + " " + pieces[i], kind = ItemKind.Armor, armorSlot = i, armorPoints = m.Item4[i], toughness = m.Item5, tier = m.Item1,
                        icon = Icons.Generic(new Color32(190, 190, 190, 255)), iconFile = m.Item3 + "_" + files[i] });
            for (int i = 0; i < 4; i++)
            {
                NetheriteArmor[i].tier = "netherite"; DiamondArmor[i].tier = "diamond";
                Armors.Add(DiamondArmor[i]); Armors.Add(NetheriteArmor[i]);
            }

            var tiers = new (string, string, float, float, float)[] {   // name, icon prefix, sword dmg, axe dmg, axe speed
                ("Wooden", "wooden", 3f, 5f, 0.8f), ("Stone", "stone", 4.5f, 7f, 0.8f), ("Iron", "iron", 6.5f, 9f, 0.9f),
                ("Golden", "golden", 4f, 5f, 1.0f), ("Diamond", "diamond", 9f, 12f, 1.0f) };
            foreach (var t in tiers)
            {
                var sw = t.Item1 == "Diamond" ? DiamondSword : new ItemDef { name = t.Item1 + " Sword", kind = ItemKind.Sword, damage = t.Item3, attackSpeed = 1.6f, icon = Icons.Sword(new Color32(150, 150, 150, 255), new Color32(200, 200, 200, 255)), iconFile = t.Item2 + "_sword" };
                Swords.Add(sw);
                Axes.Add(new ItemDef { name = t.Item1 + " Axe", kind = ItemKind.Axe, damage = t.Item4, attackSpeed = t.Item5, disablesShield = true, icon = Icons.Axe(new Color32(150, 150, 150, 255), new Color32(200, 200, 200, 255)), iconFile = t.Item2 + "_axe" });
            }
            Swords.Add(NetheriteSword); Axes.Add(NetheriteAxe);

            Foods.Add(new ItemDef { name = "Cooked Porkchop", kind = ItemKind.Food, eatTime = 1.6f, maxStack = 64, hunger = 8f, sat = 12.8f, icon = Icons.Steak(), iconFile = "cooked_porkchop" });
            Foods.Add(new ItemDef { name = "Bread", kind = ItemKind.Food, eatTime = 1.6f, maxStack = 64, hunger = 5f, sat = 6f, icon = Icons.Steak(), iconFile = "bread" });
            Foods.Add(new ItemDef { name = "Cooked Chicken", kind = ItemKind.Food, eatTime = 1.6f, maxStack = 64, hunger = 6f, sat = 7.2f, icon = Icons.Steak(), iconFile = "cooked_chicken" });
            Foods.Add(new ItemDef { name = "Golden Carrot", kind = ItemKind.Food, eatTime = 1.6f, maxStack = 64, hunger = 6f, sat = 14.4f, icon = Icons.Steak(), iconFile = "golden_carrot" });
        }

        private static void ExtraIcons()
        {
            foreach (var l in new[] { Armors, Swords, Axes, Foods })
                foreach (var d in l)
                    if (!string.IsNullOrEmpty(d.iconFile)) Set(d, d.iconFile);
        }

        // The shield has no flat sprite in the game files, so its icon is its front face taken from the entity texture.
        private static Texture2D ShieldIcon()
        {
            var src = FirstPerson.RealShield();
            if (src == null || src.width != 64) return null;
            var sp = src.GetPixels32();
            var px = new Color32[16 * 16];
            for (int iy = 0; iy < 16; iy++)
                for (int ix = 2; ix < 14; ix++)
                {
                    int u = 1 + (ix - 2);
                    int v = 1 + (int)((15 - iy) * 22f / 16f);
                    px[iy * 16 + ix] = sp[(63 - v) * 64 + u];
                }
            var t = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            t.SetPixels32(px); t.Apply(); t.filterMode = FilterMode.Point;
            return Keep.It(t);
        }

        // Swap the built-in placeholder icons for the real Minecraft item textures when the install is found.
        private static void ApplyRealIcons()
        {
            if (!McAssets.Ready) return;
            Set(NetheriteSword, "netherite_sword"); Set(DiamondSword, "diamond_sword"); Set(NetheriteAxe, "netherite_axe");
            if (Books != null) foreach (var b in Books) Set(b, "enchanted_book");
            Set(Mace, "mace"); Set(GoldenApple, "golden_apple"); Set(EnchGoldenApple, "enchanted_golden_apple");
            Set(Steak, "cooked_beef"); Set(Pearl, "ender_pearl"); Set(Totem, "totem_of_undying"); Set(Bow, "bow");
            Set(Elytra, "elytra"); Set(Rocket, "firework_rocket"); Set(Arrow, "arrow");
            Bow.pullIcons = new[] { McAssets.Tex("item/bow_pulling_0"), McAssets.Tex("item/bow_pulling_1"), McAssets.Tex("item/bow_pulling_2") };
            string[] parts = { "helmet", "chestplate", "leggings", "boots" };
            for (int i = 0; i < 4; i++) { Set(NetheriteArmor[i], "netherite_" + parts[i]); Set(DiamondArmor[i], "diamond_" + parts[i]); }
            var shIcon = ShieldIcon(); if (shIcon != null) Shield.icon = shIcon;
            ExtraIcons();
            Potion(StrengthPotion, new Color32(147, 36, 35, 255));
            Potion(HealPotion, new Color32(248, 36, 35, 255));
            Potion(SpeedPotion, new Color32(124, 175, 198, 255));
        }

        private static void Set(ItemDef d, string file)
        {
            var t = McAssets.Tex("item/" + file);
            if (t != null) d.icon = t; else Plugin.Logger.LogWarning("missing item texture " + file);
        }

        private static void Potion(ItemDef d, Color32 tint)
        {
            var bottle = McAssets.Tex("item/potion");
            var overlay = McAssets.Tex("item/potion_overlay");
            if (bottle == null) return;
            if (overlay == null) { d.icon = bottle; return; }
            var b = bottle.GetPixels32(); var o = overlay.GetPixels32();
            var res = new Color32[b.Length];
            for (int i = 0; i < b.Length; i++)
            {
                res[i] = b[i];
                if (o[i].a > 0)
                    res[i] = new Color32((byte)(o[i].r * tint.r / 255), (byte)(o[i].g * tint.g / 255), (byte)(o[i].b * tint.b / 255), 255);
            }
            var t = new Texture2D(bottle.width, bottle.height, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp;
            t.SetPixels32(res); t.Apply();
            Keep.It(t);
            d.icon = t;
        }
    }

    // 16x16 pixel-art icons painted in code (no external assets).
    internal static class Icons
    {
        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static readonly Color32 Wood = new Color32(130, 90, 45, 255);
        private static readonly Color32 WoodDark = new Color32(90, 60, 30, 255);
        private static readonly Color32 Outline = new Color32(20, 18, 24, 255);

        private static Color32[] Blank() { var p = new Color32[256]; for (int i = 0; i < 256; i++) p[i] = Clear; return p; }
        private static void Px(Color32[] p, int x, int y, Color32 c) { if (x >= 0 && x < 16 && y >= 0 && y < 16) p[(15 - y) * 16 + x] = c; }
        private static Texture2D Make(Color32[] p)
        {
            var t = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            t.SetPixels32(p);
            t.Apply();
            Keep.It(t);
            return t;
        }

        // Soft outline around every opaque pixel.
        private static void AddOutline(Color32[] p)
        {
            var src = (Color32[])p.Clone();
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    if (src[(15 - y) * 16 + x].a != 0) continue;
                    bool near = false;
                    for (int dy = -1; dy <= 1 && !near; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || nx > 15 || ny < 0 || ny > 15) continue;
                            if (src[(15 - ny) * 16 + nx].a != 0) { near = true; break; }
                        }
                    if (near) p[(15 - y) * 16 + x] = Outline;
                }
        }

        public static Texture2D Sword(Color32 blade, Color32 shine)
        {
            var p = Blank();
            for (int i = 0; i < 10; i++) { Px(p, 4 + i, 4 + i, blade); Px(p, 5 + i, 4 + i, shine); }
            Px(p, 14, 14, shine);
            for (int i = 0; i < 4; i++) { Px(p, 3 + i, 5 - i + 0, WoodDark); } // crossguard
            Px(p, 2, 6, WoodDark); Px(p, 6, 2, WoodDark);
            Px(p, 3, 3, Wood); Px(p, 2, 2, Wood); Px(p, 1, 1, WoodDark);
            AddOutline(p);
            return Make(p);
        }

        public static Texture2D Axe(Color32 head, Color32 shine)
        {
            var p = Blank();
            for (int i = 0; i < 11; i++) { Px(p, 2 + i, 2 + i, Wood); }
            for (int i = 0; i < 4; i++) for (int j = 0; j < 5; j++) Px(p, 7 + i, 8 + j - (i > 1 ? 1 : 0), head);
            for (int j = 0; j < 4; j++) Px(p, 11, 9 + j, shine);
            AddOutline(p);
            return Make(p);
        }

        public static Texture2D Mace()
        {
            var p = Blank();
            for (int i = 0; i < 9; i++) Px(p, 2 + i, 2 + i, Wood);
            var g = new Color32(120, 120, 130, 255); var d = new Color32(70, 70, 80, 255);
            for (int y = 8; y < 14; y++) for (int x = 8; x < 14; x++) Px(p, x, y, ((x + y) % 2 == 0) ? g : d);
            AddOutline(p);
            return Make(p);
        }

        public static Texture2D Apple(Color32 body, Color32 shine)
        {
            var p = Blank();
            for (int y = 3; y < 13; y++)
                for (int x = 3; x < 13; x++)
                {
                    float dx = x - 7.5f, dy = y - 7.5f;
                    if (dx * dx + dy * dy < 20f) Px(p, x, y, body);
                }
            Px(p, 5, 10, shine); Px(p, 5, 9, shine); Px(p, 6, 11, shine);
            Px(p, 8, 13, new Color32(60, 150, 50, 255)); Px(p, 9, 14, new Color32(60, 150, 50, 255));
            AddOutline(p);
            return Make(p);
        }

        public static Texture2D Steak()
        {
            var p = Blank();
            for (int y = 3; y < 13; y++)
                for (int x = 2; x < 14; x++)
                {
                    float dx = (x - 8f) / 6f, dy = (y - 8f) / 4.5f;
                    if (dx * dx + dy * dy < 1f) Px(p, x, y, (x + y) % 5 == 0 ? new Color32(120, 60, 30, 255) : new Color32(165, 85, 45, 255));
                }
            Px(p, 6, 8, new Color32(240, 220, 200, 255)); Px(p, 7, 8, new Color32(240, 220, 200, 255));
            AddOutline(p);
            return Make(p);
        }

        public static Texture2D Pearl()
        {
            var p = Blank();
            for (int y = 3; y < 13; y++)
                for (int x = 3; x < 13; x++)
                {
                    float dx = x - 7.5f, dy = y - 7.5f; float r = dx * dx + dy * dy;
                    if (r < 22f) Px(p, x, y, r < 10f ? new Color32(20, 120, 100, 255) : new Color32(30, 70, 70, 255));
                }
            Px(p, 6, 9, new Color32(140, 240, 200, 255)); Px(p, 7, 9, new Color32(140, 240, 200, 255)); Px(p, 6, 8, new Color32(140, 240, 200, 255));
            AddOutline(p);
            return Make(p);
        }

        public static Texture2D Potion(Color32 liquid)
        {
            var p = Blank();
            for (int x = 6; x < 10; x++) { Px(p, x, 14, WoodDark); Px(p, x, 13, new Color32(210, 220, 230, 255)); Px(p, x, 12, new Color32(210, 220, 230, 255)); }
            for (int y = 3; y < 12; y++)
                for (int x = 3; x < 13; x++)
                {
                    float dx = x - 7.5f, dy = y - 6.5f;
                    if (dx * dx + dy * dy < 21f) Px(p, x, y, dy < 0.5f ? liquid : new Color32(215, 225, 235, 255));
                }
            Px(p, 5, 5, new Color32(255, 255, 255, 255)); Px(p, 5, 6, new Color32(255, 255, 255, 255));
            AddOutline(p);
            return Make(p);
        }

        // Placeholder for items whose real texture isn't found: a plain tinted rounded square.
        public static Texture2D Generic(Color32 c)
        {
            var p = Blank();
            for (int y = 2; y < 14; y++)
                for (int x = 2; x < 14; x++)
                    if (!((x == 2 || x == 13) && (y == 2 || y == 13))) Px(p, x, y, c);
            AddOutline(p);
            return Make(p);
        }

        public static Texture2D Totem()
        {
            var p = Blank();
            var y1 = new Color32(235, 200, 60, 255); var g = new Color32(70, 160, 70, 255); var d = new Color32(40, 90, 40, 255);
            for (int y = 2; y < 14; y++) for (int x = 4; x < 12; x++) Px(p, x, y, y > 9 ? y1 : g);
            for (int x = 1; x < 15; x++) { Px(p, x, 9, y1); Px(p, x, 8, y1); }
            Px(p, 6, 11, d); Px(p, 9, 11, d); Px(p, 7, 5, d); Px(p, 8, 5, d);
            AddOutline(p);
            return Make(p);
        }

        public static Texture2D Shield()
        {
            var p = Blank();
            var w = new Color32(120, 80, 40, 255); var m = new Color32(190, 190, 195, 255); var b = new Color32(60, 90, 160, 255);
            for (int y = 2; y < 14; y++)
                for (int x = 3; x < 13; x++)
                {
                    bool edge = x == 3 || x == 12 || y == 13 || y == 2;
                    if (y < 5 && (x < 5 || x > 10)) continue;
                    Px(p, x, y, edge ? m : (x < 8 ? w : b));
                }
            AddOutline(p);
            return Make(p);
        }

        public static Texture2D Bow()
        {
            var p = Blank();
            for (int i = 0; i < 12; i++) { int x = 3 + (i < 6 ? i : 11 - i) / 2; Px(p, x + 2, 2 + i, Wood); }
            for (int i = 0; i < 12; i++) Px(p, 12 - i / 1 * 0, 2 + i, new Color32(230, 230, 230, 255)); // string
            AddOutline(p);
            return Make(p);
        }

        // 9x9 heart, hunger drumstick and armor are drawn via Pixel9.
        public static Texture2D Heart(Color32 fill, Color32 edge)
        {
            string[] m = {
                ".XX...XX.",
                "XffX.XffX",
                "XffffXfff",
                "XfffffffX",
                "XfffffffX",
                ".XfffffX.",
                "..XfffX..",
                "...XfX...",
                "....X....",
            };
            return Pixel9(m, fill, edge);
        }

        public static Texture2D Drumstick(Color32 fill, Color32 edge)
        {
            string[] m = {
                "....XXX..",
                "...XfffX.",
                "..XffffX.",
                "..XffffX.",
                "...XfffX.",
                "..XXXXX..",
                ".XbbX....",
                "XbbX.....",
                ".XX......",
            };
            return Pixel9(m, fill, edge);
        }

        private static Texture2D Pixel9(string[] m, Color32 fill, Color32 edge)
        {
            var t = new Texture2D(9, 9, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            var p = new Color32[81];
            for (int y = 0; y < 9; y++)
                for (int x = 0; x < 9; x++)
                {
                    char c = m[y][x];
                    Color32 col = c == 'X' ? edge : (c == 'f' ? fill : (c == 'b' ? new Color32(240, 235, 220, 255) : Clear));
                    p[(8 - y) * 9 + x] = col;
                }
            t.SetPixels32(p);
            t.Apply();
            Keep.It(t);
            return t;
        }
    }
}


