using DISPLAY_SCALER.Infrastructure.Timing;
using DISPLAY_SCALER.Models;
using DISPLAY_SCALER.Native;
using System;
using System.Collections.Generic;

namespace DISPLAY_SCALER.Services
{
    public sealed partial class DisplayService
    {
        private sealed class ProtectedNativeMode
        {
            public uint Width;
            public uint Height;
            public uint RefreshRate;
            public uint BitsPerPixel;
            public string Reason;

            public string Label
            {
                get { return Width + "×" + Height + " @ " + RefreshRate + " Гц"; }
            }

            public CustomResolution ToResolution()
            {
                return new CustomResolution
                {
                    Width = Width,
                    Height = Height,
                    RefreshRate = RefreshRate,
                    BitsPerPixel = BitsPerPixel == 0 ? DefaultBitsPerPixel : BitsPerPixel,
                    Origin = ResolutionOrigin.System,
                    AddedByDisplayScaler = false
                };
            }
        }

        private sealed class ProtectedNativeModeSnapshot
        {
            public readonly List<ProtectedNativeMode> Modes = new List<ProtectedNativeMode>(8);

            public bool HasModes
            {
                get { return Modes.Count > 0; }
            }
        }

        private ProtectedNativeModeSnapshot CaptureProtectedNativeModes(MonitorInfo monitor, CustomResolution newMode)
        {
            var snapshot = new ProtectedNativeModeSnapshot();
            if (monitor == null)
                return snapshot;
            if (TryGetCurrentDevMode(monitor, out var current))
            {
                uint currentBpp = current.dmBitsPerPel == 0 ? DefaultBitsPerPixel : current.dmBitsPerPel;
                uint currentRefresh = current.dmDisplayFrequency == 0 ? monitor.CurrentRefreshRate : current.dmDisplayFrequency;
                AddProtectedNativeMode(snapshot, current.dmPelsWidth, current.dmPelsHeight, currentRefresh, currentBpp, "current desktop mode before NVAPI trial/save");
            }

            uint nativeWidth = monitor.NativeWidth > 0 ? monitor.NativeWidth : monitor.CurrentWidth;
            uint nativeHeight = monitor.NativeHeight > 0 ? monitor.NativeHeight : monitor.CurrentHeight;
            uint currentWidth = monitor.CurrentWidth;
            uint currentHeight = monitor.CurrentHeight;

            if (newMode != null && newMode.RefreshRate > 0)
            {
                if (nativeWidth > 0 && nativeHeight > 0 && (newMode.Width != nativeWidth || newMode.Height != nativeHeight))
                {
                    if (RefreshEquals(newMode.RefreshRate, monitor.MaxVerticalRate) ||
                        RefreshEquals(newMode.RefreshRate, monitor.NativeRefreshRateHz) ||
                        RefreshEquals(newMode.RefreshRate, monitor.CurrentRefreshRate))
                    {
                        AddProtectedNativeMode(snapshot, nativeWidth, nativeHeight, newMode.RefreshRate, DefaultBitsPerPixel,
                            "native desktop mode at the same refresh as imported custom mode");
                    }
                }

                if (currentWidth > 0 && currentHeight > 0 && (newMode.Width != currentWidth || newMode.Height != currentHeight))
                {
                    if (RefreshEquals(newMode.RefreshRate, monitor.CurrentRefreshRate))
                    {
                        AddProtectedNativeMode(snapshot, currentWidth, currentHeight, newMode.RefreshRate, DefaultBitsPerPixel,
                            "current desktop resolution at the same refresh as imported custom mode");
                    }
                }
            }

            if (monitor.SupportedModes != null)
            {
                for (int i = 0; i < monitor.SupportedModes.Count; i++)
                {
                    ResolutionMode mode = monitor.SupportedModes[i];
                    if (mode == null || mode.Width == 0 || mode.Height == 0 || mode.RefreshRate == 0)
                        continue;
                    if (mode.BitsPerPixel != 0 && mode.BitsPerPixel < DefaultBitsPerPixel)
                        continue;
                    if (mode.IsInterlaced)
                        continue;

                    bool isNativeSize = nativeWidth > 0 && nativeHeight > 0 && mode.Width == nativeWidth && mode.Height == nativeHeight;
                    bool isCurrentSize = currentWidth > 0 && currentHeight > 0 && mode.Width == currentWidth && mode.Height == currentHeight;
                    if (!isNativeSize && !isCurrentSize)
                        continue;

                    string reason = isNativeSize ? "native target resolution before custom import" : "current target resolution before custom import";
                    AddProtectedNativeMode(snapshot, mode.Width, mode.Height, mode.RefreshRate, mode.BitsPerPixel == 0 ? DefaultBitsPerPixel : mode.BitsPerPixel, reason);
                }
            }

            return snapshot;
        }

