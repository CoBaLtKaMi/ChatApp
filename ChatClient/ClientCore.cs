using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace ChatClient
{
    public class ClientCore
    {
        private TcpClient? _tcpClient;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private bool _isConnected;

        public event Action<string>? OnConnected;
        public event Action<string>? OnDisconnected;
        public event Action<string>? OnMessageReceived;
        public event Action<string>? OnSystemMessage;
        public event Action<string>? OnError;

        public bool IsConnected => _isConnected;

        public void Connect(string ip, int port, string username)
        {
            try
            {
                _tcpClient = new TcpClient();
                _tcpClient.Connect(ip, port);

                var stream = _tcpClient.GetStream();
                _reader = new StreamReader(stream);
                _writer = new StreamWriter(stream) { AutoFlush = true };

                _writer.WriteLine($"/join {username}");

                // Читаем все начальные сообщения от сервера
                string? response;
                while ((response = _reader.ReadLine()) != null)
                {
                    if (response.StartsWith("SERVER:Welcome"))
                    {
                        _isConnected = true;
                        new Thread(Receive) { IsBackground = true }.Start();
                        OnConnected?.Invoke($"Connected to {ip}:{port}");
                        OnSystemMessage?.Invoke(response.Substring(7));
                        break;
                    }
                    else if (response.StartsWith("SERVER:"))
                    {
                        OnSystemMessage?.Invoke(response.Substring(7));
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Connect error: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            _isConnected = false;
            try { _writer?.WriteLine("/leave"); } catch { }
            try { _tcpClient?.Close(); } catch { }
            OnDisconnected?.Invoke("Disconnected");
        }

        public void SendMessage(string msg)
        {
            if (_isConnected && _writer != null)
            {
                try { _writer.WriteLine(msg); }
                catch (Exception ex) { OnError?.Invoke($"Send error: {ex.Message}"); }
            }
        }

        private void Receive()
        {
            while (_isConnected)
            {
                try
                {
                    var msg = _reader?.ReadLine();
                    if (msg == null) break;

                    if (msg.StartsWith("SERVER:"))
                        OnSystemMessage?.Invoke(msg.Substring(7));
                    else
                        OnMessageReceived?.Invoke(msg);
                }
                catch { break; }
            }

            if (_isConnected)
            {
                _isConnected = false;
                OnDisconnected?.Invoke("Connection lost");
            }
        }
    }
}