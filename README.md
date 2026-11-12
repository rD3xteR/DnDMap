# DnD Map Project

A small WPF desktop app that can be used to show location maps for Dungeons & Dragons on a screen. It also lets you add tokens for creatures and characters and move them around during the game.

Currently, it's in a stage of `lazy development` where the main functionality is implemented and some features are just stashed.

> The app UI itself is in Russian.

## Features

- Choose a folder with map images and open a map in a borderless fullscreen window. Only one map window exists at a time: opening another map replaces the current one in the same window (existing tokens are kept).
- Choose a folder with token images (`.png`, `.jpg`, `.jpeg`) and add tokens to the map.
- Drag tokens with the mouse, select them (highlighted), remove with `Del`.
- Token diameter follows the D&D size categories: `Normal` / `Big` / `Huge` → ×1 / ×2 / ×3 of the base diameter.
- Adjustable base diameter (slider, 20–200 px, default 100 px), applied to all tokens at once.
- Pan the map by dragging, zoom with `Ctrl` + mouse wheel or the vertical slider (1×–3×), reset the view with `Reset`. The map always covers the screen, so it cannot be dragged out of view; at 1× it is fully visible and centred.
- **Tablet mode:** an Android tablet runs the HTTP host and shows the map fullscreen; the DM opens `http://<tablet-ip>:5000/` in a laptop browser and controls maps, tokens, zoom and token size from there.
- The last used map/token folders are remembered between sessions.

## Requirements

- Windows (WPF app, target framework `net10.0-windows7.0`).
- .NET SDK 10 or newer (`global.json` requires SDK ≥ 8.0 with `rollForward: latestMajor`).
- Check your SDKs with `dotnet --list-sdks`.

## Build and run

```powershell
dotnet build DnDMap.sln -c Debug      # or -c Release
dotnet run --project DnDMap/DnDMap.csproj
```

