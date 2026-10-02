using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace MegabonkSteve
{
    // Where the Minecraft resources (textures, sounds) come from: the player's own Minecraft install if there is one,
    // otherwise a copy downloaded on request from Mojang's official servers into BepInEx\config\MegabonkSteve_resources.
    internal static class McResources
    {
        public static string MinecraftDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft"); } }
        public static string CacheDir { get { return Path.Combine(BepInEx.Paths.ConfigPath, "MegabonkSteve_resources"); } }
        private static string Marker { get { return Path.Combine(CacheDir, "complete.txt"); } }

        // The sound events the mod plays; only their files are downloaded.
        private static readonly string[] events =
        {
            "block.anvil.land", "block.chest.close", "block.chest.locked", "block.chest.open", "block.enchantment_table.use", "block.grass.step",
            "entity.arrow.hit", "entity.arrow.hit_player", "entity.arrow.shoot", "entity.blaze.shoot", "entity.creeper.primed", "entity.ender_dragon.shoot",
            "entity.ender_pearl.throw", "entity.enderman.teleport", "entity.experience_orb.pickup", "entity.firework_rocket.launch", "entity.generic.big_fall",
            "entity.generic.drink", "entity.generic.eat", "entity.generic.explode", "entity.generic.small_fall", "entity.item.pickup",
            "entity.lightning_bolt.impact", "entity.lightning_bolt.thunder", "entity.player.attack.crit", "entity.player.attack.knockback",
            "entity.player.attack.strong", "entity.player.attack.sweep", "entity.player.attack.weak", "entity.player.burp", "entity.player.death",
            "entity.player.hurt", "entity.player.levelup", "entity.player.teleport", "entity.skeleton.shoot", "entity.splash_potion.break",
            "entity.splash_potion.throw", "entity.tnt.primed", "entity.warden.sonic_boom", "entity.wind_charge.wind_burst", "entity.wither.shoot",
            "item.armor.equip_diamond", "item.armor.equip_elytra", "item.armor.equip_netherite", "item.armor.equip_chain", "item.armor.equip_iron",
            "item.armor.equip_gold", "item.crossbow.shoot", "item.elytra.flying", "item.mace.smash_air", "item.mace.smash_ground", "item.shield.block",
            "item.shield.break", "item.totem.use", "ui.toast.challenge_complete",
        };

        // True when a Minecraft install has the client jar and an asset index.
        private static bool Usable(string root)
        {
            try
            {
                string versions = Path.Combine(root, "versions");
                string indexes = Path.Combine(root, "assets", "indexes");
                if (!Directory.Exists(versions) || !Directory.Exists(indexes) || Directory.GetFiles(indexes, "*.json").Length == 0) return false;
                foreach (var jar in Directory.GetFiles(versions, "*.jar", SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(jar).StartsWith("fabric")) continue;
                    using (var z = ZipFile.OpenRead(jar))
                        if (z.GetEntry("assets/minecraft/textures/gui/sprites/hud/hotbar.png") != null) return true;
                }
            }
            catch { }
            return false;
        }

        // The folder (laid out like a .minecraft) to read resources from, or null when there are none.
        public static string Root()
        {
            // A file named MegabonkSteve_force_cache.txt in the config folder makes the mod use only the downloaded copy (for testing).
            bool forceCache = File.Exists(Path.Combine(BepInEx.Paths.ConfigPath, "MegabonkSteve_force_cache.txt"));
            if (!forceCache && Usable(MinecraftDir)) return MinecraftDir;
            if (File.Exists(Marker) && Usable(CacheDir)) return CacheDir;
            return null;
        }

        public static bool NeedsDownload { get { return Root() == null; } }

        // ------------------------------------------------------------------ download

        public static volatile string Status = "";
        public static volatile float Progress;
        public static volatile bool Running, Done, Failed;

        public static void Start()
        {
            if (Running) return;
            Running = true; Done = false; Failed = false; Progress = 0f; Status = "Starting...";
            Task.Run(() =>
            {
                try { Download().GetAwaiter().GetResult(); Done = true; Status = "Done. Restart the game to use the Minecraft resources."; Progress = 1f; }
                catch (Exception e) { Failed = true; Status = "Failed: " + e.Message; Plugin.Logger.LogError("resource download: " + e); }
                finally { Running = false; }
            });
        }

        private static async Task<string> GetString(HttpClient http, string url)
        {
            return await http.GetStringAsync(url);
        }

        private static async Task Fetch(HttpClient http, string url, string dest)
        {
            if (File.Exists(dest) && new FileInfo(dest).Length > 0) return;
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            string tmp = dest + ".part";
            using (var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                resp.EnsureSuccessStatusCode();
                using (var src = await resp.Content.ReadAsStreamAsync())
                using (var dst = File.Create(tmp)) await src.CopyToAsync(dst);
            }
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(tmp, dest);
        }

        private static async Task Download()
        {
            using (var http = new HttpClient())
            {
                http.Timeout = TimeSpan.FromMinutes(5);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("MegabonkSteve-mod/1.0");
                Status = "Reading Mojang's version list...";
                string manifest = await GetString(http, "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");
                string versionUrl = null, versionId = null;
                using (var m = JsonDocument.Parse(manifest))
                {
                    // 26.2 is the version this mod was built and tested against; use it if Mojang still lists it, else the latest release.
                    versionId = "26.2";
                    bool listed = false;
                    foreach (var v0 in m.RootElement.GetProperty("versions").EnumerateArray()) if (v0.GetProperty("id").GetString() == versionId) { listed = true; break; }
                    if (!listed) versionId = m.RootElement.GetProperty("latest").GetProperty("release").GetString();
                    foreach (var v in m.RootElement.GetProperty("versions").EnumerateArray())
                        if (v.GetProperty("id").GetString() == versionId) { versionUrl = v.GetProperty("url").GetString(); break; }
                }
                if (versionUrl == null) throw new Exception("no release found");

                Status = "Reading version " + versionId + "...";
                string vjson = await GetString(http, versionUrl);
                string clientUrl, indexUrl, indexId;
                using (var v = JsonDocument.Parse(vjson))
                {
                    clientUrl = v.RootElement.GetProperty("downloads").GetProperty("client").GetProperty("url").GetString();
                    indexUrl = v.RootElement.GetProperty("assetIndex").GetProperty("url").GetString();
                    indexId = v.RootElement.GetProperty("assetIndex").GetProperty("id").GetString();
                }
                string vdir = Path.Combine(CacheDir, "versions", versionId);
                Directory.CreateDirectory(vdir);
                File.WriteAllText(Path.Combine(vdir, versionId + ".json"), vjson);

                Status = "Downloading the Minecraft client (about 40 MB)...";
                Progress = 0.05f;
                await Fetch(http, clientUrl, Path.Combine(vdir, versionId + ".jar"));
                Progress = 0.55f;

                Status = "Reading the asset index...";
                string indexPath = Path.Combine(CacheDir, "assets", "indexes", indexId + ".json");
                await Fetch(http, indexUrl, indexPath);
                var objects = new Dictionary<string, string>();   // key -> hash
                using (var idx = JsonDocument.Parse(File.ReadAllText(indexPath)))
                    foreach (var o in idx.RootElement.GetProperty("objects").EnumerateObject())
                        objects[o.Name] = o.Value.GetProperty("hash").GetString();

                string objDir = Path.Combine(CacheDir, "assets", "objects");
                Func<string, Task> getObj = async hash =>
                    await Fetch(http, "https://resources.download.minecraft.net/" + hash.Substring(0, 2) + "/" + hash, Path.Combine(objDir, hash.Substring(0, 2), hash));

                string soundsHash;
                if (!objects.TryGetValue("minecraft/sounds.json", out soundsHash)) throw new Exception("sounds.json missing in the index");
                Status = "Downloading sound list...";
                await getObj(soundsHash);

                // Only the files behind the events this mod plays.
                var wanted = new HashSet<string>();
                using (var d = JsonDocument.Parse(File.ReadAllText(Path.Combine(objDir, soundsHash.Substring(0, 2), soundsHash))))
                    foreach (var ev in events)
                    {
                        JsonElement e;
                        if (!d.RootElement.TryGetProperty(ev, out e) || !e.TryGetProperty("sounds", out var arr)) continue;
                        foreach (var s in arr.EnumerateArray())
                        {
                            string name = s.ValueKind == JsonValueKind.String ? s.GetString() : s.GetProperty("name").GetString();
                            wanted.Add("minecraft/sounds/" + name + ".ogg");
                        }
                    }
                var hashes = wanted.Where(k => objects.ContainsKey(k)).Select(k => objects[k]).Distinct().ToList();
                int n = 0;
                foreach (var h in hashes)
                {
                    Status = "Downloading sounds " + (n + 1) + "/" + hashes.Count + "...";
                    await getObj(h);
                    n++;
                    Progress = 0.6f + 0.4f * n / Math.Max(1, hashes.Count);
                }
                File.WriteAllText(Marker, versionId + " " + DateTime.UtcNow.ToString("o"));
                Plugin.Logger.LogInfo("Minecraft resources downloaded: " + versionId + ", " + hashes.Count + " sounds");
            }
        }
    }

    // First-run prompt: offers to download the Minecraft resources when no Minecraft install is found.
    public class ResourcePrompt : MonoBehaviour
    {
        public ResourcePrompt(IntPtr ptr) : base(ptr) { }

        private GameObject canvasGo;
        private Text body;
        private RawImage barFill;
        private Rect btnYes, btnNo;
        private Text yesLabel, noLabel;
        private RectTransform panel;
        private CanvasScaler scaler;
        private bool shown, dismissed, checkedOnce;
        private float startTime;
        private CursorLockMode savedLock; private bool savedVisible;
        private const float W = 300f, H = 120f;

        public static bool IsOpen;

        private static GameObject NewUi(string name, params Type[] comps)
        {
            var arr = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Type>(comps.Length);
            for (int i = 0; i < comps.Length; i++) arr[i] = Il2CppInterop.Runtime.Il2CppType.From(comps[i]);
            return new GameObject(name, arr);
        }

        private RawImage Box(Transform parent, float x, float y, float w, float h, Color c)
        {
            var go = NewUi("box", typeof(RectTransform), typeof(RawImage));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
            var img = go.GetComponent<RawImage>();
            img.texture = Texture2D.whiteTexture; img.color = c; img.raycastTarget = false;
            return img;
        }

        private Text Label(Transform parent, string s, float x, float y, float w, float h, int size, TextAnchor a)
        {
            var go = NewUi("t", typeof(RectTransform), typeof(Text), typeof(Shadow));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
            var t = go.GetComponent<Text>();
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f != null) t.font = f;
            t.fontSize = size; t.alignment = a; t.color = Color.white; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = s;
            return t;
        }

        private void Build()
        {
            canvasGo = new GameObject("MC_RESOURCES");
            UnityEngine.Object.DontDestroyOnLoad(canvasGo);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 6300;
            scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            var bg = Box(canvasGo.transform, 0, 0, W, H, new Color(0.08f, 0.08f, 0.08f, 0.95f));
            panel = bg.rectTransform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f); panel.pivot = new Vector2(0.5f, 0.5f); panel.anchoredPosition = Vector2.zero;
            Label(panel, "Minecraft Steve mod", 0f, 6f, W, 12f, 9, TextAnchor.UpperCenter);
            body = Label(panel, "", 12f, 22f, W - 24f, 56f, 7, TextAnchor.UpperLeft);
            Box(panel, 12f, 84f, W - 24f, 5f, new Color(0.2f, 0.2f, 0.2f, 1f));
            barFill = Box(panel, 12f, 84f, 1f, 5f, new Color(0.35f, 0.85f, 0.35f, 1f));
            btnYes = new Rect(12f, 96f, 130f, 18f); btnNo = new Rect(W - 142f, 96f, 130f, 18f);
            Box(panel, btnYes.x, btnYes.y, btnYes.width, btnYes.height, new Color(0.25f, 0.45f, 0.25f, 1f));
            Box(panel, btnNo.x, btnNo.y, btnNo.width, btnNo.height, new Color(0.35f, 0.25f, 0.25f, 1f));
            yesLabel = Label(panel, "Download", btnYes.x, btnYes.y + 4f, btnYes.width, 12f, 8, TextAnchor.UpperCenter);
            noLabel = Label(panel, "Not now", btnNo.x, btnNo.y + 4f, btnNo.width, 12f, 8, TextAnchor.UpperCenter);
            body.text = "Minecraft was not found on this computer. The mod can download the Minecraft resources (textures and sounds, about 45 MB) " +
                        "from Mojang's own servers. Nothing from Minecraft is bundled with the mod, and it is not affiliated with Mojang or Microsoft.";
        }

        private void Open()
        {
            if (canvasGo == null) Build();
            IsOpen = true; shown = true;
            savedLock = Cursor.lockState; savedVisible = Cursor.visible;
            canvasGo.SetActive(true);
        }

        private void Close()
        {
            IsOpen = false; shown = false; dismissed = true;
            Cursor.lockState = savedLock; Cursor.visible = savedVisible;
            if (canvasGo != null) canvasGo.SetActive(false);
        }

        private void Update()
        {
            if (!checkedOnce)
            {
                if (startTime == 0f) startTime = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - startTime < 8f) return;   // let the game reach its menu first
                checkedOnce = true;
                if (McResources.NeedsDownload) Open();
            }
            if (Input.GetKeyDown(KeyCode.F10) && !shown && !McResources.Running && McResources.NeedsDownload) Open();
            if (!shown) return;

            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            float gs = Mathf.Max(2f, Mathf.Round(Screen.height / 360f));
            scaler.scaleFactor = gs;
            Vector2 mouse = Input.mousePosition;
            float left = Screen.width / 2f - W / 2f * gs, top = Screen.height / 2f + H / 2f * gs;
            float gx = (mouse.x - left) / gs, gy = (top - mouse.y) / gs;
            bool down = Input.GetMouseButtonDown(0);

            if (McResources.Running || McResources.Done || McResources.Failed)
            {
                body.text = McResources.Status;
                barFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(1f, (W - 24f) * McResources.Progress), 5f);
                yesLabel.text = McResources.Failed ? "Retry" : "";
                noLabel.text = McResources.Running ? "" : "Close";
                if (!McResources.Running && down)
                {
                    if (McResources.Failed && btnYes.Contains(new Vector2(gx, gy))) McResources.Start();
                    else if (btnNo.Contains(new Vector2(gx, gy))) Close();
                }
                return;
            }
            if (down)
            {
                if (btnYes.Contains(new Vector2(gx, gy))) McResources.Start();
                else if (btnNo.Contains(new Vector2(gx, gy))) Close();
            }
        }
    }
}
