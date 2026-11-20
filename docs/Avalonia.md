# Starward Avalonia / Linux port

This directory contains an in-progress Avalonia 11 port of Starward. The Windows
WinUI 3 project remains the reference implementation and is still buildable from
the original solution.

The Avalonia project reuses `Starward.Core` and `Starward.Language`, and keeps
the existing configuration, database schema and gacha cache format. It is not
yet a complete replacement for every Windows-only feature.

## Requirements

- .NET 10 SDK
- A Linux desktop with X11 or Wayland when running the GUI
- Wine or Proton for launching Windows games on Linux
- A Steam installation when Proton is used

Common Linux runtime packages include `libX11`, `libICE`, `libSM`, `fontconfig`,
and the graphics/audio libraries required by your Wine or Proton build. Exact
package names depend on the distribution.

## Build and run

```bash
dotnet build Starward.Avalonia.slnx
dotnet run --project src/Starward.Avalonia/Starward.Avalonia.csproj
```

Create a framework-dependent Linux build:

```bash
dotnet publish src/Starward.Avalonia/Starward.Avalonia.csproj \
  -c Release -r linux-x64 --self-contained false -o publish/linux-x64
./publish/linux-x64/Starward
```

Use `linux-arm64` for ARM64. Replace `--self-contained false` with `true` for a
self-contained build.

## Data locations

The default data directory on Linux is `$XDG_DATA_HOME/Starward`, falling back
to `~/.local/share/Starward`.

- `config.json`: language and user-data-folder configuration
- `data/StarwardDatabase.db`: settings, gacha records, play-time records
- `log/`: application logs
- `prefixes/`: default Wine/Proton prefixes

Override the root with `STARWARD_DATA_FOLDER`. Create a file named `portable`
next to the executable to enable portable mode.

## Linux compatibility layers

Open **Settings → Linux compatibility**. The page scans the machine and lists
every detected Wine and Proton build. Select the layer used for the current
game; the selection is stored per game and becomes the default for games without
their own selection.

The following locations are scanned automatically:

- Steam libraries from `libraryfolders.vdf`
- Steam `steamapps/common/Proton*` builds
- `compatibilitytools.d` in each Steam library
- system Steam compatibility-tool directories
- native and Flatpak Lutris, Heroic and Bottles Wine runners
- umu compatibility tools
- system Wine found on `PATH` and common `/opt/wine-*` locations
- extra directories configured under **Additional scan paths**

An additional path can point to a `compatibilitytools.d` directory, a Proton
directory containing the `proton` executable, or a Wine runner containing
`bin/wine`.

### Prefixes

**Prefix root** controls the default parent directory. Each game gets a
subdirectory named after its GameBiz (for example `hk4e_global`). Use **Browse**
next to **Prefix folder** to override the prefix for only the current game.

- Wine: `WINEPREFIX` points at the prefix directory.
- Proton: `STEAM_COMPAT_DATA_PATH` points at a Steam-compatible compatdata
  directory. Proton creates its `pfx` directory inside it on first launch.

Proton also receives `STEAM_COMPAT_CLIENT_INSTALL_PATH` from the Steam root
associated with the selected build. If no root is found, the prefix path is used
as a fallback, but a real Steam installation is strongly recommended.

Do not share one mutable prefix between two games unless that is intentional.

### Launch environment

Games are launched directly through the selected layer; Steam itself is not
required. The launcher applies:

- `WINEDEBUG=-all`
- `WINEESYNC=1`
- optional `WINEDLLOVERRIDES=winemenubuilder.exe=d`
- extra command-line arguments configured in Settings
- optional `MANGOHUD=1`
- SDL input mode: `PROTON_PREFER_SDL=1` / `PROTON_USE_SDL=1` for Proton,
  `SDL_JOYSTICK_HIDAPI=1` for Wine

For Proton, the command is `proton run <game.exe> [arguments]`. For Wine it is
`wine <game.exe> [arguments]`.

### Global and per-game settings

The compatibility layer, launch arguments, MangoHud and SDL input each have a
global default. **Per-game settings** can override them for one game, or keep
**Follow global** checked to inherit the global value. Layer scanning merges
duplicate copies of the same build found in Steam, `compatibilitytools.d` and
custom scan paths.

## Validation

```bash
dotnet test tests/Starward.Avalonia.Tests/Starward.Avalonia.Tests.csproj
```

Render a page without opening a desktop window:

```bash
dotnet run --project tests/Starward.Avalonia.Render/Starward.Avalonia.Render.csproj \
  -- /tmp/starward-settings.png Settings:1
```

Supported page arguments are `Launcher`, `Gacha`, `Screenshots`, `PlayTime`,
`Settings`, `Settings:<tab-index>`, and `Launcher:Selector` (opens the game
selector overlay).

## Implemented in the Avalonia port

- Fluent dark UI matching the original, with a 56px icon rail, full-bleed launcher and game selector overlay
- Configuration, portable mode and SQLite schema migrations v1–v21
- Game installation detection, local version reading and update information
- Linux Wine/Proton discovery with duplicate merging, per-game layer/args/MangoHud/SDL settings and prefix launch
- Gacha UID listing, per-banner statistics and rare drops, new/all fetching, JSON import/export
- Automatic gacha-link discovery in the game's `webCaches` (including Wine/Proton prefixes), with manual URL paste as fallback
- Screenshot scanning and preview
- Play-time tracking and recent sessions
- Database backup, logs, language selection and settings pages
- Localized Avalonia UI through the shared `Starward.Language` resources; English and Simplified Chinese are complete, while other locales fall back to English for the new Avalonia strings

## Not yet ported

The following features still depend on Windows APIs or the WinUI host and are
not available in the Avalonia project yet:

- `Starward.RPC` in-place game download, verification and update
- HoYoLAB toolbox, daily notes and account-record pages
- Graphics-settings editing and registry-based account switching
- Gamepad emulation, global hotkeys and overlay
- Screen capture, HDR/AVIF/WebP conversion and screenshot batch tools
- System tray, URL protocol, background service and update/setup flows
- Full gacha banner/detail views and UIGF4 import
This list is deliberate: the Avalonia build is not presented as a byte-for-byte
feature replacement before those components are ported.

## Troubleshooting

**No compatibility layers detected**

Run `wine --version` to verify Wine is visible on `PATH`, or add the runner /
Proton directory under **Additional scan paths** and press **Scan again**.

**Game starts but exits immediately**

Check the log under `Data folder/log/`, then run the same Proton/Wine command in
a terminal with the same `WINEPREFIX` or `STEAM_COMPAT_DATA_PATH`. A fresh
Proton prefix may need its first-time setup completed before the game starts.

**Gacha URL cannot be read from the web cache**

Open the in-game wish/warp history page at least once so the authkey is written
to the game cache, then press **Read cache** or **Update records**. Starward
scans every `webCaches/**/Cache_Data` block of the selected game, including
installs inside a Wine/Proton prefix and Steam libraries. Cache entries expire
quickly, so a fresh visit to the history page may be required. If the launcher
still cannot read it, paste the full history URL manually.

**Proton works in Steam but not here**

Make sure the Steam root belonging to that Proton build is visible to the
launcher and that the selected prefix is a dedicated compatdata directory, not
the `pfx` directory itself.
