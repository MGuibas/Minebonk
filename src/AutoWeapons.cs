using System;
using System.Collections.Generic;
using Assets.Scripts.Actors;
using Assets.Scripts.Actors.Player;
using Assets.Scripts.Actors.Enemies;
using Assets.Scripts.Inventory__Items__Pickups.Weapons;
using Assets.Scripts.Menu.Shop;
using UnityEngine;

namespace MegabonkSteve
{
    // Minecraft-themed automatic weapons. Megabonk's own weapon entries (levels, upgrade cards, stat tomes) are kept
    // and re-skinned: their name and icon become the Minecraft item, and their attack is replaced by ours.
    internal static class AutoWeapons
    {
        private class Def
        {
            public string name, description, icon, trig;
            public Ev ev;
        }

        private static readonly Dictionary<EWeapon, Def> defs = new Dictionary<EWeapon, Def>
        {
            { EWeapon.FireStaff, new Def { name = "Fire Charge", description = "Hurls blaze fireballs that explode on the nearest enemies.", icon = "item/fire_charge", ev = Ev.Hit, trig = "hit an enemy in melee" } },
            { EWeapon.Bow, new Def { name = "Skeleton Bow", description = "Fires arrows at nearby enemies on its own.", icon = "item/bow", ev = Ev.Shot, trig = "release your bow" } },
            { EWeapon.LightningStaff, new Def { name = "Trident of Channeling", description = "Calls down lightning on nearby enemies.", icon = "item/trident", ev = Ev.Crit, trig = "land a critical hit (a charged hit while falling)" } },
            { EWeapon.Mine, new Def { name = "TNT", description = "Drops primed TNT that blows up after four seconds.", icon = "block/tnt_side", ev = Ev.Kill, trig = "kill an enemy" } },
            { EWeapon.Sword, new Def { name = "Sweeping Edge", description = "Sweeps a wide arc that hits every enemy around you.", icon = "item/netherite_sword", ev = Ev.Charged, trig = "land a fully charged melee hit" } },
            { EWeapon.Revolver, new Def { name = "Piercing Crossbow", description = "A heavy bolt that goes straight through a line of enemies.", icon = "item/crossbow_standby", ev = Ev.ArrowHit, trig = "hit an enemy with an arrow" } },
            { EWeapon.Shotgun, new Def { name = "Mace", description = "Slams the ground and smashes everything around you.", icon = "item/mace", ev = Ev.Land, trig = "land from a fall of 2 or more blocks" } },
            { EWeapon.Sniper, new Def { name = "Sonic Boom", description = "The Warden's shriek: an instant beam through everything ahead.", icon = "item/echo_shard", ev = Ev.Combo, trig = "land 5 melee hits in a row" } },
            { EWeapon.Rockets, new Def { name = "Firework Rocket", description = "A salvo of rockets fanned out ahead of you, bursting in a big blast.", icon = "item/firework_rocket", ev = Ev.ArrowHit, trig = "hit an enemy with an arrow" } },
            { EWeapon.PoisonFlask, new Def { name = "Splash Potion", description = "Lobs a potion that leaves a poison cloud.", icon = "item/splash_potion", ev = Ev.Consume, trig = "eat or drink" } },
            { EWeapon.Chunkers, new Def { name = "Anvil", description = "Drops an anvil on an enemy.", icon = "block/anvil", ev = Ev.Hurt, trig = "get hurt" } },
        };

        public static bool Has(EWeapon w) { return defs.ContainsKey(w); }
        public static IEnumerable<EWeapon> All { get { return defs.Keys; } }

        public static string NameOf(EWeapon w) { Def d; return defs.TryGetValue(w, out d) ? d.name : null; }
        // The Sweeping Edge answers every hit you land, at every level; the others awaken at AutoLevel.
        private static bool Always(EWeapon w) { return w == EWeapon.Sword; }
        public static string DescOf(EWeapon w)
        {
            Def d; if (!defs.TryGetValue(w, out d)) return null;
            if (Always(w)) return d.description + " Activates when you " + d.trig + ", at every level.";
            string s = d.description + " Activates when you " + d.trig + ". At level " + AutoLevel + " it awakens and also fires on its own, on top of its trigger.";
            int lvl = OwnedLevel(w);
            if (lvl == AutoLevel - 1) s += " NEXT LEVEL: it awakens and also fires automatically!";
            else if (lvl >= AutoLevel) s += " Level " + lvl + ": fires automatically and on its trigger.";
            return s;
        }

        // The level of a weapon the player owns (0 when not owned), used to say on its card and its icon when it fires by itself.
        public static int OwnedLevel(EWeapon w)
        {
            try { WeaponBase b; if (lastInv != null && lastInv.weapons != null && lastInv.weapons.TryGetValue(w, out b) && b != null) return LevelOf(b); } catch { }
            return 0;
        }

        public static bool IsAuto(EWeapon w) { return !Always(w) && OwnedLevel(w) >= AutoLevel; }

        public static bool WeaponForIcon(Texture t, out EWeapon w)
        {
            w = EWeapon.None;
            if (t == null) return false;
            foreach (var kv in defs) if (McAssets.Tex(kv.Value.icon) == t) { w = kv.Key; return true; }
            return false;
        }

        // Renames the data entries once the game's data is loaded.
        public static void Reskin(DataManager dm)
        {
            McAssets.Init();
            foreach (var kv in defs)
            {
                try
                {
                    if (!dm.weapons.ContainsKey(kv.Key)) continue;
                    var data = dm.weapons[kv.Key];
                    var tex = McAssets.Tex(kv.Value.icon);
                    if (tex != null) data.icon = tex;
                }
                catch (Exception e) { Plugin.Logger.LogWarning("Reskin " + kv.Key + ": " + e.Message); }
            }
            var book = McAssets.Tex("item/enchanted_book");
            for (int i = 0; i < Ench.Count && book != null; i++)
            {
                try { var td = dm.GetTome(Ench.Tomes[i]); if (td != null) td.icon = book; }
                catch (Exception e) { Plugin.Logger.LogWarning("Reskin tome: " + e.Message); }
            }
            Plugin.Logger.LogInfo("Weapons re-skinned as Minecraft items");
        }

        // ------------------------------------------------------------------ scheduling

