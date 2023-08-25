using System;
using MultiplayerMod.Multiplayer;

namespace MultiplayerMod.Platform.Gns.Network;

[Serializable]
public record DevPlayer(string Id) : IPlayerIdentity {
    public bool Equals(IPlayerIdentity other) {
        return other is DevPlayer player && player.Equals(this);
    }
}
