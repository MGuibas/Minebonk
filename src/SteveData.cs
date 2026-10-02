using System;
using UnityEngine;
using Il2CppSystem.Collections.Generic;

namespace MegabonkSteve
{
    internal static class SteveData
    {
        private static bool done;

        public static void Register(DataManager dm)
        {
            if (done) return;
            if (dm.characterData.ContainsKey(Plugin.SteveId)) { done = true; return; }

            var source = dm.GetCharacterData(ECharacter.Fox);
            var steve = UnityEngine.Object.Instantiate(source);
            steve.name = "Steve";
            steve.eCharacter = Plugin.SteveId;
            steve.achievementRequirement = null;
            steve.numQuestsRequiredForVisibilityInCharacterSelection = 0;
            steve.isEnabled = true;
            steve.price = 0;
            steve.sortingPriority = 99;
            steve.icon = SteveTextures.MakeIcon();
            Keep.It(steve);

            dm.unsortedCharacterData.Add(steve);
            dm.characterData[Plugin.SteveId] = steve;

            // Skins: one default entry so skin lookups for the new id never miss.
            var skins = new List<SkinData>();
            foreach (var s in dm.GetSkins(ECharacter.Fox))
            {
                var skin = UnityEngine.Object.Instantiate(s);
                skin.name = "Steve_" + s.name;
                skin.character = Plugin.SteveId;
                skin.unlockRequirement = null;
                Keep.It(skin);
                skins.Add(skin);
                dm.unsortedSkins.Add(skin);
                break;
            }
            dm.skinData[Plugin.SteveId] = skins;

            done = true;
            Plugin.Logger.LogInfo("Registered Steve as ECharacter " + (int)Plugin.SteveId);
        }
    }
}


