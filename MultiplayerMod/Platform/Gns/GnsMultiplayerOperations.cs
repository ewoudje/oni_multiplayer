using JetBrains.Annotations;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Multiplayer;
using MultiplayerMod.Network;
using MultiplayerMod.Platform.Gns.Network;

namespace MultiplayerMod.Platform.Gns;

[UsedImplicitly]
public class GnsMultiplayerOperations : IMultiplayerOperations {

    public void Join() {
        Dependencies.Get<IMultiplayerClient>().Connect(new DevServerEndpoint());
    }

}
