using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace MegabonkSteve
{
    [BepInPlugin("com.guibas.minebonk", "Minebonk", "0.1.7")]
    public class Plugin : BasePlugin
    {
        public static ManualLogSource Logger;
        // Verbose diagnostics (boss telemetry, material names, movement...) are only written when this file exists in the config folder.
        public static bool DebugOn;
        public static void Dbg(string s) { if (DebugOn) Logger.LogInfo(s); }
        // ECharacter ends at Roberto (20); Steve takes the next free slot.
        public const ECharacter SteveId = (ECharacter)21;

        public override void Load()
        {
            Logger = Log;
            DebugOn = System.IO.File.Exists(System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "MegabonkSteve_debug.txt"));
            ClassInjector.RegisterTypeInIl2Cpp<SteveRig>();
            ClassInjector.RegisterTypeInIl2Cpp<MinecraftMode>();
            ClassInjector.RegisterTypeInIl2Cpp<MobRig>();
            ClassInjector.RegisterTypeInIl2Cpp<McChest>();
            ClassInjector.RegisterTypeInIl2Cpp<SkinPicker>();
            ClassInjector.RegisterTypeInIl2Cpp<ResourcePrompt>();
            new Harmony("com.guibas.minebonk").PatchAll();
            var pickerGo = new GameObject("MC_SkinPicker");
            UnityEngine.Object.DontDestroyOnLoad(pickerGo);
            pickerGo.hideFlags = HideFlags.HideAndDontSave;
            pickerGo.AddComponent<SkinPicker>();
            pickerGo.AddComponent<ResourcePrompt>();
            Logger.LogInfo("Minebonk loaded");
        }

        public static bool IsSteve(CharacterData d)
        {
            return d != null && d.eCharacter == SteveId;
        }
    }
}


