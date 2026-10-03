using DISPLAY_SCALER.Domain;
using DISPLAY_SCALER.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DISPLAY_SCALER.Services
{
    internal static class CruEdidBinProfileCodec
    {
        private const int EdidBlockSize = 128;
        private const int BaseDetailedOffset = 54;
        private const int DetailedTimingSize = 18;
        private const int MaxEdidBlocks = 64;

        public static bool LooksLikeEdidBin(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            FileInfo info = new FileInfo(path);
            if (info.Length < EdidBlockSize || info.Length % EdidBlockSize != 0 || info.Length > EdidBlockSize * MaxEdidBlocks)
                return false;

            byte[] header = new byte[8];
            using (FileStream stream = File.OpenRead(path))
            {
                if (stream.Read(header, 0, header.Length) != header.Length)
                    return false;
            }

            return header[0] == 0x00 && header[1] == 0xFF && header[2] == 0xFF && header[3] == 0xFF &&
                   header[4] == 0xFF && header[5] == 0xFF && header[6] == 0xFF && header[7] == 0x00;
        }

        public static void Export(string path, MonitorInfo monitor, CustomResolution resolution)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Путь экспорта не задан.", "path");
            if (resolution == null)
                throw new ArgumentNullException("resolution");

            CustomResolution mode = NormalizeResolution(resolution, monitor);
            byte[] edid = BuildSingleModeEdid(mode);
            File.WriteAllBytes(path, edid);
        }

        public static DisplayScalerProfile Import(string path, out string note)
        {
            note = null;
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Путь импорта не задан.", "path");

            FileInfo info = new FileInfo(path);
            if (!info.Exists)
                throw new FileNotFoundException("CRU/EDID .bin файл не найден.", path);
            if (info.Length < EdidBlockSize || info.Length % EdidBlockSize != 0 || info.Length > EdidBlockSize * MaxEdidBlocks)
                throw new InvalidDataException("CRU .bin должен содержать от 1 до " + MaxEdidBlocks + " EDID-блоков по 128 байт.");

            byte[] data = File.ReadAllBytes(path);
            if (!HasEdidHeader(data))
                throw new InvalidDataException("Файл не похож на EDID .bin: отсутствует стандартный EDID-заголовок.");

            ValidateEdidBlocks(data);

            var timings = new List<EdidDetailedTiming>();
            ParseBaseDetailedTimings(data, timings);
            ParseCtaDetailedTimings(data, timings);

            if (timings.Count == 0)
                throw new InvalidDataException("В EDID .bin не найдено detailed timing resolutions, которые можно импортировать как custom resolution.");

            EdidDetailedTiming selected = timings[0];
            if (TryReadExactRefreshMarker(data, out var exactRefreshMilliHz))
            {
                uint dtdNominal = (uint)Math.Max(1, Math.Round(selected.RefreshHz, MidpointRounding.AwayFromZero));
                uint markerNominal = (uint)Math.Round(exactRefreshMilliHz / 1000.0, MidpointRounding.AwayFromZero);
                if (markerNominal == dtdNominal)
                    selected.ExactRefreshMilliHz = exactRefreshMilliHz;
            }

            note = "CRU/EDID .bin: найдено detailed timings: " + timings.Count + ". Импортируется первый detailed timing как основной режим: " + selected.Width + "×" + selected.Height + " @ " + selected.RefreshHz.ToString(CultureInfo.InvariantCulture) + " Гц.";
            return BuildProfile(selected, note);
        }

        private static DisplayScalerProfile BuildProfile(EdidDetailedTiming timing, string note)
        {
            uint refresh = (uint)Math.Max(1, Math.Round(timing.RefreshHz, MidpointRounding.AwayFromZero));
            uint exactMilliHz = timing.ExactRefreshMilliHz > 0
                ? timing.ExactRefreshMilliHz
                : (uint)Math.Max(1, Math.Round(timing.RefreshHz * 1000.0, MidpointRounding.AwayFromZero));
            var profile = new DisplayScalerProfile
            {
                SchemaVersion = 10,
                Application = "DISPLAY-SCALER",
                ExportKind = "CRU_EDID_BIN",
                PortableAcrossDisplays = true,
                ProfileName = timing.Width + "×" + timing.Height + " @ " + refresh + " Гц",
                ExportedUtc = DateTime.UtcNow.ToString("o"),
                Resolution = new DisplayProfileResolution
                {
                    Width = timing.Width,
                    Height = timing.Height,
                    RefreshRate = refresh,
                    RefreshRateHz = refresh,
                    NominalRefreshRateHz = refresh,
                    ExactRefreshRateMilliHz = exactMilliHz,
                    ExactRefreshRatePolicy = "KeepClipboardRequestedRefresh",
                    IntegerRefreshRate = exactMilliHz == refresh * 1000U,
                    PreserveLegitimateFractionalRefreshRates = true,
                    BitsPerPixel = 32,
                    AspectRatio = AspectRatioMath.Format(timing.Width, timing.Height),
                    TimingMode = "CRU_EDID_DETAILED_TIMING",
                    EstimatedPixelClockMhz = timing.PixelClockMhz
                },
                CreationHints = new DisplayProfileCreationHints
                {
                    TargetApi = "NVIDIA_NVAPI_CUSTOM_DISPLAY",
                    TimingMode = "CRU_EDID_DETAILED_TIMING",
                    RecalculateTimingOnImport = true,
                    SourceMonitorIsBinding = false,
                    ExactRefreshRatePolicy = "KeepClipboardRequestedRefresh",
                    SizeTransferPolicy = "TryExactTargetModeNoAutomaticFit",
                    PreferTargetNativeRefreshRate = false,
                    PreserveLegitimateFractionalRefreshRates = true,
                    FitToTargetNativeWhenOversized = false,
                    PreserveExistingTargetNativeModes = true,
                    Notes = "Импортировано из CRU-compatible EDID .bin."
                },
                SourceDisplay = null,
                Diagnostics = new DisplayProfileDiagnostics()
            };
            profile.Diagnostics.Notes.Add(note);
            return profile;
        }

        private static CustomResolution NormalizeResolution(CustomResolution resolution, MonitorInfo monitor)
        {
            uint refreshMilliHz = resolution.RefreshRateMilliHz;
            if (refreshMilliHz == 0 && monitor != null)
                refreshMilliHz = DisplayService.ResolveNativeRefreshRateMilliHzStatic(monitor, resolution.RefreshRate);
            if (refreshMilliHz == 0)
                refreshMilliHz = SafeMultiplyHzToMilliHz(resolution.RefreshRate);

            return new CustomResolution
            {
                Width = resolution.Width,
                Height = resolution.Height,
                RefreshRate = resolution.RefreshRate,
                RefreshRateMilliHz = refreshMilliHz,
                BitsPerPixel = resolution.BitsPerPixel == 0 ? 32U : resolution.BitsPerPixel,
                AddedByDisplayScaler = resolution.AddedByDisplayScaler
            };
        }

        private static byte[] BuildSingleModeEdid(CustomResolution mode)
        {
            return BuildSyntheticSingleModeEdid(mode);
        }

        private static byte[] BuildSyntheticSingleModeEdid(CustomResolution mode)
        {
            byte[] edid = new byte[EdidBlockSize];
            byte[] header = { 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00 };
            Buffer.BlockCopy(header, 0, edid, 0, header.Length);

            ushort manufacturer = EncodeManufacturer("DSC");
            edid[8] = (byte)(manufacturer >> 8);
            edid[9] = (byte)(manufacturer & 0xFF);
            const ushort product = 0x5344;
            edid[10] = (byte)(product & 0xFF);
            edid[11] = (byte)(product >> 8);
            edid[16] = 1;
            edid[17] = 4;
            edid[18] = 1;
            edid[19] = 4;
            edid[20] = 0xA5;
            edid[21] = 0;
            edid[22] = 0;
            edid[23] = 0x78;
            edid[24] = 0x0A;
            edid[25] = 0xEE;
            edid[26] = 0x95;
            edid[27] = 0xA3;
            edid[28] = 0x54;
            edid[29] = 0x4C;
            edid[30] = 0x99;
            edid[31] = 0x26;
            edid[32] = 0x0F;

            for (int offset = 38; offset < 54; offset += 2)
            {
                edid[offset] = 0x01;
                edid[offset + 1] = 0x01;
            }

            byte[] dtd = BuildDetailedTimingDescriptor(mode);
            Buffer.BlockCopy(dtd, 0, edid, BaseDetailedOffset, dtd.Length);
            WriteMonitorNameDescriptor(edid, 72, "DISPLAY-SCALER");
            WriteExactRefreshDescriptor(edid, 90, mode);
            WriteDummyDescriptor(edid, 108);

            edid[126] = 0;
            UpdateChecksum(edid, 0);
            return edid;
        }

        private static byte[] BuildDetailedTimingDescriptor(CustomResolution mode)
        {
            if (mode == null)
                throw new ArgumentNullException("mode");
            if (mode.Width == 0 || mode.Height == 0)
                throw new InvalidOperationException("Ширина и высота EDID detailed timing должны быть больше нуля.");
            if (mode.Width > 4095 || mode.Height > 4095)
                throw new InvalidOperationException("Режим " + mode.Width + "×" + mode.Height + " нельзя без потерь записать в EDID 1.x detailed timing: active size ограничен 12 битами (максимум 4095).");

            uint width = mode.Width;
            uint height = mode.Height;
            uint refreshMilliHz = mode.RefreshRateMilliHz == 0 ? SafeMultiplyHzToMilliHz(mode.RefreshRate) : mode.RefreshRateMilliHz;
            if (refreshMilliHz == 0)
                throw new InvalidOperationException("Частота обновления EDID detailed timing не задана.");

            uint hBlank = CalculateHorizontalBlanking(width);
            uint vBlank = CalculateVerticalBlanking(height);
            if (hBlank > 4095 || vBlank > 4095)
                throw new InvalidOperationException("Blanking режима не помещается в 12-битные поля EDID detailed timing.");

            uint hTotal = width + hBlank;
            uint vTotal = height + vBlank;
            double pixelClock10KHzExact = ((double)hTotal * vTotal * (refreshMilliHz / 1000.0)) / 10000.0;
            if (double.IsNaN(pixelClock10KHzExact) || double.IsInfinity(pixelClock10KHzExact) || pixelClock10KHzExact < 1.0 || pixelClock10KHzExact > 65535.0)
                throw new InvalidOperationException("Режим " + mode.Label + " нельзя без потерь записать в EDID 1.x detailed timing: pixel clock превышает 655.35 МГц или некорректен.");

            uint pixelClock10KHz = (uint)Math.Round(pixelClock10KHzExact, MidpointRounding.AwayFromZero);
            if (pixelClock10KHz == 0 || pixelClock10KHz > 65535)
                throw new InvalidOperationException("Pixel clock EDID detailed timing вышел за допустимый 16-битный диапазон.");

            uint hSyncOffset = Math.Max(8U, RoundToMultiple(hBlank / 3, 8));
            uint hSyncWidth = Math.Max(8U, RoundToMultiple(hBlank / 4, 8));
            if (hSyncOffset + hSyncWidth >= hBlank)
            {
                hSyncOffset = Math.Max(8U, hBlank / 4);
                hSyncWidth = Math.Max(8U, hBlank / 4);
            }
            uint vSyncOffset = 3;
            uint vSyncWidth = 5;
            if (vSyncOffset + vSyncWidth >= vBlank)
            {
                vSyncOffset = 1;
                vSyncWidth = 3;
            }

            if (hSyncOffset > 1023 || hSyncWidth > 1023 || vSyncOffset > 63 || vSyncWidth > 63)
                throw new InvalidOperationException("Sync-параметры режима не помещаются в поля EDID detailed timing.");

            byte[] d = new byte[DetailedTimingSize];
            d[0] = (byte)(pixelClock10KHz & 0xFF);
            d[1] = (byte)((pixelClock10KHz >> 8) & 0xFF);
            d[2] = (byte)(width & 0xFF);
            d[3] = (byte)(hBlank & 0xFF);
            d[4] = (byte)((((width >> 8) & 0x0F) << 4) | ((hBlank >> 8) & 0x0F));
            d[5] = (byte)(height & 0xFF);
            d[6] = (byte)(vBlank & 0xFF);
            d[7] = (byte)((((height >> 8) & 0x0F) << 4) | ((vBlank >> 8) & 0x0F));
            d[8] = (byte)(hSyncOffset & 0xFF);
            d[9] = (byte)(hSyncWidth & 0xFF);
            d[10] = (byte)(((vSyncOffset & 0x0F) << 4) | (vSyncWidth & 0x0F));
            d[11] = (byte)((((hSyncOffset >> 8) & 0x03) << 6) | (((hSyncWidth >> 8) & 0x03) << 4) | (((vSyncOffset >> 4) & 0x03) << 2) | ((vSyncWidth >> 4) & 0x03));
            d[12] = 0;
            d[13] = 0;
            d[14] = 0;
            d[15] = 0;
            d[16] = 0;
            d[17] = 0x1E;
            return d;
        }

        private static uint SafeMultiplyHzToMilliHz(uint refresh)
        {
            if (refresh == 0 || refresh > uint.MaxValue / 1000U)
                return 0;
            return refresh * 1000U;
        }

        private static void ParseBaseDetailedTimings(byte[] data, List<EdidDetailedTiming> timings)
        {
            for (int offset = BaseDetailedOffset; offset <= 108; offset += DetailedTimingSize)
                TryParseDetailedTiming(data, offset, timings);
        }

        private static void ParseCtaDetailedTimings(byte[] data, List<EdidDetailedTiming> timings)
        {
            int blockCount = data.Length / EdidBlockSize;
            for (int block = 1; block < blockCount; block++)
            {
                int start = block * EdidBlockSize;
                if (data[start] != 0x02)
                    continue;

                int dtdOffset = data[start + 2];
                if (dtdOffset == 0 || dtdOffset < 4 || dtdOffset > 127)
                    continue;

                for (int offset = start + dtdOffset; offset + DetailedTimingSize <= start + 127; offset += DetailedTimingSize)
                    TryParseDetailedTiming(data, offset, timings);
            }
        }

        private static void TryParseDetailedTiming(byte[] data, int offset, List<EdidDetailedTiming> timings)
        {
            if (offset < 0 || offset + DetailedTimingSize > data.Length)
                return;

            uint pixelClock10KHz = (uint)(data[offset] | (data[offset + 1] << 8));
            if (pixelClock10KHz == 0)
                return;

            uint hActive = (uint)(data[offset + 2] | ((data[offset + 4] >> 4) << 8));
            uint hBlank = (uint)(data[offset + 3] | ((data[offset + 4] & 0x0F) << 8));
            uint vActive = (uint)(data[offset + 5] | ((data[offset + 7] >> 4) << 8));
            uint vBlank = (uint)(data[offset + 6] | ((data[offset + 7] & 0x0F) << 8));
            if (hActive == 0 || vActive == 0 || hBlank == 0 || vBlank == 0)
                return;

            double pixelClockMhz = pixelClock10KHz / 100.0;
            double refresh = (pixelClockMhz * 1000000.0) / ((hActive + hBlank) * (vActive + vBlank));
            if (refresh < 1.0 || refresh > 1000.0)
                return;

            timings.Add(new EdidDetailedTiming
            {
                Width = hActive,
                Height = vActive,
                RefreshHz = refresh,
                PixelClockMhz = pixelClockMhz
            });
        }

        private static bool TryReadExactRefreshMarker(byte[] data, out uint exactRefreshMilliHz)
        {
            exactRefreshMilliHz = 0;
            if (data == null || data.Length < EdidBlockSize)
                return false;

            for (int offset = BaseDetailedOffset; offset <= 108; offset += DetailedTimingSize)
            {
                uint pixelClock10KHz = (uint)(data[offset] | (data[offset + 1] << 8));
                if (pixelClock10KHz != 0 || data[offset + 3] != 0xFF)
                    continue;

                string text = System.Text.Encoding.ASCII.GetString(data, offset + 5, 13).Trim(' ', '\0', '\r', '\n');
                if (!text.StartsWith("DSCRR", StringComparison.Ordinal))
                    continue;

                if (uint.TryParse(text.Substring(5), NumberStyles.Integer, CultureInfo.InvariantCulture, out exactRefreshMilliHz) && exactRefreshMilliHz > 0)
                    return true;
            }

            exactRefreshMilliHz = 0;
            return false;
        }

        private static void ValidateEdidBlocks(byte[] data)
        {
            int actualBlocks = data.Length / EdidBlockSize;
            int declaredBlocks = 1 + data[126];
            if (declaredBlocks != actualBlocks)
                throw new InvalidDataException("EDID extension count не совпадает с размером файла: объявлено блоков " + declaredBlocks + ", фактически " + actualBlocks + ".");

            for (int block = 0; block < actualBlocks; block++)
            {
                int start = block * EdidBlockSize;
                int sum = 0;
                for (int i = 0; i < EdidBlockSize; i++)
                    sum += data[start + i];
                if ((sum & 0xFF) != 0)
                    throw new InvalidDataException("Некорректная контрольная сумма EDID-блока " + block + ".");
            }
        }

        private static bool HasEdidHeader(byte[] data)
        {
            return data != null && data.Length >= 8 && data[0] == 0x00 && data[1] == 0xFF && data[2] == 0xFF && data[3] == 0xFF &&
                   data[4] == 0xFF && data[5] == 0xFF && data[6] == 0xFF && data[7] == 0x00;
        }

        private static ushort EncodeManufacturer(string manufacturer)
        {
            string text = string.IsNullOrWhiteSpace(manufacturer) ? "DSC" : manufacturer.Trim().ToUpperInvariant();
            if (text.Length < 3)
                text = (text + "DSC").Substring(0, 3);
            if (text.Length > 3)
                text = text.Substring(0, 3);

            int a = EncodeManufacturerChar(text[0]);
            int b = EncodeManufacturerChar(text[1]);
            int c = EncodeManufacturerChar(text[2]);
            return (ushort)((a << 10) | (b << 5) | c);
        }

        private static int EncodeManufacturerChar(char c)
        {
            if (c < 'A' || c > 'Z')
                return 4;
            return c - 'A' + 1;
        }

        private static uint CalculateHorizontalBlanking(uint width)
        {
            return Math.Max(160U, RoundToMultiple(width / 12, 8));
        }

        private static uint CalculateVerticalBlanking(uint height)
        {
            return Math.Max(30U, (uint)Math.Ceiling(height * 0.035));
        }

        private static uint RoundToMultiple(uint value, uint multiple)
        {
            if (multiple == 0)
                return value;
            return ((value + multiple - 1) / multiple) * multiple;
        }

        private static void WriteMonitorNameDescriptor(byte[] edid, int offset, string name)
        {
            WriteDescriptorHeader(edid, offset, 0xFC);
            string value = (name ?? "DISPLAY-SCALER").PadRight(13, ' ');
            for (int i = 0; i < 13; i++)
                edid[offset + 5 + i] = (byte)value[i];
        }

        private static void WriteExactRefreshDescriptor(byte[] edid, int offset, CustomResolution mode)
        {
            WriteDescriptorHeader(edid, offset, 0xFF);
            uint exactMilliHz = mode == null ? 0U : mode.RefreshRateMilliHz;
            if (exactMilliHz == 0 && mode != null)
                exactMilliHz = SafeMultiplyHzToMilliHz(mode.RefreshRate);

            string value = ("DSCRR" + exactMilliHz.ToString(CultureInfo.InvariantCulture)).PadRight(13, ' ');
            if (value.Length > 13)
                value = value.Substring(0, 13);
            for (int i = 0; i < 13; i++)
                edid[offset + 5 + i] = (byte)value[i];
        }

        private static void WriteDummyDescriptor(byte[] edid, int offset)
        {
            WriteDescriptorHeader(edid, offset, 0x10);
        }

        private static void WriteDescriptorHeader(byte[] edid, int offset, byte tag)
        {
            for (int i = 0; i < DetailedTimingSize; i++)
                edid[offset + i] = 0;
            edid[offset + 3] = tag;
        }

        private static void UpdateChecksum(byte[] edid, int blockStart)
        {
            int sum = 0;
            for (int i = 0; i < 127; i++)
                sum += edid[blockStart + i];
            edid[blockStart + 127] = (byte)((256 - (sum & 0xFF)) & 0xFF);
        }

        private sealed class EdidDetailedTiming
        {
            public uint Width;
            public uint Height;
            public double RefreshHz;
            public uint ExactRefreshMilliHz;
            public double PixelClockMhz;
        }
    }
}