using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// The waiting room's second round (GameServer.Lobby.cs, GameServer.Bans.cs, SaveStore.Slots.cs): away and watching players, starting when the players are ready,
// the look and starting kit, the campaign modifiers, the black list, the settings window's name/message, and choosing a saved campaign.
internal static partial class TestRunner
{
    private static ClientCommand LobbyCmd(int playerId, Func<ClientCommand, ClientCommand> edit) => edit(new ClientCommand(playerId));

    // Someone who is away is not in the round, and stays in the waiting room: they keep getting the lobby, the others get the world.
    private static bool LobbyV2_AwayPlayerStaysInTheWaitingRoom()
    {
        var (server, host, hostId, guest, guestId) = LobbyWithGuest();
        guest.Send(LobbyCmd(guestId, c => c with { LobbyAfk = true }));
        server.Tick();
        host.Send(LobbyCmd(hostId, c => c with { LobbyStartRoundPressed = true }));
        server.Tick();
        server.Tick();
        var hostWorld = host.ReceiveLatestSnapshot();
        var guestWorld = guest.ReceiveLatestSnapshot();
        var guestLobby = guest.ReceiveLatestLobby();
        return hostWorld is not null && hostWorld.Characters.All(c => c.PlayerId != guestId) && guestWorld is null
            && guestLobby is { RoundInProgress: true } && guestLobby.Players.Any(p => p.PlayerId == guestId && p.IsAfk);
    }

    // Coming back from away while the round runs: the player joins it, with a character.
    private static bool LobbyV2_AwayPlayerJoinsTheRoundOnReturning()
    {
        var (server, host, hostId, guest, guestId) = LobbyWithGuest();
        guest.Send(LobbyCmd(guestId, c => c with { LobbyAfk = true }));
        server.Tick();
        host.Send(LobbyCmd(hostId, c => c with { LobbyStartRoundPressed = true }));
        server.Tick();
        server.Tick();
        guest.Send(LobbyCmd(guestId, c => c with { LobbyAfk = false }));
        server.Tick();
        server.Tick();
        return guest.ReceiveLatestSnapshot() is { } world && world.Characters.Any(c => c.PlayerId == guestId);
    }

    // A watcher gets the round's snapshots but no character of their own.
    private static bool LobbyV2_WatcherSeesTheRoundWithoutACharacter()
    {
        var (server, host, hostId, guest, guestId) = LobbyWithGuest();
        guest.Send(LobbyCmd(guestId, c => c with { LobbySpectate = true }));
        server.Tick();
        host.Send(LobbyCmd(hostId, c => c with { LobbyStartRoundPressed = true }));
        server.Tick();
        server.Tick();
        var world = guest.ReceiveLatestSnapshot();
        // Whatever a watcher sends does nothing, and the server does not trip over a command from someone with no character.
        guest.Send(LobbyCmd(guestId, c => c with { MoveX = 1f, InteractPressed = true }));
        server.Tick();
        return world is not null && world.Characters.All(c => c.PlayerId != guestId) && world.Characters.Any(c => c.PlayerId == hostId);
    }

    // Away and watching exclude each other, and the host can switch either off for everyone.
    private static bool LobbyV2_HostCanForbidAwayAndWatching()
    {
        var (server, host, hostId, guest, guestId) = LobbyWithGuest();
        guest.Send(LobbyCmd(guestId, c => c with { LobbyAfk = true }));
        server.Tick();
        guest.Send(LobbyCmd(guestId, c => c with { LobbySpectate = true }));
        server.Tick();
        var view = host.ReceiveLatestLobby()!.Players.First(p => p.PlayerId == guestId);
        if (view is not { IsSpectator: true, IsAfk: false })
            return false;
        host.Send(LobbyCmd(hostId, c => c with { LobbySettingsUpdate = new LobbySettings(AllowSpectate: false, AllowAfk: false) }));
        server.Tick();
        guest.Send(LobbyCmd(guestId, c => c with { LobbyAfk = true, LobbySpectate = true }));
        server.Tick();
        var after = host.ReceiveLatestLobby()!.Players.First(p => p.PlayerId == guestId);
        return after is { IsSpectator: false, IsAfk: false };
    }

