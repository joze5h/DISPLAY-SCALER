using DISPLAY_SCALER.Models;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Text;

namespace DISPLAY_SCALER.Services
{
    public sealed partial class DisplayService
    {
        private const string DisplayScalerMarkerRegistryPath = @"Software\DISPLAY-SCALER\CustomResolutionsV2";
        private const string StableDisplayScalerMarkerRegistryPath = @"Software\DISPLAY-SCALER\CustomResolutionsV3";
        private static readonly char[] InvalidRegistryNameChars = System.IO.Path.GetInvalidFileNameChars();

        public void MarkDisplayScalerResolution(MonitorInfo monitor, CustomResolution res)
        {
            if (monitor == null || res == null) return;
            RegisterDisplayScalerMode(monitor, NormalizeResolution(res));
        }

        private void RegisterDisplayScalerMode(MonitorInfo monitor, CustomResolution res)
        {
            try
            {
                RegisterDisplayScalerMode(StableDisplayScalerMarkerRegistryPath, BuildStableMonitorMarkerKey(monitor), res);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("RegisterDisplayScalerMode failed: " + ex.Message);
            }
        }

        private HashSet<ResolutionModeKey> ReadDisplayScalerModes(MonitorInfo monitor)
        {
            var set = new HashSet<ResolutionModeKey>();
            try
            {
                using (var stableKey = Registry.CurrentUser.OpenSubKey(StableDisplayScalerMarkerRegistryPath))
                {
                    ReadDisplayScalerModes(stableKey, BuildStableMonitorMarkerKey(monitor), set);
                }

                using (var legacyKey = Registry.CurrentUser.OpenSubKey(DisplayScalerMarkerRegistryPath))
                {
                    if (legacyKey == null)
                        return set;

                    string directLegacyKey = BuildMonitorMarkerKey(monitor);
                    ReadDisplayScalerModes(legacyKey, directLegacyKey, set);

                    string deviceIdFragment = SanitizeRegistryName(monitor == null ? null : monitor.DeviceId);
                    if (string.IsNullOrWhiteSpace(deviceIdFragment) || deviceIdFragment == "Display")
                        return set;

                    string[] valueNames = legacyKey.GetValueNames();
                    for (int i = 0; i < valueNames.Length; i++)
                    {
                        string valueName = valueNames[i];
                        if (string.Equals(valueName, directLegacyKey, StringComparison.OrdinalIgnoreCase) ||
                            valueName.IndexOf(deviceIdFragment, StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            continue;
                        }

                        ReadDisplayScalerModes(legacyKey, valueName, set);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ReadDisplayScalerModes failed: " + ex.Message);
            }

            return set;
        }

        private static void RegisterDisplayScalerMode(string path, string device, CustomResolution res)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(path, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (key == null)
                    return;

                string mode = FormatRegistryMode(res);
                string existing = key.GetValue(device) as string ?? string.Empty;
                if (!ContainsModeString(existing, res))
                    key.SetValue(device, string.IsNullOrEmpty(existing) ? mode : existing.TrimEnd('@') + "@" + mode, RegistryValueKind.String);
            }
        }

        private static void ReadDisplayScalerModes(RegistryKey key, string device, HashSet<ResolutionModeKey> set)
        {
            if (key == null || string.IsNullOrWhiteSpace(device) || set == null)
                return;

            AddModeListItems(key.GetValue(device) as string ?? string.Empty, set);
        }

        private static string SanitizeRegistryName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Display";

            string trimmed = value.Trim();
            var sb = new StringBuilder(trimmed.Length);
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                sb.Append(IsInvalidRegistryNameChar(c) ? '_' : c);
            }

            return sb.Length == 0 ? "Display" : sb.ToString();
        }

        private static string BuildMonitorMarkerKey(MonitorInfo monitor)
        {
            if (monitor == null)
                return "Display";

            string identity = string.Join("|", new[]
            {
                monitor.DeviceKey ?? string.Empty,
                monitor.DeviceId ?? string.Empty,
                monitor.DeviceName ?? string.Empty
            });

            return SanitizeRegistryName(identity);
        }

        private static string BuildStableMonitorMarkerKey(MonitorInfo monitor)
        {
            if (monitor == null)
                return "Display";

            if (!string.IsNullOrWhiteSpace(monitor.DeviceId))
                return SanitizeRegistryName(monitor.DeviceId);

            string edidIdentity = string.Join("|", new[]
            {
                monitor.Manufacturer ?? string.Empty,
                monitor.ProductCode ?? string.Empty,
                monitor.SerialNumber ?? string.Empty
            });
            if (!string.IsNullOrWhiteSpace(edidIdentity.Replace("|", string.Empty)))
                return SanitizeRegistryName(edidIdentity);

            return SanitizeRegistryName(monitor.DeviceName);
        }

        private static bool IsInvalidRegistryNameChar(char c)
        {
            if (c == '\\' || c == '/' || c == ':')
                return true;

            for (int i = 0; i < InvalidRegistryNameChars.Length; i++)
            {
                if (InvalidRegistryNameChars[i] == c)
                    return true;
            }

            return false;
        }

