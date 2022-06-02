using System.Diagnostics;
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
        public override bool IsActive { get; set; } = true;

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

        public static FitbitWebConfig CreateDefaultConfig(string file)
        {
            // Create default config
            var config = new FitbitWebConfig();

            // Write the default config to the file
            try
            {
                using var streamWriter = File.CreateText(file);
                JsonSerializer.Serialize(streamWriter, config);
            }
            catch (Exception e)
            {
                // Unable to recover, just return default values
                Console.WriteLine(e);
            }

            return config;
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
                    Console.WriteLine($"Wrote default config to \"{FitbitConfigFile}\", set the values there and start this again");
                    webConfig = CreateDefaultConfig(FitbitConfigFile);
                    return false;
                }

                UpdateInterval = webConfig.UpdateInterval;
                var oAuth2 = new OAuth2Helper(webConfig.FitbitCredentials, "temp");

                var authUrl = oAuth2.GenerateAuthUrl(FitbitScope);
                // Open the auth URL in the default web browser
                Process.Start(new ProcessStartInfo() { FileName = authUrl, UseShellExecute = true });

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
            // TODO Close the web connection (if possible?)
        }
    }
}
