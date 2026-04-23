using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ChatClient
{
    public partial class MainWindow : Window
    {
        private ClientCore? _client;
        private readonly ObservableCollection<string> _messages = new();
        private readonly ObservableCollection<string> _onlineUsers = new();

        public MainWindow()
        {
            InitializeComponent();
            MessagesItemsControl.ItemsSource = _messages;
            UsersListBox.ItemsSource = _onlineUsers;
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ServerIpTextBox.Text) ||
                !int.TryParse(PortTextBox.Text, out int port) ||
                string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                MessageBox.Show("Fill all fields", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _client = new ClientCore();

            _client.OnConnected += (msg) =>
                Dispatcher.Invoke(() =>
                {
                    AddSystemMessage(msg, Colors.Green);
                    UpdateUI(true);
                });

            _client.OnDisconnected += (msg) =>
                Dispatcher.Invoke(() =>
                {
                    AddSystemMessage(msg, Colors.Red);
                    UpdateUI(false);
                    _onlineUsers.Clear();
                });

            _client.OnMessageReceived += (msg) =>
                Dispatcher.Invoke(() =>
                {
                    _messages.Add(msg);
                    ScrollDown();
                });

            _client.OnSystemMessage += (msg) =>
                Dispatcher.Invoke(() => ProcessSystemMessage(msg));

            _client.OnError += (msg) =>
                Dispatcher.Invoke(() => AddSystemMessage($"Error: {msg}", Colors.OrangeRed));

            _client.Connect(ServerIpTextBox.Text, port, UsernameTextBox.Text);
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            _client?.Disconnect();
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                SendMessage();
                e.Handled = true;
            }
        }

        private void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(MessageTextBox.Text))
                return;

            _client?.SendMessage(MessageTextBox.Text);
            MessageTextBox.Clear();
        }

        private void AddSystemMessage(string msg, Color color)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _messages.Add($"[{timestamp}] {msg}");
            ScrollDown();
        }

        private void ScrollDown()
        {
            MessageScrollViewer.ScrollToBottom();
        }

        private void ProcessSystemMessage(string msg)
        {
            // Не добавляем системные сообщения о входе/выходе в лог повторно
            // (они уже добавлены через OnSystemMessage в виде Welcome и т.д.)

            // Обработка списка пользователей
            if (msg.StartsWith("Online:"))
            {
                // Формат: "Online: user1, user2, user3"
                var usersStr = msg.Substring(7).Trim();
                if (!string.IsNullOrEmpty(usersStr))
                {
                    var users = usersStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(u => u.Trim())
                        .Where(u => !string.IsNullOrEmpty(u))
                        .ToList();

                    _onlineUsers.Clear();
                    foreach (var user in users)
                    {
                        _onlineUsers.Add(user);
                    }
                }
            }
            else if (msg.Contains("joined"))
            {
                // Формат: "username joined"
                var parts = msg.Split(' ');
                if (parts.Length > 0)
                {
                    var username = parts[0];
                    if (!_onlineUsers.Contains(username))
                    {
                        _onlineUsers.Add(username);
                    }
                }
                AddSystemMessage(msg, Colors.Blue);
            }
            else if (msg.Contains("left"))
            {
                // Формат: "username left"
                var parts = msg.Split(' ');
                if (parts.Length > 0)
                {
                    var username = parts[0];
                    _onlineUsers.Remove(username);
                }
                AddSystemMessage(msg, Colors.Blue);
            }
            else if (msg.StartsWith("Welcome"))
            {
                // Приветственное сообщение уже добавлено
            }
            else
            {
                // Другие системные сообщения
                AddSystemMessage(msg, Colors.Blue);
            }
        }

        private void UpdateUI(bool connected)
        {
            ConnectButton.IsEnabled = !connected;
            DisconnectButton.IsEnabled = connected;
            SendButton.IsEnabled = connected;
            MessageTextBox.IsEnabled = connected;

            ServerIpTextBox.IsEnabled = !connected;
            PortTextBox.IsEnabled = !connected;
            UsernameTextBox.IsEnabled = !connected;

            if (connected)
            {
                MessageTextBox.Focus();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _client?.Disconnect();
            base.OnClosed(e);
        }
    }
}