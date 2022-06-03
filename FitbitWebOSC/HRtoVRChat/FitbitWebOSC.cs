using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using Fitbit.Api.Portable;
using Fitbit.Api.Portable.Models;
using Fitbit.Api.Portable.OAuth2;
using Fitbit.Models;
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
        public override string SDKName { get => "fitbitweb"; set => throw new InvalidOperationException("The SDK name can not be modified"); }

        /// <summary>
        /// The current HeartRate
        /// </summary>
        public override int HR { get; set; } = 80;

        /// <summary>
        /// If the device transmitting data to the source is connected.
        /// If your service does not support this, then you can point it to IsActive
        /// </summary>
        public override bool IsOpen { get => FitbitClient != null; set => throw new InvalidOperationException($"The {nameof(IsOpen)} property can not be set"); }

        /// <summary>
        /// If there's an active connection to the source
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

        public static string? GetOAuth2Code(string authUrl)
        {
            var httpListener = new HttpListener();
            try
            {
                httpListener.Prefixes.Add(OAuthEndpoint);
                httpListener.Start();
                var oAuthResponse = httpListener.GetContextAsync();

                // Open the auth URL in the default web browser
                Process.Start(new ProcessStartInfo() { FileName = authUrl, UseShellExecute = true });

                
                var responseContext = oAuthResponse.GetAwaiter().GetResult();

                var oauthResponse = responseContext.Request;
                var response = responseContext.Response;

                try
                {
                    byte[] responseBytes = Encoding.UTF8.GetBytes(OAuthResponse);

                    // Get a response stream and write the response to it
                    response.ContentLength64 = responseBytes.Length;
                    response.OutputStream.Write(responseBytes, 0, responseBytes.Length);

                    return oauthResponse.QueryString["code"];
                }
                finally
                {
                    response.Close();
                }
            }
            finally
            {
                try
                {
                    httpListener.Stop();
                }
                catch
                {
                    // Ignore
                }
            }
        }

        public bool TryExchangeAuthCode(OAuth2Helper oAuth2, string authCode, out OAuth2AccessToken? outToken)
        {
            try
            {
                outToken = oAuth2.ExchangeAuthCodeForAccessTokenAsync(authCode).GetAwaiter().GetResult();
                return true;
            }
            catch
            {
                // Don't print any exceptions
            }

            outToken = null;
            return false;
        }

        public void SetThroughReflection(int hr, bool isActive, bool isOpen)
        {
            var programType = Type.GetType("HRtoVRChat_OSC.Program, HRtoVRChat_OSC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            if (programType == null) throw new InvalidOperationException("Unable to find \"HRtoVRChat_OSC.Program\"");

            var activeHRManagerField = programType.GetField("activeHRManager", BindingFlags.NonPublic | BindingFlags.Static);
            if (activeHRManagerField == null) throw new InvalidOperationException("Unable to find \"HRtoVRChat_OSC.Program.activeHRManager\"");

            var activeHRManagerValue = activeHRManagerField.GetValue(null);
            if (activeHRManagerValue == null) throw new InvalidOperationException("Unable to get the value of \"HRtoVRChat_OSC.Program.activeHRManager\"");

            var activeHRManagerType = activeHRManagerValue.GetType();
            if (activeHRManagerType == null) throw new InvalidOperationException("Unable to get the type of the value of \"HRtoVRChat_OSC.Program.activeHRManager\"");

            var hrField = activeHRManagerType.GetField("HR", BindingFlags.NonPublic | BindingFlags.Instance);
            if (hrField == null) throw new InvalidOperationException("Unable to find \"HRtoVRChat_OSC.HRManagers.*.HR\"");
            var isActiveField = activeHRManagerType.GetField("isActive", BindingFlags.NonPublic | BindingFlags.Instance);
            if (isActiveField == null) throw new InvalidOperationException("Unable to find \"HRtoVRChat_OSC.HRManagers.*.isActive\"");
            var isOpenField = activeHRManagerType.GetField("isOpen", BindingFlags.NonPublic | BindingFlags.Instance);
            if (isOpenField == null) throw new InvalidOperationException("Unable to find \"HRtoVRChat_OSC.HRManagers.*.isOpen\"");

            hrField.SetValue(activeHRManagerValue, hr);
            isActiveField.SetValue(activeHRManagerValue, isActive);
            isOpenField.SetValue(activeHRManagerValue, isOpen);
        }

        public override bool Initialize()
        {
            Console.WriteLine($"Starting the FitBit Web API extension for HRtoVRChat_OSC...");

            try
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

                UpdateInterval = FitbitWebConfig.UpdateInterval;
                var oAuth2 = new OAuth2Helper(FitbitWebConfig.FitbitCredentials, OAuthEndpoint);

                OAuth2AccessToken? accessToken;
                if (string.IsNullOrWhiteSpace(FitbitWebConfig.AuthCode) || !TryExchangeAuthCode(oAuth2, FitbitWebConfig.AuthCode, out accessToken) || accessToken == null)
                {    
                    var authUrl = oAuth2.GenerateAuthUrl(FitbitScope);
                    var authCode = GetOAuth2Code(authUrl);

                    if (authCode == null)
                    {
                        Console.WriteLine("Authentication failed!");
                        return false;
                    }

                    // Set config auth code
                    FitbitWebConfig.AuthCode = authCode;
                    WriteConfig(FitbitConfigFile, FitbitWebConfig);

                    if (!TryExchangeAuthCode(oAuth2, FitbitWebConfig.AuthCode, out accessToken) || accessToken == null)
                    {
                        throw new Exception("Unable to acquire an access token!");
                    }
                }

                FitbitClient = new(FitbitWebConfig.FitbitCredentials, accessToken);
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

        public DatasetInterval? GetLatestDatasetInterval(List<DatasetInterval> dataset)
        {
            DateTime latestTime = DateTime.MinValue;
            DatasetInterval? latestInterval = null;

            foreach (var interval in dataset)
            {
                if (interval.Time > latestTime)
                {
                    latestInterval = interval;
                }
            }

            return latestInterval;
        }

        public override void Update()
        {
            if (FitbitClient == null)
            {
                return;
            }

            if (!Timer.IsRunning || Timer.Elapsed > UpdateInterval)
            {
                Console.WriteLine($"Fetching updated heartrate...");

                // Start/Restart the timer
                Timer.Restart();

                try
                {
                    var heartRate = FitbitClient.GetHeartRateIntradayV1(DateTime.UtcNow, HeartRateResolution.oneSecond).GetAwaiter().GetResult();
                    var latestInterval = GetLatestDatasetInterval(heartRate.Dataset);

                    if (latestInterval != null)
                    {
                        HR = latestInterval.Value;

                        if (FitbitWebConfig.UseReflectionWorkaround)
                        {
                            try
                            {
                                SetThroughReflection(HR, IsActive, IsOpen);
                                Console.WriteLine($"Updated heartrate with value {latestInterval.Value} from {latestInterval.Time} using the reflection workaround");
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"Failed to update heartrate with value {latestInterval.Value} from {latestInterval.Time} using the reflection workaround:\n{e}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Updated heartrate with value {latestInterval.Value} from {latestInterval.Time}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Failed to update heartrate, no values reported");
                    }
                }
                catch (FitbitRequestException e)
                {
                    Console.WriteLine($"{e}\n{string.Join('\n', e.ApiErrors.Select(x => $"{x.ErrorType}: {x.Message}"))}");
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
            FitbitClient?.HttpClient?.Dispose();
            FitbitClient = null;
        }
    }
}
