# Архитектура DnDMap

Обновлено: 2026-11-12 (M3: Android-хост; M2: HTTP-сервер и веб-пульт). Код ссылается на файлы в рабочей копии.

## Общая схема

Пять рабочих проектов: WPF-оболочка, ядро, HTTP-сервер, веб-пульт (статика внутри сервера) и Android-хост.
UI — MVVM без фреймворков: свой `ViewModelBase` и `RelayCommand` остались в WPF-проекте как UI-обвязка,
состояние игры — в ядре.

```
App.xaml (StartupUri = SettingsWindow)
   └── SettingsViewModel  «Управление»  (SettingsWindow/)
         ├── владеет единственным экземпляром MapViewModel  (Properties.cs)
         ├── Paths.Default  →  сохранение путей при закрытии окна (Methods.cs)
         ├── группа «Сервер для планшета» → MapServer (DnDMap.Server)
         │     ├── MapSession (своя, отдельная от WPF-окна)
         │     ├── PhysicalFileStore → %LOCALAPPDATA%\DnDMap\session-cache
         │     └── SPA из EmbeddedResource: браузер мастера
         └── MapViewModel.OpenWindow(path)
               ├── MapSession  (DnDMap.Core.Model) — карта, токены, кадр, диаметр
               ├── MapWindow  (безрамочное окно на весь экран, DataContext = этот MapViewModel)
               │     ├── MapViewModel.HitTest / BeginTokenDrag / DragToken …  → команды в сессию
               │     ├── TokenViewModel (экранные координаты)  → коллекция Tokens
               │     └── ZoomPan.cs — зум/панорама через ViewGeometry
               └── ImageSource карты — грузится в память (BitmapCacheOption.OnLoad)
```

Браузер и WPF-окно карты **не разделяют сессию** (M2): у сервера свой `MapSession`, у окна карты свой.
Это зафиксированный долг — сведение в одну сессию отдельной задачей.

Android-хост (M3) использует тот же `MapServer`, но вместо WPF-окна открывает SPA в WebView:

```
MainActivity (src/DnDMap.Android)
   ├── FilesDir/session-cache  (очистка при старте и выходе)
   ├── MapServer (порт 5000, 0.0.0.0; при занятом порте — свободный)
   └── WebView → http://127.0.0.1:<port>/?display=1
         └── SPA без шапки/панели; по ResizeObserver шлёт POST /api/viewport
```

`/api/viewport` задаёт `MapSession` живой CSS-размер экрана стола. Пульт мастера открывается с ноутбука
по адресу из Toast (`http://<ip-планшета>:<port>/`) и управляет той же сессией.

## Точки входа

- `DnDMap/App.xaml:6` — `StartupUri="SettingsWindow/SettingsWindow.xaml"`, стартовое окно «Управление».
- `DnDMap/App.xaml.cs:10` — `ShutdownMode = OnMainWindowClose`: закрытие стартового окна завершает всё приложение.
- Окно карты создаётся кодом: `MapWindow/ViewModel/MainViewModel.cs` (`OpenWindow`).

## Владение окнами и ViewModel

- `SettingsViewModel` создаёт **один** `MapViewModel` (`SettingsWindow/ViewModel/Properties.cs:44`) и передаёт его
  в `MapWindow` как `DataContext`. `MapWindow.xaml` больше не объявляет свой `DataContext` — мёртвого экземпляра нет.
- Окно карты не становится `MainWindow`: «закрыл главное окно — приложение завершилось» относится к «Управлению».
- Закрытие окна карты (`Closed` → `vm.OnClosing`) сбрасывает сессию: создаётся новый `MapSession`,
  очищаются токены, пути картинок и открытая карта.

## Потоки данных

1. **Выбор папок** — `SettingsViewModel.BrowseMaps/BrowseTokens` → `FolderBrowserDialog`
   (WinForms, `UseWindowsForms=true`) → `GetImages` фильтрует по `PhysicalFileStore.SupportedExtensions` (`.png/.jpg/.jpeg`)
   → `ObservableCollection<ImageItem>`.
