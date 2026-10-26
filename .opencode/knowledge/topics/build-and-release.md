# Тема: сборка, публикация, релизы

Обновлено: 2026-10-26.

## Локальная сборка

```powershell
dotnet build DnDMap.sln -c Debug      # 6-7 сек на машине разработчика
dotnet build DnDMap.sln -c Release
dotnet run --project DnDMap/DnDMap.csproj
```

- Решение одно: `DnDMap.sln` → проект `DnDMap/DnDMap.csproj`.
- SDK выбирается через `global.json`: минимум `8.0.0`, `rollForward: latestMajor`, `allowPrerelease: true` → берётся последний установленный (фактически 10.0.x).
- **Куда пишется результат**: `BaseOutputPath = ..\..\..\DndMap` + `AppendTargetFrameworkToOutputPath=false` → `C:\Users\User\source\DndMap\Debug\` (и `...\Release\`). Каталоги `bin/`, `obj/` в репозитории для результата не используются (в `obj/` — только служебные файлы NuGet/MSBuild).
- Предупреждения сборки на 2026-10-26: только 3×CS8618 (nullable). NU1902/NU1701 исчезли после удаления `squirrel.windows` (см. troubleshooting).

## Публикация и инсталлятор (Inno Setup)

Единая точка входа — `build-installer.ps1` в корне репозитория:

```powershell
.\build-installer.ps1 [-Configuration Release] [-Version 1.0.0] [-Runtime win-x64] [-SkipClean]
```

Порядок шагов:

1. Удаляет `Setup\` и `publish\<Version>`.
2. `dotnet clean` (если не `-SkipClean`), `dotnet restore`, `dotnet build --no-restore --no-incremental`.
3. `dotnet publish -r win-x64 --self-contained true -o publish\<Version>` — приложение самодостаточно, на машине игрока **не нужен .NET**. Далее проверка, что `publish\<Version>\DnDMap.exe` существует.
4. Поиск `ISCC.exe` в4 кандидатах (Program Files (x86), Program Files, `%LOCALAPPDATA%\Programs`, PATH); если не найден — **явная ошибка и exit 1** (тихого пропуска больше нет).
5. `ISCC.exe /DMyAppVersion=<ver> /DPublishDir=publish\<ver> DnDMap.iss` → `Setup\DnDMap-Setup.exe`; проверка, что файл создан, и печать размеров.

Все вызовы `dotnet`/`ISCC` обёрнуты в функции с проверкой `$LASTEXITCODE` — падение любого шага валит скрипт с тем же кодом (а значит и шаг CI). Скрипт намеренно напят только ASCII — устойчив к любой кодировке консоли.

### DnDMap.iss

- Установка **для текущего пользователя**: `PrivilegesRequired=lowest`, каталог `{localappdata}\DnDMap` — без UAC/прав администратора.
- `MinVersion=10.0`, `ArchitecturesAllowed=x64compatible`.
- Выход: `OutputDir=Setup`, `OutputBaseFilename=DnDMap-Setup` → `Setup\DnDMap-Setup.exe`.
- Иконка установщика/приложения: `DnDMap\src\logo.ico`.
- Файлы: `{#PublishDir}\*` (весь publish-каталог, включая подкаталоги).
- Ярлыки: группа меню Пуск «DnDMap» + «Uninstall DnDMap», рабочий стол (опциональная задача `desktopicon`).
- `[Run]` — предложение запустить приложение после установки (`skipifsilent` для неинтерактивной установки).
- Переопределяемые через `/D`: `MyAppVersion` (версия в свойствах и «Установленные программы»), `PublishDir` (путь относительно каталога `DnDMap.iss` — поэтому скрипт передаёт `publish\<Version>`, а не абсолютный путь).

### Каталоги

| Каталог | Назначение | Git |
| --- | --- | --- |
| `publish\<Version>\` | самодостаточная публикация приложения | игнорируется (`publish/`) |
| `Setup\` | итоговый `DnDMap-Setup.exe` | игнорируется (`/Setup/`, добавлено 2026-10-26) |
| `C:\Users\User\source\DndMap\<Config>\` | промежуточный вывод сборки | вне репозитория |

## CI/CD (GitHub Actions)

Файл: `.github/workflows/release.yml`, имя «Build and Release».

| Параметр | Значение |
| --- | --- |
| Триггеры | `push` тегов `v*`, `workflow_dispatch` (вручную) |
| Раннер | `windows-latest` |
| SDK | `dotnet-version: 10.0.x` (соответствует `net10.0-windows7.0`) |
| Права | `permissions: contents: write` (нужны `action-gh-release`) |
| Версия | из тега без `v` (`v1.2.0` → `1.2.0`); при ручном запуске — `0.0.0-dev` |
| Inno Setup | ставится через `choco install innosetup`, **только если ISCC уже отсутствует** |
| Сборка | один шаг: `.\build-installer.ps1 -Version <версия>` |
| Артефакт | `DnDMap-Setup-<version>` = `Setup/DnDMap-Setup.exe` (`if-no-files-found: error`) |
| Релиз | `softprops/action-gh-release@v2`, только для тегов, прикладывает `Setup/DnDMap-Setup.exe` |

Шаг `dotnet test` удалён (тестовых проектов нет — он проверял пустоту); вернуть при появлении тестов.

Состояние проверки: локально прогнаны шаги 1–3 скрипта (clean → build → self-contained publish) и ветка ошибок; шаг Inno Setup (`ISCC.exe`) и сам раннер проверяются первым запуском CI (`workflow_dispatch` или тег).

## Релизная практика проекта

- Теги вида `v*` (в истории есть «v1»), версия по умолчанию в скрипте — `1.0.0`.
- Артефакты сборки в репозиторий не коммитятся (`publish/`, `Setup/` игнорируются).
- Инсталлятор не подписывается кодом → SmartScreen может ругаться при первом запуске.
