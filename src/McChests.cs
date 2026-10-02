using System;
using System.Collections.Generic;
using Assets.Scripts.Inventory__Items__Pickups.Chests;
using Assets.Scripts.Inventory__Items__Pickups.Interactables;
using Assets.Scripts.Actors.Player;
using UnityEngine;

namespace MegabonkSteve
{
    // Megabonk's chests become Minecraft chests: the vanilla model with an opening lid, opened with a right click,
    // and filled with Minecraft items that go straight into the inventory (no Megabonk item roll).
    internal static class McChests
    {
        private class Loot { public ItemDef item; public int min, max; public float weight; }

        private static List<Loot> table;
        // wooden, stone, iron, golden, diamond, netherite: the good ones are rare enough to feel like finds
        private static readonly float[] ToolWeights = { 2.0f, 1.6f, 1.2f, 0.95f, 0.5f, 0.14f };

        private static void BuildTable()
        {
            if (table != null) return;
            ItemLibrary.Build();
            table = new List<Loot>
            {
                new Loot { item = ItemLibrary.Arrow, min = 8, max = 24, weight = 12f },
                new Loot { item = ItemLibrary.Steak, min = 4, max = 10, weight = 11f },
                new Loot { item = ItemLibrary.GoldenApple, min = 1, max = 2, weight = 8f },
                new Loot { item = ItemLibrary.Pearl, min = 1, max = 3, weight = 6f },
                new Loot { item = ItemLibrary.Rocket, min = 4, max = 12, weight = 6f },
                new Loot { item = ItemLibrary.StrengthPotion, min = 1, max = 1, weight = 4f },
                new Loot { item = ItemLibrary.HealPotion, min = 1, max = 1, weight = 5f },
                new Loot { item = ItemLibrary.Totem, min = 1, max = 1, weight = 1.6f },
                new Loot { item = ItemLibrary.Elytra, min = 1, max = 1, weight = 1.6f },
                new Loot { item = ItemLibrary.Shield, min = 1, max = 1, weight = 1f },
                new Loot { item = ItemLibrary.Bow, min = 1, max = 1, weight = 1.2f },
            };
            // Every armor and tool tier; the better the material, the rarer.
            foreach (var a in ItemLibrary.Armors)
                table.Add(new Loot { item = a, min = 1, max = 1, weight = a.tier == "netherite" ? 0.2f : a.tier == "diamond" ? 0.55f : a.tier == "iron" ? 1.5f : 1.1f });
            for (int i = 0; i < ItemLibrary.Swords.Count; i++)
                table.Add(new Loot { item = ItemLibrary.Swords[i], min = 1, max = 1, weight = ToolWeights[i] });
            for (int i = 0; i < ItemLibrary.Axes.Count; i++)
                table.Add(new Loot { item = ItemLibrary.Axes[i], min = 1, max = 1, weight = ToolWeights[i] * 0.7f });
            table.Add(new Loot { item = ItemLibrary.Mace, min = 1, max = 1, weight = 0.6f });
            table.Add(new Loot { item = ItemLibrary.EnchGoldenApple, min = 1, max = 1, weight = 0.75f });
            foreach (var f in ItemLibrary.Foods) table.Add(new Loot { item = f, min = 3, max = 8, weight = 5f });
        }

        private static Loot RollOnce()
        {
            float total = 0f;
            foreach (var l in table) total += l.weight;
            float r = UnityEngine.Random.value * total;
            foreach (var l in table) { r -= l.weight; if (r <= 0f) return l; }
            return table[0];
        }

        // Luck: each level gives a 12% chance to roll twice and keep the rarer find.
        private static Loot Roll(float extraReroll = 0f)
        {
            var a = RollOnce();
            if (UnityEngine.Random.value < 0.12f * Ench.Lvl[Ench.Luck] + extraReroll) { var b = RollOnce(); if (b.weight < a.weight) return b; }
            return a;
        }

        // ------------------------------------------------------------------ model

        private static string TexFor(EChest t)
        {
            switch (t)
            {
                case EChest.Corrupt: return "entity/chest/trapped";
                case EChest.Free: case EChest.FreeCrypt: return "entity/chest/ender";
                case EChest.Ghost: return "entity/chest/copper_oxidized";
                default: return "entity/chest/normal";
            }
        }

        // Chests dropped by enemies keep Megabonk's own behaviour and its items.
        public static bool InDrop;
        public static readonly HashSet<int> Drops = new HashSet<int>();
        public static bool IsDrop(InteractableChest c) { return false; }   // every chest is a Minecraft chest: the game's own chest window crashes with the Minecraft item pool

        // 10, 20, 37, 50, then 15 more each time until the tenth chest, then 10% more each time.
        public static int PriceFor(int bought)
        {
            int[] start = { 10, 20, 37, 50 };
            if (bought < 0) bought = 0;
            if (bought < start.Length) return start[bought];
            int p = 50 + 15 * (Mathf.Min(bought, 9) - 3);                  // 65, 80, ... 140 up to the tenth chest
            if (bought > 9) p = Mathf.RoundToInt(140f * Mathf.Pow(1.1f, bought - 9));   // then 10% more each time
            return p;
        }

