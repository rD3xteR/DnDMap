using DnDMap.Core.Geometry;
using DnDMap.Core.Model;
using DnDMap.Core.Protocol;
using Xunit;

namespace DnDMap.Core.Tests.Model;

public class MapSessionTests
{
    private const string MapName = "map.png";
    private const double MapWidth = 1600;
    private const double MapHeight = 900;

    private static MapSession CreateSession() => new();

    private static MapSession CreateSessionWithMap()
    {
        var session = new MapSession();
        Assert.True(session.Apply(new SetMapCommand(MapName, (int)MapWidth, (int)MapHeight)));
        return session;
    }

    [Fact]
    public void Apply_SetMap_SetsMapAndResetsView()
    {
        var session = CreateSession();

        Assert.True(session.Apply(new SetMapCommand(MapName, 1600, 900)));

        Assert.NotNull(session.Map);
        Assert.Equal(MapName, session.Map!.Name);
        Assert.Equal(1600, session.Map.Width);
        Assert.Equal(900, session.Map.Height);
        Assert.Equal(1, session.View.Zoom);
        Assert.Equal(800, session.View.Center.X);
        Assert.Equal(450, session.View.Center.Y);
        Assert.Equal(1, session.Revision);
    }

    [Fact]
    public void Apply_SetMap_ComputesBaseDiameterInMapPixels()
    {
        var session = CreateSessionWithMap();

        // Эталон 1920×1080, карта 1600×900 → fitRef = 1.2 → 100 / 1.2 = 83.3333.
        Assert.Equal(100 / 1.2, session.BaseDiameterMapPx, 6);
        Assert.Equal(100, session.MapPxToReferencePx(session.BaseDiameterMapPx), 6);
    }

    [Fact]
    public void Apply_AddToken_PlacesAtMapCenterByDefault()
    {
        var session = CreateSessionWithMap();

        Assert.True(session.Apply(new AddTokenCommand("goblin.png")));

        var token = Assert.Single(session.Tokens);
        Assert.Equal(800, token.X, 6);
        Assert.Equal(450, token.Y, 6);
        Assert.Equal(session.BaseDiameterMapPx, token.Size, 6);
        Assert.Equal(2, session.Revision);
    }

    [Fact]
    public void Apply_AddToken_WithoutMap_IsRejected()
    {
        var session = CreateSession();

        Assert.False(session.Apply(new AddTokenCommand("goblin.png")));
        Assert.Empty(session.Tokens);
        Assert.Equal(0, session.Revision);
    }

    [Fact]
    public void Apply_MoveToken_UpdatesPositionAndBumpsRevision()
    {
        var session = CreateSessionWithMap();
        session.Apply(new AddTokenCommand("goblin.png"));
        var token = session.Tokens[0];

        Assert.True(session.Apply(new MoveTokenCommand(token.Id, 100, 200)));

        Assert.Equal(100, token.X, 6);
        Assert.Equal(200, token.Y, 6);
        Assert.Equal(3, session.Revision);
    }

    [Fact]
    public void Apply_MoveToken_UnknownId_IsRejected()
    {
        var session = CreateSessionWithMap();
        var revision = session.Revision;

        Assert.False(session.Apply(new MoveTokenCommand("missing", 10, 10)));
        Assert.Equal(revision, session.Revision);
    }

    [Fact]
    public void Apply_RemoveToken_RemovesToken()
    {
        var session = CreateSessionWithMap();
        session.Apply(new AddTokenCommand("goblin.png"));
        var token = session.Tokens[0];

        Assert.True(session.Apply(new RemoveTokenCommand(token.Id)));

        Assert.Empty(session.Tokens);
    }

    [Fact]
    public void Apply_SetDiameter_ClampsToRange()
    {
        var session = CreateSessionWithMap();

        Assert.True(session.Apply(new SetDiameterCommand(500)));

        // Верхняя граница — 200 ref-px → 200 / 1.2 = 166.6667 map-px.
        Assert.Equal(200 / 1.2, session.BaseDiameterMapPx, 6);
        Assert.Equal(200, session.MapPxToReferencePx(session.BaseDiameterMapPx), 6);
    }

    [Fact]
    public void Apply_SetDiameter_ResizesAllTokens()
    {
        var session = CreateSessionWithMap();
        session.Apply(new AddTokenCommand("normal.png", EntitySize.Normal));
        session.Apply(new AddTokenCommand("big.png", EntitySize.Big));

        Assert.True(session.Apply(new SetDiameterCommand(120)));

        var tokens = session.Tokens;
        var normal = Assert.Single(tokens, t => t.EntitySize == EntitySize.Normal);
        var big = Assert.Single(tokens, t => t.EntitySize == EntitySize.Big);

        Assert.Equal(100, normal.Size, 6); // 120 / 1.2 = 100 map-px
        Assert.Equal(200, big.Size, 6);    // 100 × 2
    }

