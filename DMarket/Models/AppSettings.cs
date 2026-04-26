using System.Collections.Generic;

namespace DMarket.Models
{
    public class AppSettings
    {
        public const int CurrentSettingsVersion = 2;

        public int SettingsVersion { get; set; } = CurrentSettingsVersion;

        public List<string> Symbols { get; set; } = new();
        public int RefreshSeconds { get; set; } = 5;

        public bool HotkeyCtrl { get; set; }
        public bool HotkeyShift { get; set; }
        public bool HotkeyAlt { get; set; } = true;
        public bool HotkeyWin { get; set; }

        public string HotkeyKey { get; set; } = "Z";

        public bool ShowOnlyWhileHotkeyHeld { get; set; } = true;

        public string OverlayPosition { get; set; } = "BottomRight";

        public int OverlayMarginX { get; set; } = 12;
        public int OverlayMarginY { get; set; } = 12;

        public int DisplayMonitorIndex { get; set; } = 0;

        public bool DebugMode { get; set; } = false;

        public List<PortfolioEntry> PortfolioEntries { get; set; } = new();
    }
}
