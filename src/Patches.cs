using System;
using HarmonyLib;
using UnityEngine;
using Assets.Scripts.Actors;
using Assets.Scripts.Actors.Player;
using Il2CppInterop.Runtime;
using UnlockableBase = Assets.Scripts.Saves___Serialization.Progression.Achievements.UnlockableBase;

namespace MegabonkSteve
{
    [HarmonyPatch(typeof(DataManager), nameof(DataManager.Load))]
    internal static class Patch_DataManager_Load
    {
        private static void Postfix(DataManager __instance)
        {
            try { SteveData.Register(__instance); }
            catch (Exception e) { Plugin.Logger.LogError("Register Steve failed: " + e); }
            try { AutoWeapons.Reskin(__instance); }
            catch (Exception e) { Plugin.Logger.LogError("Reskin weapons failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(UnlockableBase), nameof(UnlockableBase.GetName))]
    internal static class Patch_GetName
    {
        private static void Postfix(UnlockableBase __instance, ref string __result)
        {
            var cd = __instance.TryCast<CharacterData>();
            if (Plugin.IsSteve(cd)) { __result = "Steve"; return; }
            if (!MinecraftMode.Active) return;
            var tdn = __instance.TryCast<TomeData>();
            if (tdn != null) { int k = Ench.IndexOf(tdn.eTome); if (k >= 0) { __result = "Enchanted Book: " + Ench.Names[k]; return; } }
            var wd = __instance.TryCast<WeaponData>();
            if (wd != null)
            {
                string n = AutoWeapons.NameOf(wd.eWeapon);
                if (n != null) __result = n;
            }
        }
    }

    [HarmonyPatch(typeof(UnlockableBase), nameof(UnlockableBase.GetDescription))]
    internal static class Patch_GetDescription
    {
        private static void Postfix(UnlockableBase __instance, ref string __result)
        {
            var cd = __instance.TryCast<CharacterData>();
            if (Plugin.IsSteve(cd)) { __result = "Mines, crafts and punches things. Straight from the overworld."; return; }
            if (!MinecraftMode.Active) return;
            var tdd = __instance.TryCast<TomeData>();
            if (tdd != null) { int k = Ench.IndexOf(tdd.eTome); if (k >= 0) { __result = Ench.Blurbs[k]; return; } }
            var wd = __instance.TryCast<WeaponData>();
            if (wd != null)
            {
                string d = AutoWeapons.DescOf(wd.eWeapon);
                if (d != null) __result = d;
            }
        }
    }

    [HarmonyPatch(typeof(PlayerRenderer), nameof(PlayerRenderer.SetCharacter))]
    internal static class Patch_SetCharacter
    {
        private static void Prefix(CharacterData characterData)
        {
            if (Plugin.IsSteve(characterData)) SaveFix.Ensure();
        }

        private static void Postfix(PlayerRenderer __instance, CharacterData characterData)
        {
            if (!Plugin.IsSteve(characterData)) return;
            try { SteveRig.Attach(__instance); }
            catch (Exception e) { Plugin.Logger.LogError("Attach Steve failed: " + e); }
        }
    }

    // Megabonk weapons fire by themselves; with Minecraft mode on, combat is manual instead.
    [HarmonyPatch(typeof(Assets.Scripts.Inventory__Items__Pickups.Weapons.WeaponBase), nameof(Assets.Scripts.Inventory__Items__Pickups.Weapons.WeaponBase.Use))]
    internal static class Patch_WeaponUse
    {
        private static bool Prefix() { return !MinecraftMode.Active; }
    }

    // Whole auto-weapon loop off: Steve has no default Megabonk abilities, only what he holds.
    [HarmonyPatch(typeof(Assets.Scripts.Inventory__Items__Pickups.Weapons.WeaponInventory), nameof(Assets.Scripts.Inventory__Items__Pickups.Weapons.WeaponInventory.Tick))]
    internal static class Patch_WeaponInventoryTick
    {
        // Megabonk's own attack loop is replaced by the Minecraft auto-weapons.
        private static bool Prefix(Assets.Scripts.Inventory__Items__Pickups.Weapons.WeaponInventory __instance)
        {
            if (!MinecraftMode.Active) return true;
            AutoWeapons.Tick(__instance);
            return false;
        }
    }

    // Minecraft walking, jumping and gravity replace Megabonk's movement (except on rails, ladders, walls, water).
    [HarmonyPatch(typeof(PlayerMovement), nameof(PlayerMovement.MovementTick))]
    internal static class Patch_MovementTick
    {
        private static void Prefix(PlayerMovement __instance)
        {
            var m = MinecraftMode.Instance;
            if (m != null) m.CaptureVel(__instance);
        }

        private static void Postfix(PlayerMovement __instance)
        {
            var m = MinecraftMode.Instance;
            if (m != null) m.MoveStep(__instance);
        }
    }

    // Chests: Minecraft model on spawn, and our own opening instead of the item roll.
    [HarmonyPatch(typeof(Assets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest), "Start")]
    internal static class Patch_ChestStart
    {
        private static void Postfix(Assets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest __instance)
        {
            if (!MinecraftMode.Active) return;
            try { McChests.Skin(__instance); } catch (Exception e) { Plugin.Logger.LogWarning("chest skin: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(Assets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest), nameof(Assets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest.Interact))]
    internal static class Patch_ChestInteract
    {
        private static bool Prefix(Assets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest __instance, ref bool __result)
        {
            if (!MinecraftMode.Active) return true;

            try { __result = McChests.TryOpen(__instance); }
            catch (Exception e) { Plugin.Logger.LogError("chest open: " + e); __result = false; }
            return false;
        }
    }

    // Once looted, a chest costs nothing to open again.
    [HarmonyPatch(typeof(Assets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest), nameof(Assets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest.GetPrice))]
    internal static class Patch_ChestPrice
    {
        private static void Postfix(Assets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest __instance, ref int __result)
        {
            if (!MinecraftMode.Active) return;
            var v = __instance.transform.Find("MC_Chest");
            var mc = v != null ? v.GetComponent<McChest>() : null;
            if (mc != null && mc.looted) { __result = 0; return; }
            if (__result > 0) __result = McChests.PriceFor(Assets.Scripts.Inventory__Items__Pickups.GoldAndMoney.MoneyUtility.chestsPurchased);
        }
    }

    // Enemies come from a pool and are re-initialised each spawn; this is where their look gets decided.
    [HarmonyPatch(typeof(Assets.Scripts.Actors.Enemies.Enemy), nameof(Assets.Scripts.Actors.Enemies.Enemy.InitEnemy))]
    internal static class Patch_EnemyInit
    {
        private static void Postfix(Assets.Scripts.Actors.Enemies.Enemy __instance)
        {
            if (!MinecraftMode.Active) return;
            try { EnemySkins.OnInit(__instance); }
            catch (Exception e) { Plugin.Logger.LogWarning("EnemySkins.OnInit: " + e.Message); }
        }
    }

    // The pool of weapons/tomes the game offers: only the Minecraft weapons, whether or not they are unlocked.
    [HarmonyPatch(typeof(Assets.Scripts.Inventory__Items__Pickups.Progression.UnlockUtility), nameof(Assets.Scripts.Inventory__Items__Pickups.Progression.UnlockUtility.GetAvailableWeapons))]
    internal static class Patch_AvailableWeapons
    {
        private static void Postfix(ref Il2CppSystem.Collections.Generic.List<WeaponData> __result)
        {
            if (!MinecraftMode.Active || !LevelFilter.Applied) return;
            var dm = DataManager.Instance;
            var list = new Il2CppSystem.Collections.Generic.List<WeaponData>();
            // Weapons are only ever offered when levelling up; shrines, shops and the like never show them.
            if (Patch_ShuffleUpgrades.Current != Assets.Scripts.UI.InGame.Rewards.EEncounter.Levelup) { __result = list; return; }
            foreach (var id in AutoWeapons.All)
            {
                try { if (dm.weapons.ContainsKey(id)) list.Add(dm.weapons[id]); } catch { }
            }
            __result = list;
        }
    }

    [HarmonyPatch(typeof(Assets.Scripts.Inventory__Items__Pickups.Progression.UnlockUtility), nameof(Assets.Scripts.Inventory__Items__Pickups.Progression.UnlockUtility.GetAvailableTomes))]
    internal static class Patch_AvailableTomes
    {
        private static void Postfix(ref Il2CppSystem.Collections.Generic.HashSet<TomeData> __result)
        {
            if (!MinecraftMode.Active || !LevelFilter.Applied) return;
            // Only the tomes that stand in for an enchanted book, until that enchantment is maxed.
            var set = new Il2CppSystem.Collections.Generic.HashSet<TomeData>();
            var dm = DataManager.Instance;
            for (int i = 0; i < Ench.Count; i++)
            {
                if (Ench.Lvl[i] >= Ench.MaxLevel) continue;
                try { var td = dm.GetTome(Ench.Tomes[i]); if (td != null) set.Add(td); } catch { }
            }
            __result = set;
        }
    }

    // Picking one of those cards grants the enchantment instead of the Megabonk tome.
    [HarmonyPatch(typeof(Assets.Scripts.Inventory__Items__Pickups.TomeInventory), nameof(Assets.Scripts.Inventory__Items__Pickups.TomeInventory.AddTome))]
    internal static class Patch_AddTome
    {
        public static bool Internal;
        private static bool Prefix(TomeData tomeData)
        {
            if (!MinecraftMode.Active || tomeData == null) return true;
            int i = Ench.IndexOf(tomeData.eTome);
            if (i < 0) return true;
            if (!Internal) MinecraftMode.Instance.LearnEnchant(i);
            return true;
        }
    }

    // Megabonk's experience gems become Minecraft experience orbs, with the orb pickup sound.
    [HarmonyPatch(typeof(Pickup), nameof(Pickup.Set))]
    internal static class Patch_PickupSet
    {
        private static void Postfix(Pickup __instance)
        {
            if (!MinecraftMode.Active) return;
            var kind = __instance.ePickup;
            if (kind == Assets.Scripts.Inventory__Items__Pickups.Pickups.EPickup.Gold) { try { Fx.SkinIngot(__instance, "item/gold_ingot"); } catch (Exception e) { Plugin.Logger.LogWarning("gold skin: " + e.Message); } return; }
            if (kind == Assets.Scripts.Inventory__Items__Pickups.Pickups.EPickup.Silver) { try { Fx.SkinIngot(__instance, "item/iron_ingot"); } catch (Exception e) { Plugin.Logger.LogWarning("silver skin: " + e.Message); } return; }
            if (kind != Assets.Scripts.Inventory__Items__Pickups.Pickups.EPickup.Xp) return;
            try { Fx.SkinXp(__instance); } catch (Exception e) { Plugin.Logger.LogWarning("orb skin: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(Pickup), nameof(Pickup.ApplyPickup))]
    internal static class Patch_PickupTaken
    {
        private static float last;
        private static void Postfix(Pickup __instance)
        {
            if (!MinecraftMode.Active) return;
            var kind = __instance.ePickup;
            if (kind == Assets.Scripts.Inventory__Items__Pickups.Pickups.EPickup.Gold) { try { Fx.SkinIngot(__instance, "item/gold_ingot"); } catch (Exception e) { Plugin.Logger.LogWarning("gold skin: " + e.Message); } return; }
            if (kind == Assets.Scripts.Inventory__Items__Pickups.Pickups.EPickup.Silver) { try { Fx.SkinIngot(__instance, "item/iron_ingot"); } catch (Exception e) { Plugin.Logger.LogWarning("silver skin: " + e.Message); } return; }
            if (kind != Assets.Scripts.Inventory__Items__Pickups.Pickups.EPickup.Xp) return;
            if (Time.time - last < 0.04f) return;
            last = Time.time;
            McSound.Play("entity.experience_orb.pickup", 0.5f, 0.75f + UnityEngine.Random.value * 0.5f);
        }
    }

    // Vanilla level-up chime when the card screen opens.
    [HarmonyPatch(typeof(LevelupScreen), nameof(LevelupScreen.ShowLevelupScreen))]
    internal static class Patch_LevelupSound
    {
        private static void Postfix()
        {
            if (MinecraftMode.Active) McSound.Play("entity.player.levelup", 0.8f);
        }
    }

    // Logs what the level-up picker offered, to check the Minecraft-only filter.
    [HarmonyPatch(typeof(UpgradePicker), nameof(UpgradePicker.ShuffleUpgrades))]
    internal static class Patch_ShuffleUpgrades
    {
        public static Assets.Scripts.UI.InGame.Rewards.EEncounter Current = Assets.Scripts.UI.InGame.Rewards.EEncounter.Levelup;
        private static void Prefix(Assets.Scripts.UI.InGame.Rewards.EEncounter encounterType) { Current = encounterType; }

        private static void Postfix(UpgradePicker __instance)
        {
            if (!MinecraftMode.Active) return;
            try
            {
                string s = "Level-up offers:";
                foreach (var b in __instance.buttons)
                {
                    if (b == null) continue;
                    string n = b.t_name != null ? b.t_name.text : "?";
                    string kind = "?";
                    try
                    {
                        if (b.isItem) kind = "item " + (b.itemData != null ? b.itemData.eItem.ToString() : "null");
                        else if (b.upgradable != null)
                        {
                            var wd = b.upgradable.TryCast<WeaponData>();
                            var td = b.upgradable.TryCast<TomeData>();
                            kind = wd != null ? "weapon " + wd.eWeapon : (td != null ? "tome " + td.eTome : "other");
                        }
                    }
                    catch { }
                    s += " [" + n + " | " + kind + "]";
                }
                Plugin.Logger.LogInfo(s);
            }
            catch (Exception e) { Plugin.Logger.LogWarning("offers log: " + e.Message); }
        }
    }

    // Shift acts as the game's crouch/slide button (C), so slides, inertia and bunny hops all work.
    [HarmonyPatch(typeof(PlayerInput), nameof(PlayerInput.Update))]
    internal static class Patch_PlayerInputShift
    {
        private static void Postfix(PlayerInput __instance)
        {
            // Right click is Minecraft's use button here, not Megabonk's aim toggle (which shows its own crosshair).
            if (MinecraftMode.Active) __instance.aiming = false;
            // Only Shift (or C) slides; Ctrl is just for sprinting.
            if (MinecraftMode.Active)
                __instance.sliding = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.C)) && !MinecraftMode.Instance.IsGliding;
        }
    }

    // The mouse must not turn the camera while the inventory screen is open.
    [HarmonyPatch(typeof(PlayerInput), nameof(PlayerInput.RotationInput))]
    internal static class Patch_RotationInput
    {
        private static bool Prefix() { return !MinecraftMode.InvOpen && !MinecraftMode.AdminOpen; }
    }

    // Elytra landings and the moment after are safe.
    [HarmonyPatch(typeof(Assets.Scripts.Inventory__Items__Pickups.PlayerHealth), nameof(Assets.Scripts.Inventory__Items__Pickups.PlayerHealth.OnPlayerLanded))]
    internal static class Patch_Landed
    {
        private static bool Prefix()
        {
            var m = MinecraftMode.Instance;
            return m == null || !m.GlideProtect;
        }
    }

    // Shield: while blocking, enemy hits do no damage.
    [HarmonyPatch(typeof(Assets.Scripts.Inventory__Items__Pickups.PlayerHealth), nameof(Assets.Scripts.Inventory__Items__Pickups.PlayerHealth.Damage))]
    internal static class Patch_PlayerDamage
    {
        private static bool Prefix(Assets.Scripts.Inventory__Items__Pickups.PlayerHealth __instance, DamageContainer dc, bool ignoreShield)
        {
            var m = MinecraftMode.Instance;
            if (m == null) return true;
            if (m.God) return false;
            if (m.TryBlock(dc)) return false;
            m.ApplyArmor(dc);
            if (m.TryTotem(__instance, dc, ignoreShield)) return false;
            m.OnHurt(__instance, dc, ignoreShield);
            return true;
        }
    }
}








namespace MegabonkSteve
{
    // Chests that enemies drop are Megabonk's own: they roll Megabonk items, which then feed the auto-weapons' stats.
    [HarmonyPatch(typeof(EffectManager), nameof(EffectManager.SpawnChest))]
    internal static class Patch_SpawnChest
    {
        private static void Prefix() { McChests.InDrop = true; }
        private static void Postfix() { McChests.InDrop = false; }
    }

    [HarmonyPatch(typeof(EffectManager), nameof(EffectManager.SpawnChestForcePosition))]
    internal static class Patch_SpawnChestForce
    {
        private static void Prefix() { McChests.InDrop = true; }
        private static void Postfix() { McChests.InDrop = false; }
    }

    [HarmonyPatch(typeof(Assets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest), "Awake")]
    internal static class Patch_ChestAwake
    {
        private static void Postfix(Assets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest __instance)
        {
            if (McChests.InDrop) { McChests.Drops.Add(__instance.GetInstanceID()); Plugin.Logger.LogInfo("enemy-dropped chest: keeps Megabonk items"); }
        }
    }

    // The item pool is only switched back on while a dropped chest is being opened.
    [HarmonyPatch(typeof(Assets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest), nameof(Assets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest.OnChestWindowClose))]
    internal static class Patch_ChestWindowClose
    {
        private static void Postfix() { LevelFilter.ItemsOff(); }
    }
}

namespace MegabonkSteve
{
    // The Chunkers weapon keeps orbiting rocks around the player on its own; the Anvil replaces it, so those are switched off.
    [HarmonyPatch(typeof(ChunkersAttack), nameof(ChunkersAttack.StartAttack))]
    internal static class Patch_ChunkersStart { private static bool Prefix() { return !MinecraftMode.Active; } }

    [HarmonyPatch(typeof(ChunkersAttack), nameof(ChunkersAttack.FixedUpdate))]
    internal static class Patch_ChunkersFixed { private static bool Prefix() { return !MinecraftMode.Active; } }

    [HarmonyPatch(typeof(ChunkersAttack), nameof(ChunkersAttack.Init))]
    internal static class Patch_ChunkersInit
    {
        private static void Postfix(ChunkersAttack __instance)
        {
            if (!MinecraftMode.Active) return;
            try { if (__instance.rotatingProjectiles != null) __instance.rotatingProjectiles.gameObject.SetActive(false); } catch { }
        }
    }
}

namespace MegabonkSteve
{
    // Every boss of a kind is the same boss, so you always know what is coming and how it behaves: the final boss, the stage
    // boss and the minibosses each map to one fixed enemy (the ones the admin panel spawns).
    [HarmonyPatch(typeof(Assets.Scripts.Managers.EnemyManager), nameof(Assets.Scripts.Managers.EnemyManager.SpawnBoss))]
    internal static class Patch_SpawnBossFixed
    {
        private static void Prefix(ref Actors.Enemies.EEnemy eEnemy, Assets.Scripts.Actors.Enemies.EEnemyFlag enemyFlag)
        {
            if (!MinecraftMode.Active) return;
            int f = (int)enemyFlag;
            Actors.Enemies.EEnemy want = eEnemy;
            if ((f & (int)Assets.Scripts.Actors.Enemies.EEnemyFlag.FinalBoss) != 0) want = Actors.Enemies.EEnemy.GhostKing;
            else if ((f & (int)Assets.Scripts.Actors.Enemies.EEnemyFlag.StageBoss) != 0) want = Actors.Enemies.EEnemy.Pharaoh1;
            else if ((f & ((int)Assets.Scripts.Actors.Enemies.EEnemyFlag.Boss | (int)Assets.Scripts.Actors.Enemies.EEnemyFlag.SummonerMiniboss)) != 0) want = Actors.Enemies.EEnemy.MinibossGolem;
            if (want != eEnemy) { Plugin.Logger.LogInfo("Boss " + eEnemy + " (" + enemyFlag + ") replaced by " + want); eEnemy = want; }
        }
    }

    // Character screen: Steve's weapon and passive entries describe what he really has.
    [HarmonyPatch(typeof(CharacterInfoUI), nameof(CharacterInfoUI.OnCharacterSelected))]
    internal static class Patch_CharacterInfo
    {
        private static void Postfix(CharacterInfoUI __instance)
        {
            try
            {
                bool steve = __instance.t_name != null && __instance.t_name.text == "Steve";
                // The game's skin shop is hidden for Steve (his skins are chosen with F9); other characters get it back.
                foreach (var tm in __instance.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
                    if (tm != null && tm.text != null && tm.text.Trim().ToLowerInvariant() == "skin") tm.gameObject.SetActive(!steve);
                if (__instance.skinSelection != null) __instance.skinSelection.gameObject.SetActive(!steve);
                if (!steve) return;
                McAssets.Init();
                __instance.t_description.text = "Mines, crafts and punches things. Straight from the overworld." + (char)10 + (char)10 + "F9: choose your skin     F8: test panel (in game)";
                __instance.t_weaponName.text = "Bare Hands";
                __instance.t_weaponDesc.text = "You start with your fists (1 damage). Find gear in chests; the best is rare.";
                var ws = McAssets.Tex("item/stick"); if (ws != null) __instance.i_weapon.texture = ws;
                __instance.t_passiveName.text = "Treasure Hunter";
                __instance.t_passiveDesc.text = "Chests hold two extra items, and critical hits drop double gold.";
                var pi = McAssets.Tex("item/diamond"); if (pi != null) __instance.i_passive.texture = pi;
            }
            catch (Exception e) { Plugin.Logger.LogWarning("character info: " + e.Message); }
        }
    }
}

namespace MegabonkSteve
{
    // Interactions (chests, pots, shrines...) are a right click in this mode; the game's own E key does nothing.
    [HarmonyPatch(typeof(Assets.Scripts.Inventory__Items__Pickups.Interactables.DetectInteractables), nameof(Assets.Scripts.Inventory__Items__Pickups.Interactables.DetectInteractables.TryInteract))]
    internal static class Patch_TryInteract
    {
        private static bool Prefix() { return !MinecraftMode.Active || MinecraftMode.AllowInteract; }
    }

    // Escape closes our screens without also opening the game's pause menu.
    [HarmonyPatch(typeof(PauseUi), nameof(PauseUi.Pause))]
    internal static class Patch_PauseGuard
    {
        private static bool Prefix()
        {
            if (!MinecraftMode.Active) return true;
            bool esc = Input.GetKey(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Escape);
            if (esc && MinecraftMode.UiOpen) return false;
            if (Time.unscaledTime < MinecraftMode.EscGraceUntil) return false;
            return true;
        }
    }
}

namespace MegabonkSteve
{
    // The game's own interaction prompt (the E key glyph and its label) is hidden: interacting is a right click here.
    [HarmonyPatch(typeof(Assets.Scripts.Inventory__Items__Pickups.Interactables.DetectInteractables), "Update")]
    internal static class Patch_HideInteractPrompt
    {
        private static void Postfix(Assets.Scripts.Inventory__Items__Pickups.Interactables.DetectInteractables __instance)
        {
            if (!MinecraftMode.Active) return;
            try
            {
                if (__instance.uiParent != null && __instance.uiParent.gameObject.activeSelf) __instance.uiParent.gameObject.SetActive(false);
                if (__instance.glyphContainer != null && __instance.glyphContainer.gameObject.activeSelf) __instance.glyphContainer.gameObject.SetActive(false);
            }
            catch { }
        }
    }
}

namespace MegabonkSteve
{
    // The flying coins that pots, chests and enemies shower out: gold ingots too.
    [HarmonyPatch(typeof(MoneyFlying), nameof(MoneyFlying.Set))]
    internal static class Patch_MoneyFlying
    {
        private static void Postfix(MoneyFlying __instance)
        {
            if (!MinecraftMode.Active) return;
            try { Fx.SkinIngot(__instance, "item/gold_ingot"); } catch (Exception e) { Plugin.Logger.LogWarning("coin skin: " + e.Message); }
        }
    }
}

namespace MegabonkSteve
{
    // The price the game shows and charges comes from here: our own table, 10, 20, 37, 50...
    [HarmonyPatch(typeof(Assets.Scripts.Inventory__Items__Pickups.GoldAndMoney.MoneyUtility), nameof(Assets.Scripts.Inventory__Items__Pickups.GoldAndMoney.MoneyUtility.GetChestPrice))]
    internal static class Patch_ChestPriceTable
    {
        private static void Postfix(ref int __result)
        {
            if (!MinecraftMode.Active) return;
            __result = McChests.PriceFor(Assets.Scripts.Inventory__Items__Pickups.GoldAndMoney.MoneyUtility.chestsPurchased);
        }
    }
}

namespace MegabonkSteve
{
    // Only Minecraft mobs spawn: an enemy that has no Minecraft look (bees, ghosts, golems, frogs...) is swapped at spawn time
    // for one that does, so nothing from the base game's bestiary shows up.
    internal static class MobSubstitute
    {
        private static readonly Actors.Enemies.EEnemy[] pool =
        {
            Actors.Enemies.EEnemy.Zombie, Actors.Enemies.EEnemy.Skeleton, Actors.Enemies.EEnemy.BoomerSpider,
            Actors.Enemies.EEnemy.Scorpion, Actors.Enemies.EEnemy.Slime, Actors.Enemies.EEnemy.GoblinStrong, Actors.Enemies.EEnemy.Bandit,
            Actors.Enemies.EEnemy.FrogGreen, Actors.Enemies.EEnemy.FrogBlue,
        };
        private static readonly System.Collections.Generic.HashSet<Actors.Enemies.EEnemy> reported = new System.Collections.Generic.HashSet<Actors.Enemies.EEnemy>();

        public static void Apply(ref EnemyData data, Assets.Scripts.Actors.Enemies.EEnemyFlag flag)
        {
            if (!MinecraftMode.Active || data == null) return;
            int bossBits = (int)(Assets.Scripts.Actors.Enemies.EEnemyFlag.Boss | Assets.Scripts.Actors.Enemies.EEnemyFlag.StageBoss
                | Assets.Scripts.Actors.Enemies.EEnemyFlag.FinalBoss | Assets.Scripts.Actors.Enemies.EEnemyFlag.SummonerMiniboss | Assets.Scripts.Actors.Enemies.EEnemyFlag.AnyBoss);
            if (((int)flag & bossBits) != 0) return;
            var type = data.enemyName;
            if (EnemySkins.Map(type) != null) return;
            var swap = pool[((int)type & 0x7fffffff) % pool.Length];
            try
            {
                var repl = DataManager.Instance.GetEnemyData(swap);
                if (repl == null) return;
                if (reported.Add(type)) Plugin.Logger.LogInfo("Mob " + type + " has no Minecraft version: spawns as " + swap);
                data = repl;
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(Assets.Scripts.Managers.EnemyManager), nameof(Assets.Scripts.Managers.EnemyManager.SpawnEnemy),
        new[] { typeof(EnemyData), typeof(int), typeof(bool), typeof(Assets.Scripts.Actors.Enemies.EEnemyFlag), typeof(bool) })]
    internal static class Patch_SpawnSubA
    {
        private static void Prefix(ref EnemyData enemyData, Assets.Scripts.Actors.Enemies.EEnemyFlag flag) { MobSubstitute.Apply(ref enemyData, flag); }
    }

    [HarmonyPatch(typeof(Assets.Scripts.Managers.EnemyManager), nameof(Assets.Scripts.Managers.EnemyManager.SpawnEnemy),
        new[] { typeof(EnemyData), typeof(Vector3), typeof(int), typeof(bool), typeof(Assets.Scripts.Actors.Enemies.EEnemyFlag), typeof(bool), typeof(float) })]
    internal static class Patch_SpawnSubB
    {
        private static void Prefix(ref EnemyData enemyData, Assets.Scripts.Actors.Enemies.EEnemyFlag flag) { MobSubstitute.Apply(ref enemyData, flag); }
    }
}

namespace MegabonkSteve
{
    // The grass tufts a map scatters become Minecraft grass: swapped just before the manager builds its draw call.
    [HarmonyPatch(typeof(GrassChunkManager), "Start")]
    internal static class Patch_GrassStart
    {
        private static void Prefix(GrassChunkManager __instance)
        {
            if (!MinecraftMode.Active) return;
            try { McTerrain.SkinGrass(__instance); } catch (Exception e) { Plugin.Logger.LogWarning("grass: " + e.Message); }
        }
    }
}

namespace MegabonkSteve
{
    // Nothing hands out a weapon for free: it only joins your inventory when you pick it on a level-up card.
    [HarmonyPatch(typeof(Assets.Scripts.Inventory__Items__Pickups.Weapons.WeaponInventory), nameof(Assets.Scripts.Inventory__Items__Pickups.Weapons.WeaponInventory.AddWeapon))]
    internal static class Patch_AddWeaponGuard
    {
        public static bool Allow;
        private static bool Prefix()
        {
            if (!MinecraftMode.Active || Allow) return true;
            Plugin.Logger.LogInfo("A free weapon was blocked (only level-up picks give weapons)");
            return false;
        }
    }

    [HarmonyPatch(typeof(UpgradePicker), nameof(UpgradePicker.SelectUpgrade))]
    internal static class Patch_SelectUpgradeGuard
    {
        private static void Prefix() { Patch_AddWeaponGuard.Allow = Patch_ShuffleUpgrades.Current == Assets.Scripts.UI.InGame.Rewards.EEncounter.Levelup; }
        private static void Postfix() { Patch_AddWeaponGuard.Allow = false; MinecraftMode.AutoMarkDirty = true; }
    }
}

namespace MegabonkSteve
{
    // Steve's passive: gold that drops while a critical hit lands counts double.
    [HarmonyPatch(typeof(Assets.Scripts.Inventory__Items__Pickups.GoldAndMoney.MoneyUtility), nameof(Assets.Scripts.Inventory__Items__Pickups.GoldAndMoney.MoneyUtility.SpawnMoney))]
    internal static class Patch_CritGold
    {
        private static void Prefix(ref int amount)
        {
            if (!MinecraftMode.Active || Time.time > MinecraftMode.CritGoldUntil || amount <= 0) return;
            Plugin.Dbg("Critical hit: gold " + amount + " doubled");
            amount *= 2;
        }
    }
}
