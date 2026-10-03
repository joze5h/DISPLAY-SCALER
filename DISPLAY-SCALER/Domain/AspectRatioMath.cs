using System;

namespace DISPLAY_SCALER.Domain
{
    public static class AspectRatioMath
    {
        private const double StandardAspectTolerance = 0.01;
        private const double UltrawideAspectTolerance = 0.03;

        private struct StandardAspect
        {
            public readonly uint Width;
            public readonly uint Height;
            public readonly string Label;
            public readonly double Tolerance;

            public StandardAspect(uint width, uint height, string label, double tolerance)
            {
                Width = width;
                Height = height;
                Label = label;
                Tolerance = tolerance;
            }
        }

        private static readonly StandardAspect[] StandardAspects =
        {
            new StandardAspect(1, 1, "1:1", StandardAspectTolerance),
            new StandardAspect(5, 4, "5:4", StandardAspectTolerance),
            new StandardAspect(4, 3, "4:3", StandardAspectTolerance),
            new StandardAspect(3, 2, "3:2", StandardAspectTolerance),
            new StandardAspect(16, 10, "16:10", StandardAspectTolerance),
            new StandardAspect(16, 9, "16:9", StandardAspectTolerance),
            new StandardAspect(21, 9, "21:9", UltrawideAspectTolerance),
            new StandardAspect(32, 9, "32:9", UltrawideAspectTolerance)
        };

        public static int Gcd(int a, int b)
        {
            a = Math.Abs(a);
            b = Math.Abs(b);
            while (b != 0)
            {
                int t = a % b;
                a = b;
                b = t;
            }
            return a == 0 ? 1 : a;
        }

        public static string Format(uint width, uint height)
        {
            if (width == 0 || height == 0)
                return "-";

            string canonicalLabel = FindCanonicalLabel(width, height);
            if (canonicalLabel != null)
                return canonicalLabel;

            int gcd = Gcd((int)width, (int)height);
            return (width / gcd) + ":" + (height / gcd);
        }

        private static string FindCanonicalLabel(uint width, uint height)
        {
            double aspect = (double)width / height;
            StandardAspect bestMatch = new StandardAspect();
            double bestError = double.MaxValue;

            for (int i = 0; i < StandardAspects.Length; i++)
            {
                StandardAspect candidate = StandardAspects[i];
                double standardAspect = (double)candidate.Width / candidate.Height;
                double relativeError = Math.Abs(aspect - standardAspect) / standardAspect;
                if (relativeError <= candidate.Tolerance && relativeError < bestError)
                {
                    bestMatch = candidate;
                    bestError = relativeError;
                }
            }

            return bestError == double.MaxValue ? null : bestMatch.Label;
        }

        public static double GetAspect(uint width, uint height, double fallback = 16.0 / 9.0)
        {
            if (width == 0 || height == 0)
                return fallback;
            return (double)width / height;
        }
    }
}