2. **Открытие карты** — `SettingsViewModel.OpenMap` → `MapViewModel.OpenWindow(path)` → `OpenMap`:
   `LoadImage` (в память, файл не блокируется) → `SetMapCommand(name, w, h)` → `MapImageSource`
   → экранная раскладка из `ViewGeometry.MapRectToScreen`.
3. **Добавление токена** — `MapViewModel.AddToken(path, EntitySize)` → `AddTokenCommand(file, size)`
   без координат → ядро ставит токен **в центр карты**. Абсолютный путь картинки запоминается в WPF-кэше
   `tokenId → path`. До открытия карты запросы копятся в `_pendingTokens` и применяются после `SetMap`.
4. **Перемещение токена** — mouse/манипуляция в пикселях окна → `ScreenToMap` → `MoveTokenCommand` (px карты).
5. **Смена размера токенов** — слайдер «Диаметр» → `SettingsViewModel.DiameterToChange` → `MapViewModel.SetDiameter`
   (px эталонного экрана 1920×1080) → `SetDiameterCommand` → `MapToken.Size` в px карты.
6. **Веб-пульт** — браузер шлёт `POST /api/commands`, сервер применяет команду к своей `MapSession`,
   `Changed` → SSE-событие `state` с полным `SessionDto` (токены в px карты, `visibleRect`).
   Экран стола в режиме `?display=1` шлёт `POST /api/viewport` со своим CSS-размером (при старте и resize),
   чтобы fit/видимая область соответствовали планшету.
   Файлы: браузер загружает `PUT /api/files/{kind}/{name}` (сырое тело), читает манифест
   `GET /api/files?kind=…` и досылает недостающее из IndexedDB; картинки — `GET /files/{kind}/{name}`.
7. **Запуск сервера** — `SettingsViewModel.StartServer`: очистка кэша → `MapServer.Start()` → URL
   `http://<локальный IPv4>:<port>/`. `OnClosing` → `StopServer` (`Dispose`).

## Рендеринг и координаты

- Рендер — **в экранных координатах**, без `RenderTransform` (`MapWindow.xaml`):
  - карта — `Image` с `Canvas.Left/Top = MapLeft/MapTop` и `Width/Height = MapWidth/MapHeight`
    из `ViewGeometry.MapRectToScreen`;
  - токены — `ItemsControl` с панелью `Canvas`, `TokenViewModel.Left/Top/Diameter` уже в px окна.
- Общая модель: токены в ядре хранятся в **пикселях карты**, кадр — центр в px карты + зум относительно fit.
  WPF-мост только переводит кадр в экранные координаты и обратно — та же математика, что будет у веб-клиента.
- Токен рисуется тремя `Ellipse`: подложка (белая), картинка, полупрозрачный оверлей выделения.
- Ограничения кадра (зум 1..3, границы панорамы) — в ядре (`SessionOptions`, `ViewGeometry`), не в WPF-свойствах.

## Границы модулей

| Модуль | Зависит от | Назначение |
| --- | --- | --- |
| `src/DnDMap.Core` | только BCL | геометрия кадра, сессия, команды, хранилище, размеры изображений |
| `src/DnDMap.Server` | `DnDMap.Core`, BCL (`TcpListener`) | HTTP/1.1, REST, SSE, манифест и загрузка файлов, SPA из ресурсов |
| `src/DnDMap.Android` | `DnDMap.Core`, `DnDMap.Server`, Android SDK | Activity + WebView, кэш `FilesDir`, keep-screen-on |
| `DnDMap/Core/` | WPF (`ICommand`, `MarkupExtension`) | `ViewModelBase`, `RelayCommand`, конвертеры, расширения разметки |
| `DnDMap/Models/` | — | DTO списков: `ImageItem` |
| `DnDMap/MapWindow/` | `DnDMap.Core`, `Core/` | окно карты + `MapViewModel` (partial: `MainViewModel`, `Properties`, `Tokens`, `ZoomPan`) + `TokenViewModel` |
| `DnDMap/SettingsWindow/` | `MapWindow.ViewModel`, `DnDMap.Server`, `DnDMap.Core`, `Models/` | окно «Управление», `SettingsViewModel` (+ запуск сервера) |

