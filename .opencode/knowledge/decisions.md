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

## 2026-11-10 — M0: ядро, тесты, курс на self-host (по согласованию с пользователем)

Цель, согласованная с пользователем: **Android-планшет** показывает карту во весь экран и поднимает
веб-сервер в локальной сети; **ноутбук мастера** заходит в браузер, загружает/скачивает карты и токены,
видит живое зеркало карты и **перетаскивает токены** — изменения сразу видны на планшете.

Решения (зафиксированы до правок):

- **Стек Android** — `.NET for Android` (`net10.0-android`), **без MAUI**. Сервер — свой, на BCL
  (`TcpListener`), потому что ASP.NET Core на Android официально не поддерживается (trimming/AOT, размер APK).
- **Хранилище на Android** — внутренняя папка приложения (`Context.FilesDir`), без SAF.
  Уточнено в тот же день: это **временный кэш**, а не библиотека — см. запись ниже.
- **Координаты** — токены в пикселях карты; кадр = центр в пикселях карты + зум относительно fit.
  Это делает вид viewport-независимым и даёт одинаковый кадр на столе и в браузере.
- **Диаметр токена** — в пикселях карты (слайдер/API говорят в px эталонного экрана 1920×1080).
  Единственное намеренное отличие от старого WPF-поведения: токен теперь масштабируется вместе с картой.
- **Рендер WPF в M1** — переписывается в экранные координаты (`Image` + абсолютные позиции токенов),
  а не «конвертация на границе»: единая модель с веб-клиентом.
- **Автономность** — интернет не нужен, внешние CDN и NuGet в прод-коде не используются.

Сделано в M0 (не закоммичено, по просьбе пользователя):

- `src/DnDMap.Core` — `Geometry` (`MapPoint`, `RectD`, `ViewState`, `ViewContext`, `ViewGeometry`),
  `Model` (`EntitySize`, `MapInfo`, `MapToken`, `MapSession`, `SessionOptions`, `SessionSnapshot`, `SessionChange`),
  `Storage` (`IFileStore`, `PhysicalFileStore` — защита от path traversal),
  `Imaging` (`ImageSizeReader` — PNG/JPEG без System.Drawing),
  `Protocol` (`Command`, DTO, `ProtocolJson`, `SessionMapper`).
- `src/DnDMap.Server` — каркас: `ServerOptions` (порт, bind, корень хранилища).
- `src/DnDMap.Core.Tests` — 75 тестов: геометрия, clamp, зум к точке, **parity-эталон** старого `ZoomPan.cs`,
  сессия/команды/revision, сериализация команд, хранилище, чтение размеров PNG/JPEG.
- `DnDMap.sln` — три проекта в solution-folder `src`.

Проверка:

- `dotnet build DnDMap.sln -c Debug` → 0 ошибок (3×CS8618 — прежние, в WPF).
- `dotnet build DnDMap.sln -c Release` → 0 ошибок, 0 предупреждений.
- `dotnet test src/DnDMap.Core.Tests` → 75/75 пройдено.
- WPF-проект не изменялся и собирается как раньше.

Не сделано (осознанно): перевод WPF на ядро (M1), HTTP-сервер и веб-клиент (M2), Android (M3), CI для APK (M4).

Issue с этим планом: https://github.com/rD3xteR/DnDMap/issues/15 (создан 2026-11-10).

## 2026-11-10 (уточнение) — файлы идут с ноутбука, планшет только кэширует

Пользователь уточнил требование: **постоянное хранение изображений на планшете не нужно**, нужна передача файлов с ноутбука.

Выбор пользователя (три вопроса, три ответа):

- **Передача** — мастер выбирает файлы/папку в браузере (диалог или drag&drop), браузер отправляет их на планшет по HTTP.
  File System Access API и чтение папки ноутбука «как есть» отпадают: по `http://` в локальной сети это не secure context.
- **Хранение на планшете** — **временный кэш в `Context.FilesDir`**, очищается при старте/выходе приложения.
  Это не библиотека: нет UI управления файлами на планшете, мастер не «открывает» файлы из планшета.