    // "Start when ready": once enough of those taking part are ready the round begins by itself; someone away is not counted.
    private static bool LobbyV2_RoundStartsByItselfWhenEnoughAreReady()
    {
        var (server, host, hostId, guest, guestId) = LobbyWithGuest();
        host.Send(LobbyCmd(hostId, c => c with { LobbySettingsUpdate = new LobbySettings(StartWhenReady: true, StartWhenReadyPercent: 100), LobbyReady = true }));
        server.Tick();
        if (server.InLobby is false)
            return false; // the guest is not ready yet
        guest.Send(LobbyCmd(guestId, c => c with { LobbyReady = true }));
        server.Tick();
        server.Tick();
        if (server.InLobby)
            return false;
        // And away players do not hold it up.
        var (server2, host2, hostId2, guest2, guestId2) = LobbyWithGuest();
        guest2.Send(LobbyCmd(guestId2, c => c with { LobbyAfk = true }));
        host2.Send(LobbyCmd(hostId2, c => c with { LobbySettingsUpdate = new LobbySettings(StartWhenReady: true, StartWhenReadyPercent: 100), LobbyReady = true }));
        server2.Tick();
        server2.Tick();
        return !server2.InLobby;
    }

    // The look and the kit chosen in the lobby are on the character the moment the round starts.
    private static bool LobbyV2_LookAndKitReachTheCharacter()
    {
        var (server, host, hostId, guest, guestId) = LobbyWithGuest();
        var look = new CharacterLook(CharacterGender.Female, 3, 4, 2, 5, 1, 4); // the beard is dropped for a woman
        guest.Send(LobbyCmd(guestId, c => c with { SetOwnRoleTo = CrewRole.Engineer, LobbyLook = look, LobbyKitIndex = 1 }));
        server.Tick();
        host.Send(LobbyCmd(hostId, c => c with { LobbyStartRoundPressed = true }));
        server.Tick();
        server.Tick();
        var me = guest.ReceiveLatestSnapshot()?.Characters.FirstOrDefault(c => c.PlayerId == guestId);
        if (me is null || me.Look is not { Gender: CharacterGender.Female, Hair: 3, Beard: 0, Accessory: 2, HairColor: 5, Skin: 4 })
            return false;
        var items = me.Inventory!.MainSlots.OfType<ItemType>().ToList();
        return StartKits.Get(CrewRole.Engineer, 1).Items.All(item => items.Contains(item));
    }

    private static bool LobbyV2_KitsAndLooksStayInRange()
    {
        var wild = new CharacterLook(CharacterGender.Male, 99, -4, 40, 77, -1, 12).Clamped();
        return wild.Hair == CharacterLook.HairStyles - 1 && wild.Beard == 0 && wild.Accessory == CharacterLook.Accessories - 1
            && wild.HairColor == CharacterLook.HairColors - 1 && wild.BeardColor == 0 && wild.Skin == CharacterLook.SkinTones - 1
            && StartKits.Get(CrewRole.Captain, -1).Items.Count > 0 && StartKits.Get(null, 7).Items.Count > 0
            && Enum.GetValues<CrewRole>().All(role => StartKits.For(role).Count == StartKits.PerRole);
    }

