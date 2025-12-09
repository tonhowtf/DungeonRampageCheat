using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace DungeonRampageCheat
{
    public class WallHackService : IDisposable
    {
        private readonly MemoryManager memoryManager;
        private readonly WallHackConfig config;
        private readonly Dictionary<string, List<IntPtr>> mapAddresses;
        private bool isAttached = false;

        public event EventHandler<string>? OnStatusChanged;
        public event EventHandler<string>? OnError;
        public MemoryManager MemoryManager => memoryManager;

        public WallHackService()
        {
            memoryManager = new MemoryManager();
            config = new WallHackConfig();
            mapAddresses = [];
        }

        public async Task<bool> AttachToGameAsync(string processName)
        {
            try
            {
                OnStatusChanged?.Invoke(this, $"🔍 Searching for process '{processName}'...");

                bool result = await Task.Run(() => memoryManager.AttachToProcess(processName));

                if (result)
                {
                    isAttached = true;
                    string processInfo = memoryManager.GetProcessInfo();
                    OnStatusChanged?.Invoke(this, $"✅ Successfully connected to: {processInfo}");
                    OnStatusChanged?.Invoke(this, "Ready to scan memory!");
                    return true;
                }
                else
                {
                    OnError?.Invoke(this, $"❌ Could not find or attach to process '{processName}'");
                    OnError?.Invoke(this, "Make sure the game is running and try again");
                    isAttached = false;
                    return false;
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, $"Error attaching to process: {ex.Message}");
                return false;
            }
        }

        public async Task ScanAllMapsAsync()
        {
            if (!isAttached)
            {
                OnError?.Invoke(this, "Not connected to process! Attach first.");
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    OnStatusChanged?.Invoke(this, "🚀 Starting comprehensive map scan...");
                    OnStatusChanged?.Invoke(this, "This may take a few seconds...");
                    mapAddresses.Clear();

                    int totalFound = 0;
                    int mapsScanned = 0;
                    int mapsFound = 0;

                    foreach (var map in config.Maps)
                    {
                        mapsScanned++;
                        OnStatusChanged?.Invoke(this, $"[{mapsScanned}/{config.Maps.Count}] Scanning '{map.MapName}'...");

                        var addresses = memoryManager.ScanMemoryPatternAll(map.ScanPattern);

                        if (addresses.Count > 0)
                        {
                            mapAddresses[map.MapName] = addresses;
                            totalFound += addresses.Count;
                            mapsFound++;
                            OnStatusChanged?.Invoke(this, $"   ✅ '{map.MapName}': {addresses.Count} addresses");
                        }
                        else
                        {
                            OnStatusChanged?.Invoke(this, $"   ⚠️ '{map.MapName}': Not found (may not be loaded)");
                        }
                    }

                    OnStatusChanged?.Invoke(this, "🎉 Scan complete!");
                    OnStatusChanged?.Invoke(this, $"Found {totalFound} addresses across {mapsFound} maps");

                    if (totalFound > 0)
                    {
                        OnStatusChanged?.Invoke(this, "✅ Ready to apply wallhacks!");
                    }
                    else
                    {
                        OnError?.Invoke(this, "❌ No patterns found. Make sure:");
                        OnError?.Invoke(this, "   1) Game is fully loaded");
                        OnError?.Invoke(this, "   2) You're in a map");
                        OnError?.Invoke(this, "   3) Game version matches patterns");
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(this, $"Scan error: {ex.Message}");
                }
            });
        }

        public bool ApplyWallHack(string mapName)
        {
            return ApplyWallHack(mapName, true);
        }

        public bool RestoreMap(string mapName)
        {
            return ApplyWallHack(mapName, false);
        }

        private bool ApplyWallHack(string mapName, bool applyHack)
        {
            if (!isAttached)
            {
                OnError?.Invoke(this, "Not connected to process!");
                return false;
            }

            if (!mapAddresses.TryGetValue(mapName, out var addresses))
            {
                OnError?.Invoke(this, $"Map '{mapName}' not found. Run scan first.");
                return false;
            }

            var map = config.Maps.Find(m => m.MapName == mapName);
            if (map == null)
                return false;

            byte[] data = applyHack ? map.ReplacePattern : map.ScanPattern;
            string action = applyHack ? "applying WallHack" : "restoring";

            OnStatusChanged?.Invoke(this, $"{action} on '{mapName}' at {addresses.Count} addresses...");

            int successCount = 0;
            int totalAddresses = addresses.Count;

            for (int i = 0; i < totalAddresses; i++)
            {
                try
                {
                    if (memoryManager.WriteMemory(addresses[i], data))
                    {
                        successCount++;
                    }

                    if (i % 10 == 0 && i > 0)
                    {
                        OnStatusChanged?.Invoke(this, $"   Progress: {i}/{totalAddresses}");
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(this, $"Error at address 0x{addresses[i].ToInt64():X}: {ex.Message}");
                }
            }

            if (successCount > 0)
            {
                map.IsEnabled = applyHack;
                OnStatusChanged?.Invoke(this, $"✅ {action} on '{mapName}': {successCount}/{totalAddresses} addresses");
                return true;
            }
            else
            {
                OnError?.Invoke(this, $"❌ Failed to {action} on '{mapName}'");
                return false;
            }
        }

        public async Task ApplyAllAsync()
        {
            if (!isAttached)
            {
                OnError?.Invoke(this, "Not connected to process!");
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    OnStatusChanged?.Invoke(this, "Applying WallHack to all maps...");

                    int successMaps = 0;
                    int totalMaps = config.Maps.Count;
                    int currentMap = 0;

                    foreach (var map in config.Maps)
                    {
                        currentMap++;
                        OnStatusChanged?.Invoke(this, $"[{currentMap}/{totalMaps}] Processing '{map.MapName}'...");

                        if (!mapAddresses.ContainsKey(map.MapName))
                        {
                            OnStatusChanged?.Invoke(this, $"   Map not scanned, scanning now...");
                            var addresses = memoryManager.ScanMemoryPatternAll(map.ScanPattern);

                            if (addresses.Count > 0)
                            {
                                mapAddresses[map.MapName] = addresses;
                            }
                            else
                            {
                                OnStatusChanged?.Invoke(this, $"   ⚠️ '{map.MapName}': Pattern not found");
                                continue;
                            }
                        }

                        if (ApplyWallHack(map.MapName))
                        {
                            successMaps++;
                            OnStatusChanged?.Invoke(this, $"   ✅ '{map.MapName}': Applied successfully");
                        }
                        else
                        {
                            OnStatusChanged?.Invoke(this, $"   ❌ '{map.MapName}': Failed to apply");
                        }
                    }

                    OnStatusChanged?.Invoke(this, $"WallHack applied to {successMaps}/{totalMaps} maps!");
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(this, $"Error applying all: {ex.Message}");
                }
            });
        }

        public async Task RestoreAllAsync()
        {
            if (!isAttached)
            {
                OnError?.Invoke(this, "Not connected to process!");
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    OnStatusChanged?.Invoke(this, "Restoring all maps...");

                    int successMaps = 0;
                    int totalMaps = config.Maps.Count;
                    int currentMap = 0;

                    foreach (var map in config.Maps)
                    {
                        currentMap++;
                        OnStatusChanged?.Invoke(this, $"[{currentMap}/{totalMaps}] Restoring '{map.MapName}'...");

                        if (mapAddresses.ContainsKey(map.MapName) && RestoreMap(map.MapName))
                        {
                            successMaps++;
                            OnStatusChanged?.Invoke(this, $"   ✅ '{map.MapName}': Restored successfully");
                        }
                        else
                        {
                            OnStatusChanged?.Invoke(this, $"   ⚠️ '{map.MapName}': No addresses to restore");
                        }
                    }

                    OnStatusChanged?.Invoke(this, $"{successMaps}/{totalMaps} maps restored!");
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(this, $"Error restoring all: {ex.Message}");
                }
            });
        }

        public bool IsConnected()
        {
            return isAttached && memoryManager.IsProcessRunning();
        }

        public string GetProcessInfo()
        {
            return memoryManager.GetProcessInfo();
        }

        public WallHackConfig GetConfig()
        {
            return config;
        }

        public void Dispose()
        {
            memoryManager?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}