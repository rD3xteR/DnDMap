# DnDMap — карта базы знаний

Проект: приложение для показа карт Dungeons & Dragons на экране с токенами существ и персонажей.
Сейчас это WPF-десктоп; идёт переезд на схему «ядро + веб-сервер + Android-хост» (self-host в локальной сети).
Репозиторий: https://github.com/rD3xteR/DnDMap
Последнее обновление: 2026-11-12

| Файл | Содержание |
| --- | --- |
| [architecture.md](architecture.md) | Компоненты, потоки данных, точки входа, связи между модулями |
| [decisions.md](decisions.md) | Журнал решений по датам (git-история + текущая работа) |
| [conventions.md](conventions.md) | Стиль кода, команды сборки/тестов, окружение, договорённости |
| [glossary.md](glossary.md) | Термины и сущности проекта |
| [troubleshooting.md](troubleshooting.md) | Известные проблемы, предупреждения сборки, баги, подозрения |
| [topics/ui-and-interaction.md](topics/ui-and-interaction.md) | Окна, ввод, рендеринг карты и токенов, координаты |
| [topics/build-and-release.md](topics/build-and-release.md) | Сборка, publish, инсталлятор Inno Setup, CI/CD |
| [topics/core-and-web.md](topics/core-and-web.md) | Ядро `DnDMap.Core`, система координат, HTTP-сервер, API/SSE, SPA |

## Быстрый старт

- Собрать всё: `dotnet build DnDMap.sln -c Debug`.
- Тесты ядра: `dotnet test src/DnDMap.Core.Tests/DnDMap.Core.Tests.csproj`.
- Тесты сервера: `dotnet test src/DnDMap.Server.Tests/DnDMap.Server.Tests.csproj`.
- Android APK (нужны JDK 17 + Android SDK 36): `dotnet workload restore src/DnDMap.Android/DnDMap.Android.csproj`,
  затем `dotnet build src/DnDMap.Android/DnDMap.Android.csproj -c Debug`; результат —
  `src/DnDMap.Android/bin/Debug/net10.0-android/ru.dndmap.host-Signed.apk`.
  В CI — workflow `Android APK` (artifact `DnDMap-android-<run>`).
- Запустить WPF-приложение: `dotnet run --project DnDMap/DnDMap.csproj` — стартовое окно «Управление»;
  там же группа «Сервер для планшета» → «Запустить сервер» и адрес для браузера.
- Каталог сборки **WPF-проекта — вне репозитория**: `<три уровня выше DnDMap/DnDMap>\DndMap\Debug`
  (см. `BaseOutputPath` в `DnDMap/DnDMap.csproj`). Новые проекты `src/*` собираются в свои `bin/obj` (в репозитории, git их игнорирует).

## Что сейчас в рабочей копии

Проверено 2026-11-12:

- **Закоммичено** `a286db0` `feat: add shared core, session model and core tests (#15)` — ядро, каркас сервера, 75 тестов.
- **Закоммичено** `863e9b3` `feat: run WPF app on shared core, drop touch input (#15)` — M1: WPF на ядре,
  сенсорный ввод удалён.
- **Закоммичено** `d96c826` `feat: add web remote, fix zoom/bounds and token sizing (#15)` — M2:
  HTTP-сервер, SPA, тесты сервера, группа «Сервер для планшета»; исправлены зум, границы и размер токенов.
- **Не закоммичено** (M3/M4, 2026-11-12): `src/DnDMap.Android` (Activity + WebView, кэш `FilesDir`,
  keep-screen-on), режим SPA `?display=1`, `POST /api/viewport`, workflow `.github/workflows/android.yml`,
  обновлённый README. Тесты: ядро 82, сервер 17. Локальная сборка APK не проверена (нет JDK 17/Android SDK).
- Ранее не закоммичено: персистентность путей (`Paths.settings`, `Paths.Designer.cs`, `App.config`).
- Подробности — в [decisions.md](decisions.md), [architecture.md](architecture.md) и [troubleshooting.md](troubleshooting.md).
