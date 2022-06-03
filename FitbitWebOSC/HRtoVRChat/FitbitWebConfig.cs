using Fitbit.Api.Portable;
using Fitbit.Models;
using Newtonsoft.Json;

namespace FitbitWebOSC.HRtoVRChat
{
    public class FitbitWebConfig
    {
        [JsonProperty(PropertyName = "fitbit_credentials")]
        public FitbitAppCredentials FitbitCredentials { get; set; } = new()
        {
            ClientId = "<OAuth 2.0 Client ID>",
            ClientSecret = "<Client Secret>"
        };

        [JsonProperty(PropertyName = "auth_code")]
        public string AuthCode { get; set; } = "<Auto-Filled>";

        [JsonProperty(PropertyName = "heart_rate_resolution")]
        public HeartRateResolution HeartRateResolution { get; set; } = HeartRateResolution.oneSecond;

        [JsonProperty(PropertyName = "update_interval")]
        public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromSeconds(30.0);

        [JsonProperty(PropertyName = "use_reflection_workaround")]
        public bool UseReflectionWorkaround { get; set; } = true;

        [JsonProperty(PropertyName = "use_utc_timezone_for_requests")]
        public bool UseUtcTimezone { get; set; } = true;
    }
}
