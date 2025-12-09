using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows;

namespace DungeonRampageCheat
{
    public static class DebugLogger
    {
        public delegate void DebugLogEventHandler(string message, Brush? color);
        public static event DebugLogEventHandler? OnDebugLog;

        public static void Log(string message, Brush? color = null)
        {
            OnDebugLog?.Invoke(message, color ?? Brushes.White);
        }
    }

    public class WallHackConfig
    {
        public class MapPattern
        {
            public string MapName { get; set; }
            public byte[] ScanPattern { get; set; }
            public byte[] ReplacePattern { get; set; }
            public bool IsEnabled { get; set; }

            public MapPattern(string name, string scanHex, string replaceHex)
            {
                MapName = name;
                ScanPattern = HexStringToByteArray(scanHex);
                ReplacePattern = HexStringToByteArray(replaceHex);
                IsEnabled = false;
            }

            private static byte[] HexStringToByteArray(string hex)
            {
                hex = hex.Replace(" ", "").Replace("-", "");
                byte[] bytes = new byte[hex.Length / 2];
                for (int i = 0; i < hex.Length; i += 2)
                {
                    bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
                }
                return bytes;
            }
        }

        public List<MapPattern> Maps { get; private set; }

        public WallHackConfig()
        {
            Maps = new List<MapPattern>
            {
                new MapPattern("Arena", "41 52 45 4E 41", "6B D1 C5 94 6F"),
                new MapPattern("Barrows", "43 41 54 41 43 4F 4D 42", "49 4E 56 41 4C 49 44"),
                new MapPattern("Cavern", "4E 4F 52 44 49 43 5F 43", "41 56 45 5F 57 41 4C"),
                new MapPattern("Dino Park", "4A 55 52 41 53 53 49 43", "47 52 4F 55 4E 44 5F 43"),
                new MapPattern("Frost Guard", "4E 4F 52 44 49", "41 56 45 5F 57"),
                new MapPattern("Jungle", "4A 55 52 41 53 53 49 43", "47 52 4F 55 4E 44 5F 43"),
                new MapPattern("Prison", "50 52 49 53 4F 4E", "80 82 73 83 79 78"),
                new MapPattern("Ruins", "41 5A 54 45 43", "37 32 2E 31 33"),
                new MapPattern("Battleheim", "4E 4F 52 44 49", "41 56 45 5F 57")
            };
        }

        public void ToggleAll(bool enabled)
        {
            foreach (var map in Maps)
            {
                map.IsEnabled = enabled;
            }
        }
    }
}