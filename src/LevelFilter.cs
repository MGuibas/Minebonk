using System;
using System.Collections.Generic;
using Assets.Scripts.Inventory__Items__Pickups.Weapons;
using UnityEngine;

namespace MegabonkSteve
{
    // Keeps the level-up cards to Minecraft content only: weapons that have a Minecraft version, and nothing
    // else (no Megabonk tomes or items). The original flags are restored when the run ends.
    internal static class LevelFilter
    {
        private static readonly List<KeyValuePair<UnityEngine.Object, bool>> saved = new List<KeyValuePair<UnityEngine.Object, bool>>();
        private static readonly List<KeyValuePair<ItemData, bool>> savedItems = new List<KeyValuePair<ItemData, bool>>();
        public static bool Applied { get; private set; }

        public static void Apply()
        {
            var dm = DataManager.Instance;
            if (dm == null) return;
            Restore();
            int w = 0, t = 0, it = 0;
            foreach (var wd in dm.unsortedWeapons)
            {
                if (wd == null) continue;
                saved.Add(new KeyValuePair<UnityEngine.Object, bool>(wd, wd.isEnabled));
                wd.isEnabled = AutoWeapons.Has(wd.eWeapon);
                if (wd.isEnabled) w++;
            }
            foreach (var td in dm.unsortedTomes)
            {
                if (td == null) continue;
                saved.Add(new KeyValuePair<UnityEngine.Object, bool>(td, td.isEnabled));
                td.isEnabled = Ench.IndexOf(td.eTome) >= 0;
                t++;
            }
            foreach (var id in dm.unsortedItems)
            {
                if (id == null) continue;
                savedItems.Add(new KeyValuePair<ItemData, bool>(id, id.inItemPool));
                id.inItemPool = false;
                it++;
            }
            Applied = true;
            Plugin.Logger.LogInfo("Level-up cards limited to Minecraft content: " + w + " weapons on, " + t + " tomes off, " + it + " items off");
        }

        // Megabonk's own items back in the pool (while a dropped chest rolls), and out again afterwards.
        public static bool ItemsActive; public static float ItemsOnAt;
        public static void ItemsOn()
        {
            ItemsActive = true; ItemsOnAt = Time.time;
            foreach (var kv in savedItems) { try { kv.Key.inItemPool = kv.Value; } catch { } }
        }

        public static void ItemsOff()
        {
            ItemsActive = false;
            if (!Applied) return;
            foreach (var kv in savedItems) { try { kv.Key.inItemPool = false; } catch { } }
        }

        // One random Megabonk item from the base game's own pool (the flags saved before the Minecraft filter).
        public static ItemData RollItem(Assets.Scripts.Inventory__Items__Pickups.Items.ItemInventory inv)
        {
            var pool = new List<ItemData>(); var weights = new List<float>(); float total = 0f;
            foreach (var kv in savedItems)
            {
                var d = kv.Key;
                if (!kv.Value || d == null || d.rarity == Assets.Scripts.Inventory__Items__Pickups.Items.EItemRarity.Quest || d.rarity == Assets.Scripts.Inventory__Items__Pickups.Items.EItemRarity.Corrupted) continue;
                try { if (d.maxAmount > 0 && inv.GetAmount(d.eItem) >= d.maxAmount) continue; } catch { }
                float w = d.rarity == Assets.Scripts.Inventory__Items__Pickups.Items.EItemRarity.Common ? 60f : d.rarity == Assets.Scripts.Inventory__Items__Pickups.Items.EItemRarity.Rare ? 28f : d.rarity == Assets.Scripts.Inventory__Items__Pickups.Items.EItemRarity.Epic ? 9f : 3f;
                pool.Add(d); weights.Add(w); total += w;
            }
            if (pool.Count == 0) return null;
            float r = UnityEngine.Random.value * total;
            for (int i = 0; i < pool.Count; i++) { r -= weights[i]; if (r <= 0f) return pool[i]; }
            return pool[pool.Count - 1];
        }

        public static void Restore()
        {
            foreach (var kv in saved)
            {
                try
                {
                    var wd = kv.Key as WeaponData; if (wd != null) { wd.isEnabled = kv.Value; continue; }
                    var td = kv.Key as TomeData; if (td != null) td.isEnabled = kv.Value;
                }
                catch { }
            }
            foreach (var kv in savedItems) { try { kv.Key.inItemPool = kv.Value; } catch { } }
            saved.Clear(); savedItems.Clear();
            Applied = false;
        }
    }
}
