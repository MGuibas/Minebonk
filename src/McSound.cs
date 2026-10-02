using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using UnityEngine;

namespace MegabonkSteve
{
    // Plays the real Minecraft sounds from the user's own install. The game build strips Unity's runtime audio-clip
    // creation, so audio goes straight to Windows: .ogg files are decoded with NVorbis and mixed by a small software
    // mixer that feeds a winmm waveOut device. Events come from sounds.json, files from the asset store.
    internal static class McSound
    {
        private class Entry { public string name; public float volume = 1f, pitch = 1f; public int weight = 1; public bool isEvent; }

        private static readonly Dictionary<string, List<Entry>> events = new Dictionary<string, List<Entry>>();
        private static readonly Dictionary<string, string> files = new Dictionary<string, string>();   // "random/explode1" -> full path
        private static readonly Dictionary<string, float[]> clips = new Dictionary<string, float[]>(); // interleaved stereo, 44.1 kHz
        private static readonly HashSet<string> loading = new HashSet<string>();
        private static readonly Dictionary<string, List<PendingPlay>> waiting = new Dictionary<string, List<PendingPlay>>();
        private static readonly object gate = new object();
        public static float Master = 0.8f;
        public static volatile bool Paused;   // game paused or a menu is open: no Minecraft sounds
        private static bool ready;
        private static int played, decodedCount;

        private class PendingPlay { public float gl, gr, pitch; public float until; public bool loop; }
        private class Voice { public float[] data; public double pos; public double step = 1; public float gl, gr; public bool loop, done; public string loopId; }

        private const int Rate = 44100;
        private static readonly List<Voice> voices = new List<Voice>();

        // ------------------------------------------------------------------ setup

        public static void Init()
        {
            if (ready) return;
            try
            {
                string mc = McResources.Root();
                if (mc == null) { Plugin.Logger.LogWarning("No Minecraft resources found: no sounds"); return; }
                // the newest asset index in that folder
                string indexId = "32"; int best = -1;
                foreach (var f in Directory.GetFiles(Path.Combine(mc, "assets", "indexes"), "*.json"))
                {
                    int n; string nm = Path.GetFileNameWithoutExtension(f);
                    if (int.TryParse(nm, out n) && n > best) { best = n; indexId = nm; }
                }
                string indexPath = Path.Combine(mc, "assets", "indexes", indexId + ".json");
                if (!File.Exists(indexPath)) { Plugin.Logger.LogWarning("No asset index " + indexPath); return; }
                string objects = Path.Combine(mc, "assets", "objects");

                using (var idx = JsonDocument.Parse(File.ReadAllText(indexPath)))
                {
                    foreach (var o in idx.RootElement.GetProperty("objects").EnumerateObject())
                    {
                        string key = o.Name;
                        string hash = o.Value.GetProperty("hash").GetString();
                        string full = Path.Combine(objects, hash.Substring(0, 2), hash);
                        if (key == "minecraft/sounds.json") LoadEvents(full);
                        else if (key.StartsWith("minecraft/sounds/") && key.EndsWith(".ogg"))
                            files[key.Substring("minecraft/sounds/".Length, key.Length - "minecraft/sounds/".Length - 4)] = full;
                    }
                }

                if (!StartMixer()) return;
                ready = true;
                Plugin.Logger.LogInfo("Minecraft sounds ready: " + events.Count + " events, " + files.Count + " files");
                Preload();
            }
            catch (Exception e) { Plugin.Logger.LogError("McSound.Init: " + e); }
        }

        private static void LoadEvents(string path)
        {
            using (var d = JsonDocument.Parse(File.ReadAllText(path)))
            {
                foreach (var ev in d.RootElement.EnumerateObject())
                {
                    if (!ev.Value.TryGetProperty("sounds", out var arr)) continue;
                    var list = new List<Entry>();
                    foreach (var s in arr.EnumerateArray())
                    {
                        var e = new Entry();
                        if (s.ValueKind == JsonValueKind.String) e.name = s.GetString();
                        else
                        {
                            e.name = s.GetProperty("name").GetString();
                            if (s.TryGetProperty("volume", out var v)) e.volume = v.GetSingle();
                            if (s.TryGetProperty("pitch", out var p)) e.pitch = p.GetSingle();
                            if (s.TryGetProperty("weight", out var w)) e.weight = Math.Max(1, w.GetInt32());
                            if (s.TryGetProperty("type", out var t) && t.GetString() == "event") e.isEvent = true;
                        }
                        list.Add(e);
                    }
                    events[ev.Name] = list;
                }
            }
        }

