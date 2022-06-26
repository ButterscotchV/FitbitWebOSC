using System.Diagnostics;
using Fitbit.Api.Portable;
using Fitbit.Api.Portable.OAuth2;
using HRtoVRChat_OSC_SDK;
using Newtonsoft.Json;

namespace FitbitWebOSC.HRtoVRChat
{
    public class FitbitWebOSC : HRSDK, IDisposable
    {
        public static readonly string FitbitConfigFolder = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FitbitWebOSC"));
        public static readonly string FitbitConfigFile = Path.GetFullPath(Path.Combine(FitbitConfigFolder, "fitbit_web_config.json"));

        public static readonly string OAuthEndpoint = "http://localhost:8080/";
        public static readonly string OAuthResponse = "<html><body>FitbitWebOSC has successfully been connected! You can close this page.</body><script type=\"text/javascript\">self.close();</script></html>";

        public static readonly string[] FitbitScope = new[]
        {
            "heartrate"
        };

        public override HRSDKOptions Options { get; } = new HRSDKOptions("FitbitWeb");

        public override int HR { get; set; } = 0;

        /// <summary>
        /// True if the connection to the Fitbit web API is open
        /// </summary>
        public override bool IsOpen { get; set; } = false;

        /// <summary>
        /// True if the device connection is active
        /// </summary>
        public override bool IsActive { get; set; } = false;

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
                    Log(LogLevel.Error, $"Failed to re-write config:\n{e}");
                }
            }
            else
            {
                WriteConfig(FitbitConfigFile, FitbitWebConfig);
                Log(LogLevel.Warn, $"Wrote default config to \"{FitbitConfigFile}\", set the values there and start this again");
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
                    Log(LogLevel.Error, "Authentication failed!");
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

        public override void OnSDKOpened()
        {
            Log(LogLevel.Log, $"Starting the Fitbit Web API extension for HRtoVRChat_OSC...");

            try
            {
                if (!InitializeConfig()) return;

                UpdateInterval = FitbitWebConfig.UpdateInterval;

                if (!InitializeFitbitClient()) return;
                IsOpen = true;
                IsActive = true;

                Log(LogLevel.Log, "Successfully connected to the FitBit Web API!");

                return;
            }
            catch (Exception e)
            {
                Log(LogLevel.Error, "An error ocurred while initializing the Fitbit Web API client", e: e);
            }

            return;
        }

        public void SendData()
        {
            // Push the current data to the server
            PushData();
        }

        public void SendData(int heartrate, bool isOpen, bool isActive)
        {
            // Update the current data
            HR = heartrate;
            IsOpen = isOpen;
            IsActive = isActive;

            // Send the updated data to the server
            SendData();
        }

        public void SendData(int heartrate, bool isActive = true)
        {
            SendData(heartrate, IsOpen, isActive);
        }

        public override void OnSDKUpdate()
        {
            if (FitbitClient == null)
            {
                return;
            }

            if (!Timer.IsRunning || Timer.Elapsed > UpdateInterval)
            {
                Log(LogLevel.Log, "Fetching updated heartrate...");

                // Start/Restart the timer
                Timer.Restart();

                try
                {
                    var heartRate = FitbitClient.GetHeartRateIntradayV1(FitbitWebConfig.UseUtcTimezone ? DateTime.UtcNow : DateTime.Now, FitbitWebConfig.HeartRateResolution, useUtcTimezone: FitbitWebConfig.UseUtcTimezone).GetAwaiter().GetResult();
                    var latestInterval = heartRate.Dataset.GetLatest();

                    if (latestInterval != null)
                    {
                        // Send the retrieved heartrate value and mark as active
                        var hrValue = latestInterval.Value;
                        SendData(hrValue, true);

                        // Convert to local time for logging
                        var hrTime = FitbitWebConfig.UseUtcTimezone ? latestInterval.Time.ToLocalTime() : latestInterval.Time;
                        Log(LogLevel.Log, $"Updated heartrate with value {hrValue} from {hrTime}");
                    }
                    else
                    {
                        // Mark as inactive
                        SendData(0, false);

                        Log(LogLevel.Warn, "Failed to update heartrate, no values reported");
                    }
                }
                catch (FitbitRequestException e)
                {
                    Log(LogLevel.Error, $"{e}\n{string.Join('\n', e.ApiErrors.Select(x => $"{x.ErrorType}: {x.Message}"))}", e: e);
                }
                catch (Exception e) when (e is TimeoutException || e.InnerException is TimeoutException)
                {
                    Log(LogLevel.Error, $"Connection timed out, try checking if the Fitbit Web API is down or if your network connection is working as expected\n{e}", e: e);
                }
                catch (Exception e)
                {
                    Log(LogLevel.Error, "An error ocurred while updating the heartrate", e: e);
                }
            }
        }

        public void CloseSDK()
        {
            if (IsReflected)
                Close();
            else
            {
                IsOpen = false;
                IsActive = false;
            }
        }

        public override void OnSDKClosed()
        {
            // Make sure everything is default
            HR = 0;
            IsOpen = false;
            IsActive = false;
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
