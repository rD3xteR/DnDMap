using DnDMap.Core.Geometry;
using DnDMap.Core.Protocol;

namespace DnDMap.Core.Model;

/// <summary>
/// Состояние игры: карта, токены и кадр. Один экземпляр на хост (WPF или Android).
/// Все изменения идут через <see cref="Apply"/>, поэтому серверу и клиентам достаточно
/// подписаться на <see cref="Changed"/> и разослать событие.
/// </summary>
public sealed class MapSession
{
    /// <summary>Ограничения набросков: сессия целиком уходит клиентам по сети.</summary>
    private const int MaxSketchStrokes = 200;
    private const int MaxSketchPointsPerStroke = 300;
    private const int MaxSketchTotalPoints = 20000;
    private const double MaxSketchWidthMapPx = 200;

    private readonly object _gate = new();
    private readonly SessionOptions _options;
    private readonly List<MapToken> _tokens = [];
    private readonly List<SketchStroke> _sketch = [];

    private MapInfo? _map;
    private ViewState _view = new(MapPoint.Zero, ViewGeometry.MinZoom);
    private double _viewportWidth;
    private double _viewportHeight;
    private double _baseDiameterMapPx;
    private bool _tabletInputLocked;
    private long _revision;

    public MapSession(SessionOptions? options = null)
    {
        _options = options ?? new SessionOptions();
        _viewportWidth = _options.ReferenceViewportWidth;
        _viewportHeight = _options.ReferenceViewportHeight;
    }

    /// <summary>Изменение состояния. Вызывается вне внутреннего lock.</summary>
    public event Action<SessionChange>? Changed;

    public SessionOptions Options => _options;

    public long Revision
    {
        get { lock (_gate) return _revision; }
    }

    public MapInfo? Map
    {
        get { lock (_gate) return _map; }
    }

    public ViewState View
    {
        get { lock (_gate) return _view; }
    }

    public double ViewportWidth
    {
        get { lock (_gate) return _viewportWidth; }
    }

    public double ViewportHeight
    {
        get { lock (_gate) return _viewportHeight; }
    }

    public double BaseDiameterMapPx
    {
        get { lock (_gate) return _baseDiameterMapPx; }
    }

    public IReadOnlyList<MapToken> Tokens
    {
        get { lock (_gate) return _tokens.ToArray(); }
    }

    /// <summary>Планшету запрещено менять карту и токены (панорама, зум, перетаскивание).</summary>
    public bool TabletInputLocked
    {
        get { lock (_gate) return _tabletInputLocked; }
    }

    /// <summary>Наброски поверх текущей карты; очищаются при смене карты.</summary>
    public IReadOnlyList<SketchStroke> Sketch
    {
        get { lock (_gate) return _sketch.ToArray(); }
    }

    public ViewContext Context
    {
        get { lock (_gate) return ContextLocked(); }
    }

    public double FitScale
    {
        get { lock (_gate) return FitScaleLocked(); }
    }

    public RectD VisibleRect
    {
        get
        {
            lock (_gate)
                return ViewGeometry.VisibleRect(
                    _view, FitScaleLocked() * _view.Zoom, _viewportWidth, _viewportHeight);
        }
    }