        public static void Skin(InteractableChest c)
        {
            if (c == null || c.transform.Find("MC_Chest") != null || IsDrop(c)) return;
            var inst = MinecraftMode.Instance;
            var baseMat = inst != null && inst.rig != null ? inst.rig.itemMat : null;
            if (baseMat == null) return;
            var tex = McAssets.Tex(TexFor(c.chestType));
            if (tex == null) return;
            var mat = Keep.It(new Material(baseMat));
            mat.mainTexture = tex;
            foreach (var n in new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo" })
                if (mat.HasProperty(n)) mat.SetTexture(n, tex);

            // Size and ground level from the model being replaced.
            float unit = 0.2f, bottom = 0f;
            var rends = c.GetComponentsInChildren<Renderer>();
            Bounds b = default(Bounds); bool have = false;
            foreach (var r in rends)
            {
                if (r == null) continue;
                if (!have) { b = r.bounds; have = true; } else b.Encapsulate(r.bounds);
            }
            if (have)
            {
                float w = Mathf.Max(b.size.x, b.size.z);
                if (w > 0.5f) unit = Mathf.Clamp(w / 14f, 0.12f, 0.3f) * 0.75f;   // a bit smaller than the original chest
                bottom = b.min.y - c.transform.position.y;
            }
            foreach (var r in rends) if (r != null) r.enabled = false;

            // The original model is rotated (Blender axes), so place ours in world space, upright, facing like the chest.
            var root = new GameObject("MC_Chest");
            root.transform.SetParent(c.transform, true);
            root.transform.position = new Vector3(c.transform.position.x, have ? b.min.y : c.transform.position.y, c.transform.position.z);
            root.transform.rotation = Quaternion.Euler(0f, c.transform.eulerAngles.y, 0f);
            var mc = root.AddComponent<McChest>();

            Transform Box(string name, Transform parent, Vector3 pivot, Vector3 center, Vector3 size, int u, int v)
            {
                var piv = new GameObject(name).transform;
                piv.SetParent(parent, false);
                piv.localPosition = pivot * unit;
                var m = new GameObject(name + "_m");
                m.transform.SetParent(piv, false);
                m.transform.localPosition = center * unit;
                m.AddComponent<MeshFilter>().sharedMesh = SteveRig.CutoutBox(tex, size.x * unit, size.y * unit, size.z * unit,
                    (int)size.x, (int)size.y, (int)size.z, u, v, false, false, 1f, 1f, true);
                m.AddComponent<MeshRenderer>().sharedMaterial = mat;
                return piv;
            }

            // Vanilla chest: 14x10x14 body, 14x5x14 lid hinged at the back, a 2x4x1 latch on the lid's front.
            Box("body", root.transform, Vector3.zero, new Vector3(0, 5, 0), new Vector3(14, 10, 14), 0, 19);
            mc.lid = Box("lid", root.transform, new Vector3(0, 10, -7), new Vector3(0, 2.5f, 7), new Vector3(14, 5, 14), 0, 0);
            Box("latch", mc.lid, new Vector3(0, -1, 14.5f), Vector3.zero, new Vector3(2, 4, 1), 0, 0);
        }

        // ------------------------------------------------------------------ opening

        public static bool TryOpen(InteractableChest c)
        {
            var inst = MinecraftMode.Instance;
            var pl = MyPlayer.Instance;
            if (c == null || inst == null || pl == null || pl.inventory == null) return false;
            BuildTable();
            Plugin.Logger.LogInfo("TryOpen chest " + c.chestType + " drop=" + IsDrop(c));
            McChest mc = null;
            var vis = c.transform.Find("MC_Chest");
            if (vis != null) mc = vis.GetComponent<McChest>();
            if (mc == null) { Skin(c); vis = c.transform.Find("MC_Chest"); if (vis != null) mc = vis.GetComponent<McChest>(); }
            if (mc == null) return false;

            if (!mc.looted)
            {
                int price = 0;
                try { price = c.GetPrice(); } catch { }
                if (price > 0) price = PriceFor(Assets.Scripts.Inventory__Items__Pickups.GoldAndMoney.MoneyUtility.chestsPurchased);
                bool afford = true;
                try { afford = pl.inventory.goldInt >= price; } catch { try { afford = c.CanAfford(); } catch { } }
                if (!afford)
                {
                    McSound.Play("block.chest.locked", 0.9f);
                    inst.Toast("Not enough gold (" + price + ")");
                    return false;
                }
                if (price > 0) { try { pl.inventory.ChangeGold(-price); } catch { } }
                Fill(mc, c.chestType);
                mc.looted = true;
                // The game's own chest events are not raised: their listeners expect its item roll and window.
                try { Assets.Scripts.Inventory__Items__Pickups.GoldAndMoney.MoneyUtility.chestsPurchased++; } catch { }
                Plugin.Logger.LogInfo("chest opened, price " + price);
            }
            inst.OpenChestUi(mc, c.transform.position);
            return true;
        }

        // Scatters the loot over random slots of the 27, like a vanilla dungeon chest.
        public static void FillPublic(McChest mc, EChest type) { BuildTable(); Fill(mc, type); }

        private static void Fill(McChest mc, EChest type)
        {
            bool ender = type == EChest.Free || type == EChest.FreeCrypt;   // the free end chests are the best ones
            int bonus = 4 + Ench.Lvl[Ench.Luck] + (ender ? 3 : 1);   // Treasure Hunter, Luck, and a better default loot
            int rolls = bonus + (type == EChest.Corrupt ? UnityEngine.Random.Range(6, 11)
                      : (type == EChest.Free || type == EChest.FreeCrypt) ? UnityEngine.Random.Range(3, 6)
                      : UnityEngine.Random.Range(5, 9));
            for (int i = 0; i < rolls; i++)
            {
                var l = Roll(ender ? 0.55f : 0.28f);
                int n = UnityEngine.Random.Range(l.min, l.max + 1);
                for (int tries = 0; tries < 40; tries++)
                {
                    int slot = UnityEngine.Random.Range(0, 27);
                    if (mc.defs[slot] == null) { mc.defs[slot] = l.item; mc.cnts[slot] = n; break; }
                }
            }
        }
    }
    // Animates the lid of a Minecraft chest.
    public class McChest : MonoBehaviour
    {
        public McChest(IntPtr ptr) : base(ptr) { }
        public Transform lid;
        private float t = -1f;
        public ItemDef[] defs = new ItemDef[27];
        public int[] cnts = new int[27];
        public bool looted;

