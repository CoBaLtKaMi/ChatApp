using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ChatServer
{
    public class ClientInfo
    {
        public TcpClient TcpClient { get; set; } = null!;
        public StreamWriter Writer { get; set; } = null!;
        public string Username { get; set; } = string.Empty;
    }

    public class ServerCore
    {
        private TcpListener? _listener;
        private readonly Dictionary<string, ClientInfo> _clients = new();
        private readonly object _lockObj = new();
        private bool _isRunning;

        public event Action<string>? OnServerStarted;
        public event Action<string>? OnServerStopped;
        public event Action<string>? OnClientConnected;
        public event Action<string>? OnClientDisconnected;
        public event Action<string, string>? OnMessageReceived;
        public event Action<string>? OnServerLog;

        public int ConnectedClientsCount
        {
            get { lock (_lockObj) return _clients.Count; }
        }

        public void Start(int port)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                _isRunning = true;

                new Thread(ListenForClients) { IsBackground = true }.Start();

                OnServerStarted?.Invoke($"Server started on port {port}");
                Log($"Server started on port {port}");
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();

            lock (_lockObj)
            {
                foreach (var c in _clients.Values)
                {
                    try { c.TcpClient?.Close(); } catch { }
                }
                _clients.Clear();
            }

            OnServerStopped?.Invoke("Server stopped");
            Log("Server stopped");
        }

        private void ListenForClients()
        {
            while (_isRunning)
            {
                try
                {
                    var client = _listener?.AcceptTcpClient();
                    if (client != null)
                    {
                        Log("New client connecting...");
                        new Thread(() => HandleClient(client)) { IsBackground = true }.Start();
                    }
                }
                catch (SocketException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log($"Accept error: {ex.Message}");
                }
            }
        }

        private void HandleClient(TcpClient tcpClient)
        {
            StreamReader? reader = null;
            StreamWriter? writer = null;
            string? username = null;

            try
            {
                var stream = tcpClient.GetStream();
                reader = new StreamReader(stream);
                writer = new StreamWriter(stream) { AutoFlush = true };

                Log("Waiting for /join...");
                var joinMsg = reader.ReadLine();
                Log($"Received: {joinMsg}");

                if (string.IsNullOrEmpty(joinMsg) || !joinMsg.StartsWith("/join "))
                {
                    writer.WriteLine("SERVER:Use /join USERNAME");
                    return;
                }

                username = joinMsg.Substring(6).Trim();
                if (string.IsNullOrEmpty(username))
                {
                    writer.WriteLine("SERVER:Username required");
                    return;
                }

                lock (_lockObj)
                {
                    if (_clients.ContainsKey(username))
                    {
                        writer.WriteLine("SERVER:Username taken");
                        return;
                    }
                    _clients.Add(username, new ClientInfo { TcpClient = tcpClient, Writer = writer, Username = username });
                }

                OnClientConnected?.Invoke(username);
                Log($"{username} connected");

                writer.WriteLine($"SERVER:Welcome {username}!");
                Broadcast($"SERVER:{username} joined");
                SendOnlineUsers(writer);

                while (_isRunning && tcpClient.Connected)
                {
                    var msg = reader.ReadLine();
                    if (msg == null) break;

                    OnMessageReceived?.Invoke(username, msg);
                    Log($"{username}: {msg}");

                    if (msg == "/users")
                        SendOnlineUsers(writer);
                    else if (msg.StartsWith("/pm "))
                    {
                        var parts = msg.Substring(4).Split(' ', 2);
                        if (parts.Length >= 2)
                            SendPrivate(username, parts[0], parts[1]);
                    }
                    else if (msg != "/leave")
                        Broadcast($"{username}: {msg}");
                }
            }
            catch (Exception ex)
            {
                Log($"HandleClient error: {ex.Message}");
            }
            finally
            {
                if (username != null)
                {
                    lock (_lockObj) { _clients.Remove(username); }
                    OnClientDisconnected?.Invoke(username);
                    Broadcast($"SERVER:{username} left");
                    Log($"{username} disconnected");
                }
                try { tcpClient.Close(); } catch { }
            }
        }

        private void Broadcast(string msg)
        {
            lock (_lockObj)
            {
                foreach (var c in _clients.Values)
                {
                    try { c.Writer.WriteLine(msg); } catch { }
                }
            }
        }

        private void SendPrivate(string from, string to, string msg)
        {
            lock (_lockObj)
            {
                if (_clients.TryGetValue(to, out var target))
                    target.Writer.WriteLine($"[PM from {from}]: {msg}");
                else if (_clients.TryGetValue(from, out var source))
                    source.Writer.WriteLine($"SERVER:User {to} not found");
            }
        }

        private void SendOnlineUsers(StreamWriter writer)
        {
            lock (_lockObj)
            {
                writer.WriteLine($"SERVER:Online: {string.Join(", ", _clients.Keys)}");
            }
        }

        private void Log(string msg)
        {
            OnServerLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] {msg}");
        }
    }
}