# DnDMap — карта базы знаний

Проект: приложение для показа карт Dungeons & Dragons на экране с токенами существ и персонажей.
Сейчас это WPF-десктоп; идёт переезд на схему «ядро + веб-сервер + Android-хост» (self-host в локальной сети).
Репозиторий: https://github.com/rD3xteR/DnDMap
Последнее обновление: 2026-11-11

| Файл | Содержание |
| --- | --- |
| [architecture.md](architecture.md) | Компоненты, потоки данных, точки входа, связи между модулями |
| [decisions.md](decisions.md) | Журнал решений по датам (git-история + текущая работа) |
| [conventions.md](conventions.md) | Стиль кода, команды сборки/тестов, окружение, договорённости |
| [glossary.md](glossary.md) | Термины и сущности проекта |
| [troubleshooting.md](troubleshooting.md) | Известные проблемы, предупреждения сборки, баги, подозрения |
| [topics/ui-and-interaction.md](topics/ui-and-interaction.md) | Окна, ввод, рендеринг карты и токенов, координаты |
| [topics/build-and-release.md](topics/build-and-release.md) | Сборка, publish, инсталлятор Inno Setup, CI/CD |
| [topics/core-and-web.md](topics/core-and-web.md) | Ядро `DnDMap.Core`, система координат, план API/SSE для веб-клиента |

## Быстрый старт

- Собрать всё: `dotnet build DnDMap.sln -c Debug`.
- Тесты ядра: `dotnet test src/DnDMap.Core.Tests/DnDMap.Core.Tests.csproj`.
- Запустить WPF-приложение: `dotnet run --project DnDMap/DnDMap.csproj` — стартовое окно «Управление».
- Каталог сборки **WPF-проекта — вне репозитория**: `<три уровня выше DnDMap/DnDMap>\DndMap\Debug`
  (см. `BaseOutputPath` в `DnDMap/DnDMap.csproj`). Новые проекты `src/*` собираются в свои `bin/obj` (в репозитории, git их игнорирует).

## Что сейчас в рабочей копии

Проверено 2026-11-11:

- **Закоммичено** `a286db0` `feat: add shared core, session model and core tests (#15)` — ядро, каркас сервера, 75 тестов.
- **Не закоммичено** (M1, по просьбе пользователя): WPF переведён на `DnDMap.Core` —
  `MapViewModel` стал мостом над `MapSession`, рендер в экранных координатах, удалены дубли
  (`GlobalSettings`, `EntitySize`, `TokenParams`, `Token`, `EntitySizeItem`), добавлен `TokenViewModel`.
- Ранее не закоммичено: персистентность путей (`Paths.settings`, `Paths.Designer.cs`, `App.config`).
- Подробности — в [decisions.md](decisions.md), [architecture.md](architecture.md) и [troubleshooting.md](troubleshooting.md).
