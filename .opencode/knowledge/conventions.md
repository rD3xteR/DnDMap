# Договорённости и конвенции

Обновлено: 2026-11-11.

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
- Используются новые фичи C#: `field`-keyword в свойствах (`MapWindow/ViewModel/Properties.cs`), primary constructors (`ImageItem`, `TokenViewModel`), collection expressions `[]`.
- **VM разбиваются по partial-файлам**: `*.cs` — конструктор/команды, `Properties.cs` — свойства и поля, отдельные файлы — логика по темам (`Tokens.cs`, `ZoomPan.cs`, `Methods.cs`). Новый код продолжать в том же стиле.
- Состояние игры — только в ядре (`MapSession`); WPF-VM не хранит карту/токены/кадр, а переводит команды и снимки в экранные координаты.
- Настройки, которые раньше были статикой (`GlobalSettings.TokenDiameter`), теперь живут в `MapSession` и меняются через `SetDiameterCommand`; список расширений — `PhysicalFileStore.SupportedExtensions` (ядро).
- `SetProperty(ref field, value)` из `ViewModelBase` — стандартный сеттер; для `double` есть отдельная перегрузка с допуском `1e-9` (исправлено 2026-10-26, раньше было `0.1` и отсекало мелкие шаги).

## Именование и известные особенности имён

- `Core/Converters/BoleanToVisibilityConverter.cs` — **опечатка в имени файла**, класс внутри называется `BooleanToVisibilityConverter`. Не «чинить» молча: имя файла правится вместе с обновлением ссылок.
- `Token.Model.cs` / `Token.Behavior.cs` — исторические имена, удалены в M1 (модель токена теперь `MapToken` в ядре).
- Перечисления: `EntitySize` (Normal=1, Big=2, Huge=3) из ядра — значения используются как множитель диаметра, не менять без пересчёта `MapToken.Size`.

## Сборка и запуск

```powershell
dotnet build DnDMap.sln -c Debug     # проверка сборки (предпочтительный способ проверки)
dotnet build DnDMap.sln -c Release
dotnet run --project DnDMap/DnDMap.csproj
```

- SDK: `global.json` требует `8.0.0` c `rollForward: latestMajor` → на машине реально используется последний установленный (проверено: 10.0.302).
- Целевая платформа `net10.0-windows7.0`, `UseWPF=true`, `UseWindowsForms=true` (нужен `FolderBrowserDialog`).
- **Каталог вывода — вне репозитория**: `BaseOutputPath = ..\..\..\DndMap` относительно `DnDMap/DnDMap/`, то есть `C:\Users\User\source\DndMap\<Configuration>\`. Искать бинарники в `bin/` не нужно — их там нет; `AppendTargetFrameworkToOutputPath/AppendRuntimeIdentifierToOutputPath = false`.
- Тестовые проекты — `src/DnDMap.Core.Tests` (xUnit, 92 теста) и `src/DnDMap.Server.Tests` (26 тестов на живом `MapServer`). Анализаторов/линтеров нет; сборка идёт с **0 предупреждений**.

### Инсталлятор

```powershell
.\build-installer.ps1 -Configuration Release -Version 1.0.0   # требует Inno Setup 6
```

## Git

- Основная ветка `master`, remote `https://github.com/rD3xteR/DnDMap.git`.
- `publish/`, `/Setup/` (каталог инсталлятора), `bin/`, `obj/`, `.vs/` — в `.gitignore`.
- В репозиторий **закоммичено**: 3 файла `.idea/.idea.DnDMap/.idea/*` (мелочь, не удалять без обсуждения) и `README.md`, `build-installer.ps1`, `.github/workflows/release.yml`.
- **Новое и пока не отслеживаемое**: `DnDMap.iss` (инсталлятор) и `.opencode/` (база знаний) — при коммите текущего функционала добавить их вместе с `Paths.settings`/`Paths.Designer.cs`/`App.config`.
- Рабочая копия содержит незакоммиченный функционал (пути, M1 — WPF на ядре) — см. decisions.md; не откатывать и не «приводить в порядок» без явной просьбы.
- Ветка M0/M1 — `feature/15-m0-core` (от `master`); коммит M1 делается только по явной просьбе.

## Работа с файлами

- Секретов в проекте нет и появляться не должно (папки — только локальные пути пользователя).
- PowerShell-скрипты в корне (`build-installer.ps1`) — **только ASCII**: они должны корректно выполняться при любой кодировке консоли/раннера. Старый скрипт был с испорченной кодировкой (см. troubleshooting) — этот урок сохранён в правиле.
- Инсталлятор собирается командой `.\build-installer.ps1 -Version <версия>`; нужен установленный Inno Setup 6 (`ISCC.exe`). Скрипт сам чистит `Setup\` и `publish\<Version>` — это ожидаемо.

## Новые проекты `src/` (с 2026-11-10)

```powershell
dotnet build DnDMap.sln -c Debug
dotnet build DnDMap.sln -c Release
dotnet test src/DnDMap.Core.Tests/DnDMap.Core.Tests.csproj
dotnet test src/DnDMap.Server.Tests/DnDMap.Server.Tests.csproj
```

- `src/DnDMap.Core`, `src/DnDMap.Server`, `src/DnDMap.Core.Tests`, `src/DnDMap.Server.Tests` — `net10.0`, `LangVersion=preview`, `Nullable=enable`.
- Статика SPA лежит в `src/DnDMap.Server/wwwroot/` и подключается как `EmbeddedResource` с явным `LogicalName`
  (`DnDMap.Server.wwwroot.*`) — файлы на диск рядом с exe не копируются, на Android это важно.
- `DnDMap.Server.Tests` поднимает настоящий сервер на `127.0.0.1:0` и временной папке (`ServerFixture`),
  ходит через `HttpClient`, SSE читает построчно. Порт 0 и очистка папки — обязательны, чтобы тесты не конфликтовали.
- Вывод — в их собственные `bin/obj` **внутри** репозитория (в отличие от WPF-проекта с `BaseOutputPath`); git их игнорирует.
- Корневой `Directory.Build.props` **не добавлять**: он применится и к существующему WPF-проекту и может изменить его сборку.
- Прод-код — только BCL. NuGet-пакеты допустимы только в тестовом проекте
  (`xunit 2.9.3`, `xunit.runner.visualstudio 3.1.5`, `Microsoft.NET.Test.Sdk 18.11.0`).
- Тесты фиксируют поведение: `LegacyParityTests` сравнивает новую геометрию со старой `ZoomPan.cs`.
  При изменении математики кадра parity-тесты обязаны оставаться зелёными (иначе это осознанное изменение поведения — фиксировать в decisions).
