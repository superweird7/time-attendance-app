using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ZKTecoManager.Services
{
    /// <summary>
    /// WebSocket service for real-time updates (live attendance feed, notifications)
    /// </summary>
    public class WebSocketService
    {
        private static WebSocketService _instance;
        private static readonly object _lock = new object();

        private HttpListener _listener;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isRunning;
        private int _port = 8081;

        // Connected clients
        private readonly ConcurrentDictionary<string, WebSocket> _clients = new ConcurrentDictionary<string, WebSocket>();

        public static WebSocketService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new WebSocketService();
                    }
                }
                return _instance;
            }
        }

        public bool IsRunning => _isRunning;
        public int Port => _port;
        public int ConnectedClients => _clients.Count;

        public event EventHandler<string> OnLog;

        public bool Start(int port = 8081)
        {
            if (_isRunning) return true;

            _port = port;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://+:{_port}/");
                _listener.Start();

                _cancellationTokenSource = new CancellationTokenSource();
                _isRunning = true;

                Task.Run(() => ListenAsync(_cancellationTokenSource.Token));

                Log($"WebSocket server started on port {_port}");
                return true;
            }
            catch (HttpListenerException)
            {
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://localhost:{_port}/");
                    _listener.Start();

                    _cancellationTokenSource = new CancellationTokenSource();
                    _isRunning = true;

                    Task.Run(() => ListenAsync(_cancellationTokenSource.Token));

                    Log($"WebSocket server started on localhost:{_port}");
                    return true;
                }
                catch (Exception ex2)
                {
                    Log($"Failed to start WebSocket server: {ex2.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to start WebSocket server: {ex.Message}");
                return false;
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _cancellationTokenSource?.Cancel();

            // Close all client connections
            foreach (var client in _clients)
            {
                try
                {
                    client.Value.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", CancellationToken.None).Wait(1000);
                }
                catch { }
            }
            _clients.Clear();

            _listener?.Stop();
            _listener?.Close();
            _isRunning = false;

            Log("WebSocket server stopped");
        }

        private async Task ListenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _isRunning)
            {
                try
                {
                    var context = await _listener.GetContextAsync();

                    if (context.Request.IsWebSocketRequest)
                    {
                        _ = Task.Run(() => HandleWebSocketAsync(context));
                    }
                    else
                    {
                        // Return info for regular HTTP requests
                        context.Response.StatusCode = 200;
                        context.Response.ContentType = "application/json";
                        var info = JsonSerializer.Serialize(new
                        {
                            service = "WebSocket Server",
                            status = "running",
                            connectedClients = _clients.Count,
                            wsUrl = $"ws://localhost:{_port}/"
                        });
                        var buffer = Encoding.UTF8.GetBytes(info);
                        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        context.Response.Close();
                    }
                }
                catch (Exception) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log($"WebSocket error: {ex.Message}");
                }
            }
        }

        private async Task HandleWebSocketAsync(HttpListenerContext context)
        {
            WebSocketContext wsContext = null;
            string clientId = Guid.NewGuid().ToString();

            try
            {
                wsContext = await context.AcceptWebSocketAsync(null);
                var webSocket = wsContext.WebSocket;

                _clients.TryAdd(clientId, webSocket);
                Log($"Client connected: {clientId} (Total: {_clients.Count})");

                // Send welcome message
                await SendToClientAsync(clientId, new
                {
                    type = "connected",
                    clientId,
                    message = "Connected to ZKTeco real-time updates",
                    timestamp = DateTime.Now.ToString("o")
                });

                // Handle incoming messages
                var buffer = new byte[4096];
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnected", CancellationToken.None);
                        break;
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await HandleClientMessage(clientId, message);
                    }
                }
            }
            catch (WebSocketException)
            {
                // Client disconnected
            }
            catch (Exception ex)
            {
                Log($"WebSocket client error: {ex.Message}");
            }
            finally
            {
                _clients.TryRemove(clientId, out _);
                Log($"Client disconnected: {clientId} (Total: {_clients.Count})");
            }
        }

        private async Task HandleClientMessage(string clientId, string message)
        {
            try
            {
                var data = JsonSerializer.Deserialize<JsonElement>(message);
                string type = data.TryGetProperty("type", out var t) ? t.GetString() : "";

                switch (type)
                {
                    case "ping":
                        await SendToClientAsync(clientId, new { type = "pong", timestamp = DateTime.Now.ToString("o") });
                        break;

                    case "subscribe":
                        // Client wants to subscribe to specific events
                        await SendToClientAsync(clientId, new { type = "subscribed", message = "Subscribed to updates" });
                        break;

                    default:
                        await SendToClientAsync(clientId, new { type = "echo", original = message });
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"Error handling message: {ex.Message}");
            }
        }

        /// <summary>
        /// Broadcast attendance event to all connected clients
        /// </summary>
        public async Task BroadcastAttendanceAsync(string badgeNumber, string employeeName, string punchTime, string punchType)
        {
            var message = new
            {
                type = "attendance",
                badge = badgeNumber,
                name = employeeName,
                time = punchTime,
                punchType,
                timestamp = DateTime.Now.ToString("o")
            };

            await BroadcastAsync(message);
        }

        /// <summary>
        /// Broadcast device status change
        /// </summary>
        public async Task BroadcastDeviceStatusAsync(string deviceName, string ip, bool isOnline)
        {
            var message = new
            {
                type = "device_status",
                device = deviceName,
                ip,
                status = isOnline ? "online" : "offline",
                timestamp = DateTime.Now.ToString("o")
            };

            await BroadcastAsync(message);
        }

        /// <summary>
        /// Broadcast notification to all clients
        /// </summary>
        public async Task BroadcastNotificationAsync(string title, string content, string level = "info")
        {
            var message = new
            {
                type = "notification",
                title,
                content,
                level,
                timestamp = DateTime.Now.ToString("o")
            };

            await BroadcastAsync(message);
        }

        /// <summary>
        /// Broadcast a message to all connected clients
        /// </summary>
        public async Task BroadcastAsync(object message)
        {
            var json = JsonSerializer.Serialize(message);
            var buffer = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(buffer);

            foreach (var client in _clients)
            {
                try
                {
                    if (client.Value.State == WebSocketState.Open)
                    {
                        await client.Value.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
                catch
                {
                    // Client disconnected, will be cleaned up
                    _clients.TryRemove(client.Key, out _);
                }
            }
        }

        /// <summary>
        /// Send message to specific client
        /// </summary>
        public async Task SendToClientAsync(string clientId, object message)
        {
            if (_clients.TryGetValue(clientId, out var webSocket) && webSocket.State == WebSocketState.Open)
            {
                var json = JsonSerializer.Serialize(message);
                var buffer = Encoding.UTF8.GetBytes(json);
                await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        private void Log(string message)
        {
            OnLog?.Invoke(this, message);
            System.Diagnostics.Debug.WriteLine($"[WebSocket] {message}");
        }
    }
}