- **Браузер** — помнит последние выбранные файлы в IndexedDB и при подключении досылает недостающие.

Следствия для плана:

- Источник правды по файлам — ноутбук; планшет — приёмник и кэш.
- `PhysicalFileStore` переиспользуется с корнем `FilesDir/session-cache` + очистка при старте; `MapSession` о файловой системе не знает.
- В M2 появляется манифест кэша (`имя`, `размер`, `хэш`) и потоковая загрузка `PUT /api/files/{kind}/{name}`;
  загрузка идёт сырым телом, без multipart — экономит память на планшете.
- `ValidateName` (только `[A-Za-z0-9._-]`) слишком строг для имён с ноутбука: пробелы и кириллица — обычное дело.
  Правило пересмотреть в M2 (ослабить или санитизировать с сохранением исходного имени).
- Коллизии имён (`map.png` из разных папок) — при разном хэше переименовывать, а не перезаписывать.

## 2026-11-11 — M1: WPF переведён на общее ядро

Сделано (по просьбе пользователя «сделай переход с WPF на ядро»):

- `DnDMap/DnDMap.csproj` ссылается на `src/DnDMap.Core`; WPF-проект использует типы ядра
  (`MapSession`, `ViewGeometry`, `EntitySize`, `Command`), своих дублей больше нет.
- `MapViewModel` переписан в **мост**: единственное состояние — `MapSession`, все изменения — командами.
  Partial-файлы сохранили прежние роли: `MainViewModel` (окно/карта), `Properties` (привязки),
  `Tokens` (токены + hit-test), `ZoomPan` (кадр).
- Рендер переведён в **экранные координаты**: у `Canvas` больше нет `RenderTransform`; карта — `Image`
  с `Canvas.Left/Top/Width/Height` из `ViewGeometry.MapRectToScreen`, токены — `TokenViewModel`
  с готовыми `Left/Top/Diameter`. Это то же представление, что получит веб-клиент.
- Ввод: `e.GetPosition(MainCanvas)` — это уже пиксели окна; в пиксели карты переводит `ScreenToMap`.
  Панорама — `PanByScreen`, зум — `ZoomAt` (точка под курсором держится при любом зуме).
- Удалены дубли: `Core/GlobalSettings.cs`, `Core/Enums/EntitySize.cs`, `Core/Parameters/TokenParams.cs`,
  `TokenModel/Token.Model.cs`, `TokenModel/Token.Behavior.cs`, `Models/EntitySizeItem.cs`.
  `ViewModelBase`/`RelayCommand`/конвертеры оставлены в WPF как UI-обвязка.
- Токены, добавленные до открытия карты, копятся в `_pendingTokens` и применяются после `SetMap`
  (сохранено старое поведение «можно готовить заранее»).
- Слайдер диаметра теперь идёт в `MapViewModel.SetDiameter` (px эталона 1920×1080), а не в статику.
- **Сенсорный ввод удалён** по просьбе пользователя (2026-11-11): из `MapWindow.xaml`/`.xaml.cs` убраны
  `IsManipulationEnabled`, `StylusDown` и все `Manipulation*`-обработчики с полями мультитача.
  Управление — только мышью (драг токена, панорама, Ctrl+колесо, слайдер, Reset, Delete).

Технические решения по ходу:

- **Абсолютный путь картинки** в ядро не попадает: сессия хранит только `MapToken.File`. WPF держит
  `tokenId → путь` в `MapViewModel._tokenImagePaths`; если токен пришёл не от WPF — путь собирается
  из `Paths.Default.TokensPath + File`.
- `MapSession.Changed` поднимается **синхронно** внутри `Apply`, поэтому при `AddToken` представление
  создаётся раньше, чем известен путь к файлу. Решение: `TokenViewModel.ImagePath` изменяемый и
  обновляется каждым `Refresh`, а `AddToken` после записи пути вызывает `Refresh()` ещё раз.
- `OpenWindow` разделён на `OpenWindow` (показать окно) и `OpenMap` (только состояние + раскладка) —
  так мост проверяется без WPF-окна.

Проверка:

- `dotnet build DnDMap.sln -c Debug` и `-c Release` → 0 ошибок, 0 предупреждений (3×CS8618 исчезли
  вместе с удалёнными файлами).
