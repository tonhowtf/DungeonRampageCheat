using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;

namespace DungeonRampageCheat
{
    public class MiscService
    {
        private readonly MemoryManager _memoryManager;
        private readonly List<IntPtr> _speedAddresses = new();
        private readonly List<IntPtr> _zoomAddresses = new();
        private bool _speedEnabled = false;

        public MiscService(MemoryManager memoryManager)
        {
            _memoryManager = memoryManager;
        }

        public async Task<bool> ScanSpeedHack()
        {
            try
            {
                DebugLogger.Log("🔍 Scanning Speed Hack patterns...", Brushes.Yellow);

                _speedAddresses.Clear();

                byte[] pattern = MiscConfig.RangerSpeed.SearchPattern;

                var addresses = await Task.Run(() =>
                    _memoryManager.ScanMemoryPatternAll(pattern)
                );

                if (addresses.Count > 0)
                {
                    _speedAddresses.AddRange(addresses);
                    DebugLogger.Log($"✅ Speed Hack: Found {addresses.Count} addresses", Brushes.Green);
                    return true;
                }
                else
                {
                    DebugLogger.Log("❌ Speed Hack: No patterns found", Brushes.Red);
                    return false;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"❌ Error scanning Speed Hack: {ex.Message}", Brushes.Red);
                return false;
            }
        }

        public bool EnableSpeedHack()
        {
            try
            {
                if (_speedAddresses.Count == 0)
                {
                    DebugLogger.Log("❌ Speed Hack: No addresses found. Run scan first!", Brushes.Red);
                    return false;
                }

                byte[] replacement = MiscConfig.RangerSpeed.ReplacePattern;
                int successCount = 0;

                foreach (var address in _speedAddresses)
                {
                    if (_memoryManager.WriteMemory(address, replacement))
                    {
                        successCount++;
                    }
                }

                if (successCount > 0)
                {
                    _speedEnabled = true;
                    DebugLogger.Log($"✅ Speed Hack enabled! ({successCount}/{_speedAddresses.Count} addresses)", Brushes.Green);
                    return true;
                }
                else
                {
                    DebugLogger.Log("❌ Failed to enable Speed Hack", Brushes.Red);
                    return false;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"❌ Error enabling Speed Hack: {ex.Message}", Brushes.Red);
                return false;
            }
        }

        public bool DisableSpeedHack()
        {
            try
            {
                if (_speedAddresses.Count == 0)
                {
                    DebugLogger.Log("❌ Speed Hack: No addresses to restore", Brushes.Red);
                    return false;
                }

                byte[] original = MiscConfig.RangerSpeed.SearchPattern;
                int successCount = 0;

                foreach (var address in _speedAddresses)
                {
                    if (_memoryManager.WriteMemory(address, original))
                    {
                        successCount++;
                    }
                }

                if (successCount > 0)
                {
                    _speedEnabled = false;
                    DebugLogger.Log($"✅ Speed Hack disabled! ({successCount}/{_speedAddresses.Count} addresses)", Brushes.Green);
                    return true;
                }
                else
                {
                    DebugLogger.Log("❌ Failed to disable Speed Hack", Brushes.Red);
                    return false;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"❌ Error disabling Speed Hack: {ex.Message}", Brushes.Red);
                return false;
            }
        }

        public async Task<bool> ScanZoomHack()
        {
            try
            {
                DebugLogger.Log("🔍 Scanning Zoom Hack (0.63)...", Brushes.Yellow);

                double zoomValue = MiscConfig.CameraZoom.SearchValue;
                byte[] zoomBytes = BitConverter.GetBytes(zoomValue);

                var addresses = await Task.Run(() =>
                    _memoryManager.ScanMemoryPatternAll(zoomBytes)
                );

                if (addresses.Count > 0)
                {
                    _zoomAddresses.Clear();
                    _zoomAddresses.AddRange(addresses);
                    DebugLogger.Log($"✅ Zoom Hack: Found {addresses.Count} addresses", Brushes.Green);
                    return true;
                }
                else
                {
                    DebugLogger.Log("❌ Zoom Hack: No addresses found. Make sure you're in-game with default zoom!", Brushes.Red);
                    return false;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"❌ Error scanning Zoom Hack: {ex.Message}", Brushes.Red);
                return false;
            }
        }

        public bool SetZoom(double zoomValue)
        {
            try
            {
                if (_zoomAddresses.Count == 0)
                {
                    DebugLogger.Log("❌ Zoom Hack: No addresses found. Run scan first!", Brushes.Red);
                    return false;
                }

                byte[] zoomBytes = BitConverter.GetBytes(zoomValue);
                int successCount = 0;

                foreach (var address in _zoomAddresses)
                {
                    if (_memoryManager.WriteMemory(address, zoomBytes))
                    {
                        successCount++;
                    }
                }

                if (successCount > 0)
                {
                    DebugLogger.Log($"✅ Zoom set to {zoomValue:F2} ({successCount}/{_zoomAddresses.Count} addresses)", Brushes.Cyan);
                    return true;
                }
                else
                {
                    DebugLogger.Log("❌ Failed to set zoom", Brushes.Red);
                    return false;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"❌ Error setting zoom: {ex.Message}", Brushes.Red);
                return false;
            }
        }

        public bool IsSpeedEnabled() => _speedEnabled;
        public bool HasSpeedAddresses() => _speedAddresses.Count > 0;
        public bool HasZoomAddresses() => _zoomAddresses.Count > 0;
    }
}