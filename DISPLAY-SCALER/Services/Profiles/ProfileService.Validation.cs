using DISPLAY_SCALER.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DISPLAY_SCALER.Services
{
    public sealed partial class ResolutionProfileService
    {
        public ProfileImportPlan ValidateProfile(DisplayScalerProfile profile, MonitorInfo targetMonitor)
        {
            var plan = new ProfileImportPlan { Profile = profile };

            if (profile == null)
            {
                plan.BlockingIssues.Add("Файл не является корректным DISPLAY-SCALER профилем.");
                FinalizePlan(plan);
                return plan;
            }

            if (!string.Equals(profile.Application, AppName, StringComparison.OrdinalIgnoreCase))
                plan.BlockingIssues.Add("Профиль создан не для DISPLAY-SCALER.");
            if (profile.SchemaVersion < MinimumSupportedSchemaVersion || profile.SchemaVersion > CurrentSchemaVersion)
                plan.BlockingIssues.Add("Неподдерживаемая версия схемы профиля: " + profile.SchemaVersion + ".");
            if (targetMonitor == null)
                plan.BlockingIssues.Add("Целевой монитор не выбран.");
            else if (!targetMonitor.IsAttached || string.IsNullOrWhiteSpace(targetMonitor.DeviceName))
                plan.BlockingIssues.Add("Целевой монитор не подключён к активному desktop.");

            if (profile.Resolution == null)
            {
                plan.BlockingIssues.Add("В профиле отсутствует блок Resolution.");
                FinalizePlan(plan);
                return plan;
            }

            plan.RequiresDriverCustomSave = RequiresDriverCustomSave(profile);
            plan.Resolution = BuildTargetImportResolution(profile, targetMonitor, out var requestedResolution, out var adaptedToTarget, out var adaptationReason, out var refreshAdaptedToTarget, out var refreshAdaptationReason);
            plan.RequestedResolution = requestedResolution;
            plan.WasResolutionAdaptedToTarget = adaptedToTarget;
            plan.ResolutionAdaptationReason = adaptationReason;
            plan.WasRefreshAdaptedToTarget = refreshAdaptedToTarget;
            plan.RefreshAdaptationReason = refreshAdaptationReason;
            plan.EstimatedPixelClockMhz = EstimatePixelClockMhz(plan.Resolution);

            if (plan.WasResolutionAdaptedToTarget && !string.IsNullOrWhiteSpace(plan.ResolutionAdaptationReason))
            {
                plan.Warnings.Add(plan.ResolutionAdaptationReason);
                plan.Checks.Add("Размер режима пересчитан перед NVAPI-проверкой.");
            }

            if (plan.WasRefreshAdaptedToTarget && !string.IsNullOrWhiteSpace(plan.RefreshAdaptationReason))
            {
                plan.Checks.Add(plan.RefreshAdaptationReason);
            }

            if (profile.Diagnostics != null && profile.Diagnostics.Notes != null)
            {
                for (int i = 0; i < profile.Diagnostics.Notes.Count; i++)
                {
                    string note = profile.Diagnostics.Notes[i];
                    if (!string.IsNullOrWhiteSpace(note))
                        plan.Checks.Add(note);
                }
            }

            ValidateResolutionShape(plan, plan.Resolution);

            if (targetMonitor != null)
            {
                plan.ExactMonitorMatch = IsExactMonitorMatch(profile.SourceDisplay, targetMonitor);
                plan.SameNativeResolution = HasSameNativeResolution(profile.SourceDisplay, targetMonitor);
                plan.AlreadyAvailableInWindows = _display.IsWindowsModeEnumerated(targetMonitor, plan.Resolution);

                ValidateTargetMonitorRange(plan, targetMonitor, plan.Resolution);
                ValidateNvidiaAvailability(plan, targetMonitor);

                if (plan.AlreadyAvailableInWindows)
                {
                    if (plan.RequiresDriverCustomSave)
                        plan.Checks.Add("Точный режим уже есть в Windows mode list: " + plan.Resolution.Label + ", но CRU/EDID импорт всё равно будет выполнять NVAPI trial/save.");
                    else
                        plan.Checks.Add("Точный режим уже есть в Windows mode list: " + plan.Resolution.Label + ". NVAPI custom-save не потребуется.");
                }
                else if (_display.TryFindBestWindowsModeForResolution(targetMonitor, plan.Resolution.Width, plan.Resolution.Height, out var existingTargetMode) && existingTargetMode != null)
                    plan.Checks.Add("На целевом мониторе уже есть режим того же размера: найдено " + existingTargetMode.Label + ", к импорту подготовлено " + plan.Resolution.Label + ".");
            }

            FinalizePlan(plan);
            return plan;
        }

        public ProfileImportPlan RevalidateEditedImport(ProfileImportPlan basePlan, MonitorInfo targetMonitor, CustomResolution editedResolution)
        {
            if (basePlan == null)
                return ValidateProfile(null, targetMonitor);

            DisplayScalerProfile editedProfile = CloneProfileWithResolution(basePlan.Profile, editedResolution);
            ProfileImportPlan editedPlan = ValidateProfile(editedProfile, targetMonitor);
            editedPlan.FilePath = basePlan.FilePath;
            return editedPlan;
        }

        private static void ValidateResolutionShape(ProfileImportPlan plan, CustomResolution res)
        {
            if (res == null)
            {
                plan.BlockingIssues.Add("Разрешение не задано.");
                return;
            }

            if (res.Width < 320 || res.Width > 16384)
                plan.BlockingIssues.Add("Ширина вне безопасного диапазона 320–16384 px.");
            if (res.Height < 200 || res.Height > 16384)
                plan.BlockingIssues.Add("Высота вне безопасного диапазона 200–16384 px.");
            if (res.RefreshRate < 1 || res.RefreshRate > 1000)
                plan.BlockingIssues.Add("Частота вне безопасного диапазона 1–1000 Гц.");
            if (res.BitsPerPixel != 32)
                plan.BlockingIssues.Add("Профиль должен использовать 32 bpp. Режимы ниже 32 bpp не являются корректной целью для Windows 8+ manifest.");

            if (res.Width > 0 && res.Height > 0)
            {
                double aspect = (double)res.Width / res.Height;
                if (aspect < 0.35 || aspect > 4.10)
                {
                    plan.BlockingIssues.Add("Соотношение сторон " + aspect.ToString("F2") + ":1 выглядит ошибочным для игрового custom-resolution. Проверьте ширину и высоту.");
                }
                else if (aspect < 0.70 || aspect > 3.60)
                {
                    AddRisk(plan, 2, "Соотношение сторон " + aspect.ToString("F2") + ":1 нетипичное. NVAPI может отклонить такой режим.");
                }
            }
        }

        private void ValidateTargetMonitorRange(ProfileImportPlan plan, MonitorInfo target, CustomResolution res)
        {
            if (target == null || res == null)
                return;

            GetTargetReferenceResolution(target, out var targetWidth, out var targetHeight);
            if (TryGetBestTargetRefreshForExactResolution(target, res.Width, res.Height, out var exactTargetRefresh))
                plan.Checks.Add("Целевой монитор уже поддерживает " + res.Width + "×" + res.Height + " до " + exactTargetRefresh + " Гц.");
            else
                ValidateResolutionAgainstTarget(plan, targetWidth, targetHeight, res);

            uint maxRefresh = GetTargetReferenceRefresh(target);
            ValidateRefreshAgainstTarget(plan, target.MinVerticalRate, maxRefresh, res.RefreshRate);

            if (_display != null && _display.HasNativeRefreshHijackRisk(target, res, out var nativeHijackReason))
            {
                AddRisk(plan, 2,
                    "Возможен конфликт с родным/current-режимом целевого монитора на той же герцовке. " +
                    "Импорт не блокируется: решение будет принято реальным NVAPI trial-тестом и post-save проверкой. " +
                    (nativeHijackReason ?? string.Empty));
            }

            if (target.MaxPixelClockMhz > 0 && plan.EstimatedPixelClockMhz > 0)
            {
                double pixelClockRatio = plan.EstimatedPixelClockMhz / target.MaxPixelClockMhz;
                if (pixelClockRatio > 1.35)
                {
                    plan.BlockingIssues.Add("Оценочный pixel clock " + plan.EstimatedPixelClockMhz.ToString("F0") + " MHz слишком сильно выше EDID Max Pixel Clock " + target.MaxPixelClockMhz + " MHz.");
                }
                else if (pixelClockRatio > 1.18)
                {
                    AddRisk(plan, 2, "Оценочный pixel clock " + plan.EstimatedPixelClockMhz.ToString("F0") + " MHz заметно выше EDID Max Pixel Clock " + target.MaxPixelClockMhz + " MHz. Драйвер/монитор могут отклонить режим.");
                }
                else if (pixelClockRatio > 1.08)
                {
                    AddRisk(plan, 1, "Оценочный pixel clock " + plan.EstimatedPixelClockMhz.ToString("F0") + " MHz немного выше EDID Max Pixel Clock " + target.MaxPixelClockMhz + " MHz. Можно попробовать, но решение примет NVAPI.");
                }
            }
        }

        private static void ValidateResolutionAgainstTarget(ProfileImportPlan plan, uint targetWidth, uint targetHeight, CustomResolution res)
        {
            if (targetWidth == 0 || targetHeight == 0 || res == null)
                return;

            double widthRatio = (double)res.Width / targetWidth;
            double heightRatio = (double)res.Height / targetHeight;
            double areaRatio = ((double)res.Width * res.Height) / ((double)targetWidth * targetHeight);

            if (widthRatio <= 1.0 && heightRatio <= 1.0)
                return;

            string targetText = targetWidth + "×" + targetHeight;
            string modeText = res.Width + "×" + res.Height;

            if (widthRatio > 1.60 || heightRatio > 1.60 || areaRatio > 2.25)
            {
                plan.BlockingIssues.Add("Разрешение " + modeText + " слишком сильно превышает базовый режим целевого монитора " + targetText + ". Это похоже на ошибочный ввод.");
                return;
            }

            if (widthRatio > 1.25 || heightRatio > 1.25 || areaRatio > 1.50)
            {
                AddRisk(plan, 2, "Разрешение " + modeText + " заметно выше базового режима целевого монитора " + targetText + ". Можно пробовать, но не факт что данный режим пройдет по тестам.");
                return;
            }

            AddRisk(plan, 1, "Разрешение " + modeText + " немного выходит за базовый режим целевого монитора " + targetText + ". Можно пробовать, но не факт что данный режим пройдет по тестам.");
        }

        private static void ValidateRefreshAgainstTarget(ProfileImportPlan plan, uint minRefresh, uint maxRefresh, uint refresh)
        {
            if (minRefresh > 0 && refresh + 1 < minRefresh)
            {
                plan.BlockingIssues.Add("Частота " + refresh + " Гц ниже диапазона целевого монитора " + minRefresh + "–" + maxRefresh + " Гц.");
                return;
            }

            if (maxRefresh == 0 || refresh <= maxRefresh)
                return;

            uint mediumLimit = maxRefresh + Math.Max(10U, (uint)Math.Ceiling(maxRefresh * 0.05));
            uint hardLimit = maxRefresh + Math.Max(35U, (uint)Math.Ceiling(maxRefresh * 0.20));

            if (refresh <= mediumLimit)
            {
                AddRisk(plan, 1, "Частота " + refresh + " Гц немного выше заявленных " + maxRefresh + " Гц. Можно пробовать, но не факт что данный режим пройдет по тестам.");
                return;
            }

            if (refresh <= hardLimit)
            {
                AddRisk(plan, 2, "Частота " + refresh + " Гц заметно выше заявленных " + maxRefresh + " Гц. Риск высокий! Скорее всего ваш монитор отклонит тест.");
                return;
            }

            plan.BlockingIssues.Add("Частота " + refresh + " Гц слишком сильно выше заявленных " + maxRefresh + " Гц.");
        }

        private static void GetTargetReferenceResolution(MonitorInfo target, out uint width, out uint height)
        {
            width = 0;
            height = 0;
            if (target == null) return;

            if (target.NativeWidth > 0 && target.NativeHeight > 0)
            {
                width = target.NativeWidth;
                height = target.NativeHeight;
                return;
            }

            if (target.CurrentWidth > 0 && target.CurrentHeight > 0)
            {
                width = target.CurrentWidth;
                height = target.CurrentHeight;
                return;
            }

            long bestArea = 0;
            if (target.SupportedModes != null)
            {
                for (int i = 0; i < target.SupportedModes.Count; i++)
                {
                    ResolutionMode mode = target.SupportedModes[i];
                    if (mode == null || mode.Width == 0 || mode.Height == 0) continue;
                    long area = (long)mode.Width * mode.Height;
                    if (area > bestArea)
                    {
                        bestArea = area;
                        width = mode.Width;
                        height = mode.Height;
                    }
                }
            }
        }

        private static bool TryGetBestTargetRefreshForExactResolution(MonitorInfo targetMonitor, uint width, uint height, out uint refresh)
        {
            refresh = 0;
            if (targetMonitor == null || width == 0 || height == 0 || targetMonitor.SupportedModes == null)
                return false;

            for (int i = 0; i < targetMonitor.SupportedModes.Count; i++)
            {
                ResolutionMode mode = targetMonitor.SupportedModes[i];
                if (mode == null || mode.Width != width || mode.Height != height)
                    continue;
                if (mode.BitsPerPixel != 0 && mode.BitsPerPixel < 32)
                    continue;
                if (mode.IsInterlaced)
                    continue;
                if (mode.RefreshRate > refresh)
                    refresh = mode.RefreshRate;
            }

            return refresh > 0;
        }

        private static uint GetTargetReferenceRefresh(MonitorInfo target)
        {
            if (target == null) return 0;

            uint maxRefresh = target.CurrentRefreshRate;
            if (target.NativeRefreshRateHz > maxRefresh)
                maxRefresh = target.NativeRefreshRateHz;
            if (target.MaxVerticalRate > maxRefresh)
                maxRefresh = target.MaxVerticalRate;

            if (target.SupportedModes != null)
            {
                for (int i = 0; i < target.SupportedModes.Count; i++)
                {
                    ResolutionMode mode = target.SupportedModes[i];
                    if (mode == null || mode.IsInterlaced)
                        continue;
                    if (mode.BitsPerPixel != 0 && mode.BitsPerPixel < 32)
                        continue;
                    if (mode.RefreshRate > maxRefresh)
                        maxRefresh = mode.RefreshRate;
                }
            }
            return maxRefresh;
        }

        private static void AddRisk(ProfileImportPlan plan, int riskScore, string message)
        {
            if (plan == null) return;
            if (riskScore > plan.RiskScore)
                plan.RiskScore = riskScore;
            if (!string.IsNullOrWhiteSpace(message))
                plan.Warnings.Add(message);
        }

        private void ValidateNvidiaAvailability(ProfileImportPlan plan, MonitorInfo target)
        {
            bool requiresDriverCustomSave = plan != null && plan.RequiresDriverCustomSave;

            if (_nv == null || !_nv.IsInitialized)
            {
                if (!plan.AlreadyAvailableInWindows || requiresDriverCustomSave)
                    plan.BlockingIssues.Add("NVAPI не инициализирован. Невозможно безопасно создать NVIDIA custom resolution.");
                return;
            }

            if (!_nv.IsAvailable)
                plan.BlockingIssues.Add("NVIDIA-драйвер не обнаружен.");

            if (!string.IsNullOrWhiteSpace(target.AdapterName) && target.AdapterName.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) < 0 &&
                !string.IsNullOrWhiteSpace(target.AdapterDeviceId) && target.AdapterDeviceId.IndexOf("VEN_10DE", StringComparison.OrdinalIgnoreCase) < 0)
            {
                if (!plan.AlreadyAvailableInWindows || requiresDriverCustomSave)
                    plan.BlockingIssues.Add("Выбранный монитор не привязан к NVIDIA-адаптеру. Custom mode через NVAPI применить нельзя.");
            }
        }

        private static void FinalizePlan(ProfileImportPlan plan)
        {
            plan.CanImport = plan.BlockingIssues.Count == 0;
            if (!plan.CanImport)
            {
                plan.RiskLevel = "BLOCKED";
                plan.RiskScore = 3;
                plan.Summary = "Импорт заблокирован: " + BuildIssueText(plan.BlockingIssues);
                return;
            }

            if (plan.RiskScore <= 0)
                plan.RiskLevel = "LOW";
            else if (plan.RiskScore == 1)
                plan.RiskLevel = "MEDIUM";
            else
                plan.RiskLevel = "HIGH";

            string label = plan.Resolution == null ? "-" : plan.Resolution.Label;
            if (plan.WasResolutionAdaptedToTarget && plan.RequestedResolution != null && plan.Resolution != null)
                plan.Summary = "Импорт " + label + " - адаптировано из " + plan.RequestedResolution.Label + " - риск: " + plan.RiskLevel;
            else if (plan.WasRefreshAdaptedToTarget && plan.RequestedResolution != null && plan.Resolution != null)
                plan.Summary = "Импорт " + label + " - частота целевого монитора вместо " + plan.RequestedResolution.RefreshRate + " Гц - риск: " + plan.RiskLevel;
            else
                plan.Summary = "Импорт " + label + " - риск: " + plan.RiskLevel;
        }

        private static string BuildIssueText(IList<string> issues)
        {
            if (issues == null || issues.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            for (int i = 0; i < issues.Count; i++)
            {
                if (i > 0) sb.Append("; ");
                sb.Append(issues[i]);
            }
            return sb.ToString();
        }

        private static bool IsExactMonitorMatch(DisplayProfileMonitor source, MonitorInfo target)
        {
            if (source == null || target == null) return false;
            string sourceFingerprint = source.Fingerprint ?? string.Empty;
            string targetFingerprint = BuildMonitorFingerprint(target);
            if (!string.IsNullOrWhiteSpace(sourceFingerprint) && string.Equals(sourceFingerprint, targetFingerprint, StringComparison.OrdinalIgnoreCase))
                return true;

            return StringEquals(source.Manufacturer, target.Manufacturer) &&
                   StringEquals(source.ProductCode, target.ProductCode) &&
                   StringEquals(source.SerialNumber, target.SerialNumber) &&
                   !string.IsNullOrWhiteSpace(source.SerialNumber);
        }

        private static bool HasSameNativeResolution(DisplayProfileMonitor source, MonitorInfo target)
        {
            if (source == null || target == null) return false;
            return source.NativeWidth > 0 && source.NativeHeight > 0 &&
                   source.NativeWidth == target.NativeWidth && source.NativeHeight == target.NativeHeight;
        }

        private static string BuildMonitorFingerprint(MonitorInfo monitor)
        {
            if (monitor == null) return string.Empty;
            return BuildFingerprint(monitor.Manufacturer, monitor.ProductCode, monitor.SerialNumber, monitor.NativeWidth, monitor.NativeHeight);
        }

        private static string BuildFingerprint(string manufacturer, string productCode, string serial, uint nativeWidth, uint nativeHeight)
        {
            var sb = new StringBuilder(64);
            AppendUpperTrimmed(sb, manufacturer);
            sb.Append('|');
            AppendUpperTrimmed(sb, productCode);
            sb.Append('|');
            AppendUpperTrimmed(sb, serial);
            sb.Append('|').Append(nativeWidth).Append('x').Append(nativeHeight);
            return sb.ToString();
        }

        private static void AppendUpperTrimmed(StringBuilder sb, string value)
        {
            if (sb == null || string.IsNullOrWhiteSpace(value))
                return;

            int start = 0;
            int end = value.Length - 1;
            while (start <= end && char.IsWhiteSpace(value[start]))
                start++;
            while (end >= start && char.IsWhiteSpace(value[end]))
                end--;

            for (int i = start; i <= end; i++)
                sb.Append(char.ToUpperInvariant(value[i]));
        }

        private static bool StringEquals(string a, string b)
        {
            return string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}