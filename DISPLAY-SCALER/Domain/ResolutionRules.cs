using DISPLAY_SCALER.Models;

namespace DISPLAY_SCALER.Domain
{
    public static class ResolutionRules
    {
        public const uint MinWidth = 320;
        public const uint MaxWidth = 16384;
        public const uint MinHeight = 200;
        public const uint MaxHeight = 16384;
        public const uint MinRefreshRate = 1;
        public const uint MaxRefreshRate = 1000;
        public const uint DefaultBitsPerPixel = 32;

        public static bool TryValidate(uint width, uint height, uint refreshRate, out string message)
        {
            if (width < MinWidth || width > MaxWidth)
            {
                message = "Ширина должна быть от " + MinWidth + " до " + MaxWidth + " px.";
                return false;
            }

            if (height < MinHeight || height > MaxHeight)
            {
                message = "Высота должна быть от " + MinHeight + " до " + MaxHeight + " px.";
                return false;
            }

            if (refreshRate < MinRefreshRate || refreshRate > MaxRefreshRate)
            {
                message = "Частота обновления должна быть от " + MinRefreshRate + " до " + MaxRefreshRate + " Гц.";
                return false;
            }

            message = null;
            return true;
        }

        public static CustomResolution Create(uint width, uint height, uint refreshRate, bool addedByDisplayScaler = true)
        {
            return new CustomResolution
            {
                Width = width,
                Height = height,
                RefreshRate = refreshRate,
                BitsPerPixel = DefaultBitsPerPixel,
                AddedByDisplayScaler = addedByDisplayScaler
            };
        }
    }
}