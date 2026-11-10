using DnDMap.Core.Model;
using DnDMap.Core.Protocol;
using Xunit;

namespace DnDMap.Core.Tests.Protocol;

public class SessionMapperTests
{
    [Fact]
    public void ToDto_ContainsVisibleRectAndDiameter()
    {
        var session = new MapSession();
        session.Apply(new SetMapCommand("map.png", 1600, 900));

        var dto = SessionMapper.ToDto(session);

        Assert.Equal(1, dto.Revision);
        Assert.Equal("map.png", dto.Map!.Name);
        Assert.Equal(1600, dto.VisibleRect.Width, 6);
        Assert.Equal(900, dto.VisibleRect.Height, 6);
        Assert.Equal(100 / 1.2, dto.BaseDiameter.MapPx, 6);
        Assert.Equal(100, dto.BaseDiameter.ReferencePx, 6);
        Assert.Equal(1, dto.View.Zoom);
        Assert.Empty(dto.Tokens);
    }

    [Fact]
    public void ToDto_TokenCoordinates_AreMapPixels()
    {
        var session = new MapSession();
        session.Apply(new SetMapCommand("map.png", 1600, 900));
        session.Apply(new AddTokenCommand("goblin.png", EntitySize.Big, 123.5, 456.25));

        var dto = SessionMapper.ToDto(session);

        var token = Assert.Single(dto.Tokens);
        Assert.Equal(123.5, token.X, 6);
        Assert.Equal(456.25, token.Y, 6);
        Assert.Equal(EntitySize.Big, token.EntitySize);
        Assert.Equal(session.BaseDiameterMapPx * 2, token.SizeMapPx, 6);
    }
}
