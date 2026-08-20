# Switching between the modded and the stock game

Local play wants our items. Online play wants the game exactly as the other players have it,
because our items exist only on this machine - the ones joining would be missing every prefab
and every item id we add.

## How the switch works

MelonLoader can turn itself off. `UserData/Loader.cfg` has, under `[loader]`:

```
disable = false
```

`true` there is the same as passing `--no-mods`: MelonLoader stops before it loads anything of
ours, and the game is stock. The switch scripts flip that one line and nothing else.

Nothing is renamed, moved or reinstalled, so there is no half-switched state to end up in and
no way to lose the mod by switching at an awkward moment.

## Windows

Two files in this folder, one click each:

| File | What it does |
| --- | --- |
| `Play Modded.bat` | items on, then launches the game |
| `Play Vanilla.bat` | stock game, then launches the game |

Or drive it directly:

```
powershell -File switch-mod.ps1            # which mode am I in?
powershell -File switch-mod.ps1 vanilla    # switch, do not launch
powershell -File switch-mod.ps1 toggle -Launch
```

Put shortcuts to the two `.bat` files wherever is convenient - desktop, taskbar, Start menu.

## Steam Deck

`switch-mod.sh` is the same thing for the Deck. It finds the game itself, including on an SD
card; override with `PUMMEL_DIR=... ./switch-mod.sh` if it cannot.

```
./switch-mod.sh              # which mode am I in?
./switch-mod.sh vanilla
./switch-mod.sh modded --launch
```

Leave the launch options alone once set:

```
WINEDLLOVERRIDES="version=n,b" %command%
```

That only *allows* MelonLoader to load - `Loader.cfg` decides whether it does. Keeping the
override permanent means the Deck and the PC are switched the same way, with one habit rather
than two.

To reach the switch from Game Mode without a keyboard, add `switch-mod.sh` to Steam as a
non-Steam game twice, once with `modded --launch` and once with `vanilla --launch` as its
arguments. Both then sit on the Deck's library alongside the game itself.

## Telling which mode you are in

When the mod is on, MelonLoader opens its console window alongside the game and the log
records `=== PummelCustomItems loaded ===`. In stock mode there is no console at all, which
is the quickest check before joining anyone.

`switch-mod.ps1` and `switch-mod.sh` with no argument also just print the current mode.
