using System;

namespace DISPLAY_SCALER.Infrastructure.Diagnostics
{
    public interface IAppLogger
    {
        void Info(string message);

        void Warn(string message);

        void Error(string message, Exception exception = null);
    }
}