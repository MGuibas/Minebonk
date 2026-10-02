using System;
using System.Collections.Generic;
using Assets.Scripts.Actors;
using Assets.Scripts.Actors.Player;
using Assets.Scripts.Actors.Enemies;
using Actors.Enemies;
using Assets.Scripts.Inventory__Items__Pickups;
using UnityEngine;

namespace MegabonkSteve
{
    // Minecraft 1.21 style gameplay layered over Megabonk: first person, manual melee with the real
    // attack-cooldown maths, hotbar and inventory, food, armor, elytra and the vanilla HUD.
    // Attached to the player when Steve is picked.
    public class MinecraftMode : MonoBehaviour
    {
        public MinecraftMode(IntPtr ptr) : base(ptr) { }

        public static MinecraftMode Instance;
        public static bool Active { get { return Instance != null; } }
        public static bool InvOpen { get { return Instance != null && Instance.invUi != null && Instance.invUi.IsOpen; } }
        // Interacting is a right click here: the game's own E key is switched off (the patch lets only our call through).
        public static bool AllowInteract;
        public static float EscGraceUntil;
        public static bool UiOpen { get { return AdminOpen || (Instance != null && ((Instance.invUi != null && Instance.invUi.IsOpen) || (Instance.chestUi != null && Instance.chestUi.IsOpen))); } }
        public static bool AdminOpen { get { return SkinPicker.IsOpen || (Instance != null && Instance.adminUi != null && Instance.adminUi.IsOpen); } }
        public bool God { get { return adminUi != null && adminUi.God; } }

        // True while the game itself is paused or showing a menu (pause, level-up...). Our own screens don't count.
        private static string lastReason = "";
        public static float CritGoldUntil;   // Treasure Hunter: gold dropped during a critical hit counts double
        private float mendAcc, lastComboHit; private int comboCount; private float prevFall;

        // The gear this mode used to start with (F8 panel): netherite tools, bow, food, armor, elytra, totem.
        public void GiveFullKit()
        {
            inv.Set(0, ItemLibrary.NetheriteSword, 1);
            inv.Set(1, ItemLibrary.NetheriteAxe, 1);
            inv.Set(2, ItemLibrary.Mace, 1);
            inv.Set(3, ItemLibrary.Bow, 1);
            inv.Set(22, ItemLibrary.DiamondSword, 1);
            inv.Set(23, ItemLibrary.Arrow, 64);
            inv.Set(24, ItemLibrary.Arrow, 64);
            inv.Set(4, ItemLibrary.GoldenApple, 3);
            inv.Set(5, ItemLibrary.Pearl, 16);
            inv.Set(6, ItemLibrary.StrengthPotion, 1);
            inv.Set(7, ItemLibrary.HealPotion, 1);
            inv.Set(8, ItemLibrary.Steak, 16);
            inv.Set(Inv.Offhand, ItemLibrary.Shield, 1);
            // Main inventory stock: elytra, rockets and both armor sets to try on.
            inv.Set(9, ItemLibrary.Elytra, 1);
            inv.Set(10, ItemLibrary.Rocket, 64);
            for (int i = 0; i < 4; i++) { inv.Set(11 + i, ItemLibrary.NetheriteArmor[i], 1); inv.Set(15 + i, ItemLibrary.DiamondArmor[i], 1); }
            inv.Set(19, ItemLibrary.Totem, 1);
            inv.Set(20, ItemLibrary.GoldenApple, 16);
            inv.Set(21, ItemLibrary.Pearl, 16);
            OnInventoryChanged();
            Toast("Full kit given");
        }

        // Enemies that end up under the ground (spawned inside a hill, pushed through a slope) are lifted back onto the surface.
        private float nextBuriedScan;
        private int buriedCursor;

        private void FixBuriedEnemies()
        {
            if (Time.time < nextBuriedScan) return;
            nextBuriedScan = Time.time + 0.5f;
            try
            {
                var em = Assets.Scripts.Managers.EnemyManager.Instance;
                var pl = MyPlayer.Instance;
                if (em == null || pl == null || pl.playerMovement == null) return;
                int mask = pl.playerMovement.whatIsGround;
                var list = new List<Enemy>();
                foreach (var kv in em.enemies) if (kv.Value != null && !kv.Value.IsDead()) list.Add(kv.Value);
                if (list.Count == 0) return;
                int n = Mathf.Min(40, list.Count);
                for (int k = 0; k < n; k++)
                {
                    var e = list[(buriedCursor + k) % list.Count];
                    try { if (e.enemyData != null && e.enemyData.isFlying) continue; } catch { }
                    Vector3 feet = e.GetFeetPosition();
                    RaycastHit hit;
                    // Ground found above the feet means the enemy is inside it.
                    if (Physics.Raycast(feet + Vector3.up * 60f, Vector3.down, out hit, 62f, mask, QueryTriggerInteraction.Ignore))
                    {
                        // ... and nothing to stand on right below: an enemy under a roof still has a floor, a buried one does not.
                        if (hit.point.y > feet.y + 1.2f && !Physics.Raycast(feet + Vector3.up * 0.5f, Vector3.down, 3f, mask, QueryTriggerInteraction.Ignore))
                        {
                            // Only lift when the way down from above is clear of a roof: the first hit must be the terrain surface.
                            Vector3 p = e.transform.position;
                            e.transform.position = new Vector3(p.x, p.y + (hit.point.y - feet.y) + 0.3f, p.z);
                            Plugin.Logger.LogInfo("Lifted a buried " + e.enemyData.enemyName + " by " + (hit.point.y - feet.y).ToString("0.0"));
                        }
                    }
                }
                buriedCursor = (buriedCursor + n) % Mathf.Max(1, list.Count);
            }
            catch (Exception ex) { if (Time.time < 60f) Plugin.Logger.LogWarning("buried scan: " + ex.Message); }
        }

        // Interface icons named after gold or silver (the counters, the pickup popups) show an ingot instead.
        private float nextCurrencyScan;
        private Texture2D goldTex, ironTex;

        private readonly HashSet<string> currencyLogged = new HashSet<string>();

        private readonly HashSet<int> currencyDone = new HashSet<int>();

