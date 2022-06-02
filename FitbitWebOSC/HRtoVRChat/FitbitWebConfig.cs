using Fitbit.Api.Portable;
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

        [JsonProperty(PropertyName = "update_interval")]
        public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromSeconds(30.0);
    }
}
