using System.Collections.Generic;

namespace DungeonRampageCheat
{
    public class MiscConfig
    {
        public class SpeedPattern
        {
            public string Name { get; set; } = string.Empty;
            public byte[] SearchPattern { get; set; } = [];
            public byte[] ReplacePattern { get; set; } = [];
        }

        public class ZoomPattern
        {
            public string Name { get; set; } = string.Empty;
            public double SearchValue { get; set; }
            public double ReplaceValue { get; set; }
        }

        public static readonly SpeedPattern RangerSpeed = new()
        {
            Name = "Ranger Speed",
            SearchPattern = [0x7B, 0x14, 0xAE, 0x47, 0xE1, 0x7A, 0x84, 0x3F,
                           0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F],
            ReplacePattern = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F,
                            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F]
        };

        public static readonly ZoomPattern CameraZoom = new()
        {
            Name = "Camera Zoom",
            SearchValue = 0.63,
            ReplaceValue = 0.2
        };

        public static readonly List<double> ZoomLevels = [0.1, 0.2, 0.3, 0.4, 0.5, 0.63];
    }
}