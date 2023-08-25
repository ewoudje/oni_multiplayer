using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Network;
using UnityEngine;

namespace MultiplayerMod.Platform.Base.Network.Components;

// ReSharper disable once ClassNeverInstantiated.Global
public class ClientComponent : MonoBehaviour {
    private BaseClient client = null!;
    private void Awake() => client = (BaseClient) Dependencies.Get<IMultiplayerClient>();
    private void Update() => client.Tick();
}
