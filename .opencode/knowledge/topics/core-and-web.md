# Тема: ядро и веб-хост

Обновлено: 2026-11-14. Статус: M0–M3 завершены (ядро, WPF, HTTP-сервер и веб-пульт, Android-хост); M4 — workflow APK готов, сборка ждёт Android-тулчейн/CI. Добавлены блокировка планшета, пустая карта-сетка и наброски.

## Зачем это

Целевая схема (согласована 2026-11-10):

```
┌──────────────────────────┐        локальная сеть         ┌──────────────────┐
│ Android-планшет (хост)   │  HTTP/SSE  ◄───────────────►  │ ноутбук мастера  │
│ экран стола + MapSession │                               │ браузер, SPA     │
└──────────────────────────┘                               └──────────────────┘
```

- Планшет показывает карту во весь экран и **сам поднимает HTTP-сервер** (BCL `TcpListener`, без ASP.NET Core).
- Ноутбук открывает браузер, **передаёт карты и токены** на планшет (выбор файлов/папки или drag&drop),
  видит живое зеркало и перетаскивает токены.
- Постоянного хранения изображений на планшете нет: там только кэш текущей сессии (см. «Хранилище»).
- Интернет не нужен: статика (HTML/CSS/JS) лежит внутри приложения, внешних CDN нет.
- Windows WPF-приложение остаётся рабочим вариантом хоста.

## Раскладка проектов

| Проект | TFM | Роль |
| --- | --- | --- |
| `src/DnDMap.Core` | `net10.0` | геометрия, модель сессии, хранилище, протокол — без UI |
| `src/DnDMap.Server` | `net10.0` | HTTP-сервер: REST + SSE + SPA в ресурсах (M2) |
| `src/DnDMap.Core.Tests` | `net10.0` | xUnit-тесты ядра + legacy-parity |
| `src/DnDMap.Server.Tests` | `net10.0` | xUnit-тесты API/SSE/загрузки на живом сервере |
| `DnDMap` (WPF) | `net10.0-windows7.0` | UI поверх `DnDMap.Core` (M1): мост «сессия ↔ экран» |
| Android (M3) | `net10.0-android` | экран стола + хостинг сервера |

Namespace'ы ядра — `DnDMap.Core.{Geometry,Model,Storage,Imaging,Protocol}` (`RootNamespace = DnDMap.Core`).

## Система координат

Главное решение: **всё хранится в пикселях карты**, а не в экранных.

| Величина | Где живёт | Смысл |
| --- | --- | --- |
| `MapPoint` / `RectD` | `Geometry` | пиксели исходного изображения карты |
| `ViewState(Center, Zoom)` | `Geometry`, в `MapSession` | центр вида в px карты + зум относительно fit |
| `ViewportWidth/Height` | `MapSession.SetViewport` | живой размер экрана/окна/WebView |
| `FitScale` | `ViewGeometry.FitScale` | `min(vw/mw, vh/mh)` — вписывание карты |
| `VisibleRect` | `ViewGeometry.VisibleRect` | что видно, в px карты: `viewport / (fit × zoom)` |

Клиенты (WPF, браузер) **не дублируют** fit/clamp/зум: хост присылает `visibleRect`, клиент только
растягивает его на свой canvas. Это гарантирует одинаковый кадр на столе и в пульте.

Пересчёт — только через `ViewGeometry.MapToScreen` / `ScreenToMap` / `MapRectToScreen`.

Намеренные отличия от старого WPF-поведения:

1. **Диаметр токена — в px карты.** Слайдер и API по-прежнему говорят в px эталонного экрана
   (`SessionOptions.ReferenceViewport = 1920×1080`, 100 по умолчанию, 20–200). Внутри:
   `mapPx = refPx / fitRef`. Следствие: при зуме токен масштабируется вместе с картой.
2. **`ZoomAtPoint` удерживает точку под курсором всегда** (старый код — только при zoom=1).

Оба отличия зафиксированы parity-тестами и в [decisions.md](../decisions.md).

## Модель сессии

`MapSession` (`src/DnDMap.Core/Model/MapSession.cs`) — единственное изменяемое состояние:

