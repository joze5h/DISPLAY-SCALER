using System;

namespace DISPLAY_SCALER.Domain
{
    public static class RefreshRateMath
    {
        private const uint SignificantFractionalThresholdMilliHz = 1U;

        public static uint NormalizeDisplayMilliHz(uint nominalHz, uint exactMilliHz)
        {
            if (nominalHz == 0)
                return 0;

            uint nominalMilliHz = ToMilliHz(nominalHz);
            if (nominalMilliHz == 0)
                return 0;

            if (exactMilliHz == 0)
                return nominalMilliHz;

            uint roundedNominal = RoundMilliHzToHz(exactMilliHz);
            if (roundedNominal != nominalHz)
                return nominalMilliHz;

            uint diff = AbsDiff(exactMilliHz, nominalMilliHz);
            return diff < SignificantFractionalThresholdMilliHz ? nominalMilliHz : exactMilliHz;
        }

        public static uint ResolveNativeMilliHz(uint nominalHz, params uint[] candidates)
        {
            if (nominalHz == 0)
                return 0;

            uint nominalMilliHz = ToMilliHz(nominalHz);
            if (nominalMilliHz == 0 || candidates == null)
                return nominalMilliHz;

            foreach (uint candidate in candidates)
            {
                if (candidate == 0)
                    continue;

                uint roundedNominal = RoundMilliHzToHz(candidate);
                if (roundedNominal != nominalHz)
                    continue;

                uint diff = AbsDiff(candidate, nominalMilliHz);

                if (diff >= SignificantFractionalThresholdMilliHz)
                    return candidate;
            }

            return nominalMilliHz;
        }

        public static uint ToMilliHz(uint hz)
        {
            return hz <= uint.MaxValue / 1000U ? hz * 1000U : 0;
        }

        public static uint RoundMilliHzToHz(uint milliHz)
        {
            return (uint)Math.Round(milliHz / 1000.0, MidpointRounding.AwayFromZero);
        }

        public static string Format(uint nominalHz, uint milliHz)
        {
            if (milliHz == 0 || (nominalHz > 0 && milliHz == ToMilliHz(nominalHz)))
                return nominalHz + " Гц";

            uint integerPart = milliHz / 1000U;
            uint fraction = milliHz % 1000U;
            return integerPart + "." + fraction.ToString("D3") + " Гц";
        }

        private static uint AbsDiff(uint a, uint b)
        {
            return a > b ? a - b : b - a;
        }
    }
}
