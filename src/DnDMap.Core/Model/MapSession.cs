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
    private readonly object _gate = new();
    private readonly SessionOptions _options;
    private readonly List<MapToken> _tokens = [];

    private MapInfo? _map;
    private ViewState _view = new(MapPoint.Zero, ViewGeometry.MinZoom);
    private double _viewportWidth;
    private double _viewportHeight;
    private double _baseDiameterMapPx;
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
                _tokens.ToArray());
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
        _ => SessionChangeKind.View
    };
}
