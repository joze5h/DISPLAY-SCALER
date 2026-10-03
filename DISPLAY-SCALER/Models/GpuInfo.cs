using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DISPLAY_SCALER.Models
{
    public sealed class GpuInfo : INotifyPropertyChanged
    {
        private string _fullName;
        private string _family;

        public string FullName
        {
            get { return _fullName; }
            set
            {
                if (string.Equals(_fullName, value, StringComparison.Ordinal))
                    return;

                _fullName = value;
                _family = null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Family));
            }
        }

        public string Family
        {
            get
            {
                if (_family == null)
                    _family = ResolveFamily(_fullName);
                return _family;
            }
        }

        public System.IntPtr NvHandle { get; set; }

        public bool DvcSupported { get; set; }

        public string NvApiVersion { get; set; }

        public bool IsReady { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private static string ResolveFamily(string name)
        {
            if (string.IsNullOrEmpty(name)) return "—";
            if (name.IndexOf("RTX 50", StringComparison.OrdinalIgnoreCase) >= 0) return "RTX 50";
            if (name.IndexOf("RTX 40", StringComparison.OrdinalIgnoreCase) >= 0) return "RTX 40";
            if (name.IndexOf("RTX 30", StringComparison.OrdinalIgnoreCase) >= 0) return "RTX 30";
            if (name.IndexOf("RTX 20", StringComparison.OrdinalIgnoreCase) >= 0) return "RTX 20";
            if (name.IndexOf("GTX 16", StringComparison.OrdinalIgnoreCase) >= 0) return "GTX 16";
            if (name.IndexOf("GTX 10", StringComparison.OrdinalIgnoreCase) >= 0) return "GTX 10";
            if (name.IndexOf("QUADRO", StringComparison.OrdinalIgnoreCase) >= 0) return "Quadro";
            return "NVIDIA";
        }

        internal void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, DISPLAY_SCALER.Infrastructure.Mvvm.PropertyChangedEventArgsCache.Get(n));
    }
}