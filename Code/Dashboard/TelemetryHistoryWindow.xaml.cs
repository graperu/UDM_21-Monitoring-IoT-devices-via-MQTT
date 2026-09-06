using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Newtonsoft.Json;
using UDM_21.Dashboard.Controllers;
using UDM_21.Dashboard.Models;
using UDM_21.Dashboard.Services;


namespace UDM_21.Dashboard
{
    /// <summary>
    /// Interaction logic for  TelemetryHistoryWindow.xaml
    /// </summary>
    public partial class TelemetryHistoryWindow : Window
    {
            private readonly string _deviceId;
            private readonly TelemetryHistoryManager _historyManager;

            public TelemetryHistoryWindow(
                string deviceId,
                TelemetryHistoryManager historyManager)
            {
                InitializeComponent();

                _deviceId = deviceId;
                _historyManager = historyManager;

                LoadHistory();
            }

            private void LoadHistory()
            {
                var history =
                    _historyManager.GetHistory(_deviceId);

                HistoryListView.ItemsSource = history;
            }

        private void HistoryDelete_Click(object sender, RoutedEventArgs e)
        {
            _historyManager.ClearHistory(_deviceId);
            MessageBox.Show($"Telemetry history for device {_deviceId} has been cleared.", "History Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
