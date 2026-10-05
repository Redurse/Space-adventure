using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Anabiosis.Client;

// Keeps Windows from quietly slowing the game down. On laptops with hybrid CPUs (Intel P/E cores)
// the scheduler may park the render thread on a slow efficiency core, or mark the process as
// "efficiency mode" (EcoQoS), and the very same frame then costs 3-4 times more. Measured here: the
// same visibility-mask work took 10 ms in one run and 40 ms in the next. A game should always ask for
// full speed. Everything is best effort - a refusal is ignored, the game just runs as before.
internal static class ProcessPerformance
{
    private const int ProcessPowerThrottling = 4;
    private const uint ProcessPowerThrottlingCurrentVersion = 1;
    private const uint ProcessPowerThrottlingExecutionSpeed = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessPowerThrottlingState
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(IntPtr process, int informationClass,
        ref ProcessPowerThrottlingState information, int size);

    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint milliseconds);

    public static void Apply()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            process.PriorityClass = ProcessPriorityClass.High;

            // Control bit set, state bit clear = "never throttle execution speed for this process".
            var state = new ProcessPowerThrottlingState
            {
                Version = ProcessPowerThrottlingCurrentVersion,
                ControlMask = ProcessPowerThrottlingExecutionSpeed,
                StateMask = 0,
            };
            SetProcessInformation(process.Handle, ProcessPowerThrottling, ref state, Marshal.SizeOf<ProcessPowerThrottlingState>());

            // The default 15.6 ms timer tick makes frame pacing and Thread.Sleep coarse.
            timeBeginPeriod(1);
        }
        catch (Exception)
        {
            // Not worth failing a launch over.
        }
    }
}