        private static bool RefreshEquals(uint a, uint b)
        {
            if (a == 0 || b == 0)
                return false;
            return Math.Abs((long)a - (long)b) <= 1;
        }

        private static void AddProtectedNativeMode(ProtectedNativeModeSnapshot snapshot, uint width, uint height, uint refresh, uint bitsPerPixel, string reason)
        {
            if (snapshot == null || width == 0 || height == 0 || refresh == 0)
                return;

            uint bpp = bitsPerPixel == 0 ? DefaultBitsPerPixel : bitsPerPixel;
            if (bpp < DefaultBitsPerPixel)
                return;

            for (int i = 0; i < snapshot.Modes.Count; i++)
            {
                ProtectedNativeMode existing = snapshot.Modes[i];
                if (existing.Width == width &&
                    existing.Height == height &&
                    Math.Abs((long)existing.RefreshRate - (long)refresh) <= 1 &&
                    existing.BitsPerPixel == bpp)
                {
                    return;
                }
            }

            snapshot.Modes.Add(new ProtectedNativeMode
            {
                Width = width,
                Height = height,
                RefreshRate = refresh,
                BitsPerPixel = bpp,
                Reason = reason
            });
        }

        private ApplyResult VerifyProtectedNativeModesAfterCustomSave(MonitorInfo monitor, CustomResolution newMode, ProtectedNativeModeSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.HasModes)
                return ApplyResult.Ok("Native/current mode guard: нет исходных режимов для проверки.");

            var problems = new List<string>(4);
            for (int i = 0; i < snapshot.Modes.Count; i++)
            {
                ProtectedNativeMode mode = snapshot.Modes[i];
                if (!IsWindowsModeEnumerated(monitor, mode.ToResolution()))
                    problems.Add("не найден " + mode.Label + " (" + mode.Reason + ")");
            }

            ApplyResult hijackGuard = VerifyProtectedNativeModesAreNotHijacked(monitor, newMode, snapshot);
            if (!hijackGuard.Success)
                problems.Add(hijackGuard.Message + " " + hijackGuard.Detail);

            if (problems.Count == 0)
                return ApplyResult.Ok("Native/current mode guard: исходные режимы целевого монитора сохранились и не перехватываются новым custom mode.");

            string deleteText = string.Empty;
            if (newMode != null && _nv != null && _nv.IsInitialized)
            {
                bool deleted = _nv.DeleteCustomDisplay(
                    GetNvapiDeviceName(monitor),
                    newMode.Width,
                    newMode.Height,
                    newMode.RefreshRate,
                    newMode.RefreshRateMilliHz,
                    newMode.BitsPerPixel,
                    out var deleteError);

                ApplyResult refreshAfterRollback = RefreshWindowsModeList(monitor);
                NativeWait.Sleep(700);

                deleteText = deleted
                    ? " Новый custom mode " + FormatMode(newMode) + " удалён для отката. "
                    : " Автооткат через DeleteCustomDisplay не удался: " + deleteError + ". ";
                deleteText = string.Concat(deleteText, "Обновление mode list после отката: ", FormatApplyResult(refreshAfterRollback));
            }

