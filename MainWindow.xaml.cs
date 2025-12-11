using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DungeonRampageCheat
{
    public partial class MainWindow : Window
    {
        private WallHackService? _wallHackService;
        private DispatcherTimer? _processMonitor;
        private bool _speedRangerEnabled = false;
        private bool _speedBerserkerEnabled = false;
        private readonly List<IntPtr> _speedRangerAddresses = new();
        private readonly List<IntPtr> _speedBerserkerAddresses = new();
        private readonly List<IntPtr> _zoomAddresses = new();
        private bool _isScanning = false;
        private InfiniteRangeMelee? _infiniteRangeMelee;
        private string _gameInstallPath = "";

        public MainWindow()
        {
            InitializeComponent();
            InitializeLogger();
            LogMessage("🎮 Dungeon Rampage Cheat initialized");
            LogMessage("📝 Created by: tonhowtf");
            AutoDetectGamePath();
        }

        private void AutoDetectGamePath()
        {
            LogMessage("🔍 Auto-detecting game installation...", Brushes.Yellow);

            string[] possiblePaths = new[]
            {
                @"C:\Program Files (x86)\Steam\steamapps\common\DungeonRampage",
                @"C:\Program Files\Steam\steamapps\common\DungeonRampage",
                @"D:\Steam\steamapps\common\DungeonRampage",
                @"D:\SteamLibrary\steamapps\common\DungeonRampage",
                @"E:\Steam\steamapps\common\DungeonRampage",
                @"F:\Steam\steamapps\common\DungeonRampage",
                @"G:\Steam\steamapps\common\DungeonRampage",
                @"C:\Games\DungeonRampage",
                @"C:\Program Files\DungeonRampage",
                @"C:\Program Files (x86)\DungeonRampage"
            };

            foreach (var path in possiblePaths)
            {
                if (TrySetGamePath(path))
                {
                    LogMessage($"✅ Game found automatically: {path}", Brushes.Green);
                    return;
                }
            }

            string steamPath = GetSteamInstallPath();
            if (!string.IsNullOrEmpty(steamPath))
            {
                string dungeonPath = System.IO.Path.Combine(steamPath, "steamapps", "common", "DungeonRampage");
                if (TrySetGamePath(dungeonPath))
                {
                    LogMessage($"✅ Game found via Steam registry: {dungeonPath}", Brushes.Green);
                    return;
                }

                string[] steamLibraryFolders = GetSteamLibraryFolders(steamPath);
                foreach (var libraryFolder in steamLibraryFolders)
                {
                    string dungeonLibraryPath = System.IO.Path.Combine(libraryFolder, "steamapps", "common", "DungeonRampage");
                    if (TrySetGamePath(dungeonLibraryPath))
                    {
                        LogMessage($"✅ Game found in Steam library: {dungeonLibraryPath}", Brushes.Green);
                        return;
                    }
                }
            }

            LogMessage("⚠️ Game not found automatically. Use Browse button to locate it manually.", Brushes.Orange);
            RangeStatusText.Text = "Status: Not found - Browse manually";
            RangeStatusText.Foreground = Brushes.Orange;
        }

        private string GetSteamInstallPath()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    if (key != null)
                    {
                        var installPath = key.GetValue("InstallPath")?.ToString();
                        if (!string.IsNullOrEmpty(installPath))
                            return installPath;
                    }
                }

                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    if (key != null)
                    {
                        var installPath = key.GetValue("InstallPath")?.ToString();
                        if (!string.IsNullOrEmpty(installPath))
                            return installPath;
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        private string[] GetSteamLibraryFolders(string steamPath)
        {
            var libraries = new List<string>();

            try
            {
                string libraryFoldersPath = System.IO.Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");

                if (System.IO.File.Exists(libraryFoldersPath))
                {
                    string content = System.IO.File.ReadAllText(libraryFoldersPath);

                    var lines = content.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains("\"path\""))
                        {
                            int startIndex = line.IndexOf("\"", line.IndexOf("\"path\"") + 6);
                            if (startIndex != -1)
                            {
                                int endIndex = line.IndexOf("\"", startIndex + 1);
                                if (endIndex != -1)
                                {
                                    string libraryPath = line.Substring(startIndex + 1, endIndex - startIndex - 1);
                                    libraryPath = libraryPath.Replace("\\\\", "\\");
                                    libraries.Add(libraryPath);
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return libraries.ToArray();
        }

        private bool TrySetGamePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.Directory.Exists(path))
                return false;

            _infiniteRangeMelee = new InfiniteRangeMelee();

            if (_infiniteRangeMelee.SetGamePath(path))
            {
                _gameInstallPath = path;
                GamePathTextBox.Text = path;
                ApplyRangeButton.IsEnabled = true;
                RestoreRangeButton.IsEnabled = true;
                RangeSlider.IsEnabled = true;
                Range3Button.IsEnabled = true;
                Range5Button.IsEnabled = true;
                Range10Button.IsEnabled = true;
                Range20Button.IsEnabled = true;
                Range50Button.IsEnabled = true;
                RangeStatusText.Text = "Status: Ready";
                RangeStatusText.Foreground = Brushes.LimeGreen;
                RangeInfoText.Text = "Game detected! Select multiplier and click Apply. Close game before applying!";
                return true;
            }

            return false;
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

            _speedRangerEnabled = false;
            _speedBerserkerEnabled = false;
            _speedRangerAddresses.Clear();
            _speedBerserkerAddresses.Clear();
            _zoomAddresses.Clear();
            ToggleSpeedRangerButton.IsEnabled = false;
            ToggleSpeedRangerButton.Content = "▶️ Enable Speed";
            ToggleSpeedBerserkerButton.IsEnabled = false;
            ToggleSpeedBerserkerButton.Content = "▶️ Enable Speed";
            SpeedRangerStatusText.Text = "Status: Not scanned";
            SpeedBerserkerStatusText.Text = "Status: Not scanned";

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

        private async void ScanSpeedRangerButton_Click(object sender, RoutedEventArgs e)
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
            SpeedRangerStatusText.Text = "Status: Scanning...";
            SpeedRangerStatusText.Foreground = Brushes.Yellow;

            try
            {
                var addresses = await Task.Run(() =>
                {
                    var memoryManager = _wallHackService.MemoryManager;
                    byte[] pattern = MiscConfig.RangerSpeed.SearchPattern;
                    return memoryManager.ScanMemoryPatternAll(pattern);
                });

                _speedRangerAddresses.Clear();
                _speedRangerAddresses.AddRange(addresses);

                if (_speedRangerAddresses.Count > 0)
                {
                    LogMessage($"✅ Found {_speedRangerAddresses.Count} Speed Hack addresses (Ranger)", Brushes.Green);
                    SpeedRangerStatusText.Text = $"Status: Ready ({_speedRangerAddresses.Count} addresses found)";
                    SpeedRangerStatusText.Foreground = Brushes.LimeGreen;
                    ToggleSpeedRangerButton.IsEnabled = true;
                    SpeedRangerInfoText.Text = $"Found {_speedRangerAddresses.Count} addresses. Click Enable to activate.";
                }
                else
                {
                    LogMessage("❌ No Speed Hack patterns found. Make sure you're playing as Ranger!", Brushes.Red);
                    SpeedRangerStatusText.Text = "Status: Not found";
                    SpeedRangerStatusText.Foreground = Brushes.Red;
                    SpeedRangerInfoText.Text = "No patterns found. Make sure you're in-game as Ranger class.";
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Speed scan error: {ex.Message}", Brushes.Red);
                SpeedRangerStatusText.Text = "Status: Error";
                SpeedRangerStatusText.Foreground = Brushes.Red;
            }
            finally
            {
                _isScanning = false;
            }
        }

        private void ToggleSpeedRangerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_wallHackService == null || _speedRangerAddresses.Count == 0)
            {
                LogMessage("❌ No addresses found. Scan first!", Brushes.Red);
                return;
            }

            var memoryManager = _wallHackService.MemoryManager;

            if (!_speedRangerEnabled)
            {
                byte[] replacement = MiscConfig.RangerSpeed.ReplacePattern;
                int successCount = 0;

                foreach (var address in _speedRangerAddresses)
                {
                    if (memoryManager.WriteMemory(address, replacement))
                    {
                        successCount++;
                    }
                }

                if (successCount > 0)
                {
                    _speedRangerEnabled = true;
                    ToggleSpeedRangerButton.Content = "⏸️ Disable Speed";
                    ToggleSpeedRangerButton.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23));
                    LogMessage($"✅ Speed Hack enabled (Ranger)! ({successCount}/{_speedRangerAddresses.Count} addresses)", Brushes.Green);
                    SpeedRangerStatusText.Text = "Status: ENABLED";
                    SpeedRangerStatusText.Foreground = Brushes.LimeGreen;
                    SpeedRangerInfoText.Text = "Speed hack is active. Movement speed increased!";
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

                foreach (var address in _speedRangerAddresses)
                {
                    if (memoryManager.WriteMemory(address, original))
                    {
                        successCount++;
                    }
                }

                if (successCount > 0)
                {
                    _speedRangerEnabled = false;
                    ToggleSpeedRangerButton.Content = "▶️ Enable Speed";
                    ToggleSpeedRangerButton.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x7D, 0x2D));
                    LogMessage($"✅ Speed Hack disabled (Ranger)! ({successCount}/{_speedRangerAddresses.Count} addresses)", Brushes.Green);
                    SpeedRangerStatusText.Text = "Status: Disabled";
                    SpeedRangerStatusText.Foreground = Brushes.Gray;
                    SpeedRangerInfoText.Text = "Speed hack is inactive. Click Enable to activate.";
                }
                else
                {
                    LogMessage("❌ Failed to disable Speed Hack", Brushes.Red);
                }
            }
        }

        private async void ScanSpeedBerserkerButton_Click(object sender, RoutedEventArgs e)
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
            LogMessage("🔍 Scanning Speed Hack patterns (Berserker only)...", Brushes.Yellow);
            SpeedBerserkerStatusText.Text = "Status: Scanning...";
            SpeedBerserkerStatusText.Foreground = Brushes.Yellow;

            try
            {
                var addresses = await Task.Run(() =>
                {
                    var memoryManager = _wallHackService.MemoryManager;
                    byte[] pattern = MiscConfig.BerserkerSpeed.SearchPattern;
                    return memoryManager.ScanMemoryPatternAll(pattern);
                });

                _speedBerserkerAddresses.Clear();
                _speedBerserkerAddresses.AddRange(addresses);

                if (_speedBerserkerAddresses.Count > 0)
                {
                    LogMessage($"✅ Found {_speedBerserkerAddresses.Count} Speed Hack addresses (Berserker)", Brushes.Green);
                    SpeedBerserkerStatusText.Text = $"Status: Ready ({_speedBerserkerAddresses.Count} addresses found)";
                    SpeedBerserkerStatusText.Foreground = Brushes.LimeGreen;
                    ToggleSpeedBerserkerButton.IsEnabled = true;
                    SpeedBerserkerInfoText.Text = $"Found {_speedBerserkerAddresses.Count} addresses. Click Enable to activate.";
                }
                else
                {
                    LogMessage("❌ No Speed Hack patterns found. Make sure you're playing as Berserker!", Brushes.Red);
                    SpeedBerserkerStatusText.Text = "Status: Not found";
                    SpeedBerserkerStatusText.Foreground = Brushes.Red;
                    SpeedBerserkerInfoText.Text = "No patterns found. Make sure you're in-game as Berserker class.";
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Speed scan error: {ex.Message}", Brushes.Red);
                SpeedBerserkerStatusText.Text = "Status: Error";
                SpeedBerserkerStatusText.Foreground = Brushes.Red;
            }
            finally
            {
                _isScanning = false;
            }
        }

        private void ToggleSpeedBerserkerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_wallHackService == null || _speedBerserkerAddresses.Count == 0)
            {
                LogMessage("❌ No addresses found. Scan first!", Brushes.Red);
                return;
            }

            var memoryManager = _wallHackService.MemoryManager;

            if (!_speedBerserkerEnabled)
            {
                byte[] replacement = MiscConfig.BerserkerSpeed.ReplacePattern;
                int successCount = 0;

                foreach (var address in _speedBerserkerAddresses)
                {
                    if (memoryManager.WriteMemory(address, replacement))
                    {
                        successCount++;
                    }
                }

                if (successCount > 0)
                {
                    _speedBerserkerEnabled = true;
                    ToggleSpeedBerserkerButton.Content = "⏸️ Disable Speed";
                    ToggleSpeedBerserkerButton.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23));
                    LogMessage($"✅ Speed Hack enabled (Berserker)! ({successCount}/{_speedBerserkerAddresses.Count} addresses)", Brushes.Green);
                    SpeedBerserkerStatusText.Text = "Status: ENABLED";
                    SpeedBerserkerStatusText.Foreground = Brushes.LimeGreen;
                    SpeedBerserkerInfoText.Text = "Speed hack is active. Movement speed increased!";
                }
                else
                {
                    LogMessage("❌ Failed to enable Speed Hack", Brushes.Red);
                }
            }
            else
            {
                byte[] original = MiscConfig.BerserkerSpeed.SearchPattern;
                int successCount = 0;

                foreach (var address in _speedBerserkerAddresses)
                {
                    if (memoryManager.WriteMemory(address, original))
                    {
                        successCount++;
                    }
                }

                if (successCount > 0)
                {
                    _speedBerserkerEnabled = false;
                    ToggleSpeedBerserkerButton.Content = "▶️ Enable Speed";
                    ToggleSpeedBerserkerButton.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x7D, 0x2D));
                    LogMessage($"✅ Speed Hack disabled (Berserker)! ({successCount}/{_speedBerserkerAddresses.Count} addresses)", Brushes.Green);
                    SpeedBerserkerStatusText.Text = "Status: Disabled";
                    SpeedBerserkerStatusText.Foreground = Brushes.Gray;
                    SpeedBerserkerInfoText.Text = "Speed hack is inactive. Click Enable to activate.";
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

        private void BrowseGamePathButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Dungeon Rampage executable",
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                FileName = "DungeonRampage.exe"
            };

            if (dialog.ShowDialog() == true)
            {
                string exePath = dialog.FileName;
                string gamePath = System.IO.Path.GetDirectoryName(exePath);

                if (!string.IsNullOrEmpty(gamePath))
                {
                    if (TrySetGamePath(gamePath))
                    {
                        LogMessage($"✅ Game path set manually: {gamePath}", Brushes.Green);
                    }
                    else
                    {
                        LogMessage("❌ Invalid game path. AttackTimeline.json not found!", Brushes.Red);
                        RangeStatusText.Text = "Status: Invalid path";
                        RangeStatusText.Foreground = Brushes.Red;
                        MessageBox.Show("Invalid game path!\n\nMake sure you selected the Dungeon Rampage.exe file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void ApplyRangeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_infiniteRangeMelee == null || string.IsNullOrEmpty(_gameInstallPath))
            {
                LogMessage("❌ Game path not set! Browse for game folder first.", Brushes.Red);
                return;
            }

            var result = MessageBox.Show(
                "⚠️ IMPORTANT:\n\n" +
                "1. Close Dungeon Rampage completely\n" +
                "2. Apply the range modification\n" +
                "3. Reopen the game\n\n" +
                "Make sure the game is CLOSED before continuing!\n\n" +
                "Continue?",
                "Warning - Close Game First",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result != MessageBoxResult.Yes)
                return;

            ApplyRangeButton.IsEnabled = false;
            LogMessage("🎯 Applying Infinite Range hack...", Brushes.Yellow);
            RangeStatusText.Text = "Status: Applying...";
            RangeStatusText.Foreground = Brushes.Yellow;

            try
            {
                double multiplier = RangeSlider.Value;
                int modified = _infiniteRangeMelee.ApplyInfiniteRange(multiplier);

                if (modified > 0)
                {
                    LogMessage($"✅ Infinite Range applied! Modified {modified} values with {multiplier}x multiplier", Brushes.Green);
                    RangeStatusText.Text = $"Status: Applied ({multiplier}x)";
                    RangeStatusText.Foreground = Brushes.LimeGreen;
                    RangeInfoText.Text = $"Range multiplier {multiplier}x active! Reopen game to apply changes.";
                    MessageBox.Show(
                        $"✅ Success!\n\n" +
                        $"Modified {modified} values with {multiplier}x multiplier.\n\n" +
                        $"Now:\n" +
                        $"1. Open Dungeon Rampage\n" +
                        $"2. Enter a dungeon\n" +
                        $"3. Test your melee range!\n\n" +
                        $"Backup files created automatically.",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                else
                {
                    LogMessage("❌ Failed to apply Infinite Range. Check if game files exist.", Brushes.Red);
                    RangeStatusText.Text = "Status: Failed";
                    RangeStatusText.Foreground = Brushes.Red;
                    MessageBox.Show("Failed to apply Infinite Range!\n\nMake sure:\n- Game is closed\n- Game files exist\n- You have write permissions", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error applying range: {ex.Message}", Brushes.Red);
                RangeStatusText.Text = "Status: Error";
                RangeStatusText.Foreground = Brushes.Red;
            }
            finally
            {
                ApplyRangeButton.IsEnabled = true;
            }
        }

        private void RestoreRangeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_infiniteRangeMelee == null || string.IsNullOrEmpty(_gameInstallPath))
            {
                LogMessage("❌ Game path not set!", Brushes.Red);
                return;
            }

            var result = MessageBox.Show(
                "Restore original range values?\n\n" +
                "Make sure the game is CLOSED!",
                "Confirm Restore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result != MessageBoxResult.Yes)
                return;

            RestoreRangeButton.IsEnabled = false;
            LogMessage("↩️ Restoring original range values...", Brushes.Yellow);

            try
            {
                int restored = _infiniteRangeMelee.RestoreOriginal();

                if (restored > 0)
                {
                    LogMessage($"✅ Restored {restored} files to original values", Brushes.Green);
                    RangeStatusText.Text = "Status: Restored";
                    RangeStatusText.Foreground = Brushes.Gray;
                    RangeInfoText.Text = "Original values restored. Reopen game for changes to take effect.";
                    RangeSlider.Value = 10.0;
                    MessageBox.Show($"✅ Restored!\n\n{restored} files restored to original.\n\nReopen the game for changes to take effect.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    LogMessage("⚠️ No backup files found to restore.", Brushes.Orange);
                    MessageBox.Show("No backup files found!\n\nBackup files are created when you first apply the hack.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error restoring: {ex.Message}", Brushes.Red);
            }
            finally
            {
                RestoreRangeButton.IsEnabled = true;
            }
        }

        private void RangeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (RangeValueText == null)
                return;

            double value = Math.Round(e.NewValue, 1);
            RangeValueText.Text = $"Multiplier: {value}x";

            if (value <= 5)
                RangeValueText.Foreground = Brushes.LimeGreen;
            else if (value <= 15)
                RangeValueText.Foreground = Brushes.Yellow;
            else if (value <= 30)
                RangeValueText.Foreground = Brushes.Orange;
            else
                RangeValueText.Foreground = Brushes.Red;
        }

        private void RangePreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tagValue)
            {
                if (double.TryParse(tagValue, out double rangeValue))
                {
                    RangeSlider.Value = rangeValue;
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