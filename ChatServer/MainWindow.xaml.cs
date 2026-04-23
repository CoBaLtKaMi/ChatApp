using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace ChatServer
{
    public partial class MainWindow : Window
    {
        private ServerCore? _server;
        private readonly ObservableCollection<string> _logMessages;

        public MainWindow()
        {
            InitializeComponent();
            _logMessages = new ObservableCollection<string>();
            LogListBox.ItemsSource = _logMessages;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(PortTextBox.Text, out int port))
            {
                MessageBox.Show("Invalid port number");
                return;
            }

            _server = new ServerCore();
            _server.OnServerStarted += (msg) => Dispatcher.Invoke(() => AddLog(msg));
            _server.OnServerStopped += (msg) => Dispatcher.Invoke(() => AddLog(msg));
            _server.OnClientConnected += (user) => Dispatcher.Invoke(() => { AddLog($"Connected: {user}"); UpdateStatus(); });
            _server.OnClientDisconnected += (user) => Dispatcher.Invoke(() => { AddLog($"Disconnected: {user}"); UpdateStatus(); });
            _server.OnMessageReceived += (user, msg) => Dispatcher.Invoke(() => AddLog($"{user}: {msg}"));
            _server.OnServerLog += (msg) => Dispatcher.Invoke(() => AddLog(msg));

            _server.Start(port);

            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            PortTextBox.IsEnabled = false;
            StatusTextBlock.Text = "Running";
            StatusTextBlock.Foreground = new SolidColorBrush(Colors.Green);
            UpdateStatus();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _server?.Stop();
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            PortTextBox.IsEnabled = true;
            StatusTextBlock.Text = "Stopped";
            StatusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
            UpdateStatus();
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            _logMessages.Clear();
        }

        private void AddLog(string message)
        {
            _logMessages.Add(message);
            if (LogListBox.Items.Count > 0)
                LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
        }

        private void UpdateStatus()
        {
            ClientsCountTextBlock.Text = _server?.ConnectedClientsCount.ToString() ?? "0";
        }

        protected override void OnClosed(EventArgs e)
        {
            _server?.Stop();
            base.OnClosed(e);
        }
    }
}