            return ApplyResult.Fail(
                "Импорт отменён: новый custom mode изменил, скрыл или перехватил исходные режимы целевого монитора.",
                "Проблемы защиты native/current mode: " + string.Join("; ", problems) + "." + deleteText);
        }

        private ApplyResult VerifyProtectedNativeModesAreNotHijacked(MonitorInfo monitor, CustomResolution newMode, ProtectedNativeModeSnapshot snapshot)
        {
            if (monitor == null || newMode == null || snapshot == null || !snapshot.HasModes)
                return ApplyResult.Ok("Native/current hijack guard: нет режимов для runtime-проверки.");

            if (string.IsNullOrWhiteSpace(monitor.DeviceName))
                return ApplyResult.Ok("Native/current hijack guard: нет DeviceName для runtime-проверки.");

            bool haveCurrentBefore = TryGetCurrentDevMode(monitor, out var currentBefore);
            string currentBeforeText = haveCurrentBefore ? FormatDevMode(currentBefore) : "unknown";

            var problems = new List<string>(4);
            for (int i = 0; i < snapshot.Modes.Count; i++)
            {
                ProtectedNativeMode mode = snapshot.Modes[i];
                if (!ShouldRuntimeVerifyProtectedModeAgainstCustomMode(mode, newMode))
                    continue;

                if (!TryGetMatchingDevMode(monitor, mode.ToResolution(), 0, out var protectedModeDevMode))
                {
                    problems.Add("не удалось получить DEVMODE для " + mode.Label);
                    continue;
                }

                int changeResult = Win32Display.ChangeDisplaySettingsEx(monitor.DeviceName, ref protectedModeDevMode, IntPtr.Zero, Win32Display.CDS_FULLSCREEN, IntPtr.Zero);
                NativeWait.Sleep(650);

                if (changeResult != Win32Display.DISP_CHANGE_SUCCESSFUL || !TryGetCurrentDevMode(monitor, out var currentAfter))
                {
                    problems.Add(mode.Label + " не применился для проверки: " + Win32Display.ChangeResultToString(changeResult));
                    RestoreDisplayModeAfterGuard(monitor, haveCurrentBefore, ref currentBefore);
                    continue;
                }

                uint actualRefresh = currentAfter.dmDisplayFrequency == 0 ? mode.RefreshRate : currentAfter.dmDisplayFrequency;
                bool hijacked = currentAfter.dmPelsWidth != mode.Width ||
                                currentAfter.dmPelsHeight != mode.Height ||
                                !RefreshEquals(actualRefresh, mode.RefreshRate);

                if (hijacked)
                {
                    problems.Add("при временном выборе " + mode.Label + " Windows реально включил " + FormatDevMode(currentAfter) +
                        "; до проверки было " + currentBeforeText);
                }

                RestoreDisplayModeAfterGuard(monitor, haveCurrentBefore, ref currentBefore);
                NativeWait.Sleep(350);
            }

            if (problems.Count > 0)
            {
                return ApplyResult.Fail(
                    "Native/current hijack guard: обнаружен перехват родного режима новым custom mode.",
                    string.Join("; ", problems));
            }

            return ApplyResult.Ok("Native/current hijack guard: родные режимы применяются как сами себя.");
        }

        private static bool ShouldRuntimeVerifyProtectedModeAgainstCustomMode(ProtectedNativeMode protectedMode, CustomResolution newMode)
        {
            if (protectedMode == null || newMode == null)
                return false;
            if (!RefreshEquals(protectedMode.RefreshRate, newMode.RefreshRate))
                return false;
            return protectedMode.Width != newMode.Width || protectedMode.Height != newMode.Height;
        }

