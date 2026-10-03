using DISPLAY_SCALER.Domain;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DISPLAY_SCALER.Models
{
    public enum ResolutionOrigin
    {
        Unknown = 0,
        System = 1,
        Custom = 2,
        DisplayScaler = 3
    }

    public sealed class CustomResolution : INotifyPropertyChanged
    {
        private uint _width;
        private uint _height;
        private uint _refreshRate;
        private uint _refreshRateMilliHz;
        private uint _bitsPerPixel = ResolutionRules.DefaultBitsPerPixel;
        private bool _addedByDisplayScaler;
        private ResolutionOrigin _origin = ResolutionOrigin.Custom;
        private bool _isApplied;
        private string _aspectRatio;
        private string _label;

        public uint Width
        {
            get { return _width; }
            set
            {
                if (Set(ref _width, value))
                    RaiseCalculatedLabels();
            }
        }

        public uint Height
        {
            get { return _height; }
            set
            {
                if (Set(ref _height, value))
                    RaiseCalculatedLabels();
            }
        }

        public uint RefreshRate
        {
            get { return _refreshRate; }
            set
            {
                if (Set(ref _refreshRate, value))
                    RaiseCalculatedLabels();
            }
        }

        public uint RefreshRateMilliHz
        {
            get { return _refreshRateMilliHz; }
            set
            {
                if (Set(ref _refreshRateMilliHz, value))
                {
                    _label = null;
                    OnPropertyChanged(nameof(RefreshRateLabel));
                    OnPropertyChanged(nameof(Label));
                }
            }
        }

        public uint BitsPerPixel
        {
            get { return _bitsPerPixel; }
            set { Set(ref _bitsPerPixel, value == 0 ? ResolutionRules.DefaultBitsPerPixel : value); }
        }

        public bool AddedByDisplayScaler
        {
            get { return _origin == ResolutionOrigin.DisplayScaler || _addedByDisplayScaler; }
            set
            {
                bool oldAddedByDisplayScaler = AddedByDisplayScaler;
                ResolutionOrigin oldOrigin = _origin;

                _addedByDisplayScaler = value;
                if (value)
                    _origin = ResolutionOrigin.DisplayScaler;
                else if (_origin == ResolutionOrigin.DisplayScaler)
                    _origin = ResolutionOrigin.Custom;

                if (oldAddedByDisplayScaler != AddedByDisplayScaler)
                    OnPropertyChanged(nameof(AddedByDisplayScaler));
                if (oldOrigin != _origin)
                    OnPropertyChanged(nameof(Origin));

                if (oldAddedByDisplayScaler != AddedByDisplayScaler || oldOrigin != _origin)
                {
                    OnPropertyChanged(nameof(SourceLabel));
                    OnPropertyChanged(nameof(CanDelete));
                    OnPropertyChanged(nameof(CanEdit));
                }
            }
        }

        public ResolutionOrigin Origin
        {
            get { return _origin; }
            set
            {
                if (_origin == value)
                    return;

                bool oldAddedByDisplayScaler = AddedByDisplayScaler;
                _origin = value;
                _addedByDisplayScaler = value == ResolutionOrigin.DisplayScaler;

                OnPropertyChanged(nameof(Origin));
                if (oldAddedByDisplayScaler != AddedByDisplayScaler)
                    OnPropertyChanged(nameof(AddedByDisplayScaler));
                OnPropertyChanged(nameof(SourceLabel));
                OnPropertyChanged(nameof(CanDelete));
                OnPropertyChanged(nameof(CanEdit));
            }
        }

        public bool CanDelete
        {
            get { return Origin == ResolutionOrigin.Custom || Origin == ResolutionOrigin.DisplayScaler; }
        }

        public bool CanEdit
        {
            get { return CanDelete; }
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

        public string RefreshRateLabel
        {
            get { return RefreshRateMath.Format(RefreshRate, RefreshRateMilliHz); }
        }

        public string Label
        {
            get
            {
                if (_label == null)
                    _label = Width + "×" + Height + " @ " + RefreshRateLabel;
                return _label;
            }
        }

        public string SourceLabel
        {
            get
            {
                switch (Origin)
                {
                    case ResolutionOrigin.System:
                        return "СИСТЕМНЫЕ";
                    case ResolutionOrigin.DisplayScaler:
                        return "ДОБАВЛЕНО ЧЕРЕЗ DISPLAY-SCALER";
                    case ResolutionOrigin.Custom:
                        return "КАСТОМНЫЕ";
                    default:
                        return "НЕИЗВЕСТНЫЙ ИСТОЧНИК";
                }
            }
        }

        public bool IsApplied
        {
            get { return _isApplied; }
            set { Set(ref _isApplied, value); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void RaiseCalculatedLabels()
        {
            _aspectRatio = null;
            _label = null;
            OnPropertyChanged(nameof(AspectRatio));
            OnPropertyChanged(nameof(RefreshRateLabel));
            OnPropertyChanged(nameof(Label));
        }

        private void OnPropertyChanged([CallerMemberName] string n = null)
        {
            PropertyChanged?.Invoke(this, DISPLAY_SCALER.Infrastructure.Mvvm.PropertyChangedEventArgsCache.Get(n));
        }
    }

    public sealed class EsportsPreset
    {
        public string Name { get; private set; }
        public uint Width { get; private set; }
        public uint Height { get; private set; }
        public uint RefreshRate { get; private set; }
        public string Accent { get; private set; }

        public EsportsPreset(string name, uint width, uint height, uint refreshRate, string accent)
        {
            Name = name;
            Width = width;
            Height = height;
            RefreshRate = refreshRate;
            Accent = accent;
        }
    }

    public sealed class EsportsPresetGroup
    {
        public string AspectRatio { get; private set; }
        public EsportsPreset[] Presets { get; private set; }

        public EsportsPresetGroup(string aspectRatio, EsportsPreset[] presets)
        {
            AspectRatio = aspectRatio;
            Presets = presets ?? new EsportsPreset[0];
        }
    }

    public static class EsportsPresets
    {
        public static readonly EsportsPresetGroup[] Groups =
        {
            new EsportsPresetGroup("1:1", new[]
            {
                Preset("1080×1080", 1080, 1080),
                Preset("1280×1280", 1280, 1280),
                Preset("1440×1440", 1440, 1440),
                Preset("1600×1600", 1600, 1600)
            }),
            new EsportsPresetGroup("4:3", new[]
            {
                Preset("1024×768", 1024, 768),
                Preset("1152×864", 1152, 864),
                Preset("1600×1200", 1600, 1200),
                Preset("1800×1350", 1800, 1350),
                Preset("1920×1440", 1920, 1440),
            }),
            new EsportsPresetGroup("16:10", new[]
            {
                Preset("1280×800", 1280, 800),
                Preset("1440×900", 1440, 900),
                Preset("1680×1050", 1680, 1050),
                Preset("1728×1080", 1728, 1080),
                Preset("1920×1200", 1920, 1200)
            })
        };

        public static readonly EsportsPreset[] All = BuildAllPresets();

        private static EsportsPreset Preset(string name, uint width, uint height)
        {
            return new EsportsPreset(name, width, height, 0, "#76B900");
        }

        private static EsportsPreset[] BuildAllPresets()
        {
            int count = 0;
            for (int i = 0; i < Groups.Length; i++)
            {
                EsportsPreset[] presets = Groups[i].Presets;
                if (presets != null)
                    count += presets.Length;
            }

            var result = new EsportsPreset[count];
            int index = 0;
            for (int i = 0; i < Groups.Length; i++)
            {
                EsportsPreset[] presets = Groups[i].Presets;
                if (presets == null)
                    continue;

                for (int j = 0; j < presets.Length; j++)
                    result[index++] = presets[j];
            }

            return result;
        }
    }

}
