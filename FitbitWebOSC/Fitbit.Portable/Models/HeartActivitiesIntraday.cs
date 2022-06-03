using Fitbit.Api.Portable.Models;

namespace Fitbit.Models
{
    public class HeartActivitiesIntraday
    {
        public IntradayActivitiesHeart ActivitiesHeart { get; set; }
        public List<DatasetInterval> Dataset { get; set; }
        public int DatasetInterval { get; set; }
        public string DatasetType { get; set; }
    }
}
