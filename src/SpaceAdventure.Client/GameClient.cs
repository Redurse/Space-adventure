using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client;

// Holds the latest state received from the server and forwards outgoing commands.
// Owns no simulation logic of its own.
public sealed class GameClient
{
    private readonly IClientConnection _connection;

    public int PlayerId { get; }
    public WorldSnapshot? LatestSnapshot { get; private set; }

    public GameClient(IClientConnection connection, int playerId)
    {
        _connection = connection;
        PlayerId = playerId;
    }

    public void Send(ClientCommand command) => _connection.Send(command);

    public void SendInput(
        Vec2 move,
        int powerSystemIndex,
        float powerDirection,
        bool interactPressed,
        float turretAimDirection,
        bool firePressed,
        int toggleHoldSlotIndex = -1,
        int toggleReactorSlotIndex = -1,
        string? travelToPointId = null) =>
        Send(new ClientCommand(PlayerId, move.X, move.Y, powerSystemIndex, powerDirection, interactPressed, turretAimDirection, firePressed, toggleHoldSlotIndex, toggleReactorSlotIndex, travelToPointId));

    public void PollSnapshots()
    {
        var snapshot = _connection.ReceiveLatestSnapshot();
        if (snapshot is not null)
            LatestSnapshot = snapshot;
    }
}
