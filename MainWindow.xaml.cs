using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DungeonRampageCheat
{
    public partial class MainWindow : Window
    {
        private WallHackService? _wallHackService;
        private DispatcherTimer? _processMonitor;
        private bool _speedEnabled = false;
        private readonly List<IntPtr> _speedAddresses = new();
        private readonly List<IntPtr> _zoomAddresses = new();
        private bool _isScanning = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeLogger();
            LogMessage("🎮 Dungeon Rampage Cheat initialized");
            LogMessage("📝 Created by: tonhowtf");
        }

        private void InitializeLogger()
        {
            LogTextBlock.Inlines.Clear();

            DebugLogger.OnDebugLog += (message, color) =>
            {
                Dispatcher.Invoke(() =>
                {
                    string timestamp = DateTime.Now.ToString("HH:mm:ss");
                    var run = new System.Windows.Documents.Run($"[{timestamp}] {message}\n")
                    {
                        Foreground = color
                    };
                    LogTextBlock.Inlines.Add(run);
                    LogScrollViewer.ScrollToEnd();
                });
            };
        }

        private void LogMessage(string message, Brush? color = null)
        {
            DebugLogger.Log(message, color);
        }

        private async void AttachButton_Click(object sender, RoutedEventArgs e)
        {
            string processName = ProcessNameTextBox.Text.Trim();

            if (string.IsNullOrEmpty(processName))
            {
                MessageBox.Show("Please enter a process name!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AttachButton.IsEnabled = false;
            ScanButton.IsEnabled = false;

            try
            {
                _wallHackService = new WallHackService();
                _wallHackService.OnStatusChanged += (s, msg) => LogMessage(msg);
                _wallHackService.OnError += (s, msg) => LogMessage(msg, Brushes.Red);

                bool connected = await _wallHackService.AttachToGameAsync(processName);

                if (connected)
                {
                    StatusText.Text = $"Connected: {_wallHackService.GetProcessInfo()}";
                    StatusIndicator.Fill = Brushes.LimeGreen;
                    ScanButton.IsEnabled = true;
                    StartProcessMonitoring();
                }
                else
                {
                    StatusText.Text = "Connection failed";
                    StatusIndicator.Fill = Brushes.Red;
                    AttachButton.IsEnabled = true;
                    _wallHackService?.Dispose();
                    _wallHackService = null;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error: {ex.Message}", Brushes.Red);
                StatusText.Text = "Connection error";
                StatusIndicator.Fill = Brushes.Red;
                AttachButton.IsEnabled = true;
                _wallHackService?.Dispose();
                _wallHackService = null;
            }
        }

        private void StartProcessMonitoring()
        {
            _processMonitor = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _processMonitor.Tick += (s, e) =>
            {
                if (_wallHackService != null && !_wallHackService.IsConnected())
                {
                    LogMessage("⚠️ Process terminated. Disconnecting...", Brushes.Orange);
                    DisconnectFromGame();
                }
            };
            _processMonitor.Start();
        }

        private void StopProcessMonitoring()
        {
            _processMonitor?.Stop();
            _processMonitor = null;
        }

        private void DisconnectFromGame()
        {
            StopProcessMonitoring();
            _wallHackService?.Dispose();
            _wallHackService = null;

            StatusText.Text = "Disconnected";
            StatusIndicator.Fill = Brushes.Gray;
            AttachButton.IsEnabled = true;
            ScanButton.IsEnabled = false;
            ApplyAllButton.IsEnabled = false;
            RestoreAllButton.IsEnabled = false;
            MasterToggle.IsEnabled = false;
            MasterToggle.IsChecked = false;
            MapsPanel.Children.Clear();

            _speedEnabled = false;
            _speedAddresses.Clear();
            _zoomAddresses.Clear();
            ToggleSpeedButton.IsEnabled = false;
            ToggleSpeedButton.Content = "▶️ Enable Speed";
            SpeedStatusText.Text = "Status: Not scanned";

            LogMessage("🔓 Disconnected from process");
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (_wallHackService == null) return;

            ScanButton.IsEnabled = false;
            ApplyAllButton.IsEnabled = false;
            RestoreAllButton.IsEnabled = false;

            try
            {
                await _wallHackService.ScanAllMapsAsync();
                PopulateMapsPanel();
                ApplyAllButton.IsEnabled = true;
                RestoreAllButton.IsEnabled = true;
                MasterToggle.IsEnabled = true;
            }
            finally
            {
                ScanButton.IsEnabled = true;
            }
        }

        private void PopulateMapsPanel()
        {
            if (_wallHackService == null) return;

            MapsPanel.Children.Clear();
            var config = _wallHackService.GetConfig();

            foreach (var map in config.Maps)
            {
                var checkbox = new CheckBox
                {
                    Content = $"🗺️ {map.MapName}",
                    Style = (Style)FindResource("ModernToggle"),
                    Tag = map.MapName,
                    IsChecked = map.IsEnabled
                };

                checkbox.Checked += (s, e) =>
                {
                    if (s is CheckBox cb && cb.Tag is string mapName)
                    {
                        _wallHackService?.ApplyWallHack(mapName);
                        UpdateMasterToggleState();
                    }
                };

                checkbox.Unchecked += (s, e) =>
                {
                    if (s is CheckBox cb && cb.Tag is string mapName)
                    {
                        _wallHackService?.RestoreMap(mapName);
                        UpdateMasterToggleState();
                    }
                };

                MapsPanel.Children.Add(checkbox);
            }
        }

        private void UpdateMasterToggleState()
        {
            if (_wallHackService == null) return;

            var config = _wallHackService.GetConfig();
            int enabledCount = config.Maps.Count(m => m.IsEnabled);

            MasterToggle.IsChecked = enabledCount == config.Maps.Count && enabledCount > 0;
        }

        private async void MasterToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (_wallHackService == null) return;

            MasterToggle.IsEnabled = false;
            await _wallHackService.ApplyAllAsync();

            foreach (CheckBox cb in MapsPanel.Children.OfType<CheckBox>())
            {
                cb.IsChecked = true;
            }

            MasterToggle.IsEnabled = true;
        }

        private async void MasterToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_wallHackService == null) return;

            MasterToggle.IsEnabled = false;
            await _wallHackService.RestoreAllAsync();

            foreach (CheckBox cb in MapsPanel.Children.OfType<CheckBox>())
            {
                cb.IsChecked = false;
            }

            MasterToggle.IsEnabled = true;
        }

        private async void ApplyAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_wallHackService == null) return;

            ApplyAllButton.IsEnabled = false;
            await _wallHackService.ApplyAllAsync();
            MasterToggle.IsChecked = true;

            foreach (CheckBox cb in MapsPanel.Children.OfType<CheckBox>())
            {
                cb.IsChecked = true;
            }

            ApplyAllButton.IsEnabled = true;
        }

        private async void RestoreAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_wallHackService == null) return;

            RestoreAllButton.IsEnabled = false;
            await _wallHackService.RestoreAllAsync();
            MasterToggle.IsChecked = false;

            foreach (CheckBox cb in MapsPanel.Children.OfType<CheckBox>())
            {
                cb.IsChecked = false;
            }

            RestoreAllButton.IsEnabled = true;
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            LogTextBlock.Inlines.Clear();
            LogMessage("Log cleared");
        }

        private void CopyLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string logText = new System.Windows.Documents.TextRange(
                    LogTextBlock.ContentStart,
                    LogTextBlock.ContentEnd
                ).Text;

                Clipboard.SetText(logText);
                LogMessage("📋 Log copied to clipboard", Brushes.Green);
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to copy log: {ex.Message}", Brushes.Red);
            }
        }

        private async void ScanSpeedButton_Click(object sender, RoutedEventArgs e)
        {
            if (_wallHackService == null)
            {
                LogMessage("❌ Not connected! Attach to process first.", Brushes.Red);
                return;
            }

            if (_isScanning)
            {
                LogMessage("⚠️ Already scanning, please wait...", Brushes.Orange);
                return;
            }

            _isScanning = true;
            LogMessage("🔍 Scanning Speed Hack patterns (Ranger only)...", Brushes.Yellow);
            SpeedStatusText.Text = "Status: Scanning...";
            SpeedStatusText.Foreground = Brushes.Yellow;

            try
            {
                var addresses = await Task.Run(() =>
                {
                    var memoryManager = _wallHackService.MemoryManager;
                    byte[] pattern = MiscConfig.RangerSpeed.SearchPattern;
                    return memoryManager.ScanMemoryPatternAll(pattern);
                });

                _speedAddresses.Clear();
                _speedAddresses.AddRange(addresses);

                if (_speedAddresses.Count > 0)
                {
                    LogMessage($"✅ Found {_speedAddresses.Count} Speed Hack addresses", Brushes.Green);
                    SpeedStatusText.Text = $"Status: Ready ({_speedAddresses.Count} addresses found)";
                    SpeedStatusText.Foreground = Brushes.LimeGreen;
                    ToggleSpeedButton.IsEnabled = true;
                    SpeedInfoText.Text = $"Found {_speedAddresses.Count} addresses. Click Enable to activate.";
                }
                else
                {
                    LogMessage("❌ No Speed Hack patterns found. Make sure you're playing as Ranger!", Brushes.Red);
                    SpeedStatusText.Text = "Status: Not found";
                    SpeedStatusText.Foreground = Brushes.Red;
                    SpeedInfoText.Text = "No patterns found. Make sure you're in-game as Ranger class.";
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Speed scan error: {ex.Message}", Brushes.Red);
                SpeedStatusText.Text = "Status: Error";
                SpeedStatusText.Foreground = Brushes.Red;
            }
            finally
            {
                _isScanning = false;
            }
        }

        private void ToggleSpeedButton_Click(object sender, RoutedEventArgs e)
        {
            if (_wallHackService == null || _speedAddresses.Count == 0)
            {
                LogMessage("❌ No addresses found. Scan first!", Brushes.Red);
                return;
            }

            var memoryManager = _wallHackService.MemoryManager;

            if (!_speedEnabled)
            {
                byte[] replacement = MiscConfig.RangerSpeed.ReplacePattern;
                int successCount = 0;

                foreach (var address in _speedAddresses)
                {
                    if (memoryManager.WriteMemory(address, replacement))
                    {
                        successCount++;
                    }
                }

                if (successCount > 0)
                {
                    _speedEnabled = true;
                    ToggleSpeedButton.Content = "⏸️ Disable Speed";
                    ToggleSpeedButton.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23));
                    LogMessage($"✅ Speed Hack enabled! ({successCount}/{_speedAddresses.Count} addresses)", Brushes.Green);
                    SpeedStatusText.Text = "Status: ENABLED";
                    SpeedStatusText.Foreground = Brushes.LimeGreen;
                    SpeedInfoText.Text = "Speed hack is active. Movement speed increased!";
                }
                else
                {
                    LogMessage("❌ Failed to enable Speed Hack", Brushes.Red);
                }
            }
            else
            {
                byte[] original = MiscConfig.RangerSpeed.SearchPattern;
                int successCount = 0;

                foreach (var address in _speedAddresses)
                {
                    if (memoryManager.WriteMemory(address, original))
                    {
                        successCount++;
                    }
                }

                if (successCount > 0)
                {
                    _speedEnabled = false;
                    ToggleSpeedButton.Content = "▶️ Enable Speed";
                    ToggleSpeedButton.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x7D, 0x2D));
                    LogMessage($"✅ Speed Hack disabled! ({successCount}/{_speedAddresses.Count} addresses)", Brushes.Green);
                    SpeedStatusText.Text = "Status: Disabled";
                    SpeedStatusText.Foreground = Brushes.Gray;
                    SpeedInfoText.Text = "Speed hack is inactive. Click Enable to activate.";
                }
                else
                {
                    LogMessage("❌ Failed to disable Speed Hack", Brushes.Red);
                }
            }
        }

        private async void ScanZoomButton_Click(object sender, RoutedEventArgs e)
        {
            if (_wallHackService == null)
            {
                LogMessage("❌ Not connected! Attach to process first.", Brushes.Red);
                return;
            }

            if (_isScanning)
            {
                LogMessage("⚠️ Already scanning, please wait...", Brushes.Orange);
                return;
            }

            _isScanning = true;
            LogMessage("🔍 Scanning Zoom Hack (0.63)...", Brushes.Yellow);
            ZoomStatusText.Text = "Status: Scanning...";
            ZoomStatusText.Foreground = Brushes.Yellow;

            try
            {
                var addresses = await Task.Run(() =>
                {
                    var memoryManager = _wallHackService.MemoryManager;
                    double zoomValue = MiscConfig.CameraZoom.SearchValue;
                    byte[] zoomBytes = BitConverter.GetBytes(zoomValue);
                    return memoryManager.ScanMemoryPatternAll(zoomBytes);
                });

                _zoomAddresses.Clear();
                _zoomAddresses.AddRange(addresses);

                if (_zoomAddresses.Count > 0)
                {
                    LogMessage($"✅ Found {_zoomAddresses.Count} Zoom addresses", Brushes.Green);
                    ZoomStatusText.Text = $"Status: Ready ({_zoomAddresses.Count} addresses found)";
                    ZoomStatusText.Foreground = Brushes.LimeGreen;
                    RestoreZoomButton.IsEnabled = true;
                    ZoomSlider.IsEnabled = true;
                    Zoom01Button.IsEnabled = true;
                    Zoom02Button.IsEnabled = true;
                    Zoom03Button.IsEnabled = true;
                    Zoom04Button.IsEnabled = true;
                    Zoom05Button.IsEnabled = true;
                    ZoomInfoText.Text = $"Found {_zoomAddresses.Count} addresses. Use slider or presets!";
                }
                else
                {
                    LogMessage("❌ No Zoom patterns found. Make sure zoom is at default (0.63)!", Brushes.Red);
                    ZoomStatusText.Text = "Status: Not found";
                    ZoomStatusText.Foreground = Brushes.Red;
                    ZoomInfoText.Text = "No patterns found. Make sure you're in-game at default zoom.";
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Zoom scan error: {ex.Message}", Brushes.Red);
                ZoomStatusText.Text = "Status: Error";
                ZoomStatusText.Foreground = Brushes.Red;
            }
            finally
            {
                _isScanning = false;
            }
        }

        private void RestoreZoomButton_Click(object sender, RoutedEventArgs e)
        {
            ZoomSlider.Value = 0.63;
            LogMessage("↩️ Zoom restored to default (0.63)", Brushes.Green);
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_wallHackService == null || !ZoomSlider.IsEnabled || _zoomAddresses.Count == 0)
                return;

            double zoomValue = Math.Round(e.NewValue, 2);
            ZoomValueText.Text = $"Current: {zoomValue:F2}" + (Math.Abs(zoomValue - 0.63) < 0.01 ? " (Default)" : "");

            byte[] zoomBytes = BitConverter.GetBytes(zoomValue);
            var memoryManager = _wallHackService.MemoryManager;
            int successCount = 0;

            foreach (var address in _zoomAddresses)
            {
                if (memoryManager.WriteMemory(address, zoomBytes))
                {
                    successCount++;
                }
            }

            if (successCount > 0)
            {
                LogMessage($"🔭 Zoom set to {zoomValue:F2} ({successCount}/{_zoomAddresses.Count} addresses)", Brushes.Cyan);
            }
        }

        private void ZoomPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tagValue)
            {
                if (double.TryParse(tagValue, out double zoomValue))
                {
                    ZoomSlider.Value = zoomValue;
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            StopProcessMonitoring();
            _wallHackService?.Dispose();
            base.OnClosed(e);
        }
    }
}