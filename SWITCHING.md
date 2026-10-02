# Switching between the modded and the stock game

Local play wants our items. Online play with people who do not have the mod wants the game
exactly as they have it. Both are one click away, and neither involves uninstalling anything.

## Telling which mode you are in

Look at the version label in the corner of the main menu. With the mod loaded, a second line
in gold under the game's version reads `PummelItems <version>`; the stock game shows a tiny
build number there instead.

Behind that label the mod also adds itself to the version the game compares when someone
joins. So the game itself refuses to mix modded and stock players, or two different versions
of the mod, and shows its ordinary "version mismatch" message instead of letting a session
fall apart.

## Steam Deck

Install or update with one command in Konsole (Desktop Mode):

```
curl -sL https://raw.githubusercontent.com/GpsyKd/PummelItems/main/install-deck.sh | bash
```

Then, once, in Steam: Pummel Party → gear icon → Properties → **Launch Options**, and replace
whatever is there with the line the installer prints, which looks like

```
/home/deck/.local/share/pummelitems/launch.sh %command%
```

The installer reads Steam's settings and says whether that is already done.

From then on, every time Pummel Party starts, a menu in the style of Steam's own launch-option
menu asks how:

| Choice | What you get |
| --- | --- |
| **Играть с модом** | our items, for local play |
| **Играть без мода** | the stock game, for playing online |
| **Отмена** | back to the library, nothing started |

Choose with the D-pad or the stick and A (B backs out), or tap it on the screen. Left alone, it
starts the way it did last time when the countdown under the menu runs out. There is still one
Pummel Party in the library.

"Играть без мода" is genuinely stock: the launcher does not set the one setting that lets
Proton load MelonLoader's `version.dll`, so MelonLoader never starts at all.

How it works, for when it does not:

- The menu is `deck/picker.py`: plain Python on libX11, which SteamOS always has. Zenity, which
  an earlier version used, is not on SteamOS, and the copy inside Steam is too old.
- In Game Mode the screen belongs to gamescope, which shows only windows that carry the
  `STEAM_GAME` property. The menu sets it to the game's id, so it is shown as part of the game
  starting.
- Every launch is logged in `~/.local/share/pummelitems/launch.log`: what was chosen, how, and
  why the menu could not be shown if it could not. Without a menu the game still starts, in the
  last mode used, which is kept in `~/.local/share/pummelitems/mode` (`modded` or `vanilla`).
- Steam's real launch-option menu cannot be borrowed: it lists only the options the game's
  developer registered with Steam, and an entry added to Steam's cache by hand is dropped
  whenever Steam refreshes the game's details.

## Windows

Install or update with one command in PowerShell:

```
[Net.ServicePointManager]::SecurityProtocol = 'Tls12'; irm https://raw.githubusercontent.com/GpsyKd/PummelItems/main/install-windows.ps1 | iex
```

Then, once, in Steam: Pummel Party → right click → Properties → **Launch Options**, and paste
the line the installer prints (it also puts it on the clipboard), which looks like

```
"D:\SteamLibrary\steamapps\common\Pummel Party\PummelItemsLauncher.exe" %command%
```

From then on the same menu as on the Deck comes up whenever the game starts: **Играть с
модом**, **Играть без мода**, **Отмена** - in Russian when Windows is, in English otherwise.
Arrows and Enter, the mouse, or a controller's D-pad and A; B or Escape backs out. Left alone,
it starts the way it did last time when the countdown runs out.

- **With the mod**, it makes sure MelonLoader's own off switch (`disable` in
  `UserData/Loader.cfg`) is off - the old `Play Vanilla.bat` used to leave it on - and starts
  the game.
- **Without the mod**, it passes the game MelonLoader's `--no-mods`, and MelonLoader stops
  before loading anything; it does not even write a log. Nothing on disk changes.

The launcher waits for the game to close, so Steam sees the game as running the whole time
and syncs the cloud after it, not before. Its log is `UserData/PummelCustomItems/launcher.log`.

The old `Play Modded.bat` / `Play Vanilla.bat` are gone; the installer removes them.