        private static readonly string[] preload =
        {
            "entity.player.attack.strong", "entity.player.attack.weak", "entity.player.attack.crit", "entity.player.attack.sweep",
            "entity.player.attack.knockback", "entity.arrow.shoot", "entity.arrow.hit", "entity.arrow.hit_player", "entity.skeleton.shoot",
            "entity.ender_pearl.throw", "entity.player.teleport", "entity.generic.eat", "entity.generic.drink", "entity.player.burp",
            "item.shield.block", "item.totem.use", "item.elytra.flying", "item.armor.equip_netherite",
            "item.armor.equip_diamond", "item.armor.equip_elytra", "entity.firework_rocket.launch", "entity.player.levelup",
            "entity.experience_orb.pickup", "entity.player.hurt", "entity.player.death", "entity.tnt.primed", "entity.generic.explode",
            "entity.lightning_bolt.thunder", "entity.lightning_bolt.impact", "entity.blaze.shoot", "item.mace.smash_ground",
            "item.mace.smash_air", "block.grass.step", "entity.generic.small_fall", "entity.generic.big_fall"
        };

        private static void Preload()
        {
            foreach (var ev in preload)
            {
                List<Entry> list;
                if (!events.TryGetValue(ev, out list)) continue;
                foreach (var e in list) if (!e.isEvent) RequestClip(e.name);
            }
        }

        // ------------------------------------------------------------------ decoding

        private static void RequestClip(string name)
        {
            string path;
            lock (gate)
            {
                if (clips.ContainsKey(name) || loading.Contains(name) || !files.TryGetValue(name, out path)) return;
                loading.Add(name);
            }
            ThreadPool.QueueUserWorkItem(_ =>
            {
                float[] stereo = null;
                try
                {
                    using (var vr = new NVorbis.VorbisReader(path))
                    {
                        int ch = vr.Channels, rate = vr.SampleRate;
                        long total = vr.TotalSamples * ch;
                        var all = new float[total];
                        var buf = new float[4096 * ch];
                        long pos = 0; int n;
                        while (pos < total && (n = vr.ReadSamples(buf, 0, buf.Length)) > 0)
                        {
                            int copy = (int)Math.Min(n, total - pos);
                            Array.Copy(buf, 0, all, pos, copy);
                            pos += copy;
                        }
                        stereo = ToStereo44k(all, (int)(pos / ch), ch, rate);
                    }
                }
                catch (Exception e) { Plugin.Logger.LogWarning("decode " + name + ": " + e.Message); }

                List<PendingPlay> pend = null;
                lock (gate)
                {
                    loading.Remove(name);
                    if (stereo != null) { clips[name] = stereo; decodedCount++; }
                    if (waiting.TryGetValue(name, out pend)) waiting.Remove(name);
                }
                if (stereo != null && pend != null)
                    foreach (var p in pend) if (Environment.TickCount / 1000f < p.until || true) StartVoice(stereo, p.gl, p.gr, p.pitch, p.loop, name);
            });
        }

        // Resamples to 44.1 kHz and expands mono to stereo (linear interpolation).
        private static float[] ToStereo44k(float[] src, int frames, int ch, int rate)
        {
            int outFrames = (int)((long)frames * Rate / rate);
            var o = new float[outFrames * 2];
            double ratio = (double)rate / Rate;
            for (int i = 0; i < outFrames; i++)
            {
                double sp = i * ratio;
                int i0 = (int)sp; double fr = sp - i0;
                int i1 = Math.Min(i0 + 1, frames - 1);
                if (i0 >= frames) break;
                float l0 = src[i0 * ch], l1 = src[i1 * ch];
                float r0 = ch > 1 ? src[i0 * ch + 1] : l0, r1 = ch > 1 ? src[i1 * ch + 1] : l1;
                o[i * 2] = (float)(l0 + (l1 - l0) * fr);
                o[i * 2 + 1] = (float)(r0 + (r1 - r0) * fr);
            }
            return o;
        }

        // ------------------------------------------------------------------ public API

        // Kept for the main-thread loop; decoding and mixing run on their own threads, so nothing to do per frame.
        public static void Tick() { }