- `dotnet test src/DnDMap.Core.Tests` → 75/75.
- Внерепозиторный smoke-тест моста (34 проверки): раскладка карты, hit-test, драг, зум к точке,
  панорама, диаметр/кламп, pending-токены, `OnClosing` → все зелёные.
- Живой запуск `DnDMap.exe`: стартовое окно работает.
- Issue #15: https://github.com/rD3xteR/DnDMap/issues/15 (ветка `feature/15-m0-core`).

Не сделано: HTTP-сервер и веб-клиент (M2), Android (M3), CI для APK (M4).

Коммит: `feat: run WPF app on shared core, drop touch input (#15)`.

## 2026-11-11 — M2: HTTP-сервер и веб-пульт

Сделано (продолжение после M1, по просьбе «переходи к следующему пункту»):

- `src/DnDMap.Server` перестал быть каркасом: `MapServer` на `TcpListener` (BCL, без ASP.NET Core),
  `Http/HttpProtocol` (разбор запроса, ответы с `Content-Length`, SSE-заголовки), `Http/RequestBodyStream`
  (тело с префиксом из буфера заголовков и лимитом), `EmbeddedAssets` (SPA из ресурсов сборки).
- Маршруты: `GET /` (SPA), `/app.css`, `/app.js`; `GET /api/session`; `POST /api/commands`;
  `GET /api/events` (SSE, событие `state` с полным `SessionDto`); `GET /api/files?kind=map|token`
  (манифест `{name,size,hash}`); `PUT|DELETE /api/files/{kind}/{name}`; `GET /files/{kind}/{name}`.
- Размеры карты при `setMap` берёт **сервер** из файла (`ImageSizeReader`), клиент присылает `0/0`.
  Перед `setMap`/`addToken` проверяется наличие файла в кэше (409), иначе в состоянии была бы битая ссылка.
- Загрузка — сырое тело без multipart (экономия памяти на планшете), лимит `ServerOptions.MaxUploadBytes`
  = 256 МБ. Запись атомарная: временный `*.upload` → `File.Move(overwrite: true)`; после записи файл
  проверяется `ImageSizeReader`, битый — удаляется.
- `ValidateName` ослаблен под имена с ноутбука: разрешены пробелы и кириллица, запрещены разделители
  пути, управляющие символы, `CON`/`NUL`/`COM1`…, завершающая точка/пробел; длина ≤ 120.
- `FileHashCache` считает SHA-256 по требованию и инвалидирует запись при загрузке/удалении через сервер.
- SPA (`wwwroot/` → `EmbeddedResource`) без CDN: SSE-подписка, карта/токены в экранных координатах
  (тот же `visibleRect`, что у WPF), драг токенов, панорама, зум колесом/слайдером, две drop-зоны,
  IndexedDB-кэш выбранных файлов и досылка недостающих по манифесту.
- Poison-пилюля зума: `visibleRect` масштабируется клиентом, zoom-at-point считается на клиенте
  из `visibleRect` + `view.zoom` — без дублирования fit/clamp.
- WPF: в окне «Управление» группа «Сервер для планшета» — порт, старт/стоп, URL с локальным IPv4,
  статус. Кэш — `%LOCALAPPDATA%\DnDMap\session-cache`, очищается при старте и в `OnClosing`
  (`SettingsViewModel.StartServer/StopServer/ClearCache`).

Осознанные ограничения (долг):

- Серверная `MapSession` **не связана** с сессией WPF-окна карты: браузер показывает свой стол.
  Свести их — отдельная задача (общий экземпляр `MapSession` для WPF и сервера).
- Аутентификации нет: предполагается доверенная локальная сеть.
- `IndexedDB`-досылка сравнивает только имя+размер (хэш в манифесте есть, но клиент его пока не сверяет).
- HTTP/1.1 без keep-alive; SSE-соединение одно на вкладку и переподключается средствами `EventSource`.

Проверка:

