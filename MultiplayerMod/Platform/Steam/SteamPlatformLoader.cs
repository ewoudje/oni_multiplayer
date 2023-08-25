﻿extern alias ValveSockets;
using HarmonyLib;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Loader;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.Core.Unity;
using MultiplayerMod.Multiplayer;
using MultiplayerMod.Network;
using MultiplayerMod.Platform.Steam.Network;
using MultiplayerMod.Platform.Steam.Network.Components;

namespace MultiplayerMod.Platform.Steam;

// ReSharper disable once UnusedType.Global
public class SteamPlatformLoader : IModComponentLoader {

    private readonly Core.Logging.Logger log = LoggerFactory.GetLogger<SteamPlatformLoader>();

    public void OnLoad(Harmony harmony) {
        if (!PlatformSelector.IsSteamPlatform())
            return;

        log.Info("Steam platform detected");
        Dependencies.Register<IMultiplayerOperations, SteamMultiplayerOperations>();

        Dependencies.Register<IMultiplayerServer, SteamServer>();
        Dependencies.Register<IMultiplayerClient, SteamClient>();
        Dependencies.Register<SteamLobby>();
        UnityObject.CreateStaticWithComponent<LobbyJoinRequestComponent>();
    }

}