        private void SkinCurrencyUi()
        {
            if (Time.time < nextCurrencyScan) return;
            nextCurrencyScan = Time.time + 3f;
            try
            {
                if (goldTex == null) goldTex = McAssets.Tex("item/gold_ingot");
                if (ironTex == null) ironTex = McAssets.Tex("item/iron_ingot");
                if (goldTex == null || ironTex == null) return;
                // The counters (top bar, chest price...) name their number text after the currency; the icon is its nearest picture.
                foreach (var tm in UnityEngine.Object.FindObjectsOfType<TMPro.TextMeshProUGUI>())
                {
                    string n = tm.name.ToLowerInvariant();
                    if (n.Contains("golden") || n.Contains("filter") || n.Contains("earned") || n.Contains("multiplier")) continue;
                    bool s = n.Contains("silver"), g = n.Contains("gold") || n.Contains("coin") || n.Contains("money") || n.Contains("price") || n.Contains("cost");
                    if (!s && !g) continue;
                    var par = tm.transform.parent;
                    if (par == null) continue;
                    UnityEngine.UI.RawImage bestRaw = null; UnityEngine.UI.Image bestImg = null; float bd = 260f;
                    var scope = par;
                    for (int lvl = 0; lvl < 3 && scope != null && bestRaw == null && bestImg == null; lvl++, scope = scope.parent)
                    {
                        foreach (var ri in scope.GetComponentsInChildren<UnityEngine.UI.RawImage>(true))
                        {
                            if (ri.name == "MC_IngotIcon") continue;
                            float d = Vector3.Distance(ri.transform.position, tm.transform.position);
                            if (d < bd && ri.rectTransform.rect.width <= 90f && ri.rectTransform.rect.width > 4f) { bd = d; bestRaw = ri; bestImg = null; }
                        }
                        foreach (var im in scope.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                        {
                            float d = Vector3.Distance(im.transform.position, tm.transform.position);
                            if (d < bd && im.rectTransform.rect.width <= 90f && im.rectTransform.rect.width > 4f) { bd = d; bestImg = im; bestRaw = null; }
                        }
                    }
                    if (currencyLogged.Add("tmp:" + tm.name + (bestRaw != null || bestImg != null))) Plugin.Logger.LogInfo("currency text " + tm.name + " icon=" + (bestRaw != null ? "raw " + bestRaw.name : bestImg != null ? "img " + bestImg.name : "none") + " d=" + bd.ToString("0"));
                    if (bestRaw == null && bestImg == null && currencyLogged.Add("dump:" + tm.name))
                    {
                        string dump = "currency dump " + tm.name + " parent=" + par.name + " grand=" + (par.parent != null ? par.parent.name : "-") + " children:";
                        for (int ci = 0; ci < par.childCount; ci++) { var c = par.GetChild(ci); dump += " [" + c.name + (c.GetComponent<UnityEngine.UI.RawImage>() != null ? " raw" : "") + (c.GetComponent<UnityEngine.UI.Image>() != null ? " img" : "") + "]"; }
                        Plugin.Logger.LogInfo(dump);
                    }
                    var tex = s ? ironTex : goldTex;
                    if (bestRaw != null) bestRaw.texture = tex;
                    else if (bestImg != null && currencyDone.Add(bestImg.GetInstanceID()))
                    {
                        // Sprites can't be created in this build, so the picture is laid over the original image, which is switched off.
                        var go = NewUiRaw("MC_IngotIcon");
                        go.transform.SetParent(bestImg.transform, false);
                        var rt = go.GetComponent<RectTransform>();
                        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                        var raw = go.GetComponent<UnityEngine.UI.RawImage>();
                        raw.texture = tex; raw.raycastTarget = false;
                        bestImg.enabled = false;
                    }
                }
            }
            catch (Exception ex) { if (Time.time < 120f) Plugin.Logger.LogWarning("currency icons: " + ex.Message); }
        }

        private static GameObject NewUiRaw(string name)
        {
            var arr = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Type>(2);
            arr[0] = Il2CppInterop.Runtime.Il2CppType.From(typeof(RectTransform));
            arr[1] = Il2CppInterop.Runtime.Il2CppType.From(typeof(UnityEngine.UI.RawImage));
            return new GameObject(name, arr);
        }

        // Shady-guy shops and Moai shrines are replaced by free end chests (better loot, no price).
        private float nextShopScan;

        private void ReplaceShopsWithChests()
        {
            if (Time.time < nextShopScan) return;
            nextShopScan = Time.time + 2f;
            try
            {
                var shady = UnityEngine.Object.FindObjectsOfType<InteractableShadyGuy>();
                var moai = UnityEngine.Object.FindObjectsOfType<InteractableShrineMoai>();
                if (shady.Length == 0 && moai.Length == 0) return;
                Assets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest template = null;
                foreach (var c in UnityEngine.Object.FindObjectsOfType<Assets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest>())
                    if (c != null && c.gameObject.activeInHierarchy) { template = c; break; }
                if (template == null) return;   // wait until the map has one chest to copy
                foreach (var s in shady) SwapForChest(s.gameObject, template);
                foreach (var m in moai) SwapForChest(m.gameObject, template);
            }
            catch (Exception ex) { if (Time.time < 120f) Plugin.Logger.LogWarning("shop swap: " + ex.Message); }
        }

        private static void SwapForChest(GameObject old, Assets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest template)
        {
            if (old == null || !old.activeSelf) return;
            Vector3 pos = old.transform.position;
            Quaternion rot = old.transform.rotation;
            old.SetActive(false);
            var go = UnityEngine.Object.Instantiate(template.gameObject, pos, rot);
            go.name = "EnderChest(Clone)";
            var old2 = go.transform.Find("MC_Chest");
            if (old2 != null) { old2.name = "MC_Chest_old"; UnityEngine.Object.Destroy(old2.gameObject); }
            var chest = go.GetComponent<Assets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest>();
            if (chest != null) chest.chestType = Assets.Scripts.Inventory__Items__Pickups.Interactables.EChest.Free;
            Plugin.Logger.LogInfo("A " + old.name + " became a free end chest at " + pos);
        }

        // Weapon icons in the corner say "AUTO" next to their level once they fire on their own.
        private float nextAutoMark;

        private void MarkAutoWeaponsUi()
        {
            if (Time.time < nextAutoMark) return;
            nextAutoMark = Time.time + 1f;
            try
            {
                foreach (var tm in UnityEngine.Object.FindObjectsOfType<TMPro.TextMeshProUGUI>())
                {
                    if (tm == null || tm.text == null || !tm.text.StartsWith("LVL") || tm.text.Contains("AUTO") || tm.transform.parent == null) continue;
                    var ri = tm.transform.parent.GetComponentInChildren<UnityEngine.UI.RawImage>();
                    EWeapon w;
                    if (ri == null || !AutoWeapons.WeaponForIcon(ri.texture, out w) || !AutoWeapons.IsAuto(w)) continue;
                    tm.text = tm.text.TrimEnd() + " AUTO";
                }
            }
            catch (Exception ex) { if (Time.time < 120f) Plugin.Logger.LogWarning("auto mark: " + ex.Message); }
        }

        // Megabonk's own shield pickup draws a green bubble around the player; seen from inside it fills the screen, so it is hidden in first person.
        private float nextAuraScan;
        private Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<Shield> auras;

        private void HideShieldAura()
        {
            try
            {
                if (Time.time > nextAuraScan) { nextAuraScan = Time.time + 0.5f; auras = UnityEngine.Object.FindObjectsOfType<Shield>(); }
                if (auras == null) return;
                foreach (var a in auras)
                    if (a != null && a.renderer != null) a.renderer.enabled = !firstPerson;
            }
            catch { }
        }

        // The model was rebuilt (a new skin was chosen): point everything at the new one.
        public void SwapRig(SteveRig r)
        {
            rig = r;
            if (fp != null) { fp.Destroy(); fp = null; }
            if (invUi != null) invUi.skin = null;
            RefreshHeld();
        }

        // Where the hand closes on each item sprite, in sprite pixels from its centre: swords and tools by the handle, bows by the grip.
        private static Vector3 GripFor(ItemDef d)
        {
            if (d == null) return Vector3.zero;
            if (d.kind == ItemKind.Sword || d.kind == ItemKind.Axe || d.kind == ItemKind.Mace) return new Vector3(-4.5f, -4.5f, 0f);
            if (d.kind == ItemKind.Bow) return new Vector3(-1f, -1f, 0f);
            return Vector3.zero;
        }

        public void LearnEnchant(int which)
        {
            if (!Ench.Add(which)) return;
            McSound.Play("block.enchantment_table.use", 0.9f);
            Toast(Ench.Names[which] + " " + Ench.Roman(Ench.Lvl[which]));
        }

        // Reading an enchanted book from a chest grants the enchantment.
        private void ReadBook(ItemDef book)
        {
            try
            {
                if (book.ench < 0) return;
                if (Ench.Lvl[book.ench] >= Ench.MaxLevel) { Toast(Ench.Names[book.ench] + " is already maxed"); return; }
                // Goes through the game's tome inventory, so it shows up in the owned-tomes list with its name and level.
                var td = DataManager.Instance.GetTome(Ench.Tomes[book.ench]);
                var me = MyPlayer.Instance;
                if (td != null && me != null && me.inventory != null && me.inventory.tomeInventory != null)
                {
                    Patch_AddTome.Internal = true;
                    try { me.inventory.tomeInventory.AddTome(td, new Il2CppSystem.Collections.Generic.List<Assets.Scripts.Inventory__Items__Pickups.Stats.StatModifier>(), ERarity.Common); }
                    finally { Patch_AddTome.Internal = false; }
                }
                Ench.Add(book.ench);
                UseOne(selected);
                McSound.Play("block.enchantment_table.use", 0.9f);
                Toast(Ench.Names[book.ench] + " " + Ench.Roman(Ench.Lvl[book.ench]) + ": " + Ench.Blurbs[book.ench]);
            }
            catch (System.Exception e) { Plugin.Logger.LogWarning("read book: " + e.Message); }
        }
        public static bool GameBlocked
        {
            get
            {
                var i = Instance;
                bool ourUi = i != null && ((i.invUi != null && i.invUi.IsOpen) || (i.chestUi != null && i.chestUi.IsOpen));
                bool paused = Time.timeScale <= 0.001f && !ourUi;
                // Megabonk keeps its own clock and window stack: pause menu, level-up, chest and shrine windows all show up here.
                bool gamePaused = false, window = false, levelUp = false;
                try { gamePaused = Assets.Scripts.Utility.MyTime.paused; } catch { }
                try { window = WindowManager.HasOpenWindow(); } catch { }
                try { levelUp = LevelupScreen.isLevelingUp; } catch { }
                lastReason = (paused ? "timeScale " : "") + (gamePaused ? "MyTime.paused " : "") + (window ? "window " : "") + (levelUp ? "levelup " : "");
                return paused || gamePaused || window || levelUp;
            }
        }

        // Megabonk HP is in the hundreds; scale Minecraft half-hearts of damage into that range.
        public const float DamageScale = 6f;
        public const float BlocksToUnits = 2.5f; // Steve is ~4.5 units tall vs 1.8 blocks in Minecraft

        public readonly Inv inv = new Inv();
        public int selected;
        public PlayerRenderer playerRenderer;
        public SteveRig rig;

        private McHud hud;
        private McInventory invUi;
        private McAdmin adminUi;
        private McChestUi chestUi;
        private bool wasBlocked;
        private FirstPerson fp;
        private bool firstPerson = true;

        private float lastAttackTime = -10f;
        private float eatStart = -1f;
        private bool eatLocked;
        private float food = 20f;
        private float saturation = 5f;
        private float exhaustion;
        private float absorption, absorbUntil;
        private float strengthUntil, speedUntil;
        private float itemNameUntil;
        private bool camSaved, fpLogged;
        private float savedDefaultZ, savedExtra;
        private Vector3 savedOffset;
        private float pearlReadyAt;
        private readonly List<Pearl> pearls = new List<Pearl>();
        private float xpProgress;
        private int xpLevel;
        private int kills;
        private float armorPoints, armorToughness;

        private ItemDef Held { get { return inv.defs[selected]; } }
        private ItemDef Offhand { get { return inv.defs[Inv.Offhand]; } }

        private void Awake()
        {
            Instance = this;
            McAssets.Init();
            ItemLibrary.Build();
            try { Assets.Scripts.Inventory__Items__Pickups.GoldAndMoney.MoneyUtility.chestBasePrice = 10; } catch { }
            hud = new McHud();
            invUi = new McInventory(inv, OnInventoryChanged);
            LevelFilter.Apply();
            Ench.Reset();
            RecomputeArmor();
            itemNameUntil = Time.time + 3f;
            McSound.Init();
            McAdmin.LoadVolume();
            adminUi = new McAdmin();
            chestUi = new McChestUi(inv, OnInventoryChanged, GiveItem);
            Plugin.Logger.LogInfo("MinecraftMode active");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            LevelFilter.Restore();
            EnemySkins.RestoreAll();
            AutoWeapons.Clear();
            if (hud != null) hud.Destroy();
            if (invUi != null) invUi.Destroy();
            if (adminUi != null) adminUi.Destroy();
            McTerrain.Restore();
            if (chestUi != null) chestUi.Destroy();
            if (fp != null) fp.Destroy();
        }

        private PlayerHealth Health
        {
            get
            {
                var p = MyPlayer.Instance;
                if (p == null || p.inventory == null) return null;
                return p.inventory.playerHealth;
            }
        }

        // ---------------------------------------------------------------- inventory

        private readonly ItemDef[] lastArmor = new ItemDef[4];

        private void OnInventoryChanged()
        {
            RecomputeArmor();
            RefreshHeld();
            // Equip sound when a piece of armor (or the elytra) is put on.
            for (int i = 0; i < 4; i++)
            {
                var d = inv.defs[36 + i];
                if (d != null && d != lastArmor[i])
                {
                    string ev = d.kind == ItemKind.Elytra ? "item.armor.equip_elytra"
                              : "item.armor.equip_" + (d.tier == "netherite" ? "netherite" : d.tier == "chainmail" ? "chain" : d.tier == "iron" ? "iron" : d.tier == "gold" ? "gold" : "diamond");
                    McSound.Play(ev);
                }
                lastArmor[i] = d;
            }
        }

        private void RecomputeArmor()
        {
            armorPoints = 0f; armorToughness = 0f;
            for (int i = 36; i < 40; i++)
                if (inv.defs[i] != null && inv.defs[i].kind == ItemKind.Armor) { armorPoints += inv.defs[i].armorPoints; armorToughness += inv.defs[i].toughness; }
        }

        private bool HasElytra { get { var c = inv.defs[37]; return c != null && c.kind == ItemKind.Elytra; } }

        private void RefreshHeld()
        {
            eatStart = -1f;
            bowDrawing = false;
            if (rig != null)
            {
                bool hasShield = (Held != null && Held.kind == ItemKind.Shield) || (Offhand != null && Offhand.kind == ItemKind.Shield);
                rig.SetHeld(Held != null && Held.kind != ItemKind.Shield ? Held.icon : null, GripFor(Held));
                rig.SetShield(hasShield);
                rig.SetElytra(HasElytra);
                rig.SetArmor(new[] { inv.defs[36], inv.defs[37], inv.defs[38], inv.defs[39] });
            }
            if (fp != null)
            {
                fp.SetHeld(Held != null && Held.kind != ItemKind.Shield ? Held.icon : null);
                fp.SetOffhand(Offhand != null ? Offhand.icon : null, (Held != null && Held.kind == ItemKind.Shield) || (Offhand != null && Offhand.kind == ItemKind.Shield));
            }
        }

        // Removes one from a slot (food, pearls, rockets...).
        private void UseOne(int slot)
        {
            if (inv.defs[slot] == null) return;
            inv.cnts[slot]--;
            if (inv.cnts[slot] <= 0) inv.Set(slot, null, 0);
            if (slot == selected) RefreshHeld();
        }

        // Totem of Undying in either hand: cancels a lethal hit, leaves half a heart, then regeneration II (45 s)
        // and absorption II (5 s), exactly like vanilla. The totem is used up.
        private float regenUntil, regenTimer, satHealUntil;

        public bool TryTotem(PlayerHealth h, DamageContainer dc, bool ignoreShield)
        {
            if (h == null || dc == null) return false;
            int slot = -1;
            if (Held != null && Held.kind == ItemKind.Totem) slot = selected;
            else if (Offhand != null && Offhand.kind == ItemKind.Totem) slot = Inv.Offhand;
            if (slot < 0) return false;
            if (!h.WillDamageKill(dc, ignoreShield)) return false;
            UseOne(slot);
            RefreshHeld();
            h.hp = h.maxHp;                                  // full health
            h.overheal = 8f / 20f * h.maxHp;                 // and four golden hearts
            absorption = 8f; absorbUntil = Time.time + 30f;
            regenUntil = Time.time + 45f; regenTimer = 0f;
            if (hud != null) hud.PopTotem(ItemLibrary.Totem.icon);
            McSound.Play("item.totem.use");
            Plugin.Logger.LogInfo("Totem of Undying used");
            return true;
        }

        // Right click with armor or elytra in hand equips it (swapping whatever is worn), as in vanilla.
        private void TryEquipHeld()
        {
            var d = Held;
            if (d == null || (d.kind != ItemKind.Armor && d.kind != ItemKind.Elytra)) return;
            int target = d.kind == ItemKind.Elytra ? 37 : 36 + d.armorSlot;
            var od = inv.defs[target]; int oc = inv.cnts[target];
            inv.Set(target, d, 1);
            inv.Set(selected, od, oc);
            OnInventoryChanged();
        }

        // Armor reduces incoming enemy damage (simplified vanilla: 4% per armor point, max 80%).
        public void ApplyArmor(DamageContainer dc)
        {
            if (dc == null || dc.enemy == null) return;
            // Every armor point cuts about 8% of what is left, and toughness a little more: a single chainmail helmet already shows,
            // full iron takes about a third of the damage, full netherite about a sixth.
            float taken = Mathf.Pow(0.92f, armorPoints) * Mathf.Max(0.6f, 1f - 0.015f * armorToughness);
            dc.damage *= Mathf.Max(0.03f, taken * Ench.DamageTakenMul);   // never less than 3% gets through
        }

        // ---------------------------------------------------------------- view

        private void EnsureView()
        {
            var pc = PlayerCamera.Instance;
            if (pc == null || pc.camera == null) return;

            // Stock orbit camera, with its follow distance collapsed to zero for first person and restored
            // for third person (the game's own Player1st state doesn't move the camera).
            if (pc.GetCameraState() == PlayerCamera.ECameraState.Player1st)
                pc.SetCameraState(PlayerCamera.ECameraState.Player3rd);
            if (!camSaved)
            {
                camSaved = true;
                savedDefaultZ = pc.defaultZ; savedOffset = pc.offset3rdPerson; savedExtra = pc.maxExtraZoomoutDistance;
                baseFov = pc.camera.fieldOfView;
            }
            if (firstPerson)
            {
                pc.defaultZ = 0f; pc.currentZ = 0f; pc.offset3rdPerson = new Vector3(0f, -SneakDrop, 0f); pc.maxExtraZoomoutDistance = 0f;
            }
            else
            {
                pc.defaultZ = savedDefaultZ; pc.offset3rdPerson = savedOffset; pc.maxExtraZoomoutDistance = savedExtra;
            }

            if (fp == null && !fpLogged)
            {
                fpLogged = true;
                Plugin.Dbg("fp precheck rig=" + (rig != null) + " itemMat=" + (rig != null && rig.itemMat != null) + " skinTex=" + (rig != null && rig.skinTex != null));
            }
            if (fp == null && rig != null && rig.itemMat != null && rig.skinTex != null)
            {
                fp = new FirstPerson(pc.camera.transform, rig.itemMat, rig.skinTex, rig.slim);
                RefreshHeld();
                Plugin.Logger.LogInfo("view model created under " + pc.camera.name);
            }
            if (fp != null) fp.SetVisible(firstPerson);
            if (rig != null) rig.gameObject.SetActive(!firstPerson);
        }

        private float baseFov, fovBoost;

        private void LateUpdate()
        {
            if (Time.timeScale <= 0f) return;
            EnsureView();
            bool moving = playerRenderer != null && playerRenderer.moving;

            // Eat / drink progress for the view model (vanilla counts down the remaining use time).
            float eatFrac = -1f, eatTicksLeft = 0f;
            if (eatStart > 0f && Held != null)
            {
                float dur = Held.eatTime > 0f ? Held.eatTime : 1.6f;
                float elapsed = Time.time - eatStart;
                eatFrac = Mathf.Clamp01(1f - elapsed / dur);
                eatTicksLeft = (dur - elapsed) * 20f;
            }
            if (fp != null)
            {
                var item = Weapon;
                // Vanilla: the held item's height tracks attackStrength^3 while the same item is held.
                fp.targetHeight = IsAttackItem(item) ? Mathf.Pow(AttackStrength(item), 3f) : 1f;
                fp.sprinting = Sprinting;
                fp.Tick(moving, Blocking, eatFrac, eatTicksLeft);
            }
            UpdateThirdPerson();
            // Bow draw: sprite stage, view-model pose and FOV squeeze all follow the draw time.
            float drawSecs = bowDrawing ? Time.time - bowStart : 0f;
            if (bowDrawing)
            {
                int stage = drawSecs >= 0.9f ? 2 : (drawSecs >= 0.65f ? 1 : 0);
                if (stage != bowStage)
                {
                    bowStage = stage;
                    var b = Held;
                    if (fp != null && b != null && b.pullIcons != null && b.pullIcons[stage] != null) fp.SetHeldIcon(b.pullIcons[stage]);
                }
            }
            if (fp != null) { fp.bowing = bowDrawing; fp.bowPower = bowDrawing ? BowPower(drawSecs) : 0f; fp.bowTicks = drawSecs * 20f; }
            if (rig != null) { rig.aiming = bowDrawing; rig.blocking = Blocking; }

            if (rig != null)
            {
                rig.SetGliding(gliding);
                // Third-person pose: flat along the look direction while gliding, forward lean when crouched.
                float tiltTarget = 0f;
                if (gliding && Camera.main != null)
                {
                    float pitchDown = -Mathf.Asin(Mathf.Clamp(Camera.main.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
                    tiltTarget = Mathf.Clamp(90f + pitchDown, 25f, 175f);
                }
                rig.tiltTarget = tiltTarget;
                rig.crouchTarget = (Sneaking && !gliding) ? 28f : 0f;
            }

            // Sprint widens the FOV like vanilla. Only the FOV is touched; the camera transform is left to the game.
            var pc = PlayerCamera.Instance;
            if (firstPerson && pc != null && pc.camera != null && baseFov > 0f)
            {
                fovBoost = Mathf.MoveTowards(fovBoost, (Sprinting || gliding) ? 1f : 0f, Time.deltaTime * 6f);
                float bowSqueeze = bowDrawing ? 0.04f * (drawSecs >= 1f ? 1f : drawSecs * drawSecs) : 0f;
                pc.camera.fieldOfView = baseFov * (1f + 0.18f * fovBoost) * (1f - bowSqueeze);
            }
        }

        // Third-person body: limbs follow the real speed, the head follows the camera and the body turns with it
        // (always while moving or gliding; while standing only once the head is turned past 50 degrees).
        private float bodyYaw;
        private bool bodyYawInit;

        private void UpdateThirdPerson()
        {
            if (rig == null) return;
            var pl = MyPlayer.Instance;
            float hSpeed = 0f;
            if (pl != null && pl.playerMovement != null && pl.playerMovement.rb != null)
            {
                var v = pl.playerMovement.rb.velocity;
                hSpeed = new Vector2(v.x, v.z).magnitude / VelToUnits;
            }
            rig.moveSpeed = hSpeed;
            rig.flying = gliding;
            rig.crouching = Sneaking && !gliding;

            var cam = Camera.main;
            if (firstPerson || cam == null || playerRenderer == null) { bodyYawInit = false; rig.headPitch = 0f; rig.headYaw = 0f; return; }

            float camYaw = cam.transform.eulerAngles.y;
            float pitchDown = -Mathf.Asin(Mathf.Clamp(cam.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (!bodyYawInit)
            {
                bodyYawInit = true;
                bodyYaw = playerRenderer.rendererObject != null ? playerRenderer.rendererObject.transform.eulerAngles.y : camYaw;
            }
            bool moving = hSpeed > 0.02f || gliding || bowDrawing;
            if (moving) bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, camYaw, (gliding ? 900f : 540f) * Time.deltaTime);
            else
            {
                float d = Mathf.DeltaAngle(bodyYaw, camYaw);
                if (Mathf.Abs(d) > 50f) bodyYaw = camYaw - Mathf.Sign(d) * 50f;
            }
            float yawRad = bodyYaw * Mathf.Deg2Rad;
            playerRenderer.ForceRotation(new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad)));
            rig.headYaw = Mathf.Clamp(Mathf.DeltaAngle(bodyYaw, camYaw), -50f, 50f);
            rig.headPitch = gliding ? 0f : Mathf.Clamp(pitchDown, -80f, 80f);
        }

        // ---------------------------------------------------------------- per-frame logic

        private bool jumpPressed;
        private float nextPitchLog, pitchMin = 999f, pitchMax = -999f;
        private float lastWTap = -10f;
        private bool sprintLatched;

        private void Update()
        {
            McSound.Tick();
            bool blockedNow = GameBlocked;
            McSound.Paused = blockedNow;
            if (blockedNow != wasBlocked) { wasBlocked = blockedNow; Plugin.Dbg("Game blocked=" + blockedNow + " (" + lastReason + ")"); }
            TickHud();

            // Chest screen: same idea as the inventory (time paused, mouse free).
            TickBossNames();
            HideShieldAura();
            if (LevelFilter.ItemsActive && Time.time > LevelFilter.ItemsOnAt + 1.5f && !WindowManager.HasOpenWindow()) LevelFilter.ItemsOff();
            if (pendingChest != null && Time.time >= pendingAt) { var pc = pendingChest; pendingChest = null; chestUi.Open(pc); trackedChest = pc; sawChestOpen = true; }
            // A free end chest is used up: after its screen closes it vanishes in a puff of smoke.
            if (sawChestOpen && !chestUi.IsOpen)
            {
                sawChestOpen = false;
                try
                {
                    var host = trackedChest != null ? trackedChest.transform.parent : null;
                    var ic = host != null ? host.GetComponent<Assets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest>() : null;
                    if (ic != null && (ic.chestType == Assets.Scripts.Inventory__Items__Pickups.Interactables.EChest.Free || ic.chestType == Assets.Scripts.Inventory__Items__Pickups.Interactables.EChest.FreeCrypt))
                    {
                        Fx.Puffs(host.position + Vector3.up * 1.2f, new Color(0.6f, 0.3f, 0.9f), 10, 5f, 2.2f);
                        McSound.PlayAt("entity.enderman.teleport", host.position, 0.8f);
                        host.gameObject.SetActive(false);
                    }
                }
                catch (Exception ex) { Plugin.Logger.LogWarning("end chest vanish: " + ex.Message); }
                trackedChest = null;
            }
            if (chestUi.IsOpen)
            {
                chestUi.Tick();
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape)) { if (Input.GetKeyDown(KeyCode.Escape)) EscGraceUntil = Time.unscaledTime + 0.4f; chestUi.Close(); }
                return;
            }

            // Admin panel (F8): the game keeps running behind it, but the player does not react to the keyboard or mouse.
            if (Input.GetKeyDown(KeyCode.F8) && !invUi.IsOpen) adminUi.Toggle();
            if (adminUi.IsOpen)
            {
                adminUi.Tick();
                TickPearls(); TickArrows(); AutoWeapons.TickEffects();
                return;
            }

            // Inventory screen (E). Time is paused while it's open, so this runs on real frames.
            if (invUi.IsOpen)
            {
                invUi.Tick();
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape)) { if (Input.GetKeyDown(KeyCode.Escape)) EscGraceUntil = Time.unscaledTime + 0.4f; invUi.Close(); }
                return;
            }
            if (Time.timeScale <= 0f || GameBlocked || SkinPicker.IsOpen) return;   // paused or in a menu: no swings, shots or items
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (invUi.skin == null && rig != null) invUi.skin = rig.skinTex;
                invUi.Open();
                return;
            }

            // Level-up mode: L gives one level, K gives five (each opens the card screen), F7 toggles the
            // Minecraft-only card filter.
            if (Plugin.DebugOn && Input.GetKeyDown(KeyCode.L)) GiveLevels(1);   // cheat keys only with the debug file
            if (Plugin.DebugOn && Input.GetKeyDown(KeyCode.K)) GiveLevels(5);
            if (Input.GetKeyDown(KeyCode.F7))
            {
                if (LevelFilter.Applied) LevelFilter.Restore(); else LevelFilter.Apply();
                Plugin.Logger.LogInfo("Minecraft-only cards " + (LevelFilter.Applied ? "ON" : "OFF"));
            }
            if (Input.GetKeyDown(KeyCode.F5)) firstPerson = !firstPerson;
            if (Input.GetKeyDown(KeyCode.F6)) { mcMovement = !mcMovement; Plugin.Logger.LogInfo("Minecraft movement " + (mcMovement ? "ON" : "OFF")); }
            HandleHotbarInput();
            if (Input.GetMouseButtonUp(1)) rmbConsumed = false;
            if (Input.GetMouseButtonDown(1)) rmbConsumed = TryInteractRightClick();
            SkinExistingChests();
            MarkAutoWeaponsUi();
            ReplaceShopsWithChests();
            McTerrain.Tick();
            if (hud != null) hud.SetPrompt(InteractPrompt());
            try { var dix = MyPlayer.Instance.playerInput.detectInteractables; if (dix != null && dix.t_interact != null) { dix.t_interact.enabled = false; dix.t_interact.alpha = 0f; } } catch { }

            // Key events for the movement tick, which can't see GetKeyDown reliably.
            if (Input.GetKeyDown(KeyCode.Space)) jumpPressed = true;
            if (Input.GetKeyDown(KeyCode.W)) { if (Time.time - lastWTap < 0.3f) sprintLatched = true; lastWTap = Time.time; }
            if (Input.GetKeyDown(KeyCode.LeftControl)) sprintLatched = true;

            var held = Held;
            TrackFall();
            if (Time.time > nextPitchLog)
            {
                nextPitchLog = Time.time + 5f;
                var plp = MyPlayer.Instance;
                if (plp != null && plp.playerInput != null)
                {
                    float px = plp.playerInput.cameraRotation.x;
                    if (px < pitchMin) pitchMin = px;
                    if (px > pitchMax) pitchMax = px;
                    Plugin.Dbg(McSound.Stats());
                    Plugin.Dbg("camera pitch now=" + px.ToString("0.0") + " min=" + pitchMin.ToString("0.0") + " max=" + pitchMax.ToString("0.0")
                        + " desired=" + plp.playerInput.desiredCameraRotation.x.ToString("0.0"));
                }
            }

            // Eating / drinking: hold right click; each use takes one item from the stack.
            if (held != null && (held.kind == ItemKind.Food || held.kind == ItemKind.Potion))
            {
                if (Input.GetMouseButtonUp(1)) { eatStart = -1f; eatLocked = false; }
                if (RmbDown && !eatLocked) { eatStart = Time.time; nextEatSound = Time.time + 0.2f; }
                // Chewing / gulping sounds repeat while the item is being used.
                if (eatStart > 0f && Time.time >= nextEatSound)
                {
                    nextEatSound = Time.time + 0.25f;
                    McSound.Play(held.kind == ItemKind.Potion ? "entity.generic.drink" : "entity.generic.eat", 0.8f, 0.9f + UnityEngine.Random.value * 0.2f);
                }
                if (eatStart > 0f && Time.time - eatStart >= (held.eatTime > 0f ? held.eatTime : 1.6f))
                {
                    ConsumeHeld();
                    eatStart = -1f;
                    eatLocked = true; // vanilla needs a fresh press for the next one
                }
            }
            else { eatStart = -1f; eatLocked = false; }

            if (held != null && held.kind == ItemKind.Pearl && RmbDown && Time.time >= pearlReadyAt)
            {
                pearlReadyAt = Time.time + 10f;
                ThrowPearl();
                AutoWeapons.Event(AutoWeapons.Ev.Item);
                UseOne(selected);
            }
            TickPearls();

            if (held != null && held.kind == ItemKind.Rocket && RmbDown && gliding)
            {
                rocketUntil = Time.time + 1.3f;
                McSound.Play("entity.firework_rocket.launch");
                AutoWeapons.Event(AutoWeapons.Ev.Item);
                UseOne(selected);
            }

            if (held != null && held.kind == ItemKind.Tome && RmbDown) ReadBook(held);

            if (held != null && (held.kind == ItemKind.Armor || held.kind == ItemKind.Elytra) && RmbDown)
                TryEquipHeld();

            // Mending: slow regeneration scaled by max health.
            if (Ench.Lvl[Ench.Mending] > 0)
            {
                var mh = Health;
                if (mh != null && mh.hp < mh.maxHp)
                {
                    mendAcc += mh.maxHp * Ench.RegenPerSecond * Time.deltaTime;
                    if (mendAcc >= 1f) { int add = (int)mendAcc; mendAcc -= add; mh.hp = Mathf.Min(mh.maxHp, mh.hp + add); }
                }
            }

            // Regeneration II from the totem: half a heart every 25 ticks.
            if (Time.time < regenUntil || Time.time < satHealUntil)
            {
                regenTimer += Time.deltaTime;
                if (regenTimer >= 1.25f)
                {
                    regenTimer -= 1.25f;
                    var rh = Health;
                    if (rh != null) rh.hp = Mathf.Min(rh.maxHp, rh.hp + Mathf.Max(1, rh.maxHp / 20));
                }
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                var d = inv.defs[selected]; int c = inv.cnts[selected];
                inv.Set(selected, inv.defs[Inv.Offhand], inv.cnts[Inv.Offhand]);
                inv.Set(Inv.Offhand, d, c);
                AutoWeapons.Event(AutoWeapons.Ev.Item);
                RefreshHeld();
            }
            bool shieldInOff = Offhand != null && Offhand.kind == ItemKind.Shield;
            bool mainUsesRmb = held != null && (held.kind == ItemKind.Food || held.kind == ItemKind.Potion
                                                 || held.kind == ItemKind.Pearl || held.kind == ItemKind.Rocket
                                                 || held.kind == ItemKind.Bow || held.kind == ItemKind.Tome);

            // Bow: hold right click to draw (needs an arrow somewhere in the inventory), release to shoot.
            if (held != null && held.kind == ItemKind.Bow)
            {
                if (RmbDown && !bowDrawing && FindArrowSlot() >= 0)
                {
                    bowDrawing = true; bowStart = Time.time; bowStage = 0;
                    if (fp != null && held.pullIcons != null && held.pullIcons[0] != null) fp.SetHeldIcon(held.pullIcons[0]);
                }
                if (bowDrawing && !(Input.GetMouseButton(1) && !rmbConsumed)) ReleaseBow(held);
            }
            else if (bowDrawing) bowDrawing = false;
            TickArrows();
            if (shieldHits > 0f && Time.time > shieldRegenAt) { shieldHits = Mathf.Max(0f, shieldHits - 1f); shieldRegenAt = Time.time + 1.2f; }
            Blocking = Time.time >= shieldBrokenUntil && (Input.GetMouseButton(1) && !rmbConsumed) && ((held != null && held.kind == ItemKind.Shield) || (shieldInOff && !mainUsesRmb));
            McSound.Loop("item.elytra.flying", gliding, 0.7f);
            ClearStartingWeapons();
            if (rig != null && rig.itemMat != null) AutoWeapons.baseMat = rig.itemMat;
            AutoWeapons.TickEffects();

            if (Input.GetMouseButtonDown(0) && !bowDrawing && !Blocking && eatStart < 0f) TryAttack();

            // Hunger: exhaustion converts saturation then food, as vanilla does.
            exhaustion += Time.deltaTime * 0.04f;
            if (exhaustion >= 4f)
            {
                exhaustion -= 4f;
                if (saturation > 0f) saturation = Mathf.Max(0f, saturation - 1f);
                else food = Mathf.Max(0f, food - 1f);
            }

            // Absorption: lasts 2 minutes, is set (not stacked) by golden apples and is eaten by damage first.
            var h = Health;
            if (absorption > 0f)
            {
                if (Time.time > absorbUntil) { absorption = 0f; if (h != null) h.overheal = 0f; }
                else if (h != null && h.maxHp > 0)
                {
                    float expected = absorption / 20f * h.maxHp;
                    if (h.overheal < expected - 0.5f) absorption = h.overheal / h.maxHp * 20f;
                    else h.overheal = Mathf.Max(h.overheal, expected);
                }
            }
        }

        private void TickHud()
        {
            if (hud == null) return;
            // No HUD or view model during the portal intro / death cameras.
            var pc = PlayerCamera.Instance;
            bool cinematic = pc != null && (pc.GetCameraState() == PlayerCamera.ECameraState.Portal
                                            || pc.GetCameraState() == PlayerCamera.ECameraState.Death);
            hud.SetVisible(!cinematic);
            if (fp != null) fp.SetVisible(firstPerson && !cinematic);
            if (cinematic) return;
            var h = Health;
            float hearts = 20f, abs = 0f;
            if (h != null && h.maxHp > 0)
            {
                hearts = Mathf.Clamp(h.hp / (float)h.maxHp * 20f, 0f, 20f);
                abs = Mathf.Clamp(absorption, 0f, 20f);
            }
            hud.SetOffhand(Offhand != null ? Offhand.icon : null, inv.cnts[Inv.Offhand]);
            float pearlLeft = Mathf.Clamp01((pearlReadyAt - Time.time) / 10f);
            for (int i = 0; i < 9; i++) hud.cooldown[i] = (inv.defs[i] != null && inv.defs[i].kind == ItemKind.Pearl) ? pearlLeft : 0f;
            var item = Weapon;
            var pl = MyPlayer.Instance;
            hud.targetInReach = false;
            if (IsAttackItem(item) && pl != null && Camera.main != null)
            {
                Vector3 origin = pl.transform.position + Vector3.up * (pl.height * 0.5f);
                hud.targetInReach = FindTarget(origin, Camera.main.transform.forward, 3f * BlocksToUnits) != null;
            }
            float s = IsAttackItem(item) ? AttackStrength(item) : 1f;
            float nameAlpha = Mathf.Clamp01((itemNameUntil - Time.time) * 2f);
            // The bar is Megabonk's own experience: same level, same progress to the next one.
            try
            {
                var gx = pl != null && pl.inventory != null ? pl.inventory.playerXp : null;
                if (gx != null) { xpLevel = gx.level; xpProgress = Inventory__Items__Pickups.XpUtility.CurrentLevelProgress(gx.xp); }
            }
            catch { }
            var effs = new List<KeyValuePair<string, float>>();
            if (absorption > 0f && absorbUntil > Time.time) effs.Add(new KeyValuePair<string, float>("absorption", absorbUntil - Time.time));
            if (regenUntil > Time.time) effs.Add(new KeyValuePair<string, float>("regeneration", regenUntil - Time.time));
            if (satHealUntil > Time.time) effs.Add(new KeyValuePair<string, float>("saturation", satHealUntil - Time.time));
            if (strengthUntil > Time.time) effs.Add(new KeyValuePair<string, float>("strength", strengthUntil - Time.time));
            if (speedUntil > Time.time) effs.Add(new KeyValuePair<string, float>("speed", speedUntil - Time.time));
            hud.SetEffects(effs);
            hud.Tick(inv.defs, inv.cnts, selected, hearts, abs, food, armorPoints, xpProgress, xpLevel, s, IsAttackItem(item),
                     Held != null ? Held.name : "", nameAlpha);
        }

        private void HandleHotbarInput()
        {
            for (int i = 0; i < 9; i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) Select(i);
            float scroll = Input.mouseScrollDelta.y;
            if (scroll > 0.1f) Select((selected + 8) % 9);
            else if (scroll < -0.1f) Select((selected + 1) % 9);
        }

        private void Select(int i)
        {
            if (i == selected) return;
            selected = i;
            AutoWeapons.Event(AutoWeapons.Ev.Item);
            itemNameUntil = Time.time + 2f;
            RefreshHeld();
        }

        // What the left click swings with: a real weapon, or bare fists for anything else (food, armor, tools...).
        private ItemDef Weapon { get { var h = Held; return IsAttackItem(h) ? h : ItemLibrary.Fist; } }

        private static bool IsAttackItem(ItemDef d)
        {
            return d != null && (d.kind == ItemKind.Sword || d.kind == ItemKind.Axe || d.kind == ItemKind.Mace);
        }

        // Attack strength 0..1 exactly as Minecraft computes it (ticks since last swing / cooldown ticks).
        private float AttackStrength(ItemDef d)
        {
            float cooldownTicks = 20f / Mathf.Max(0.1f, d.attackSpeed * Ench.SwingSpeedMul);
            float ticks = (Time.time - lastAttackTime) * 20f;
            return Mathf.Clamp01((ticks + 0.5f) / cooldownTicks);
        }

        private bool PlayerFalling()
        {
            var p = MyPlayer.Instance;
            if (p == null || p.playerMovement == null) return false;
            var pm = p.playerMovement;
            return !pm.grounded && pm.rb != null && pm.rb.velocity.y < -0.1f;
        }

        // ---------------------------------------------------------------- combat

        private void TryAttack()
        {
            var item = Weapon;
            var player = MyPlayer.Instance;
            if (player == null) return;

            float s = AttackStrength(item);
            lastAttackTime = Time.time;
            if (rig != null) rig.Swing();
            if (fp != null) fp.Swing();

            Vector3 origin = player.transform.position + Vector3.up * (player.height * 0.5f);
            Vector3 aim = Camera.main != null ? Camera.main.transform.forward : player.transform.forward;
            Vector3 flatAim = new Vector3(aim.x, 0f, aim.z);
            if (flatAim.sqrMagnitude < 0.001f) flatAim = player.transform.forward;
            flatAim.Normalize();
            if (playerRenderer != null) playerRenderer.ForceRotation(flatAim);

            float reach = 3f * BlocksToUnits;
            Enemy target = FindTarget(origin, aim, reach);
            if (target == null)
            {
                exhaustion += 0.1f;
                McSound.Play("entity.player.attack.weak");   // the swing whoosh
                return;
            }

            // Damage formula from 1.21: base * (0.2 + s^2 * 0.8), critical +50% when falling at >84.8% charge.
            float dmg = item.damage;
            if (Time.time < strengthUntil) dmg += 3f;
            dmg *= 0.2f + s * s * 0.8f;
            dmg *= Ench.MeleeMul;
            bool crit = s > 0.848f && PlayerFalling();
            if (crit) dmg *= 1.5f;

            // Mace smash (vanilla 1.21): needs a fall of more than 1.5 blocks; +4 damage per block for the first 3,
            // +2 for the next 5 and +1 beyond that. The fall damage is cancelled by the hit.
            bool smash = false;
            if (item.kind == ItemKind.Mace && fallBlocks > 1.5f && !gliding)
            {
                float fb = fallBlocks;
                // The higher the fall, the harder it lands, and it keeps accelerating: about +30 at 3 blocks, +125 at 8, +360 at 15.
                dmg += fb * 6f + fb * fb * 1.2f;
                smash = true;
                Plugin.Logger.LogInfo("Mace smash from " + fb.ToString("0.0") + " blocks");
            }

            Hit(target, dmg, flatAim, crit, s);
            AutoWeapons.Event(AutoWeapons.Ev.Hit);
            if (s > 0.848f) AutoWeapons.Event(AutoWeapons.Ev.Charged);
            if (crit) AutoWeapons.Event(AutoWeapons.Ev.Crit);
            if (target.IsDead()) AutoWeapons.Event(AutoWeapons.Ev.Kill);
            if (Time.time - lastComboHit > 3f) comboCount = 0;
            lastComboHit = Time.time;
            if (++comboCount >= 5) { comboCount = 0; AutoWeapons.Event(AutoWeapons.Ev.Combo); }
            // Vanilla hit sounds: crit, strong (charged), weak (rushed), plus knockback when sprinting.
            McSound.Play(crit ? "entity.player.attack.crit" : (s > 0.9f ? "entity.player.attack.strong" : "entity.player.attack.weak"));
            if (Sprinting && s > 0.9f) McSound.Play("entity.player.attack.knockback");
            if (smash) McSound.Play(fallBlocks > 4f ? "item.mace.smash_ground" : "item.mace.smash_air");
            if (smash)
            {
                smashProtectUntil = Time.time + 1.5f;
                // The shockwave hurts everything within 4 blocks of the target, less the farther it is.
                {
                    Vector3 sc = target.transform.position;
                    float rad = 4f * BlocksToUnits;
                    var seenS = new HashSet<int> { target.GetInstanceID() };
                    foreach (var col in Physics.OverlapSphere(sc, rad))
                    {
                        var se = col.GetComponentInParent<Enemy>();
                        if (se == null || se.IsDead() || !seenS.Add(se.GetInstanceID())) continue;
                        float fall = 1f - Mathf.Clamp01(Vector3.Distance(se.transform.position, sc) / rad) * 0.6f;
                        Hit(se, dmg * 0.7f * fall, (se.transform.position - sc).normalized, false, 1f);
                    }
                    Fx.Puffs(sc + Vector3.up * 0.5f, new Color(0.55f, 0.5f, 0.45f), 10, 6f, 2.2f);
                }
                peakY = player.transform.position.y;
                fallBlocks = 0f;
            }

            // Sweeping edge: swords at full charge hit nearby enemies for 1 damage.
            if (item.kind == ItemKind.Sword && s > 0.848f && !crit)
            {
                McSound.Play("entity.player.attack.sweep");
                Vector3 c = target.transform.position;
                var hits = Physics.OverlapSphere(c, 1.0f * BlocksToUnits);
                var seen = new HashSet<int> { target.GetInstanceID() };
                foreach (var col in hits)
                {
                    var e = col.GetComponentInParent<Enemy>();
                    if (e == null || !seen.Add(e.GetInstanceID()) || e.IsDead()) continue;
                    Hit(e, 1f, (e.transform.position - origin).normalized, false, 1f);
                }
            }
            exhaustion += 0.1f;
        }

        private static Enemy FindTarget(Vector3 origin, Vector3 aim, float reach)
        {
            Enemy best = null;
            float bestScore = float.MaxValue;
            var cols = Physics.OverlapSphere(origin, reach + 1.5f);
            foreach (var col in cols)
            {
                var e = col.GetComponentInParent<Enemy>();
                if (e == null || e.IsDead()) continue;
                Vector3 to = e.GetCenterPosition() - origin;
                float dist = new Vector3(to.x, 0f, to.z).magnitude;
                if (dist > reach + 1.5f) continue;
                float angle = Vector3.Angle(new Vector3(aim.x, 0f, aim.z), new Vector3(to.x, 0f, to.z));
                if (angle > 45f) continue;
                float score = angle + dist * 2f;
                if (score < bestScore) { bestScore = score; best = e; }
            }
            return best;
        }

        private void Hit(Enemy enemy, float dmg, Vector3 dir, bool crit, float strength)
        {
            var dc = new DamageContainer(1f, "Steve");
            dc.damage = dmg * DamageScale;
            dc.crit = crit;
            dc.direction = dir;
            dc.knockback = (strength > 0.848f ? 6f : 2f) * Ench.KnockMul;
            dc.enemy = enemy;
            if (crit) CritGoldUntil = Time.time + 0.4f;
            enemy.DamageFromPlayerOther(dc);
            if (enemy.IsDead()) { kills++; McSound.Play("entity.experience_orb.pickup", 0.5f, 0.8f + UnityEngine.Random.value * 0.4f); }
        }

        // ---------------------------------------------------------------- interaction, loot and notices

        private bool rmbConsumed;
        private bool RmbDown { get { return Input.GetMouseButtonDown(1) && !rmbConsumed; } }

        // Everything the game opens with E (chests, shrines, portals...) also opens with a right click.
        private bool TryInteractRightClick()
        {
            try
            {
                var pl = MyPlayer.Instance;
                var di = pl != null && pl.playerInput != null ? pl.playerInput.detectInteractables : null;
                if (di == null || di.currentInteractable == null || !di.CanInteract()) return false;
                // Shrines and shops roll Megabonk items for their windows, so the base item pool is on while one is open.
                string kindName = di.currentInteractable.GetType().Name;
                if (!kindName.Contains("Chest")) { LevelFilter.ItemsOn(); Plugin.Logger.LogInfo("interact " + kindName + ": Megabonk items on"); }
                AllowInteract = true;
                try { di.TryInteract(); } finally { AllowInteract = false; }
                return true;
            }
            catch (Exception ex) { Plugin.Logger.LogWarning("interact: " + ex.Message); return false; }
        }

        private string InteractPrompt()
        {
            try
            {
                var pl = MyPlayer.Instance;
                var di = pl != null && pl.playerInput != null ? pl.playerInput.detectInteractables : null;
                if (di == null || di.currentInteractable == null) return "";
                string s = di.currentInteractable.GetInteractString();
                if (string.IsNullOrEmpty(s)) return "";
                s = System.Text.RegularExpressions.Regex.Replace(s, "<[^>]*>", "").Trim();
                return s;
            }
            catch { return ""; }
        }

        public void Toast(string s) { if (hud != null) hud.AddToast(s); }

        private static string CleanDesc(string d)
        {
            d = System.Text.RegularExpressions.Regex.Replace(d ?? "", "<[^>]*>", "").Replace((char)10, (char)32).Trim();
            return d.Length > 140 ? d.Substring(0, 137) + "..." : d;
        }

        public void RewardPair(Texture ia, string ta, string da, Color ca, Texture ib, string tb, string db, Color cb)
        {
            if (hud != null) hud.ShowRewardPair(ia, ta, CleanDesc(da), ca, ib, tb, CleanDesc(db), cb);
        }

        public void Reward(Texture icon, string title, string desc, Color c)
        {
            if (hud == null) return;
            desc = System.Text.RegularExpressions.Regex.Replace(desc ?? "", "<[^>]*>", "").Replace((char)10, (char)32).Trim();
            if (desc.Length > 110) desc = desc.Substring(0, 107) + "...";
            hud.ShowReward(icon, title, desc, c);
        }

        // A shove without damage (skeleton arrows): added to the walking velocity and lifts the player off the ground a moment.
        private float forceAirUntil;
        public void AddKnockback(Vector3 dir, float blocksPerTick)
        {
            if (Blocking) { McSound.Play("item.shield.block", 0.8f); return; }
            Vector3 d = new Vector3(dir.x, 0f, dir.z);
            if (d.sqrMagnitude < 0.001f) return;
            d.Normalize();
            mcVel += d * blocksPerTick;
            mcVel.y = Mathf.Max(mcVel.y, 0.28f);
            forceAirUntil = Time.time + 0.15f;
            McSound.Play("entity.player.hurt", 0.4f, 1.2f);
        }

        public void OpenChestUi(McChest c, Vector3 at)
        {
            if (chestUi == null || c == null || pendingChest != null) return;
            // The lid swings up first; the chest screen follows once it is mostly open.
            c.Open();
            McSound.PlayAt("block.chest.open", at, 1f, 0.9f + UnityEngine.Random.value * 0.1f);
            pendingChest = c; pendingAt = Time.time + 0.45f;
            Plugin.Logger.LogInfo("chest ui queued");
        }

        private McChest pendingChest, trackedChest;
        private bool sawChestOpen;
        private float pendingAt;

        private static string BossNameFor(int kind) { return kind == 5 ? "Wither" : kind == 6 ? "Elder Guardian" : kind == 7 ? "Ender Dragon" : kind == 12 ? "Warden" : kind == 11 ? "Iron Golem" : null; }
        private float nameScan;
        private Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<TargetOfInterestPrefab> targets;

        // Boss markers show the Minecraft boss's name instead of Megabonk's.
        private void TickBossNames()
        {
            try
            {
                if (Time.time > nameScan) { nameScan = Time.time + 0.5f; targets = UnityEngine.Object.FindObjectsOfType<TargetOfInterestPrefab>(); }
                if (targets == null) return;
                foreach (var t in targets)
                {
                    if (t == null || t.enemy == null || t.t_name == null) continue;
                    var rig = t.enemy.GetComponentInChildren<MobRig>();
                    string bn = rig != null && (rig.bossKind || rig.kind == 11) ? BossNameFor(rig.kind) : null;
                    if (bn == null) continue;
                    t.t_name.text = bn;
                }
            }
            catch { }
        }

        // Puts items into the inventory: merges into existing stacks first, then free slots (hotbar before storage).
        public bool GiveItem(ItemDef def, int n)
        {
            if (def == null || n <= 0) return false;
            int left = n;
            if (def.maxStack > 1)
                for (int i = 0; i < 36 && left > 0; i++)
                    if (inv.defs[i] == def && inv.cnts[i] < def.maxStack)
                    {
                        int put = Mathf.Min(left, def.maxStack - inv.cnts[i]);
                        inv.cnts[i] += put; left -= put;
                    }
            for (int i = 0; i < 36 && left > 0; i++)
                if (inv.defs[i] == null)
                {
                    int put = Mathf.Min(left, Mathf.Max(1, def.maxStack));
                    inv.Set(i, def, put); left -= put;
                }
            if (n - left > 0) { Toast("+" + (n - left) + " " + def.name); McSound.Play("entity.item.pickup", 0.6f, 1f + UnityEngine.Random.value * 0.4f); }
            if (left > 0) Toast("Inventory full");
            RefreshHeld();
            return left == 0;
        }

        private float nextChestScan;
        private void SkinExistingChests()
        {
            if (Time.time < nextChestScan) return;
            nextChestScan = Time.time + 4f;
            try
            {
                foreach (var c in UnityEngine.Object.FindObjectsOfType<Assets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest>())
                    McChests.Skin(c);
            }
            catch (Exception ex) { if (Time.time < 30f) Plugin.Logger.LogWarning("chest scan: " + ex.Message); }
        }

        private void ConsumeHeld()
        {
            var item = Held;
            var h = Health;
            if (item == null) return;
            float scale = h != null ? h.maxHp / 20f : 5f;
            AutoWeapons.Event(AutoWeapons.Ev.Consume);
            if (item.kind == ItemKind.Food)
            {
                if (h != null && item.heal > 0f) h.hp = Mathf.Min(h.maxHp, h.hp + Mathf.RoundToInt(item.heal * scale));
                // Vanilla: the absorption effect is refreshed to its level (2 hearts for a golden apple), not stacked.
                if (item.absorption > 0f) { absorption = Mathf.Max(absorption, item.absorption); absorbUntil = Time.time + 120f; }
                food = Mathf.Min(20f, food + (item.hunger > 0f ? item.hunger : (item.name == "Steak" ? 8f : 4f)));
                saturation = Mathf.Min(food, saturation + (item.sat > 0f ? item.sat : (item.name == "Steak" ? 12.8f : 9.6f)));
                McSound.Play("entity.player.burp", 0.9f);
                // Food keeps healing for a while after you eat it (the Saturation effect); a golden apple adds 5 s of Regeneration.
                satHealUntil = Mathf.Max(satHealUntil, Time.time) + (item.hunger > 0f ? item.hunger : 4f) * 0.9f;
                if (item.absorption > 0f) regenUntil = Mathf.Max(regenUntil, Time.time) + 5f;
            }
            else if (item.kind == ItemKind.Potion)
            {
                if (item == ItemLibrary.HealPotion && h != null) h.hp = Mathf.Min(h.maxHp, h.hp + Mathf.RoundToInt(item.heal * scale));
                if (item == ItemLibrary.StrengthPotion) strengthUntil = Time.time + 180f;
                if (item == ItemLibrary.SpeedPotion) speedUntil = Time.time + 180f;
            }
            UseOne(selected);
        }

        public bool Blocking { get; private set; }

        private float lastBlockSound, lastHurtSound, nextEatSound;
        // A shield can only take so much: eight blocked hits break it for five seconds, and it recovers slowly.
        private float shieldHits, shieldBrokenUntil, shieldRegenAt;

        public bool TryBlock(DamageContainer dc)
        {
            bool blocked = Blocking && dc != null && dc.enemy != null;
            if (blocked)
            {
                AutoWeapons.Event(AutoWeapons.Ev.Block);
                shieldHits += dc.enemy.enemyFlag != Assets.Scripts.Actors.Enemies.EEnemyFlag.None ? 3f : 1f;
                shieldRegenAt = Time.time + 1.5f;
                if (shieldHits >= 8f)
                {
                    shieldHits = 0f; shieldBrokenUntil = Time.time + 5f;
                    McSound.Play("item.shield.break", 1f);
                    Toast("Your shield is down for 5 seconds");
                }
            }
            if (blocked && Time.time - lastBlockSound > 0.15f)
            {
                lastBlockSound = Time.time;
                McSound.Play("item.shield.block", 1f, 0.8f + UnityEngine.Random.value * 0.4f);
            }
            return blocked;
        }

        // Called for every enemy hit that isn't blocked: hurt grunt, or the death sound if it was lethal.
        public void OnHurt(PlayerHealth h, DamageContainer dc, bool ignoreShield)
        {
            AutoWeapons.Event(AutoWeapons.Ev.Hurt);
            if (Time.time - lastHurtSound < 0.4f) return;
            lastHurtSound = Time.time;
            bool lethal = false;
            try { lethal = h != null && h.WillDamageKill(dc, ignoreShield); } catch { }
            McSound.Play(lethal ? "entity.player.death" : "entity.player.hurt");
        }

        // ---------------------------------------------------------------- admin panel actions

        public void AdminSpawn(EEnemy type, int count)
        {
            try
            {
                var em = Assets.Scripts.Managers.EnemyManager.Instance;
                var dm = DataManager.Instance;
                var pl = MyPlayer.Instance;
                if (em == null || dm == null || pl == null) return;
                var data = dm.GetEnemyData(type);
                var cam = Camera.main;
                Vector3 fwd = cam != null ? new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z) : pl.transform.forward;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                fwd.Normalize();
                Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
                for (int i = 0; i < count; i++)
                {
                    Vector3 pos = pl.transform.position + fwd * UnityEngine.Random.Range(14f, 26f) + right * UnityEngine.Random.Range(-10f, 10f);
                    RaycastHit h;
                    if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out h, 150f)) pos = h.point;
                    var e = em.SpawnEnemy(data, pos + Vector3.up * 0.5f, 1, true, EEnemyFlag.None, false, 1f);
                    if (e != null)
                    {
                        // Put the feet on the ground whatever the model's origin is.
                        Vector3 off = e.transform.position - e.GetFeetPosition();
                        e.transform.position = new Vector3(pos.x, pos.y + off.y + 0.2f, pos.z);
                    }
                }
                Plugin.Logger.LogInfo("Admin: spawned " + count + " x " + type);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning("AdminSpawn: " + ex); }
        }

        public void AdminSpawnBoss(EEnemy type, EEnemyFlag flag)
        {
            try
            {
                var em = Assets.Scripts.Managers.EnemyManager.Instance;
                var pl = MyPlayer.Instance;
                if (em == null || pl == null) return;
                var cam = Camera.main;
                Vector3 fwd = cam != null ? new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z) : pl.transform.forward;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                Vector3 pos = pl.transform.position + fwd.normalized * 45f;
                RaycastHit h;
                if (Physics.Raycast(pos + Vector3.up * 60f, Vector3.down, out h, 200f)) pos = h.point;
                var e = em.SpawnBoss(type, 0, flag, pos + Vector3.up * 0.5f, 1f);
                if (e != null)
                {
                    Vector3 off = e.transform.position - e.GetFeetPosition();
                    e.transform.position = new Vector3(pos.x, pos.y + off.y + 0.2f, pos.z);
                }
                Plugin.Logger.LogInfo("Admin: boss " + type + " flag " + flag);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning("AdminSpawnBoss: " + ex); }
        }

        public void AdminKillAll()
        {
            try
            {
                var em = Assets.Scripts.Managers.EnemyManager.Instance;
                if (em == null) return;
                var list = new List<Enemy>();
                foreach (var kv in em.enemies) if (kv.Value != null) list.Add(kv.Value);
                int n = 0;
                foreach (var e in list) { if (!e.IsDead()) { e.Kill("Admin"); n++; } }
                Plugin.Logger.LogInfo("Admin: killed " + n);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning("AdminKillAll: " + ex.Message); }
        }

        public void AdminHeal()
        {
            var h = Health;
            if (h != null) h.hp = h.maxHp;
            food = 20f; saturation = 20f;
        }

        public void AdminLevel() { GiveLevels(1); }

        private void GiveLevels(int n)
        {
            try
            {
                var p = MyPlayer.Instance;
                var xp = p != null && p.inventory != null ? p.inventory.playerXp : null;
                if (xp == null) return;
                // One AddXp per level so each level-up screen opens in turn.
                for (int i = 0; i < n; i++) xp.AddXp(Inventory__Items__Pickups.XpUtility.XpToNextLevel(xp.xp));
                Plugin.Logger.LogInfo("Gave " + n + " level(s), now level " + xp.level);
            }
            catch (Exception e) { Plugin.Logger.LogWarning("GiveLevels: " + e.Message); }
        }

        // Steve starts with no automatic weapon (the cloned Fox's fire staff is removed); new ones come from level-ups.
        private bool weaponsCleared;
        private float nextWeaponCheck;
        private void ClearStartingWeapons()
        {
            if (weaponsCleared || Time.time < nextWeaponCheck) return;
            nextWeaponCheck = Time.time + 0.5f;
            try
            {
                var p = MyPlayer.Instance;
                var wi = p != null && p.inventory != null ? p.inventory.weaponInventory : null;
                if (wi == null || wi.weapons == null || wi.weapons.Count == 0) return;
                var keys = new List<EWeapon>();
                foreach (var kv in wi.weapons) keys.Add(kv.Key);
                foreach (var k in keys)
                {
                    var w = wi.weapons[k];
                    wi.weapons.Remove(k);
                    try { var rem = Assets.Scripts.Inventory__Items__Pickups.Weapons.WeaponInventory.A_WeaponRemoved; if (rem != null) rem.Invoke(w); } catch { }
                }
                weaponsCleared = true;
                Plugin.Logger.LogInfo("Removed " + keys.Count + " starting weapon(s)");
            }
            catch (Exception e) { Plugin.Logger.LogWarning("ClearStartingWeapons: " + e.Message); }
        }

        // ---------------------------------------------------------------- bow and arrows

        private bool bowDrawing;
        private float bowStart;
        private int bowStage;
        private readonly List<ArrowEntity> arrows = new List<ArrowEntity>();

        private class ArrowEntity
        {
            public GameObject go;
            public Vector3 vel;
            public float damage;
            public bool crit, stuck;
            public float life;
        }

        private int FindArrowSlot()
        {
            for (int i = 0; i < 36; i++)
                if (inv.defs[i] != null && inv.defs[i].kind == ItemKind.Arrow && inv.cnts[i] > 0) return i;
            return -1;
        }

        // Eye position and aim direction: first person uses the camera; third person aims from Steve's eyes at
        // whatever the crosshair points at.
        private bool GetAim(out Vector3 eye, out Vector3 dir)
        {
            eye = Vector3.zero; dir = Vector3.forward;
            var player = MyPlayer.Instance;
            var cam = Camera.main;
            if (player == null || cam == null) return false;
            eye = firstPerson ? cam.transform.position : player.transform.position + Vector3.up * (player.height * 0.85f);
            dir = cam.transform.forward;
            if (!firstPerson)
            {
                Vector3 start = cam.transform.position + cam.transform.forward * ((eye - cam.transform.position).magnitude + 0.5f);
                RaycastHit ah;
                Vector3 aimPoint = Physics.Raycast(start, cam.transform.forward, out ah, 200f)
                    ? ah.point : cam.transform.position + cam.transform.forward * 100f;
                dir = (aimPoint - eye).normalized;
            }
            return true;
        }

        // Vanilla draw power: 0 to 1 over one second, accelerating (f^2 + 2f) / 3.
        private float BowPower(float seconds)
        {
            float f = seconds;
            return Mathf.Clamp01((f * f + f * 2f) / 3f);
        }

        private void ReleaseBow(ItemDef bow)
        {
            float secs = Time.time - bowStart;
            float power = BowPower(secs);
            bowDrawing = false;
            if (fp != null) fp.SetHeldIcon(bow.icon);
            if (power < 0.1f) return;
            int slot = FindArrowSlot();
            if (slot < 0) return;
            Vector3 eye, dir;
            if (!GetAim(out eye, out dir)) return;
            UseOne(slot);
            AutoWeapons.Event(AutoWeapons.Ev.Shot);
            McSound.Play("entity.arrow.shoot", 1f, 1f / (UnityEngine.Random.value * 0.4f + 1.2f) + power * 0.5f);

            float speed = power * 3f;                         // blocks per tick, vanilla
            float dmg = Mathf.Ceil(speed * 2f);               // base arrow damage 2 per unit of speed
            bool crit = power >= 1f;
            if (crit) dmg += UnityEngine.Random.Range(0, (int)(dmg / 2f) + 3);
            dmg *= Ench.ProjMul;

            int shots = 1 + Ench.ExtraProjectiles;
            for (int k = 0; k < shots; k++)
            {
            Vector3 sdir = shots == 1 ? dir : Quaternion.AngleAxis((k - (shots - 1) / 2f) * 7f, Vector3.up) * dir;
            var go = new GameObject("arrow");
            go.transform.position = eye + sdir * 1.2f;
            var icon = ItemLibrary.Arrow.icon;
            if (icon != null && rig != null && rig.itemMat != null)
            {
                var mat = Keep.It(new Material(rig.itemMat));
                mat.mainTexture = icon;
                foreach (var n in new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo" })
                    if (mat.HasProperty(n)) mat.SetTexture(n, icon);
                go.AddComponent<MeshFilter>().sharedMesh = ItemMesh.Build(icon);
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                go.transform.localScale = Vector3.one * 0.12f;
            }
            arrows.Add(new ArrowEntity { go = go, vel = sdir * speed * VelToUnits, damage = dmg, crit = crit });
            }
            // Release feedback: the view model kicks back and the body's arm recoils.
            if (fp != null) { fp.Swing(); fp.Recoil(power); }
            // no body swing on release: the bow is drawn, not swung
        }

        private void TickArrows()
        {
            for (int i = arrows.Count - 1; i >= 0; i--)
            {
                var a = arrows[i];
                if (a.go == null) { arrows.RemoveAt(i); continue; }
                a.life += Time.deltaTime;
                if (a.stuck)
                {
                    if (a.life > 20f) { UnityEngine.Object.Destroy(a.go); arrows.RemoveAt(i); }
                    continue;
                }
                float ticks = Time.deltaTime * 20f;
                a.vel *= Mathf.Pow(0.99f, ticks);
                a.vel.y -= 20f * BlocksToUnits * Time.deltaTime;          // 0.05 blocks/tick^2
                Vector3 from = a.go.transform.position;
                Vector3 step = a.vel * Time.deltaTime;
                float len = step.magnitude;
                RaycastHit hit;
                bool hitSomething = false;
                if (len > 0.0001f)
                {
                    var hits = Physics.RaycastAll(from, step.normalized, len + 0.1f);
                    float best = float.MaxValue;
                    foreach (var h in hits)
                    {
                        if (h.collider == null || h.collider.GetComponentInParent<MyPlayer>() != null) continue;
                        if (h.collider.isTrigger && h.collider.GetComponentInParent<Enemy>() == null) continue;
                        if (h.distance < best) { best = h.distance; hitSomething = true; }
                    }
                    if (hitSomething)
                    {
                        RaycastHit chosen = default(RaycastHit);
                        foreach (var h in hits) if (h.distance == best) { chosen = h; break; }
                        hit = chosen;
                        var enemy = hit.collider.GetComponentInParent<Enemy>();
                        if (enemy != null && !enemy.IsDead())
                        {
                            var dc = new DamageContainer(1f, "Steve");
                            dc.damage = a.damage * DamageScale;
                            dc.crit = a.crit;
                            dc.direction = a.vel.normalized;
                            dc.knockback = 3f * Ench.KnockMul;
                            dc.enemy = enemy;
                            if (a.crit) CritGoldUntil = Time.time + 0.4f;
                            enemy.DamageFromPlayerOther(dc);
                            McSound.Play("entity.arrow.hit_player", 0.8f);
                            AutoWeapons.Event(AutoWeapons.Ev.ArrowHit);
                            McSound.PlayAt("entity.arrow.hit", hit.point, 0.9f);
                            if (enemy.IsDead()) kills++;
                            UnityEngine.Object.Destroy(a.go);
                            arrows.RemoveAt(i);
                            continue;
                        }
                        McSound.PlayAt("entity.arrow.hit", hit.point, 0.8f);
                        a.go.transform.position = hit.point - a.vel.normalized * 0.2f;
                        a.stuck = true; a.life = 0f;
                        continue;
                    }
                }
                a.go.transform.position = from + step;
                if (a.vel.sqrMagnitude > 0.01f)
                    a.go.transform.rotation = Quaternion.LookRotation(a.vel) * Quaternion.Euler(0f, -90f, 0f) * Quaternion.Euler(0f, 0f, -45f);
                if (a.life > 30f) { UnityEngine.Object.Destroy(a.go); arrows.RemoveAt(i); }
            }
        }

        // ---------------------------------------------------------------- ender pearl

        private class Pearl
        {
            public GameObject go;
            public Vector3 vel;
            public float life;
        }

        private void ThrowPearl()
        {
            var player = MyPlayer.Instance;
            var cam = Camera.main;
            if (player == null || cam == null) return;
            if (fp != null) fp.Swing();
            McSound.Play("entity.ender_pearl.throw", 0.5f, 0.4f / (UnityEngine.Random.value * 0.4f + 0.8f));

            // Thrown from Steve's eyes toward whatever the crosshair points at (in third person the camera is
            // behind him, so aim the throw at the point the camera sees).
            Vector3 eye = firstPerson ? cam.transform.position : player.transform.position + Vector3.up * (player.height * 0.85f);
            Vector3 dir = cam.transform.forward;
            if (!firstPerson)
            {
                Vector3 start = cam.transform.position + cam.transform.forward * ((eye - cam.transform.position).magnitude + 0.5f);
                RaycastHit ah;
                Vector3 aimPoint = Physics.Raycast(start, cam.transform.forward, out ah, 200f)
                    ? ah.point : cam.transform.position + cam.transform.forward * 100f;
                dir = (aimPoint - eye).normalized;
            }
            var go = new GameObject("pearl");
            go.transform.position = eye + dir * 1.5f;
            if (ItemLibrary.Pearl.icon != null && rig != null && rig.itemMat != null)
            {
                var mat = Keep.It(new Material(rig.itemMat));
                mat.mainTexture = ItemLibrary.Pearl.icon;
                foreach (var n in new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo" })
                    if (mat.HasProperty(n)) mat.SetTexture(n, ItemLibrary.Pearl.icon);
                go.AddComponent<MeshFilter>().sharedMesh = ItemMesh.Build(ItemLibrary.Pearl.icon);
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                go.transform.localScale = Vector3.one * 0.04f;
            }
            // 1.5 blocks/tick in vanilla = 30 blocks/s.
            pearls.Add(new Pearl { go = go, vel = dir * 30f * BlocksToUnits, life = 0f });
        }

        private void TickPearls()
        {
            for (int i = pearls.Count - 1; i >= 0; i--)
            {
                var p = pearls[i];
                float dt = Time.deltaTime;
                Vector3 from = p.go.transform.position;
                p.vel.y -= 12f * BlocksToUnits * dt; // 0.03 blocks/tick^2
                Vector3 step = p.vel * dt;
                RaycastHit hit;
                bool hitSomething = Physics.Raycast(from, step.normalized, out hit, step.magnitude + 0.2f);
                p.life += dt;
                p.go.transform.position = from + step;
                // Sprite-style projectiles always face the camera, as in vanilla.
                if (Camera.main != null) p.go.transform.rotation = Camera.main.transform.rotation;
                if (hitSomething || p.life > 8f)
                {
                    Vector3 dest = hitSomething ? hit.point + hit.normal * 1.0f : p.go.transform.position;
                    TeleportPlayer(dest);
                    UnityEngine.Object.Destroy(p.go);
                    pearls.RemoveAt(i);
                }
            }
        }

        private void TeleportPlayer(Vector3 dest)
        {
            var player = MyPlayer.Instance;
            if (player == null) return;
            var rb = player.playerMovement != null ? player.playerMovement.rb : null;
            if (rb != null) { rb.position = dest; rb.velocity = Vector3.zero; }
            player.transform.position = dest;
            velocity = Vector3.zero; mcVel = Vector3.zero; cmdVel = Vector3.zero;
            McSound.Play("entity.player.teleport", 0.9f);
        }

        // ---------------------------------------------------------------- movement
        // Vanilla 1.21 constants, per 20 Hz tick, in blocks. 1 block = 2.5 world units; 1 block/tick = 50 units/s.
        private const float VelToUnits = 50f;
        private Vector3 velocity;          // blocks per tick
        private Vector3 lastRbVel;
        private float jumpCooldown;
        private bool wasControlling;
        private float sneakT;
        public bool Sneaking { get; private set; }
        public bool Sprinting { get; private set; }
        private bool gliding;
        private float glideStart, glideEndAt = -10f, rocketUntil;
        private float nextFlagLog;
        private bool mcMovement = true;
        private float lastStepTime = -1f;

        public bool IsGliding { get { return gliding; } }
        public bool GlideProtect { get { return gliding || Time.time < glideEndAt + 1.5f || Time.time < smashProtectUntil; } }

        // Fall distance in blocks since the last time on the ground (vanilla fallDistance), used by the mace.
        private float peakY, fallBlocks, smashProtectUntil;

        private void TrackFall()
        {
            var pl = MyPlayer.Instance;
            if (pl == null || pl.playerMovement == null) return;
            float y = pl.transform.position.y;
            if (pl.playerMovement.grounded || gliding) peakY = y;
            else if (y > peakY) peakY = y;
            if ((pl.playerMovement.grounded || gliding) && prevFall >= 2.0f && !gliding) { if (Weapon.kind == ItemKind.Mace) { AutoWeapons.LandHeight = prevFall; AutoWeapons.Event(AutoWeapons.Ev.Land); AutoWeapons.LandHeight = 0f; } }
            fallBlocks = Mathf.Max(0f, (peakY - y) / BlocksToUnits);
            prevFall = pl.playerMovement.grounded ? 0f : fallBlocks;
        }

        public void CaptureVel(PlayerMovement pm)
        {
            if (pm != null && pm.rb != null) lastRbVel = pm.rb.velocity;
        }

        private float groundGap;
        private Vector3 groundPoint;

        // Grounded if the game says so or a ray finds floor within a generous reach of the feet.
        private bool GroundCheck(PlayerMovement pm, out Vector3 normal)
        {
            normal = Vector3.up;
            Vector3 feet = pm.feet != null ? pm.feet.position : pm.rb.position;
            RaycastHit hit;
            int mask = pm.whatIsGround;
            bool probe = false;
            groundGap = 99f;
            if (Physics.Raycast(feet + Vector3.up * 1.0f, Vector3.down, out hit, 1.0f + 0.8f, mask))
            {
                groundGap = feet.y - hit.point.y;
                groundPoint = hit.point;
                if (hit.normal.y > 0.4f) normal = hit.normal;
                probe = groundGap < 0.6f;
            }
            return pm.grounded || pm.onGround || probe;
        }

        // Climb steps and kerbs up to 0.6 blocks like vanilla's step height.
        private void StepUp(PlayerMovement pm, Vector3 hDir)
        {
            Vector3 feet = pm.feet != null ? pm.feet.position : pm.rb.position;
            int mask = pm.whatIsGround;
            RaycastHit low;
            if (!Physics.Raycast(feet + Vector3.up * 0.2f, hDir, out low, 0.9f, mask)) return;
            if (Mathf.Abs(low.normal.y) > 0.5f) return; // a slope, not a wall
            if (Physics.Raycast(feet + Vector3.up * 1.7f, hDir, 1.1f, mask)) return; // no room above
            RaycastHit top;
            Vector3 probe = low.point + hDir * 0.35f + Vector3.up * 1.7f;
            if (!Physics.Raycast(probe, Vector3.down, out top, 1.7f, mask)) return;
            float rise = top.point.y - feet.y;
            if (rise > 0.05f && rise <= 0.6f * BlocksToUnits) pm.rb.position += Vector3.up * (rise + 0.03f);
        }

        public void MoveStep(PlayerMovement pm)
        {
            if (pm == null || pm.rb == null) return;
            // Shift is Megabonk's crouch/slide (the C key): hand movement back to the game for inertia and bunny hops.
            // (No crouching or sliding while the elytra is open.)
            bool megabonkSlide = !gliding && (Input.GetKey(KeyCode.LeftShift) || pm.IsCrouching() || pm.IsSliding());
            Sneaking = megabonkSlide;
            sneakT = Mathf.MoveTowards(sneakT, megabonkSlide ? 1f : 0f, Time.deltaTime * 8f);
            // Rails (the game's grind), ladders, walls and water also keep Megabonk's own movement.
            if (pm.IsGrinding() || pm.IsWallClimbing() || pm.onLadder || pm.isUnderwater || megabonkSlide)
            {
                if (wasControlling) { pm.rb.useGravity = true; wasControlling = false; }
                velocity = pm.rb.velocity / VelToUnits;
                mcVel = velocity; cmdVel = velocity; // keep the inertia when Minecraft movement resumes
                gliding = false;
                jumpPressed = false;
                return;
            }
            var rb = pm.rb;
            if (!mcMovement)
            {
                if (wasControlling) { rb.useGravity = true; wasControlling = false; }
                jumpPressed = false;
                return;
            }
            rb.useGravity = false;
            // Smooths the body between physics steps so the first-person camera glued to it doesn't stutter.
            if (rb.interpolation != RigidbodyInterpolation.Interpolate) rb.interpolation = RigidbodyInterpolation.Interpolate;
            if (!wasControlling) { mcVel = pm.rb.velocity / VelToUnits; cmdVel = mcVel; }
            wasControlling = true;

            // Real time between movement ticks (the game runs its own tick rate, not necessarily the physics step).
            float now = Time.time;
            float dt = lastStepTime > 0f ? Mathf.Clamp(now - lastStepTime, 0.004f, 0.05f) : Time.fixedDeltaTime;
            lastStepTime = now;
            float t = dt * 20f; // ticks elapsed
            var cam = Camera.main;
            float yaw = (cam != null ? cam.transform.eulerAngles.y : 0f) * Mathf.Deg2Rad;
            Vector3 fwdDir = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            Vector3 rightDir = new Vector3(fwdDir.z, 0f, -fwdDir.x);

            Vector3 groundNormal;
            bool grounded = GroundCheck(pm, out groundNormal);
            if (Time.time < forceAirUntil) grounded = false;

            bool uiBlocked = AdminOpen;
            float fwd = uiBlocked ? 0f : (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
            float strafe = uiBlocked ? 0f : (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            if (uiBlocked) jumpPressed = false;
            if (fwd <= 0f) sprintLatched = false;
            bool usingItem = Blocking || eatStart > 0f;
            // Sprint: hold Ctrl (any order with W) or double-tap W; ends when W is released.
            Sprinting = (sprintLatched || Input.GetKey(KeyCode.LeftControl)) && fwd > 0f && !usingItem && food > 6f && !gliding;

            // Our own state (blocks per tick, after friction) plus corrections for what physics actually allowed.
            Vector3 v = mcVel;
            Vector3 actual = lastRbVel / VelToUnits;
            Vector2 ch = new Vector2(cmdVel.x, cmdVel.z);
            if (ch.magnitude > 0.03f)
            {
                float k = Mathf.Clamp01(Vector2.Dot(new Vector2(actual.x, actual.z), ch.normalized) / ch.magnitude);
                if (k < 0.7f) { v.x *= k; v.z *= k; }
            }
            if (cmdVel.y > 0.05f && actual.y < cmdVel.y * 0.2f) v.y = 0f;                 // bumped a ceiling
            if (grounded && v.y <= 0.05f) v.y = Mathf.Min(v.y, 0f);
            bool airborneUp = v.y > 0.05f;
            if (airborneUp) grounded = false;

            // ---- Elytra: open with jump while falling, close on landing.
            if (!gliding && HasElytra && jumpPressed && !grounded && v.y < 0f) { gliding = true; glideStart = Time.time; v = actual; }
            if (gliding && (!HasElytra || (grounded && Time.time - glideStart > 0.4f))) { gliding = false; glideEndAt = Time.time; }
            if (gliding)
            {
                Vector3 look = cam != null ? cam.transform.forward : fwdDir;
                float pitch = -Mathf.Asin(Mathf.Clamp(look.y, -1f, 1f));          // vanilla xRot: positive looks down
                float lookH = Mathf.Sqrt(look.x * look.x + look.z * look.z);
                v = actual;
                float speedH = Mathf.Sqrt(v.x * v.x + v.z * v.z);
                float cosP = Mathf.Cos(pitch); cosP = cosP * cosP;
                v.y += 0.08f * (-1f + cosP * 0.75f) * t;
                if (v.y < 0f && lookH > 0f)
                {
                    float d = v.y * -0.1f * cosP * t;
                    v += new Vector3(look.x * d / lookH, d, look.z * d / lookH);
                }
                if (pitch < 0f && lookH > 0f)
                {
                    float d = speedH * -Mathf.Sin(pitch) * 0.04f * t;
                    v += new Vector3(-look.x * d / lookH, d * 3.2f, -look.z * d / lookH);
                }
                if (lookH > 0f)
                {
                    v.x += (look.x / lookH * speedH - v.x) * 0.1f * t;
                    v.z += (look.z / lookH * speedH - v.z) * 0.1f * t;
                }
                if (Time.time < rocketUntil) v += (look * 0.1f + (look * 1.5f - v) * 0.5f) * t;
                v.x *= Mathf.Pow(0.99f, t); v.y *= Mathf.Pow(0.98f, t); v.z *= Mathf.Pow(0.99f, t);
                mcVel = v; cmdVel = v;
                rb.velocity = v * VelToUnits;
                jumpPressed = false;
                return;
            }

            Vector3 input = new Vector3(strafe, 0f, fwd);
            input *= 0.98f;
            if (usingItem) input *= 0.2f;
            if (input.sqrMagnitude > 1f) input.Normalize();

            // Vanilla: on flat ground (0.6 slipperiness) acceleration equals the movement speed attribute.
            float speedAttr = 0.1f * (Sprinting ? 1.3f : 1f) * (Time.time < speedUntil ? 1.2f : 1f);
            float accel = grounded ? speedAttr : (Sprinting ? 0.026f : 0.02f);
            float f = grounded ? 0.6f * 0.91f : 0.91f;

            if (jumpCooldown > 0f) jumpCooldown -= dt;
            bool jumped = false;
            if (!uiBlocked && (Input.GetKey(KeyCode.Space) || jumpPressed) && grounded && jumpCooldown <= 0f)
            {
                v.y = 0.42f;
                if (Sprinting) { v.x += fwdDir.x * 0.2f; v.z += fwdDir.z * 0.2f; }
                jumpCooldown = 0.5f; // vanilla's 10-tick auto-jump delay when holding the key
                exhaustion += Sprinting ? 0.2f : 0.05f;
                jumped = true;
            }
            jumpPressed = false;

            Vector3 wish = (rightDir * input.x + fwdDir * input.z) * accel * t;
            v.x += wish.x; v.z += wish.z;

            // Vanilla moves by the velocity *before* friction and gravity are applied for the next tick.
            Vector3 move = v;

            if (grounded && !jumped)
            {
                // Follow the ground: ramps keep their speed along the slope and the player stays glued to it.
                Vector3 h = new Vector3(v.x, 0f, v.z);
                float hm = h.magnitude;
                float vy = -0.05f;
                if (hm > 0.0005f)
                {
                    Vector3 hd = h.normalized;
                    Vector3 feet = pm.feet != null ? pm.feet.position : pm.rb.position;
                    RaycastHit ahead;
                    float run = 0.8f;
                    if (groundGap < 90f && Physics.Raycast(feet + hd * run + Vector3.up * 1.6f, Vector3.down, out ahead, 3.2f, (int)pm.whatIsGround))
                    {
                        float slope = (ahead.point.y - groundPoint.y) / run;
                        if (slope > 0.02f && slope < 1.8f) vy = hm * slope + 0.02f;           // uphill: climb along it
                        else if (slope < -0.02f && slope > -2.5f) vy = hm * slope - 0.05f;    // downhill: stay on the ground
                    }
                    StepUp(pm, hd);
                }
                move.y = vy;
            }

            // State for next tick: friction on the ground plane, gravity on the vertical.
            float drag = Mathf.Pow(f, t);
            v.x *= drag; v.z *= drag;
            v.y = (v.y - 0.08f * t) * Mathf.Pow(0.98f, t);
            if (v.y < -3.92f) v.y = -3.92f;
            if (grounded && !jumped) v.y = -0.0784f;

            mcVel = v;
            cmdVel = move;
            velocity = move;
            rb.velocity = move * VelToUnits;

            // Footsteps while walking on the ground and a thud on landing, like vanilla.
            if (grounded && !jumped)
            {
                stepAcc += new Vector2(move.x, move.z).magnitude * t;
                float thr = Sprinting ? 1.6f : 1.9f;
                if (stepAcc > thr) { stepAcc = 0f; McSound.Play("block.grass.step", 0.35f, 0.9f + UnityEngine.Random.value * 0.2f); }
            }
            if (grounded && !prevGrounded && airFall > 2f)
                McSound.Play(airFall > 5.5f ? "entity.generic.big_fall" : "entity.generic.small_fall", 0.8f);
            if (!grounded) airFall = Mathf.Max(airFall, fallBlocks); else airFall = 0f;
            prevGrounded = grounded;

            if (Time.time > nextFlagLog)
            {
                nextFlagLog = Time.time + 3f;
                Plugin.Dbg("move dt=" + dt.ToString("0.000") + " grounded=" + grounded + " sprint=" + Sprinting
                    + " hMove=" + new Vector2(move.x, move.z).magnitude.ToString("0.000") + "b/t vy=" + move.y.ToString("0.000")
                    + " gap=" + groundGap.ToString("0.00") + " pmGrounded=" + pm.grounded);
            }
        }

        private Vector3 mcVel, cmdVel;
        private float stepAcc, airFall;
        private bool prevGrounded = true;

        public float SneakDrop { get { return sneakT * 0.3f * BlocksToUnits; } }
    }
}









