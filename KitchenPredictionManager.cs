using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace TerrariaKitchen
{
    public class KitchenPredictionManager : IDisposable
    {
        private ClientWebSocket? _socket;
        internal event EventHandler<Exception>? OnErrorOccurred;
        internal event EventHandler<string?>? OnDisconnected;
        internal Func<string, Task<bool>>? OnConnectedHandler;
        internal event EventHandler<PredictionLockEventData>? OnPredictionEnd;

        private bool _reconnectRequested = false, _reconnectCompleted = false;
        public string? SessionId { get; private set; }

        public bool Ready { get; private set; } = false;

        private bool GotWelcome { get; set; } = false;

        private readonly Uri target_url = new Uri("wss://eventsub.wss.twitch.tv/ws");

        private Queue<string> _previousTokens = new Queue<string>(10);

        public async Task<bool> StartSubscription()
        {
            if (_socket?.State == WebSocketState.Open)
            {
                if (!Ready && GotWelcome)
                {
                    Ready = await (OnConnectedHandler?.Invoke(SessionId ?? string.Empty) ?? Task.FromResult(false));
                }
                return true;
            }
            _socket = new ClientWebSocket();
            try
            {
                if (_socket.State is WebSocketState.Open or WebSocketState.Connecting)
                    return true;

                await _socket.ConnectAsync(target_url, CancellationToken.None);

#pragma warning disable 4014
                Task.Run(async () => await ProcessDataAsync(_socket));
#pragma warning restore 4014

                return _socket.State == WebSocketState.Open;
            }
            catch (Exception ex)
            {
                OnErrorOccurred?.Invoke(this, ex);
            }
            return false;
        }

        public async Task<bool> DisconnectAsync(string? reason = "")
        {
            try
            {
                if (_socket?.State is WebSocketState.Open or WebSocketState.Connecting)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    _socket.Dispose();
                    _socket = null;
                    OnDisconnected?.Invoke(this, reason);
                }
                Ready = false;
                GotWelcome = false;

                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred?.Invoke(this, ex);
                return false;
            }
        }

        private async Task ProcessDataAsync(ClientWebSocket socket)
        {
            const int minimumBufferSize = 256;
            var storeSize = 4096;
            var decoder = Encoding.UTF8.GetDecoder();

            var store = MemoryPool<byte>.Shared.Rent(storeSize).Memory;
            var buffer = MemoryPool<byte>.Shared.Rent(minimumBufferSize).Memory;

            var payloadSize = 0;

            while (socket?.State == WebSocketState.Open)
            {
                try
                {
                    ValueWebSocketReceiveResult receiveResult;
                    do
                    {
                        receiveResult = await socket.ReceiveAsync(buffer, CancellationToken.None);

                        if (payloadSize + receiveResult.Count >= storeSize)
                        {
                            storeSize += Math.Max(4096, receiveResult.Count);
                            var newStore = MemoryPool<byte>.Shared.Rent(storeSize).Memory;
                            store.CopyTo(newStore);
                            store = newStore;
                        }

                        buffer.CopyTo(store[payloadSize..]);

                        payloadSize += receiveResult.Count;
                    } while (!receiveResult.EndOfMessage);

                    switch (receiveResult.MessageType)
                    {
                        case WebSocketMessageType.Text:
                            {
                                var intermediate = MemoryPool<char>.Shared.Rent(payloadSize).Memory;

                                if (payloadSize == 0)
                                    continue;

                                decoder.Convert(store.Span[..payloadSize], intermediate.Span, true, out _, out var charsCount, out _);
                                var message = intermediate[..charsCount];
                                ProcessContents(message.Span.ToString());
                                payloadSize = 0;
                                break;
                            }
                        case WebSocketMessageType.Binary:
                            break;
                        case WebSocketMessageType.Close:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception ex)
                {
                    OnErrorOccurred?.Invoke(this, ex);
                    break;
                }
            }
        }

        private async Task ReconnectAsync(string? uri)
        {
            var reconnectUrl = target_url;
            if (!string.IsNullOrEmpty(uri))
            {
                reconnectUrl = new Uri(uri);
            }

            if (_reconnectRequested)
            {
                var reconnectClient = new ClientWebSocket();

                await reconnectClient.ConnectAsync(reconnectUrl, CancellationToken.None);


                for (var i = 0; i < 200; i++)
                {
                    if (_socket?.State != WebSocketState.Open) // Stop reconnecting if we are disconnected for any reason.
                        break;

                    if (_reconnectCompleted)
                    {
                        var oldRunningClient = _socket;
                        _socket = reconnectClient;

                        if (oldRunningClient.State == WebSocketState.Open)
                            await oldRunningClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
                        oldRunningClient.Dispose();

                        _reconnectRequested = false;
                        _reconnectCompleted = false;

                        return;
                    }

                    await Task.Delay(100);
                }

                OnErrorOccurred?.Invoke(this, new Exception(""));
                return;
            }

            // Do a full reconnect if we fail the normal reconnect
            if (_socket?.State == WebSocketState.Open)
            {
                await DisconnectAsync("Failed to reconnect");
            }
        }

        private async void ProcessContents(string v)
        {
            _lastReceived = DateTimeOffset.Now;

            var json = JsonDocument.Parse(v);
            var metadata = json.RootElement.GetProperty("metadata");
            var messageType = metadata.GetProperty("message_type").GetString();
            var message_id = metadata.GetProperty("message_id").GetString();
            if (message_id == null || _previousTokens.Contains(message_id))
            {
                return;
            }
            _previousTokens.Enqueue(message_id);
            if (_previousTokens.Count >= 10)
            {
                _previousTokens.Dequeue();
            }

            switch (messageType)
            {
                case "session_welcome":
                    if (_reconnectRequested)
                        _reconnectCompleted = true;

                    var welcome_session = json.RootElement.GetProperty("payload").GetProperty("session");
                    SessionId = welcome_session.GetProperty("id").GetString();
                    var keepAliveTimeout = welcome_session.GetProperty("keepalive_timeout_seconds").GetDouble() * 1.2;
                    _keepAliveTimeout = TimeSpan.FromSeconds(keepAliveTimeout);

                    GotWelcome = true;
                    if (!Ready)
                    {
                        Ready = await (OnConnectedHandler?.Invoke(SessionId ?? string.Empty) ?? Task.FromResult(false));
                    }
                    break;
                case "session_disconnect":
                    var dc_session = json.RootElement.GetProperty("payload").GetProperty("session");
                    var dc_reason = "Disconnected from server";
                    if (dc_session.TryGetProperty("disconnect_reason", out var reason))
                    {
                        dc_reason = reason.GetString();
                    }
                    await DisconnectAsync(dc_reason);
                    break;
                case "session_reconnect":
                    var rc_session = json.RootElement.GetProperty("payload").GetProperty("session");
                    _reconnectRequested = true;
                    string? s = null;
                    if (rc_session.TryGetProperty("reconnect_url", out var reconnect_url))
                    {
                        s = reconnect_url.GetString();
                    }
                    await Task.Run(async () => await ReconnectAsync(s));
                    break;
                case "notification":
                    var payload = json.RootElement.GetProperty("payload");
                    var subscriptionType = payload.GetProperty("subscription").GetProperty("type").GetString();
                    if (subscriptionType == "channel.prediction.lock" && payload.TryGetProperty("event", out var subEvent))
                    {
                        var outcomes = subEvent.GetProperty("outcomes");
                        var responseData = new PredictionLockEventData
                        {
                            Id = subEvent.GetProperty("id").ToString(),
                        };
                        for (int i = 0; i < outcomes.GetArrayLength(); ++i)
                        {
                            var outcome = outcomes[i];
                            if (outcome.TryGetProperty("id", out var outcome_id) && outcome.TryGetProperty("title", out var outcome_title) && outcome_id.GetString() is string a && outcome_title.GetString() is string b)
                            {
                                responseData.Outcomes[b] = a;
                            }
                        }
                        OnPredictionEnd?.Invoke(this, responseData);
                    }
                    break;
                case "revocation":
                    var revocation_type = json.RootElement.GetProperty("payload").GetProperty("subscription").GetProperty("type").GetString();
                    if (revocation_type == "channel.prediction.lock")
                    {
                        var revocation_reason = metadata.GetProperty("payload").GetProperty("subscription").GetProperty("status").GetString();
                        await DisconnectAsync($"EventSub Revoked - {revocation_reason}");
                    }
                    break;
                default:
                    break;
            }
        }

        private DateTimeOffset _lastReceived = DateTimeOffset.MinValue;
        private TimeSpan _keepAliveTimeout = TimeSpan.Zero;

        private async Task KeepAlive()
        {
            while (_socket?.State == WebSocketState.Open)
            {
                if (_lastReceived != DateTimeOffset.MinValue)
                    if (_keepAliveTimeout != TimeSpan.Zero)
                        if (_lastReceived.Add(_keepAliveTimeout) < DateTimeOffset.Now)
                            break;

                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            await DisconnectAsync(_socket?.State == WebSocketState.Open ? "Connection timed out." : string.Empty);
        }

        public void Dispose()
        {
            Ready = false;
            GotWelcome = false;
            GC.SuppressFinalize(this);
            _socket?.Dispose();
        }

        public class PredictionLockEventData
        {
            public string Id { get; set; }

            public Dictionary<string, string> Outcomes { get; set; } = new Dictionary<string, string>();
        }
    }
}