    [Fact]
    public void Apply_SetView_ClampsZoom()
    {
        var session = CreateSessionWithMap();

        Assert.True(session.Apply(new SetViewCommand(800, 450, 99)));
        Assert.Equal(3, session.View.Zoom);

        Assert.True(session.Apply(new SetViewCommand(800, 450, 0.1)));
        Assert.Equal(1, session.View.Zoom);
    }

    [Fact]
    public void Apply_ResetView_ReturnsToCenterAndZoomOne()
    {
        var session = CreateSessionWithMap();
        session.Apply(new SetViewCommand(100, 100, 2.5));

        Assert.True(session.Apply(new ResetViewCommand()));

        Assert.Equal(1, session.View.Zoom);
        Assert.Equal(800, session.View.Center.X, 6);
        Assert.Equal(450, session.View.Center.Y, 6);
    }

    [Fact]
    public void Apply_WithoutMap_RejectsViewCommands()
    {
        var session = CreateSession();

        Assert.False(session.Apply(new SetViewCommand(0, 0, 2)));
        Assert.False(session.Apply(new ResetViewCommand()));
        Assert.False(session.Apply(new SetDiameterCommand(100)));
    }

    [Fact]
    public void Changed_RaisedOncePerAcceptedCommand()
    {
        var session = CreateSessionWithMap();
        var changes = new List<SessionChange>();
        session.Changed += changes.Add;

        session.Apply(new AddTokenCommand("goblin.png"));
        session.Apply(new MoveTokenCommand("missing", 0, 0)); // отклонена
        session.Apply(new SetDiameterCommand(120));

        Assert.Equal(2, changes.Count);
        Assert.Equal(SessionChangeKind.Tokens, changes[0].Kind);
        Assert.Equal(SessionChangeKind.Diameter, changes[1].Kind);
    }

    [Fact]
    public void Revision_IncreasesMonotonically()
    {
        var session = CreateSessionWithMap();
        var revisions = new List<long> { session.Revision };

        session.Apply(new AddTokenCommand("a.png"));
        revisions.Add(session.Revision);
        session.Apply(new AddTokenCommand("b.png"));
        revisions.Add(session.Revision);

        Assert.Equal(revisions.OrderBy(r => r), revisions);
        Assert.Equal(3, revisions[^1]);
    }

    [Fact]
    public void SetViewport_ChangesVisibleRect()
    {
        var session = CreateSessionWithMap();
        var before = session.VisibleRect;

        Assert.True(session.SetViewport(1000, 1000));

        var after = session.VisibleRect;

        Assert.Equal(0, before.X, 6);
        Assert.Equal(1600, before.Width, 6);

        // fit = min(1000/1600, 1000/900) = 0.625 → высота видимой области 1000 / 0.625 = 1600.
        Assert.Equal(0, after.X, 6);
        Assert.Equal(-350, after.Y, 6);
        Assert.Equal(1600, after.Width, 6);
        Assert.Equal(1600, after.Height, 6);
    }

    [Fact]
    public void SetViewport_SameSize_ReturnsFalse()
    {
        var session = CreateSessionWithMap();
        var revision = session.Revision;

        Assert.False(session.SetViewport(1920, 1080));
        Assert.Equal(revision, session.Revision);
    }

    [Fact]
    public void SetMap_KeepsTokens_AndClampsThemToNewMap()
    {
        var session = CreateSessionWithMap();
        session.Apply(new AddTokenCommand("goblin.png", EntitySize.Normal, 1500, 800));

        Assert.True(session.Apply(new SetMapCommand("small.png", 800, 600)));

        var token = Assert.Single(session.Tokens);
        Assert.Equal(800, token.X, 6);
        Assert.Equal(600, token.Y, 6);
    }

    [Fact]
    public void Snapshot_IsConsistent()
    {
        var session = CreateSessionWithMap();
        session.Apply(new AddTokenCommand("goblin.png"));

        var snapshot = session.Snapshot();

        Assert.Equal(session.Revision, snapshot.Revision);
        Assert.Equal(MapName, snapshot.Map!.Name);
        Assert.Single(snapshot.Tokens);
        Assert.Equal(1, snapshot.View.Zoom);
        Assert.Equal(1600, snapshot.VisibleRect.Width, 6);
    }

    [Fact]
    public void Apply_InvalidMap_IsRejected()
    {
        var session = CreateSession();

        Assert.False(session.Apply(new SetMapCommand("map.png", 0, 900)));
        Assert.False(session.Apply(new SetMapCommand("", 1600, 900)));
        Assert.Null(session.Map);
    }

