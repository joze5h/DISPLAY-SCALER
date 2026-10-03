using System;

namespace DISPLAY_SCALER.Domain
{
    public static class FovPreviewCalculator
    {
        public const double BaselineAspect = 16.0 / 9.0;
        public const string BaselineLabel = "16:9";

        public static double GetAspect(uint width, uint height)
        {
            return AspectRatioMath.GetAspect(width, height, BaselineAspect);
        }

        public static double GetHorizontalVisiblePercent(uint width, uint height)
        {
            double custom = GetAspect(width, height);
            if (custom <= 0)
                return 100.0;
            return Math.Min(100.0, custom / BaselineAspect * 100.0);
        }

        public static double GetVerticalVisiblePercent(uint width, uint height)
        {
            double custom = GetAspect(width, height);
            if (custom <= 0)
                return 100.0;
            return Math.Min(100.0, BaselineAspect / custom * 100.0);
        }

        public static int CompareToBaseline(uint width, uint height)
        {
            double diff = GetAspect(width, height) - BaselineAspect;
            if (Math.Abs(diff) < 0.0001)
                return 0;
            return diff < 0 ? -1 : 1;
        }
    }
}