        private static void RestoreDisplayModeAfterGuard(MonitorInfo monitor, bool haveCurrentBefore, ref Win32Display.DEVMODE currentBefore)
        {
            if (!haveCurrentBefore || monitor == null || string.IsNullOrWhiteSpace(monitor.DeviceName))
                return;

            Win32Display.ChangeDisplaySettingsEx(monitor.DeviceName, ref currentBefore, IntPtr.Zero, Win32Display.CDS_FULLSCREEN, IntPtr.Zero);
        }

        private static bool TryGetCurrentDevMode(MonitorInfo monitor, out Win32Display.DEVMODE dm)
        {
            dm = CreateDevMode();
            if (monitor == null || string.IsNullOrWhiteSpace(monitor.DeviceName))
                return false;

            return Win32Display.EnumDisplaySettingsEx(monitor.DeviceName, Win32Display.ENUM_CURRENT_SETTINGS, ref dm, 0) != 0;
        }

        private static bool WaitForCurrentDisplayMode(MonitorInfo monitor, CustomResolution normalized, int timeoutMs, out string currentText)
        {
            currentText = "не удалось прочитать текущий режим";
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs <= 0 ? 1 : timeoutMs);

            do
            {
                if (TryGetCurrentDevMode(monitor, out var dm))
                {
                    currentText = FormatDevMode(dm);
                    if (DevModeMatchesResolution(dm, normalized))
                        return true;
                }

                NativeWait.Sleep(150);
            }
            while (DateTime.UtcNow < deadline);

