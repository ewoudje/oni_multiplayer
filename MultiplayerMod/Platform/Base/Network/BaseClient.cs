using System;
using MultiplayerMod.Core.Unity;
using MultiplayerMod.Multiplayer;
using MultiplayerMod.Network;
using MultiplayerMod.Network.Events;
using UnityEngine;

namespace MultiplayerMod.Platform.Base.Network;

public abstract class BaseClient : IMultiplayerClient {
    public MultiplayerClientState State { get; private set; } = MultiplayerClientState.Disconnected;

    public abstract IPlayerIdentity Player { get; }

    public event Action<MultiplayerClientState>? StateChanged;
    public event Action<CommandReceivedEventArgs>? CommandReceived;

    protected void OnCommandReceived(CommandReceivedEventArgs args) {
        CommandReceived?.Invoke(args);
    }

    protected GameObject GameObject = null!;

    public abstract void Connect(IMultiplayerEndpoint endpoint);

    public void Disconnect() {
        if (State <= MultiplayerClientState.Disconnected)
            throw new NetworkPlatformException("Client not connected");

        if (GameObject)
            UnityObject.Destroy(GameObject);
        DoDisconnect();
    }

    protected virtual void DoDisconnect() { }

    public abstract void Tick();

    public abstract void Send(
        IMultiplayerCommand command,
        MultiplayerCommandOptions options = MultiplayerCommandOptions.None
    );

    protected void SetState(MultiplayerClientState status) {
        State = status;
        StateChanged?.Invoke(status);
    }
}