> [!NOTE]
> The build output directory is set by `BaseOutputPath` in `DnDMap/DnDMap.csproj` to `..\..\..\DndMap\<Configuration>` **relative to the project file**, i.e. three levels above `DnDMap/DnDMap/`. On a typical checkout at `C:\Users\<user>\source\repos\DnDMap` that means `C:\Users\<user>\source\DndMap\Debug\` — outside the repository.

## Usage

1. Launch the app — the control window **«Управление»** opens.
2. **Выбор карты** → **Обзор...** → pick a folder with map images → select a map → **Открыть карту**. Selecting another map later reuses the already-open map window (tokens are preserved).
3. **Параметры круга** → set the base diameter and the creature size for the next token.
4. **Галерея изображений** → **Обзор...** → pick a folder with token images → select one → **Добавить токен**.
5. Interact with the map window (it opens maximized, without a title bar — close it with `Alt+F4`):

| Action | Result |
| --- | --- |
| Drag empty space | Pan the map (clamped: the map always covers the screen) |
| Drag a token | Move the token |
| Click a token | Select it (red highlight) |
| `Del` | Delete the selected token |
| `Ctrl` + mouse wheel | Zoom in/out at the cursor |
| Vertical slider / `Reset` | Zoom / reset zoom and position |

## Project structure

```
DnDMap.sln
├── DnDMap/                        # WPF host (desktop)
│   ├── App.xaml(.cs)              # entry point; startup window = SettingsWindow
│   ├── SettingsWindow/            # «Управление» window + HTTP-server controls
│   ├── MapWindow/                 # fullscreen map window + MapViewModel (partial)
│   ├── TokenModel/                # Token model + its behavior (partial classes)
│   ├── Models/                    # ImageItem, EntitySizeItem
│   ├── Core/                      # UI infrastructure (ViewModelBase, RelayCommand, …)
│   ├── Paths.settings / App.config# persisted map & token folders (user settings)
│   └── src/logo.ico               # application icon
├── src/DnDMap.Core/               # geometry, session model, storage, protocol (BCL only)
├── src/DnDMap.Server/             # HTTP host: REST + SSE + embedded SPA (wwwroot/)
├── src/DnDMap.Core.Tests/         # xUnit tests for the core
├── src/DnDMap.Server.Tests/       # xUnit tests against a live HTTP server
├── src/DnDMap.Android/            # Android tablet host (net10.0-android, no MAUI)
├── build-installer.ps1            # build + publish + Inno Setup installer script
├── DnDMap.iss                     # Inno Setup script (produces Setup\DnDMap-Setup.exe)
├── .github/workflows/release.yml  # WPF installer pipeline (tag v*)
├── .github/workflows/android.yml  # APK pipeline (branch push / manual)
├── publish/                       # local `dotnet publish` output (git-ignored)
└── global.json                    # .NET SDK selection
```

ViewModels are split across partial classes: `MapViewModel` lives in `MainViewModel.cs`, `Properties.cs`, `Tokens.cs`, `ZoomPan.cs`; `SettingsViewModel` lives in `SettingsViewModel.cs`, `Properties.cs`, `Methods.cs`.

## Installer / release

```powershell
.\build-installer.ps1 -Configuration Release -Version 1.0.0
```

The script:

1. cleans `Setup\` and `publish\<Version>`;
2. restores and builds the solution;
3. publishes the app **self-contained** (`-r win-x64`) to `publish\<Version>` — the target machine needs no .NET runtime;
4. compiles `DnDMap.iss` with Inno Setup into `Setup\DnDMap-Setup.exe`.

Requirements: .NET SDK (see `global.json`) and [Inno Setup 6](https://jrsoftware.org/isdl.php) (`ISCC.exe`; on CI it is installed via `choco install innosetup`). The script exits with a clear error if `ISCC.exe` is not found.

The installer is **per-user** (installs to `%LOCALAPPDATA%\DnDMap`, no admin/UAC prompt), creates Start Menu and desktop shortcuts and an uninstaller entry.

The GitHub Actions workflow `.github/workflows/release.yml` runs on tags matching `v*` (or manually via *workflow_dispatch*, version `0.0.0-dev`), builds everything with the script, uploads `DnDMap-Setup-<version>` as an artifact and attaches `Setup/DnDMap-Setup.exe` to the GitHub release when the run was triggered by a tag.

## Android host (APK)

`src/DnDMap.Android` is a plain `net10.0-android` app (no MAUI). It starts the same `DnDMap.Server` on port 5000, keeps the screen on and opens the SPA with `?display=1` in a fullscreen WebView, so the tablet shows only the map. Maps and tokens are cached in `FilesDir/session-cache` and wiped on app start and exit; the DM's browser keeps the originals in IndexedDB and re-uploads missing files.

Build requirements: Android workload, JDK 17 and Android SDK 35+.

```powershell
dotnet workload restore src/DnDMap.Android/DnDMap.Android.csproj
dotnet build src/DnDMap.Android/DnDMap.Android.csproj -c Debug
```

The installable APK is `src/DnDMap.Android/bin/Debug/net10.0-android/ru.dndmap.host-Signed.apk`. Install it with `adb install -r <apk>` or copy it to the tablet and open it (allow installation from unknown sources).

On launch the app shows a toast with the address for the DM, e.g. `http://192.168.1.42:5000/`. Open it in a browser on the laptop — the remote control page has file upload, map/token selection, zoom and token-size controls.

The GitHub Actions workflow `.github/workflows/android.yml` builds the APK on every push to any branch that touches the host projects (or via *workflow_dispatch*) and uploads `DnDMap-android-<run>` as an artifact. The debug build is signed with the runner's debug key, so it installs directly; release signing with your own keystore is a later step.

> The Android project is intentionally **not** part of `Debug|Release` solution configurations, so `dotnet build DnDMap.sln` keeps working on machines without the Android toolchain.

## Status / known limitations

- Automated tests cover the shared core and the HTTP server (`src/DnDMap.Core.Tests`, `src/DnDMap.Server.Tests`).
- Tokens and the open map are not persisted between sessions — everything is reset when the map window (or the Android app) closes. (Switching to another map in the still-open window keeps the tokens.)
- Closing the control window exits the whole application (`ShutdownMode.OnMainWindowClose`).
- The installer is not code-signed — Windows SmartScreen may warn on first run.
- The full CI pipeline (Inno Setup compilation + release upload) is verified by the first `workflow_dispatch`/tag run, not locally.
- The Android host has no authentication and assumes a trusted local network; the APK from CI is debug-signed.
