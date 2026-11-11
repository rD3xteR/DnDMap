# Тема: окна, ввод и рендеринг

Обновлено: 2026-11-11 (M1 — WPF переведён на ядро; сенсорный ввод удалён).

## Окно «Управление» (SettingsWindow)

- Файл: `DnDMap/SettingsWindow/SettingsWindow.xaml` (+ `.xaml.cs`), VM — `SettingsWindow/ViewModel/*`.
- Сетка 2 колонки:
  - сверху на всю ширину — **«Выбор карты»**: кнопка «Обзор...», текущий путь, список файлов-карт, кнопка «Открыть карту»;
  - слева — **«Параметры круга»**: слайдер диаметра (20–200) + ComboBox размера существа (сборка через `{ext:EnumBindingSource {x:Type enums:EntitySize}}`, `EntitySize` — из ядра);
  - справа — **«Галерея изображений»**: «Обзор...», путь, список картинок-токенов;
  - снизу — кнопка **«Добавить токен»** (`IsDefault` → работает по `Enter`).
- Обзор папок — `System.Windows.Forms.FolderBrowserDialog` (поэтому в csproj включён `UseWindowsForms`).
- Списки — только **имена файлов** (`ImageItem.Name`), не превью картинок.
- Пути запоминаются при закрытии окна (`Window_Closing` → `vm.OnClosing()` → `Paths.Default.Save()`).
- Слайдер диаметра и `AddToken` идут не в статику, а в единственный `MapViewModel`
  (`SettingsWindow/ViewModel/Properties.cs:44`): `DiameterToChange` → `MapViewModel.SetDiameter`,
  «Добавить токен» → `MapViewModel.AddToken(path, EntitySize)`.
- Расширения картинок — `PhysicalFileStore.SupportedExtensions` из ядра, не локальный список.

## Окно карты (MapWindow)

- Файл: `DnDMap/MapWindow/MapWindow.xaml` (+ `.xaml.cs`), VM — `MapWindow/ViewModel/*`.
- `WindowStyle="None"`, `WindowState="Maximized"` → безрамочное полноэкранное окно; закрытие — `Alt+F4` (обработчик `Closed` вызывает `vm.OnClosing()`).
- Структура: `Grid` (чёрный) → `Border ClipToBounds` → `Canvas x:Name="MainCanvas"` **без `RenderTransform`**:
  - карта — `Image` с `Source={Binding MapImageSource}`, `Stretch="Fill"`, размер `MapWidth/MapHeight`,
    позиция `Canvas.Left/Top = MapLeft/MapTop`; размеры и позиция считаются ядром (`ViewGeometry.MapRectToScreen`);
  - токены — `ItemsControl` (панель `Canvas`), `TokenViewModel.Left/Top/Diameter` уже в **пикселях окна**.
- Поверх карты — `StackPanel` слева: вертикальный `ZoomSlider` (1–3, шаг 0.05, `Mode=OneWay`) и кнопка `Reset`.

## Ввод (всё в `MapWindow.xaml.cs`)

Координаты мыши/жеста — сразу **пиксели окна**: `e.GetPosition(MainCanvas)` без трансформаций.
Перевод в пиксели карты делает `MapViewModel` через `ViewGeometry.ScreenToMap`.

Мышь:

| Событие | Поведение | Метод VM |
| --- | --- | --- |
| `MouseLeftButtonDown` | hit-test по токену: попал → перетаскивание, не попал → панорама. Захват мыши в обоих случаях | `HitTest`, `BeginTokenDrag` |
| `MouseMove` | тянем токен или двигаем кадр на экранную дельту | `DragToken`, `PanByScreen` |
| `MouseLeftButtonUp` | конец драга, снятие захвата мыши | `EndTokenDrag` |
| `MouseWheel` + `Ctrl` | зум под курсором, шаг ±0.1 | `ZoomAt(point, zoom ± 0.1)` |
| `ZoomSlider_ValueChanged` | зум к центру (игнорируется во время перетаскивания/панорамы) | `ZoomTo(value)` |
| `ResetButton_Click` | `ResetViewCommand` (1×, по центру) | `ResetView` |
| `KeyDown` (`Delete`) | удаление выделенного токена | `RemoveSelectedToken` |
| `SizeChanged` / `Loaded` | VM узнаёт живой размер холста | `SetViewportSize` |

