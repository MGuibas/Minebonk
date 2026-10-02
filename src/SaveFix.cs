using System;
using Assets.Scripts.Saves___Serialization.Progression;
using Assets.Scripts.Saves___Serialization.Progression.Stats;
using Assets.Scripts._Data.Hats;

namespace MegabonkSteve
{
    // Game save dictionaries are keyed by ECharacter and throw on unknown ids; seed Steve's entries.
    internal static class SaveFix
    {
        public static void Ensure()
        {
            try
            {
                var sm = SaveManager.Instance;
                if (sm == null) return;
                var prefs = sm.config?.preferences;
                if (prefs != null)
                {
                    if (!prefs.characterSkins.ContainsKey(Plugin.SteveId)) prefs.characterSkins[Plugin.SteveId] = 0;
                    if (!prefs.characterHats.ContainsKey(Plugin.SteveId)) prefs.characterHats[Plugin.SteveId] = (EHat)0;
                }
                var prog = sm.progression;
                if (prog != null && !prog.characterProgression.ContainsKey(Plugin.SteveId))
                    prog.characterProgression[Plugin.SteveId] = new CharacterProgression();
                var kills = StatTrackingUtility.keysKillsCharacters;
                if (kills != null && !kills.ContainsKey(Plugin.SteveId))
                    kills[Plugin.SteveId] = "kills_steve";
            }
            catch (Exception e) { Plugin.Logger.LogError("SaveFix: " + e.Message); }
        }
    }
}

