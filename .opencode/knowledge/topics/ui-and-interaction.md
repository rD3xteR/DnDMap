# Тема: окна, ввод и рендеринг

Обновлено: 2026-10-26.

## Окно «Управление» (SettingsWindow)

- Файл: `DnDMap/SettingsWindow/SettingsWindow.xaml` (+ `.xaml.cs`), VM — `SettingsWindow/ViewModel/*`.
- Сетка 2 колонки:
  - сверху на всю ширину — **«Выбор карты»**: кнопка «Обзор...», текущий путь, список файлов-карт, кнопка «Открыть карту»;
  - слева — **«Параметры круга»**: слайдер диаметра (20–200) + ComboBox размера существа (сборка через `{ext:EnumBindingSource {x:Type enums:EntitySize}}`);
  - справа — **«Галерея изображений»**: «Обзор...», путь, список картинок-токенов;
  - снизу — кнопка **«Добавить токен»** (`IsDefault` → работает по `Enter`).
- Обзор папок — `System.Windows.Forms.FolderBrowserDialog` (поэтому в csproj включён `UseWindowsForms`).
- Списки — только **имена файлов** (`ImageItem.Name`), не превью картинок.
- Пути запоминаются при закрытии окна (`Window_Closing` → `vm.OnClosing()` → `Paths.Default.Save()`).

## Окно карты (MapWindow)

- Файл: `DnDMap/MapWindow/MapWindow.xaml` (+ `.xaml.cs`), VM — `MapWindow/ViewModel/*`.
- `WindowStyle="None"`, `WindowState="Maximized"` → безрамочное полноэкранное окно; закрытие — `Alt+F4` (обработчик `Closed` вызывает `vm.OnClosing()`).
- Структура: `Grid` (чёрный) → `Border ClipToBounds` → `Canvas x:Name="MainCanvas"` с `RenderTransform = Scale(Zoom) + Translate(OffsetX, OffsetY)`.
  - фон canvas — `ImageBrush` с `MapPath`;
  - токены — `ItemsControl` (панель `Canvas`), позиция через `Canvas.Left/Top` в привязке к `Token.X/Y`.
- Поверх карты — `StackPanel` слева: вертикальный `ZoomSlider` (1–3, шаг 0.05) и кнопка `Reset`.

## Ввод (всё в `MapWindow.xaml.cs`)

Мышь:

| Событие | Поведение | Ключевые строки |
| --- | --- | --- |
| `MouseLeftButtonDown` | hit-test по токену: попал → начинаем перетаскивание токена (`SetSelectedToken`), не попал → панорама. Захват мыши в обоих случаях | 73-96 |
| `MouseMove` | тянем токен (`MoveSelectedToken`) или двигаем карту (`Pan`) | 99-119 |
| `MouseLeftButtonUp` | снятие захвата мыши | 122-136 |
| `MouseWheel` + `Ctrl` | зум под курсором, шаг ±0.1 (`ZoomAtPoint`) | 139-150 |
| `ZoomSlider_ValueChanged` | зум к центру (игнорируется во время перетаскивания/панорамы) | 153-161 |
| `ResetButton_Click` | `Zoom=1`, `OffsetX/Y=0` | 164-170 |
| `KeyDown` (`Delete`) | удаление выделенного токена | 173-181 |
| `SizeChanged` / `Loaded` | `UpdateViewportSize` → VM узнаёт фактический размер холста | 23-35 |

Сенсорный ввод (мультитач, `IsManipulationEnabled="True"` на MainCanvas):

| Событие | Поведение |
| --- | --- |
| `StylusDown` | `e.Handled = true` — подавляет промоцию тач-событий в мышь, чтобы жест не обрабатывался дважды |
| `ManipulationStarting` | `ManipulationContainer = MainCanvas` — координаты жеста в пространстве карты (с учётом RenderTransform) |
| `ManipulationStarted` | hit-test: попал на токен → режим перетаскивания токена, иначе панорама |
| `ManipulationDelta` | один палец → панорама (`Pan(translation × Zoom)` — перевод контентных единиц в экранные) или драг токена; два пальца → pinch-зум `ZoomAtPoint` к центру жеста (`scale` из `DeltaManipulation.Scale`). `IsInertial` игнорируется |
| `ManipulationCompleted` | сброс флага перетаскивания |
| `ManipulationBoundaryFeedback` | `e.Handled = true` — подавляет системный «отскок» |

- `IsHitOnToken` — поднимается по визуальному дереву от `InputHitTest` до `ContentPresenter`, чей `Content` является `Token`.
- **ВАЖНО:** `e.GetPosition(MainCanvas)` уже учитывает `RenderTransform` элемента и возвращает координаты в пространстве карты. Раньше здесь была функция `TransformToMap` (обратная инверсия) — она применяла инверсию **повторно** и ломала драг при зуме (см. troubleshooting); удалена 2026-10-26.

## Токен

- Модель: `TokenModel/Token.Model.cs` — `Id (Guid)`, `ImagePath`, `X`, `Y`, `EntitySize`, `IsSelected`, вычисляемый `Diameter = GlobalSettings.TokenDiameter × (int)EntitySize`.
- Поведение: `TokenModel/Token.Behavior.cs` — `GetOffset`, `Move`, `OnGlobalTokenSizeChanged`.
- Отображение (`MapWindow.xaml:44-57`): три концентрических `Ellipse`
  1. белая подложка (видна, если картинка с прозрачностью/не загрузилась);
  2. `ImageBrush` с картинкой токена (`Stretch="Fill"`, обводка чёрная 3px);
  3. оверлей выделения `IndianRed`, `Opacity=0.5`, виден через `BooleanToVisibilityConverter` по `IsSelected`.
- Логика добавления/выделения/перемещения — `MapWindow/ViewModel/Tokens.cs`.
- Размер всех токенов меняется одновременно через событие `GlobalSettings.TokenDiameterChanged`.

## Зум и панорама (`MapWindow/ViewModel/ZoomPan.cs`)

- `SetMapSize(w,h)` — из пикселей картинки; `SetViewportSize(w,h)` — из `MainCanvas.ActualWidth/Height`.
- `RecalcOffset` — держит карту в границах: если карта меньше viewport по оси — центрирует, иначе зажимает смещение в `[viewport - scaled, 0]`.
- `Pan(dx,dy)` — то же зажимание, возвращает фактически применённое смещение.
- `ZoomAtPoint(point, zoom)` — зум к точке: считает сдвиг `dx/dy` из отношения масштабов, затем снова зажимает смещение. Кламп `[1..3]` — синхронизирован со свойством `Zoom` 2026-10-26 (раньше был `[1..5]` и за верхней границей смещение считалось от «чужого» масштаба).

## Схема координат (важно при доработках)

```
Экран → (инверсия RenderTransform) → локальные координаты Canvas (0..viewport)
Токены хранятся в локальных координатах Canvas.
Ограничения панорамы считаются от _mapWidth/_mapHeight × Zoom (пиксели картинки).
⚠️ Это две разные величины при несовпадении размера картинки и окна — см. troubleshooting (⏳).
```
