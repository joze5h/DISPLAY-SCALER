using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DISPLAY_SCALER.Native
{
    internal static class Win32DisplayAdapterRestart
    {
        private static readonly Guid DisplayClassGuid = new Guid("4d36e968-e325-11ce-bfc1-08002be10318");

        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        private const uint DigcfPresent = 0x00000002;
        private const uint DifPropertyChange = 0x00000012;
        private const uint DicsEnable = 0x00000001;
        private const uint DicsDisable = 0x00000002;
        private const uint DicsFlagGlobal = 0x00000001;
        private const int ErrorNoMoreItems = 259;

        internal sealed class StateChangeResult
        {
            public int Enumerated { get; set; }
            public int Changed { get; set; }
            public string Error { get; set; }
        }

        public static StateChangeResult DisablePresentDisplayAdapters()
        {
            return SetPresentDisplayAdaptersState(DicsDisable);
        }

        public static StateChangeResult EnablePresentDisplayAdapters()
        {
            return SetPresentDisplayAdaptersState(DicsEnable);
        }

        private static StateChangeResult SetPresentDisplayAdaptersState(uint stateChange)
        {
            var result = new StateChangeResult();
            var errors = new List<string>();
            Guid displayClassGuid = DisplayClassGuid;
            IntPtr devices = SetupDiGetClassDevs(ref displayClassGuid, IntPtr.Zero, IntPtr.Zero, DigcfPresent);

            if (devices == InvalidHandleValue)
            {
                int error = Marshal.GetLastWin32Error();
                result.Error = BuildWin32Error("SetupDiGetClassDevs", error);
                return result;
            }

            try
            {
                uint index = 0;
                while (true)
                {
                    var device = new SP_DEVINFO_DATA
                    {
                        cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVINFO_DATA))
                    };

                    if (!SetupDiEnumDeviceInfo(devices, index, ref device))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error != ErrorNoMoreItems)
                            errors.Add(BuildWin32Error("SetupDiEnumDeviceInfo[" + index + "]", error));
                        break;
                    }

                    result.Enumerated++;

                    var parameters = new SP_PROPCHANGE_PARAMS
                    {
                        ClassInstallHeader = new SP_CLASSINSTALL_HEADER
                        {
                            cbSize = (uint)Marshal.SizeOf(typeof(SP_CLASSINSTALL_HEADER)),
                            InstallFunction = DifPropertyChange
                        },
                        StateChange = stateChange,
                        Scope = DicsFlagGlobal,
                        HwProfile = 0
                    };

                    if (!SetupDiSetClassInstallParams(
                        devices,
                        ref device,
                        ref parameters,
                        (uint)Marshal.SizeOf(typeof(SP_PROPCHANGE_PARAMS))))
                    {
                        int error = Marshal.GetLastWin32Error();
                        errors.Add(BuildWin32Error("SetupDiSetClassInstallParams[" + index + "]", error));
                        index++;
                        continue;
                    }

                    if (!SetupDiCallClassInstaller(DifPropertyChange, devices, ref device))
                    {
                        int error = Marshal.GetLastWin32Error();
                        errors.Add(BuildWin32Error("SetupDiCallClassInstaller[" + index + "]", error));
                        index++;
                        continue;
                    }

                    result.Changed++;
                    index++;
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(devices);
            }

            if (errors.Count > 0)
                result.Error = string.Join(" | ", errors);

            return result;
        }

        private static string BuildWin32Error(string operation, int error)
        {
            return operation + " failed. Win32Error=" + error + " (" + new Win32Exception(error).Message + ").";
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public UIntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_CLASSINSTALL_HEADER
        {
            public uint cbSize;
            public uint InstallFunction;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_PROPCHANGE_PARAMS
        {
            public SP_CLASSINSTALL_HEADER ClassInstallHeader;
            public uint StateChange;
            public uint Scope;
            public uint HwProfile;
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(
            ref Guid classGuid,
            IntPtr enumerator,
            IntPtr hwndParent,
            uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiEnumDeviceInfo(
            IntPtr deviceInfoSet,
            uint memberIndex,
            ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiSetClassInstallParams(
            IntPtr deviceInfoSet,
            ref SP_DEVINFO_DATA deviceInfoData,
            ref SP_PROPCHANGE_PARAMS classInstallParams,
            uint classInstallParamsSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiCallClassInstaller(
            uint installFunction,
            IntPtr deviceInfoSet,
            ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);
    }
}