- `Map`, `Tokens`, `View`, `ViewportWidth/Height`, `BaseDiameterMapPx`;
- `Apply(Command)` — все изменения через команды; `false` = команда отклонена, состояние не изменилось;
- `SetViewport(w, h)` — хост сообщает размер; кадр пересчитывается и ограничивается;
- `Revision` — растёт на каждое принятое изменение;
- `Changed(SessionChange)` — событие вне lock, по нему сервер рассылает SSE;
- `Snapshot()` — согласованный снимок всех полей под одним lock.

Потокобезопасность: один `lock` на всю сессию, событие поднимается **после** выхода из lock
(подписчик не может устроить deadlock и не блокирует запись).

### Команды (`Protocol/Commands.cs`)

JSON-дискриминатор `type` (`JsonPolymorphic`), обязательные поля помечены `[JsonRequired]`:

| `type` | Поля | Что делает |
| --- | --- | --- |
| `setMap` | `name`, `width`, `height` | открыть карту, сбросить кадр, пересчитать диаметр, прижать токены в границы |
| `addToken` | `file`, `entitySize?`, `x?`, `y?` | добавить токен; без координат — **в центр карты** (issue #4), явные координаты клампятся в границы карты |
| `moveToken` | `tokenId`, `x`, `y` | переместить (px карты); центр клампится в `[0,width]×[0,height]` |
| `removeToken` | `tokenId` | удалить |
| `setDiameter` | `referencePx` | базовый диаметр в px эталона, клампится в 20–200 |
| `setView` | `centerX`, `centerY`, `zoom` | задать кадр, зум клампится в 1..3 |
| `resetView` | — | 1×, по центру |
| `addSketchStroke` | `color`, `widthMapPx`, `points[]` | штрих наброска в px карты; цвет `#rrggbb` или `"eraser"` |
| `undoSketch` | — | убрать последний штрих (409, если пусто) |
| `clearSketch` | — | очистить все наброски (409, если пусто) |
| `setTabletLock` | `locked` | запретить ввод с планшета (панорама, зум, токены) |

`SessionChangeKind` (`Map`, `Tokens`, `View`, `Diameter`, `Viewport`, `Sketch`, `InputLock`) — чтобы сервер не гонял полное состояние
на каждую мелочь, а клиент понимал, что перерисовать.

Лимиты набросков в `MapSession`: 200 штрихов, 300 точек на штрих, 20 000 точек суммарно,
толщина клампится в 1..200 px карты, точки — в границы карты. При смене карты наброски очищаются.
Сериализуются отдельным `SketchDto` (не входят в `SessionDto`: они большие и меняются редко).

## Хранилище

`IFileStore` — `List/Exists/OpenRead/Write/Delete/ValidateName`.
`PhysicalFileStore` — папка `{root}/maps` и `{root}/tokens`:
имена ≤ 120 символов; разрешены пробелы и кириллица; запрещены разделители пути, управляющие символы,
`CON`/`NUL`/`COM1`…, завершающая точка/пробел; нормализация пути и проверка «внутри папки вида» (anti-traversal).
`Write` атомарный: временный `*.upload` → `File.Move(overwrite: true)` (оборванная загрузка не оставляет битую карту).

`ImageSizeReader` читает ширину/высоту PNG (IHDR) и JPEG (SOF) из первых 64 КБ, без `System.Drawing`.

### Где живут файлы (уточнено 2026-11-10)

Постоянного хранения изображений на планшете **нет**. Источник правды — ноутбук мастера:

| Сторона | Что держит | Время жизни |
| --- | --- | --- |
| Ноутбук, браузер | выбранные файлы + хэши в IndexedDB | постоянно, управляет мастер |
| Планшет, `FilesDir/session-cache/{maps,tokens}` | копию присланного | до выхода/старта приложения |
| WPF (текущий режим) | свои папки `maps/`, `tokens/` | как раньше |

`PhysicalFileStore` переиспользуется как кэш с корнем `FilesDir/session-cache` + очистка при старте.
`MapSession` о файловой системе не знает: проверка «файл есть в кэше» — на уровне сервера (M2),
иначе в состоянии появилась бы ссылка на отсутствующий файл.

Сделано в M2:

- `ValidateName` ослаблен под имена с ноутбука (пробелы, кириллица), см. «Хранилище».
- Лимит одного файла — 256 МБ (`ServerOptions.MaxUploadBytes`), общий объём кэша не ограничен:
  кэш живёт одну сессию и очищается при старте/выходе.
- Коллизии имён (`map.png` из разных папок) **не переименовываются**: загрузка перезаписывает файл.
  Браузер сверяет имя+размер, поэтому при одинаковом имени и другом содержимом возможна путаница —
  известный долг, см. [troubleshooting.md](../troubleshooting.md).

## Сервер M2 (`src/DnDMap.Server`)

`MapServer` (`MapServer.cs`) — HTTP/1.1 на `TcpListener`, одно соединение на запрос (`Connection: close`),
кроме SSE. `Port = 0` выбирает свободный порт (используется тестами). `Start()`, `StopAsync()`, `Dispose()`,
свойство `Log` для диагностики. Один `MapSession` на сервер, подписка на `Changed` → рассылка SSE.

| Маршрут | Ответ / поведение |
| --- | --- |
| `GET /`, `/app.css`, `/app.js` | SPA из `EmbeddedResource` (без файлов на диске и CDN) |
| `GET /api/session` | полный `SessionDto` |
| `POST /api/commands` | `Command` JSON → `{accepted,revision}`; 409, если команда отклонена |
| `POST /api/viewport` | `{width,height}` → `MapSession.SetViewport`; обновляет fit и `visibleRect` |
| `POST /api/maps/blank` | `{columns,rows}` → рисует PNG-сетку, кладёт в `maps/`, открывает как карту; 400 вне 10–30 × 5–20 |
| `GET /api/sketch` | `SketchDto` — все штрихи текущей карты |
| `GET /api/events` | SSE: `state` с полным `SessionDto` и отдельное событие `sketch` с `SketchDto`; ping каждые 15 с |
| `GET /api/files?kind=map\|token` | `{files:[{name,size,hash}]}`, SHA-256 из `FileHashCache` |
| `PUT /api/files/{kind}/{name}` | сырое тело (без multipart), 413 при превышении лимита, 415 для не-PNG/JPEG |
| `DELETE /api/files/{kind}/{name}` | 204 / 404, инвалидирует хэш |
| `GET /files/{kind}/{name}` | байты для `<img>` с правильным `Content-Type` |

Особенности:

- `setMap` игнорирует присланные клиентом размеры: сервер читает их из файла (`ImageSizeReader`).
- `setMap`/`addToken` требуют, чтобы файл уже был в кэше (409) — клиент сначала загружает, потом командует.
- Загрузка проверяет сигнатуру после записи: битый файл удаляется, ответ 415.
- SSE-очередь на клиента ограничена (16, `DropOldest`): медленный браузер не тормозит остальных.
- `ServerDtos.cs` — DTO сети: манифест, результат загрузки, результат команды, ошибка.

SPA (`wwwroot/app.js`):

- подписка `EventSource('/api/events')`: событие `state` → `applyState`, событие `sketch` → `state.sketch` + перерисовка canvas;
  на старте параллельно `GET /api/session` и `GET /api/sketch`;
- рендер как в WPF — экранные координаты из `visibleRect` (в него входит зум: `fit × zoom`), без `RenderTransform`;
- драг токена и панорама — pointer events с оптимистичным обновлением и throttle (30/40 мс);
  оба ограничиваются локально (`ClampAxis` в `app.js`) и повторно в ядре;
- зум — колесо, слайдер, кнопки «−/+»; `zoom-at-point` считается на клиенте из `visibleRect` + `view.zoom`;
- слайдер «Токены» 20–200 ref-px → `setDiameter` (throttle 60 мс), подпись синхронизируется из `baseDiameter`;
- файлы: две drop-зоны + `<input type=file multiple>`, IndexedDB хранит `{kind,name,size,blob}`,
  при старте сравнивает с манифестом и досылает недостающее (сейчас по имени+размеру);
- «Пустая карта»: числа клеток (клампятся в UI) → `POST /api/maps/blank`, затем `refreshManifest` и выбор имени в списке;
- наброски: canvas поверх `<img>`, ниже токенов. Штрих в px карты, координаты через `layout` (`offsetX/Y`, `scale`);
  ластик — `globalCompositeOperation = 'destination-out'`, поэтому карта не портится. Штрих добавляется в `state.sketch`
  оптимистично и заменяется серверным `sketch` по SSE;
- чекбокс «планшет: только показ» → `setTabletLock`; на планшете состояние приходит в `SessionDto.tabletInputLocked`,
  показывает бейдж `#lockBadge` и прячет кнопки зума; `pointerdown` при замке игнорируется;
- `?display=1` — экран стола на планшете: шапка и панель скрыты, ввод отключён (кроме разрешённых жестов),
  сцена сама сообщает размер через `POST /api/viewport` (при старте, `resize`, `orientationchange` и `ResizeObserver`).

Исправления 2026-11-12: `visibleRect` без множителя `zoom` (из-за него зум «не работал» в браузере),
кламп панорамы/драга на клиенте + кламп токена в ядре, слайдер размера токенов. Подробности — [decisions.md](../decisions.md).



**M1 — WPF на ядре (завершён 2026-11-11).** `MapViewModel` стал мостом над `MapSession`: состояние, команды
и геометрия — в ядре; WPF только переводит кадр в экранные координаты и обратно. Рендер переписан без
`RenderTransform`; ввод идёт через `ScreenToMap`. Дубли `EntitySize`/`Token`/`TokenParams`/`GlobalSettings`
удалены, `ViewModelBase`/`RelayCommand` оставлены как UI-обвязка. Подробности — [decisions.md](../decisions.md),
рендер и ввод — [ui-and-interaction.md](ui-and-interaction.md).

**M2 — сервер (завершён 2026-11-11).** `TcpListener`, маршруты из таблицы «Сервер M2», SSE с полным
`SessionDto`, статика из ресурсов, кэш `%LOCALAPPDATA%\DnDMap\session-cache` (очистка при старте и выходе),
запуск из окна «Управление». Проверено 26 тестами API + живым прогоном SPA (ядро — 92 теста).
Подробности — [decisions.md](../decisions.md).

Не сделано (долг M2): серверная сессия не связана с сессией WPF-окна карты; аутентификации нет
(доверенная LAN); IndexedDB сверяет имя+размер, а не хэш.

**M3 — Android (готово 2026-11-12).** `src/DnDMap.Android/DnDMap.Android.csproj` — `net10.0-android`,
без MAUI. `MainActivity` (`DnDMap.Host`): очистка `FilesDir/session-cache` → `MapServer` (порт 5000,
при занятом — `Port=0`) → WebView на `http://127.0.0.1:<port>/?display=1`, keep-screen-on,
поворот обрабатывается `ConfigurationChanges` без перезапуска сервера. По выходу сервер
останавливается в пуле потоков (синхронный `Dispose` на UI-потоке может заблокироваться),
кэш очищается. Адрес для мастера показывается Toast'ом. Тестовый режим SPA (`display=1`) скрывает
пульт и отключает ввод на планшете. Серверный эндпоинт размера — `POST /api/viewport`.
Проект **не** добавлен в конфигурации `Debug|Release` решения (ломает сборку без SDK).

**M4 — CI/упаковка (готово 2026-11-12).** `.github/workflows/android.yml`: `workflow_dispatch`
или push в любую ветку с изменениями Android/Core/Server → JDK 17 (`setup-java`), `dotnet workload install android`,
sdkmanager (`platforms;android-36`, `build-tools;36.0.0`), `dotnet build -c Debug`.
Debug-сборка подписывается автоматическим debug-ключом раннера → устанавливаемый APK;
артефакт `DnDMap-android-<run>` с `**/*-Signed.apk`. Release-подпись своим keystore — на будущее.

**Браузер (M2).** Выбор файлов и папки (`<input type=file>`, `webkitdirectory`) + drag&drop;
хранение выбранного в IndexedDB; при подключении — сравнение с манифестом и досылка недостающего.
File System Access API не используется: по `http://` в локальной сети это не secure context.

## Подводные камни

- Корневой `Directory.Build.props` **не добавлять** — изменит сборку существующего WPF-проекта.
- NuGet в прод-коде не использовать (автономность, Android): только BCL.
- Android SDK/JDK 17 на машине нет, а workload `android` установлен не для текущего SDK 10.0.302
  (`dotnet build` → NETSDK1147) — M3 локально не собирается, нужна установка тулчейна или CI.
- При запущенном WPF-приложении сборка даёт MSB3026 (файл занят) — это не ошибка.