    [Fact]
    public void VisibleRect_ShrinksWithZoom()
    {
        var session = CreateSessionWithMap();
        var atFit = session.VisibleRect;

        Assert.True(session.Apply(new SetViewCommand(800, 450, 2)));

        var zoomed = session.VisibleRect;

        // Видимая область — вьюпорт / (fit × zoom): при зуме 2 она вдвое меньше.
        Assert.Equal(atFit.Width / 2, zoomed.Width, 6);
        Assert.Equal(atFit.Height / 2, zoomed.Height, 6);
        Assert.Equal(800, zoomed.X + (zoomed.Width / 2), 6);
        Assert.Equal(450, zoomed.Y + (zoomed.Height / 2), 6);
    }

    [Fact]
    public void Snapshot_VisibleRect_AlsoHonoursZoom()
    {
        var session = CreateSessionWithMap();
        session.Apply(new SetViewCommand(800, 450, 2.5));

        var snapshot = session.Snapshot();

        Assert.Equal(session.VisibleRect.Width, snapshot.VisibleRect.Width, 6);
        Assert.Equal(session.VisibleRect.Height, snapshot.VisibleRect.Height, 6);
    }

    [Fact]
    public void Apply_MoveToken_ClampsCenterToMapBounds()
    {
        var session = CreateSessionWithMap();
        session.Apply(new AddTokenCommand("goblin.png"));
        var id = session.Tokens[0].Id;

        Assert.True(session.Apply(new MoveTokenCommand(id, 5000, -300)));

        var token = Assert.Single(session.Tokens);
        Assert.Equal(MapWidth, token.X, 6);
        Assert.Equal(0, token.Y, 6);
    }

    [Fact]
    public void Apply_AddTokenWithExplicitPosition_ClampsToMapBounds()
    {
        var session = CreateSessionWithMap();

        Assert.True(session.Apply(new AddTokenCommand("goblin.png", EntitySize.Normal, -50, 4000)));

        var token = Assert.Single(session.Tokens);
        Assert.Equal(0, token.X, 6);
        Assert.Equal(MapHeight, token.Y, 6);
    }

    [Fact]
    public void SetTabletInputLock_IsVisibleInSnapshotAndRaisesChange()
    {
        var session = CreateSessionWithMap();
        SessionChange? last = null;
        session.Changed += change => last = change;

        Assert.True(session.Apply(new SetTabletInputLockCommand(true)));

        Assert.True(session.TabletInputLocked);
        Assert.True(session.Snapshot().TabletInputLocked);
        Assert.Equal(SessionChangeKind.InputLock, last!.Kind);

        Assert.True(session.Apply(new SetTabletInputLockCommand(false)));
        Assert.False(session.Snapshot().TabletInputLocked);
    }

    [Fact]
    public void Sketch_AddUndoAndClear()
    {
        var session = CreateSessionWithMap();
        PointDto[] points = [new(10, 10), new(50, 60)];

        Assert.True(session.Apply(new AddSketchStrokeCommand("#c9a227", 12, points)));
        Assert.True(session.Apply(new AddSketchStrokeCommand(SketchStroke.EraserColor, 20, points)));

        Assert.Equal(2, session.Sketch.Count);
        Assert.Equal("#c9a227", session.Sketch[0].Color);
        Assert.Equal(12, session.Sketch[0].WidthMapPx);
        Assert.Equal(2, session.Sketch[0].Points.Count);

        Assert.True(session.Apply(new UndoSketchCommand()));
        Assert.Single(session.Sketch);

        Assert.True(session.Apply(new ClearSketchCommand()));
        Assert.Empty(session.Sketch);

        // Отменять и очищать нечего — команды отклонены.
        Assert.False(session.Apply(new UndoSketchCommand()));
        Assert.False(session.Apply(new ClearSketchCommand()));
    }

    [Fact]
    public void Sketch_IsClearedWhenMapChanges()
    {
        var session = CreateSessionWithMap();
        session.Apply(new AddSketchStrokeCommand(
            "#000000", 6, [new PointDto(1, 1), new PointDto(2, 2)]));

        Assert.True(session.Apply(new SetMapCommand("other.png", 1000, 700)));

        Assert.Empty(session.Sketch);
    }

    [Fact]
    public void Sketch_RejectsInvalidStrokes_AndClampsPoints()
    {
        var session = CreateSessionWithMap();

        Assert.False(session.Apply(new AddSketchStrokeCommand("red", 6, [new PointDto(1, 1)])));
        Assert.False(session.Apply(new AddSketchStrokeCommand("#000000", 6, [])));

        Assert.True(session.Apply(new AddSketchStrokeCommand(
            "#000000", 500, [new PointDto(-100, 5000), new PointDto(800, 450)])));

        var stroke = Assert.Single(session.Sketch);
        Assert.Equal(200, stroke.WidthMapPx);
        Assert.Equal(0, stroke.Points[0].X, 6);
        Assert.Equal(MapHeight, stroke.Points[0].Y, 6);
        Assert.Equal(800, stroke.Points[1].X, 6);
    }
}
