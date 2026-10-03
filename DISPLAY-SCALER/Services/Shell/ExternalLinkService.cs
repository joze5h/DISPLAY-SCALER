using System;
using System.Diagnostics;

namespace DISPLAY_SCALER.Services.Shell
{
    public sealed class ExternalLinkService : IExternalLinkService
    {
        public void Open(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL is empty.", nameof(url));

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
    }
}