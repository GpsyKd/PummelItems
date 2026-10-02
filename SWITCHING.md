# Switching between the modded and the stock game

Local play wants our items. Online play with people who do not have the mod wants the game
exactly as they have it. Both are one click away, and neither involves uninstalling anything.

## Telling which mode you are in

Look at the version label in the corner of the main menu. With the mod loaded, a second, small
line under the game's version reads `PummelItems <version>`; the stock game shows a build
number there instead.

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

Windows has no launch wrapper like that, so the switch is MelonLoader's own off switch:
`UserData/Loader.cfg`, under `[loader]`, has `disable = false`, and `true` makes MelonLoader stop
before it loads anything of ours. The scripts flip that one line - nothing is renamed, moved or
reinstalled, so there is no half-switched state to end up in.

Two files in the game folder, one click each:

| File | What it does |
| --- | --- |
| `Play Modded.bat` | turns the mod on and starts the game |
| `Play Vanilla.bat` | turns it off and starts the stock game |

Put shortcuts to them wherever is convenient - desktop, taskbar, Start menu. Or drive the
switch directly:

```
powershell -File switch-mod.ps1            # which mode am I in?
powershell -File switch-mod.ps1 vanilla    # switch, do not launch
powershell -File switch-mod.ps1 toggle -Launch
```

The scripts find the game themselves from Steam's own list of library folders, whichever drive
it is on.