    // The campaign modifiers are clamped, reach the round, and ride in its snapshots.
    private static bool LobbyV2_CampaignModifiersReachTheRound()
    {
        var (server, host, hostId, _, _) = LobbyWithGuest();
        host.Send(LobbyCmd(hostId, c => c with { LobbySettingsUpdate = new LobbySettings(RewardMultiplier: 2.04f, ShopPriceMultiplier: 99f, ShipyardPriceMultiplier: 0f, VoiceChat: false) }));
        server.Tick();
        var settings = host.ReceiveLatestLobby()!.Settings!;
        if (settings.RewardMultiplier != 2.0f || settings.ShopPriceMultiplier != CampaignModifiers.Max || settings.ShipyardPriceMultiplier != CampaignModifiers.Min)
            return false;
        host.Send(LobbyCmd(hostId, c => c with { LobbyStartRoundPressed = true }));
        server.Tick();
        var world = host.ReceiveLatestSnapshot();
        return world?.Modifiers is { Reward: 2.0f, ShopPrice: CampaignModifiers.Max, ShipyardPrice: CampaignModifiers.Min }
            && CampaignModifiers.Scale(100, 1.5f) == 150 && CampaignModifiers.Scale(1, 0.1f) == 1 && CampaignModifiers.Scale(0, 3f) == 0;
    }

    // The host renames the server and writes a message of the day; only the host can.
    private static bool LobbyV2_HostRenamesTheServerAndWritesAMessage()
    {
        var (server, host, hostId, guest, guestId) = LobbyWithGuest();
        guest.Send(LobbyCmd(guestId, c => c with { LobbySettingsUpdate = new LobbySettings(ServerName: "Чужое", Motd: "взлом") }));
        server.Tick();
        if (server.LobbyServerName != "Тестовый сервер")
            return false;
        host.Send(LobbyCmd(hostId, c => c with { LobbySettingsUpdate = new LobbySettings(ServerName: "  Новое имя\nв две строки ", Motd: "Добро пожаловать", OpenServer: false) }));
        server.Tick();
        var lobby = guest.ReceiveLatestLobby()!;
        return server.LobbyServerName == "Новое имя в две строки" && lobby.ServerName == "Новое имя в две строки"
            && lobby.Settings is { Motd: "Добро пожаловать", OpenServer: false };
    }

    // The black list: the host bans a network player, the address is refused from then on, and taking it off lets them in again.
    private static bool LobbyV2_BanListRefusesTheAddressUntilRemoved()
    {
        var server = new GameServer("Тестовый сервер", maxPlayers: 4, savePath: null);
        var host = new ClosableConnection();
        var hostId = server.Connect(host);
        var guest = new ClosableConnection();
        var guestId = server.Connect(guest, "203.0.113.9");
        server.Tick();
        host.Send(LobbyCmd(hostId, c => c with { Nickname = "Капитан" }));
        guest.Send(LobbyCmd(guestId, c => c with { Nickname = "Хулиган" }));
        server.Tick();
        host.Send(LobbyCmd(hostId, c => c with { LobbyBanPlayerId = guestId }));
        server.Tick();
        server.Tick();
        var view = host.ReceiveLatestLobby()!;
        if (!server.IsBanned("203.0.113.9") || server.IsBanned("203.0.113.10") || server.PlayerCount != 1 || view.Bans is not [{ Id: "203.0.113.9", Nickname: "Хулиган" }])
            return false;
        // The host's own (addressless) connection cannot be put on the list, even by itself.
        host.Send(LobbyCmd(hostId, c => c with { LobbyBanPlayerId = hostId }));
        server.Tick();
        if (server.BannedAddresses.Count != 1)
            return false;
        host.Send(LobbyCmd(hostId, c => c with { LobbyUnbanId = "203.0.113.9" }));
        server.Tick();
        return !server.IsBanned("203.0.113.9") && host.ReceiveLatestLobby()!.Bans is null;
    }

