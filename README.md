# Minebonk — Minecraft gameplay for Megabonk

**Minebonk** is a mod that adds **Steve** as a playable character in Megabonk. The world and the enemies stay Megabonk; the gameplay becomes Minecraft's: first-person view, hotbar and inventory, the 1.21 combat rules, Minecraft mobs and bosses, chests with Minecraft loot, and weapons that are Minecraft items.

> **Not affiliated with Mojang, Microsoft or the makers of Megabonk.** This is an unofficial fan mod. It contains no Minecraft files: textures and sounds are read from your own Minecraft install, or downloaded once from Mojang's servers if you accept the prompt (see below).

## A native mod, not two games running together

Some recent projects (for example [universal-modder](https://github.com/rehan-remade/universal-modder)'s Minecraft-in-GTA V demo) mix two games by running **both at the same time** and streaming camera, ground and events between them over a local connection. Minebonk does not do that. There is **only one process: Megabonk**. The Minecraft look and rules are rebuilt inside Megabonk itself (BepInEx + Harmony plugin): the Steve model, blocks, items, mobs, combat, HUD and sounds are all drawn and simulated by the mod in Megabonk's own engine. No Minecraft instance is launched, nothing runs in the background, and there is no extra latency or double memory use.

## Install

1. Install **BepInEx 6 IL2CPP, build be.755** (the `BepInExPack_IL2CPP` pack on Thunderstore, or the build from builds.bepinex.dev) into your Megabonk folder and run the game once, so BepInEx creates its folders. (Tested with the Steam version of Megabonk.)
2. Copy the `BepInEx` folder from this package into the Megabonk folder (it adds `BepInEx/plugins/Minebonk/Minebonk.dll` and `NVorbis.dll`).
3. Start the game and pick **Steve** in the character screen.

### Minecraft resources
- If **Minecraft Java Edition** is installed (launcher folder `%APPDATA%\.minecraft`), the mod uses it automatically. Tested with 26.2.
- If it is not found, a prompt appears in the menu offering to download the resources (about 41 MB: the Minecraft client and the sounds the mod uses) from Mojang's official servers into `BepInEx/config/MegabonkSteve_resources`. Press **F10** to open the prompt again. Restart the game after it finishes.

## How you play

- **You start with your bare hands.** Everything else is found in chests: swords and axes from wood to netherite, armor from chainmail to netherite, food, arrows, a bow, shield, elytra, totem, pearls, potions. Better gear is rarer, and the difference in damage and protection is large.
- **Minecraft combat:** attack cooldown and charge, critical hits when falling, shield blocking (it breaks after a number of hits and recovers), mace smash damage that grows with the height you fell from, bow draw, ender pearls, golden apples, totem of undying, elytra with rockets.
- **Weapons from level-ups** are Minecraft items (Fire Charge, Trident of Channeling, TNT, Skeleton Bow, Piercing Crossbow, Mace, Sonic Boom, Firework Rocket, Splash Potion, Anvil, Sweeping Edge). Each fires when *you* do something (hit, crit, kill, block, land from a fall, eat, shoot an arrow...). At **level 5** a weapon *awakens*: it keeps answering its trigger and also fires on its own. Sweeping Edge never becomes automatic.
- **Enchanted books** appear as level-up cards: Sharpness, Power, Efficiency, Multishot, Protection, Knockback, Mending and Luck, up to level V.
- **Chests:** Minecraft chests cost 10, 20, 37, 50 and so on. Free end chests replace the shady shop and the Moai, and vanish after you open them. Chests dropped by bosses give two Megabonk passive items.
- **Passive — Treasure Hunter:** chests hold two extra items and critical hits drop double gold.
- **Mobs:** Megabonk's enemies are Minecraft mobs (zombies, skeletons, creepers, spiders, slimes, pillagers, pigs, cows, bees, iron golems). Bosses: Wither, Elder Guardian, Warden, Ender Dragon.

## Controls

| Key | Action |
|---|---|
| Right click | Use item / block with shield / open chests and interact (the E key is disabled) |
| Left click | Attack (with anything; bare hands do 1 damage) |
| E | Inventory |
| F | Swap main hand and offhand |
| 1–9, wheel | Hotbar |
| Ctrl / double W | Sprint |
| Shift | Slide / crouch |
| F5 | First / third person |
| F8 | Test panel (in game): spawn mobs and bosses, heal, kill all, full kit, sound volume |
| F9 | Skin picker (character screen and in game) |
| F10 | Download prompt for the Minecraft resources |

### Skins
F9 lists the skins saved in the Minecraft launcher, the nine default skins, and any 64x64 `.png` you put in `BepInEx/config/MegabonkSteve_skins` (end the file name with `_slim` for slim arms). The choice is remembered.

## Troubleshooting
- The log is `BepInEx/LogOutput.log`. For more detail, create an empty file `BepInEx/config/MegabonkSteve_debug.txt` (this also enables the cheat keys L and K for levels).
- No sound or flat textures: the Minecraft resources were not found; press F10 or install Minecraft.
- Game update broke something: this mod patches Megabonk's code. Updates can change it.

## Credits and licenses
- Mod code: MIT, see `LICENSE.txt`.
- [NVorbis](https://github.com/NVorbis/NVorbis) (MIT) decodes Ogg Vorbis sounds, see `THIRD_PARTY_NOTICE_NVorbis.txt`.
- Built with BepInEx and Harmony.
- Minecraft is a trademark of Mojang AB. Megabonk belongs to its developer. Neither is included here.

---

# Español (resumen)

**Minebonk** añade a **Steve** como personaje de Megabonk con el juego de Minecraft: primera persona, hotbar, inventario, combate de la 1.21, mobs y jefes de Minecraft, cofres con botín de Minecraft y armas que son objetos de Minecraft.

**Instalación:** instala BepInEx 6 IL2CPP en la carpeta de Megabonk, abre el juego una vez, copia la carpeta `BepInEx` de este paquete encima y elige a Steve. Necesita los recursos de Minecraft: si tienes Minecraft Java los usa solos; si no, ofrece descargarlos (unos 41 MB) desde los servidores de Mojang la primera vez (F10 para volver a abrir el aviso).

**Teclas:** clic derecho usar/interactuar (la E está desactivada), E inventario, F cambiar de mano, F5 cámara, F8 panel de pruebas, F9 skins, F10 descarga de recursos.

Mod no oficial: no está afiliado con Mojang, Microsoft ni con los creadores de Megabonk, y no incluye ningún archivo de Minecraft.



---

## Building from source

Requirements: .NET SDK 6 or newer, and a Megabonk folder with BepInEx 6 IL2CPP (be.755) installed and run once (so `BepInEx/interop` exists).

```
cd src
dotnet build -c Release -p:GamePath="C:\Path\To\Megabonk"
```

The result is `src/bin/Release/net6.0/Minebonk.dll` (plus `NVorbis.dll`). Copy both to `BepInEx/plugins/Minebonk/`.

The project references the game's own generated interop assemblies from your install; they are not included in this repository.
