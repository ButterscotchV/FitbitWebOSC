namespace Fitbit.Models
{
    public class ActiveZoneMinutesIntraday
    {
        public DateTime DateTime { get; set; }
        public List<ActiveZoneMinutesIntradayData> Minutes { get; set; }
    }
}