    // A black list kept in a file survives a restart of the server.
    private static bool LobbyV2_BanListIsKeptBetweenRuns()
    {
        var directory = Path.Combine(Path.GetTempPath(), "anabiosis-bans-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var savePath = Path.Combine(directory, "save.json");
            var first = new GameServer("A", maxPlayers: 4, savePath);
            var host = new ClosableConnection();
            var hostId = first.Connect(host);
            var guest = new ClosableConnection();
            var guestId = first.Connect(guest, "198.51.100.7");
            first.Tick();
            host.Send(LobbyCmd(hostId, c => c with { Nickname = "Капитан" }));
            guest.Send(LobbyCmd(guestId, c => c with { Nickname = "Гость" }));
            first.Tick();
            host.Send(LobbyCmd(hostId, c => c with { LobbyBanPlayerId = guestId }));
            first.Tick();
            var second = new GameServer("A", maxPlayers: 4, savePath);
            return second.IsBanned("198.51.100.7");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // Saved campaigns: listed from the folder beside the save, chosen by the host, and the round that starts is that campaign.
    private static bool LobbyV2_ChosenSavedCampaignIsWhatTheRoundContinues()
    {
        var directory = Path.Combine(Path.GetTempPath(), "anabiosis-slots-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var savePath = Path.Combine(directory, "save.json");
            var source = new World(ShipKind.Custom, null, 4242, true);
            source.StartCampaign();
            source.DebugAddCredits(4000);
            var save = source.CreateSave();
            SaveStore.SaveSlot(save, SaveStore.SlotsDirectoryFor(savePath));

            var server = new GameServer("Тестовый сервер", maxPlayers: 4, savePath);
            var host = new ClosableConnection();
            var hostId = server.Connect(host);
            server.Tick();
            host.Send(LobbyCmd(hostId, c => c with { Nickname = "Капитан" }));
            server.Tick();
            var listed = host.ReceiveLatestLobby()!.Saves;
            if (listed is not [{ Id: "c4242.json" } info] || info.Credits != save.Credits)
                return false;
            // An id that is not a slot's name is ignored.
            host.Send(LobbyCmd(hostId, c => c with { LobbyLoadSaveId = "../save.json" }));
            server.Tick();
            if (host.ReceiveLatestLobby()!.LoadedSaveTitle is not null)
                return false;
            host.Send(LobbyCmd(hostId, c => c with { LobbyLoadSaveId = "c4242.json" }));
            server.Tick();
            if (host.ReceiveLatestLobby()!.LoadedSaveTitle is null)
                return false;
            host.Send(LobbyCmd(hostId, c => c with { LobbyLoadSaveId = "" }));
            server.Tick();
            if (host.ReceiveLatestLobby()!.LoadedSaveTitle is not null)
                return false;
            host.Send(LobbyCmd(hostId, c => c with { LobbyLoadSaveId = "c4242.json" }));
            server.Tick();
            host.Send(LobbyCmd(hostId, c => c with { LobbyStartRoundPressed = true }));
            server.Tick();
            return host.ReceiveLatestSnapshot() is { } world && world.Credits == save.Credits;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static bool LobbyV2_SlotIdsCannotPointElsewhere()
    {
        return SaveStore.IsValidSlotId("c1.json") && SaveStore.IsValidSlotId("c987654.json")
            && !SaveStore.IsValidSlotId("save.json") && !SaveStore.IsValidSlotId("../c1.json") && !SaveStore.IsValidSlotId("c1/../2.json")
            && !SaveStore.IsValidSlotId("c.json") && !SaveStore.IsValidSlotId("c1.json.exe") && !SaveStore.IsValidSlotId("") && !SaveStore.IsValidSlotId(null);
    }

    // A player who started without a lobby (solo) still gets the chosen look, from the first command.
    private static bool LobbyV2_LookArrivesWithACommandInARunningRound()
    {
        var server = new GameServer(ShipKind.Custom);
        var connection = new ClosableConnection();
        var playerId = server.Connect(connection);
        server.Tick();
        var look = new CharacterLook(CharacterGender.Male, 2, 3, 1, 2, 2, 3);
        connection.Send(LobbyCmd(playerId, c => c with { LobbyLook = look }));
        server.Tick();
        return connection.ReceiveLatestSnapshot()?.Characters.FirstOrDefault(c => c.PlayerId == playerId)?.Look == look;
    }
}