        public static Material baseMat;
        private static readonly Dictionary<EWeapon, float> nextFire = new Dictionary<EWeapon, float>();
        private static readonly HashSet<EWeapon> logged = new HashSet<EWeapon>();

        // ------------------------------------------------------------------ triggers

        // Below this level a weapon only fires on its trigger; from it on, the weapon awakens and fires by itself.
        public const int AutoLevel = 5;

        public enum Ev { Hit, Charged, Crit, Kill, Shot, Block, Land, Consume, Item, Combo, Hurt, ArrowHit }

        public static float LandHeight;   // blocks fallen before the last landing
        private static WeaponInventory lastInv;
        private static readonly HashSet<EWeapon> awakened = new HashSet<EWeapon>();

        private static int LevelOf(WeaponBase w) { try { return w.level; } catch { return 1; } }

        private static float CooldownFor(EWeapon id, WeaponBase w)
        {
            float cd = 1f;
            try { cd = w.GetCooldown(); } catch { }
            if (cd < 0.1f) cd = 0.1f;
            // Vanilla pacing: lightning and TNT are rarer than the arrows and fireballs.
            if (id == EWeapon.LightningStaff) cd = Mathf.Max(cd * 2.5f, 3f);
            else if (id == EWeapon.Mine) cd = Mathf.Max(cd * 3f, 5f);
            else if (id == EWeapon.Shotgun) cd = Mathf.Max(cd * 2f, 2.5f);
            else if (id == EWeapon.Sniper) cd = Mathf.Max(cd * 1.6f, 2.2f);
            else if (id == EWeapon.Revolver) cd = Mathf.Max(cd * 1.4f, 1.0f);
            return cd * Ench.CooldownMul;
        }

        // Something you did (a hit, a kill, a block...): every weapon that is still below its awakening level and listens
        // to that trigger fires, unless it is on cooldown.
        public static void Event(Ev e)
        {
            var wi = lastInv; var player = MyPlayer.Instance;
            if (wi == null || wi.weapons == null || player == null || MinecraftMode.GameBlocked) return;
            foreach (var kv in wi.weapons)
            {
                var id = kv.Key; var w = kv.Value;
                Def d;
                if (w == null || !defs.TryGetValue(id, out d) || d.ev != e) continue;   // awakened weapons still answer their trigger
                float t;
                if (nextFire.TryGetValue(id, out t) && Time.time < t) continue;
                bool fired = false;
                cur = id;
                try { fired = Fire(id, w, player); }
                catch (Exception ex) { Plugin.Logger.LogError("AutoWeapon event " + id + ": " + ex); }
                nextFire[id] = Time.time + (fired ? Mathf.Max(0.4f, CooldownFor(id, w) * 0.5f) : 0.15f);
            }
        }

        private static void Announce(EWeapon id, string title, string text, Color c)
        {
            var m = MinecraftMode.Instance;
            Def d;
            if (m == null || !defs.TryGetValue(id, out d)) return;
            m.Reward(McAssets.Tex(d.icon), title, text, c);
        }

        public static void Tick(WeaponInventory wi)
        {
            var player = MyPlayer.Instance;
            if (wi == null || wi.weapons == null || player == null) return;
            lastInv = wi;
            if (MinecraftMode.GameBlocked) return;   // no abilities while the game is paused or in a menu
            foreach (var kv in wi.weapons)
            {
                var id = kv.Key; var w = kv.Value;
                Def d;
                if (w == null || !defs.TryGetValue(id, out d)) continue;
                if (logged.Add(id))
                {
                    LogStats(id, w);
                    Announce(id, d.name, Always(id) ? "Activates when you " + d.trig + ", at every level: it never becomes automatic." : "Activates when you " + d.trig + ". At level " + AutoLevel + " it fires on its own.", new Color(0.55f, 0.9f, 1f));
                }
                if (Always(id) || LevelOf(w) < AutoLevel) continue;   // trigger-only until it awakens (the sweep never does)
                if (awakened.Add(id) && LevelOf(w) == AutoLevel)
                {
                    Announce(id, d.name + " awakened!", "It still fires when you " + d.trig + ", and now also fires on its own.", new Color(1f, 0.85f, 0.25f));
                    McSound.Play("ui.toast.challenge_complete", 0.9f);
                }
                float t;
                if (!nextFire.TryGetValue(id, out t)) t = 0f;
                if (Time.time < t) continue;
                float cd = CooldownFor(id, w);
                bool fired = false;
                cur = id;
                try { fired = Fire(id, w, player); }
                catch (Exception e) { Plugin.Logger.LogError("AutoWeapon " + id + ": " + e); }
                nextFire[id] = Time.time + (fired ? cd : 0.2f);
            }
        }

        private static void LogStats(EWeapon id, WeaponBase w)
        {
            try
            {
                string s = "weapon " + id + " lvl=" + w.level + " cooldown=" + w.GetCooldown() + " data.damage=" + w.weaponData.damage + " stats:";
                foreach (var kv in w.weaponStats) s += " " + kv.Key + "=" + kv.Value;
                Plugin.Logger.LogInfo(s);
            }
            catch (Exception e) { Plugin.Logger.LogWarning("LogStats: " + e.Message); }
        }

        private static float Stat(WeaponBase w, EStat s, float fallback)
        {
            try { float v = w.GetValue(s); return v; } catch { return fallback; }
        }

        // Which weapon is firing right now, so the enchantments know what they apply to.
        private static EWeapon cur;
        private static bool Ranged(EWeapon id)
        {
            return id == EWeapon.Bow || id == EWeapon.FireStaff || id == EWeapon.Rockets || id == EWeapon.Revolver
                || id == EWeapon.Sniper || id == EWeapon.PoisonFlask;
        }

        private static float Damage(WeaponBase w)
        {
            float d = Stat(w, EStat.DamageMultiplier, 0f);
            if (d <= 0f) d = w.weaponData.damage;
            d *= Ranged(cur) ? Ench.ProjMul : Ench.MeleeMul;
            return Mathf.Max(1f, d);
        }

