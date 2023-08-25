using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Network;
using UnityEngine;

namespace MultiplayerMod.Platform.Base.Network.Components;

// ReSharper disable once ClassNeverInstantiated.Global
public class ServerComponent : MonoBehaviour {
    private BaseServer server = null!;
    private void Awake() => server = (BaseServer) Dependencies.Get<IMultiplayerServer>();
    private void Update() => server.Tick();
}
