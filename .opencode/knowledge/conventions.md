# Договорённости и конвенции

Обновлено: 2026-10-26.

## Языки

| Что | Язык |
| --- | --- |
| Интерфейс приложения (XAML-подписи, заголовки окон) | русский |
| Комментарии в коде | преимущественно русские, встречаются английские |
| `README.md` | **английский** (явный выбор пользователя, 2026-10-26) |
| `.opencode/knowledge/` | русский |
| Commit-сообщения | смешанные (русские «Добавьте файлы проекта», английские «ci/cd») |

## Стиль C#

- `LangVersion = preview`, `Nullable = enable`, `ImplicitUsings = enable` (`DnDMap/DnDMap.csproj`).
- Используются новые фичи C#: `field`-keyword в свойствах (`MapWindow/ViewModel/Properties.cs:11-17`), primary constructors (`TokenParams`, `ImageItem`), collection expressions `[]`.
- **VM разбиваются по partial-файлам**: `*.cs` — конструктор/команды, `Properties.cs` — свойства и поля, отдельные файлы — логика по темам (`Tokens.cs`, `ZoomPan.cs`, `Methods.cs`). Новый код продолжать в том же стиле.
- Модель тоже partial: `Token.Model.cs` (свойства) + `Token.Behavior.cs` (методы).
- Статические глобальные настройки через `Core/GlobalSettings.cs` (static + event), не через DI.
- `SetProperty(ref field, value)` из `ViewModelBase` — стандартный сеттер; для `double` есть отдельная перегрузка с допуском `000.1` (внимание: это не типографическая ошибка в docs, а реальная константа `000.1` в `ViewModelBase.cs:29`).

## Именование и известные особенности имён

- `Core/Converters/BoleanToVisibilityConverter.cs` — **опечатка в имени файла**, класс внутри называется `BooleanToVisibilityConverter`. Не «чинить» молча: имя файла правится вместе с обновлением ссылок.
- `Token.Model.cs` / `Token.Behavior.cs` — точка вместо привычного суффикса.
- Перечисления: `EntitySize` (Normal=1, Big=2, Huge=3) — значения используются как множитель диаметра, не менять без пересчёта `Token.Diameter`.

## Сборка и запуск

```powershell
dotnet build DnDMap.sln -c Debug     # проверка сборки (предпочтительный способ проверки)
dotnet build DnDMap.sln -c Release
dotnet run --project DnDMap/DnDMap.csproj
```

- SDK: `global.json` требует `8.0.0` c `rollForward: latestMajor` → на машине реально используется последний установленный (проверено: 10.0.302).
- Целевая платформа `net10.0-windows7.0`, `UseWPF=true`, `UseWindowsForms=true` (нужен `FolderBrowserDialog`).
- **Каталог вывода — вне репозитория**: `BaseOutputPath = ..\..\..\DndMap` относительно `DnDMap/DnDMap/`, то есть `C:\Users\User\source\DndMap\<Configuration>\`. Искать бинарники в `bin/` не нужно — их там нет; `AppendTargetFrameworkToOutputPath/AppendRuntimeIdentifierToOutputPath = false`.
- Тестовых проектов нет; анализаторов/линтеров нет (кроме предупреждений SDK и NuGet). После удаления `squirrel.windows` (2026-10-26) сборка даёт только 3×CS8618.

### Инсталлятор

```powershell
.\build-installer.ps1 -Configuration Release -Version 1.0.0   # требует Inno Setup 6
```

## Git

- Основная ветка `master`, remote `https://github.com/rD3xteR/DnDMap.git`.
- `publish/`, `/Setup/` (каталог инсталлятора), `bin/`, `obj/`, `.vs/` — в `.gitignore`.
- В репозиторий **закоммичено**: 3 файла `.idea/.idea.DnDMap/.idea/*` (мелочь, не удалять без обсуждения) и `README.md`, `build-installer.ps1`, `.github/workflows/release.yml`.
- **Новое и пока не отслеживаемое**: `DnDMap.iss` (инсталлятор) и `.opencode/` (база знаний) — при коммите текущего функционала добавить их вместе с `Paths.settings`/`Paths.Designer.cs`/`App.config`.
- Рабочая копия содержит незакоммиченный функционал (пути, partial-рефакторинг, CI/инсталлятор) — см. decisions.md; не откатывать и не «приводить в порядок» без явной просьбы.

## Работа с файлами

- Секретов в проекте нет и появляться не должно (папки — только локальные пути пользователя).
- PowerShell-скрипты в корне (`build-installer.ps1`) — **только ASCII**: они должны корректно выполняться при любой кодировке консоли/раннера. Старый скрипт был с испорченной кодировкой (см. troubleshooting) — этот урок сохранён в правиле.
- Инсталлятор собирается командой `.\build-installer.ps1 -Version <версия>`; нужен установленный Inno Setup 6 (`ISCC.exe`). Скрипт сам чистит `Setup\` и `publish\<Version>` — это ожидаемо.
