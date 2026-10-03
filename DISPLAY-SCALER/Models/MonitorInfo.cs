using DISPLAY_SCALER.Domain;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DISPLAY_SCALER.Models
{
    public sealed class MonitorInfo : INotifyPropertyChanged
    {
        public string DeviceName { get; set; }

        public string FriendlyName
        {
            get => _friendlyName;
            set
            {
                if (string.Equals(_friendlyName, value, StringComparison.Ordinal))
                    return;

                _friendlyName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }

        private string _friendlyName;

        public string Manufacturer { get; set; }

        public string SerialNumber { get; set; }

        public string DeviceId { get; set; }

        public string DeviceKey { get; set; }

        public string NvApiDeviceName { get; set; }

        public string AdapterName { get; set; }

        public string AdapterDeviceId { get; set; }

        public uint CurrentWidth { get; set; }

        public uint CurrentHeight { get; set; }
        public uint CurrentRefreshRate { get; set; }
        public uint CurrentRefreshRateMilliHz { get; set; }
        public uint CurrentRefreshRateNumerator { get; set; }
        public uint CurrentRefreshRateDenominator { get; set; }
        public uint CurrentBitsPerPixel { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsAttached { get; set; }

        public uint NativeWidth { get; set; }

        public uint NativeHeight { get; set; }
        public uint NativeRefreshRateHz { get; set; }
        public uint NativeRefreshRateMilliHz { get; set; }
        public uint NativeRefreshRateNumerator { get; set; }
        public uint NativeRefreshRateDenominator { get; set; }
        public uint MaxVerticalRate { get; set; }
        public uint MinVerticalRate { get; set; }
        public uint MaxHorizontalRate { get; set; }
        public uint MinHorizontalRate { get; set; }
        public uint MaxPixelClockMhz { get; set; }
        public double DiagonalInch { get; set; }
        public string ProductCode { get; set; }
        public int ManufactureYear { get; set; }
        public int ManufactureWeek { get; set; }

        public int Saturation
        {
            get => _saturation;
            set
            {
                if (_saturation == value)
                    return;

                _saturation = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SaturationDisplay));
            }
        }

        private int _saturation = 50;

        public int SaturationDefault { get; set; } = 50;
        public bool SaturationSupported { get; set; }

        public string SaturationDisplay => $"{Saturation}%";

        public ObservableCollection<ResolutionMode> SupportedModes { get; } = new ObservableCollection<ResolutionMode>();

        public ObservableCollection<CustomResolution> CustomResolutions { get; } = new ObservableCollection<CustomResolution>();

        public string DisplayTitle => string.IsNullOrWhiteSpace(FriendlyName) ? (DeviceName ?? "Монитор") : FriendlyName;

        public string CurrentModeText => CurrentWidth > 0 && CurrentHeight > 0
            ? CurrentWidth + "×" + CurrentHeight + " @ " + RefreshRateMath.Format(CurrentRefreshRate, CurrentRefreshRateMilliHz) + " - " + CurrentBitsPerPixel + " бит"
            : "-";

        public string CurrentResolutionText => CurrentWidth > 0 && CurrentHeight > 0 ? $"{CurrentWidth}×{CurrentHeight}" : "—";

        public string CurrentRefreshText => CurrentRefreshRate > 0 ? RefreshRateMath.Format(CurrentRefreshRate, CurrentRefreshRateMilliHz) : "-";

        public string NativeModeText => NativeWidth > 0
            ? (NativeRefreshRateMilliHz > 0 ? $"{NativeWidth}×{NativeHeight} @ {RefreshRateMath.Format(NativeRefreshRateHz, NativeRefreshRateMilliHz)}" : $"{NativeWidth}×{NativeHeight}")
            : "—";

        public string DiagonalText => DiagonalInch > 0 ? $"{DiagonalInch:F1}″" : "—";

        public string RangeText
        {
            get
            {
                if (MaxVerticalRate == 0 && MaxHorizontalRate == 0) return "—";
                string v = MaxVerticalRate > 0 ? $"{MinVerticalRate}–{MaxVerticalRate} Гц V" : "—";
                string h = MaxHorizontalRate > 0 ? $"{MinHorizontalRate}–{MaxHorizontalRate} кГц H" : "";
                return string.IsNullOrEmpty(h) ? v : $"{v} · {h}";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, DISPLAY_SCALER.Infrastructure.Mvvm.PropertyChangedEventArgsCache.Get(n));
    }

    public sealed class ResolutionMode
    {
        private uint _width;
        private uint _height;
        private uint _refreshRate;
        private uint _bitsPerPixel;
        private bool _isInterlaced;
        private string _label;
        private string _aspectRatio;

        public uint Width
        {
            get { return _width; }
            set
            {
                if (_width == value) return;
                _width = value;
                InvalidateCachedText();
            }
        }

        public uint Height
        {
            get { return _height; }
            set
            {
                if (_height == value) return;
                _height = value;
                InvalidateCachedText();
            }
        }

        public uint RefreshRate
        {
            get { return _refreshRate; }
            set
            {
                if (_refreshRate == value) return;
                _refreshRate = value;
            }
        }

        public uint BitsPerPixel
        {
            get { return _bitsPerPixel; }
            set { _bitsPerPixel = value; }
        }

        public bool IsInterlaced
        {
            get { return _isInterlaced; }
            set
            {
                if (_isInterlaced == value) return;
                _isInterlaced = value;
            }
        }

        public string Label
        {
            get
            {
                if (_label == null)
                    _label = Width + "×" + Height;
                return _label;
            }
        }

        public string AspectRatio
        {
            get
            {
                if (_aspectRatio == null)
                    _aspectRatio = AspectRatioMath.Format(Width, Height);
                return _aspectRatio;
            }
        }

        private void InvalidateCachedText()
        {
            _label = null;
            _aspectRatio = null;
        }
    }
}
