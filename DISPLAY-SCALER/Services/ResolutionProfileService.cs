using DISPLAY_SCALER.Services.Display;
using DISPLAY_SCALER.Services.Profiles;
using System;
using System.Text;

namespace DISPLAY_SCALER.Services
{
    public sealed partial class ResolutionProfileService : IResolutionProfileService
    {
        private const string AppName = "DISPLAY-SCALER";
        private const int CurrentSchemaVersion = 10;
        private const int MinimumSupportedSchemaVersion = 1;
        private const int ProfileJsonMaxLength = 1024 * 1024;
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
        private static readonly string LogEntrySeparator = Environment.NewLine + new string('=', 92) + Environment.NewLine;
        private static readonly string CachedAppVersion = ResolveAppVersion();
        private readonly IDisplayService _display;
        private readonly NvApiService _nv;
        private readonly DisplayDriverRestartService _driverRestart;

        public ResolutionProfileService(IDisplayService display, NvApiService nv, DisplayDriverRestartService driverRestart)
        {
            _display = display ?? throw new ArgumentNullException(nameof(display));
            _nv = nv ?? throw new ArgumentNullException(nameof(nv));
            _driverRestart = driverRestart ?? throw new ArgumentNullException(nameof(driverRestart));
        }
    }
}