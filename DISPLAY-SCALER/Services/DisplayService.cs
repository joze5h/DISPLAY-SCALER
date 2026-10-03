namespace DISPLAY_SCALER.Services
{
    public sealed partial class DisplayService : DISPLAY_SCALER.Services.Display.IDisplayService
    {
        private const uint DefaultBitsPerPixel = 32;
        private const int MaxDisplayModeEnumeration = 4096;
        private readonly NvApiService _nv;

        public DisplayService(NvApiService nv = null)
        {
            _nv = nv ?? NvApiService.Instance;
        }
    }
}