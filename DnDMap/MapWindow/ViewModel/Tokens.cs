using System.IO;
using System.Windows;
using DnDMap.Core.Geometry;
using DnDMap.Core.Model;
using DnDMap.Core.Protocol;
using Point = System.Windows.Point;

namespace DnDMap.MapWindow.ViewModel;

/// <summary>
/// Токены: команды в сессию, попадание по экранной точке и синхронизация
/// представления с состоянием ядра.
/// </summary>
public partial class MapViewModel
{
    private string? _selectedTokenId;
    private string? _dragTokenId;
    private MapPoint _dragOffset;

    /// <summary>
    /// Добавить токен. Пока карта не открыта, запрос запоминается и применяется
    /// после её открытия (старое поведение: токены можно готовить заранее).
    /// </summary>
    public void AddToken(string imagePath, EntitySize size)
    {
        if (_session.Map is null)
        {
            _pendingTokens.Add((imagePath, size));
            return;
        }

        var before = _session.Tokens
            .Select(token => token.Id)
            .ToHashSet(StringComparer.Ordinal);

        if (!_session.Apply(new AddTokenCommand(Path.GetFileName(imagePath), size)))
            return;

        var added = _session.Tokens.FirstOrDefault(token => !before.Contains(token.Id));
        if (added is null)
            return;

        // Apply поднимает Changed синхронно, поэтому представление уже создано
        // без пути к картинке — обновляем его ещё раз с известным путём.
        _tokenImagePaths[added.Id] = imagePath;
        Refresh();
    }

    public TokenViewModel? HitTest(Point screenPoint)
    {
        var snapshot = _session.Snapshot();
        var scale = snapshot.FitScale * snapshot.View.Zoom;
        if (snapshot.Map is null || scale <= 0)
            return null;

        // Обходим сверху вниз: последний нарисованный токен ловит клик первым.
        for (var index = snapshot.Tokens.Count - 1; index >= 0; index--)
        {
            var token = snapshot.Tokens[index];
            var center = ViewGeometry.MapToScreen(
                new MapPoint(token.X, token.Y),
                snapshot.View.Center,
                scale,
                snapshot.ViewportWidth,
                snapshot.ViewportHeight);

            var radius = token.Size * scale / 2.0;
            var dx = screenPoint.X - center.X;
            var dy = screenPoint.Y - center.Y;

            if ((dx * dx) + (dy * dy) <= radius * radius)
                return Tokens.FirstOrDefault(item => item.Id == token.Id);
        }

        return null;
    }

    public void BeginTokenDrag(TokenViewModel token, Point screenPoint)
    {
        var mapToken = _session.Tokens.FirstOrDefault(item => item.Id == token.Id);
        if (mapToken is null)
            return;

        var mapPoint = ScreenToMap(screenPoint);
        _dragTokenId = token.Id;
        _dragOffset = new MapPoint(mapPoint.X - mapToken.X, mapPoint.Y - mapToken.Y);
        SelectToken(token);
    }

    public void DragToken(Point screenPoint)
    {
        if (_dragTokenId is null)
            return;

        var mapPoint = ScreenToMap(screenPoint);
        _session.Apply(new MoveTokenCommand(
            _dragTokenId,
            mapPoint.X - _dragOffset.X,
            mapPoint.Y - _dragOffset.Y));
    }

    public void EndTokenDrag() => _dragTokenId = null;

    public void SelectToken(TokenViewModel? token)
    {
        var id = token?.Id;
        if (id == _selectedTokenId)
            return;

        _selectedTokenId = id;
        SelectedTokenId = id;

        foreach (var item in Tokens)
            item.IsSelected = item.Id == id;
    }

    public void RemoveSelectedToken()
    {
        if (_selectedTokenId is null)
            return;

        if (!_session.Apply(new RemoveTokenCommand(_selectedTokenId)))
            return;

        _selectedTokenId = null;
        SelectedTokenId = null;
    }

    private void FlushPendingTokens()
    {
        if (_pendingTokens.Count == 0)
            return;

        foreach (var (imagePath, size) in _pendingTokens.ToArray())
            AddToken(imagePath, size);

        _pendingTokens.Clear();
    }

    private MapPoint ScreenToMap(Point screenPoint)
    {
        var snapshot = _session.Snapshot();
        var scale = snapshot.FitScale * snapshot.View.Zoom;
        if (snapshot.Map is null || scale <= 0)
            return MapPoint.Zero;

        return ViewGeometry.ScreenToMap(
            new MapPoint(screenPoint.X, screenPoint.Y),
            snapshot.View.Center,
            scale,
            snapshot.ViewportWidth,
            snapshot.ViewportHeight);
    }

    /// <summary>Пересчитывает экранные координаты из снимка сессии.</summary>
    private void Refresh()
    {
        var snapshot = _session.Snapshot();
        var scale = snapshot.FitScale * snapshot.View.Zoom;

        UpdateMapRect(snapshot, scale);
        SyncTokens(snapshot, scale);

        OnPropertyChanged(nameof(Zoom));
    }

    private void UpdateMapRect(SessionSnapshot snapshot, double scale)
    {
        if (snapshot.Map is null || scale <= 0)
        {
            MapLeft = 0;
            MapTop = 0;
            MapWidth = 0;
            MapHeight = 0;
            return;
        }

        var rect = ViewGeometry.MapRectToScreen(
            new RectD(0, 0, snapshot.Map.Width, snapshot.Map.Height),
            snapshot.View.Center,
            scale,
            snapshot.ViewportWidth,
            snapshot.ViewportHeight);

        MapLeft = rect.X;
        MapTop = rect.Y;
        MapWidth = rect.Width;
        MapHeight = rect.Height;
    }

    private void SyncTokens(SessionSnapshot snapshot, double scale)
    {
        var alive = snapshot.Tokens
            .Select(token => token.Id)
            .ToHashSet(StringComparer.Ordinal);

        for (var index = Tokens.Count - 1; index >= 0; index--)
        {
            if (!alive.Contains(Tokens[index].Id))
                Tokens.RemoveAt(index);
        }

        var byId = Tokens.ToDictionary(item => item.Id, StringComparer.Ordinal);

        foreach (var token in snapshot.Tokens)
        {
            if (!byId.TryGetValue(token.Id, out var viewModel))
            {
                viewModel = new TokenViewModel(token.Id, token.File, ResolveImagePath(token));
                Tokens.Add(viewModel);
                byId[token.Id] = viewModel;
            }

            // Путь к картинке уточняется по мере поступления (например, после AddToken).
            viewModel.ImagePath = ResolveImagePath(token);
            viewModel.IsSelected = token.Id == _selectedTokenId;

            var center = ViewGeometry.MapToScreen(
                new MapPoint(token.X, token.Y),
                snapshot.View.Center,
                scale,
                snapshot.ViewportWidth,
                snapshot.ViewportHeight);

            var diameter = token.Size * scale;
            viewModel.Diameter = diameter;
            viewModel.Left = center.X - (diameter / 2.0);
            viewModel.Top = center.Y - (diameter / 2.0);
        }
    }

    private string? ResolveImagePath(MapToken token)
    {
        if (_tokenImagePaths.TryGetValue(token.Id, out var path))
            return path;

        var tokensPath = Paths.Default.TokensPath;
        return string.IsNullOrWhiteSpace(tokensPath)
            ? null
            : Path.Combine(tokensPath, token.File);
    }
}
