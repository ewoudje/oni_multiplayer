﻿extern alias ValveSockets;
using HarmonyLib;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Loader;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.Multiplayer;
using MultiplayerMod.Network;
using MultiplayerMod.Platform.Gns.Network;
using ValveSockets::Valve.Sockets;

namespace MultiplayerMod.Platform.Gns;

[HarmonyPatch(typeof(Global), "OnApplicationQuit")]
public class CleanupPatch {
    public static void Postfix() {
        Library.Deinitialize();
    }
};

// ReSharper disable once UnusedType.Global
public class GnsPlatformLoader : IModComponentLoader {

    private readonly Core.Logging.Logger log = LoggerFactory.GetLogger<GnsPlatformLoader>();

    public void OnLoad(Harmony harmony) {
        if (PlatformSelector.IsSteamPlatform())
            return;

        log.Info("No steam platform detected");
        Dependencies.Register<IMultiplayerOperations, GnsMultiplayerOperations>();

        Library.Initialize();
        Dependencies.Register<IMultiplayerServer, GnsServer>();
        Dependencies.Register<IMultiplayerClient, GnsClient>();
    }

}
