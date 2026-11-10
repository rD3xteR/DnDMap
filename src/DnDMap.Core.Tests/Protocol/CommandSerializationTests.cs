using System.Text.Json;
using DnDMap.Core.Model;
using DnDMap.Core.Protocol;
using Xunit;

namespace DnDMap.Core.Tests.Protocol;

public class CommandSerializationTests
{
    private static Command Deserialize(string json) =>
        JsonSerializer.Deserialize<Command>(json, ProtocolJson.Options)!;

    [Fact]
    public void Deserialize_SetMap()
    {
        var command = Assert.IsType<SetMapCommand>(
            Deserialize("""{"type":"setMap","name":"map.png","width":1600,"height":900}"""));

        Assert.Equal("map.png", command.Name);
        Assert.Equal(1600, command.Width);
        Assert.Equal(900, command.Height);
    }

    [Fact]
    public void Deserialize_AddToken_WithStringEnum()
    {
        var command = Assert.IsType<AddTokenCommand>(
            Deserialize("""{"type":"addToken","file":"goblin.png","entitySize":"big"}"""));

        Assert.Equal("goblin.png", command.File);
        Assert.Equal(EntitySize.Big, command.EntitySize);
        Assert.Null(command.X);
        Assert.Null(command.Y);
    }

    [Fact]
    public void Deserialize_MoveToken()
    {
        var command = Assert.IsType<MoveTokenCommand>(
            Deserialize("""{"type":"moveToken","tokenId":"abc","x":1.5,"y":2.5}"""));

        Assert.Equal("abc", command.TokenId);
        Assert.Equal(1.5, command.X);
        Assert.Equal(2.5, command.Y);
    }

    [Fact]
    public void Deserialize_RemoveToken()
    {
        var command = Assert.IsType<RemoveTokenCommand>(
            Deserialize("""{"type":"removeToken","tokenId":"abc"}"""));

        Assert.Equal("abc", command.TokenId);
    }

    [Fact]
    public void Deserialize_SetDiameter()
    {
        var command = Assert.IsType<SetDiameterCommand>(
            Deserialize("""{"type":"setDiameter","referencePx":120}"""));

        Assert.Equal(120, command.ReferencePx);
    }

    [Fact]
    public void Deserialize_SetView()
    {
        var command = Assert.IsType<SetViewCommand>(
            Deserialize("""{"type":"setView","centerX":800,"centerY":450,"zoom":2}"""));

        Assert.Equal(800, command.CenterX);
        Assert.Equal(450, command.CenterY);
        Assert.Equal(2, command.Zoom);
    }

    [Fact]
    public void Deserialize_ResetView()
    {
        Assert.IsType<ResetViewCommand>(Deserialize("""{"type":"resetView"}"""));
    }

    [Fact]
    public void Deserialize_UnknownType_Throws()
    {
        Assert.Throws<JsonException>(() => Deserialize("""{"type":"nope"}"""));
    }

    [Fact]
    public void Deserialize_MissingRequiredField_Throws()
    {
        Assert.Throws<JsonException>(() => Deserialize("""{"type":"moveToken","x":1,"y":2}"""));
    }

    [Fact]
    public void Serialize_UsesDiscriminatorAndCamelCase()
    {
        var json = JsonSerializer.Serialize<Command>(new MoveTokenCommand("abc", 1.5, 2.5), ProtocolJson.Options);

        Assert.Contains("\"type\":\"moveToken\"", json);
        Assert.Contains("\"tokenId\":\"abc\"", json);
    }
}
