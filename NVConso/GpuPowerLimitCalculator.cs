namespace NVConso
{
    public static class GpuPowerLimitCalculator
    {
        public static uint GetPowerLimit(
            GpuPowerMode mode,
            uint minimumPowerLimit,
            uint defaultPowerLimit,
            uint maximumPowerLimit)
        {
            uint stockLimit = Clamp(defaultPowerLimit, minimumPowerLimit, maximumPowerLimit);

            return mode switch
            {
                GpuPowerMode.Canicule => Clamp(minimumPowerLimit, minimumPowerLimit, maximumPowerLimit),
                GpuPowerMode.VideoSurf => GetPercentageLimit(
                    minimumPowerLimit,
                    stockLimit,
                    maximumPowerLimit,
                    Constants.VideoSurfPercentage),
                GpuPowerMode.Indie2D => GetPercentageLimit(
                    minimumPowerLimit,
                    stockLimit,
                    maximumPowerLimit,
                    Constants.Indie2DPercentage),
                GpuPowerMode.Stock => stockLimit,
                GpuPowerMode.Max => Clamp(maximumPowerLimit, minimumPowerLimit, maximumPowerLimit),
                _ => stockLimit
            };
        }

        public static uint ResolveDefaultPowerLimit(
            uint minimumPowerLimit,
            uint maximumPowerLimit,
            uint? defaultPowerLimit,
            uint? currentPowerLimit)
        {
            uint fallbackLimit = defaultPowerLimit
                ?? currentPowerLimit
                ?? maximumPowerLimit;

            return Clamp(fallbackLimit, minimumPowerLimit, maximumPowerLimit);
        }

        private static uint GetPercentageLimit(
            uint minimumPowerLimit,
            uint stockLimit,
            uint maximumPowerLimit,
            int percentage)
        {
            uint range = stockLimit - minimumPowerLimit;
            uint target = minimumPowerLimit + range * (uint)percentage / 100;
            return Clamp(target, minimumPowerLimit, maximumPowerLimit);
        }

        private static uint Clamp(uint value, uint minimumPowerLimit, uint maximumPowerLimit)
        {
            return Math.Clamp(value, minimumPowerLimit, maximumPowerLimit);
        }
    }
}
