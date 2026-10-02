# Switching between the modded and the stock game

Local play wants our items. Online play with people who do not have the mod wants the game
exactly as they have it. Both are one click away, and neither involves uninstalling anything.

## Telling which mode you are in

Look at the version number in the corner of the main menu. With the mod loaded it ends in
`+PummelItems.<version>`; the stock game shows the plain number.

That label is not decoration: it is the version the game compares when someone joins. So the
game itself refuses to mix modded and stock players, or two different versions of the mod, and
shows its ordinary "version mismatch" message instead of letting a session fall apart.

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

From then on Pummel Party asks every time it starts:

| Button | What you get |
| --- | --- |
| **С модом** | our items, for local play |
| **Без мода** | the stock game, for playing online |

Tap one on the screen, or leave it, and after ten seconds it starts the way it did last time.
There is still one Pummel Party in the library.

In Game Mode the screen belongs to gamescope, which shows only windows marked as part of the
game being played - an ordinary dialog runs there unseen. The launcher marks its question with
the game's id (the `STEAM_GAME` window property), so it appears over the game's start-up.
Every launch is logged in `~/.local/share/pummelitems/launch.log`: which mode was chosen, how,
and whether the question could be shown.

Steam's own "choose a launch option" menu is not used on purpose: it lists only the launch
options the game's developer registered with Steam, and an extra one added to Steam's cache by
hand is silently dropped whenever Steam refreshes the game's details.

"Без мода" is genuinely stock: the launcher does not set the one setting that lets Proton load
MelonLoader's `version.dll`, so MelonLoader never starts at all.

If the dialog ever fails to appear, the game still starts, in the last mode used. To change it
by hand, edit `~/.local/share/pummelitems/mode` (`modded` or `vanilla`).

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
