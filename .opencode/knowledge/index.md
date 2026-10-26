# DnDMap — карта базы знаний

Проект: десктопное WPF-приложение для показа карт Dungeons & Dragons на экране с токенами существ и персонажей.
Репозиторий: https://github.com/rD3xteR/DnDMap
Последнее обновление: 2026-10-26

| Файл | Содержание |
| --- | --- |
| [architecture.md](architecture.md) | Компоненты, потоки данных, точки входа, связи между модулями |
| [decisions.md](decisions.md) | Журнал решений по датам (git-история + текущая работа) |
| [conventions.md](conventions.md) | Стиль кода, команды сборки, окружение, договорённости |
| [glossary.md](glossary.md) | Термины и сущности проекта |
| [troubleshooting.md](troubleshooting.md) | Известные проблемы, предупреждения сборки, баги, подозрения |
| [topics/ui-and-interaction.md](topics/ui-and-interaction.md) | Окна, ввод, рендеринг карты и токенов, координаты |
| [topics/build-and-release.md](topics/build-and-release.md) | Сборка, publish, инсталлятор Inno Setup, CI/CD |

## Быстрый старт

- Собрать: `dotnet build DnDMap.sln` (подробности и неочевидности — в [conventions.md](conventions.md)).
- Запустить: `dotnet run --project DnDMap/DnDMap.csproj` — стартовое окно «Управление».
- Каталог сборки **вне репозитория**: `<три уровня выше DnDMap/DnDMap>\DndMap\Debug` (см. `BaseOutputPath` в `DnDMap/DnDMap.csproj`).

## Что сейчас в рабочей копии

Ветка `master` содержит незакоммиченные изменения (проверено `git status` 2026-10-26):
персистентность путей (`Paths.settings`, `Paths.Designer.cs`, `App.config` — **не отслеживаются git**),
разбиение `MapViewModel` на файлы `Tokens.cs` / `ZoomPan.cs` (старый `Methods.cs` удалён),
зум/панорамирование с ограничением границ, русские подписи в UI.
Подробности — в [decisions.md](decisions.md) и [troubleshooting.md](troubleshooting.md).