        public static string Stats() { lock (gate) { return "sound clips=" + clips.Count + " decoded=" + decodedCount + " voices=" + voices.Count + " played=" + played; } }

        private static Entry Pick(string ev, int depth)
        {
            List<Entry> list;
            if (depth > 3 || !events.TryGetValue(ev, out list) || list.Count == 0) return null;
            int total = 0; foreach (var e in list) total += e.weight;
            int r = UnityEngine.Random.Range(0, total);
            foreach (var e in list)
            {
                r -= e.weight;
                if (r < 0) return e.isEvent ? Pick(e.name, depth + 1) : e;
            }
            return list[0];
        }

        public static void Play(string ev, float vol = 1f, float pitch = 1f) { Go(ev, vol, pitch, false, Vector3.zero, false); }
        public static void PlayAt(string ev, Vector3 pos, float vol = 1f, float pitch = 1f) { Go(ev, vol, pitch, true, pos, false); }

        // Volume and left/right balance for a source in the world, relative to the camera.
        private static void Spatial(Vector3 pos, out float gain, out float pan)
        {
            gain = 1f; pan = 0f;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 d = pos - cam.transform.position;
            float dist = d.magnitude;
            gain = Mathf.Clamp01(1f - Mathf.Max(0f, dist - 15f) / 245f);
            if (dist > 0.01f) pan = Mathf.Clamp(Vector3.Dot(d / dist, cam.transform.right), -1f, 1f) * 0.7f;
        }

        private static void Go(string ev, float vol, float pitch, bool has3d, Vector3 pos, bool loop)
        {
            if (!ready || Paused) return;
            var e = Pick(ev, 0);
            if (e == null) return;
            float v = vol * e.volume * Master;
            float g = 1f, pan = 0f;
            if (has3d) Spatial(pos, out g, out pan);
            v *= g;
            if (v <= 0.002f) return;
            float gl = v * Mathf.Sqrt((1f - pan) * 0.5f) * 1.2f, gr = v * Mathf.Sqrt((1f + pan) * 0.5f) * 1.2f;
            float p = pitch * e.pitch;
            float[] data;
            lock (gate)
            {
                if (!clips.TryGetValue(e.name, out data))
                {
                    List<PendingPlay> list;
                    if (!waiting.TryGetValue(e.name, out list)) waiting[e.name] = list = new List<PendingPlay>();
                    if (list.Count < 4) list.Add(new PendingPlay { gl = gl, gr = gr, pitch = p, loop = loop, until = 0 });
                }
            }
            if (data != null) StartVoice(data, gl, gr, p, loop, null);
            else RequestClip(e.name);
        }

        private static void StartVoice(float[] data, float gl, float gr, float pitch, bool loop, string loopId)
        {
            lock (gate)
            {
                if (voices.Count > 48) voices.RemoveAt(0);
                voices.Add(new Voice { data = data, step = Mathf.Clamp(pitch, 0.4f, 2.5f), gl = gl, gr = gr, loop = loop, loopId = loopId });
                played++;
            }
        }

        // Looping sound (elytra flight): call every frame with the desired state.
        private static Voice loopVoice;
        private static string loopEvent;
        public static void Loop(string ev, bool on, float vol = 1f)
        {
            if (!ready) return;
            if (Paused) on = false;
            lock (gate)
            {
                if (!on)
                {
                    if (loopVoice != null) { loopVoice.done = true; loopVoice = null; loopEvent = null; }
                    return;
                }
                if (loopVoice != null && !loopVoice.done && loopEvent == ev) return;
            }
            var e = Pick(ev, 0);
            if (e == null) return;
            float[] data;
            lock (gate) { clips.TryGetValue(e.name, out data); }
            if (data == null) { RequestClip(e.name); return; }
            float g = vol * e.volume * Master * 1.2f;
            var v = new Voice { data = data, step = 1, gl = g, gr = g, loop = true };
            lock (gate) { voices.Add(v); loopVoice = v; loopEvent = ev; played++; }
        }

