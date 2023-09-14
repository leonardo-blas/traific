using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Core.Telemetry.Internal;
using Unity.Services.Core.Threading.Internal;
using UnityEditor;

namespace Unity.Services.Wire.Internal
{
    class Client : IWire
    {
        internal enum ConnectionState
        {
            Disconnected,
            Connected,
            Connecting,
            Disconnecting
        }

        public readonly ISubscriptionRepository SubscriptionRepository;

        TaskCompletionSource<ConnectionState> m_ConnectionCompletionSource;
        TaskCompletionSource<ConnectionState> m_DisconnectionCompletionSource;
        internal ConnectionState m_ConnectionState = ConnectionState.Disconnected;
        CancellationTokenSource m_PingCancellationSource;
        Task<bool> m_PingTask;
        IWebSocket m_WebsocketClient;

        internal IBackoffStrategy m_Backoff;
        readonly CommandManager m_CommandManager;
        readonly Configuration m_Config;
        readonly IMetrics m_Metrics;
        readonly IUnityThreadUtils m_ThreadUtils;

        event Action m_OnConnected;

        bool m_DirectClient = false;
        string m_DirectSubscriptionChannel = null;
        bool m_WebsocketInitialized = false;

        bool m_WantConnected = false;

        public Client(Configuration config, Core.Scheduler.Internal.IActionScheduler actionScheduler, IMetrics metrics,
                      IUnityThreadUtils threadUtils)
        {
            m_ThreadUtils = threadUtils;
            m_Config = config;
            m_Metrics = metrics;
            SubscriptionRepository = new ConcurrentDictSubscriptionRepository();
            SubscriptionRepository.SubscriptionCountChanged += (int subscriptionCount) =>
            {
                m_Metrics?.SendGaugeMetric("subscription_count", subscriptionCount);
                Logger.LogVerbose($"Subscription count changed: {subscriptionCount}");
            };
            m_Backoff = new ExponentialBackoffStrategy();
            m_CommandManager = new CommandManager(config, actionScheduler);
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += PlayModeStateChanged;
#endif
        }

