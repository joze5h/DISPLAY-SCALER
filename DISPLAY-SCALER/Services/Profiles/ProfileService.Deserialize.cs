using DISPLAY_SCALER.Domain;
using DISPLAY_SCALER.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace DISPLAY_SCALER.Services
{
    public sealed partial class ResolutionProfileService
    {
        private const long MaxPortableProfileUtf8Bytes = (long)ProfileJsonMaxLength * 4L;

        public ProfileImportPlan LoadAndValidate(string path, MonitorInfo targetMonitor)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Путь импорта не задан.", nameof(path));
            DisplayScalerProfile profile;
            if (CruEdidBinProfileCodec.LooksLikeEdidBin(path))
            {
                string note;
                profile = CruEdidBinProfileCodec.Import(path, out note);
            }
            else
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.Length > MaxPortableProfileUtf8Bytes)
                    throw new InvalidDataException("Portable profile is too large.");

                string json = File.ReadAllText(path, Encoding.UTF8);
                if (json.Length > ProfileJsonMaxLength)
                    throw new InvalidDataException("Portable profile JSON exceeds the supported size limit.");

                var serializer = new JavaScriptSerializer { MaxJsonLength = ProfileJsonMaxLength };
                profile = DeserializePortableProfile(json, serializer);
            }

            var plan = ValidateProfile(profile, targetMonitor);
            plan.FilePath = path;
            return plan;
        }

        private static DisplayScalerProfile DeserializePortableProfile(string json, JavaScriptSerializer serializer)
        {
            DisplayScalerProfile profile = null;
            try
            {
                profile = serializer.Deserialize<DisplayScalerProfile>(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Deserialize portable profile failed: " + ex.Message);
                profile = null;
            }

            Dictionary<string, object> root = null;
            try
            {
                root = serializer.DeserializeObject(json) as Dictionary<string, object>;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Deserialize portable dictionary failed: " + ex.Message);
                root = null;
            }

            if (profile == null)
                profile = BuildMinimalProfileFromDictionary(root);

            if (profile != null && profile.Resolution != null)
            {
                uint refresh = NormalizeNominalRefreshRate(profile.Resolution);
                uint exactMilliHz = NormalizeProfileExactMilliHz(profile.Resolution, refresh);
                if (root != null && root.TryGetValue("Resolution", out object rawResolutionObject))
                {
                    var rawResolution = rawResolutionObject as Dictionary<string, object>;
                    uint rawRefresh = ReadPortableRefreshRate(rawResolution, refresh);
                    if (rawRefresh > 0)
                        refresh = rawRefresh;

                    exactMilliHz = ReadPortableExactRefreshMilliHz(rawResolution, exactMilliHz);
                    profile.Resolution.SourceNativeRefreshRateMilliHz = ReadRoundedUInt(rawResolution, "SourceNativeRefreshRateMilliHz", profile.Resolution.SourceNativeRefreshRateMilliHz);
                    profile.Resolution.SourceNativeRefreshRateNumerator = ReadRoundedUInt(rawResolution, "SourceNativeRefreshRateNumerator", profile.Resolution.SourceNativeRefreshRateNumerator);
                    profile.Resolution.SourceNativeRefreshRateDenominator = ReadRoundedUInt(rawResolution, "SourceNativeRefreshRateDenominator", profile.Resolution.SourceNativeRefreshRateDenominator);
                    profile.Resolution.ExactRefreshRatePolicy = ReadString(rawResolution, "ExactRefreshRatePolicy", profile.Resolution.ExactRefreshRatePolicy);
                    profile.Resolution.PreserveLegitimateFractionalRefreshRates = ReadBool(rawResolution, "PreserveLegitimateFractionalRefreshRates", profile.Resolution.PreserveLegitimateFractionalRefreshRates);
                }

                profile.Resolution.RefreshRate = refresh;
                profile.Resolution.RefreshRateHz = refresh;
                profile.Resolution.NominalRefreshRateHz = refresh;
                profile.Resolution.ExactRefreshRateMilliHz = NormalizeProfileExactMilliHz(profile.Resolution, refresh, exactMilliHz);
                profile.Resolution.IntegerRefreshRate = profile.Resolution.ExactRefreshRateMilliHz == NormalizeMilliHz(refresh, 0);
                profile.Resolution.PreserveLegitimateFractionalRefreshRates = true;
                if (string.IsNullOrWhiteSpace(profile.Resolution.ExactRefreshRatePolicy))
                    profile.Resolution.ExactRefreshRatePolicy = "PreferTargetNativeFractionalWhenNominalMatches";
                if (profile.Resolution.BitsPerPixel == 0)
                    profile.Resolution.BitsPerPixel = 32;
                if (string.IsNullOrWhiteSpace(profile.Resolution.TimingMode))
                    profile.Resolution.TimingMode = "NVIDIA_AUTO_TARGET_EQUIVALENT_REFRESH";
            }

            return profile;
        }

        private static DisplayScalerProfile BuildMinimalProfileFromDictionary(Dictionary<string, object> root)
        {
            if (root == null)
                return null;

            var profile = new DisplayScalerProfile();
            profile.SchemaVersion = ReadInt(root, "SchemaVersion", CurrentSchemaVersion);
            profile.Application = ReadString(root, "Application", AppName);
            profile.ExportKind = ReadString(root, "ExportKind", "PortableResolution");
            profile.PortableAcrossDisplays = ReadBool(root, "PortableAcrossDisplays", true);
            profile.ProfileName = ReadString(root, "ProfileName", string.Empty);
            profile.ExportedUtc = ReadString(root, "ExportedUtc", string.Empty);
            profile.ExportedByAppVersion = ReadString(root, "ExportedByAppVersion", string.Empty);

            Dictionary<string, object> resolution = null;
            if (root.TryGetValue("Resolution", out object rawResolutionObject))
                resolution = rawResolutionObject as Dictionary<string, object>;

            uint refreshRate = ReadPortableRefreshRate(resolution, 0);
            uint exactRefreshMilliHz = ReadPortableExactRefreshMilliHz(resolution, 0);

            profile.Resolution = new DisplayProfileResolution
            {
                Width = ReadUInt(resolution, "Width", 0),
                Height = ReadUInt(resolution, "Height", 0),
                RefreshRate = refreshRate,
                RefreshRateHz = refreshRate,
                NominalRefreshRateHz = refreshRate,
                ExactRefreshRateMilliHz = exactRefreshMilliHz,
                SourceNativeRefreshRateMilliHz = ReadRoundedUInt(resolution, "SourceNativeRefreshRateMilliHz", 0),
                SourceNativeRefreshRateNumerator = ReadRoundedUInt(resolution, "SourceNativeRefreshRateNumerator", 0),
                SourceNativeRefreshRateDenominator = ReadRoundedUInt(resolution, "SourceNativeRefreshRateDenominator", 0),
                ExactRefreshRatePolicy = ReadString(resolution, "ExactRefreshRatePolicy", "PreferTargetNativeFractionalWhenNominalMatches"),
                IntegerRefreshRate = false,
                PreserveLegitimateFractionalRefreshRates = true,
                BitsPerPixel = ReadUInt(resolution, "BitsPerPixel", 32),
                TimingMode = ReadString(resolution, "TimingMode", "NVIDIA_AUTO_TARGET_EQUIVALENT_REFRESH")
            };
            profile.Resolution.IntegerRefreshRate = profile.Resolution.ExactRefreshRateMilliHz == NormalizeMilliHz(profile.Resolution.NominalRefreshRateHz, 0);

            return profile;
        }

        private static uint NormalizeIntegerRefreshRate(DisplayProfileResolution resolution)
        {
            return NormalizeNominalRefreshRate(resolution);
        }

        private static uint NormalizeNominalRefreshRate(DisplayProfileResolution resolution)
        {
            if (resolution == null)
                return 0;
            if (resolution.NominalRefreshRateHz > 0)
                return resolution.NominalRefreshRateHz;
            if (resolution.RefreshRateHz > 0)
                return resolution.RefreshRateHz;
            return resolution.RefreshRate;
        }

        private static uint NormalizeProfileExactMilliHz(DisplayProfileResolution resolution, uint nominalRefresh)
        {
            if (resolution == null)
                return NormalizeMilliHz(nominalRefresh, 0);
            return NormalizeProfileExactMilliHz(resolution, nominalRefresh, resolution.ExactRefreshRateMilliHz);
        }

        private static uint NormalizeMilliHz(uint nominalRefresh, uint exactMilliHz)
        {
            return RefreshRateMath.NormalizeDisplayMilliHz(nominalRefresh, exactMilliHz);
        }

        private static uint ReadPortableRefreshRate(Dictionary<string, object> resolution, uint fallback)
        {
            uint hz = ReadRoundedUInt(resolution, "NominalRefreshRateHz", 0);
            if (hz > 0) return hz;
            hz = ReadRoundedUInt(resolution, "RefreshRateHz", 0);
            if (hz > 0) return hz;
            hz = ReadRoundedUInt(resolution, "RefreshRate", 0);
            if (hz > 0) return hz;
            return fallback;
        }

        private static uint ReadPortableExactRefreshMilliHz(Dictionary<string, object> resolution, uint fallback)
        {
            uint milliHz = ReadRoundedUInt(resolution, "ExactRefreshRateMilliHz", 0);
            if (milliHz > 0) return milliHz;
            milliHz = ReadRoundedUInt(resolution, "RefreshRateMilliHz", 0);
            if (milliHz > 0) return milliHz;
            milliHz = ReadRoundedUInt(resolution, "SourceNativeRefreshRateMilliHz", 0);
            if (milliHz > 0) return milliHz;
            return fallback;
        }

        private static uint ReadUInt(Dictionary<string, object> dict, string key, uint fallback)
        {
            return ReadRoundedUInt(dict, key, fallback);
        }

        private static int ReadInt(Dictionary<string, object> dict, string key, int fallback)
        {
            if (dict == null || !dict.TryGetValue(key, out object value) || value == null)
                return fallback;

            try
            {
                if (value is int) return (int)value;
                if (value is uint)
                {
                    uint integer = (uint)value;
                    return integer > int.MaxValue ? fallback : (int)integer;
                }
                if (value is long)
                {
                    long integer = (long)value;
                    return integer < int.MinValue || integer > int.MaxValue ? fallback : (int)integer;
                }
                if (value is decimal)
                    return TryRoundToInt((decimal)value, out int decimalResult) ? decimalResult : fallback;
                if (value is double)
                    return TryRoundToInt((double)value, out int doubleResult) ? doubleResult : fallback;
                if (value is float)
                    return TryRoundToInt((double)(float)value, out int floatResult) ? floatResult : fallback;

                string text = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInt))
                    return parsedInt;
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedDouble) &&
                    TryRoundToInt(parsedDouble, out int parsedRounded))
                    return parsedRounded;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ReadInt failed: " + ex.Message);
            }

            return fallback;
        }

        private static uint ReadRoundedUInt(Dictionary<string, object> dict, string key, uint fallback)
        {
            if (dict == null || !dict.TryGetValue(key, out object value) || value == null)
                return fallback;

            try
            {
                if (value is uint) return (uint)value;
                if (value is int)
                {
                    int integer = (int)value;
                    return integer < 0 ? fallback : (uint)integer;
                }
                if (value is long)
                {
                    long integer = (long)value;
                    return integer < 0 || integer > uint.MaxValue ? fallback : (uint)integer;
                }
                if (value is decimal)
                    return TryRoundToUInt((decimal)value, out uint decimalResult) ? decimalResult : fallback;
                if (value is double)
                    return TryRoundToUInt((double)value, out uint doubleResult) ? doubleResult : fallback;
                if (value is float)
                    return TryRoundToUInt((double)(float)value, out uint floatResult) ? floatResult : fallback;

                string text = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsedUInt))
                    return parsedUInt;
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedDouble) &&
                    TryRoundToUInt(parsedDouble, out uint parsedRounded))
                    return parsedRounded;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ReadUInt failed: " + ex.Message);
            }

            return fallback;
        }

        private static bool TryRoundToInt(double value, out int result)
        {
            result = 0;
            if (double.IsNaN(value) || double.IsInfinity(value))
                return false;

            double rounded = Math.Round(value, 0, MidpointRounding.AwayFromZero);
            if (rounded < int.MinValue || rounded > int.MaxValue)
                return false;

            result = (int)rounded;
            return true;
        }

        private static bool TryRoundToInt(decimal value, out int result)
        {
            result = 0;
            decimal rounded = Math.Round(value, 0, MidpointRounding.AwayFromZero);
            if (rounded < int.MinValue || rounded > int.MaxValue)
                return false;

            result = (int)rounded;
            return true;
        }

        private static bool TryRoundToUInt(double value, out uint result)
        {
            result = 0;
            if (double.IsNaN(value) || double.IsInfinity(value))
                return false;

            double rounded = Math.Round(value, 0, MidpointRounding.AwayFromZero);
            if (rounded < 0.0 || rounded > uint.MaxValue)
                return false;

            result = (uint)rounded;
            return true;
        }

        private static bool TryRoundToUInt(decimal value, out uint result)
        {
            result = 0;
            decimal rounded = Math.Round(value, 0, MidpointRounding.AwayFromZero);
            if (rounded < 0m || rounded > uint.MaxValue)
                return false;

            result = (uint)rounded;
            return true;
        }

        private static string ReadString(Dictionary<string, object> dict, string key, string fallback)
        {
            if (dict == null || !dict.TryGetValue(key, out object value) || value == null)
                return fallback;
            string text = Convert.ToString(value, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        private static bool ReadBool(Dictionary<string, object> dict, string key, bool fallback)
        {
            if (dict == null || !dict.TryGetValue(key, out object value) || value == null)
                return fallback;
            if (value is bool) return (bool)value;
            string text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (bool.TryParse(text, out bool parsed)) return parsed;
            return fallback;
        }

        private static uint NormalizeProfileExactMilliHz(DisplayProfileResolution resolution, uint nominalRefresh, uint exactMilliHz)
        {
            uint nominalMilliHz = NormalizeMilliHz(nominalRefresh, 0);
            if (nominalRefresh == 0)
                return 0;

            if (exactMilliHz == 0 && resolution != null)
                exactMilliHz = resolution.SourceNativeRefreshRateMilliHz;

            return NormalizeMilliHz(nominalRefresh, exactMilliHz == 0 ? nominalMilliHz : exactMilliHz);
        }
    }
}