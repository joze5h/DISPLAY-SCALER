using DISPLAY_SCALER.Infrastructure.Timing;
using DISPLAY_SCALER.Models;
using DISPLAY_SCALER.Native;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DISPLAY_SCALER.Services
{
    public sealed class DisplayDriverRestartService
    {
        private const int DriverDisableSettleMs = 2500;
        private const int DriverEnableSettleMs = 3000;

        private const string GraphicsConfigurationsRegistryPath = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Configuration";
        private const string GraphicsConnectivityRegistryPath = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Connectivity";
        private const string DisplayRegistryPath = @"SYSTEM\CurrentControlSet\Enum\DISPLAY";
        private const string DisplayScalerMarkerRegistryPath = @"Software\DISPLAY-SCALER\CustomResolutionsV2";
        private const string StableDisplayScalerMarkerRegistryPath = @"Software\DISPLAY-SCALER\CustomResolutionsV3";

        public DriverRestartResult RestartDisplayAdapter(MonitorInfo monitor)
        {
            if (monitor == null)
                return DriverRestartResult.NotAttempted("Монитор не определён.");

            return RestartAllPresentDisplayAdapters();
        }

        public DriverRestartResult RestartAllPresentDisplayAdapters()
        {
            var result = new DriverRestartResult
            {
                Attempted = true,
                CommandLine = "SetupAPI: present display adapters -> DICS_DISABLE -> DICS_ENABLE"
            };

            try
            {
                Win32DisplayAdapterRestart.StateChangeResult disabled = Win32DisplayAdapterRestart.DisablePresentDisplayAdapters();
                if (disabled.Changed <= 0)
                {
                    result.Success = false;
                    result.Error = BuildStateChangeFailure("отключить", disabled);
                    return result;
                }

                NativeWait.Sleep(DriverDisableSettleMs);

                Win32DisplayAdapterRestart.StateChangeResult enabled = Win32DisplayAdapterRestart.EnablePresentDisplayAdapters();
                if (enabled.Changed <= 0)
                {
                    result.Success = false;
                    result.Error = BuildStateChangeFailure("включить", enabled);
                    return result;
                }

                NativeWait.Sleep(DriverEnableSettleMs);

                result.Success = true;
                result.Output = BuildRestartDetail(disabled, enabled);
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Display-adapter PnP restart failed: " + ex.Message);
                result.Success = false;
                result.Error = ex.Message;
                return result;
            }
        }

        public ApplyResult ResetAllDisplayConfiguration()
        {
            var details = new List<string>();
            var errors = new List<string>();

            int configurations = DeleteAllSubKeys(Registry.LocalMachine, GraphicsConfigurationsRegistryPath, errors);
            int connectivity = DeleteAllSubKeys(Registry.LocalMachine, GraphicsConnectivityRegistryPath, errors);
            int displayOverrides = DeleteAllDisplayOverrides(errors);
            int markers = DeleteAllRegistryValues(Registry.CurrentUser, DisplayScalerMarkerRegistryPath, errors);
            markers += DeleteAllRegistryValues(Registry.CurrentUser, StableDisplayScalerMarkerRegistryPath, errors);

            details.Add("GraphicsDrivers Configuration: " + configurations);
            details.Add("GraphicsDrivers Connectivity: " + connectivity);
            details.Add("EDID override/cache entries: " + displayOverrides);
            details.Add("DISPLAY-SCALER markers: " + markers);

            string detail = string.Join("; ", details);
            if (errors.Count > 0)
            {
                return ApplyResult.Fail(
                    "Полный сброс выполнен не полностью.",
                    detail + ". Ошибки: " + string.Join(" | ", errors));
            }

            return ApplyResult.Ok(
                "Полный сброс конфигурации дисплеев выполнен.",
                detail);
        }

        private static string BuildRestartDetail(
            Win32DisplayAdapterRestart.StateChangeResult disabled,
            Win32DisplayAdapterRestart.StateChangeResult enabled)
        {
            var parts = new List<string>
            {
                "Отключено адаптеров: " + disabled.Changed + " из " + disabled.Enumerated,
                "включено адаптеров: " + enabled.Changed + " из " + enabled.Enumerated
            };

            if (!string.IsNullOrWhiteSpace(disabled.Error))
                parts.Add("предупреждение при отключении: " + disabled.Error.Trim());

            if (!string.IsNullOrWhiteSpace(enabled.Error))
                parts.Add("предупреждение при включении: " + enabled.Error.Trim());

            return string.Join("; ", parts) + ".";
        }

        private static string BuildStateChangeFailure(string action, Win32DisplayAdapterRestart.StateChangeResult state)
        {
            string detail = state == null || string.IsNullOrWhiteSpace(state.Error)
                ? "SetupAPI не изменил состояние ни одного присутствующего display-адаптера."
                : state.Error.Trim();

            int enumerated = state == null ? 0 : state.Enumerated;
            return "Не удалось " + action + " графический драйвер. Найдено display-адаптеров: " + enumerated + ". " + detail;
        }

        private static int DeleteAllSubKeys(RegistryKey hive, string path, List<string> errors)
        {
            if (hive == null || string.IsNullOrWhiteSpace(path))
                return 0;

            int removed = 0;
            try
            {
                using (RegistryKey key = hive.OpenSubKey(path, true))
                {
                    if (key == null)
                        return 0;

                    string[] names = key.GetSubKeyNames();
                    for (int i = 0; i < names.Length; i++)
                    {
                        try
                        {
                            key.DeleteSubKeyTree(names[i]);
                            removed++;
                        }
                        catch (Exception ex)
                        {
                            errors.Add(path + "\\" + names[i] + ": " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(path + ": " + ex.Message);
            }

            return removed;
        }

        private static int DeleteAllRegistryValues(RegistryKey hive, string path, List<string> errors)
        {
            if (hive == null || string.IsNullOrWhiteSpace(path))
                return 0;

            int removed = 0;
            try
            {
                using (RegistryKey key = hive.OpenSubKey(path, true))
                {
                    if (key == null)
                        return 0;

                    string[] names = key.GetValueNames();
                    for (int i = 0; i < names.Length; i++)
                    {
                        try
                        {
                            key.DeleteValue(names[i], false);
                            removed++;
                        }
                        catch (Exception ex)
                        {
                            errors.Add(path + "\\" + names[i] + ": " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(path + ": " + ex.Message);
            }

            return removed;
        }

        private static int DeleteAllDisplayOverrides(List<string> errors)
        {
            int removed = 0;
            try
            {
                using (RegistryKey displays = Registry.LocalMachine.OpenSubKey(DisplayRegistryPath, true))
                {
                    if (displays == null)
                        return 0;

                    string[] displayIds = displays.GetSubKeyNames();
                    for (int i = 0; i < displayIds.Length; i++)
                    {
                        string displayId = displayIds[i];
                        using (RegistryKey display = displays.OpenSubKey(displayId, true))
                        {
                            if (display == null)
                                continue;

                            string[] deviceIds = display.GetSubKeyNames();
                            for (int j = 0; j < deviceIds.Length; j++)
                            {
                                string deviceId = deviceIds[j];
                                string devicePath = DisplayRegistryPath + "\\" + displayId + "\\" + deviceId + "\\Device Parameters";
                                try
                                {
                                    using (RegistryKey deviceParameters = display.OpenSubKey(deviceId + "\\Device Parameters", true))
                                    {
                                        if (deviceParameters == null)
                                            continue;

                                        removed += DeleteSubKeyTreeIfPresent(deviceParameters, "EDID_OVERRIDE");
                                        removed += DeleteSubKeyTreeIfPresent(deviceParameters, "EDID_RECOVERY");
                                        if (deviceParameters.GetValue("EDID") != null)
                                        {
                                            deviceParameters.DeleteValue("EDID", false);
                                            removed++;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    errors.Add(devicePath + ": " + ex.Message);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(DisplayRegistryPath + ": " + ex.Message);
            }

            return removed;
        }

        private static int DeleteSubKeyTreeIfPresent(RegistryKey key, string subKeyName)
        {
            using (RegistryKey existing = key.OpenSubKey(subKeyName))
            {
                if (existing == null)
                    return 0;
            }

            key.DeleteSubKeyTree(subKeyName);
            return 1;
        }
    }
}