- `dotnet build DnDMap.sln -c Debug` → 0 ошибок, 0 предупреждений.
- `dotnet test src/DnDMap.Core.Tests` → 78/78; `dotnet test src/DnDMap.Server.Tests` → 15/15
  (API, SSE, загрузка, манифест, имена, ошибки).
- Живой прогон SPA (Chromium, сервер на `127.0.0.1:5123`, реальные PNG): загрузка через input,
  «Открыть карту», «+ токен», драг токена (координаты ушли в сессию), зум колесом (slider = 1.12),
  перезапуск сервера с чистым кэшем → браузер дослал 2 файла из IndexedDB.
- Внерепозиторный smoke WPF-обвязки (17 проверок): старт/стоп/рестарт сервера из `SettingsViewModel`,
  URL, кэш, `/api/session`, SPA, загрузка карты и `setMap` с размером из файла — всё зелёное.

## 2026-11-12 — исправления веб-пульта (zoom, границы, размер токенов)

Три жалобы пользователя, все воспроизведены на живом сервере:

1. **Зум не работал.** `MapSession.VisibleRect` и `SessionSnapshot.VisibleRect` передавали в
   `ViewGeometry.VisibleRect` только `FitScale`, без `View.Zoom`. В браузере кадр рисуется
   **исключительно** по `visibleRect`, поэтому ползунок/колесо меняли `view.zoom`, но картинка
   не увеличивалась (видимая область оставалась как при 1×). WPF баг не задевал: он сам считает
   `FitScale * Zoom`. Решение: в `VisibleRect` передаётся полный масштаб `FitScale * View.Zoom`;
   сигнатура `ViewGeometry.VisibleRect(view, scale, ...)` не изменилась, добавлено пояснение.
2. **Карта и токены уходили за границы.** Сервер клампит кадр (`ClampCenter`), но клиент рисовал
   панораму оптимистично и мог утащить её далеко за край до ответа сервера; токен центр не клампился
   вовсе (в сессии оказывались `x=2864` при карте 800). Решение: `MoveTokenCommand` и явные координаты
   `AddTokenCommand` клампят центр в `[0,width]×[0,height]` **в ядре** (одинаково для WPF и веба);
   SPA повторяет `ClampAxis`/`ClampCenter` локально и отправляет уже ограниченный кадр/токен.
3. **Нельзя было изменить размер токенов.** В API/ядре команда `setDiameter` была, в пульте — нет.
   Решение: в шапке SPA слайдер «Токены» 20–200 ref-px → `setDiameter` (throttle 60 мс), значение
   синхронизируется из `baseDiameter.referencePx`; обновляются все токены сразу.

Заодно: зум в пульте получил кнопки «−/+» и подпись `1.25×`; `zoomAt` переводит `clientX/Y` в
координаты сцены (раньше якорь смещался на высоту шапки).

Проверка: `dotnet test` ядра 82/82 (+4: `VisibleRect` с зумом, снимок, кламп `moveToken`/`addToken`);
сервер 15/15. Живой прогон: при `zoom=2.5` `visibleRect.width` = 1066.67 → 426.67, картинка карты
растёт; панорама останавливается на краю (`centerX` ≤ 586.67 для карты 800); драг токена даёт
`x=800, y=600` вместо 2864/1943; слайдер диаметра 200 → `sizeMapPx` 111.11.

## 2026-11-12 — M3: Android-хост и режим экрана стола

Сделано:

- `src/DnDMap.Android` (`net10.0-android`, без MAUI): `MainActivity` в namespace `DnDMap.Host`,
  `ApplicationId = ru.dndmap.host`. Старт: очистка `FilesDir/session-cache` → `MapServer` на порту 5000
  (если занят — `Port=0`) → WebView `http://127.0.0.1:<port>/?display=1`, keep-screen-on.
  Поворот не пересоздаёт Activity (`ConfigurationChanges`), кэш чистится и в `OnDestroy`;
  `Dispose` сервера уводится в пул, чтобы не заблокировать UI-поток на `StopAsync`.
  Адрес для мастера — Toast с локальным IPv4. Разрешения: `INTERNET`, `ACCESS_NETWORK_STATE`;
  `usesCleartextTraffic=true` для HTTP на localhost.
