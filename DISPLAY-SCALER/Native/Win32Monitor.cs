using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;

namespace DISPLAY_SCALER.Native
{
    internal static class Win32Monitor
    {
        private static string PnpVendorName(string pnpId)
        {
            if (string.IsNullOrEmpty(pnpId) || pnpId.Length < 3) return pnpId;
            string id = pnpId.Substring(0, 3).ToUpperInvariant();
            switch (id)
            {
                case "ACI": return "Asus";
                case "ACR": return "Acer";
                case "AOC": return "AOC";
                case "APP": return "Apple";
                case "AUO": return "AU Optronics";
                case "BNQ": return "BenQ";
                case "CMO": return "Chimei";
                case "CPL": return "Compal";
                case "CPQ": return "Compaq";
                case "DEC": return "Dec";
                case "DEL": return "Dell";
                case "DOF": return "DOF";
                case "GSM": return "LG";
                case "HEI": return "Hyundai";
                case "HIQ": return "Hyundai";
                case "HSD": return "Hannspree";
                case "HSL": return "Hansol";
                case "HTC": return "Hitachi";
                case "HWP": return "HP";
                case "IBM": return "IBM";
                case "ICL": return "Fujitsu";
                case "IFS": return "InFocus";
                case "IQT": return "Hyundai";
                case "IVM": return "Iiyama";
                case "KFC": return "KFC Computek";
                case "LEN": return "Lenovo";
                case "LGD": return "LG Display";
                case "LKM": return "ADLAS / AZALEA";
                case "LNK": return "LINK Technologies";
                case "LPL": return "LG Philips";
                case "LTN": return "Lite-On";
                case "MAG": return "MAG InnoVision";
                case "MAX": return "Belinea";
                case "MEI": return "Panasonic";
                case "MEL": return "Mitsubishi";
                case "MIR": return "miro Computer Products";
                case "MSI": return "MSI";
                case "MS_": return "Panasonic";
                case "NAN": return "Nanao";
                case "NEC": return "NEC";
                case "NOK": return "Nokia Data";
                case "NVD": return "Nvidia";
                case "OPT": return "Optoma";
                case "OQI": return "OPTIQUEST";
                case "PBN": return "Packard Bell";
                case "PCK": return "Daewoo";
                case "PDC": return "Polaroid";
                case "PGS": return "Princeton Graphic Systems";
                case "PHL": return "Philips";
                case "PIO": return "Pioneer";
                case "PNR": return "Planar";
                case "SAM": return "Samsung";
                case "SAN": return "Sanyo Electric";
                case "SBI": return "Smarttech";
                case "SEC": return "Seiko Epson";
                case "SGI": return "SGI";
                case "SMC": return "Standard Microsystems";
                case "SNI": return "Siemens Nixdorf";
                case "SNY": return "Sony";
                case "SPT": return "Sceptre";
                case "SRC": return "Shamrock";
                case "STN": return "Samsung Electronics America";
                case "STP": return "Sceptre";
                case "SUN": return "Sun Microsystems";
                case "TAT": return "Tatung";
                case "TOS": return "Toshiba";
                case "TRL": return "Royal Information";
                case "TSB": return "Toshiba";
                case "UNK": return "Unknown";
                case "UNM": return "Unisys";
                case "VES": return "VESA";
                case "VIZ": return "Vizio";
                case "VSC": return "ViewSonic";
                case "WAC": return "Wacom";
                case "WDE": return "Westinghouse";
                case "YMH": return "Yamaha";
                case "ZCM": return "Zenith";
                default: return id;
            }
        }

