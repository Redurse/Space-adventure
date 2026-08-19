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
        string? travelToPointId = null,
        ItemType? buyItemType = null,
        int sellSlotIndex = -1,
        bool acceptCargoQuestPressed = false,
        bool turnInCargoQuestPressed = false,
        ShipUpgradeTrack? purchaseUpgradeTrack = null,
        float helmThrustX = 0,
        float helmThrustY = 0,
        bool helmStabilizePressed = false,
        string? doorToggleId = null,
        bool pushOffPressed = false,
        float pushOffDirectionX = 0,
        float pushOffDirectionY = 0,
        ShipKind? purchaseShipKind = null,
        QuestKind? acceptQuestKind = null,
        bool dockPressed = false,
        SlotRef? moveItemFrom = null,
        SlotRef? moveItemTo = null,
        float lookX = 0,
        float lookY = 0,
        int? attachTankFromSlot = null,
        int? attachTankToSlot = null,
        int? detachTankSlot = null,
        bool cutHeld = false,
        string? hireCandidateId = null,
        bool weldHeld = false,
        PinRef? pinInteractId = null,
        bool wireLayCancelPressed = false,
        string? componentOperateId = null,
        string? componentMountInteractId = null,
        SlotRef? dropItemFrom = null,
        string? pickupDroppedItemId = null,
        bool abandonQuestPressed = false,
        string? warpToSystemId = null,
        string? nickname = null,
        CrewRole? setOwnRoleTo = null,
        bool clearOwnRolePressed = false,
        int? playCardRank = null,
        CardSuit? playCardSuit = null,
        bool cardGameTakePressed = false,
        bool cardGameEndRoundPressed = false,
        long lastServerTimestampMs = 0,
        float? travelToX = null,
        float? travelToY = null,
        float? wireBendAtX = null,
        float? wireBendAtY = null,
        bool toggleLightsPressed = false,
        bool toggleReactorEmergencyPressed = false,
        bool toggleDoorsLockedPressed = false,
        bool axeSwingHeld = false) =>
        Send(new ClientCommand(PlayerId, move.X, move.Y, powerSystemIndex, powerDirection, interactPressed, turretAimDirection, firePressed, toggleHoldSlotIndex, toggleReactorSlotIndex, travelToPointId, buyItemType, sellSlotIndex, acceptCargoQuestPressed, turnInCargoQuestPressed, purchaseUpgradeTrack, helmThrustX, helmThrustY, helmStabilizePressed, doorToggleId, pushOffPressed, pushOffDirectionX, pushOffDirectionY, purchaseShipKind, acceptQuestKind, dockPressed, lookX, lookY, moveItemFrom, moveItemTo, attachTankFromSlot, attachTankToSlot, detachTankSlot, cutHeld, hireCandidateId, weldHeld, pinInteractId, wireLayCancelPressed, componentOperateId, componentMountInteractId, dropItemFrom, pickupDroppedItemId, abandonQuestPressed, warpToSystemId, nickname, setOwnRoleTo, clearOwnRolePressed, playCardRank, playCardSuit, cardGameTakePressed, cardGameEndRoundPressed, lastServerTimestampMs, travelToX, travelToY, wireBendAtX, wireBendAtY, toggleLightsPressed, toggleReactorEmergencyPressed, toggleDoorsLockedPressed, axeSwingHeld));

    public void PollSnapshots()
    {
        var snapshot = _connection.ReceiveLatestSnapshot();
        if (snapshot is not null)
            LatestSnapshot = snapshot;
    }
}