- Hit-test и так делает ядро-совместимый расчёт: обход токенов сверху вниз, расстояние до центра
  в пикселях окна (`MapViewModel.HitTest`), без `InputHitTest` по визуальному дереву.
- **ВАЖНО:** `e.GetPosition(MainCanvas)` возвращает пиксели окна, потому что у `Canvas` больше нет
  `RenderTransform`. Нельзя снова делить на зум/вычитать offset в code-behind — это делает `ScreenToMap`.
- **Сенсорный ввод удалён 2026-11-11** по просьбе пользователя: обработчики `StylusDown`/`Manipulation*`,
  `IsManipulationEnabled` и поля мультитача убраны из `MapWindow.xaml`/`.xaml.cs`. Осталось только управление мышью.

## Токен

- Ядро: `DnDMap.Core.Model.MapToken` — `Id (string)`, `File`, `EntitySize`, `X/Y` (**центр в px карты**), `Size` (диаметр в px карты).
- Представление: `MapWindow/ViewModel/TokenViewModel` — `Id`, `File`, `ImagePath` (абсолютный путь, только WPF),
  `Left`, `Top`, `Diameter`, `IsSelected` — всё в пикселях окна.
- Отображение (`MapWindow.xaml`): три концентрических `Ellipse`
  1. белая подложка (видна, если картинка с прозрачностью/не загрузилась);
  2. `ImageBrush` с картинкой токена (`Stretch="Fill"`, обводка чёрная 3px);
  3. оверлей выделения `IndianRed`, `Opacity=0.5`, виден через `BooleanToVisibilityConverter` по `IsSelected`.
- Логика — `MapWindow/ViewModel/Tokens.cs`: команды в `MapSession`, кэш `tokenId → абсолютный путь к картинке`,
  синхронизация `Tokens` из `Snapshot()`.
- Диаметр всех токенов меняется одновременно: `SetDiameterCommand` в сессии пересчитывает `MapToken.Size`.

## Зум, панорама и диаметр (`MapWindow/ViewModel/ZoomPan.cs`)

- Вся математика — `DnDMap.Core.Geometry.ViewGeometry`: `FitScale`, `MapToScreen`/`ScreenToMap`,
  `MapRectToScreen`, `ZoomAtPoint`, `PanBy`, `ClampCenter`, `VisibleRect`.
- `SetViewportSize(w,h)` → `MapSession.SetViewport`: кадр пересчитывается и центр прижимается к границам.
- `ZoomAt(point, zoom)` — точка под курсором/пальцем остаётся на месте **при любом зуме** (в старом коде — только при 1×).
- `ZoomTo(zoom)` — то же к центру вьюпорта (слайдер).
- `PanByScreen(dx,dy)` — сдвиг кадра ровно на экранные пиксели.
- `SetDiameter(refPx)` — клампится в 20–200 px эталонного экрана 1920×1080; в сессии переводится в px карты.
- Кламп зума (`1..3`) и границы панорамы живут в `SessionOptions`/`ViewGeometry`, а не в WPF-свойствах.

## Схема координат (важно при доработках)

```
Экран (px окна)  ⇄  ViewGeometry.MapToScreen / ScreenToMap  ⇄  карта (px картинки)
Токены в ядре хранятся в px карты (X/Y — центр).
На экран они попадают уже пересчитанными: Left = centerX - Diameter/2.
Диаметр: в сессии — px карты; API/слайдер — px эталонного экрана 1920×1080.
Следствие: токен масштабируется вместе с картой (намеренное отличие от старого WPF).
```