            return false;
        }

        private static bool DevModeMatchesResolution(Win32Display.DEVMODE dm, CustomResolution normalized)
        {
            if (normalized == null)
                return false;

            uint frequency = dm.dmDisplayFrequency == 0 ? normalized.RefreshRate : dm.dmDisplayFrequency;
            uint bpp = dm.dmBitsPerPel == 0 ? DefaultBitsPerPixel : dm.dmBitsPerPel;
            return dm.dmPelsWidth == normalized.Width &&
                   dm.dmPelsHeight == normalized.Height &&
                   Math.Abs((long)frequency - (long)normalized.RefreshRate) <= 1 &&
                   bpp >= DefaultBitsPerPixel;
        }

        private static string FormatDevMode(Win32Display.DEVMODE dm)
        {
            uint bpp = dm.dmBitsPerPel == 0 ? DefaultBitsPerPixel : dm.dmBitsPerPel;
            return dm.dmPelsWidth + "x" + dm.dmPelsHeight + "x" + bpp + "@" + dm.dmDisplayFrequency;
        }

        public bool HasNativeRefreshHijackRisk(MonitorInfo monitor, CustomResolution res, out string reason)
        {
            reason = null;
            if (monitor == null || res == null)
                return false;
            return HasNativeRefreshHijackRiskInternal(monitor, NormalizeResolution(res), out reason);
        }

        private static bool HasNativeRefreshHijackRiskInternal(MonitorInfo monitor, CustomResolution mode, out string reason)
        {
            reason = null;
            if (monitor == null || mode == null || mode.Width == 0 || mode.Height == 0 || mode.RefreshRate == 0)
                return false;

            uint nativeWidth = monitor.NativeWidth > 0 ? monitor.NativeWidth : monitor.CurrentWidth;
            uint nativeHeight = monitor.NativeHeight > 0 ? monitor.NativeHeight : monitor.CurrentHeight;
            uint currentWidth = monitor.CurrentWidth;
            uint currentHeight = monitor.CurrentHeight;

            if (CheckSingleNativeHijackRisk(mode, nativeWidth, nativeHeight, monitor.NativeRefreshRateHz, "native refresh", out reason))
                return true;
            if (CheckSingleNativeHijackRisk(mode, nativeWidth, nativeHeight, monitor.MaxVerticalRate, "max vertical refresh", out reason))
                return true;
            if (CheckSingleNativeHijackRisk(mode, currentWidth, currentHeight, monitor.CurrentRefreshRate, "current desktop refresh", out reason))
                return true;

            if (monitor.SupportedModes != null)
            {
                for (int i = 0; i < monitor.SupportedModes.Count; i++)
                {
                    ResolutionMode candidate = monitor.SupportedModes[i];
                    if (candidate == null || candidate.Width == 0 || candidate.Height == 0 || candidate.RefreshRate == 0)
                        continue;

                    bool isNativeSize = nativeWidth > 0 && nativeHeight > 0 && candidate.Width == nativeWidth && candidate.Height == nativeHeight;
                    bool isCurrentSize = currentWidth > 0 && currentHeight > 0 && candidate.Width == currentWidth && candidate.Height == currentHeight;
                    if (!isNativeSize && !isCurrentSize)
                        continue;

                    string label = isNativeSize ? "native listed mode" : "current listed mode";
                    if (CheckSingleNativeHijackRisk(mode, candidate.Width, candidate.Height, candidate.RefreshRate, label, out reason))
                        return true;
                }
            }

            return false;
        }

        private static bool CheckSingleNativeHijackRisk(CustomResolution customMode, uint protectedWidth, uint protectedHeight, uint protectedRefresh, string protectedReason, out string reason)
        {
            reason = null;
            if (customMode == null || protectedWidth == 0 || protectedHeight == 0 || protectedRefresh == 0)
                return false;
            if (!RefreshEquals(customMode.RefreshRate, protectedRefresh))
                return false;
            if (customMode.Width == protectedWidth && customMode.Height == protectedHeight)
                return false;

            bool sameWidthTaller = customMode.Width == protectedWidth && customMode.Height > protectedHeight;
            bool sameHeightWider = customMode.Height == protectedHeight && customMode.Width > protectedWidth;
            if (!sameWidthTaller && !sameHeightWider)
                return false;

            reason = "Custom mode " + customMode.Width + "×" + customMode.Height + " @ " + customMode.RefreshRate +
                " Гц conflicts with protected " + protectedWidth + "×" + protectedHeight + " @ " + protectedRefresh +
                " Гц (" + (protectedReason ?? "native/current mode") + "). It keeps one native axis and exceeds the other at the same refresh, which can hijack the native Windows mode entry.";
            return true;
        }

        private sealed class ActiveDisplayModeSnapshot
        {
            public readonly List<ActiveDisplayMode> Modes = new List<ActiveDisplayMode>(8);
        }

        private sealed class ActiveDisplayMode
        {
            public string DeviceName;
            public Win32Display.DEVMODE Mode;

            public string Label
            {
                get { return DeviceName + " " + FormatDevMode(Mode); }
            }
        }

        private static ActiveDisplayModeSnapshot CaptureActiveDisplayModeSnapshot(string targetDeviceName)
        {
            var snapshot = new ActiveDisplayModeSnapshot();

            try
            {
                for (uint i = 0; i < 32; i++)
                {
                    var device = new Win32Display.DISPLAY_DEVICE();
                    device.cb = MarshalSizeOfDisplayDevice();
                    if (Win32Display.EnumDisplayDevices(null, i, ref device, 0) == 0)
                        break;

                    if ((device.StateFlags & Win32Display.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0)
                        continue;
                    if ((device.StateFlags & Win32Display.DISPLAY_DEVICE_MIRRORING_DRIVER) != 0)
                        continue;
                    if ((device.StateFlags & Win32Display.DISPLAY_DEVICE_REMOTE) != 0)
                        continue;
                    if ((device.StateFlags & Win32Display.DISPLAY_DEVICE_DISCONNECT) != 0)
                        continue;
                    if (string.IsNullOrWhiteSpace(device.DeviceName))
                        continue;
                    if (SameDisplayDevice(device.DeviceName, targetDeviceName))
                        continue;

                    var dm = CreateDevMode();
                    if (Win32Display.EnumDisplaySettingsEx(device.DeviceName, Win32Display.ENUM_CURRENT_SETTINGS, ref dm, 0) == 0)
                        continue;
                    if (dm.dmPelsWidth == 0 || dm.dmPelsHeight == 0)
                        continue;

                    PrepareEnumeratedModeForRestore(ref dm);

                    snapshot.Modes.Add(new ActiveDisplayMode
                    {
                        DeviceName = device.DeviceName,
                        Mode = dm
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Active display mode snapshot failed: " + ex.Message);
            }

            return snapshot;
        }

        private static string RestoreNonTargetDisplayModes(ActiveDisplayModeSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Modes.Count == 0)
                return "Multi-monitor guard: no secondary active displays captured.";

            var details = new List<string>(snapshot.Modes.Count);
            for (int i = 0; i < snapshot.Modes.Count; i++)
            {
                ActiveDisplayMode expected = snapshot.Modes[i];
                if (expected == null || string.IsNullOrWhiteSpace(expected.DeviceName))
                    continue;

                var current = CreateDevMode();
                if (Win32Display.EnumDisplaySettingsEx(expected.DeviceName, Win32Display.ENUM_CURRENT_SETTINGS, ref current, 0) == 0)
                {
                    details.Add(expected.DeviceName + ": current mode read failed");
                    continue;
                }

                if (DevModesSameDesktopMode(current, expected.Mode))
                {
                    details.Add(expected.DeviceName + ": unchanged");
                    continue;
                }

                Win32Display.DEVMODE restore = expected.Mode;
                PrepareEnumeratedModeForRestore(ref restore);

                int test = Win32Display.ChangeDisplaySettingsEx(expected.DeviceName, ref restore, IntPtr.Zero, Win32Display.CDS_TEST, IntPtr.Zero);
                if (test != Win32Display.DISP_CHANGE_SUCCESSFUL)
                {
                    details.Add(expected.DeviceName + ": drift " + FormatDevMode(current) + " -> " + FormatDevMode(expected.Mode) + " not restored; CDS_TEST " + Win32Display.ChangeResultToString(test));
                    continue;
                }

                int apply = Win32Display.ChangeDisplaySettingsEx(expected.DeviceName, ref restore, IntPtr.Zero, 0, IntPtr.Zero);
                NativeWait.Sleep(350);

                if (apply == Win32Display.DISP_CHANGE_SUCCESSFUL)
                    details.Add(expected.DeviceName + ": restored " + FormatDevMode(current) + " -> " + FormatDevMode(expected.Mode));
                else
                    details.Add(expected.DeviceName + ": drift " + FormatDevMode(current) + " -> " + FormatDevMode(expected.Mode) + " restore failed: " + Win32Display.ChangeResultToString(apply));
            }

            return "Multi-monitor guard: " + string.Join("; ", details) + ".";
        }

        private static bool DevModesSameDesktopMode(Win32Display.DEVMODE a, Win32Display.DEVMODE b)
        {
            uint aRefresh = a.dmDisplayFrequency;
            uint bRefresh = b.dmDisplayFrequency;
            if (aRefresh == 0 || bRefresh == 0)
                aRefresh = bRefresh = Math.Max(aRefresh, bRefresh);

            uint aBpp = a.dmBitsPerPel == 0 ? DefaultBitsPerPixel : a.dmBitsPerPel;
            uint bBpp = b.dmBitsPerPel == 0 ? DefaultBitsPerPixel : b.dmBitsPerPel;

            return a.dmPelsWidth == b.dmPelsWidth &&
                   a.dmPelsHeight == b.dmPelsHeight &&
                   Math.Abs((long)aRefresh - (long)bRefresh) <= 1 &&
                   aBpp == bBpp &&
                   a.dmPositionX == b.dmPositionX &&
                   a.dmPositionY == b.dmPositionY &&
                   a.dmDisplayOrientation == b.dmDisplayOrientation;
        }

        private static bool SameDisplayDevice(string left, string right)
        {
            return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static int MarshalSizeOfDisplayDevice()
        {
            return System.Runtime.InteropServices.Marshal.SizeOf(typeof(Win32Display.DISPLAY_DEVICE));
        }
    }
}
