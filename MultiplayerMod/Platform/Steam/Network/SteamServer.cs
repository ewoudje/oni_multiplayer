﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MultiplayerMod.Core.Collections;
using MultiplayerMod.Core.Extensions;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.Core.Scheduling;
using MultiplayerMod.Core.Unity;
using MultiplayerMod.Multiplayer;
using MultiplayerMod.Network;
using MultiplayerMod.Network.Events;
using MultiplayerMod.Platform.Steam.Network.Components;
using MultiplayerMod.Platform.Steam.Network.Messaging;
using Steamworks;
using UnityEngine;
using static Steamworks.Constants;
using static Steamworks.EResult;
using static Steamworks.ESteamNetConnectionEnd;
using static Steamworks.ESteamNetworkingConnectionState;

namespace MultiplayerMod.Platform.Steam.Network;

public class SteamServer : IMultiplayerServer {

    public MultiplayerServerState State { private set; get; } = MultiplayerServerState.Stopped;

    public IMultiplayerEndpoint Endpoint {
        get {
            if (State != MultiplayerServerState.Started)
                throw new NetworkPlatformException("Server isn't started");

            return new SteamServerEndpoint(lobby.Id);
        }
    }

    public List<IPlayerIdentity> Players => new(players.Keys);

    public event Action<ServerStateChangedEventArgs>? StateChanged;
    public event Action<IPlayerIdentity>? PlayerConnected;
    public event Action<IPlayerIdentity>? PlayerDisconnected;
    public event Action<CommandReceivedEventArgs>? CommandReceived;

    private readonly Core.Logging.Logger log = LoggerFactory.GetLogger<SteamServer>();

    private Callback<SteamServersConnected_t> steamServersConnectedCallback = null!;
    private TaskCompletionSource<bool> lobbyCompletionSource = null!;
    private TaskCompletionSource<bool> steamServersCompletionSource = null!;
    private CancellationTokenSource callbacksCancellationTokenSource = null!;

    private HSteamNetPollGroup pollGroup;
    private HSteamListenSocket listenSocket;
    private readonly NetworkMessageProcessor messageProcessor = new();
    private readonly NetworkMessageFactory messageFactory = new();
    private readonly SteamNetworkingConfigValue_t[] networkConfig = { Configuration.SendBufferSize() };
    private Callback<SteamNetConnectionStatusChangedCallback_t> connectionStatusChangedCallback = null!;

    private readonly Dictionary<IPlayerIdentity, HSteamNetConnection> players = new();
    private readonly IPlayerIdentity currentPlayer = new SteamPlayerIdentity(SteamUser.GetSteamID());

    private readonly UnityTaskScheduler scheduler;
    private readonly SteamLobby lobby;

    private GameObject? gameObject;

    public SteamServer(SteamLobby lobby, UnityTaskScheduler scheduler) {
        this.lobby = lobby;
        this.scheduler = scheduler;
    }

    public void Start() {
        if (!SteamManager.Initialized)
            throw new NetworkPlatformException("Steam API is not initialized");

        log.Debug("Starting...");
        SetState(MultiplayerServerState.Preparing);
        try {
            Initialize();
        } catch (Exception) {
            Reset();
            SetState(MultiplayerServerState.Error);
            throw;
        }
        gameObject = UnityObject.CreateStaticWithComponent<SteamServerComponent>();
    }

    public void Stop() {
        if (State <= MultiplayerServerState.Stopped)
            throw new NetworkPlatformException("Server isn't started");

        log.Debug("Stopping...");
        if (gameObject != null)
            UnityObject.Destroy(gameObject);
        Reset();
        SetState(MultiplayerServerState.Stopped);
    }

    public void Tick() {
        switch (State) {
            case MultiplayerServerState.Starting:
            case MultiplayerServerState.Started:
                GameServer.RunCallbacks();
                SteamGameServerNetworkingSockets.RunCallbacks();
                ReceiveMessages();
                break;
        }
    }

    public void Send(IPlayerIdentity player, IMultiplayerCommand command) {
        var connections = new SingletonCollection<HSteamNetConnection>(players[player]);
        SendCommand(command, MultiplayerCommandOptions.None, connections);
    }

    public void Send(IMultiplayerCommand command, MultiplayerCommandOptions options) {
        IEnumerable<KeyValuePair<IPlayerIdentity, HSteamNetConnection>> recipients = players;
        if (options.HasFlag(MultiplayerCommandOptions.SkipHost))
            recipients = recipients.Where(entry => !entry.Key.Equals(currentPlayer));

        SendCommand(command, options, recipients.Select(it => it.Value));
    }

    private void SetState(MultiplayerServerState state) {
        State = state;
        StateChanged?.Invoke(new ServerStateChangedEventArgs(state));
    }

    private void Initialize() {
        steamServersConnectedCallback = Callback<SteamServersConnected_t>
            .CreateGameServer(_ => ConnectedToSteamCallback());

        lobbyCompletionSource = new TaskCompletionSource<bool>();
        steamServersCompletionSource = new TaskCompletionSource<bool>();
        callbacksCancellationTokenSource = new CancellationTokenSource();
        Task.WhenAll(lobbyCompletionSource.Task, steamServersCompletionSource.Task)
            .ContinueWith(
                _ => OnServerStarted(),
                callbacksCancellationTokenSource.Token,
                TaskContinuationOptions.None,
                scheduler
            );

        lobby.OnCreate += OnLobbyCreated;
        lobby.Create();

        var version = typeof(SteamServer).Assembly.GetName().Version.ToString();
        log.Debug($"Initializing game server version {version}");
        if (!GameServer.Init(0, 27020, 27015, EServerMode.eServerModeNoAuthentication, version))
            throw new NetworkPlatformException("Game server init failed");

        SteamGameServer.SetModDir("OxygenNotIncluded");
        SteamGameServer.SetProduct("OxygenNotIncluded Multiplayer");
        SteamGameServer.SetGameDescription("OxygenNotIncluded Multiplayer");

        SteamGameServer.LogOnAnonymous();

        SteamNetworkingUtils.GetRelayNetworkStatus(out var status);
        if (status.m_eAvail != ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Current)
            SteamNetworkingUtils.InitRelayNetworkAccess();

        connectionStatusChangedCallback = Callback<SteamNetConnectionStatusChangedCallback_t>
            .CreateGameServer(HandleConnectionStatusChanged);

        listenSocket = SteamGameServerNetworkingSockets.CreateListenSocketP2P(0, networkConfig.Length, networkConfig);
        pollGroup = SteamGameServerNetworkingSockets.CreatePollGroup();

        SetState(MultiplayerServerState.Starting);
    }

