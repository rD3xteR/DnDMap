# Журнал решений

Даты по git-истории и фактическим изменениям. Автор по умолчанию — владелец репозитория (rD3xteR), записи 2026-10-26 — по итогам анализа проекта агентом.

## 2025-11-07 — импорт проекта

- Заведены `.gitignore` и `.gitattributes` по стандартным шаблонам Visual Studio (`09e5480`), затем исходники WPF-проекта (`cf3ccc4`).
- Решение: шаблон VS-репозитория целиком, без укорачивания — отсюда 366 строк `.gitignore`.

## 2025-11-08 — вёрстка, вывод сборки, README, CI

- `782082f`: добавлен икон `src/logo.ico`, `ApplicationIcon`, и изменён каталог вывода — в `.csproj` появился `BaseOutputPath = ..\..\..\DndMap` (вывод **вне** репозитория, см. conventions).
- `b3410ec`: создан `README.md` (краткое англоязычное описание, статус «lazy development»).
- `83ac66d`: добавлен GitHub Actions `.github/workflows/release.yml` — сборка и релиз по тегам `v*`.
- `077cf61`: метка «v1» в истории.

## 2025-11-09 — дорожная карта (GitHub Issues)

Открыты три issue, два из них — запланированные фичи, один — баг:

- **#4 «Tokens always spawn at X = 0 and Y = 0»** — открыт, без описания; причина видна в коде: `MapViewModel.AddToken` не задаёт координаты, у `Token.X/Y` значение по умолчанию 0.
- **#5 «Feature. Closed blocks of map with hidden info»** — фича: закрытые блоки карты с скрытой информацией (для мастера).
- **#6 «CI/CD. Installer and auto-releases»** — фича: доработка инсталлятора и автоворелизов.

## 2025-12-12 — правки по issues #1–#3

- `08434a6` «Solve issues #1, #2, #3 edit gitignore» — закрыты issues #1–#3 (текст сейчас недоступен в списке открытых), параллельно поправлен `.gitignore`.

## 2026-10-26 — состояние рабочей копии (не закоммичено)

По `git status`/`git diff` зафиксированы следующие изменения — они ещё не в истории, при работе с ними учитывай:

- **Персистентность путей**: добавлены `Paths.settings`, `Paths.Designer.cs`, `App.config`; `SettingsViewModel` запоминает `MapsPath`/`TokensPath` и сохраняет их при закрытии окна. Эти файлы **untracked** — в `HEAD` ссылок на `Paths` нет (`git grep Paths HEAD` пуст), поэтому свежий клон фичу не получит.
- **Рефакторинг MapViewModel**: `MapWindow/ViewModel/Methods.cs` удалён, вместо него `Tokens.cs` (добавление/выделение/перемещение токенов) и `ZoomPan.cs` (зум/панорама с ограничением границ). `MapViewModel` теперь разнесён на 4 partial-файла.
- **UI**: подписи окон и групп переведены/написаны по-русски («Управление», «Выбор карты», «Параметры круга», «Галерея изображений»), заголовок окна карты — «Draggable Circles».
- **Интеракция карты**: переработаны mouse-хендлеры `MapWindow.xaml.cs` (панорама, перетаскивание токена, зум по Ctrl+колесу, удаление по `Del`), добавлены `UpdateViewportSize`/`TransformToMap`.

## 2026-10-26 — CI и установщик (по согласованию с пользователем)

Решения пользователя (вопросы до начала правок):
- технология инсталлятора — **Inno Setup** (варианты рассматривались: Velopack, починка Squirrel, zip);
- сборка для конечного пользователя — **self-contained** (`-r win-x64 --self-contained true`), чтобы игрокам не нужен был .NET;
- удалить пакет `squirrel.windows` из проекта — **да**;
- ставить что-либо на машину для локальной верификации — **нет** (проверка = ручной просмотр + первый запуск CI).

Что изменено:
- `.github/workflows/release.yml` — SDK `10.0.x` (был 8.0.x), удалён падающий `dotnet add package` в корне, добавлены `permissions: contents: write` и `if-no-files-found: error`, версия выводится из тега (иначе `0.0.0-dev`), Inno Setup ставится через choco только при отсутствии, добавлен `softprops/action-gh-release@v2`, удалён бессмысленный шаг `dotnet test`.
- `build-installer.ps1` — переписан целиком: clean → restore → build → self-contained publish → ISCC, все вызовы с проверкой `$LASTEXITCODE`, явный `exit 1` если `ISCC.exe` не найден или `Setup\DnDMap-Setup.exe` не создан; записан только ASCII (старый файл имел испорченную кодировку).
- `DnDMap.iss` — новый файл: per-user установка в `{localappdata}\DnDMap` без UAC, ярлыки меню Пуск/рабочего стола, деинсталлятор, запуск после установки; `MyAppVersion` и `PublishDir` передаются через `/D`.
- `DnDMap/DnDMap.csproj` — удалён `PackageReference squirrel.windows 2.0.1` (никем не используется) → исчезли NU1902 (уязвимый SharpCompress) и NU1701.
- `.gitignore` — добавлен `/Setup/` (каталог выхода инсталлятора).
- `README.md` — раздел «Installer / release» переписан под Inno Setup + self-contained.

Проверка:
- `.\build-installer.ps1 -Version 1.0.0-localtest` прогнан локально: clean/restore/build/publish прошли, `DnDMap.exe` создан, затем ожидаемый `exit 1` на отсутствии ISCC; тестовый каталог `publish\1.0.0-localtest` удалён.
- `dotnet build` → `exit 0`, остались только 3×CS8618.
- Синтаксис `build-installer.ps1` проверен парсером PowerShell; `release.yml` вычитан вручную (парсера YAML в окружении нет).
- Найден и исправлен баг поиска ISCC: `"$env:ProgramFiles(x86)\..."` в PowerShell даёт путь без пробела — заменено на `${env:ProgramFiles(x86)}` (проверено на реальном пути `C:\Program Files (x86)\...`).

Не сделано (намеренно): коммит/пуш, запуск CI, закрытие issue #6 — после первого успешного рана CI его можно закрыть.

## 2026-10-26 — документация и база знаний

- `README.md` дополнен (по выбору пользователя — **на английском языке**: сборка, использование, структура, инсталлятор, ограничения).
- Создана база знаний `.opencode/knowledge/` (на русском).
- Сборка проверена: `dotnet build DnDMap.sln -c Debug` → успешно, 0 ошибок; после удаления `squirrel.windows` остались только 3×CS8618.
