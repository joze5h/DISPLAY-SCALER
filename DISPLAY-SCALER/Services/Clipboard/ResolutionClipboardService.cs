using DISPLAY_SCALER.Models;
using DISPLAY_SCALER.Services.Profiles;
using System;
using System.Text.RegularExpressions;
using System.Windows;

namespace DISPLAY_SCALER.Services.Clipboard
{
    public sealed class ResolutionClipboardService : IResolutionClipboardService
    {
        private const string ClipboardFormat = "DISPLAY-SCALER_PORTABLE_PROFILE_JSON_V1";
        private readonly IResolutionProfileService _profiles;

        public ResolutionClipboardService(IResolutionProfileService profiles)
        {
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        }

        public void CopyProfile(DisplayScalerProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            string json = _profiles.SerializeProfile(profile);
            string text = BuildHumanReadableClipboardText(profile, json);

            var data = new DataObject();
            data.SetData(ClipboardFormat, json);
            data.SetData(DataFormats.UnicodeText, text);
            data.SetData(DataFormats.Text, text);
            Clipboard.SetDataObject(data, true);
        }

        public bool TryReadProfile(out DisplayScalerProfile profile, out string error)
        {
            profile = null;
            error = null;

            try
            {
                IDataObject data = Clipboard.GetDataObject();
                string text = null;

                if (data != null && data.GetDataPresent(ClipboardFormat))
                    text = data.GetData(ClipboardFormat) as string;

                if (string.IsNullOrWhiteSpace(text) && data != null && data.GetDataPresent(DataFormats.UnicodeText))
                    text = data.GetData(DataFormats.UnicodeText) as string;

                if (string.IsNullOrWhiteSpace(text) && data != null && data.GetDataPresent(DataFormats.Text))
                    text = data.GetData(DataFormats.Text) as string;

                if (string.IsNullOrWhiteSpace(text))
                {
                    error = "Системный буфер обмена не содержит DISPLAY-SCALER профиля или текста разрешения.";
                    return false;
                }

                string json = ExtractJson(text);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    profile = _profiles.DeserializeProfileText(json);
                    if (IsUsableProfile(profile))
                        return true;
                }

                profile = TryParsePlainTextResolution(text);
                if (IsUsableProfile(profile))
                    return true;

                error = "Не удалось распознать DISPLAY-SCALER JSON или строку вида 1440x1080@240.";
                profile = null;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool IsUsableProfile(DisplayScalerProfile profile)
        {
            return profile != null && profile.Resolution != null &&
                   profile.Resolution.Width > 0 && profile.Resolution.Height > 0 &&
                   profile.Resolution.NominalRefreshRateHz > 0;
        }

        private static string ExtractJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            int first = text.IndexOf('{');
            if (first < 0)
                return null;

            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = first; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return text.Substring(first, i - first + 1);
                    if (depth < 0)
                        return null;
                }
            }

            return null;
        }

        private static DisplayScalerProfile TryParsePlainTextResolution(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            Match match = Regex.Match(text, @"(?<w>\d{3,5})\s*[x×]\s*(?<h>\d{3,5})(?:\s*(?:@|at)\s*(?<hz>\d{2,4})(?:[\.,](?<frac>\d{1,3}))?\s*(?:hz|гц)?)?", RegexOptions.IgnoreCase);
            if (!match.Success)
                return null;
            if (!uint.TryParse(match.Groups["w"].Value, out var width) ||
                !uint.TryParse(match.Groups["h"].Value, out var height))
                return null;

            if (!uint.TryParse(match.Groups["hz"].Value, out var hz) || hz == 0)
                hz = 60;

            uint exactMilliHz = hz * 1000U;
            if (match.Groups["frac"].Success)
            {
                string fracText = match.Groups["frac"].Value;
                if (fracText.Length < 3)
                    fracText = fracText.PadRight(3, '0');
                else if (fracText.Length > 3)
                    fracText = fracText.Substring(0, 3);
                if (uint.TryParse(fracText, out var frac))
                    exactMilliHz = hz * 1000U + frac;
            }

            var profile = new DisplayScalerProfile();
            profile.SchemaVersion = 10;
            profile.Application = "DISPLAY-SCALER";
            profile.ExportKind = "PortableResolutionClipboard";
            profile.PortableAcrossDisplays = true;
            profile.ProfileName = width + "x" + height + " @ " + hz + " Hz";
            profile.ExportedUtc = DateTime.UtcNow.ToString("o");
            profile.Resolution = new DisplayProfileResolution
            {
                Width = width,
                Height = height,
                RefreshRate = hz,
                RefreshRateHz = hz,
                NominalRefreshRateHz = hz,
                ExactRefreshRateMilliHz = exactMilliHz,
                BitsPerPixel = 32,
                IntegerRefreshRate = exactMilliHz == hz * 1000U,
                PreserveLegitimateFractionalRefreshRates = true,
                ExactRefreshRatePolicy = "KeepClipboardRequestedRefresh",
                TimingMode = "NVIDIA_AUTO_TARGET_EXACT_CLIPBOARD_TRANSFER"
            };
            profile.CreationHints = new DisplayProfileCreationHints
            {
                TargetApi = "NVIDIA_NVAPI_CUSTOM_DISPLAY",
                TimingMode = "NVIDIA_AUTO_TARGET_EXACT_CLIPBOARD_TRANSFER",
                RecalculateTimingOnImport = true,
                SourceMonitorIsBinding = false,
                ExactRefreshRatePolicy = "KeepClipboardRequestedRefresh",
                SizeTransferPolicy = "TryExactTargetModeNoAutomaticFit",
                PreferTargetNativeRefreshRate = false,
                PreserveLegitimateFractionalRefreshRates = true,
                FitToTargetNativeWhenOversized = false,
                PreserveExistingTargetNativeModes = true,
                Notes = "Plain-text clipboard import. Requested width/height/refresh must not be silently adapted by target native/max refresh or same-size modes at different refresh rates."
            };
            profile.Diagnostics = new DisplayProfileDiagnostics();
            profile.Diagnostics.Notes.Add("Профиль создан из plain-text clipboard строки.");
            return profile;
        }

        private static string BuildHumanReadableClipboardText(DisplayScalerProfile profile, string json)
        {
            string label = "DISPLAY-SCALER Resolution Profile";
            if (profile != null && profile.Resolution != null)
                label = profile.Resolution.Width + "x" + profile.Resolution.Height + " @ " + profile.Resolution.NominalRefreshRateHz + " Hz";

            return "DISPLAY-SCALER Resolution Profile" + Environment.NewLine +
                   label + Environment.NewLine +
                   "Paste this text into DISPLAY-SCALER to import the exact portable resolution." + Environment.NewLine +
                   json;
        }
    }
}
