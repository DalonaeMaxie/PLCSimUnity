// CdpClient.cs
// Minimal Chrome DevTools Protocol (CDP) client over a WebSocket.
// Thread model: a background task receives messages; events are queued and
// drained on Unity's main thread via DrainEvents().

using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace CdpWebView
{
    public sealed class CdpClient : IDisposable
    {
        public readonly struct CdpEvent
        {
            public readonly string Method;
            public readonly JObject Params;
            public CdpEvent(string method, JObject p) { Method = method; Params = p; }
        }

        readonly ClientWebSocket _ws = new ClientWebSocket();
        readonly CancellationTokenSource _cts = new CancellationTokenSource();
        readonly ConcurrentDictionary<int, TaskCompletionSource<JObject>> _pending =
            new ConcurrentDictionary<int, TaskCompletionSource<JObject>>();
        readonly ConcurrentQueue<CdpEvent> _events = new ConcurrentQueue<CdpEvent>();
        readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        int _nextId;

        // CDP rejects explicit nulls for optional params, so drop them.
        static readonly Newtonsoft.Json.JsonSerializer IgnoreNulls = new Newtonsoft.Json.JsonSerializer
        {
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore
        };

        public bool IsOpen => _ws.State == WebSocketState.Open;

        public async Task ConnectAsync(string webSocketDebuggerUrl)
        {
            _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            await _ws.ConnectAsync(new Uri(webSocketDebuggerUrl), _cts.Token);
            _ = Task.Run(ReceiveLoop);
        }

        /// Send a command and await its result.
        public Task<JObject> Send(string method, object parameters = null)
        {
            int id = Interlocked.Increment(ref _nextId);
            var tcs = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[id] = tcs;

            var msg = new JObject { ["id"] = id, ["method"] = method };
            if (parameters != null) msg["params"] = JObject.FromObject(parameters, IgnoreNulls);
            _ = SendRaw(msg.ToString(Newtonsoft.Json.Formatting.None), id);
            return tcs.Task;
        }

        /// Fire-and-forget (input events, frame acks). Errors are swallowed.
        public void Post(string method, object parameters = null)
        {
            Send(method, parameters).ContinueWith(t => { var _ = t.Exception; },
                TaskContinuationOptions.OnlyOnFaulted);
        }

        async Task SendRaw(string json, int id)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                await _sendLock.WaitAsync(_cts.Token);
                try
                {
                    await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
                }
                finally { _sendLock.Release(); }
            }
            catch (Exception e)
            {
                if (_pending.TryRemove(id, out var tcs)) tcs.TrySetException(e);
            }
        }

        async Task ReceiveLoop()
        {
            var buffer = new byte[1 << 16];
            var sb = new StringBuilder();
            try
            {
                while (!_cts.IsCancellationRequested && _ws.State == WebSocketState.Open)
                {
                    sb.Clear();
                    WebSocketReceiveResult r;
                    do
                    {
                        r = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                        if (r.MessageType == WebSocketMessageType.Close) return;
                        sb.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
                    } while (!r.EndOfMessage);

                    var msg = JObject.Parse(sb.ToString());
                    if (msg.TryGetValue("id", out var idTok))
                    {
                        if (_pending.TryRemove(idTok.Value<int>(), out var tcs))
                        {
                            if (msg["error"] != null)
                                tcs.TrySetException(new Exception("CDP error: " + msg["error"]));
                            else
                                tcs.TrySetResult(msg["result"] as JObject ?? new JObject());
                        }
                    }
                    else if (msg["method"] != null)
                    {
                        _events.Enqueue(new CdpEvent((string)msg["method"], msg["params"] as JObject ?? new JObject()));
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                _events.Enqueue(new CdpEvent("__closed", new JObject { ["reason"] = e.Message }));
            }
            finally
            {
                foreach (var kv in _pending)
                    kv.Value.TrySetCanceled();
                _pending.Clear();
            }
        }

        /// Call from the main thread (e.g. Update) to handle queued events.
        public void DrainEvents(Action<CdpEvent> handler, int max = 32)
        {
            while (max-- > 0 && _events.TryDequeue(out var ev)) handler(ev);
        }

        public void Dispose()
        {
            try { _cts.Cancel(); } catch { }
            try { _ws.Dispose(); } catch { }
        }
    }
}