Удалены как дубли ядра: `Core/GlobalSettings.cs`, `Core/Enums/EntitySize.cs`, `Core/Parameters/TokenParams.cs`,
`TokenModel/*`, `Models/EntitySizeItem.cs`.

Связь **SettingsWindow → MapViewModel** осталась: окно настроек создаёт и владеет VM окна карты.
Это главная точка связанности при дальнейшем рефакторинге (в M2 на её месте появится клиент протокола).

## Персистентность

- Пути к папкам карт/токенов: пользовательские настройки .NET (`Paths.settings` → `Paths.Designer.cs`,
  секция `DnDMap.Paths` в `App.config`), сохраняются в `SettingsViewModel.OnClosing`.
- Токены, открытая карта, масштаб — **не сохраняются**; при закрытии окна карты сбрасываются.
- Эти три файла в текущей рабочей копии не отслеживаются git (см. troubleshooting).

## Слой ядра и сервера

| Проект | TFM | Назначение | Состояние |
| --- | --- | --- | --- |
| `src/DnDMap.Core` | net10.0 | геометрия кадра, модель сессии, DTO, хранилище, размеры изображений | готово, покрыто тестами |
| `src/DnDMap.Server` | net10.0 | HTTP-сервер на BCL (`TcpListener`), SSE, SPA в ресурсах | готово (M2), покрыто тестами |
| `src/DnDMap.Android` | net10.0-android | экран стола + хостинг сервера | готово (M3), локально не собиралось без SDK/JDK 17 |
| `src/DnDMap.Core.Tests` | net10.0 | xUnit-тесты ядра | 82 теста, все зелёные |
| `src/DnDMap.Server.Tests` | net10.0 | xUnit-тесты API/SSE/загрузки на живом сервере | 17 тестов, все зелёные |

Поток данных после M2:

```
MapSession (Core)  ←  Command (WPF-мост и POST /api/commands)
      │ Changed(SessionChange)
      ├── WPF-мост: Snapshot() → экранные координаты (MapLeft/MapTop, TokenViewModel)
      └── SSE-рассылка: все браузеры получают полный SessionDto (visibleRect + токены)
```

Ключевые типы: `MapSession` (`Model/MapSession.cs`), `ViewGeometry` (`Geometry/ViewGeometry.cs`),
`SessionSnapshot`, `Command`/`SessionDto` (`Protocol/`), `MapServer` (`src/DnDMap.Server/MapServer.cs`).
Подробности — [topics/core-and-web.md](topics/core-and-web.md).

## Файлы карт и токенов (уточнено 2026-11-10)

Источник правды — **ноутбук мастера**, планшет — приёмник и временный кэш.

```
Браузер мастера                                 Планшет (хост)
┌──────────────────────────┐                  ┌──────────────────────────────┐
│ file picker / drag&drop  │  PUT /api/files  │ FilesDir/session-cache/      │
│ IndexedDB: выбранные     │ ───────────────► │   maps/  tokens/             │
│ файлы и их хэши          │ ◄─────────────── │ очистка при старте/выходе    │
│ манифест: чего не хватает│ GET /api/files   │ MapSession + экран стола     │
└──────────────────────────┘                  └──────────────────────────────┘
```

- На планшете **нет библиотеки и UI управления файлами**: только кэш текущей сессии.
- Перед `setMap`/`addToken` сервер проверяет наличие файла в кэше — иначе в состоянии появилась бы битая ссылка.
- `MapSession` о файловой системе не знает; проверка — на уровне сервера (M2, `MapServer.HandleCommandAsync`).
- WPF по-прежнему работает с обычными папками пользователя (`Paths.Default.MapsPath/TokensPath`).
- Имена файлов с ноутбука (пробелы, кириллица) проходят: `ValidateName` ослаблен в M2
  (запрещены только разделители, управляющие символы, `CON`/`NUL`…, завершающая точка/пробел).