        public static EdidInfo ParseEdid(byte[] edid)
        {
            var info = new EdidInfo();
            if (edid == null || edid.Length < 128) return info;

            if (!(edid[0] == 0x00 && edid[1] == 0xFF && edid[2] == 0xFF && edid[3] == 0xFF &&
                  edid[4] == 0xFF && edid[5] == 0xFF && edid[6] == 0xFF && edid[7] == 0x00))
            {
                return info;
            }

            int checksum = 0;
            for (int i = 0; i < 128; i++)
                checksum = (checksum + edid[i]) & 0xFF;
            if (checksum != 0)
                return info;

            ushort mfg = (ushort)((edid[8] << 8) | edid[9]);
            char c1 = (char)('@' + ((mfg >> 10) & 0x1F));
            char c2 = (char)('@' + ((mfg >> 5) & 0x1F));
            char c3 = (char)('@' + (mfg & 0x1F));
            string pnp = new string(new[] { c1, c2, c3 });
            info.PnpVendorCode = pnp;
            info.Manufacturer = PnpVendorName(pnp);

            info.ProductCode = (ushort)((edid[11] << 8) | edid[10]);

            int week = edid[16] == 0xFF ? 0 : edid[16];
            int year = edid[17] + 1990;
            info.Week = week;
            info.Year = year;

            for (int i = 0; i < 4; i++)
            {
                int off = 54 + i * 18;
                if (off + 18 > edid.Length) break;

                if (edid[off] == 0x00 && edid[off + 1] == 0x00)
                {
                    byte tag = edid[off + 2];

                    switch (tag)
                    {
                        case 0xFC:
                            info.ModelName = CleanAscii(edid, off + 5, 13);
                            break;

                        case 0xFF:
                            info.SerialNumber = CleanAscii(edid, off + 5, 13);
                            break;

                        case 0xFE:
                            info.AsciiData = CleanAscii(edid, off + 5, 13);
                            break;

                        case 0xFD:

                            if (edid[off + 5] != 0) info.MinVerticalRate = edid[off + 5];
                            if (edid[off + 6] != 0) info.MaxVerticalRate = edid[off + 6];
                            if (edid[off + 7] != 0) info.MinHorizontalRate = edid[off + 7];
                            if (edid[off + 8] != 0) info.MaxHorizontalRate = edid[off + 8];
                            if (edid[off + 9] != 0) info.MaxPixelClockMhz = (uint)(edid[off + 9] * 10);
                            break;
                    }
                }
                else if (info.NativeWidth == 0 || info.NativeHeight == 0)
                {
                    ParseDetailedTiming(edid, off, info);
                }
            }

            byte physicalWidthCm = edid[21];
            byte physicalHeightCm = edid[22];
            if (physicalWidthCm != 0 && physicalHeightCm != 0)
            {
                info.PhysicalWidthCm = physicalWidthCm;
                info.PhysicalHeightCm = physicalHeightCm;
                double w = physicalWidthCm;
                double h = physicalHeightCm;
                info.DiagonalInch = Math.Round(Math.Sqrt(w * w + h * h) / 2.54, 1);
            }

            info.IsValid = true;
            return info;
        }

        private static void ParseDetailedTiming(byte[] edid, int off, EdidInfo info)
        {
            uint pixelClock = (uint)(((edid[off + 1] << 8) | edid[off]) * 10000);
            if (pixelClock == 0) return;

            uint hActive = (uint)(edid[off + 2] | ((edid[off + 4] & 0xF0) << 4));
            uint hBlank = (uint)(edid[off + 3] | ((edid[off + 4] & 0x0F) << 8));
            uint vActive = (uint)(edid[off + 5] | ((edid[off + 7] & 0xF0) << 4));
            uint vBlank = (uint)(edid[off + 6] | ((edid[off + 7] & 0x0F) << 8));

            if (hActive > 0 && vActive > 0)
            {
                info.NativeWidth = hActive;
                info.NativeHeight = vActive;
            }

            uint hTotal = hActive + hBlank;
            uint vTotal = vActive + vBlank;
            if (hTotal > 0 && vTotal > 0)
            {
                ulong denominator = (ulong)hTotal * vTotal;
                if (denominator > 0)
                {
                    ulong numerator = pixelClock;
                    ulong gcd = Gcd(numerator, denominator);
                    numerator /= gcd;
                    denominator /= gcd;

                    if (numerator <= uint.MaxValue && denominator <= uint.MaxValue)
                    {
                        info.NativeRefreshRateNumerator = (uint)numerator;
                        info.NativeRefreshRateDenominator = (uint)denominator;
                        info.NativeRefreshRateMilliHz = (uint)Math.Round(((double)numerator / denominator) * 1000.0, MidpointRounding.AwayFromZero);
                        info.NativeRefreshRateHz = (uint)Math.Round((double)numerator / denominator, MidpointRounding.AwayFromZero);
                    }
                }
            }
        }

