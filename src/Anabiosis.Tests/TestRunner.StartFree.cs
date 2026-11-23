using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// A real new game starts with the ship free beside the home station in its base position (World.StartFree.cs).
internal static partial class TestRunner
{
    private static bool StartFree_ShipIsFreeStillAndNotTurned()
    {
        var world = new World(startDocked: false);
        world.SpawnCharacter(1);
        world.Step(RealtimeStep);
        var snapshot = world.CreateSnapshot();
        var field = snapshot.ShipField;
        return !world.IsDocked && snapshot.Voyage.DockedPointId is null
            && field.RotationDegrees == 0f && Math.Abs(field.VelocityX) < 0.5f && Math.Abs(field.VelocityY) < 0.5f;
    }

    // Free, but not inside or against the station: a clear gap in front of its port.
    private static bool StartFree_ShipStartsClearOfTheStation()
    {
        var world = new World(startDocked: false);
        world.SpawnCharacter(1);
        world.Step(RealtimeStep);
        var snapshot = world.CreateSnapshot();
        var ship = snapshot.Rooms;
        var halfX = (ship.Max(r => r.Right) - ship.Min(r => r.Left)) / 2.0;
        var halfY = (ship.Max(r => r.Bottom) - ship.Min(r => r.Top)) / 2.0;
        var centre = new Vec2(snapshot.ShipField.X, snapshot.ShipField.Y);
        var offset = snapshot.Station.WorldOffset;
        foreach (var room in snapshot.Station.Rooms)
            foreach (var rect in room.Rects)
            {
                var left = rect.Left + offset.X; var right = rect.Right + offset.X;
                var top = rect.Top + offset.Y; var bottom = rect.Bottom + offset.Y;
                var overlapsX = centre.X + halfX + 1 > left && centre.X - halfX - 1 < right;
                var overlapsY = centre.Y + halfY + 1 > top && centre.Y - halfY - 1 < bottom;
                if (overlapsX && overlapsY)
                    return false;
            }
        return true;
    }

    // And it can still dock to it afterwards.
    private static bool StartFree_CanDockAfterwards()
    {
        var world = new World(startDocked: false);
        world.SpawnCharacter(1);
        world.Step(RealtimeStep);
        DockAtStation(world, "home-station");
        return world.IsDocked;
    }
}