        private static int Count(WeaponBase w)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(Stat(w, EStat.Projectiles, 1f)));
            if (cur == EWeapon.Bow || cur == EWeapon.FireStaff || cur == EWeapon.Rockets || cur == EWeapon.Revolver || cur == EWeapon.LightningStaff)
                n += Ench.ExtraProjectiles;
            return n;
        }

        // ------------------------------------------------------------------ targeting and damage

        private static Vector3 Chest(MyPlayer p) { return p.transform.position + Vector3.up * (p.height * 0.6f); }

        // Colliders do not change owner, so the (slow) parent lookup is remembered; many enemies make Near() the hot spot.
        private static readonly Dictionary<int, Enemy> colOwner = new Dictionary<int, Enemy>();

        private static List<Enemy> Near(Vector3 pos, float radius)
        {
            var list = new List<Enemy>();
            var seen = new HashSet<int>();
            if (colOwner.Count > 6000) colOwner.Clear();
            foreach (var col in Physics.OverlapSphere(pos, radius))
            {
                int cid = col.GetInstanceID();
                Enemy e;
                if (!colOwner.TryGetValue(cid, out e) || e == null) { e = col.GetComponentInParent<Enemy>(); colOwner[cid] = e; }
                if (e == null || e.IsDead() || !seen.Add(e.GetInstanceID())) continue;
                list.Add(e);
            }
            return list;
        }

        private static Enemy Nearest(Vector3 pos, float radius)
        {
            Enemy best = null; float bd = float.MaxValue;
            foreach (var e in Near(pos, radius))
            {
                float d = (e.GetCenterPosition() - pos).sqrMagnitude;
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        private static void Deal(Enemy e, float dmg, Vector3 dir, float knock, string src)
        {
            if (e == null || e.IsDead()) return;
            var dc = new DamageContainer(1f, src);
            dc.damage = dmg;
            dc.direction = dir;
            dc.knockback = knock * Ench.KnockMul;
            dc.enemy = e;
            e.DamageFromPlayerOther(dc);
        }

        private static void Explode(Vector3 pos, float radius, float dmg, string src, float vol = 1f, float pitch = 1f, bool fx = true)
        {
            McSound.PlayAt("entity.generic.explode", pos, vol * Mathf.Clamp(radius / 5f, 0.5f, 1.6f), pitch * (0.9f + UnityEngine.Random.value * 0.25f));
            foreach (var e in Near(pos, radius))
            {
                Vector3 d = e.GetCenterPosition() - pos;
                float fall = 1f - Mathf.Clamp01(d.magnitude / radius) * 0.5f;
                Deal(e, dmg * fall, d.normalized, 6f, src);
            }
            if (!fx) return;
            Fx.Explosion(pos, radius);
        }

        // ------------------------------------------------------------------ weapons

        private static bool Fire(EWeapon id, WeaponBase w, MyPlayer p)
        {
            Vector3 origin = Chest(p);
            switch (id)
            {
                case EWeapon.Bow:
                {
                    var target = Nearest(origin, 70f);
                    if (target == null) return false;
                    int n = Count(w);
                    float dmg = Damage(w);
                    McSound.PlayAt("entity.skeleton.shoot", origin, 0.9f, 1f / (UnityEngine.Random.value * 0.4f + 0.8f));
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 to = target.GetCenterPosition() - origin;
                        float dist = to.magnitude;
                        float speed = 150f;
                        // Lead the drop: aim a bit above the target by the gravity it will fall on the way.
                        float t = dist / speed;
                        Vector3 aim = (to + Vector3.up * (0.5f * 50f * t * t)).normalized;
                        aim = Quaternion.AngleAxis((i - (n - 1) / 2f) * 6f, Vector3.up) * aim;
                        Spawn(new Proj { kind = PKind.Arrow, vel = aim * speed, gravity = 50f, drag = 0.99f, damage = dmg, src = "Skeleton Bow" },
                              origin + aim * 1.2f, McAssets.Tex("item/arrow"), 0.12f);
                    }
                    return true;
                }
                case EWeapon.FireStaff:
                {
                    var target = Nearest(origin, 60f);
                    if (target == null) return false;
                    int n = Count(w);
                    float dmg = Damage(w);
                    McSound.PlayAt("entity.blaze.shoot", origin, 0.8f, 1f + UnityEngine.Random.value * 0.2f);
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 dir = (target.GetCenterPosition() - origin).normalized;
                        dir = Quaternion.AngleAxis((i - (n - 1) / 2f) * 10f, Vector3.up) * dir;
                        Spawn(new Proj { kind = PKind.Fire, vel = dir * 40f, gravity = 0f, drag = 1f, damage = dmg, radius = 4f, homing = target, src = "Fire Charge" },
                              origin + dir * 1.5f, McAssets.Tex("item/fire_charge"), 0.1f);
                    }
                    return true;
                }
                case EWeapon.LightningStaff:
                {
                    var list = Near(origin, 50f);
                    if (list.Count == 0) return false;
                    int n = Mathf.Min(list.Count, Count(w));
                    float dmg = Damage(w);
                    for (int i = 0; i < n; i++)
                    {
                        var e = list[UnityEngine.Random.Range(0, list.Count)];
                        Vector3 at = e.transform.position;
                        Lightning(at);
                        Explode(at, 3f, dmg, "Trident of Channeling");
                    }
                    return true;
                }
                case EWeapon.Mine:
                {
                    // Only drop TNT when there is something around to hit, and never more than two lit at once.
                    if (Nearest(p.transform.position, 40f) == null) return false;
                    int lit = 0;
                    foreach (var pr in projs) if (pr.kind == PKind.Tnt) lit++;
                    if (lit >= 2) return false;
                    Vector3 f = Camera.main != null ? Camera.main.transform.forward : p.transform.forward;
                    f.y = 0f; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
                    Vector3 pos = p.transform.position + f * 3f + Vector3.up * 1f;
                    SpawnTnt(pos, Damage(w) * 1.5f);
                    return true;
                }
                case EWeapon.Sword:
                {
                    var list = Near(p.transform.position, 15f);
                    if (list.Count == 0) return false;
                    float dmg = Damage(w);
                    McSound.Play("entity.player.attack.sweep", 1f);
                    foreach (var e in list) Deal(e, dmg, (e.transform.position - p.transform.position).normalized, 0.8f, "Sweeping Edge");
                    SweepFx(p);
                    return true;
                }
                case EWeapon.Revolver:
                {
                    // Piercing crossbow: one heavy, fast bolt per shot that passes through up to four enemies.
                    var target = Nearest(origin, 85f);
                    if (target == null) return false;
                    float dmg = Damage(w) * 1.3f;
                    int n = Count(w);
                    float speed = 260f, grav = 10f;
                    McSound.PlayAt("item.crossbow.shoot", origin, 1f);
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 to = target.GetCenterPosition() - origin;
                        float t = to.magnitude / speed;
                        Vector3 aim = (to + Vector3.up * (0.5f * grav * t * t)).normalized;
                        aim = Quaternion.AngleAxis((i - (n - 1) / 2f) * 5f, Vector3.up) * aim;
                        var pr = new Proj { kind = PKind.Arrow, vel = aim * speed, gravity = grav, drag = 1f, damage = dmg, src = NameOf(id), pierce = 4, ignore = new HashSet<int>() };
                        Spawn(pr, origin + aim * 1.2f, McAssets.Tex("item/arrow"), 0.16f);
                    }
                    return true;
                }
                case EWeapon.Shotgun:
                {
                    // Mace: a ground slam that smashes everything close and throws it up and away.
                    var list = Near(p.transform.position, 14f);
                    if (list.Count == 0) return false;
                    float dmg = Damage(w) * 2.4f * (1f + 0.35f * LandHeight);   // the higher the fall, the harder the slam
                    McSound.PlayAt("item.mace.smash_ground", p.transform.position, 1f);
                    foreach (var e in list)
                    {
                        Vector3 d = e.transform.position - p.transform.position;
                        float fall = 1f - Mathf.Clamp01(d.magnitude / 14f) * 0.4f;
                        Deal(e, dmg * fall, d.normalized, 12f, "Mace");
                    }
                    Ring(p.transform.position + Vector3.up * 0.4f, 14f, new Color(0.45f, 0.42f, 0.4f), 36);
                    Burst(p.transform.position + Vector3.up * 0.4f, new Color(0.55f, 0.5f, 0.45f), 12, 18f);
                    return true;
                }
                case EWeapon.Sniper:
                {
                    // Sonic boom: an instant beam straight through everything between you and the target.
                    var target = Nearest(origin, 110f);
                    if (target == null) return false;
                    float dmg = Damage(w) * 2.2f;
                    Vector3 dir = (target.GetCenterPosition() - origin).normalized;
                    McSound.PlayAt("entity.warden.sonic_boom", origin, 1f);
                    var seen = new HashSet<int>();
                    foreach (var h in Physics.SphereCastAll(origin, 2.2f, dir, 120f))
                    {
                        var e = h.collider != null ? h.collider.GetComponentInParent<Enemy>() : null;
                        if (e == null || e.IsDead() || !seen.Add(e.GetInstanceID())) continue;
                        Deal(e, dmg, dir, 10f, "Sonic Boom");
                    }
                    for (float d = 3f; d < 120f; d += 4f)
                        Fx.SonicBoom(origin + dir * d, 7f);
                    return true;
                }
                case EWeapon.Rockets:
                {
                    var target = Nearest(origin, 70f);
                    if (target == null) return false;
                    int n = Count(w);
                    float dmg = Damage(w);
                    McSound.PlayAt("entity.firework_rocket.launch", origin, 0.9f);
                    n += 2;   // a salvo, fanned out, no homing: the counterpart to the homing Fire Charge
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 dir = (target.GetCenterPosition() - origin).normalized;
                        dir = Quaternion.Euler(UnityEngine.Random.Range(-3f, 3f), (i - (n - 1) / 2f) * 9f, 0f) * dir;
                        Spawn(new Proj { kind = PKind.Fire, vel = dir * 60f, gravity = 0f, drag = 1f, damage = dmg * 0.8f, radius = 5.5f, src = "Firework Rocket" },
                              origin + dir * 1.5f, McAssets.Tex("item/firework_rocket"), 0.12f);
                    }
                    return true;
                }
                case EWeapon.Tornado:
                {
                    var list = Near(p.transform.position, 20f);
                    if (list.Count == 0) return false;
                    float dmg = Damage(w);
                    McSound.PlayAt("entity.wind_charge.wind_burst", p.transform.position, 1f);
                    foreach (var e in list) Deal(e, dmg * 0.8f, (e.transform.position - p.transform.position).normalized, 16f, "Wind Charge");
                    Ring(p.transform.position + Vector3.up * 1.5f, 20f, new Color(0.8f, 0.95f, 0.95f), 30);
                    return true;
                }
                case EWeapon.PoisonFlask:
                {
                    var target = Nearest(origin, 55f);
                    if (target == null) return false;
                    float speed = 62f;
                    Vector3 to = target.GetCenterPosition() - origin;
                    float t = to.magnitude / speed;
                    Vector3 aim = (to + Vector3.up * (0.5f * 50f * t * t)).normalized;
                    McSound.PlayAt("entity.splash_potion.throw", origin, 0.9f);
                    Spawn(new Proj { kind = PKind.Potion, vel = aim * speed, gravity = 50f, drag = 1f, damage = Damage(w), radius = 9f, src = "Splash Potion" },
                          origin + aim * 1.2f, McAssets.Tex("item/splash_potion"), 0.12f);
                    return true;
                }
                case EWeapon.Chunkers:
                {
                    var list = Near(origin, 50f);
                    if (list.Count == 0) return false;
                    var e = list[UnityEngine.Random.Range(0, list.Count)];
                    var a = new Proj { kind = PKind.Anvil, vel = Vector3.down * 40f, gravity = 90f, drag = 1f, damage = Damage(w) * 2.2f, radius = 6f, src = "Anvil" };
                    a.go = AnvilModel(e.transform.position + Vector3.up * 40f);
                    projs.Add(a);
                    return true;
                }
            }
            return false;
        }

        // The vanilla anvil silhouette: base, waist, neck and a long top, from the real block texture.
        private static GameObject AnvilModel(Vector3 pos)
        {
            var root = new GameObject("anvil");
            root.transform.position = pos;
            var tex = McAssets.Tex("block/anvil");
            var mat = tex != null ? TexMat(tex) : ColorMat(new Color(0.22f, 0.22f, 0.25f));
            float s = 0.2f;   // world units per pixel
            void Part(Vector3 c, Vector3 size)
            {
                var g = CubeObj(root.transform.position, size * s, mat);
                g.transform.SetParent(root.transform, true);
                g.transform.localPosition = c * s;
            }
            Part(new Vector3(0, -6f, 0), new Vector3(12, 4, 12));      // base
            Part(new Vector3(0, -3.5f, 0), new Vector3(8, 1, 10));     // waist
            Part(new Vector3(0, -0.5f, 0), new Vector3(4, 5, 8));      // neck
            Part(new Vector3(0, 5f, 0), new Vector3(10, 6, 16));       // top, long axis forward
            return root;
        }

        // The vanilla sweep_attack particle: eight white frames of a curved slash, shown in front of the player.
        private class Sweep { public GameObject go; public MeshFilter mf; public MeshRenderer mr; public float t; public Vector3 pos; }
        private static readonly List<Sweep> sweeps = new List<Sweep>();
        private static Mesh[] sweepMeshes; private static Material[] sweepMats;

        private static void SweepFx(MyPlayer p)
        {
            if (sweepMeshes == null)
            {
                sweepMeshes = new Mesh[8]; sweepMats = new Material[8];
                for (int i = 0; i < 8; i++)
                {
                    var t = McAssets.Tex("particle/sweep_" + i);
                    if (t == null) continue;
                    sweepMeshes[i] = ItemMesh.Build(t);
                    sweepMats[i] = TexMat(t);
                }
            }
            if (sweepMeshes[0] == null || sweepMats[0] == null) return;
            Vector3 f = Camera.main != null ? Camera.main.transform.forward : p.transform.forward;
            f.y = 0f; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
            var go = new GameObject("sweep");
            go.transform.localScale = Vector3.one * 0.9f;
            var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = sweepMeshes[0];
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = sweepMats[0];
            sweeps.Add(new Sweep { go = go, mf = mf, mr = mr, t = 0f, pos = p.transform.position + Vector3.up * (p.height * 0.55f) + f * 7f });
        }

        // A flat ring of puffs spreading outward (sweep and wind effects).
        private static void Ring(Vector3 center, float radius, Color c, int count)
        {
            var f = Fx.Get("generic", 8, c);
            for (int i = 0; i < count; i++)
            {
                float ang = i / (float)count * Mathf.PI * 2f;
                Vector3 v = new Vector3(Mathf.Cos(ang), 0.05f, Mathf.Sin(ang)) * (radius * 2.2f);
                Fx.Spawn(f, center, v, UnityEngine.Random.Range(1.6f, 2.6f), 0.5f, 0f, 3f);
            }
        }

        // Lingering effect on the ground (the splash potion's poison cloud).
        private class Zone { public Vector3 pos; public float until, next, dmg, radius; public string src; }
        // Poison keeps ticking for a few seconds after the enemy leaves the cloud.
        private class Poison { public Enemy e; public float until, next, dmg; public string src; }
        private static readonly Dictionary<int, Poison> poisoned = new Dictionary<int, Poison>();
        private static readonly List<Zone> zones = new List<Zone>();

        // ------------------------------------------------------------------ projectiles and effects

        private enum PKind { Arrow, Fire, Tnt, EnemyArrow, EnemyBall, Potion, Anvil }

        private class Proj
        {
            public PKind kind;
            public GameObject go;
            public Vector3 vel;
            public float gravity, drag = 1f, damage, radius, life, fuse;
            public Enemy homing;
            public bool stuck;
            public Enemy shooter;
            public bool homePlayer;
            public int pierce;
            public HashSet<int> ignore;
            public string src;
            public Renderer rend;
            public float trailT;
            public Material tntWhite, tntNormal;
        }

        private class Particle { public GameObject go; public Vector3 vel; public float life, max, width; }

        // Used by the mob models: an arrow shot by an enemy, and a blast that hurts other enemies.
        public static void SpawnEnemyArrow(Vector3 pos, Vector3 vel, float dmg, Enemy shooter)
        {
            Spawn(new Proj { kind = PKind.EnemyArrow, vel = vel, gravity = 50f, drag = 0.99f, damage = dmg, shooter = shooter, src = "Skeleton" },
                  pos, McAssets.Tex("item/arrow"), 0.12f);
        }

        // A boss projectile: a glowing cube that steers toward the player and blasts on impact.
        public static void SpawnEnemyBall(Vector3 pos, Vector3 vel, float dmg, Enemy shooter, Color c, float radius, float size)
        {
            var p = new Proj { kind = PKind.EnemyBall, vel = vel, gravity = 0f, drag = 1f, damage = dmg, radius = radius, shooter = shooter, homePlayer = true, src = "Boss" };
            p.go = CubeObj(pos, Vector3.one * size, ColorMat(c));
            projs.Add(p);
        }

        public static void EnemyBlast(Vector3 pos, float radius, float dmg, Enemy shooter)
        {
            McSound.PlayAt("entity.generic.explode", pos, 1.2f, 0.9f + UnityEngine.Random.value * 0.2f);
            Fx.Explosion(pos, radius);
            Fx.Puffs(pos, new Color(0.6f, 0.3f, 0.9f), 8, radius * 1.2f, 2.5f);
            var pl = MyPlayer.Instance;
            if (pl == null) return;
            Vector3 d = pl.transform.position + Vector3.up * (pl.height * 0.5f) - pos;
            if (d.magnitude >= radius) return;
            try
            {
                var ph = pl.inventory != null ? pl.inventory.playerHealth : null;
                if (ph != null) ph.DamagePlayerExternal(dmg * (1f - d.magnitude / radius), 6f, d.normalized, false, "Boss", enemy: shooter);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning("boss blast: " + ex.Message); }
        }

        public static void ExplodeAt(Vector3 pos, float radius, float dmg, string src, Enemy except)
        {
            McSound.PlayAt("entity.generic.explode", pos, 1.4f, 0.9f + UnityEngine.Random.value * 0.2f);
            foreach (var e in Near(pos, radius))
            {
                if (e == except) continue;
                Vector3 d = e.GetCenterPosition() - pos;
                Deal(e, dmg * (1f - Mathf.Clamp01(d.magnitude / radius) * 0.5f), d.normalized, 6f, src);
            }
            Fx.Explosion(pos, radius);
        }

        private static readonly List<Proj> projs = new List<Proj>();
        private static readonly List<Particle> particles = new List<Particle>();
        private static readonly Dictionary<Color, Material> colorMats = new Dictionary<Color, Material>();
        private static Mesh cube;

        private static Material TexMat(Texture2D tex)
        {
            if (baseMat == null) return null;
            var m = Keep.It(new Material(baseMat));
            m.mainTexture = tex;
            foreach (var n in new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo" })
                if (m.HasProperty(n)) m.SetTexture(n, tex);
            return m;
        }

        private static Material ColorMat(Color c)
        {
            Material m;
            if (colorMats.TryGetValue(c, out m) && m != null) return m;
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, c); t.Apply(); t.filterMode = FilterMode.Point;
            Keep.It(t);
            m = TexMat(t);
            if (m != null) colorMats[c] = m;
            return m;
        }

        private static Mesh Cube()
        {
            if (cube != null) return cube;
            var verts = new Il2CppSystem.Collections.Generic.List<Vector3>();
            var uvs = new Il2CppSystem.Collections.Generic.List<Vector2>();
            var tris = new Il2CppSystem.Collections.Generic.List<int>();
            Vector3[] n = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down };
            for (int f = 0; f < 6; f++)
            {
                Vector3 nn = n[f];
                Vector3 a = Vector3.Cross(nn, Mathf.Abs(nn.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
                Vector3 b = Vector3.Cross(nn, a);
                int i0 = verts.Count;
                verts.Add((nn - a - b) * 0.5f); verts.Add((nn + a - b) * 0.5f); verts.Add((nn + a + b) * 0.5f); verts.Add((nn - a + b) * 0.5f);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                bool flip = Vector3.Dot(Vector3.Cross(verts[i0 + 1] - verts[i0], verts[i0 + 2] - verts[i0]), nn) < 0f;
                int[] order = flip ? new[] { 0, 2, 1, 0, 3, 2 } : new[] { 0, 1, 2, 0, 2, 3 };
                foreach (var o in order) tris.Add(i0 + o);
            }
            var m = new Mesh();
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            cube = Keep.It(m);
            return cube;
        }

        private static GameObject CubeObj(Vector3 pos, Vector3 scale, Material m)
        {
            var go = new GameObject("fx");
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = Cube();
            if (m != null) go.AddComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        private static void Spawn(Proj p, Vector3 pos, Texture2D icon, float scale)
        {
            if (icon != null && baseMat != null)
            {
                var go = new GameObject(p.kind.ToString());
                go.transform.position = pos;
                go.AddComponent<MeshFilter>().sharedMesh = ItemMesh.Build(icon);
                go.AddComponent<MeshRenderer>().sharedMaterial = TexMat(icon);
                go.transform.localScale = Vector3.one * scale;
                p.go = go;
            }
            else p.go = new GameObject(p.kind.ToString());
            p.go.transform.position = pos;
            projs.Add(p);
        }

        // The TNT block: red sides with the label, the tan fuse top and the plain bottom, packed into one texture.
        private static Mesh tntMesh; private static Texture2D tntAtlas;

        private static bool BuildTntAtlas()
        {
            if (tntMesh != null && tntAtlas != null) return true;
            var side = McAssets.Tex("block/tnt_side"); var top = McAssets.Tex("block/tnt_top"); var bottom = McAssets.Tex("block/tnt_bottom");
            if (side == null || top == null || bottom == null || side.width != top.width || side.width != bottom.width) return false;
            int s = side.width;
            var atlas = new Texture2D(s * 3, s, TextureFormat.RGBA32, false);
            var px = new Color32[s * 3 * s];
            var tiles = new[] { side.GetPixels32(), top.GetPixels32(), bottom.GetPixels32() };
            for (int t = 0; t < 3; t++)
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++) px[y * s * 3 + t * s + x] = tiles[t][y * s + x];
            atlas.SetPixels32(px); atlas.Apply(); atlas.filterMode = FilterMode.Point;
            tntAtlas = Keep.It(atlas);

            var verts = new Il2CppSystem.Collections.Generic.List<Vector3>();
            var uvs = new Il2CppSystem.Collections.Generic.List<Vector2>();
            var tris = new Il2CppSystem.Collections.Generic.List<int>();
            Vector3[] n = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down };
            int[] tile = { 0, 0, 0, 0, 1, 2 };
            for (int f = 0; f < 6; f++)
            {
                Vector3 nn = n[f];
                Vector3 a = Vector3.Cross(nn, Mathf.Abs(nn.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
                Vector3 b = Vector3.Cross(nn, a);
                int i0 = verts.Count;
                verts.Add((nn - a - b) * 0.5f); verts.Add((nn + a - b) * 0.5f); verts.Add((nn + a + b) * 0.5f); verts.Add((nn - a + b) * 0.5f);
                float u0 = tile[f] / 3f, u1 = (tile[f] + 1) / 3f;
                uvs.Add(new Vector2(u0, 0)); uvs.Add(new Vector2(u1, 0)); uvs.Add(new Vector2(u1, 1)); uvs.Add(new Vector2(u0, 1));
                bool flip = Vector3.Dot(Vector3.Cross(verts[i0 + 1] - verts[i0], verts[i0 + 2] - verts[i0]), nn) < 0f;
                int[] order = flip ? new[] { 0, 2, 1, 0, 3, 2 } : new[] { 0, 1, 2, 0, 2, 3 };
                foreach (var o in order) tris.Add(i0 + o);
            }
            var m = new Mesh();
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            tntMesh = Keep.It(m);
            return true;
        }

        private static void SpawnTnt(Vector3 pos, float dmg)
        {
            var side = McAssets.Tex("block/tnt_side");
            var p = new Proj { kind = PKind.Tnt, damage = dmg, radius = 7f, fuse = 4f, src = "TNT" };
            bool atlas = BuildTntAtlas();
            var matN = atlas ? TexMat(tntAtlas) : (side != null ? TexMat(side) : ColorMat(new Color(0.8f, 0.2f, 0.2f)));
            p.tntNormal = matN;
            p.tntWhite = ColorMat(Color.white);
            McSound.PlayAt("entity.tnt.primed", pos, 1f);
            p.go = CubeObj(pos, Vector3.one * 2.2f, matN);
            if (atlas) p.go.GetComponent<MeshFilter>().sharedMesh = tntMesh;
            p.rend = p.go.GetComponent<Renderer>();
            projs.Add(p);
        }

        private static readonly List<LightningBolt> bolts = new List<LightningBolt>();

        // Vanilla lightning: the bolt itself plus thunder, the impact bang and a flash of the sky.
        private static void Lightning(Vector3 at)
        {
            bolts.Add(new LightningBolt(at));
            McSound.PlayAt("entity.lightning_bolt.thunder", at, 1.6f, 0.8f + UnityEngine.Random.value * 0.4f);
            McSound.PlayAt("entity.lightning_bolt.impact", at, 1.2f, 0.9f + UnityEngine.Random.value * 0.2f);
            Burst(at, new Color(0.85f, 0.9f, 1f), 10, 6f);
        }
        private static void Burst(Vector3 pos, Color c, int count, float speed)
        {
            Fx.Puffs(pos, c, count, speed * 0.5f, 1.6f);
        }

        public static void Clear()
        {
            foreach (var p in projs) if (p.go != null) UnityEngine.Object.Destroy(p.go);
            foreach (var p in particles) if (p.go != null) UnityEngine.Object.Destroy(p.go);
            projs.Clear(); particles.Clear(); nextFire.Clear(); logged.Clear(); bolts.Clear(); zones.Clear(); sweeps.Clear(); Fx.Clear(); awakened.Clear(); lastInv = null; poisoned.Clear();
        }

        // Called every frame while the game is running.
        public static void TickEffects()
        {
            float dt = Time.deltaTime;
            Fx.Tick(dt);
            if (poisoned.Count > 0)
            {
                List<int> done = null;
                foreach (var kv in poisoned)
                {
                    var ps = kv.Value;
                    if (ps.e == null || ps.e.IsDead() || Time.time > ps.until) { (done ?? (done = new List<int>())).Add(kv.Key); continue; }
                    if (Time.time < ps.next) continue;
                    ps.next = Time.time + 0.5f;
                    Deal(ps.e, ps.dmg, Vector3.up, 0f, ps.src);
                    Fx.Swirl(ps.e.GetCenterPosition(), new Color(0.35f, 0.9f, 0.35f));
                }
                if (done != null) foreach (var k in done) poisoned.Remove(k);
            }
            for (int i = bolts.Count - 1; i >= 0; i--)
            {
                bolts[i].Update(dt);
                if (bolts[i].Done) bolts.RemoveAt(i);
            }
            for (int i = sweeps.Count - 1; i >= 0; i--)
            {
                var sw = sweeps[i];
                sw.t += dt;
                int frame = (int)(sw.t / 0.04f);
                if (frame >= 8 || sw.go == null) { if (sw.go != null) UnityEngine.Object.Destroy(sw.go); sweeps.RemoveAt(i); continue; }
                if (sweepMeshes[frame] != null) { sw.mf.sharedMesh = sweepMeshes[frame]; sw.mr.sharedMaterial = sweepMats[frame]; }
                sw.go.transform.position = sw.pos;
                if (Camera.main != null) sw.go.transform.rotation = Camera.main.transform.rotation;
            }
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                var z = zones[i];
                if (Time.time > z.until) { zones.RemoveAt(i); continue; }
                if (Time.time < z.next) continue;
                z.next = Time.time + 0.5f;
                foreach (var e in Near(z.pos, z.radius))
                {
                    Deal(e, z.dmg, Vector3.up, 0f, z.src);
                    poisoned[e.GetInstanceID()] = new Poison { e = e, until = Time.time + 4f, next = Time.time + 0.5f, dmg = z.dmg, src = z.src };
                }
                Fx.Swirl(z.pos + UnityEngine.Random.insideUnitSphere * (z.radius * 0.6f), new Color(0.35f, 0.9f, 0.35f));
            }
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.life += dt;
                if (p.go == null || p.life >= p.max) { if (p.go != null) UnityEngine.Object.Destroy(p.go); particles.RemoveAt(i); continue; }
                p.vel.y -= 25f * dt;
                p.go.transform.position += p.vel * dt;
                float s = 1f - p.life / p.max;
                if (p.vel != Vector3.zero) p.go.transform.localScale = p.go.transform.localScale * (1f - dt * 1.5f);
                else
                {
                    // Bolt segments flicker and thin out; a flash of white at the very start.
                    float flick = (p.life < 0.08f || (int)(p.life * 40f) % 2 == 0) ? 1f : 0.55f;
                    p.go.transform.localScale = new Vector3(p.width * s * flick, p.go.transform.localScale.y, p.width * s * flick);
                }
            }

            for (int i = projs.Count - 1; i >= 0; i--)
            {
                var p = projs[i];
                if (p.go == null) { projs.RemoveAt(i); continue; }
                p.life += dt;

                if (p.kind == PKind.Tnt)
                {
                    p.fuse -= dt;
                    // Falls until it rests on something.
                    RaycastHit gh;
                    if (!Physics.Raycast(p.go.transform.position + Vector3.up * 0.2f, Vector3.down, out gh, 1.4f))
                        p.go.transform.position += Vector3.down * 12f * dt;
                    // Blinks white every 0.25 s like vanilla, and swells just before the blast.
                    bool white = ((int)(p.life / 0.25f)) % 2 == 1;
                    if (p.rend != null && p.tntWhite != null) p.rend.sharedMaterial = white ? p.tntWhite : p.tntNormal;
                    float swell = 1f + Mathf.Clamp01(1f - p.fuse) * 0.25f;
                    p.go.transform.localScale = Vector3.one * 2.2f * swell;
                    if (p.fuse <= 0f)
                    {
                        Explode(p.go.transform.position, p.radius, p.damage, p.src);
                        UnityEngine.Object.Destroy(p.go); projs.RemoveAt(i);
                    }
                    continue;
                }

                if (p.stuck)
                {
                    if (p.life > 12f) { UnityEngine.Object.Destroy(p.go); projs.RemoveAt(i); }
                    continue;
                }

                // Flight
                if (p.homePlayer)
                {
                    var hp = MyPlayer.Instance;
                    if (hp != null)
                    {
                        Vector3 want = (hp.transform.position + Vector3.up * (hp.height * 0.5f) - p.go.transform.position).normalized * p.vel.magnitude;
                        p.vel = Vector3.Lerp(p.vel, want, Mathf.Clamp01(dt * 1.6f));
                    }
                    p.go.transform.Rotate(180f * dt, 240f * dt, 0f);
                }
                if (p.homing != null && !p.homing.IsDead())
                {
                    Vector3 want = (p.homing.GetCenterPosition() - p.go.transform.position).normalized * p.vel.magnitude;
                    p.vel = Vector3.Lerp(p.vel, want, Mathf.Clamp01(dt * 4f));
                }
                p.vel *= Mathf.Pow(p.drag, dt * 20f);
                p.vel.y -= p.gravity * dt;
                p.trailT -= dt;
                if (p.trailT <= 0f && (p.kind == PKind.Fire || p.kind == PKind.EnemyBall))
                {
                    p.trailT = 0.05f;
                    if (p.src == "Firework Rocket") Fx.Spark(p.go.transform.position, 1.2f); else Fx.Flame(p.go.transform.position, 1.3f);
                }
                Vector3 from = p.go.transform.position;
                Vector3 step = p.vel * dt;
                float len = step.magnitude;
                bool hitIt = false; RaycastHit hit = default(RaycastHit);
                if (len > 0.0001f)
                {
                    float best = float.MaxValue;
                    bool hostile = p.kind == PKind.EnemyArrow || p.kind == PKind.EnemyBall;   // an enemy's arrow hits the player and passes through mobs
                    foreach (var h in Physics.RaycastAll(from, step.normalized, len + 0.2f))
                    {
                        if (h.collider == null) continue;
                        bool isPlayer = h.collider.GetComponentInParent<MyPlayer>() != null;
                        bool isEnemy = h.collider.GetComponentInParent<Enemy>() != null;
                        if (hostile ? isEnemy : isPlayer) continue;
                        if (isEnemy && p.ignore != null)
                        {
                            var ie = h.collider.GetComponentInParent<Enemy>();
                            if (ie != null && p.ignore.Contains(ie.GetInstanceID())) continue;
                        }
                        if (h.collider.isTrigger && !(hostile ? isPlayer : isEnemy)) continue;
                        if (h.distance < best) { best = h.distance; hit = h; hitIt = true; }
                    }
                }
                if (hitIt && p.kind == PKind.EnemyBall)
                {
                    EnemyBlast(hit.point, p.radius, p.damage, p.shooter);
                    UnityEngine.Object.Destroy(p.go); projs.RemoveAt(i);
                    continue;
                }
                if (hitIt && p.kind == PKind.EnemyArrow)
                {
                    var player = hit.collider.GetComponentInParent<MyPlayer>();
                    if (player != null)
                    {
                        // Enemy arrows only shove you back: no damage (a shield stops the shove too).
                        var mm = MinecraftMode.Instance;
                        if (mm != null) mm.AddKnockback(p.vel.normalized, 0.55f);
                        McSound.PlayAt("entity.arrow.hit", hit.point, 0.9f);
                        UnityEngine.Object.Destroy(p.go); projs.RemoveAt(i);
                    }
                    else
                    {
                        p.go.transform.position = hit.point - p.vel.normalized * 0.2f;
                        p.stuck = true; p.life = 0f;
                        McSound.PlayAt("entity.arrow.hit", hit.point, 0.7f);
                    }
                    continue;
                }
                if (hitIt && p.kind == PKind.Potion)
                {
                    McSound.PlayAt("entity.splash_potion.break", hit.point, 1f);
                    Fx.Splash(hit.point);
                    zones.Add(new Zone { pos = hit.point, until = Time.time + 5f, next = Time.time, dmg = Mathf.Max(2f, p.damage * 0.8f), radius = p.radius, src = p.src });
                    UnityEngine.Object.Destroy(p.go); projs.RemoveAt(i);
                    continue;
                }
                if (hitIt && p.kind == PKind.Anvil)
                {
                    McSound.PlayAt("block.anvil.land", hit.point, 1.2f);
                    Explode(hit.point, p.radius, p.damage, p.src, 0.9f, 0.7f, false);   // a thud, no flying rocks
                    UnityEngine.Object.Destroy(p.go); projs.RemoveAt(i);
                    continue;
                }
                if (hitIt)
                {
                    var enemy = hit.collider.GetComponentInParent<Enemy>();
                    if (p.kind == PKind.Arrow && enemy != null && p.pierce > 0)
                    {
                        // piercing arrows keep flying through the enemy they just hit
                        Deal(enemy, p.damage, p.vel.normalized, 3f, p.src);
                        p.ignore.Add(enemy.GetInstanceID());
                        p.pierce--;
                        p.go.transform.position = hit.point + p.vel.normalized * 0.6f;
                        continue;
                    }
                    if (p.kind == PKind.Fire) Explode(hit.point, p.radius, p.damage, p.src, 0.6f, 1.35f);
                    else if (enemy != null) Deal(enemy, p.damage, p.vel.normalized, 3f, p.src);
                    if (p.kind == PKind.Arrow && enemy == null)
                    {
                        p.go.transform.position = hit.point - p.vel.normalized * 0.2f;
                        p.stuck = true; p.life = 0f;
                    }
                    else { UnityEngine.Object.Destroy(p.go); projs.RemoveAt(i); }
                    continue;
                }
                p.go.transform.position = from + step;
                if ((p.kind == PKind.Arrow || p.kind == PKind.EnemyArrow) && p.vel.sqrMagnitude > 0.01f)
                    p.go.transform.rotation = Quaternion.LookRotation(p.vel) * Quaternion.Euler(0f, -90f, 0f) * Quaternion.Euler(0f, 0f, -45f);
                else if ((p.kind == PKind.Fire || p.kind == PKind.Potion) && Camera.main != null) p.go.transform.rotation = Camera.main.transform.rotation; // billboard
                if (p.life > 8f)
                {
                    if (p.kind == PKind.Fire) Explode(p.go.transform.position, p.radius, p.damage, p.src);
                    UnityEngine.Object.Destroy(p.go); projs.RemoveAt(i);
                }
            }
        }
    }
}