    private void Reset() {
        lobby.OnCreate -= OnLobbyCreated;
        lobby.Leave();

        connectionStatusChangedCallback.Unregister();
        SteamGameServerNetworkingSockets.DestroyPollGroup(pollGroup);
        SteamGameServerNetworkingSockets.CloseListenSocket(listenSocket);

        GameServer.Shutdown();

        steamServersConnectedCallback.Unregister();

        lobbyCompletionSource.TrySetCanceled();
        steamServersCompletionSource.TrySetCanceled();
        callbacksCancellationTokenSource.Cancel();
    }

    private void OnServerStarted() {
        lobby.GameServerId = SteamGameServer.GetSteamID();
        SetState(MultiplayerServerState.Started);
    }

    private void OnLobbyCreated() => lobbyCompletionSource.SetResult(true);

    private void ConnectedToSteamCallback() => steamServersCompletionSource.SetResult(true);

    private void ReceiveMessages() {
        var messages = new IntPtr[128];
        var messagesCount = SteamGameServerNetworkingSockets.ReceiveMessagesOnPollGroup(pollGroup, messages, 128);
        for (var i = 0; i < messagesCount; i++) {
            var steamMessage = Marshal.PtrToStructure<SteamNetworkingMessage_t>(messages[i]);
            var message = messageProcessor.Process(
                steamMessage.m_conn.m_HSteamNetConnection,
                steamMessage.GetNetworkMessageHandle()
            );
            if (message != null) {
                IPlayerIdentity player = new SteamPlayerIdentity(steamMessage.m_identityPeer.GetSteamID());
                if (message.Options.HasFlag(MultiplayerCommandOptions.ExecuteOnServer)) {
                    CommandReceived?.Invoke(new CommandReceivedEventArgs(player, message.Command));
                } else {
                    var connections = players.Where(it => !it.Key.Equals(player)).Select(it => it.Value);
                    SendCommand(message.Command, message.Options, connections);
                }
            }
            SteamNetworkingMessage_t.Release(messages[i]);
        }
    }

    private void SendCommand(
        IMultiplayerCommand command,
        MultiplayerCommandOptions options,
        IEnumerable<HSteamNetConnection> connections
    ) {
        var sequence = messageFactory.Create(command, options);
        sequence.ForEach(handle => connections.ForEach(connection => Send(handle, connection)));
    }

    private void Send(INetworkMessageHandle handle, HSteamNetConnection connection) {
        var result = SteamGameServerNetworkingSockets.SendMessageToConnection(
            connection,
            handle.Pointer,
            handle.Size,
            k_nSteamNetworkingSend_Reliable,
            out _
        );
        if (result != k_EResultOK)
            log.Error($"Failed to send message, result: {result}");
    }

    private void HandleConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t data) {
        var connection = data.m_hConn;
        var clientSteamId = data.m_info.m_identityRemote.GetSteamID();
        var state = data.m_info.m_eState;
        switch (state) {
            case k_ESteamNetworkingConnectionState_Connecting:
                if (TryAcceptConnection(connection, clientSteamId))
                    PlayerConnected?.Invoke(new SteamPlayerIdentity(clientSteamId));
                break;
            case k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
            case k_ESteamNetworkingConnectionState_ClosedByPeer:
                CloseConnection(connection, clientSteamId);
                break;
            default:
                log.Trace(
                    () => $"Unhandled connection state change {data.m_eOldState} -> {state} for client {clientSteamId}"
                );
                break;
        }
    }

    private bool TryAcceptConnection(HSteamNetConnection connection, CSteamID clientSteamId) {
        var result = SteamGameServerNetworkingSockets.AcceptConnection(connection);
        if (result != k_EResultOK) {
            log.Error($"Unable to accept connection from {clientSteamId} (error {result})");
            var reason = (int) k_ESteamNetConnectionEnd_AppException_Generic + (int) result;
            SteamGameServerNetworkingSockets.CloseConnection(
                connection,
                reason,
                pszDebug: $"Failed to accept connection (error {result})",
                bEnableLinger: false
            );
            return false;
        }
        SteamGameServerNetworkingSockets.SetConnectionPollGroup(connection, pollGroup);
        players[new SteamPlayerIdentity(clientSteamId)] = connection;
        log.Debug($"Connection accepted from {clientSteamId}");
        return true;
    }

    private void CloseConnection(HSteamNetConnection connection, CSteamID clientSteamId) {
        PlayerDisconnected?.Invoke(new SteamPlayerIdentity(clientSteamId));
        SteamGameServerNetworkingSockets.CloseConnection(
            connection,
            (int) k_ESteamNetConnectionEnd_App_Generic,
            null,
            false
        );
        players.Remove(new SteamPlayerIdentity(clientSteamId));
        Debug.Log($"Connection closed for {clientSteamId}");
    }

}
