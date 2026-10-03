using DISPLAY_SCALER.Models;
using DISPLAY_SCALER.Native;
using System;

namespace DISPLAY_SCALER.Services
{
    public sealed partial class DisplayService
    {
        private void ResolveNvapiDeviceName(MonitorInfo monitor)
        {
            if (monitor == null)
                return;

            monitor.NvApiDeviceName = null;
            if (_nv == null || !_nv.IsInitialized)
                return;

            byte[] edid = Win32Monitor.ReadEdidFromRegistry(monitor.DeviceId);
            string resolved = _nv.ResolveDisplayNameForMonitor(monitor.DeviceName, monitor.DeviceId, edid, out var detail);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                monitor.NvApiDeviceName = resolved;
                return;
            }

            System.Diagnostics.Debug.WriteLine("ResolveNvapiDeviceName failed for " + (monitor.DeviceName ?? string.Empty) + ": " + (detail ?? string.Empty));
        }

        private string GetNvapiDeviceName(MonitorInfo monitor)
        {
            if (monitor == null)
                return null;

            if (string.IsNullOrWhiteSpace(monitor.NvApiDeviceName))
                ResolveNvapiDeviceName(monitor);

            return string.IsNullOrWhiteSpace(monitor.NvApiDeviceName)
                ? monitor.DeviceName
                : monitor.NvApiDeviceName;
        }
    }
}
