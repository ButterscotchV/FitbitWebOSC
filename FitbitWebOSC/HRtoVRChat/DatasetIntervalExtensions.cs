using Fitbit.Api.Portable.Models;

namespace FitbitWebOSC.HRtoVRChat
{
    public static class DatasetIntervalExtensions
    {
        public static DatasetInterval? GetLatest(this List<DatasetInterval> dataset)
        {
            if (dataset.Count <= 0) return null;

            var latestInterval = dataset[0];
            var latestTime = latestInterval.Time;

            // Skip the first interval, it's the default value
            for (var i = 1; i < dataset.Count; i++)
            {
                var interval = dataset[i];
                if (interval.Time > latestTime)
                {
                    latestInterval = interval;
                }
            }

            return latestInterval;
        }
    }
}
