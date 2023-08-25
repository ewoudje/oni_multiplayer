using System;
using MultiplayerMod.Multiplayer;
using Steamworks;

namespace MultiplayerMod.Platform.Steam.Network;

[Serializable]
public record SteamPlayer(CSteamID Id) : IPlayerIdentity {
    public bool Equals(IPlayerIdentity other) {
        return other is SteamPlayer player && player.Equals(this);
    }
}
