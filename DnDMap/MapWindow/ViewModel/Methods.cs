using DnDMap.Core.Parameters;
using System.Windows.Input;
using Point = System.Windows.Point;

namespace DnDMap.MapWindow.ViewModel;

public partial class MapViewModel
{
    public ICommand MouseDownCommand { get; }
    public ICommand MouseMoveCommand { get; }

    public void OpenWindow(string mapPath)
    {
        _window = new();
        _window.DataContext = this;
        MapPath = mapPath;
        _window.Show();
    }

    public void OnClosing()
    {
        Tokens.Clear();
        SelectedToken = null;
        SelectedTokenOffset = null;
        MapPath = null;
    }

    private void OnMouseDown(Point mousePoint)
    {
        if (SelectedToken is not null)
            SelectedToken.IsSelected = false;

        var selectedToken = Tokens
            .LastOrDefault(t => t.PointAboveToken(mousePoint));
        if (selectedToken is null) return;

        SelectedToken = selectedToken;
        SelectedTokenOffset = selectedToken.GetOffset(mousePoint);
        SelectedToken.IsSelected = true;
    }

    private void OnMouseMove(Point mousePoint)
    {
        if (SelectedToken is null
            || SelectedTokenOffset is null)
            return;

        if (!SelectedToken.PointAboveToken(mousePoint))
            return;

        SelectedToken.Move(mousePoint, (Point)SelectedTokenOffset);
    }

    public void AddToken(TokenParams tokenParams)
    {
        Tokens.Add(new(tokenParams.TokenImagePath, tokenParams.EntitySize));
    }

    private void OnGlobalTokenSizeChanged()
    {
        foreach (var token in Tokens)
        {
            token.OnGlobalTokenSizeChanged();
        }
    }
}