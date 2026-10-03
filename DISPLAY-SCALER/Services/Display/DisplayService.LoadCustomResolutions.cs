using DISPLAY_SCALER.Domain;
using DISPLAY_SCALER.Models;
using DISPLAY_SCALER.Native;
using System;
using System.Collections.Generic;

namespace DISPLAY_SCALER.Services
{
    public sealed partial class DisplayService
    {
        private static readonly Comparison<CustomResolution> CustomModeSortComparison = CompareCustomModes;

        private readonly struct ResolutionModeKey : IEquatable<ResolutionModeKey>
        {
            private readonly uint _width;
            private readonly uint _height;
            private readonly uint _bitsPerPixel;
            private readonly uint _refreshRate;
            private readonly uint _refreshRateMilliHz;
            private readonly bool _isInterlaced;

            public ResolutionModeKey(uint width, uint height, uint bitsPerPixel, uint refreshRate)
                : this(width, height, bitsPerPixel, refreshRate, 0, false)
            {
            }

            public ResolutionModeKey(uint width, uint height, uint bitsPerPixel, uint refreshRate, bool isInterlaced)
                : this(width, height, bitsPerPixel, refreshRate, 0, isInterlaced)
            {
            }

            public ResolutionModeKey(uint width, uint height, uint bitsPerPixel, uint refreshRate, uint refreshRateMilliHz)
                : this(width, height, bitsPerPixel, refreshRate, refreshRateMilliHz, false)
            {
            }

            public ResolutionModeKey(uint width, uint height, uint bitsPerPixel, uint refreshRate, uint refreshRateMilliHz, bool isInterlaced)
            {
                _width = width;
                _height = height;
                _bitsPerPixel = bitsPerPixel == 0 ? DefaultBitsPerPixel : bitsPerPixel;
                _refreshRate = refreshRate;
                _refreshRateMilliHz = refreshRateMilliHz == 0
                    ? 0
                    : RefreshRateMath.NormalizeDisplayMilliHz(refreshRate, refreshRateMilliHz);
                _isInterlaced = isInterlaced;
            }

            public uint Width { get { return _width; } }
            public uint Height { get { return _height; } }
            public uint BitsPerPixel { get { return _bitsPerPixel; } }
            public uint RefreshRate { get { return _refreshRate; } }
            public uint RefreshRateMilliHz { get { return _refreshRateMilliHz; } }
            public bool IsInterlaced { get { return _isInterlaced; } }

            public bool Equals(ResolutionModeKey other)
            {
                return _width == other._width &&
                       _height == other._height &&
                       _bitsPerPixel == other._bitsPerPixel &&
                       _refreshRate == other._refreshRate &&
                       _refreshRateMilliHz == other._refreshRateMilliHz &&
                       _isInterlaced == other._isInterlaced;
            }

            public override bool Equals(object obj)
            {
                return obj is ResolutionModeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)_width;
                    hash = (hash * 397) ^ (int)_height;
                    hash = (hash * 397) ^ (int)_bitsPerPixel;
                    hash = (hash * 397) ^ (int)_refreshRate;
                    hash = (hash * 397) ^ (int)_refreshRateMilliHz;
                    hash = (hash * 397) ^ (_isInterlaced ? 1 : 0);
                    return hash;
                }
            }
        }

        public void LoadCustomResolutions(MonitorInfo monitor)
        {
            if (monitor == null)
                return;

            monitor.CustomResolutions.Clear();

            var modes = new List<CustomResolution>(32);
            var seen = new HashSet<ResolutionModeKey>();
            HashSet<ResolutionModeKey> displayScalerModes = ReadDisplayScalerModes(monitor);

            if (_nv != null && _nv.IsInitialized)
            {
                List<CustomResolution> nvModes = _nv.EnumerateCustomDisplays(GetNvapiDeviceName(monitor), out var enumError);
                for (int i = 0; i < nvModes.Count; i++)
                {
                    CustomResolution mode = nvModes[i];
                    if (mode != null)
                        AddCustomMode(modes, seen, displayScalerModes, mode.Width, mode.Height, mode.BitsPerPixel, mode.RefreshRate, mode.RefreshRateMilliHz);
                }
            }

            string existing = Win32Monitor.ReadNonStandardModes(monitor.DeviceId);
            if (!string.IsNullOrEmpty(existing))
                AddCustomModesFromModeList(existing, modes, seen, displayScalerModes);

            AddDisplayScalerMarkerModes(monitor, modes, seen, displayScalerModes);

            modes.Sort(CustomModeSortComparison);

            for (int i = 0; i < modes.Count; i++)
                monitor.CustomResolutions.Add(modes[i]);
        }

        private static void AddCustomModesFromModeList(
            string existing,
            List<CustomResolution> modes,
            HashSet<ResolutionModeKey> seen,
            HashSet<ResolutionModeKey> displayScalerModes)
        {
            int start = 0;
            while (start < existing.Length)
            {
                int end = existing.IndexOf('@', start);
                if (end < 0)
                    end = existing.Length;

                if (TryGetTrimmedSegment(existing, start, end - start, out var trimmedStart, out var trimmedLength) &&
                    TryParseModeParts(existing, trimmedStart, trimmedLength, out var width, out var height, out var bits, out var refresh))
                {
                    AddCustomMode(modes, seen, displayScalerModes, width, height, bits, refresh);
                }

                start = end + 1;
            }
        }

        private void AddDisplayScalerMarkerModes(MonitorInfo monitor, List<CustomResolution> modes, HashSet<ResolutionModeKey> seen, HashSet<ResolutionModeKey> displayScalerModes)
        {
            if (monitor == null || displayScalerModes == null || displayScalerModes.Count == 0)
                return;

            List<ResolutionModeKey> freshWindowsModes = null;

            var orderedMarkers = new List<ResolutionModeKey>(displayScalerModes);
            orderedMarkers.Sort((a, b) =>
            {
                bool aExact = a.RefreshRateMilliHz != 0;
                bool bExact = b.RefreshRateMilliHz != 0;
                if (aExact != bExact)
                    return aExact ? -1 : 1;
                return b.RefreshRateMilliHz.CompareTo(a.RefreshRateMilliHz);
            });

            foreach (ResolutionModeKey marker in orderedMarkers)
            {
                bool usable = IsSupportedModeSnapshot(monitor, marker.Width, marker.Height, marker.RefreshRate);
                if (!usable)
                {
                    if (freshWindowsModes == null)
                        freshWindowsModes = EnumerateWindowsModeKeys(monitor);

                    usable = ContainsWindowsMode(freshWindowsModes, marker);
                }

                if (!usable)
                    continue;

                if (!TryMarkExistingCustomMode(modes, marker))
                {
                    AddCustomMode(
                        modes,
                        seen,
                        displayScalerModes,
                        marker.Width,
                        marker.Height,
                        marker.BitsPerPixel,
                        marker.RefreshRate,
                        marker.RefreshRateMilliHz);
                }
            }
        }

        private static List<ResolutionModeKey> EnumerateWindowsModeKeys(MonitorInfo monitor)
        {
            var result = new List<ResolutionModeKey>(128);
            if (monitor == null || string.IsNullOrWhiteSpace(monitor.DeviceName))
                return result;

            var seen = new HashSet<ResolutionModeKey>();
            AddWindowsModeKeys(monitor, 0, result, seen);
            AddWindowsModeKeys(monitor, Win32Display.EDS_RAWMODE, result, seen);
            return result;
        }

        private static void AddWindowsModeKeys(MonitorInfo monitor, uint flags, List<ResolutionModeKey> result, HashSet<ResolutionModeKey> seen)
        {
            for (int i = 0; i < MaxDisplayModeEnumeration; i++)
            {
                var dm = CreateDevMode();
                if (Win32Display.EnumDisplaySettingsEx(monitor.DeviceName, i, ref dm, flags) == 0)
                    break;

                if (dm.dmPelsWidth == 0 || dm.dmPelsHeight == 0)
                    continue;

                uint bpp = dm.dmBitsPerPel == 0 ? DefaultBitsPerPixel : dm.dmBitsPerPel;
                if (bpp < DefaultBitsPerPixel)
                    continue;

                var mode = new ResolutionModeKey(
                    dm.dmPelsWidth,
                    dm.dmPelsHeight,
                    bpp,
                    dm.dmDisplayFrequency,
                    (dm.dmDisplayFlags & 0x2) != 0);
                if (seen.Add(mode))
                    result.Add(mode);
            }
        }

        private static bool ContainsWindowsMode(List<ResolutionModeKey> modes, ResolutionModeKey marker)
        {
            if (modes == null)
                return false;

            for (int i = 0; i < modes.Count; i++)
            {
                ResolutionModeKey candidate = modes[i];
                if (candidate.Width != marker.Width || candidate.Height != marker.Height)
                    continue;

                if (candidate.RefreshRate == 0 || Math.Abs((long)candidate.RefreshRate - (long)marker.RefreshRate) <= 1)
                    return true;
            }

            return false;
        }

        private static bool IsSupportedModeSnapshot(MonitorInfo monitor, uint width, uint height, uint refresh)
        {
            if (monitor == null) return false;
            for (int i = 0; i < monitor.SupportedModes.Count; i++)
            {
                ResolutionMode mode = monitor.SupportedModes[i];
                if (mode.Width == width && mode.Height == height &&
                    Math.Abs((long)mode.RefreshRate - (long)refresh) <= 1 &&
                    (mode.BitsPerPixel == 0 || mode.BitsPerPixel >= DefaultBitsPerPixel))
                {
                    return true;
                }
            }
            return false;
        }

        private static int CompareCustomModes(CustomResolution a, CustomResolution b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int aspect = GetAspectSortKey(a.AspectRatio).CompareTo(GetAspectSortKey(b.AspectRatio));
            if (aspect != 0) return aspect;

            int area = ((long)b.Width * b.Height).CompareTo((long)a.Width * a.Height);
            if (area != 0) return area;

            int refresh = b.RefreshRate.CompareTo(a.RefreshRate);
            if (refresh != 0) return refresh;

            int exactRefresh = b.RefreshRateMilliHz.CompareTo(a.RefreshRateMilliHz);
            if (exactRefresh != 0) return exactRefresh;

            int width = a.Width.CompareTo(b.Width);
            if (width != 0) return width;

            return a.Height.CompareTo(b.Height);
        }

        private static int GetAspectSortKey(string aspectRatio)
        {
            switch (aspectRatio)
            {
                case "16:9": return 0;
                case "16:10": return 1;
                case "21:9": return 2;
                case "32:9": return 3;
                case "4:3": return 4;
                case "5:4": return 5;
                case "3:2": return 6;
                case "1:1": return 7;
                default: return 100;
            }
        }

        private static void AddCustomMode(
            List<CustomResolution> modes,
            HashSet<ResolutionModeKey> seen,
            HashSet<ResolutionModeKey> displayScalerModes,
            uint width,
            uint height,
            uint bits,
            uint refresh,
            uint refreshRateMilliHz = 0)
        {
            if (width < ResolutionRules.MinWidth || width > ResolutionRules.MaxWidth ||
                height < ResolutionRules.MinHeight || height > ResolutionRules.MaxHeight ||
                refresh < ResolutionRules.MinRefreshRate || refresh > ResolutionRules.MaxRefreshRate)
            {
                return;
            }

            bits = NormalizeBitsPerPixel(bits);

            if (refreshRateMilliHz == 0 && ContainsNominalCustomMode(modes, width, height, bits, refresh))
                return;

            uint exactMilliHz = RefreshRateMath.NormalizeDisplayMilliHz(refresh, refreshRateMilliHz);
            var key = new ResolutionModeKey(width, height, bits, refresh, exactMilliHz);
            if (!seen.Add(key)) return;

            bool addedByDisplayScaler = IsDisplayScalerMode(displayScalerModes, width, height, bits, refresh, exactMilliHz);
            var mode = new CustomResolution
            {
                Width = width,
                Height = height,
                BitsPerPixel = bits,
                RefreshRate = refresh,
                RefreshRateMilliHz = exactMilliHz,
                Origin = addedByDisplayScaler ? ResolutionOrigin.DisplayScaler : ResolutionOrigin.Custom,
                AddedByDisplayScaler = addedByDisplayScaler
            };

            modes.Add(mode);
        }

        private static bool ContainsNominalCustomMode(List<CustomResolution> modes, uint width, uint height, uint bits, uint refresh)
        {
            if (modes == null)
                return false;

            for (int i = 0; i < modes.Count; i++)
            {
                CustomResolution mode = modes[i];
                if (mode != null &&
                    mode.Width == width &&
                    mode.Height == height &&
                    NormalizeBitsPerPixel(mode.BitsPerPixel) == bits &&
                    mode.RefreshRate == refresh)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDisplayScalerMode(HashSet<ResolutionModeKey> displayScalerModes, uint width, uint height, uint bits, uint refresh, uint refreshRateMilliHz)
        {
            if (displayScalerModes == null || displayScalerModes.Count == 0)
                return false;

            bits = NormalizeBitsPerPixel(bits);
            uint exactMilliHz = RefreshRateMath.NormalizeDisplayMilliHz(refresh, refreshRateMilliHz);
            foreach (ResolutionModeKey marker in displayScalerModes)
            {
                if (marker.Width != width ||
                    marker.Height != height ||
                    marker.BitsPerPixel != bits ||
                    marker.RefreshRate != refresh)
                {
                    continue;
                }

                if (marker.RefreshRateMilliHz == 0 ||
                    Math.Abs((long)marker.RefreshRateMilliHz - (long)exactMilliHz) <= 1L)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryMarkExistingCustomMode(List<CustomResolution> modes, ResolutionModeKey marker)
        {
            if (modes == null)
                return false;

            if (marker.RefreshRateMilliHz == 0)
            {
                for (int i = 0; i < modes.Count; i++)
                {
                    CustomResolution existing = modes[i];
                    if (existing != null &&
                        existing.AddedByDisplayScaler &&
                        existing.Width == marker.Width &&
                        existing.Height == marker.Height &&
                        NormalizeBitsPerPixel(existing.BitsPerPixel) == marker.BitsPerPixel &&
                        existing.RefreshRate == marker.RefreshRate)
                    {
                        return true;
                    }
                }
            }

            for (int i = 0; i < modes.Count; i++)
            {
                CustomResolution mode = modes[i];
                if (mode == null ||
                    mode.Width != marker.Width ||
                    mode.Height != marker.Height ||
                    NormalizeBitsPerPixel(mode.BitsPerPixel) != marker.BitsPerPixel ||
                    mode.RefreshRate != marker.RefreshRate)
                {
                    continue;
                }

                uint modeExactMilliHz = RefreshRateMath.NormalizeDisplayMilliHz(mode.RefreshRate, mode.RefreshRateMilliHz);
                if (marker.RefreshRateMilliHz != 0 &&
                    Math.Abs((long)modeExactMilliHz - (long)marker.RefreshRateMilliHz) > 1L)
                {
                    continue;
                }

                mode.AddedByDisplayScaler = true;
                return true;
            }

            return false;
        }

        private static uint NormalizeBitsPerPixel(uint bits)
        {
            return bits == 0 || bits < DefaultBitsPerPixel ? DefaultBitsPerPixel : bits;
        }

        private static string FormatMode(CustomResolution res)
        {
            if (res == null) return string.Empty;
            return FormatMode(res.Width, res.Height, NormalizeBitsPerPixel(res.BitsPerPixel), res.RefreshRate);
        }

        private static string FormatMode(uint width, uint height, uint bits, uint refresh)
        {
            return width + "x" + height + "x" + NormalizeBitsPerPixel(bits) + "x" + refresh;
        }

        private static string FormatRegistryMode(CustomResolution res)
        {
            if (res == null) return string.Empty;
            uint exactMilliHz = RefreshRateMath.NormalizeDisplayMilliHz(res.RefreshRate, res.RefreshRateMilliHz);
            return FormatMode(res) + "x" + exactMilliHz;
        }
    }
}
