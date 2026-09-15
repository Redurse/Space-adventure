namespace Anabiosis.Shared.Model;

// Direct user request ("удали все текущие корабли в разделе начать новую игру... полностью удалить
// из кода") - Scout/Frigate/Cruiser/Corvette/Destroyer/Freighter (every hand-authored/catalog-built
// fixed hull class) are gone, along with the New Game ship-select screen's own fixed-kind row that
// used to pick one of them. Custom (player-drawn in the Ship Editor, Ship.Custom.cs) is the only
// kind left - every ship in the game is a CustomShipDefinition now, whether the player built it
// themselves or it's the frozen, unnamed default hull a fresh World/GameServer/SoloSession falls
// back to when none is supplied (ShipDefaultHull.cs's own doc comment explains why that fallback
// exists and what it preserves).
public enum ShipKind
{
    Custom,
}
