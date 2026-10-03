namespace DISPLAY_SCALER.Models
{
    public sealed class ModeDuplicateInfo
    {
        public CustomResolution Resolution { get; set; }
        public bool ExistsInWindowsModeList { get; set; }
        public bool ExistsInWindowsRawModeList { get; set; }
        public bool ExistsInNvidiaCustomDisplay { get; set; }
        public bool HasDisplayScalerMarker { get; set; }
        public string DisplayTitle { get; set; }

        public string ModeLabel
        {
            get { return Resolution == null ? "—" : Resolution.Label; }
        }

        public string WindowsModeListText
        {
            get
            {
                if (ExistsInWindowsModeList) return "yes";
                if (ExistsInWindowsRawModeList) return "raw only";
                return "no";
            }
        }

        public string NvidiaCustomDisplayText
        {
            get { return ExistsInNvidiaCustomDisplay ? "yes" : "no"; }
        }

        public string DisplayScalerMarkerText
        {
            get { return HasDisplayScalerMarker ? "yes" : "no"; }
        }
    }

    public sealed class DisplaySettingOption
    {
        public DisplaySettingOption()
        {
        }

        public DisplaySettingOption(uint value, string label)
        {
            Value = value;
            Label = label;
        }

        public uint Value { get; set; }
        public string Label { get; set; }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Label) ? Value.ToString() : Label;
        }
    }

    public enum DuplicateModeAction
    {
        Cancel = 0,
        AddMarker = 1,
        Ignore = 2
    }
}