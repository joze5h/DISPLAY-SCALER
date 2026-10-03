using DISPLAY_SCALER.Infrastructure.Timing;
using DISPLAY_SCALER.Models;
using DISPLAY_SCALER.Native;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DISPLAY_SCALER.Services
{
    public sealed partial class DisplayService
    {
        private static readonly Comparison<ResolutionMode> SupportedModeSortComparison = CompareSupportedModes;

        private sealed class DisplaySnapshot
        {
            public string DeviceName;
            public bool IsPrimary;
            public int Left;
            public int Top;
        }

        private sealed class ActiveDisplayConfigSnapshot
        {
            public Win32Display.DISPLAYCONFIG_PATH_INFO[] Paths;
            public Win32Display.DISPLAYCONFIG_MODE_INFO[] Modes;
            public uint PathCount;
            public uint ModeCount;
        }

        public List<MonitorInfo> EnumerateMonitors()
        {
            var result = new List<MonitorInfo>(4);
            var activeDisplays = EnumerateActiveDisplaySnapshots();

            if (activeDisplays.Count == 0)
                activeDisplays = EnumerateAttachedDisplaySnapshotsFallback();

            Dictionary<string, Win32Display.DISPLAY_DEVICE> adapters = EnumerateDisplayAdaptersByName();
            ActiveDisplayConfigSnapshot activeDisplayConfig = CaptureActiveDisplayConfigSnapshot();

            for (int i = 0; i < activeDisplays.Count; i++)
            {
                DisplaySnapshot active = activeDisplays[i];
                if (string.IsNullOrWhiteSpace(active.DeviceName) || !adapters.TryGetValue(active.DeviceName, out var adapter))
                    continue;

                if ((adapter.StateFlags & Win32Display.DISPLAY_DEVICE_MIRRORING_DRIVER) != 0)
                    continue;
                if ((adapter.StateFlags & Win32Display.DISPLAY_DEVICE_REMOTE) != 0)
                    continue;
                if ((adapter.StateFlags & Win32Display.DISPLAY_DEVICE_DISCONNECT) != 0)
                    continue;

                var monitor = new MonitorInfo
                {
                    DeviceName = adapter.DeviceName,
                    DeviceId = adapter.DeviceID,
                    DeviceKey = adapter.DeviceKey,
                    IsPrimary = active.IsPrimary || (adapter.StateFlags & Win32Display.DISPLAY_DEVICE_PRIMARY_DEVICE) != 0,
                    IsAttached = true,
                    AdapterName = adapter.DeviceString,
                    AdapterDeviceId = adapter.DeviceID,
                    PositionX = active.Left,
                    PositionY = active.Top
                };

                LoadCurrentSettings(monitor, activeDisplayConfig);

                if (TryGetMonitorDevice(adapter.DeviceName, out var monitorDevice))
                {
                    if (!string.IsNullOrWhiteSpace(monitorDevice.DeviceString))
                        monitor.FriendlyName = monitorDevice.DeviceString;

                    if (!string.IsNullOrWhiteSpace(monitorDevice.DeviceID))
                        monitor.DeviceId = monitorDevice.DeviceID;

                    if (!string.IsNullOrWhiteSpace(monitorDevice.DeviceKey))
                        monitor.DeviceKey = monitorDevice.DeviceKey;
                }

                LoadEdid(monitor);
                LoadWindowsMonitorName(monitor);
                LoadSupportedModes(monitor);
                FillMissingMetricsFromModes(monitor);
                ResolveNvapiDeviceName(monitor);
                AttachNvidiaSaturation(monitor);

                if (string.IsNullOrWhiteSpace(monitor.FriendlyName))
                    monitor.FriendlyName = "Generic PnP Monitor";

                result.Add(monitor);
            }

            return result;
        }

        private static List<DisplaySnapshot> EnumerateActiveDisplaySnapshots()
        {
            var result = new List<DisplaySnapshot>(4);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                Win32Display.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                    delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref Win32Display.RECT rect, IntPtr data)
                    {
                        var info = new Win32Display.MONITORINFOEX
                        {
                            cbSize = Marshal.SizeOf(typeof(Win32Display.MONITORINFOEX))
                        };

                        if (Win32Display.GetMonitorInfoW(hMonitor, ref info) &&
                            !string.IsNullOrWhiteSpace(info.szDevice) &&
                            seen.Add(info.szDevice))
                        {
                            result.Add(new DisplaySnapshot
                            {
                                DeviceName = info.szDevice,
                                IsPrimary = (info.dwFlags & Win32Display.MONITORINFOF_PRIMARY) != 0,
                                Left = info.rcMonitor.Left,
                                Top = info.rcMonitor.Top
                            });
                        }

                        return true;
                    }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("EnumDisplayMonitors failed: " + ex.Message);
                result.Clear();
            }

            return result;
        }

        private static List<DisplaySnapshot> EnumerateAttachedDisplaySnapshotsFallback()
        {
            var result = new List<DisplaySnapshot>(4);
            uint adapterIndex = 0;
            while (true)
            {
                var adapter = CreateDisplayDevice();
                if (Win32Display.EnumDisplayDevices(null, adapterIndex, ref adapter, 0) == 0)
                    break;
                adapterIndex++;

                if ((adapter.StateFlags & Win32Display.DISPLAY_DEVICE_MIRRORING_DRIVER) != 0)
                    continue;
                if ((adapter.StateFlags & Win32Display.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0)
                    continue;

                result.Add(new DisplaySnapshot
                {
                    DeviceName = adapter.DeviceName,
                    IsPrimary = (adapter.StateFlags & Win32Display.DISPLAY_DEVICE_PRIMARY_DEVICE) != 0
                });
            }

            return result;
        }

        private static Dictionary<string, Win32Display.DISPLAY_DEVICE> EnumerateDisplayAdaptersByName()
        {
            var result = new Dictionary<string, Win32Display.DISPLAY_DEVICE>(StringComparer.OrdinalIgnoreCase);

            for (uint adapterIndex = 0; ; adapterIndex++)
            {
                var candidate = CreateDisplayDevice();
                if (Win32Display.EnumDisplayDevices(null, adapterIndex, ref candidate, 0) == 0)
                    break;

                if (string.IsNullOrWhiteSpace(candidate.DeviceName) || result.ContainsKey(candidate.DeviceName))
                    continue;

                result.Add(candidate.DeviceName, candidate);
            }

            return result;
        }

        private static bool TryGetMonitorDevice(string adapterName, out Win32Display.DISPLAY_DEVICE monitor)
        {
            monitor = CreateDisplayDevice();
            if (string.IsNullOrWhiteSpace(adapterName))
                return false;

            Win32Display.DISPLAY_DEVICE first = CreateDisplayDevice();
            bool hasFirst = false;

            for (uint i = 0; i < 16; i++)
            {
                var candidate = CreateDisplayDevice();
                if (Win32Display.EnumDisplayDevices(adapterName, i, ref candidate, 0) == 0)
                    break;

                if (!hasFirst)
                {
                    first = candidate;
                    hasFirst = true;
                }

                if ((candidate.StateFlags & Win32Display.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0)
                {
                    monitor = candidate;
                    return true;
                }
            }

            if (hasFirst)
            {
                monitor = first;
                return true;
            }

            return false;
        }

        private static Win32Display.DISPLAY_DEVICE CreateDisplayDevice()
        {
            return new Win32Display.DISPLAY_DEVICE
            {
                cb = Marshal.SizeOf(typeof(Win32Display.DISPLAY_DEVICE))
            };
        }

        private void LoadCurrentSettings(MonitorInfo m, ActiveDisplayConfigSnapshot activeDisplayConfig)
        {
            var dm = CreateDevMode();
            if (Win32Display.EnumDisplaySettingsEx(m.DeviceName, Win32Display.ENUM_CURRENT_SETTINGS, ref dm, 0) != 0)
            {
                m.CurrentWidth = dm.dmPelsWidth;
                m.CurrentHeight = dm.dmPelsHeight;
                m.CurrentRefreshRate = dm.dmDisplayFrequency;
                m.CurrentBitsPerPixel = dm.dmBitsPerPel == 0 ? DefaultBitsPerPixel : dm.dmBitsPerPel;
                m.PositionX = dm.dmPositionX;
                m.PositionY = dm.dmPositionY;

                if (TryGetActiveDisplayRefreshRate(activeDisplayConfig, m.DeviceName, dm, out var numerator, out var denominator, out var milliHz))
                {
                    m.CurrentRefreshRateNumerator = numerator;
                    m.CurrentRefreshRateDenominator = denominator;
                    m.CurrentRefreshRateMilliHz = milliHz;
                    if (milliHz > 0)
                        m.CurrentRefreshRate = (uint)Math.Round(milliHz / 1000.0, MidpointRounding.AwayFromZero);
                }
                else if (m.CurrentRefreshRate > 0)
                {
                    m.CurrentRefreshRateMilliHz = m.CurrentRefreshRate * 1000U;
                    m.CurrentRefreshRateNumerator = m.CurrentRefreshRate;
                    m.CurrentRefreshRateDenominator = 1;
                }
            }
        }

        private static ActiveDisplayConfigSnapshot CaptureActiveDisplayConfigSnapshot()
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                int sizes = Win32Display.GetDisplayConfigBufferSizes(Win32Display.QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount);
                if (sizes != Win32Display.ERROR_SUCCESS || pathCount == 0 || modeCount == 0)
                    return null;

                var paths = new Win32Display.DISPLAYCONFIG_PATH_INFO[(int)pathCount];
                var modes = new Win32Display.DISPLAYCONFIG_MODE_INFO[(int)modeCount];
                uint queryPathCount = pathCount;
                uint queryModeCount = modeCount;
                int query = Win32Display.QueryDisplayConfig(
                    Win32Display.QDC_ONLY_ACTIVE_PATHS,
                    ref queryPathCount,
                    paths,
                    ref queryModeCount,
                    modes,
                    IntPtr.Zero);

                if (query == Win32Display.ERROR_INSUFFICIENT_BUFFER)
                {
                    NativeWait.Sleep(60);
                    continue;
                }

                if (query != Win32Display.ERROR_SUCCESS)
                    return null;

                return new ActiveDisplayConfigSnapshot
                {
                    Paths = paths,
                    Modes = modes,
                    PathCount = queryPathCount,
                    ModeCount = queryModeCount
                };
            }

            return null;
        }

        private static bool TryGetActiveDisplayRefreshRate(ActiveDisplayConfigSnapshot snapshot, string deviceName, Win32Display.DEVMODE currentMode, out uint numerator, out uint denominator, out uint milliHz)
        {
            numerator = 0;
            denominator = 0;
            milliHz = 0;

            if (snapshot == null || snapshot.Paths == null || snapshot.Modes == null || snapshot.PathCount == 0 || snapshot.ModeCount == 0)
                return false;

            if (!string.IsNullOrWhiteSpace(deviceName))
            {
                for (uint i = 0; i < snapshot.PathCount; i++)
                {
                    Win32Display.DISPLAYCONFIG_PATH_INFO path = snapshot.Paths[(int)i];
                    if (!TryGetDisplayConfigSourceName(path.sourceInfo, out var sourceDeviceName) ||
                        !SameDisplayDevice(sourceDeviceName, deviceName) ||
                        !DisplayConfigPathMatchesCurrentMode(path, snapshot.Modes, snapshot.ModeCount, currentMode))
                    {
                        continue;
                    }

                    if (TryReadPathRefresh(path, snapshot.Modes, snapshot.ModeCount, out numerator, out denominator, out milliHz))
                        return true;
                }
            }

            for (uint i = 0; i < snapshot.PathCount; i++)
            {
                Win32Display.DISPLAYCONFIG_PATH_INFO path = snapshot.Paths[(int)i];
                if (!DisplayConfigPathMatchesCurrentMode(path, snapshot.Modes, snapshot.ModeCount, currentMode))
                    continue;

                if (TryReadPathRefresh(path, snapshot.Modes, snapshot.ModeCount, out numerator, out denominator, out milliHz))
                    return true;
            }

            return false;
        }

        private static bool TryReadPathRefresh(
            Win32Display.DISPLAYCONFIG_PATH_INFO path,
            Win32Display.DISPLAYCONFIG_MODE_INFO[] modes,
            uint modeCount,
            out uint numerator,
            out uint denominator,
            out uint milliHz)
        {
            numerator = 0;
            denominator = 0;
            milliHz = 0;

            Win32Display.DISPLAYCONFIG_RATIONAL rate = path.targetInfo.refreshRate;
            if (rate.Numerator == 0 || rate.Denominator == 0)
                rate = TryGetTargetModeVSync(path, modes, modeCount);

            if (rate.Numerator == 0 || rate.Denominator == 0)
                return false;

            numerator = rate.Numerator;
            denominator = rate.Denominator;
            double valueMilliHz = ((double)numerator / denominator) * 1000.0;
            if (double.IsNaN(valueMilliHz) || double.IsInfinity(valueMilliHz) || valueMilliHz <= 0.0 || valueMilliHz > uint.MaxValue)
                return false;

            milliHz = (uint)Math.Round(valueMilliHz, MidpointRounding.AwayFromZero);
            return milliHz > 0;
        }

        private static bool TryGetDisplayConfigSourceName(Win32Display.DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo, out string deviceName)
        {
            deviceName = null;
            var request = new Win32Display.DISPLAYCONFIG_SOURCE_DEVICE_NAME
            {
                header = new Win32Display.DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = Win32Display.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME,
                    size = (uint)Marshal.SizeOf(typeof(Win32Display.DISPLAYCONFIG_SOURCE_DEVICE_NAME)),
                    adapterId = sourceInfo.adapterId,
                    id = sourceInfo.id
                }
            };

            int status = Win32Display.DisplayConfigGetDeviceInfo(ref request);
            if (status != Win32Display.ERROR_SUCCESS || string.IsNullOrWhiteSpace(request.viewGdiDeviceName))
                return false;

            deviceName = request.viewGdiDeviceName;
            return true;
        }

        private static bool DisplayConfigPathMatchesCurrentMode(Win32Display.DISPLAYCONFIG_PATH_INFO path, Win32Display.DISPLAYCONFIG_MODE_INFO[] modes, uint modeCount, Win32Display.DEVMODE currentMode)
        {
            const uint DISPLAYCONFIG_PATH_MODE_IDX_INVALID = 0xFFFFFFFF;
            const uint DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE = 1;

            if (path.sourceInfo.modeInfoIdx == DISPLAYCONFIG_PATH_MODE_IDX_INVALID || path.sourceInfo.modeInfoIdx >= modeCount)
                return false;

            Win32Display.DISPLAYCONFIG_MODE_INFO mode = modes[(int)path.sourceInfo.modeInfoIdx];
            if (mode.infoType != DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
                return false;

            return mode.sourceMode.width == currentMode.dmPelsWidth &&
                   mode.sourceMode.height == currentMode.dmPelsHeight &&
                   mode.sourceMode.position.x == currentMode.dmPositionX &&
                   mode.sourceMode.position.y == currentMode.dmPositionY;
        }

        private static Win32Display.DISPLAYCONFIG_RATIONAL TryGetTargetModeVSync(Win32Display.DISPLAYCONFIG_PATH_INFO path, Win32Display.DISPLAYCONFIG_MODE_INFO[] modes, uint modeCount)
        {
            const uint DISPLAYCONFIG_PATH_MODE_IDX_INVALID = 0xFFFFFFFF;
            const uint DISPLAYCONFIG_MODE_INFO_TYPE_TARGET = 2;
            if (path.targetInfo.modeInfoIdx == DISPLAYCONFIG_PATH_MODE_IDX_INVALID || path.targetInfo.modeInfoIdx >= modeCount)
                return new Win32Display.DISPLAYCONFIG_RATIONAL();

            Win32Display.DISPLAYCONFIG_MODE_INFO mode = modes[(int)path.targetInfo.modeInfoIdx];
            if (mode.infoType != DISPLAYCONFIG_MODE_INFO_TYPE_TARGET)
                return new Win32Display.DISPLAYCONFIG_RATIONAL();

            return mode.targetMode.targetVideoSignalInfo.vSyncFreq;
        }

        private void LoadEdid(MonitorInfo m)
        {
            byte[] edid = Win32Monitor.ReadEdidFromRegistry(m.DeviceId);
            if (edid == null) return;
            var info = Win32Monitor.ParseEdid(edid);
            if (!info.IsValid) return;

            if (!string.IsNullOrWhiteSpace(info.Manufacturer)) m.Manufacturer = info.Manufacturer;
            if (!string.IsNullOrWhiteSpace(info.ModelName) && info.ModelName != "0") m.FriendlyName = info.ModelName;
            if (!string.IsNullOrWhiteSpace(info.SerialNumber)) m.SerialNumber = info.SerialNumber;
            m.NativeWidth = info.NativeWidth;
            m.NativeHeight = info.NativeHeight;
            m.NativeRefreshRateHz = info.NativeRefreshRateHz;
            m.NativeRefreshRateMilliHz = info.NativeRefreshRateMilliHz;
            m.NativeRefreshRateNumerator = info.NativeRefreshRateNumerator;
            m.NativeRefreshRateDenominator = info.NativeRefreshRateDenominator;
            m.MaxVerticalRate = info.MaxVerticalRate;
            m.MinVerticalRate = info.MinVerticalRate;
            m.MaxHorizontalRate = info.MaxHorizontalRate;
            m.MinHorizontalRate = info.MinHorizontalRate;
            m.MaxPixelClockMhz = info.MaxPixelClockMhz;
            m.DiagonalInch = info.DiagonalInch;
            m.ProductCode = info.ProductCode.ToString();
            m.ManufactureYear = info.Year;
            m.ManufactureWeek = info.Week;
        }

        private void LoadWindowsMonitorName(MonitorInfo m)
        {
            try
            {
                var info = Win32Monitor.ReadMonitorIdFromWmi(m.DeviceId);
                if (info == null) return;

                if (!string.IsNullOrWhiteSpace(info.FriendlyName))
                    m.FriendlyName = info.FriendlyName;
                if (!string.IsNullOrWhiteSpace(info.Manufacturer))
                    m.Manufacturer = info.Manufacturer;
                if (!string.IsNullOrWhiteSpace(info.SerialNumber))
                    m.SerialNumber = info.SerialNumber;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LoadWindowsMonitorName failed: " + ex.Message);
            }
        }

        private void LoadSupportedModes(MonitorInfo m)
        {
            m.SupportedModes.Clear();
            var modes = new List<ResolutionMode>(128);
            var seen = new HashSet<ResolutionModeKey>();

            for (int mode = 0; mode < MaxDisplayModeEnumeration; mode++)
            {
                var dm = CreateDevMode();
                if (Win32Display.EnumDisplaySettingsEx(m.DeviceName, mode, ref dm, 0) == 0)
                    break;

                uint width = dm.dmPelsWidth;
                uint height = dm.dmPelsHeight;
                uint refresh = dm.dmDisplayFrequency;
                uint bpp = dm.dmBitsPerPel == 0 ? DefaultBitsPerPixel : dm.dmBitsPerPel;
                bool interlaced = (dm.dmDisplayFlags & 0x2) != 0;

                if (width == 0 || height == 0 || refresh == 0)
                    continue;

                if (bpp < DefaultBitsPerPixel)
                    continue;

                var key = new ResolutionModeKey(width, height, bpp, refresh, interlaced);
                if (!seen.Add(key))
                    continue;

                modes.Add(new ResolutionMode
                {
                    Width = width,
                    Height = height,
                    RefreshRate = refresh,
                    BitsPerPixel = bpp,
                    IsInterlaced = interlaced
                });
            }

            modes.Sort(SupportedModeSortComparison);

            for (int i = 0; i < modes.Count; i++)
                m.SupportedModes.Add(modes[i]);
        }

        private static int CompareSupportedModes(ResolutionMode a, ResolutionMode b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int area = ((long)b.Width * b.Height).CompareTo((long)a.Width * a.Height);
            if (area != 0) return area;

            int refresh = b.RefreshRate.CompareTo(a.RefreshRate);
            if (refresh != 0) return refresh;

            int bits = a.BitsPerPixel.CompareTo(b.BitsPerPixel);
            if (bits != 0) return bits;

            return a.IsInterlaced == b.IsInterlaced ? 0 : (a.IsInterlaced ? 1 : -1);
        }

        private static Win32Display.DEVMODE CreateDevMode()
        {
            return new Win32Display.DEVMODE
            {
                dmSize = (ushort)Marshal.SizeOf(typeof(Win32Display.DEVMODE)),
                dmDriverExtra = 0
            };
        }

        private void FillMissingMetricsFromModes(MonitorInfo m)
        {
            if (m == null) return;

            if (m.SupportedModes.Count == 0)
            {
                FillMissingMetricsFromCurrentMode(m);
                return;
            }

            uint minRefresh = uint.MaxValue;
            uint maxRefresh = 0;
            ResolutionMode best = null;

            for (int i = 0; i < m.SupportedModes.Count; i++)
            {
                ResolutionMode mode = m.SupportedModes[i];
                if (mode.Width == 0 || mode.Height == 0) continue;

                long modeArea = (long)mode.Width * mode.Height;
                long bestArea = best == null ? 0 : (long)best.Width * best.Height;
                if (best == null ||
                    modeArea > bestArea ||
                    (modeArea == bestArea && mode.RefreshRate > best.RefreshRate))
                {
                    best = mode;
                }

                if (mode.RefreshRate > 0)
                {
                    if (mode.RefreshRate < minRefresh) minRefresh = mode.RefreshRate;
                    if (mode.RefreshRate > maxRefresh) maxRefresh = mode.RefreshRate;
                }
            }

            if (m.NativeWidth == 0 || m.NativeHeight == 0)
            {
                if (m.CurrentWidth > 0 && m.CurrentHeight > 0)
                {
                    m.NativeWidth = m.CurrentWidth;
                    m.NativeHeight = m.CurrentHeight;
                }
                else if (best != null)
                {
                    m.NativeWidth = best.Width;
                    m.NativeHeight = best.Height;
                }
            }

            if (m.MinVerticalRate == 0 && minRefresh != uint.MaxValue)
                m.MinVerticalRate = minRefresh;
            if (m.MaxVerticalRate == 0 && maxRefresh > 0)
                m.MaxVerticalRate = maxRefresh;

            FillMissingMetricsFromCurrentMode(m);
        }

        private static void FillMissingMetricsFromCurrentMode(MonitorInfo m)
        {
            if (m == null) return;

            if ((m.NativeWidth == 0 || m.NativeHeight == 0) && m.CurrentWidth > 0 && m.CurrentHeight > 0)
            {
                m.NativeWidth = m.CurrentWidth;
                m.NativeHeight = m.CurrentHeight;
            }

            if (m.CurrentRefreshRate > 0)
            {
                if (m.MinVerticalRate == 0) m.MinVerticalRate = m.CurrentRefreshRate;
                if (m.MaxVerticalRate == 0) m.MaxVerticalRate = m.CurrentRefreshRate;
            }

            if (m.NativeRefreshRateMilliHz == 0 && m.CurrentRefreshRateMilliHz > 0 &&
                m.NativeWidth == m.CurrentWidth && m.NativeHeight == m.CurrentHeight)
            {
                m.NativeRefreshRateMilliHz = m.CurrentRefreshRateMilliHz;
                m.NativeRefreshRateHz = m.CurrentRefreshRate;
                m.NativeRefreshRateNumerator = m.CurrentRefreshRateNumerator;
                m.NativeRefreshRateDenominator = m.CurrentRefreshRateDenominator;
            }
        }

        private void AttachNvidiaSaturation(MonitorInfo m)
        {
            try
            {
                if (!_nv.IsInitialized) return;
                string nvapiDeviceName = GetNvapiDeviceName(m);
                if (!_nv.CanControlSaturation(nvapiDeviceName)) return;

                int lvl = _nv.GetSaturationForDisplay(nvapiDeviceName, out int defaultLevel);
                m.SaturationDefault = defaultLevel > 0 ? defaultLevel : 50;
                m.SaturationSupported = true;
                if (lvl >= 0)
                    m.Saturation = lvl;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AttachNvidiaSaturation failed: " + ex.Message);
                m.SaturationSupported = false;
            }
        }
    }
}