        private static ulong Gcd(ulong a, ulong b)
        {
            while (b != 0)
            {
                ulong t = a % b;
                a = b;
                b = t;
            }
            return a == 0 ? 1UL : a;
        }

        private static string CleanAscii(byte[] data, int offset, int length)
        {
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                byte b = data[offset + i];
                if (b == 0x0A) break;
                if (b == 0) break;
                if (b >= 0x20 && b < 0x7F) sb.Append((char)b);
            }
            return sb.ToString().Trim().TrimEnd('\n');
        }

        public static byte[] ReadEdidFromRegistry(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return null;

            string regPath = BuildDeviceParametersRegistryPath(deviceId);
            if (regPath == null) return null;
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(regPath))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("EDID");
                        if (val is byte[] bytes) return bytes;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ReadEdidFromRegistry failed: " + ex.Message);
            }
            return null;
        }

        public static string ReadNonStandardModes(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return null;
            string regPath = BuildDeviceParametersRegistryPath(deviceId);
            if (regPath == null) return null;
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(regPath))
                {
                    if (key == null) return null;

                    var sb = new StringBuilder();
                    foreach (string valueName in key.GetValueNames())
                    {
                        if (!valueName.StartsWith("DALNonStandardModesBCD", StringComparison.OrdinalIgnoreCase))
                            continue;

                        object val = key.GetValue(valueName);
                        string decoded = null;
                        if (val is string s) decoded = s;
                        else if (val is byte[] b) decoded = DecodeNonStandardModesBcd(b);

                        if (!string.IsNullOrWhiteSpace(decoded))
                        {
                            if (sb.Length > 0 && sb[sb.Length - 1] != '@')
                                sb.Append('@');
                            sb.Append(decoded.TrimEnd('@')).Append('@');
                        }
                    }

                    return sb.Length == 0 ? null : sb.ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ReadNonStandardModes failed: " + ex.Message);
            }
            return null;
        }

        public static bool RemoveNonStandardMode(string deviceId, uint width, uint height, uint refresh, out string detail)
        {
            detail = null;
            if (string.IsNullOrEmpty(deviceId) || width == 0 || height == 0 || refresh == 0)
                return false;

            string regPath = BuildDeviceParametersRegistryPath(deviceId);
            if (regPath == null)
                return false;

            int changedValues = 0;
            int removedModes = 0;
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(regPath, RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (key == null)
                        return false;

                    foreach (string valueName in key.GetValueNames())
                    {
                        if (!valueName.StartsWith("DALNonStandardModesBCD", StringComparison.OrdinalIgnoreCase))
                            continue;

                        object value = key.GetValue(valueName);
                        RegistryValueKind kind = key.GetValueKind(valueName);

                        if (value is string text)
                        {
                            string next = RemoveModeFromLegacyList(text, width, height, refresh, out int removed);
                            if (removed > 0)
                            {
                                removedModes += removed;
                                changedValues++;
                                SetOrDeleteRegistryValue(key, valueName, next, kind == RegistryValueKind.Unknown ? RegistryValueKind.String : kind);
                            }
                        }
                        else if (value is byte[] data)
                        {
                            string ascii = Encoding.ASCII.GetString(data).TrimEnd('\0');
                            if (LooksLikeLegacyModeList(ascii))
                            {
                                string next = RemoveModeFromLegacyList(ascii, width, height, refresh, out int removed);
                                if (removed > 0)
                                {
                                    removedModes += removed;
                                    changedValues++;
                                    byte[] encoded = string.IsNullOrWhiteSpace(next) ? new byte[0] : Encoding.ASCII.GetBytes(next.TrimEnd('@') + "@\0");
                                    SetOrDeleteRegistryValue(key, valueName, encoded, kind == RegistryValueKind.Unknown ? RegistryValueKind.Binary : kind);
                                }
                            }
                            else
                            {
                                byte[] next = RemoveModeFromBcdList(data, width, height, refresh, out int removed);
                                if (removed > 0)
                                {
                                    removedModes += removed;
                                    changedValues++;
                                    SetOrDeleteRegistryValue(key, valueName, next, kind == RegistryValueKind.Unknown ? RegistryValueKind.Binary : kind);
                                }
                            }
                        }
                    }
                }

                if (removedModes > 0)
                {
                    detail = "Removed " + removedModes + " DALNonStandardModesBCD entr" + (removedModes == 1 ? "y" : "ies") + " from " + changedValues + " registry value" + (changedValues == 1 ? "" : "s") + ".";
                    return true;
                }
            }
            catch (Exception ex)
            {
                detail = "RemoveNonStandardMode failed: " + ex.Message;
                System.Diagnostics.Debug.WriteLine(detail);
                return false;
            }

            detail = "Matching DALNonStandardModesBCD entry was not found.";
            return false;
        }

        private static void SetOrDeleteRegistryValue(RegistryKey key, string valueName, object value, RegistryValueKind kind)
        {
            if (key == null || string.IsNullOrEmpty(valueName))
                return;

            if (value == null)
            {
                key.DeleteValue(valueName, false);
                return;
            }

            if (value is string text)
            {
                if (string.IsNullOrWhiteSpace(text))
                    key.DeleteValue(valueName, false);
                else
                    key.SetValue(valueName, text, kind == RegistryValueKind.Unknown ? RegistryValueKind.String : kind);
                return;
            }

            if (value is byte[] bytes)
            {
                if (bytes.Length == 0)
                    key.DeleteValue(valueName, false);
                else
                    key.SetValue(valueName, bytes, RegistryValueKind.Binary);
            }
        }

        private static string RemoveModeFromLegacyList(string existing, uint width, uint height, uint refresh, out int removed)
        {
            removed = 0;
            if (string.IsNullOrWhiteSpace(existing))
                return string.Empty;

            var sb = new StringBuilder(existing.Length);
            int start = 0;
            while (start < existing.Length)
            {
                int end = existing.IndexOf('@', start);
                if (end < 0)
                    end = existing.Length;

                string item = existing.Substring(start, end - start).Trim();
                if (!string.IsNullOrWhiteSpace(item))
                {
                    if (LegacyModeMatches(item, width, height, refresh))
                    {
                        removed++;
                    }
                    else
                    {
                        if (sb.Length > 0) sb.Append('@');
                        sb.Append(item);
                    }
                }

                start = end + 1;
            }

            return sb.Length == 0 ? string.Empty : sb.ToString() + "@";
        }

        private static bool LegacyModeMatches(string item, uint width, uint height, uint refresh)
        {
            if (string.IsNullOrWhiteSpace(item))
                return false;

            string[] parts = item.Split(new[] { 'x', 'X' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3 && parts.Length != 4)
                return false;

            if (!uint.TryParse(parts[0], out uint w) || !uint.TryParse(parts[1], out uint h))
                return false;

            uint r;
            if (parts.Length == 4)
            {
                if (!uint.TryParse(parts[3], out r))
                    return false;
            }
            else if (!uint.TryParse(parts[2], out r))
            {
                return false;
            }

            return w == width && h == height && Math.Abs((long)r - (long)refresh) <= 1;
        }

        private static byte[] RemoveModeFromBcdList(byte[] data, uint width, uint height, uint refresh, out int removed)
        {
            removed = 0;
            if (data == null || data.Length == 0)
                return new byte[0];

            var result = new List<byte>(data.Length);
            for (int i = 0; i + 7 < data.Length; i += 8)
            {
                if (IsZeroBcdChunk(data, i))
                    continue;

                uint w = 0;
                uint h = 0;
                uint r = 0;

                bool decodedWidth = TryDecodeBcdPair(data[i], data[i + 1], out w);
                bool decodedHeight = TryDecodeBcdPair(data[i + 2], data[i + 3], out h);
                bool decodedRefresh = TryDecodeBcdPair(data[i + 6], data[i + 7], out r);
                bool decoded = decodedWidth && decodedHeight && decodedRefresh;

                if (decoded && w == width && h == height && Math.Abs((long)r - (long)refresh) <= 1)
                {
                    removed++;
                    continue;
                }

                for (int j = 0; j < 8; j++)
                    result.Add(data[i + j]);
            }

            return result.ToArray();
        }

        private static bool IsZeroBcdChunk(byte[] data, int offset)
        {
            if (data == null || offset < 0 || offset + 7 >= data.Length)
                return true;
            for (int i = 0; i < 8; i++)
            {
                if (data[offset + i] != 0)
                    return false;
            }
            return true;
        }

        private static string DecodeNonStandardModesBcd(byte[] data)
        {
            if (data == null || data.Length == 0) return null;

            string ascii = Encoding.ASCII.GetString(data).TrimEnd('\0');
            if (LooksLikeLegacyModeList(ascii))
                return ascii;

            var sb = new StringBuilder();
            for (int i = 0; i + 7 < data.Length; i += 8)
            {
                if (!TryDecodeBcdPair(data[i], data[i + 1], out uint width) ||
                    !TryDecodeBcdPair(data[i + 2], data[i + 3], out uint height) ||
                    !TryDecodeBcdPair(data[i + 6], data[i + 7], out uint refresh))
                {
                    continue;
                }

                if (width >= 320 && width <= 7680 &&
                    height >= 240 && height <= 4320 &&
                    refresh >= 24 && refresh <= 1000)
                {
                    sb.Append(width).Append('x').Append(height).Append("x32x").Append(refresh).Append('@');
                }
            }

            return sb.Length == 0 ? null : sb.ToString();
        }

        private static bool LooksLikeLegacyModeList(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return value.IndexOf('x') >= 0 && value.IndexOf('@') >= 0;
        }

        private static bool TryDecodeBcdPair(byte high, byte low, out uint result)
        {
            result = 0;
            if (!TryDecodeBcdByte(high, out uint h) || !TryDecodeBcdByte(low, out uint l))
                return false;

            result = h * 100 + l;
            return true;
        }

        private static bool TryDecodeBcdByte(byte value, out uint result)
        {
            uint hi = (uint)((value >> 4) & 0x0F);
            uint lo = (uint)(value & 0x0F);
            if (hi > 9 || lo > 9)
            {
                result = 0;
                return false;
            }

            result = hi * 10 + lo;
            return true;
        }

        public static MonitorWmiInfo ReadMonitorIdFromWmi(string deviceId)
        {
            string normalizedDeviceId = NormalizeMonitorInstanceName(deviceId);
            string pnpId = ExtractPnpId(deviceId);
            MonitorWmiInfo pnpFallback = null;

            using (var searcher = new ManagementObjectSearcher(
                @"root\wmi",
                "SELECT Active, InstanceName, ManufacturerName, UserFriendlyName, SerialNumberID FROM WmiMonitorID WHERE Active=True"))
            {
                foreach (ManagementObject obj in searcher.Get().Cast<ManagementObject>())
                {
                    string instanceName = Convert.ToString(obj["InstanceName"]);
                    string normalizedInstanceName = NormalizeMonitorInstanceName(instanceName);
                    var info = new MonitorWmiInfo
                    {
                        InstanceName = instanceName,
                        FriendlyName = DecodeWmiString(obj["UserFriendlyName"]),
                        Manufacturer = DecodeWmiString(obj["ManufacturerName"]),
                        SerialNumber = DecodeWmiString(obj["SerialNumberID"])
                    };

                    if (!string.IsNullOrEmpty(normalizedDeviceId) &&
                        !string.IsNullOrEmpty(normalizedInstanceName) &&
                        normalizedInstanceName.StartsWith(normalizedDeviceId, StringComparison.OrdinalIgnoreCase))
                    {
                        return info;
                    }

                    if (pnpFallback == null &&
                        !string.IsNullOrEmpty(pnpId) &&
                        !string.IsNullOrEmpty(instanceName) &&
                        instanceName.IndexOf("\\" + pnpId + "\\", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        pnpFallback = info;
                    }
                }
            }

            return pnpFallback;
        }

        private static string ExtractPnpId(string deviceId)
        {
            return ExtractDeviceIdSegment(deviceId, 1);
        }

        private static string BuildDeviceParametersRegistryPath(string deviceId)
        {
            string bus = ExtractDeviceIdSegment(deviceId, 0);
            string pnp = ExtractDeviceIdSegment(deviceId, 1);
            string instance = ExtractDeviceIdSegment(deviceId, 2);
            if (string.IsNullOrEmpty(bus) || string.IsNullOrEmpty(pnp) || string.IsNullOrEmpty(instance))
                return null;

            return @"SYSTEM\CurrentControlSet\Enum\" + bus + @"\" + pnp + @"\" + instance + @"\Device Parameters";
        }

        private static string ExtractDeviceIdSegment(string deviceId, int segmentIndex)
        {
            if (string.IsNullOrWhiteSpace(deviceId) || segmentIndex < 0)
                return null;

            int start = 0;
            int currentSegment = 0;
            for (int i = 0; i <= deviceId.Length; i++)
            {
                if (i == deviceId.Length || deviceId[i] == '\\')
                {
                    if (currentSegment == segmentIndex)
                    {
                        int length = i - start;
                        return length > 0 ? deviceId.Substring(start, length) : null;
                    }

                    currentSegment++;
                    start = i + 1;
                }
            }

            return null;
        }

        private static string NormalizeMonitorInstanceName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string text = value.Trim();
            if (text.IndexOf('/') >= 0)
                text = text.Replace('/', '\\');
            int suffix = text.IndexOf('_');
            if (suffix > 0) text = text.Substring(0, suffix);
            return text.ToUpperInvariant();
        }

        private static string DecodeWmiString(object value)
        {
            if (!(value is ushort[] chars) || chars.Length == 0) return null;

            var sb = new StringBuilder(chars.Length);
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i] == 0) break;
                sb.Append((char)chars[i]);
            }

            string text = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
    }

    internal sealed class MonitorWmiInfo
    {
        public string InstanceName;
        public string FriendlyName;
        public string Manufacturer;
        public string SerialNumber;
    }

    internal sealed class EdidInfo
    {
        public bool IsValid;
        public string PnpVendorCode;
        public string Manufacturer;
        public ushort ProductCode;
        public string ModelName;
        public string SerialNumber;
        public string AsciiData;
        public int Week;
        public int Year;
        public uint NativeWidth;
        public uint NativeHeight;
        public uint NativeRefreshRateHz;
        public uint NativeRefreshRateMilliHz;
        public uint NativeRefreshRateNumerator;
        public uint NativeRefreshRateDenominator;
        public uint MinVerticalRate;
        public uint MaxVerticalRate;
        public uint MinHorizontalRate;
        public uint MaxHorizontalRate;
        public uint MaxPixelClockMhz;
        public byte PhysicalWidthCm;
        public byte PhysicalHeightCm;
        public double DiagonalInch;
    }
}