        public void SetupDirectClient()
        {
            m_DirectClient = true;
        }

#if UNITY_EDITOR
        async void PlayModeStateChanged(PlayModeStateChange state)
        {
            try
            {
                if (state != PlayModeStateChange.ExitingPlayMode)
                {
                    return;
                }
                Logger.Log("Exiting playmode, disconnecting, and cleaning subscription repo.");
                await DisconnectAsync();

                foreach (var sub in SubscriptionRepository.GetAll())
                {
                    sub.Value.Dispose();
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

#endif

        async Task<Reply> SendCommandAsync(UInt32 id, Message command)
        {
            var time = DateTime.Now;
            var tags = new Dictionary<string, string> {{"method", command.GetMethod()}};
            m_CommandManager.RegisterCommand(id);

            m_WebsocketClient.Send(command.GetBytes());

            Logger.LogVerbose($"sending {command.GetMethod()} command: {command.Serialize()}");
            try
            {
                var reply = await m_CommandManager.WaitForCommandAsync(id);
                tags.Add("result", "success");
                m_Metrics?.SendHistogramMetric("command", (DateTime.Now - time).TotalMilliseconds, tags);
                return reply;
            }
            catch (Exception)
            {
                tags.Add("result", "failure");
                m_Metrics?.SendHistogramMetric("command", (DateTime.Now - time).TotalMilliseconds, tags);
                throw;
            }
        }

        /// <summary>
        /// Ping is a routine responsible for sending a Ping command to centrifuge at a regular interval.
        /// The main objective is to detect connectivity issues with the server.
        /// It could also be used to measure the command round trip latency.
        /// </summary>
        /// <typeparam name="TPayload"> The TPayload class representation of the payloads sent to your channel</typeparam>
        /// <returns> Return true if the routine exits because the system noticed the ws connection was closed by itself, false if an error happened during the Ping command</returns>
        async Task<bool> PingAsync()
        {
            if (m_PingCancellationSource != null)
            {
                throw new WireUnexpectedException("ping cancellation already exists");
            }

            m_PingCancellationSource = new CancellationTokenSource();
            while (true)
            {
                Command<PingRequest> command = new Command<PingRequest>(Message.Method.PING, new PingRequest());
                try
                {
                    await SendCommandAsync(command.id, command);
                }
                catch (CommandInterruptedException)
                {
                    OnPingInterrupted(null);
                    return false;
                }
                catch (Exception e)
                {
                    OnPingInterrupted(e);
                    return false;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(m_Config.PingIntervalInSeconds),
                        m_PingCancellationSource.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            m_PingCancellationSource = null;

            return true;
        }

        private void OnPingInterrupted(Exception exception)
        {
            if (exception != null)
            {
                Logger.LogError("Exception caught during Ping command: " + exception.Message);
            }

            m_WebsocketClient.Close();
            m_PingCancellationSource = null;
        }

        internal async Task DisconnectAsync()
        {
            if (m_DisconnectionCompletionSource != null)
            {
                await m_DisconnectionCompletionSource.Task;
                return;
            }

            m_WantConnected = false;
            if (m_WebsocketClient == null)
            {
                ChangeConnectionState(ConnectionState.Disconnected);
                return;
            }

            m_DisconnectionCompletionSource = new TaskCompletionSource<ConnectionState>();
            ChangeConnectionState(ConnectionState.Disconnecting);
            m_WebsocketClient.Close();
            await m_DisconnectionCompletionSource.Task;
        }

        internal async Task ResetAsync(bool reconnect)
        {
            await DisconnectAsync();
            m_CommandManager.Clear();
            SubscriptionRepository.Clear();
            if (reconnect)
            {
                await ConnectAsync();
            }
        }

        public async Task ConnectAsync()
        {
            m_WantConnected = true;
            Logger.LogVerbose("Connection initiated. Checking state prior to connection.");
            while (m_ConnectionState == ConnectionState.Disconnecting)
            {
                Logger.LogVerbose(
                    "Disconnection already in progress. Waiting for disconnection to complete before proceeding.");
                await m_DisconnectionCompletionSource.Task;
            }

            while (m_ConnectionState == ConnectionState.Connecting)
            {
                Logger.LogVerbose("Connection already in progress. Waiting for connection to complete.");
                await m_ConnectionCompletionSource.Task;
            }

            if (m_ConnectionState == ConnectionState.Connected)
            {
                Logger.LogVerbose("Already connected.");
                return;
            }

            ChangeConnectionState(ConnectionState.Connecting);

            // initialize websocket object
            InitWebsocket();

            // Connect to the websocket server
            Logger.Log($"Attempting connection on: {m_Config.address}");
            m_WebsocketClient.Connect();
            await m_ConnectionCompletionSource.Task;
        }

        void InitWebsocket()
        {
            Logger.LogVerbose("Initializing Websocket.");
            if (m_WebsocketInitialized)
            {
                return;
            }
            m_WebsocketInitialized = true;

            // use the eventual websocket override instead of the default one
            m_WebsocketClient = m_Config.WebSocket ?? WebSocketFactory.CreateInstance(m_Config.address);

            //  Add OnOpen event listener
            m_WebsocketClient.OnOpen += () => m_ThreadUtils.PostAsync(OnWebsocketOpen);

            // Add OnMessage event listener
            m_WebsocketClient.OnMessage += data => m_ThreadUtils.PostAsync(() => OnWebsocketMessage(data));

            // Add OnError event listener
            m_WebsocketClient.OnError += errMsg => m_ThreadUtils.PostAsync(() => OnWebsocketError(errMsg));

            // Add OnClose event listener
            m_WebsocketClient.OnClose += code => m_ThreadUtils.PostAsync(() => OnWebsocketClose(code));
        }

        internal async void OnWebsocketOpen()
        {
            try
            {
                Logger.Log($"Websocket connected to : {m_Config.address}. Initiating Wire handshake.");
                var subscriptionRequests = await SubscribeRequest.getRequestFromRepo(SubscriptionRepository);

                var request = new ConnectRequest(m_Config?.token?.AccessToken ?? string.Empty, subscriptionRequests);
                var command = new Command<ConnectRequest>(Message.Method.CONNECT, request);
                try
                {
                    var reply = await SendCommandAsync(command.id, command);
                    m_Backoff.Reset();
                    SubscriptionRepository.RecoverSubscriptions(reply);
                    ChangeConnectionState(ConnectionState.Connected);
                }
                catch (CommandInterruptedException exception)
                {
                    // Wire handshake failed
                    m_ConnectionCompletionSource.TrySetException(
                        new ConnectionFailedException(
                            $"Socket closed during connection attempt: {exception.m_Code}"));
                    m_WebsocketClient.Close();
                }
                catch (Exception exception)
                {
                    // Unknown exception caught during connection
                    m_ConnectionCompletionSource.TrySetException(exception);
                    m_WebsocketClient.Close();
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

        internal void OnWebsocketMessage(byte[] payload)
        {
            try
            {
                m_Metrics?.SendSumMetric("message_received", 1);
                Logger.LogVerbose("WS received message: " + Encoding.UTF8.GetString(payload));
                var messages = BatchMessages
                    .SplitMessages(payload); // messages can be batched so we need to split them..
                foreach (var message in messages)
                {
                    var reply = Reply.FromJson(message);

                    if (reply.id > 0)
                    {
                        HandleCommandReply(reply);
                    }
                    else if (reply.result?.type > 0)
                    {
                        HandlePushMessage(reply);
                    }
                    else
                    {
                        HandlePublications(reply);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

        void OnWebsocketError(string msg)
        {
            m_Metrics?.SendSumMetric("websocket_error");
            Logger.LogError($"Websocket connection error: {msg}");
        }

        internal async void OnWebsocketClose(WebSocketCloseCode originalCode)
        {
            try
            {
                var code = (CentrifugeCloseCode)originalCode;
                Logger.Log("Websocket closed with code: " + code);
                ChangeConnectionState(ConnectionState.Disconnected);
                m_CommandManager.OnDisconnect(new CommandInterruptedException($"websocket disconnected: {code}",
                    code));
                if (m_DisconnectionCompletionSource != null)
                {
                    m_DisconnectionCompletionSource.SetResult(ConnectionState.Disconnected);
                    m_DisconnectionCompletionSource = null;
                }

                if (m_WantConnected && ShouldReconnect(code))
                {
                    // TokenVerificationFailed is a special Wire custom error that happens when the token verification failed on server side
                    // to prevent any rate limitation on UAS the server will wait a specified amount of time before retrying therefore it's useless
                    // to try again too early from the client.
                    var secondsUntilNextAttempt = (int)originalCode == 4333 ? 10.0f : m_Backoff.GetNext(); // TODO: get rid of the cast when the close code gets public
                    Logger.LogVerbose($"Retrying websocket connection in : {secondsUntilNextAttempt} s");
                    await Task.Delay(TimeSpan.FromSeconds(secondsUntilNextAttempt));
                    await ConnectAsync();
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

        private bool ShouldReconnect(CentrifugeCloseCode code)
        {
            switch (code)
            {
                // irrecoverable error codes
                case CentrifugeCloseCode.WebsocketUnsupportedData:
                case CentrifugeCloseCode.WebsocketMandatoryExtension:
                case CentrifugeCloseCode.InvalidToken:
                case CentrifugeCloseCode.ForceNoReconnect:
                    return false;
                case CentrifugeCloseCode.WebsocketNotSet:
                case CentrifugeCloseCode.WebsocketNormal:
                case CentrifugeCloseCode.WebsocketAway:
                case CentrifugeCloseCode.WebsocketProtocolError:
                case CentrifugeCloseCode.WebsocketUndefined:
                case CentrifugeCloseCode.WebsocketNoStatus:
                case CentrifugeCloseCode.WebsocketAbnormal:
                case CentrifugeCloseCode.WebsocketInvalidData:
                case CentrifugeCloseCode.WebsocketPolicyViolation:
                case CentrifugeCloseCode.WebsocketTooBig:
                case CentrifugeCloseCode.WebsocketServerError:
                case CentrifugeCloseCode.WebsocketTlsHandshakeFailure:
                case CentrifugeCloseCode.Normal:
                case CentrifugeCloseCode.Shutdown:
                case CentrifugeCloseCode.BadRequest:
                case CentrifugeCloseCode.InternalServerError:
                case CentrifugeCloseCode.Expired:
                case CentrifugeCloseCode.SubscriptionExpired:
                case CentrifugeCloseCode.Stale:
                case CentrifugeCloseCode.Slow:
                case CentrifugeCloseCode.WriteError:
                case CentrifugeCloseCode.InsufficientState:
                case CentrifugeCloseCode.ForceReconnect:
                case CentrifugeCloseCode.ConnectionLimit:
                case CentrifugeCloseCode.ChannelLimit:
                // case CentrifugeCloseCode.TokenVerificationFailed:
                default:
                    return true;
            }
        }

        void ChangeConnectionState(ConnectionState state)
        {
            var tags = new Dictionary<string, string> {{"state", state.ToString()}, };
            m_Metrics?.SendSumMetric("connection_state_change", 1, tags);
            m_ConnectionState = state;

            switch (state)
            {
                case ConnectionState.Disconnected:
                    Logger.LogVerbose("Wire disconnected.");
                    SubscriptionRepository.OnSocketClosed();
                    m_PingCancellationSource?.Cancel();
                    break;
                case ConnectionState.Connected:
                    Logger.LogVerbose("Wire connected.");
                    m_ConnectionCompletionSource.SetResult(ConnectionState.Connected);
                    m_ConnectionCompletionSource = null;
                    if (m_PingTask == null || m_PingTask.IsCompleted)
                    {
                        m_PingTask = PingAsync(); // start ping pong thread
                    }
                    else
                    {
                        // TODO: report something wrong
                        throw new WireUnexpectedException("Wire connected but encountered a prior non-null, not completed ping operation.");
                    }
                    m_OnConnected?.Invoke();
                    m_OnConnected = null;
                    break;
                case ConnectionState.Connecting:
                    Logger.LogVerbose("Wire connecting...");
                    m_ConnectionCompletionSource = new TaskCompletionSource<ConnectionState>();

                    break;
                case ConnectionState.Disconnecting:
                    Logger.LogVerbose("Wire is disconnecting");

                    break;
                default:
                    Logger.LogError("ConnectionState default case label!");
                    throw new NotImplementedException();
            }
        }

        // Handle publications from a channel
        void HandlePublications(Reply reply)
        {
            if (string.IsNullOrEmpty(reply.result.channel))
            {
                throw new NoChannelPublicationException(reply.originalString);
            }

            var subscription = GetSubscriptionForReply(reply);
            if (subscription == null)
            {
                Logger.LogError(
                    $"The Wire server is sending publications related to an unknown channel: {reply.result.channel}.");
                return;
            }

            subscription.OnMessageReceived(reply);
        }

        // Handle push actions emitted from the server
        void HandlePushMessage(Reply reply)
        {
            var tags = new Dictionary<string, string> {{"push_type", reply.result.type.ToString()}};
            m_Metrics?.SendSumMetric("push_received", 1, tags);
            var subscription = GetSubscriptionForReply(reply);
            if (subscription == null)
            {
                Logger.LogError($"The Wire server is sending push messages of type[{reply.result.type}] related to an unknown channel: {reply.result.channel}.");
                return;
            }
            switch (reply.result.type)
            {
                case PushType.UNSUB: // force unsubscribe from server
                {
                    subscription.OnKickReceived();
                    SubscriptionRepository.RemoveSub(subscription);
                    break;
                }
                case PushType.MESSAGE:
                {
                    Logger.LogVerbose($"PushMessage[{reply.originalString}]");
                    subscription.OnMessageReceived(reply);
                    break;
                }
                default:
                    Logger.LogError("Not implemented type: " + reply.result.type);
                    // TODO: find a way of reporting this
                    break;
            }
        }

        Subscription GetSubscriptionForReply(Reply reply)
        {
            var channel = reply.result.channel;
            var subscription = SubscriptionRepository.GetSub(reply.result.channel);
            if (subscription == null)
            {
                Logger.LogWarning($"Could not get subscription with channel[{channel}] from subscription repository!");
                if (m_DirectSubscriptionChannel != null)
                {
                    Logger.LogVerbose($"Getting Direct Subscription Channel[{m_DirectSubscriptionChannel}]");
                    subscription = SubscriptionRepository.GetSub(m_DirectSubscriptionChannel);
                }
            }
            if (subscription == null)
            {
                Logger.LogWarning($"Could not get direct subscription channel[{m_DirectSubscriptionChannel}] Defaulting to first subscription!");
                if (channel == ".")
                {
                    var subscriptions = (ConcurrentDictSubscriptionRepository)SubscriptionRepository;
                    var subscriptionKeyValuePairs = subscriptions.Subscriptions.ToArray();
                    Logger.LogVerbose($"We have subscriptions.Length[{subscriptionKeyValuePairs.Length}]");
                    if (subscriptionKeyValuePairs.Length > 0)
                    {
                        subscription = subscriptionKeyValuePairs[0].Value;
                        Logger.LogVerbose($"Push Message with channel[{channel}] received. Defaulting to first subscription[{subscription.Channel}]!");
                    }
                    else
                    {
                        Logger.LogVerbose($"Push Message with channel[{channel}] received. Defaulting to first subscription, but no subscription has been created!");
                    }
                }
            }
            return subscription;
        }

        // Handle replies from commands issued by the client
        void HandleCommandReply(Reply reply)
        {
            m_CommandManager.OnCommandReplyReceived(reply);
        }

        async Task SubscribeAsync(Subscription subscription)
        {
            if (m_ConnectionState != ConnectionState.Connected)
            {
                var tcs = new TaskCompletionSource<bool>();
                m_OnConnected += () =>
                {
                    tcs.SetResult(true);
                };
                try
                {
                    await ConnectAsync();
                }
                catch (Exception e)
                {
                    Logger.Log("Could not subscribe, issue while trying to connect. Subscription will resume when a connection is made.");
                    Logger.LogException(e);
                }
                await tcs.Task;
            }
            try
            {
                var token = await subscription.RetrieveTokenAsync();

                if (m_DirectClient)
                {
                    if (SubscriptionRepository.ServerHasSubscription(subscription))
                    {
                        Logger.LogVerbose($"Promoting Subscription[{subscription.Channel}]");
                        SubscriptionRepository.PromoteSubscriptionHandle(subscription);
                        m_DirectSubscriptionChannel = subscription.Channel;
                        return;
                    }
                }

                if (SubscriptionRepository.IsAlreadySubscribed(subscription))
                {
                    throw new AlreadySubscribedException(subscription.Channel);
                }

                var recover = SubscriptionRepository.IsRecovering(subscription);
                var request = new SubscribeRequest
                {
                    channel = subscription.Channel, token = token, recover = recover, offset = subscription.Offset
                };
                var command = new Command<SubscribeRequest>(Message.Method.SUBSCRIBE, request);
                var reply = await SendCommandAsync(command.id, command);

                subscription.Epoch = reply.result.epoch;
                SubscriptionRepository.OnSubscriptionComplete(subscription, reply);
            }
            catch (Exception exception)
            {
                subscription.OnError($"Subscription failed: {exception.Message}");
                throw;
            }
        }

        public IChannel CreateChannel(IChannelTokenProvider tokenProvider)
        {
            var subscription = new Subscription(tokenProvider);
            subscription.UnsubscribeReceived += async(TaskCompletionSource<bool> completionSource) =>
            {
                try
                {
                    if (SubscriptionRepository.IsAlreadySubscribed(subscription))
                    {
                        await UnsubscribeAsync(subscription);
                    }
                    else
                    {
                        SubscriptionRepository.RemoveSub(subscription);
                    }

                    completionSource.SetResult(true);
                }
                catch (Exception e)
                {
                    // TODO: find a way of reporting this
                    Logger.LogException(e);
                    completionSource.SetException(e);
                }
            };
            subscription.SubscribeReceived += async(TaskCompletionSource<bool> completionSource) =>
            {
                try
                {
                    await SubscribeAsync(subscription);
                    completionSource.SetResult(true);
                }
                catch (Exception e)
                {
                    completionSource.SetException(e);
                }
            };
            subscription.KickReceived += () =>
            {
                SubscriptionRepository.RemoveSub(subscription);
            };
            subscription.DisposeReceived += () =>
            {
                SubscriptionRepository.RemoveSub(subscription);
            };
            return subscription;
        }

        async Task UnsubscribeAsync(Subscription subscription)
        {
            if (!SubscriptionRepository.IsAlreadySubscribed(subscription))
            {
                throw new AlreadyUnsubscribedException(subscription.Channel);
            }

            var request = new UnsubscribeRequest {channel = subscription.Channel, };

            var command = new Command<UnsubscribeRequest>(Message.Method.UNSUBSCRIBE, request);
            await SendCommandAsync(command.id, command);
            SubscriptionRepository.RemoveSub(subscription);
        }
    }
}
