using System;
using System.Diagnostics;

namespace DISPLAY_SCALER.Infrastructure.Diagnostics
{
    public sealed class DebugLogger : IAppLogger
    {
        public void Info(string message)
        {
            Debug.WriteLine("[INFO] " + message);
        }

        public void Warn(string message)
        {
            Debug.WriteLine("[WARN] " + message);
        }

        public void Error(string message, Exception exception = null)
        {
            Debug.WriteLine("[ERROR] " + message);
            if (exception != null)
                Debug.WriteLine(exception.ToString());
        }
    }
}