using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DungeonRampageCheat
{
    public class MemoryManager : IDisposable
    {
        private const int PROCESS_ALL_ACCESS = 0x001F0FFF;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_VM_OPERATION = 0x0008;

        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_PRIVATE = 0x20000;

        private const uint PAGE_NOACCESS = 0x01;
        private const uint PAGE_READONLY = 0x02;
        private const uint PAGE_READWRITE = 0x04;
        private const uint PAGE_WRITECOPY = 0x08;
        private const uint PAGE_EXECUTE_READ = 0x20;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        private const uint PAGE_EXECUTE_WRITECOPY = 0x80;
        private const uint PAGE_GUARD = 0x100;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
            byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
            byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress,
            out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [DllImport("kernel32.dll")]
        private static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EnumProcessModulesEx(IntPtr hProcess, [Out] IntPtr[] lphModule,
            uint cb, out uint lpcbNeeded, uint dwFilterFlag);

        [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule,
            StringBuilder lpFilename, uint nSize);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetModuleInformation(IntPtr hProcess, IntPtr hModule,
            out MODULEINFO lpmodinfo, uint cb);

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_INFO
        {
            public ushort wProcessorArchitecture;
            public ushort wReserved;
            public uint dwPageSize;
            public IntPtr lpMinimumApplicationAddress;
            public IntPtr lpMaximumApplicationAddress;
            public IntPtr dwActiveProcessorMask;
            public uint dwNumberOfProcessors;
            public uint dwProcessorType;
            public uint dwAllocationGranularity;
            public ushort wProcessorLevel;
            public ushort wProcessorRevision;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MODULEINFO
        {
            public IntPtr lpBaseOfDll;
            public uint SizeOfImage;
            public IntPtr EntryPoint;
        }

        public class ModuleInfo
        {
            public string Name { get; set; } = string.Empty;
            public IntPtr BaseAddress { get; set; }
            public long Size { get; set; }
        }

        private class MemoryRegion
        {
            public IntPtr BaseAddress { get; set; }
            public long Size { get; set; }
            public uint Protect { get; set; }
        }

        private IntPtr processHandle = IntPtr.Zero;
        private Process? targetProcess;
        private bool is64Bit = false;
        private bool isAttached = false;
        private SYSTEM_INFO systemInfo;
        private readonly List<ModuleInfo> gameModules = [];
        private readonly List<MemoryRegion> cachedRegions = [];
        private DateTime lastRegionScanTime = DateTime.MinValue;

        public bool AttachToProcess(string processName)
        {
            try
            {
                DebugLogger.Log("=== MEMORY MANAGER ATTACH ===");
                DebugLogger.Log($"Searching for process: '{processName}'");

                Cleanup();

                processName = processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase).Trim();

                DebugLogger.Log("Trying exact process name match...");
                var processes = Process.GetProcessesByName(processName);

                if (processes.Length == 0)
                {
                    DebugLogger.Log("Trying partial name match...");
                    processes = Process.GetProcesses()
                        .Where(p =>
                        {
                            try
                            {
                                return p.ProcessName.Contains(processName, StringComparison.OrdinalIgnoreCase) ||
                                       (!string.IsNullOrEmpty(p.MainWindowTitle) &&
                                        p.MainWindowTitle.Contains(processName, StringComparison.OrdinalIgnoreCase));
                            }
                            catch
                            {
                                return false;
                            }
                        })
                        .ToArray();
                }

                if (processes.Length == 0)
                {
                    DebugLogger.Log("❌ Process not found!");
                    return false;
                }

                targetProcess = processes[0];
                DebugLogger.Log($"✅ Found process: {targetProcess.ProcessName} (PID: {targetProcess.Id})");

                if (Environment.Is64BitOperatingSystem)
                {
                    if (IsWow64Process(targetProcess.Handle, out bool isWow64))
                    {
                        is64Bit = !isWow64;
                    }
                    else
                    {
                        is64Bit = true;
                    }
                }
                else
                {
                    is64Bit = false;
                }

                DebugLogger.Log($"📊 Process architecture: {(is64Bit ? "64-bit" : "32-bit")}");

                int desiredAccess = (int)(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION);
                processHandle = OpenProcess(desiredAccess, false, targetProcess.Id);

                if (processHandle == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    DebugLogger.Log($"❌ Failed to open process. Error code: {error}");
                    return false;
                }

                DebugLogger.Log($"✅ Process handle obtained: 0x{processHandle.ToInt64():X}");

                GetSystemInfo(out systemInfo);
                DebugLogger.Log($"System memory range: 0x{systemInfo.lpMinimumApplicationAddress.ToInt64():X} - 0x{systemInfo.lpMaximumApplicationAddress.ToInt64():X}");

                FindGameModules();
                CacheMemoryRegions();

                isAttached = true;
                DebugLogger.Log("=== ATTACH SUCCESSFUL ===");
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"❌ Exception during attach: {ex.Message}");
                return false;
            }
        }

        private void FindGameModules()
        {
            try
            {
                DebugLogger.Log("🔍 Finding game modules...");
                gameModules.Clear();

                IntPtr[] moduleHandles = new IntPtr[1024];

                if (!EnumProcessModulesEx(processHandle, moduleHandles, (uint)(moduleHandles.Length * IntPtr.Size),
                    out uint bytesNeeded, 0x03))
                {
                    DebugLogger.Log("⚠️ Failed to enumerate modules");
                    return;
                }

                int moduleCount = (int)(bytesNeeded / IntPtr.Size);
                DebugLogger.Log($"Found {moduleCount} modules");

                for (int i = 0; i < moduleCount; i++)
                {
                    try
                    {
                        if (!GetModuleInformation(processHandle, moduleHandles[i], out MODULEINFO moduleInfo, (uint)Marshal.SizeOf(typeof(MODULEINFO))))
                            continue;

                        StringBuilder moduleName = new(260);
                        if (GetModuleFileNameEx(processHandle, moduleHandles[i], moduleName, 260) == 0)
                            continue;

                        string name = System.IO.Path.GetFileName(moduleName.ToString());

                        if (IsRelevantModule(name))
                        {
                            gameModules.Add(new ModuleInfo
                            {
                                Name = name,
                                BaseAddress = moduleInfo.lpBaseOfDll,
                                Size = moduleInfo.SizeOfImage
                            });
                            DebugLogger.Log($"  ✅ {name} @ 0x{moduleInfo.lpBaseOfDll.ToInt64():X}");
                        }
                    }
                    catch { }
                }

                DebugLogger.Log($"✅ Total relevant modules: {gameModules.Count}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"❌ Error finding modules: {ex.Message}");
            }
        }

        private static bool IsRelevantModule(string moduleName)
        {
            string lowerName = moduleName.ToLowerInvariant();

            if (lowerName.Contains("dungeon") || lowerName.Contains("rampage"))
                return true;

            if (lowerName.Contains("adobe air") || lowerName.Contains("adobeair") || lowerName.Contains("air.dll"))
                return true;

            if (lowerName.Contains("d3d") || lowerName.Contains("dxgi") || lowerName.Contains("d2d"))
                return true;

            if (lowerName.EndsWith(".exe") && !lowerName.Contains("cheat") && !lowerName.Contains("steam"))
                return true;

            return false;
        }

        private void CacheMemoryRegions()
        {
            try
            {
                DebugLogger.Log("📊 Scanning writable memory regions...");
                cachedRegions.Clear();

                long address = systemInfo.lpMinimumApplicationAddress.ToInt64();
                long maxAddress = systemInfo.lpMaximumApplicationAddress.ToInt64();
                long totalMemory = 0;
                int regionCount = 0;
                int relevantCount = 0;

                while (address < maxAddress)
                {
                    MEMORY_BASIC_INFORMATION memInfo;
                    int result = VirtualQueryEx(processHandle, new IntPtr(address), out memInfo,
                        (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION)));

                    if (result == 0)
                    {
                        address += 65536;
                        continue;
                    }

                    long regionSize = memInfo.RegionSize.ToInt64();

                    if (regionSize > 0 && regionSize < 2L * 1024 * 1024 * 1024)
                    {
                        if (memInfo.State == MEM_COMMIT &&
                            (memInfo.Protect == PAGE_READWRITE ||
                             memInfo.Protect == PAGE_WRITECOPY ||
                             memInfo.Protect == PAGE_EXECUTE_READWRITE))
                        {
                            cachedRegions.Add(new MemoryRegion
                            {
                                BaseAddress = memInfo.BaseAddress,
                                Size = regionSize,
                                Protect = memInfo.Protect
                            });
                            totalMemory += regionSize;
                            relevantCount++;
                        }
                    }

                    regionCount++;

                    if (regionCount % 10000 == 0)
                    {
                        DebugLogger.Log($"Progress: {regionCount} regions checked, {relevantCount} writable (~{totalMemory / 1024 / 1024}MB)");
                    }

                    address = memInfo.BaseAddress.ToInt64() + regionSize;
                }

                lastRegionScanTime = DateTime.Now;
                DebugLogger.Log($"✅ Scan complete!");
                DebugLogger.Log($"   Regions checked: {regionCount}");
                DebugLogger.Log($"   Writable found: {relevantCount} ({totalMemory / 1024 / 1024}MB)");

                if (relevantCount < 10)
                {
                    DebugLogger.Log("⚠️ CRITICAL: Very few regions found!");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"❌ Error: {ex.Message}");
            }
        }

        public List<IntPtr> ScanMemoryPatternAll(byte[] pattern)
        {
            var stopwatch = Stopwatch.StartNew();
            DebugLogger.Log("=== PATTERN SCAN ===");
            DebugLogger.Log($"Pattern: {BitConverter.ToString(pattern).Replace("-", " ")}");

            List<IntPtr> results = [];

            if (!isAttached || processHandle == IntPtr.Zero)
            {
                DebugLogger.Log("❌ Not attached!");
                return results;
            }

            try
            {
                if ((DateTime.Now - lastRegionScanTime).TotalSeconds > 30 || cachedRegions.Count == 0)
                {
                    CacheMemoryRegions();
                }

                if (cachedRegions.Count == 0)
                {
                    DebugLogger.Log("❌ No regions!");
                    return results;
                }

                DebugLogger.Log($"Scanning {cachedRegions.Count} regions...");

                var concurrentResults = new System.Collections.Concurrent.ConcurrentBag<IntPtr>();
                int scanned = 0;

                Parallel.ForEach(cachedRegions, new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
                }, region =>
                {
                    try
                    {
                        if (region.Size < pattern.Length)
                            return;

                        byte[] buffer = new byte[region.Size];

                        if (ReadProcessMemory(processHandle, region.BaseAddress, buffer, (int)region.Size, out IntPtr read))
                        {
                            for (int i = 0; i <= (int)read - pattern.Length; i++)
                            {
                                if (buffer[i] == pattern[0])
                                {
                                    bool match = true;
                                    for (int j = 1; j < pattern.Length; j++)
                                    {
                                        if (buffer[i + j] != pattern[j])
                                        {
                                            match = false;
                                            break;
                                        }
                                    }
                                    if (match)
                                    {
                                        concurrentResults.Add(IntPtr.Add(region.BaseAddress, i));
                                    }
                                }
                            }
                        }

                        int current = System.Threading.Interlocked.Increment(ref scanned);
                        if (current % 50 == 0)
                        {
                            DebugLogger.Log($"Progress: {current}/{cachedRegions.Count}, Found: {concurrentResults.Count}");
                        }
                    }
                    catch { }
                });

                results = [.. concurrentResults.OrderBy(addr => addr.ToInt64())];

                stopwatch.Stop();
                DebugLogger.Log($"✅ DONE in {stopwatch.ElapsedMilliseconds}ms - Found {results.Count}");

                for (int i = 0; i < Math.Min(5, results.Count); i++)
                {
                    DebugLogger.Log($"  [{i + 1}] 0x{results[i].ToInt64():X}");
                }

                return results;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"❌ Error: {ex.Message}");
                return results;
            }
        }

        public bool WriteMemory(IntPtr address, byte[] data)
        {
            if (!isAttached || processHandle == IntPtr.Zero)
                return false;

            try
            {
                return WriteProcessMemory(processHandle, address, data, data.Length, out IntPtr written)
                    && (int)written == data.Length;
            }
            catch { return false; }
        }

        public byte[]? ReadMemory(IntPtr address, int size)
        {
            if (!isAttached || processHandle == IntPtr.Zero)
                return null;

            try
            {
                byte[] buffer = new byte[size];
                return ReadProcessMemory(processHandle, address, buffer, size, out _) ? buffer : null;
            }
            catch { return null; }
        }

        public bool IsProcessRunning()
        {
            try { return isAttached && targetProcess != null && !targetProcess.HasExited; }
            catch { return false; }
        }

        public string GetProcessInfo()
        {
            try { return targetProcess != null && !targetProcess.HasExited ? $"{targetProcess.ProcessName} (PID: {targetProcess.Id})" : "Not attached"; }
            catch { return "Not attached"; }
        }

        private void Cleanup()
        {
            if (processHandle != IntPtr.Zero)
            {
                CloseHandle(processHandle);
                processHandle = IntPtr.Zero;
            }

            targetProcess = null;
            isAttached = false;
            gameModules.Clear();
            cachedRegions.Clear();
        }

        public void Dispose()
        {
            Cleanup();
            GC.SuppressFinalize(this);
        }
    }
}