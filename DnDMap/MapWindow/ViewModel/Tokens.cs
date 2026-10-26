using DnDMap.Core.Parameters;
using DnDMap.TokenModel;
using Point = System.Windows.Point;

namespace DnDMap.MapWindow.ViewModel
{
    public partial class MapViewModel
    {
        public void AddToken(TokenParams tokenParams)
        {
            Tokens.Add(new(tokenParams.TokenImagePath, tokenParams.EntitySize));
        }

        public void SetSelectedToken(Point mousePoint, Token token)
        {
            SelectedToken?.IsSelected = false;

            var selectedToken = Tokens
                .LastOrDefault(t => t.Id == token.Id);
            if (selectedToken is null) return;

            SelectedToken = selectedToken;
            SelectedTokenOffset = SelectedToken.GetOffset(mousePoint);
            SelectedToken.IsSelected = true;
        }

        public void MoveSelectedToken(Point mousePoint)
        {
            if (SelectedToken is null || SelectedTokenOffset is null)
                return;

            SelectedToken.Move(mousePoint, (Point)SelectedTokenOffset);
        }

        private void OnGlobalTokenSizeChanged()
        {
            foreach (var token in Tokens)
            {
                token.OnGlobalTokenSizeChanged();
            }
        }
    }
}