# DnD Map Project

A small WPF desktop app that can be used to show location maps for Dungeons & Dragons on a screen. It also lets you add tokens for creatures and characters and move them around during the game.

Currently, it's in a stage of `lazy development` where the main functionality is implemented and some features are just stashed.

> The app UI itself is in Russian.

## Features

- Choose a folder with map images and open a map in a borderless fullscreen window.
- Choose a folder with token images (`.png`, `.jpg`, `.jpeg`) and add tokens to the map.
- Drag tokens with the mouse, select them (highlighted), remove with `Del`.
- Token diameter follows the D&D size categories: `Normal` / `Big` / `Huge` → ×1 / ×2 / ×3 of the base diameter.
- Adjustable base diameter (slider, 20–200 px, default 100 px), applied to all tokens at once.
- Pan the map by dragging, zoom with `Ctrl` + mouse wheel or the vertical slider (1×–3×), reset the view with `Reset`.
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
2. **Выбор карты** → **Обзор...** → pick a folder with map images → select a map → **Открыть карту**.
3. **Параметры круга** → set the base diameter and the creature size for the next token.
4. **Галерея изображений** → **Обзор...** → pick a folder with token images → select one → **Добавить токен**.
5. Interact with the map window (it opens maximized, without a title bar — close it with `Alt+F4`):

| Action | Result |
| --- | --- |
| Drag empty space | Pan the map |
| Drag a token | Move the token |
| Click a token | Select it (red highlight) |
| `Del` | Delete the selected token |
| `Ctrl` + mouse wheel | Zoom in/out at the cursor |
| Vertical slider / `Reset` | Zoom / reset zoom and position |

## Project structure

```
DnDMap.sln
├── DnDMap/                        # the only project — WPF app
│   ├── App.xaml(.cs)              # entry point; startup window = SettingsWindow
│   ├── SettingsWindow/            # «Управление» window (XAML + partial ViewModel)
│   ├── MapWindow/                 # fullscreen map window + MapViewModel (partial)
│   ├── TokenModel/                # Token model + its behavior (partial classes)
│   ├── Models/                    # ImageItem, EntitySizeItem
│   ├── Core/                      # ViewModelBase, RelayCommand, GlobalSettings,
│   │                               # enums, converters, WPF/markup extensions
│   ├── Paths.settings / App.config# persisted map & token folders (user settings)
│   └── src/logo.ico               # application icon
├── build-installer.ps1            # build + publish + Inno Setup installer script
├── DnDMap.iss                     # Inno Setup script (produces Setup\DnDMap-Setup.exe)
├── .github/workflows/release.yml  # build & release pipeline (tag v*)
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

## Status / known limitations

- No automated tests: the solution contains no test projects.
- Tokens and the open map are not persisted — everything is reset when the map window closes.
- Closing the control window exits the whole application (`ShutdownMode.OnMainWindowClose`).
- The installer is not code-signed — Windows SmartScreen may warn on first run.
- The full CI pipeline (Inno Setup compilation + release upload) is verified by the first `workflow_dispatch`/tag run, not locally.
