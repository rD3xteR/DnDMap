# Архитектура DnDMap

Обновлено: 2026-10-26. Код ссылается на файлы в рабочей копии.

## Общая схема

Один WPF-проект (`DnDMap/DnDMap.csproj`, `net10.0-windows7.0`), MVVM без фреймворков:
свой `ViewModelBase` (`Core/BaseClasses/ViewModelBase.cs`) и свой `RelayCommand` (`Core/Command/RelayCommand.cs`).
DI-контейнера нет — все связи создаются вручную.

```
App.xaml (StartupUri = SettingsWindow)
   └── SettingsViewModel  «Управление»  (SettingsWindow/)
         ├── владеет единственным экземпляром MapViewModel  (Properties.cs:44)
         ├── Paths.Default  →  сохранение путей при закрытии окна (Methods.cs:15-21)
         └── MapViewModel.OpenWindow(path)
               └── MapWindow  (безрамочное окно на весь экран, DataContext = этот MapViewModel)
                     ├── Token  (TokenModel/) — коллекция Tokens
                     ├── GlobalSettings (static) — диаметр, событие TokenDiameterChanged
                     └── ZoomPan.cs — зум/панорама с ограничением границ
```

## Точки входа

- `DnDMap/App.xaml:6` — `StartupUri="SettingsWindow/SettingsWindow.xaml"`, стартовое окно «Управление».
- `DnDMap/App.xaml.cs:10` — `ShutdownMode = OnMainWindowClose`: закрытие стартового окна завершает всё приложение (включая окно карты).
- Окно карты создаётся кодом: `MapWindow/ViewModel/MainViewModel.cs:14-23` (`OpenWindow`).

## Владение окнами и ViewModel

- `SettingsViewModel` создаёт **один** `MapViewModel` (`SettingsWindow/ViewModel/Properties.cs:44`) и передаёт его в `MapViewModel.OpenWindow` как `DataContext` нового `MapWindow` (`MainViewModel.cs:16-17`).
- В `MapWindow.xaml:16-18` тоже объявлен `DataContext` (`<vm:MapViewModel/>`) — он перезаписывается в `OpenWindow`, фактически это мёртвый экземпляр.
- Окно карты не становится `MainWindow`, поэтому правило «закрыл главное окно — приложение завершилось» относится к «Управлению».

## Потоки данных

1. **Выбор папок** — `SettingsViewModel.BrowseMaps/BrowseTokens` (`Methods.cs:23-49`) → `FolderBrowserDialog` (WinForms, проект подключает `UseWindowsForms=true`) → `GetImages` читает файлы с расширениями из `GlobalSettings.SupportedExtensions` (`.png/.jpg/.jpeg`) → `ObservableCollection<ImageItem>`.
2. **Открытие карты** — `OpenMap` (`Methods.cs:66-71`) → `MapViewModel.OpenWindow(path)` → `MapPath` (привязка к `ImageBrush` фона canvas) + `SetMapSize` по пиксельным размерам картинки (`MainViewModel.cs:21-22`).
3. **Добавление токена** — `AddToken` (`Methods.cs:59-64`) → `TokenParams(EntitySize, path)` → `MapViewModel.AddToken` (`Tokens.cs:9-12`) → `new Token(...)`. **Координаты не задаются** — токен появляется в (0,0) (см. issue #4 и troubleshooting).
4. **Перемещение токена** — `MapWindow.xaml.cs` (mouse-хендлеры) → инверсия `RenderTransform` (`TransformToMap`, строки 38-50) → `SetSelectedToken` / `MoveSelectedToken` (`Tokens.cs`) → свойства `Token.X/Y` → привязки `Canvas.Left/Top`.
5. **Смена размера токенов** — слайдер «Диаметр» → `SettingsViewModel.DiameterToChange` → static `GlobalSettings.TokenDiameter` → событие `TokenDiameterChanged` → `MapViewModel.OnGlobalTokenSizeChanged` → `Token.Diameter` (=`TokenDiameter × (int)EntitySize`) (`Tokens.cs:35-41`, `Token.Model.cs:31`).

## Рендеринг и координаты

- Карта — `Canvas.Background` c `ImageBrush Stretch="Uniform"` (`MapWindow.xaml:67-69`); токены — `ItemsControl` с панелью `Canvas`, позиционирование через `Canvas.Left/Top` (`MapWindow.xaml:38-65`).
- Трансформация холста: `ScaleTransform(Zoom)` + `TranslateTransform(OffsetX/OffsetY)` (`MapWindow.xaml:31-36`).
- Ограничения: `Zoom` в свойстве зажат в `[1..3]` (`Properties.cs:15`), панорама не даёт увести карту за границы (`ZoomPan.cs:RecalcOffset/Pan`).
- Токен рисуется тремя `Ellipse`: подложка (белая), картинка, полупрозрачный оверлей выделения (`MapWindow.xaml:47-55`).

## Границы модулей

| Модуль | Зависит от | Назначение |
| --- | --- | --- |
| `Core/` | WPF (только `MarkupExtension`, `ICommand`) | базовые классы, настройки, перечисления, конвертеры |
| `TokenModel/` | `Core/` | модель токена (`Token.Model.cs`) и её поведение (`Token.Behavior.cs`) |
| `Models/` | `Core/` | DTO для списков: `ImageItem`, `EntitySizeItem` |
| `MapWindow/` | `TokenModel/`, `Core/` | окно карты + `MapViewModel` (partial: `MainViewModel`, `Properties`, `Tokens`, `ZoomPan`) |
| `SettingsWindow/` | `MapWindow.ViewModel`, `Core/`, `Models/` | окно «Управление» + `SettingsViewModel` (partial: `SettingsViewModel`, `Properties`, `Methods`) |

Обратите внимание на связь **SettingsWindow → MapViewModel**: окно настроек создаёт и владеет VM окна карты — это главная точка связанности при рефакторинге.

## Персистентность

- Пути к папкам карт/токенов: пользовательские настройки .NET (`Paths.settings` → `Paths.Designer.cs`, секция `DnDMap.Paths` в `App.config`), сохраняются в `SettingsViewModel.OnClosing` (`Methods.cs:15-21`).
- Токены, открытая карта, масштаб — **не сохраняются**; при закрытии окна карты сбрасываются (`MainViewModel.cs:25-31`).
- Эти три файла в текущей рабочей копии не отслеживаются git (см. troubleshooting).