- SPA: режим `?display=1` скрывает шапку и панель файлов, отключает панораму/драг/зум/Delete на планшете.
- Новый маршрут `POST /api/viewport` `{width,height}` → `MapSession.SetViewport`; ответ
  `{accepted,revision}`. Экран стола присылает CSS-размер сцены при старте, `resize`,
  `orientationchange` и по `ResizeObserver` (первый layout может не дать `window.resize` —
  без observer сервер оставался с чужим размером).
- Тесты сервера: 17 (+2) — `/api/viewport` меняет `visibleRect` (320×320 при 1000×1000 и карте 320×200)
  и отклоняет нулевой размер. Важно: сервер не поддерживает chunked body, поэтому тест шлёт `StringContent`
  (`PostAsJsonAsync` использует chunked → 400 «Пустой размер вьюпорта»).

Проверка: сборка `DnDMap.sln` Debug/Release — 0 предупреждений; живой прогон в Chromium:
экран `?display=1` (779×878, шапка `display:none`) отправил размер, после `setMap` сервер отдал
`visibleRect` 800×901.67; панорама с планшета не сработала; в пульте зум 2× дал 400×444.56,
панорама ограничилась `centerX=600, centerY=377.72`.

Не проверено локально: сама сборка APK — нет JDK 17/Android SDK и workload для SDK 10.0.302.
Проверено в CI 2026-11-12: run 43171726784 (`Android APK` #1) — success, артефакт `DnDMap-android-1` (~6.1 МБ).

## 2026-11-12 — M4: APK в CI

- `.github/workflows/android.yml`: `workflow_dispatch` + push в **любую** ветку при изменениях
  `src/DnDMap.Android|Core|Server` (APK нужен и в рабочих ветках, а не только в `master`); JDK 17,
  `dotnet workload install android`, sdkmanager
  (`platforms;android-36`, `build-tools;36.0.0`), `dotnet build -c Debug`.
- Решение: собирать **Debug**, потому что он автоматически подписывается debug-ключом раннера и сразу
  ставится на планшет. Release без своего keystore даёт unsigned APK; собственная подпись — отдельный шаг,
  когда понадобится распространение.
- Артефакт `DnDMap-android-<run>` c `**/*-Signed.apk`; путь к локальной сборке и установке — в README.
- Android-проект намеренно не добавлен в `DnDMap.sln` (конфигурации `Debug|Release`), чтобы обычная
  сборка работала без Android-тулчейна.

## 2026-11-12 — исправление: APK падал при запуске (Fast Deployment)

Пользователь поставил первый APK на планшет и в BlueStacks — в обоих случаях мгновенный вылет.
Стектрейс снят через `adb logcat -b crash` из BlueStacks: `SIGABRT`, `Abort message: 'No assemblies
found in /data/user/0/ru.dndmap.host/files/.__override__/x86_64 ... Fast Deployment. Exiting...'`.

Причина не в коде приложения: Debug-сборка .NET Android использует Fast Deployment — managed-сборки
не пакуются в APK, их заливает `dotnet build -t:Run` по adb. APK, скачанный артефактом и
установленный вручную, поэтому нерабочий. Release этим не страдает (сборки вшиваются всегда).

Решение: `EmbedAssembliesIntoApk=true` в `src/DnDMap.Android/DnDMap.Android.csproj` (Debug остаётся
debug-подписанным, но становится самодостаточным) + шаг CI `Verify APK is self-contained`, который
проверяет наличие managed-сборок в APK, чтобы регрессия ловилась до установки на планшет.
Формат упаковки оказался неочевидным: в .NET 10 Debug каждая сборка лежит как
`lib/<abi>/lib_<Assembly>.dll.so` (а не в `assemblies/` и не в assembly store). Первая версия
проверки искала `assemblies/`, вторая — assembly store; обе дали ложное падение, хотя APK был
самодостаточным (42 МБ вместо 6 МБ fast-deployment). Итоговая проверка ищет
`lib_DnDMap.Android.dll.so` и альтернативные форматы.
Диагностика BlueStacks (`HD-Adb` 1.0.36, `error: closed`) описана в [troubleshooting.md](troubleshooting.md).
