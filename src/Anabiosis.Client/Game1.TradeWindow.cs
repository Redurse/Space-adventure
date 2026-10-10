using System.Linq;
using Anabiosis.Client.Rendering;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client;

public partial class Game1
{
    // Whether the player has the Trader's store open right now (the station dialogue is open on a Trader NPC).
    private bool IsTalkingToTrader(WorldSnapshot? snapshot) =>
        _openBlock.Kind == BlockKind.Station && _talkingToNpcId is not null &&
        snapshot?.Station.Npcs.FirstOrDefault(n => n.Id == _talkingToNpcId)?.Kind == NpcKind.Trader;
}
