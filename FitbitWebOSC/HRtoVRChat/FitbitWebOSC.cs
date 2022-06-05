using System.Diagnostics;
using Fitbit.Api.Portable;
using Fitbit.Api.Portable.Models;
using Fitbit.Api.Portable.OAuth2;
using HRtoVRChat_OSC_SDK;
using Newtonsoft.Json;

namespace FitbitWebOSC.HRtoVRChat
{
    public class FitbitWebOSC : ExternalHRSDK, IDisposable
    {
        public static readonly string FitbitConfigFolder = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FitbitWebOSC"));
        public static readonly string FitbitConfigFile = Path.GetFullPath(Path.Combine(FitbitConfigFolder, "fitbit_web_config.json"));

        public static readonly string OAuthEndpoint = "http://localhost:8080/";
        public static readonly string OAuthResponse = "<html><body>FitbitWebOSC has successfully been connected! You can close this page.</body><script type=\"text/javascript\">self.close();</script></html>";

        public static readonly string[] FitbitScope = new[]
        {
            "heartrate"
        };

        /// <summary>
        /// The name of your SDK
        /// </summary>
        public override string SDKName { get; set; } = "fitbitweb";

        /// <summary>
        /// If the device transmitting data to the source is connected.
        /// If your service does not support this, then you can point it to IsActive
        /// </summary>
        public bool IsOpen => FitbitClient != null;

        /// <summary>
        /// If there's an active connection to the source
        /// </summary>
        public bool IsActive { get; set; } = false;

        public Messages.HRMessage CachedMessage = new();

        public Stopwatch Timer = new();

        // TODO Make this value load from a config
        public TimeSpan UpdateInterval = TimeSpan.FromSeconds(30.0);

        public FitbitWebConfig FitbitWebConfig = new();
        public FitbitClient? FitbitClient;

        private static readonly JsonSerializer JsonSerializer = new()
        {
            Formatting = Formatting.Indented
        };

        public static FitbitWebConfig? LoadConfig(string file)
        {
            using var streamReader = File.OpenText(file);
            using var reader = new JsonTextReader(streamReader);
            return JsonSerializer.Deserialize<FitbitWebConfig>(reader);
        }

        public static void WriteConfig(string file, FitbitWebConfig config)
        {
            using var streamWriter = File.CreateText(file);
            JsonSerializer.Serialize(streamWriter, config);
        }

        public bool InitializeConfig()
        {
            // Make the config folder if needed
            if (!Directory.Exists(FitbitConfigFolder))
            {
                Directory.CreateDirectory(FitbitConfigFolder);
            }

            if (File.Exists(FitbitConfigFile))
            {
                FitbitWebConfig = LoadConfig(FitbitConfigFile) ?? throw new NullReferenceException($"Unable to load config \"{FitbitConfigFile}\"...");

                try
                {
                    // Write missing default configs and remove invalid configs
                    WriteConfig(FitbitConfigFile, FitbitWebConfig);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to re-write config:\n{e}");
                }
            }
            else
            {
                WriteConfig(FitbitConfigFile, FitbitWebConfig);
                Console.WriteLine($"Wrote default config to \"{FitbitConfigFile}\", set the values there and start this again");
                return false;
            }

            return true;
        }

        public bool InitializeFitbitClient()
        {
            var oAuth2 = new OAuth2Helper(FitbitWebConfig.FitbitCredentials, OAuthEndpoint);

            OAuth2AccessToken? accessToken;
            if (string.IsNullOrWhiteSpace(FitbitWebConfig.AuthCode) || !OAuth2Utils.TryExchangeAuthCodeForAccessToken(oAuth2, FitbitWebConfig.AuthCode, out accessToken) || accessToken == null)
            {
                var authUrl = oAuth2.GenerateAuthUrl(FitbitScope);
                var authCode = OAuth2Utils.GetOAuth2CodeFromUrl(authUrl);

                if (authCode == null)
                {
                    Console.WriteLine("Authentication failed!");
                    return false;
                }

                // Set config auth code
                FitbitWebConfig.AuthCode = authCode;
                WriteConfig(FitbitConfigFile, FitbitWebConfig);

                if (!OAuth2Utils.TryExchangeAuthCodeForAccessToken(oAuth2, FitbitWebConfig.AuthCode, out accessToken) || accessToken == null)
                {
                    throw new Exception("Unable to acquire an access token!");
                }
            }

            FitbitClient = new(FitbitWebConfig.FitbitCredentials, accessToken);
            return true;
        }

        public override bool Initialize()
        {
            Console.WriteLine($"Starting the FitBit Web API extension for HRtoVRChat_OSC...");

            try
            {
                if (!InitializeConfig()) return false;

                UpdateInterval = FitbitWebConfig.UpdateInterval;

                if (!InitializeFitbitClient()) return false;
                IsActive = true;

                Console.WriteLine("Successfully connected to the FitBit Web API!");

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return false;
        }

        public void SendHeartrate(int heartrate)
        {
            SendMessage(heartrate, IsOpen, IsActive);
        }

        public void SendMessage(int heartrate, bool isOpen, bool isActive)
        {
            // Reuse the message
            CachedMessage.HR = heartrate;
            CachedMessage.IsOpen = isOpen;
            CachedMessage.IsActive = isActive;

            // Send the reused message
            CurrentHRData = CachedMessage;
        }

        public override void Update()
        {
            if (FitbitClient == null)
            {
                return;
            }

            if (!Timer.IsRunning || Timer.Elapsed > UpdateInterval)
            {
                Console.WriteLine("Fetching updated heartrate...");

                // Start/Restart the timer
                Timer.Restart();

                try
                {
                    var heartRate = FitbitClient.GetHeartRateIntradayV1(FitbitWebConfig.UseUtcTimezone ? DateTime.UtcNow : DateTime.Now, FitbitWebConfig.HeartRateResolution, useUtcTimezone: FitbitWebConfig.UseUtcTimezone).GetAwaiter().GetResult();
                    var latestInterval = heartRate.Dataset.GetLatest();

                    if (latestInterval != null)
                    {
                        // This connection is active, there is data available
                        IsActive = true;

                        // Send the retrieved heartrate value
                        var hrValue = latestInterval.Value;
                        SendHeartrate(hrValue);

                        // Convert to local time
                        var hrTime = FitbitWebConfig.UseUtcTimezone ? latestInterval.Time.ToLocalTime() : latestInterval.Time;
                        Console.WriteLine($"Updated heartrate with value {hrValue} from {hrTime}");
                    }
                    else
                    {
                        // This connection is inactive, there is no data available
                        IsActive = false;

                        // Send the updated IsActive value
                        SendHeartrate(0);

                        Console.WriteLine("Failed to update heartrate, no values reported");
                    }
                }
                catch (FitbitRequestException e)
                {
                    Console.WriteLine($"{e}\n{string.Join('\n', e.ApiErrors.Select(x => $"{x.ErrorType}: {x.Message}"))}");
                }
                catch (Exception e) when (e is TimeoutException || e.InnerException is TimeoutException)
                {
                    Console.WriteLine($"Connection timed out, try checking if the Fitbit Web API is down or if your network connection is working as expected\n{e}");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        public override void Destroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            FitbitClient?.HttpClient?.Dispose();
            FitbitClient = null;
        }
    }
}
