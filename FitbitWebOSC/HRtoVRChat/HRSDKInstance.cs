using System.Diagnostics;
using HRtoVRChat_OSC_SDK;

namespace FitbitWebOSC.HRtoVRChat
{
    public class HRSDKInstance : ExternalHRSDK, IDisposable
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
        public override bool IsOpen { get; set; } = true;

        /// <summary>
        /// If there's an active connection to the source
        /// </summary>
        public override bool IsActive { get; set; } = true;

        public Stopwatch Timer = new();

        // TODO Make this value load from a config
        public TimeSpan Interval = TimeSpan.FromSeconds(30);

        public override bool Initialize()
        {
            // TODO Actually return whether this should be used
            Console.WriteLine($"Started the FitBit Web API extension for HRtoVRChat_OSC");
            return true;
        }

        public override void Update()
        {
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