        // ------------------------------------------------------------------ winmm mixer

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEFORMATEX
        {
            public ushort wFormatTag, nChannels;
            public uint nSamplesPerSec, nAvgBytesPerSec;
            public ushort nBlockAlign, wBitsPerSample, cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEHDR
        {
            public IntPtr lpData;
            public uint dwBufferLength, dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags, dwLoops;
            public IntPtr lpNext, reserved;
        }

        [DllImport("winmm.dll")] private static extern int waveOutOpen(out IntPtr hWaveOut, int uDeviceID, ref WAVEFORMATEX fmt, IntPtr cb, IntPtr inst, int flags);
        [DllImport("winmm.dll")] private static extern int waveOutPrepareHeader(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] private static extern int waveOutWrite(IntPtr h, IntPtr hdr, int size);

        private const int WHDR_DONE = 1;
        private const int Frames = 1024;             // ~23 ms per buffer
        private const int BufCount = 4;
        private static IntPtr hWave;
        private static readonly IntPtr[] hdrs = new IntPtr[BufCount];
        private static readonly IntPtr[] bufs = new IntPtr[BufCount];

        private static bool StartMixer()
        {
            var fmt = new WAVEFORMATEX
            {
                wFormatTag = 1, nChannels = 2, nSamplesPerSec = Rate, wBitsPerSample = 16,
                nBlockAlign = 4, nAvgBytesPerSec = Rate * 4, cbSize = 0
            };
            int r = waveOutOpen(out hWave, -1, ref fmt, IntPtr.Zero, IntPtr.Zero, 0);
            if (r != 0) { Plugin.Logger.LogError("waveOutOpen failed: " + r); return false; }
            int hs = Marshal.SizeOf(typeof(WAVEHDR));
            for (int i = 0; i < BufCount; i++)
            {
                bufs[i] = Marshal.AllocHGlobal(Frames * 4);
                hdrs[i] = Marshal.AllocHGlobal(hs);
                var h = new WAVEHDR { lpData = bufs[i], dwBufferLength = (uint)(Frames * 4), dwFlags = 0 };
                Marshal.StructureToPtr(h, hdrs[i], false);
                waveOutPrepareHeader(hWave, hdrs[i], hs);
                // prime with silence so the device starts running
                FillBuffer(i);
                waveOutWrite(hWave, hdrs[i], hs);
            }
            var t = new Thread(MixLoop) { IsBackground = true, Name = "MC audio mixer" };
            t.Start();
            return true;
        }

        private static void MixLoop()
        {
            int hs = Marshal.SizeOf(typeof(WAVEHDR));
            while (true)
            {
                bool any = false;
                for (int i = 0; i < BufCount; i++)
                {
                    int flags = Marshal.ReadInt32(hdrs[i], IntPtr.Size + 8 + IntPtr.Size);   // dwFlags
                    if ((flags & WHDR_DONE) == 0) continue;
                    FillBuffer(i);
                    waveOutWrite(hWave, hdrs[i], hs);
                    any = true;
                }
                if (!any) Thread.Sleep(4);
            }
        }

        private static readonly float[] mix = new float[Frames * 2];

        private static void FillBuffer(int index)
        {
            Array.Clear(mix, 0, mix.Length);
            if (Paused)
            {
                // silence, and the voices freeze where they are
                Marshal.Copy(new short[Frames * 2], 0, bufs[index], Frames * 2);
                return;
            }
            lock (gate)
            {
                for (int vi = voices.Count - 1; vi >= 0; vi--)
                {
                    var v = voices[vi];
                    if (v.done) { voices.RemoveAt(vi); continue; }
                    var d = v.data;
                    int frames = d.Length / 2;
                    for (int f = 0; f < Frames; f++)
                    {
                        int i0 = (int)v.pos;
                        if (i0 >= frames - 1)
                        {
                            if (v.loop) { v.pos = 0; i0 = 0; }
                            else { v.done = true; break; }
                        }
                        float fr = (float)(v.pos - i0);
                        int a = i0 * 2, b = a + 2;
                        float l = d[a] + (d[b] - d[a]) * fr;
                        float r = d[a + 1] + (d[b + 1] - d[a + 1]) * fr;
                        mix[f * 2] += l * v.gl;
                        mix[f * 2 + 1] += r * v.gr;
                        v.pos += v.step;
                    }
                }
            }
            var o = new short[Frames * 2];
            for (int i = 0; i < o.Length; i++)
            {
                float s = mix[i];
                // soft clip so several loud sounds together don't distort
                s = s > 1f ? 1f : (s < -1f ? -1f : s);
                o[i] = (short)(s * 32000f);
            }
            Marshal.Copy(o, 0, bufs[index], o.Length);
        }
    }
}

