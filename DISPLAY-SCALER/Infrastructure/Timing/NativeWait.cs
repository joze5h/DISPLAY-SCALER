using System.Threading;

namespace DISPLAY_SCALER.Infrastructure.Timing
{
    public static class NativeWait
    {
        public static void Sleep(int milliseconds)
        {
            if (milliseconds <= 0)
                return;

            Thread.Sleep(milliseconds);
        }
    }
}