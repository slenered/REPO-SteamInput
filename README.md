#### *REPO with **Steam Input support** doesn't exist. It can't hurt you...*
# REPO now with Native Steam Input!*
#### *oh...*
***
Adds support for the Steam Input API to REPO.
Everything that Steam Input can do, now REPO can do such as:
* **Supports just about all controllers that exist.** *I don't think Steam supports my TV remote though...*
* **Motion Control for aiming. Or as movement...** *you psycho.* (Requires compatible controller)
* **Flick stick.** *If you really needed an FPS control scheme in a spoopy robot game.*
* **A niche way to rotate an item AND move the camera to see where your going.** *Wow. desperate much?*
* **Uhh... And a way to have your cake and eat it too?...** *Yeah! I sold it!*

**Installation:**\
As the action and config files are generated after the games starts you will need to restart the game and may need to restart Steam.\
Then apply the relevant layout by selecting the largest button in Steam's controller configuration menu.\
(If you have a Dual Sense Controller or Steam Controller (1st edition) and would like to share your config, I would be happy to include it.)

**Notes:**  
All button toggles should be handled by Steam except for grab of which you can enable in the config (The game disengages grab if you grab nothing.)\
The mod comes with a few default control schemes for: XBox Controllers (no gyro), Nintendo Switch Pro Controllers (1st edition), and The Steam Deck.

If you plan on uninstalling the mod, there are a few files that gets added to the steam root folder during first launch.\
Your Steam installation may be different but the common paths are:
* Window: `C:\Program Files (x86)\Steam\`
* Linux/SteamOS: `~/.steam/steam/` or `~/.local/share/Steam/`\
Created files: 
* `/REPO/TouchMenuIcons/*` The entire folder. (path to REPO usually is `steam/steamapps/common/`)
* `Steam/controller_config/game_actions_3241660.vdf` and `game_actions_3241660.version`.
* `Steam/steamapps/common/Steam Controller Configs/[STEAM USER ID 32bit/Just your friend code]/config/3241660/` This folder holds all controller configs. Even Controller to keyboard maps.\
Default Configs will be named something like `xbox_360.vdf` or `nintendo_switch_pro.vdf` anything else is user generated. (if it has the prefix `controller_` followed by the controller, it's a user's config)\
If you have Steam Cloud enabled (Save backups) Steam will back up your controller configs. You'll have to delete them through the SteamInput menu to be rid of them. 

*this mod is required for Native Steam Input to function. Mouse and Keyboard emulation still works as always. 
***
If you discover any issues, please report them to .
***
### Changelog:
1.0.0 - 1.0.1: Released.