    /// <summary>Согласованный снимок состояния под одним lock.</summary>
    public SessionSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new SessionSnapshot(
                _revision,
                _map,
                _view,
                _viewportWidth,
                _viewportHeight,
                _baseDiameterMapPx,
                MapPxToReferencePxLocked(_baseDiameterMapPx),
                _tokens.ToArray(),
                _tabletInputLocked,
                _sketch.ToArray());
        }
    }

    /// <summary>
    /// Хост сообщает живой размер экрана/окна/WebView. Размер влияет на кадр,
    /// поэтому подписчики получают событие. false — размер не изменился.
    /// </summary>
    public bool SetViewport(double width, double height)
    {
        if (width <= 0 || height <= 0)
            return false;

        SessionChange change;
        lock (_gate)
        {
            if (Math.Abs(_viewportWidth - width) < 1e-6 && Math.Abs(_viewportHeight - height) < 1e-6)
                return false;

            _viewportWidth = width;
            _viewportHeight = height;

            if (_map is not null)
            {
                var scale = FitScaleLocked() * _view.Zoom;
                _view = _view with { Center = ViewGeometry.ClampCenter(_view.Center, ContextLocked(), scale) };
            }

            _revision++;
            change = new SessionChange(_revision, SessionChangeKind.Viewport, null);
        }

        Changed?.Invoke(change);
        return true;
    }

    /// <summary>
    /// Применяет команду. false — команда отклонена (нет карты, неизвестный токен,
    /// некорректные размеры); состояние при этом не меняется.
    /// </summary>
    public bool Apply(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);

        SessionChange change;
        lock (_gate)
        {
            if (!ApplyLocked(command))
                return false;

            _revision++;
            change = new SessionChange(_revision, KindOf(command), command);
        }

        Changed?.Invoke(change);
        return true;
    }

    /// <summary>Диаметр в пикселях карты → эквивалент на эталонном экране.</summary>
    public double MapPxToReferencePx(double mapPx)
    {
        lock (_gate) return MapPxToReferencePxLocked(mapPx);
    }

    /// <summary>Диаметр в пикселях эталонного экрана → пиксели карты.</summary>
    public double ReferencePxToMapPx(double referencePx)
    {
        lock (_gate) return ReferencePxToMapPxLocked(referencePx);
    }

    // ----- внутреннее (всё под _gate) -----

    private bool ApplyLocked(Command command)
    {
        switch (command)
        {
            case SetMapCommand setMap:
                return ApplySetMapLocked(setMap);

            case AddTokenCommand addToken:
                return ApplyAddTokenLocked(addToken);

            case MoveTokenCommand moveToken:
            {
                if (_map is null)
                    return false;

                var token = FindTokenLocked(moveToken.TokenId);
                if (token is null)
                    return false;

                // Центр токена не выходит за карту: клиент не может утащить его в пустоту.
                token.X = Math.Clamp(moveToken.X, 0, _map.Width);
                token.Y = Math.Clamp(moveToken.Y, 0, _map.Height);
                return true;
            }

            case RemoveTokenCommand removeToken:
            {
                var token = FindTokenLocked(removeToken.TokenId);
                if (token is null)
                    return false;

                _tokens.Remove(token);
                return true;
            }

            case SetDiameterCommand setDiameter:
                return ApplySetDiameterLocked(setDiameter);

            case SetViewCommand setView:
                return ApplySetViewLocked(setView);

            case ResetViewCommand:
                return ApplyResetViewLocked();

            case AddSketchStrokeCommand addStroke:
                return ApplyAddSketchStrokeLocked(addStroke);

            case UndoSketchCommand:
                return ApplyUndoSketchLocked();

            case ClearSketchCommand:
                return ApplyClearSketchLocked();

            case SetTabletInputLockCommand setLock:
                _tabletInputLocked = setLock.Locked;
                return true;

            default:
                return false;
        }
    }

    private bool ApplySetMapLocked(SetMapCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name) || command.Width <= 0 || command.Height <= 0)
            return false;

        _map = new MapInfo(command.Name, command.Width, command.Height);
        _view = ViewGeometry.Reset(command.Width, command.Height);
        _baseDiameterMapPx = ReferencePxToMapPxLocked(_options.DefaultDiameterReferencePx);

        // Наброски относятся к конкретной карте: на новой карте они бессмысленны.
        _sketch.Clear();

        // Диаметр пересчитывается под новую карту, а позиции прижимаются к её границам,
        // чтобы при переходе на карту меньшего размера токены не оказались «за картой».
        foreach (var token in _tokens)
        {
            token.Size = _baseDiameterMapPx * (int)token.EntitySize;
            token.X = Math.Clamp(token.X, 0, command.Width);
            token.Y = Math.Clamp(token.Y, 0, command.Height);
        }

        return true;
    }

    private bool ApplyAddTokenLocked(AddTokenCommand command)
    {
        if (_map is null || string.IsNullOrWhiteSpace(command.File))
            return false;

        // Без явных координат токен появляется в центре карты, а не в (0,0) — issue #4.
        var x = Math.Clamp(command.X ?? _map.Width / 2.0, 0, _map.Width);
        var y = Math.Clamp(command.Y ?? _map.Height / 2.0, 0, _map.Height);

        _tokens.Add(new MapToken(
            Guid.NewGuid().ToString("N"),
            command.File,
            command.EntitySize,
            x,
            y)
        {
            Size = _baseDiameterMapPx * (int)command.EntitySize
        });

        return true;
    }

    private bool ApplySetDiameterLocked(SetDiameterCommand command)
    {
        if (_map is null)
            return false;

        var referencePx = Math.Clamp(
            command.ReferencePx,
            _options.MinDiameterReferencePx,
            _options.MaxDiameterReferencePx);

        _baseDiameterMapPx = ReferencePxToMapPxLocked(referencePx);

        foreach (var token in _tokens)
            token.Size = _baseDiameterMapPx * (int)token.EntitySize;

        return true;
    }

    private bool ApplySetViewLocked(SetViewCommand command)
    {
        if (_map is null)
            return false;

        var zoom = ViewGeometry.ClampZoom(command.Zoom, _options.MinZoom, _options.MaxZoom);
        var scale = FitScaleLocked() * zoom;
        var center = ViewGeometry.ClampCenter(
            new MapPoint(command.CenterX, command.CenterY),
            ContextLocked(),
            scale);

        _view = new ViewState(center, zoom);
        return true;
    }

    private bool ApplyResetViewLocked()
    {
        if (_map is null)
            return false;

        _view = ViewGeometry.Reset(_map.Width, _map.Height);
        return true;
    }

    private bool ApplyAddSketchStrokeLocked(AddSketchStrokeCommand command)
    {
        if (_map is null || command.Points is null || command.Points.Count == 0)
            return false;

        var color = command.Color?.Trim();
        if (string.IsNullOrEmpty(color) ||
            (color != SketchStroke.EraserColor && !IsHexColor(color)))
            return false;

        var width = Math.Clamp(command.WidthMapPx, 1, MaxSketchWidthMapPx);
        var points = new List<MapPoint>(Math.Min(command.Points.Count, MaxSketchPointsPerStroke));

        foreach (var point in command.Points.Take(MaxSketchPointsPerStroke))
        {
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
                continue;

            // Точки не выходят за карту: клиент не может «нарисовать» в пустоте.
            points.Add(new MapPoint(
                Math.Clamp(point.X, 0, _map.Width),
                Math.Clamp(point.Y, 0, _map.Height)));
        }

        if (points.Count == 0)
            return false;

        TrimSketchLocked();
        _sketch.Add(new SketchStroke(Guid.NewGuid().ToString("N"), color, width, points));
        return true;
    }

    private bool ApplyUndoSketchLocked()
    {
        if (_sketch.Count == 0)
            return false;

        _sketch.RemoveAt(_sketch.Count - 1);
        return true;
    }

    private bool ApplyClearSketchLocked()
    {
        if (_sketch.Count == 0)
            return false;

        _sketch.Clear();
        return true;
    }

    /// <summary>Держит наброски в разумных размерах: старые штрихи уходят первыми.</summary>
    private void TrimSketchLocked()
    {
        while (_sketch.Count > 0 &&
               (_sketch.Count >= MaxSketchStrokes || TotalSketchPointsLocked() > MaxSketchTotalPoints))
        {
            _sketch.RemoveAt(0);
        }
    }

    private int TotalSketchPointsLocked() => _sketch.Sum(stroke => stroke.Points.Count);

    private static bool IsHexColor(string color) =>
        color.Length == 7 && color[0] == '#' && color.Skip(1).All(Uri.IsHexDigit);

    private MapToken? FindTokenLocked(string tokenId) =>
        _tokens.FirstOrDefault(t => string.Equals(t.Id, tokenId, StringComparison.Ordinal));

    private ViewContext ContextLocked() =>
        new(_map?.Width ?? 0, _map?.Height ?? 0, _viewportWidth, _viewportHeight);

    private double FitScaleLocked() => _map is null
        ? 0
        : ViewGeometry.FitScale(_map.Width, _map.Height, _viewportWidth, _viewportHeight);

    private double ReferenceFitLocked() => _map is null
        ? 0
        : ViewGeometry.FitScale(
            _map.Width,
            _map.Height,
            _options.ReferenceViewportWidth,
            _options.ReferenceViewportHeight);

    private double ReferencePxToMapPxLocked(double referencePx)
    {
        var fit = ReferenceFitLocked();
        return fit <= 0 ? 0 : referencePx / fit;
    }

    private double MapPxToReferencePxLocked(double mapPx)
    {
        var fit = ReferenceFitLocked();
        return fit <= 0 ? 0 : mapPx * fit;
    }

    private static SessionChangeKind KindOf(Command command) => command switch
    {
        SetMapCommand => SessionChangeKind.Map,
        AddTokenCommand or RemoveTokenCommand or MoveTokenCommand => SessionChangeKind.Tokens,
        SetDiameterCommand => SessionChangeKind.Diameter,
        SetViewCommand or ResetViewCommand => SessionChangeKind.View,
        AddSketchStrokeCommand or UndoSketchCommand or ClearSketchCommand => SessionChangeKind.Sketch,
        SetTabletInputLockCommand => SessionChangeKind.InputLock,
        _ => SessionChangeKind.View
    };
}
