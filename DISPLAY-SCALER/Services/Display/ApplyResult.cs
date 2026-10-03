namespace DISPLAY_SCALER.Services
{
    public enum ApplyErrorCode
    {
        None = 0,
        Unknown = 1,
        MonitorNotSelected = 10,
        MonitorNotAttached = 11,
        ResolutionNotSpecified = 12,
        InvalidResolution = 20,
        InvalidBitsPerPixel = 21,
        MonitorRangeViolation = 22,
        NativeRefreshHijackRisk = 30,
        NvApiNotInitialized = 40,
        NvApiRejectedMode = 41,
        ModeAlreadyExists = 50,
        ModeNotVisibleInWindows = 60,
        UnsupportedMode = 65,
        DriverRestartFailed = 70,
        UserCancelled = 80
    }

    public sealed class ApplyResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Detail { get; set; }
        public ApplyErrorCode ErrorCode { get; set; }

        public static ApplyResult Ok(string detail = null)
        {
            return new ApplyResult { Success = true, Message = "OK", Detail = detail, ErrorCode = ApplyErrorCode.None };
        }

        public static ApplyResult Ok(string msg, string detail)
        {
            return new ApplyResult { Success = true, Message = msg ?? "OK", Detail = detail, ErrorCode = ApplyErrorCode.None };
        }

        public static ApplyResult Fail(string msg)
        {
            return new ApplyResult { Success = false, Message = msg, ErrorCode = ApplyErrorCode.Unknown };
        }

        public static ApplyResult Fail(string msg, string detail)
        {
            return new ApplyResult { Success = false, Message = msg, Detail = detail, ErrorCode = ApplyErrorCode.Unknown };
        }

        public static ApplyResult Fail(ApplyErrorCode code, string msg)
        {
            return new ApplyResult { Success = false, Message = msg, ErrorCode = code };
        }

        public static ApplyResult Fail(ApplyErrorCode code, string msg, string detail)
        {
            return new ApplyResult { Success = false, Message = msg, Detail = detail, ErrorCode = code };
        }
    }
}