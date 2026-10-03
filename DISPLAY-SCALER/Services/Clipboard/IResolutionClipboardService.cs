using DISPLAY_SCALER.Models;

namespace DISPLAY_SCALER.Services.Clipboard
{
    public interface IResolutionClipboardService
    {
        void CopyProfile(DisplayScalerProfile profile);
        bool TryReadProfile(out DisplayScalerProfile profile, out string error);
    }
}