        private static bool RemoveDisplayScalerMode(MonitorInfo monitor, CustomResolution res)
        {
            try
            {
                bool changed = RemoveDisplayScalerMode(StableDisplayScalerMarkerRegistryPath, BuildStableMonitorMarkerKey(monitor), res);
                changed |= RemoveDisplayScalerMode(DisplayScalerMarkerRegistryPath, BuildMonitorMarkerKey(monitor), res);
                changed |= RemoveLegacyDisplayScalerModeFallback(monitor, res);
                return changed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("RemoveDisplayScalerMode failed: " + ex.Message);
                return false;
            }
        }

        private static bool RemoveLegacyDisplayScalerModeFallback(MonitorInfo monitor, CustomResolution res)
        {
            string deviceIdFragment = SanitizeRegistryName(monitor == null ? null : monitor.DeviceId);
            if (string.IsNullOrWhiteSpace(deviceIdFragment) || deviceIdFragment == "Display")
                return false;

            using (var key = Registry.CurrentUser.OpenSubKey(DisplayScalerMarkerRegistryPath, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (key == null)
                    return false;

                string directLegacyKey = BuildMonitorMarkerKey(monitor);
                bool changed = false;
                string[] valueNames = key.GetValueNames();
                for (int i = 0; i < valueNames.Length; i++)
                {
                    string valueName = valueNames[i];
                    if (string.Equals(valueName, directLegacyKey, StringComparison.OrdinalIgnoreCase) ||
                        valueName.IndexOf(deviceIdFragment, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    string existing = key.GetValue(valueName) as string ?? string.Empty;
                    string next = RemoveModeFromModeListString(existing, res);
                    if (StringEqualsOrdinal(existing, next))
                        continue;

                    SetOrDeleteValue(key, valueName, next, RegistryValueKind.String);
                    changed = true;
                }

                return changed;
            }
        }

        private static bool RemoveDisplayScalerMode(string path, string device, CustomResolution res)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(path, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (key == null)
                    return false;

                string existing = key.GetValue(device) as string ?? string.Empty;
                string next = RemoveModeFromModeListString(existing, res);
                if (StringEqualsOrdinal(existing, next))
                    return false;

                SetOrDeleteValue(key, device, next, RegistryValueKind.String);
                return true;
            }
        }

        private static string RemoveModeFromModeListString(string existing, CustomResolution res)
        {
            if (string.IsNullOrWhiteSpace(existing))
                return string.Empty;

            var sb = new StringBuilder(existing.Length);
            int start = 0;
            while (start < existing.Length)
            {
                int end = existing.IndexOf('@', start);
                if (end < 0)
                    end = existing.Length;

                if (TryGetTrimmedSegment(existing, start, end - start, out var trimmedStart, out var trimmedLength) &&
                    !ModeStringMatches(existing, trimmedStart, trimmedLength, res))
                {
                    if (sb.Length > 0)
                        sb.Append('@');

                    sb.Append(existing, trimmedStart, trimmedLength);
                }

                start = end + 1;
            }

            return sb.ToString();
        }

        private static bool ContainsModeString(string existing, CustomResolution res)
        {
            if (string.IsNullOrWhiteSpace(existing) || res == null)
                return false;

            uint wantedBpp = res.BitsPerPixel == 0 ? DefaultBitsPerPixel : res.BitsPerPixel;
            uint wantedExactMilliHz = DISPLAY_SCALER.Domain.RefreshRateMath.NormalizeDisplayMilliHz(res.RefreshRate, res.RefreshRateMilliHz);

            int start = 0;
            while (start < existing.Length)
            {
                int end = existing.IndexOf('@', start);
                if (end < 0)
                    end = existing.Length;

                if (TryGetTrimmedSegment(existing, start, end - start, out var trimmedStart, out var trimmedLength) &&
                    TryParseModeParts(existing, trimmedStart, trimmedLength, out var width, out var height, out var bits, out var refresh, out var exactMilliHz) &&
                    width == res.Width &&
                    height == res.Height &&
                    bits == wantedBpp &&
                    refresh == res.RefreshRate &&
                    exactMilliHz != 0 &&
                    Math.Abs((long)exactMilliHz - (long)wantedExactMilliHz) <= 1L)
                {
                    return true;
                }

                start = end + 1;
            }

            return false;
        }

        private static void AddModeListItems(string existing, HashSet<ResolutionModeKey> set)
        {
            if (string.IsNullOrWhiteSpace(existing) || set == null)
                return;

            int start = 0;
            while (start < existing.Length)
            {
                int end = existing.IndexOf('@', start);
                if (end < 0)
                    end = existing.Length;

                if (TryGetTrimmedSegment(existing, start, end - start, out var trimmedStart, out var trimmedLength) &&
                    TryParseModeParts(existing, trimmedStart, trimmedLength, out var width, out var height, out var bits, out var refresh, out var exactMilliHz))
                {
                    set.Add(new ResolutionModeKey(width, height, bits, refresh, exactMilliHz));
                }

                start = end + 1;
            }
        }

        private static bool TryGetTrimmedSegment(string text, int start, int length, out int trimmedStart, out int trimmedLength)
        {
            trimmedStart = start;
            trimmedLength = 0;
            if (string.IsNullOrEmpty(text) || length <= 0 || start < 0 || start >= text.Length)
                return false;

            int end = Math.Min(text.Length, start + length) - 1;
            while (trimmedStart <= end && char.IsWhiteSpace(text[trimmedStart]))
                trimmedStart++;
            while (end >= trimmedStart && char.IsWhiteSpace(text[end]))
                end--;

            trimmedLength = end - trimmedStart + 1;
            return trimmedLength > 0;
        }

        private static bool ModeStringMatches(string item, CustomResolution res)
        {
            if (string.IsNullOrWhiteSpace(item) || res == null)
                return false;

            return ModeStringMatches(item, 0, item.Length, res);
        }

        private static bool ModeStringMatches(string item, int start, int length, CustomResolution res)
        {
            if (string.IsNullOrEmpty(item) || res == null)
                return false;
            if (!TryParseModeParts(item, start, length, out var width, out var height, out var bits, out var refresh, out var exactMilliHz))
                return false;

            uint wantedBpp = res.BitsPerPixel == 0 ? DefaultBitsPerPixel : res.BitsPerPixel;
            if (width != res.Width || height != res.Height || bits != wantedBpp || refresh != res.RefreshRate)
                return false;

            if (exactMilliHz == 0)
                return true;

            uint wantedExactMilliHz = DISPLAY_SCALER.Domain.RefreshRateMath.NormalizeDisplayMilliHz(res.RefreshRate, res.RefreshRateMilliHz);
            return Math.Abs((long)exactMilliHz - (long)wantedExactMilliHz) <= 1L;
        }

        private static bool TryParseModeParts(string item, out uint width, out uint height, out uint bits, out uint refresh)
        {
            return TryParseModeParts(item, 0, item == null ? 0 : item.Length, out width, out height, out bits, out refresh, out _);
        }

        private static bool TryParseModeParts(string item, int start, int length, out uint width, out uint height, out uint bits, out uint refresh)
        {
            return TryParseModeParts(item, start, length, out width, out height, out bits, out refresh, out _);
        }

        private static bool TryParseModeParts(string item, int start, int length, out uint width, out uint height, out uint bits, out uint refresh, out uint exactMilliHz)
        {
            width = 0;
            height = 0;
            bits = DefaultBitsPerPixel;
            refresh = 0;
            exactMilliHz = 0;

            if (string.IsNullOrWhiteSpace(item) || length <= 0)
                return false;

            int index = start;
            int end = Math.Min(item.Length, start + length);
            while (index < end && char.IsWhiteSpace(item[index]))
                index++;

            if (!TryParseUIntUntil(item, ref index, end, out width)) return false;
            if (index >= end || !IsModeSeparator(item[index])) return false;
            index++;

            if (!TryParseUIntUntil(item, ref index, end, out height)) return false;
            if (index >= end || !IsModeSeparator(item[index])) return false;
            index++;

            if (!TryParseUIntUntil(item, ref index, end, out var third)) return false;
            if (index < end && IsModeSeparator(item[index]))
            {
                bits = third == 0 ? DefaultBitsPerPixel : third;
                index++;
                if (!TryParseUIntUntil(item, ref index, end, out refresh)) return false;

                if (index < end && IsModeSeparator(item[index]))
                {
                    index++;
                    if (!TryParseUIntUntil(item, ref index, end, out exactMilliHz)) return false;
                }
            }
            else
            {
                refresh = third;
            }

            while (index < end && char.IsWhiteSpace(item[index]))
                index++;

            if (index < end && item[index] == '@')
                index++;

            while (index < end && char.IsWhiteSpace(item[index]))
                index++;

            return width > 0 && height > 0 && refresh > 0 && index >= end;
        }

        private static bool TryParseUIntUntil(string text, ref int index, int end, out uint value)
        {
            value = 0;
            if (index >= end)
                return false;

            bool hasDigit = false;
            while (index < end)
            {
                char c = text[index];
                if (c < '0' || c > '9')
                    break;

                hasDigit = true;
                uint digit = (uint)(c - '0');
                if (value > (uint.MaxValue - digit) / 10U)
                    return false;

                value = value * 10U + digit;
                index++;
            }

            return hasDigit;
        }

        private static bool IsModeSeparator(char c)
        {
            return c == 'x' || c == 'X';
        }

        private static bool StringEqualsOrdinal(string a, string b)
        {
            return string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.Ordinal);
        }

        private static void SetOrDeleteValue(RegistryKey key, string valueName, string value, RegistryValueKind kind)
        {
            if (string.IsNullOrWhiteSpace(value))
                key.DeleteValue(valueName, false);
            else
                key.SetValue(valueName, value, kind);
        }
    }
}