        public void Open() { if (t < 0f) t = 0f; }

        private void Update()
        {
            if (t < 0f || lid == null) return;
            t = Mathf.Min(1f, t + Time.deltaTime / 0.35f);
            float e = 1f - (1f - t) * (1f - t);
            lid.localRotation = Quaternion.Euler(-105f * e, 0f, 0f);   // swings up and back
        }
    }
}




namespace MegabonkSteve
{
    // Chests that enemies drop are walked into and normally open the game's own item window, which crashes in this setup.
    // Instead, walking into one rolls a Megabonk item from the base game's pool and adds it straight to the player,
    // so it feeds the abilities and weapons as usual.
    [HarmonyLib.HarmonyPatch(typeof(Assets.Scripts.Inventory__Items__Pickups.Interactables.OpenChest), "OnTriggerStay")]
    internal static class Patch_DropChest
    {
        private static bool Prefix(Assets.Scripts.Inventory__Items__Pickups.Interactables.OpenChest __instance, Collider other)
        {
            if (!MinecraftMode.Active) return true;
            var inst = MinecraftMode.Instance;
            if (inst == null || other == null || other.GetComponentInParent<Assets.Scripts.Actors.Player.MyPlayer>() == null) return false;
            try
            {
                if (__instance.pickedup || !__instance.CanPickup()) return false;
                __instance.pickedup = true;
                var pl = Assets.Scripts.Actors.Player.MyPlayer.Instance;
                var inv2 = pl.inventory.itemInventory;
                var got = new System.Collections.Generic.List<(Texture, string, string, Color)>();
                for (int k = 0; k < 2; k++)
                {
                    var item = LevelFilter.RollItem(inv2);
                    if (item == null) continue;
                    inv2.AddItem(item.eItem);
                    var rc = item.rarity == Assets.Scripts.Inventory__Items__Pickups.Items.EItemRarity.Common ? Color.white
                           : item.rarity == Assets.Scripts.Inventory__Items__Pickups.Items.EItemRarity.Rare ? new Color(0.45f, 0.65f, 1f)
                           : item.rarity == Assets.Scripts.Inventory__Items__Pickups.Items.EItemRarity.Epic ? new Color(0.78f, 0.45f, 1f) : new Color(1f, 0.8f, 0.2f);
                    string d = ""; try { d = item.GetDescription(); } catch { }
                    got.Add((item.GetIcon(), item.GetName(), d, rc));
                    inst.Toast("Found " + item.GetName());
                    Plugin.Logger.LogInfo("enemy-dropped chest gave " + item.eItem + " (" + item.rarity + ")");
                }
                // two items: one on each side, each with its own info
                if (got.Count == 2) inst.RewardPair(got[0].Item1, got[0].Item2, got[0].Item3, got[0].Item4, got[1].Item1, got[1].Item2, got[1].Item3, got[1].Item4);
                else if (got.Count == 1) inst.Reward(got[0].Item1, got[0].Item2, got[0].Item3, got[0].Item4);
                McSound.PlayAt("block.chest.open", __instance.transform.position, 1f);
                McSound.Play("entity.player.levelup", 0.5f, 1.4f);
                __instance.gameObject.SetActive(false);
            }
            catch (System.Exception e) { Plugin.Logger.LogError("drop chest: " + e); }
            return false;
        }
    }
}
