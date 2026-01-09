# XenopurgeEvolved

Dynamic xeno variants that evolve as you progress, creating unpredictable threats.

** WARNING: This mod will likely make the game significantly harder. **

A MelonLoader mod for Xenopurge.

![Screenshot](screenshots\terrify_marksman.png)

![Screenshot](screenshots\terrify_markman_cutter.png)

![Screenshot](screenshots\voltaic_martyr.png)

Source code is available on [GitHub](https://github.com/bottledwhisky/XenopurgeEvolved).

## Features

- **4 xeno variants per run**: Each xeno type (Scout, Sleeper, Mauler, Spitter) gets one random variant
- **Mid-campaign evolution**: Variants evolve once during missions 8-12
- **16 unique variants** with distinct abilities:
  - **Scout**: Splitting (spawns 2 at 50% HP), Sacrifice (buffs allies on death), Hunter (aggressive AI)
  - **Sleeper**: Stealth (invisible from radar + speed boost), Lifesteal, Suffocation (stun), and Electrified Skin (melee retaliation), Bleed (damage over time)
  - **Mauler**: Heavy Armor (damage reduction), Incubator (spawns Scout), Terrifying Presence (forced taunt)
  - **Spitter**: Agile (kiting), Assault (rush-down), Sharpshooter (accuracy boost)

## Requirements

- [MelonLoader](https://melonloader.co/)

## Installation

Skip to step 2 if you already have MelonLoader installed.

1. Install MelonLoader
    a. `<game_directory>` is the directory where the game executable is located. For example, `C:\Program Files (x86)\Steam\steamapps\common\Xenopurge`. If you still cannot find it, right-click the game in your Steam library, select "Manage", then "Browse local files".
2. Place the mod DLL in `<game_directory>/Mods/`
3. Restart the game

## How It Works

- Each campaign randomly assigns one variant to each of the 4 xeno types
- Variants provide unique abilities that change xeno behavior and tactics
- Evolution occurs automatically upon starting a new run and getting a squad unit.

## Notes

- Mac users: MelonLoader only supports Windows and Linux. Wait for Steam Workshop support.
- Xenopurge uses Mono, not IL2CPP.
