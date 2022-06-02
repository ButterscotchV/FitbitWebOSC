using System.Diagnostics;
using Fitbit.Api.Portable;
using Fitbit.Api.Portable.OAuth2;
using HRtoVRChat_OSC_SDK;

namespace FitbitWebOSC.HRtoVRChat
{
    public class FitbitWebOSC : ExternalHRSDK, IDisposable
    {
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
        public TimeSpan Interval = TimeSpan.FromSeconds(30);

        public FitbitClient? FitbitClient;

        public static readonly string[] FitBitScope = new[]
        {
            "heartrate"
        };

        public override bool Initialize()
        {
            // TODO Actually return whether this should be used
            Console.WriteLine($"Starting the FitBit Web API extension for HRtoVRChat_OSC");

            var oAuth2 = new OAuth2Helper(new FitbitAppCredentials()
            {
                ClientId = "temp",
                ClientSecret = "temp"
            }, "temp");

            var authUrl = oAuth2.GenerateAuthUrl(FitBitScope);
            // Open the auth URL in the default web browser
            Process.Start(authUrl);

            return true;
        }

        public override void Update()
        {
            if (FitbitClient == null)
            {
                return;
            }

            if (!Timer.IsRunning || Timer.Elapsed > Interval)
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
