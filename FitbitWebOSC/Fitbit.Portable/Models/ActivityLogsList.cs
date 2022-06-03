using Newtonsoft.Json;

namespace Fitbit.Api.Portable.Models
{
    public class ActivityLogsList
    {
        [JsonProperty(PropertyName = "activities")]
        public List<Activities> Activities { get; set; }


    }
}
