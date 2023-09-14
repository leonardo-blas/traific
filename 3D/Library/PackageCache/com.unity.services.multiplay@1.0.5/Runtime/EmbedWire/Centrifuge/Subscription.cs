using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.Services.Core;
using Unity.Services.Core.Threading.Internal;

namespace Unity.Services.Wire.Internal
{
    /// <summary>
    /// Subscription represents a subscription to a channel
    /// </summary>
    /// <typeparam name="TPayload"> The TPayload class representation of the payloads sent to your channel</typeparam>
    class Subscription : IChannel
    {
        public event Action<string> MessageReceived;
        public event Action<byte[]> BinaryMessageReceived;
        public event Action KickReceived;
        public event Action<SubscriptionState> NewStateReceived;
        public event Action<TaskCompletionSource<bool>> UnsubscribeReceived;
        public event Action<TaskCompletionSource<bool>> SubscribeReceived;
        public event Action<string> ErrorReceived;
        public event Action DisposeReceived;

        public string Channel;
        public bool IsConnected { get => SubscriptionState == SubscriptionState.Synced; }

        public UInt64 Offset;
        public string Epoch;
        public SubscriptionState SubscriptionState = SubscriptionState.Unsynced;

        IChannelTokenProvider m_TokenProvider;

        bool m_Disposed;

        string ChannelDisplay => string.IsNullOrEmpty(Channel) ? "unknown" : Channel;

        public Subscription(IChannelTokenProvider tokenProvider)
        {
            m_TokenProvider = tokenProvider;
            Offset = 0;
            m_Disposed = false;
        }

        public async Task<string> RetrieveTokenAsync()
        {
            ChannelToken tokenData;
            try
            {
                tokenData = await m_TokenProvider.GetTokenAsync();
            }
            catch (Exception e)
            {
                throw new RequestFailedException((int)WireErrorCode.TokenRetrieverFailed,
                    "Exception caught while running the token retriever.", e);
            }

            ValidateTokenData(tokenData.ChannelName, tokenData.Token);
            Channel = tokenData.ChannelName;
            return tokenData.Token;
        }

        private void ValidateTokenData(string channel, string token)
        {
            if (string.IsNullOrEmpty(channel))
            {
                throw new EmptyChannelException();
            }

            if (string.IsNullOrEmpty(token))
            {
                throw new EmptyTokenException();
            }

            if (!string.IsNullOrEmpty(Channel) && Channel != channel)
            {
                throw new ChannelChangedException(channel, Channel);
            }
        }

        public void OnMessageReceived(Reply reply)
        {
            if (reply.error != null && reply.error.code != 0)
            {
                var errMsg = $"Received publication with error : code: {reply.error.code}, message: {reply.error.message}";
                Logger.LogError(errMsg);
                ErrorReceived?.Invoke(errMsg);
                return;
            }

            if (reply.result?.publications?.Length > 0)
            {
                foreach (var publication in reply.result.publications)
                {
                    try
                    {
                        MessageReceived?.Invoke(publication.data.payload);
                        BinaryMessageReceived?.Invoke(Encoding.UTF8.GetBytes(publication.data.payload));
                    }
                    finally
                    {
                        Offset = publication.offset;
                    }
                }
                return;
            }

            if (reply.result?.data?.data?.payload != null)
            {
                MessageReceived?.Invoke(reply.result.data.data.payload);
                Offset++;
                return;
            }

            // For multiplay, we won't have the wire envelope (the payload field)
            // we send directly what's inside the data field
            var jObject = JObject.Parse(reply.originalString);
            Logger.LogVerbose("MessageReceived, invoking event with reply result!");
            MessageReceived?.Invoke(jObject.SelectToken("result.data.data").ToString());
        }

        internal void OnUnsubscriptionComplete()
        {
            SubscriptionState = SubscriptionState.Unsubscribed;
            NewStateReceived?.Invoke(SubscriptionState);
        }

        public void OnKickReceived()
        {
            if (KickReceived != null)
            {
                SubscriptionState = SubscriptionState.Unsubscribed;
                NewStateReceived?.Invoke(SubscriptionState);
                KickReceived?.Invoke();
            }
        }

        public void OnConnectivityChangeReceived(bool connected)
        {
            SubscriptionState = connected ? SubscriptionState.Synced : SubscriptionState.Unsynced;
            NewStateReceived?.Invoke(SubscriptionState);
        }

        ~Subscription()
        {
            // Do nothing
        }

        public void Dispose()
        {
            Dispose(true);
        }

        // Deterministic calls to Dispose will trigger an unsubscribe.
        // Non deterministic calls to Dispose won't.
        internal void Dispose(bool disposing)
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;

            try
            {
                // we want to actually unsubscribe when Dispose is called in a deterministic way:
                if (disposing)
                {
                    UnsubscribeReceived?.Invoke(new TaskCompletionSource<bool>());
                }
                // when called from a finalizer, we only get rid of the memory
                else
                {
                    DisposeReceived?.Invoke();
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }

            m_TokenProvider = null;

            // cleaning up event listeners
            DisposeReceived = null;
            UnsubscribeReceived = null;
            MessageReceived = null;
            BinaryMessageReceived = null;
            NewStateReceived = null;
            KickReceived = null;
            SubscribeReceived = null;
            ErrorReceived = null;
        }

        public Task SubscribeAsync()
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(ChannelDisplay);
            }
            Logger.LogVerbose($"Subscribing to {ChannelDisplay}");
            SubscriptionState = SubscriptionState.Subscribing;
            NewStateReceived?.Invoke(SubscriptionState);
            var completionSource = new TaskCompletionSource<bool>();
            SubscribeReceived?.Invoke(completionSource);
            return completionSource.Task;
        }

        public Task UnsubscribeAsync()
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(ChannelDisplay);
            }
            Logger.LogVerbose($"Unsubscribing from {ChannelDisplay}");
            var completionSource = new TaskCompletionSource<bool>();
            UnsubscribeReceived?.Invoke(completionSource);
            return completionSource.Task;
        }

        internal void OnError(string reason)
        {
            SubscriptionState = SubscriptionState.Error;
            NewStateReceived?.Invoke(SubscriptionState);
            ErrorReceived?.Invoke(reason);
        }
    }
}
