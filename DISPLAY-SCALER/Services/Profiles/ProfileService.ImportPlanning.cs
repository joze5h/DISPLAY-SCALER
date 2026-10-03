using DISPLAY_SCALER.Models;
using System;

namespace DISPLAY_SCALER.Services
{
    public sealed partial class ResolutionProfileService
    {
        private const string PreserveUserEditedRefreshPolicy = "PreserveUserEditedRefreshRate";
        private const string KeepClipboardRequestedRefreshPolicy = "KeepClipboardRequestedRefresh";

        private uint ResolveImportRefresh(DisplayScalerProfile profile, MonitorInfo targetMonitor, out uint nominalRefresh, out uint exactMilliHz, out bool adaptedToTarget, out string reason)
        {
            DisplayProfileResolution resolution = profile == null ? null : profile.Resolution;
            nominalRefresh = NormalizeIntegerRefreshRate(resolution);
            exactMilliHz = 0;
            adaptedToTarget = false;
            reason = null;

            if (resolution == null || nominalRefresh == 0)
                return 0;

            uint sourceExactMilliHz = NormalizeProfileExactMilliHz(resolution, nominalRefresh);
            exactMilliHz = sourceExactMilliHz;

            if (ShouldPreserveUserEditedRefresh(profile))
                return nominalRefresh;

            uint targetExactMilliHz = ResolveExactMilliHzForTargetNominal(targetMonitor, nominalRefresh);
            if (targetExactMilliHz > 0 &&
                ExactMilliHzRoundsToNominal(targetExactMilliHz, nominalRefresh) &&
                targetExactMilliHz != sourceExactMilliHz)
            {
                exactMilliHz = targetExactMilliHz;
                adaptedToTarget = true;
                reason = "Номинальная частота " + nominalRefresh +
                    " Гц сохранена; адаптирована только точная дробная частота под целевой монитор: " +
                    FormatExactRefresh(sourceExactMilliHz) + " -> " + FormatExactRefresh(targetExactMilliHz) + ".";
            }

            return nominalRefresh;
        }

        private static bool ExactMilliHzRoundsToNominal(uint milliHz, uint nominalRefresh)
        {
            if (milliHz == 0 || nominalRefresh == 0)
                return false;

            uint rounded = (uint)Math.Round(milliHz / 1000.0, MidpointRounding.AwayFromZero);
            return rounded == nominalRefresh;
        }

        private static string FormatExactRefresh(uint milliHz)
        {
            if (milliHz == 0)
                return "0 Гц";

            return (milliHz / 1000.0).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " Гц";
        }

        private static bool ShouldPreserveUserEditedRefresh(DisplayScalerProfile profile)
        {
            if (profile == null || profile.Resolution == null)
                return false;

            if (IsExactTransferRefreshPolicy(profile.Resolution.ExactRefreshRatePolicy))
                return true;
            if (profile.CreationHints != null && IsExactTransferRefreshPolicy(profile.CreationHints.ExactRefreshRatePolicy))
                return true;

            return false;
        }

        private static bool IsExactTransferRefreshPolicy(string policy)
        {
            return string.Equals(policy, PreserveUserEditedRefreshPolicy, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(policy, KeepClipboardRequestedRefreshPolicy, StringComparison.OrdinalIgnoreCase);
        }

        private uint ResolveExactMilliHzForTargetNominal(MonitorInfo targetMonitor, uint nominal)
        {
            if (nominal == 0)
                return 0;

            if (targetMonitor != null)
            {
                uint targetNativeMilliHz = _display.ResolveNativeRefreshRateMilliHz(targetMonitor, nominal);
                uint normalizedTarget = NormalizeMilliHz(nominal, targetNativeMilliHz);
                if (normalizedTarget > 0)
                    return normalizedTarget;
            }

            return NormalizeMilliHz(nominal, 0);
        }

        private CustomResolution BuildTargetImportResolution(DisplayScalerProfile profile, MonitorInfo targetMonitor, out CustomResolution requestedResolution, out bool adaptedToTarget, out string adaptationReason, out bool refreshAdaptedToTarget, out string refreshAdaptationReason)
        {
            adaptedToTarget = false;
            adaptationReason = null;
            requestedResolution = null;
            refreshAdaptedToTarget = false;
            refreshAdaptationReason = null;

            if (profile == null || profile.Resolution == null)
                return null;
            ResolveImportRefresh(profile, targetMonitor, out var resolvedNominalRefresh, out var resolvedExactMilliHz, out var refreshAdapted, out var refreshReason);
            refreshAdaptedToTarget = refreshAdapted;
            refreshAdaptationReason = refreshReason;

            uint sourceNominalRefresh = NormalizeIntegerRefreshRate(profile.Resolution);
            requestedResolution = new CustomResolution
            {
                Width = profile.Resolution.Width,
                Height = profile.Resolution.Height,
                RefreshRate = sourceNominalRefresh,
                RefreshRateMilliHz = NormalizeProfileExactMilliHz(profile.Resolution, sourceNominalRefresh),
                BitsPerPixel = profile.Resolution.BitsPerPixel == 0 ? 32U : profile.Resolution.BitsPerPixel,
                AddedByDisplayScaler = true
            };

            CustomResolution resolved = CloneImportResolution(requestedResolution);
            resolved.RefreshRate = resolvedNominalRefresh;
            resolved.RefreshRateMilliHz = resolvedExactMilliHz;

            return resolved;
        }

        private static CustomResolution CloneImportResolution(CustomResolution source)
        {
            if (source == null)
                return null;
            return new CustomResolution
            {
                Width = source.Width,
                Height = source.Height,
                RefreshRate = source.RefreshRate,
                RefreshRateMilliHz = source.RefreshRateMilliHz,
                BitsPerPixel = source.BitsPerPixel == 0 ? 32U : source.BitsPerPixel,
                AddedByDisplayScaler = true,
                IsApplied = false
            };
        }
    }
}