using System.Diagnostics;
using System.Net;
using System.Text;
using Fitbit.Api.Portable;
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

                FitbitWebConfig webConfig;
                if (File.Exists(FitbitConfigFile))
                {
                    webConfig = LoadConfig(FitbitConfigFile) ?? throw new NullReferenceException($"Unable to load config \"{FitbitConfigFile}\"...");
                }
                else
                {
                    WriteConfig(FitbitConfigFile, new FitbitWebConfig());
                    Console.WriteLine($"Wrote default config to \"{FitbitConfigFile}\", set the values there and start this again");
                    return false;
                }

                UpdateInterval = webConfig.UpdateInterval;
                var oAuth2 = new OAuth2Helper(webConfig.FitbitCredentials, OAuthEndpoint);

                OAuth2AccessToken? accessToken;
                if (string.IsNullOrWhiteSpace(webConfig.AuthCode) || !TryExchangeAuthCode(oAuth2, webConfig.AuthCode, out accessToken) || accessToken == null)
                {    
                    var authUrl = oAuth2.GenerateAuthUrl(FitbitScope);
                    var authCode = GetOAuth2Code(authUrl);

                    if (authCode == null)
                    {
                        Console.WriteLine("Authentication failed!");
                        return false;
                    }

                    // Set config auth code
                    webConfig.AuthCode = authCode;
                    WriteConfig(FitbitConfigFile, webConfig);

                    if (!TryExchangeAuthCode(oAuth2, webConfig.AuthCode, out accessToken) || accessToken == null)
                    {
                        throw new Exception("Unable to acquire an access token!");
                    }
                }

                FitbitClient = new(webConfig.FitbitCredentials, accessToken);

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return false;
        }

        public override void Update()
        {
            if (FitbitClient == null)
            {
                return;
            }

            if (!Timer.IsRunning || Timer.Elapsed > UpdateInterval)
            {
                // Start/Restart the timer
                Timer.Restart();

                // TODO Request from the web API
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
