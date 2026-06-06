/*
APP
Copyright (C) 2023  Travis Nickles

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Diagnostics;
using System.Threading;

namespace APP
{
    /// <summary>
    /// Tuning for the HID read → mapping → virtual output path. Prioritizes
    /// consistent low latency over CPU and background-thread efficiency.
    /// </summary>
    public static class InputLatencyPipeline
    {
        public const bool DEFAULT_LOW_LATENCY_INPUT = true;
        public const int DEFAULT_HID_INPUT_BUFFERS = 6;
        public const int LEGACY_HID_INPUT_BUFFERS = 3;
        public const int LATENCY_SAMPLE_INTERVAL = 4;

        /// <summary>App-level low-latency input mode (see LowLatencyInput in app config).</summary>
        public static bool IsEnabled => Global.getLowLatencyInputMode();

        public static int HidInputBufferCount =>
            IsEnabled ? DEFAULT_HID_INPUT_BUFFERS : LEGACY_HID_INPUT_BUFFERS;

        public static ThreadPriority InputThreadPriority =>
            IsEnabled ? ThreadPriority.Highest : ThreadPriority.AboveNormal;

        public static ThreadPriority UsbOutputCopyThreadPriority =>
            IsEnabled ? ThreadPriority.AboveNormal : ThreadPriority.Normal;

        /// <summary>USB rumble/lightbar copy thread is redundant when output is written on the input thread.</summary>
        public static bool InlineUsbOutput => IsEnabled;

        /// <summary>Skip per-frame ManualResetEvent wait after HID read (Set-before-read kept for halt/sync).</summary>
        public static bool SkipPostReadSyncWait => IsEnabled;

        /// <summary>Update rolling latency average every N frames instead of every frame.</summary>
        public static bool ThrottleLatencyAveraging => IsEnabled;

        /// <summary>Cap BT output-report poll nibble to this value when enabled (0 = fastest).</summary>
        public static int BtPollRateCap => 0;

        public static int GetEffectiveBtPollRate(int configuredRate)
        {
            if (!IsEnabled)
            {
                return configuredRate;
            }

            return Math.Min(configuredRate, BtPollRateCap);
        }

        public static void ApplySystemTuning()
        {
            if (!IsEnabled)
            {
                return;
            }

            try
            {
                Process process = Process.GetCurrentProcess();
                ProcessPriorityClass desired = ProcessPriorityClass.High;
                if (process.PriorityClass < desired)
                {
                    process.PriorityClass = desired;
                }
            }
            catch
            {
                // Ignore elevation failures.
            }

            try
            {
                // IO_PRIORITY_HIGH (3) — favor timely HID completion vs default normal (2).
                IntPtr ioPrio = new IntPtr(3);
                Util.NtSetInformationProcess(Process.GetCurrentProcess().Handle,
                    Util.PROCESS_INFORMATION_CLASS.ProcessIoPriority, ref ioPrio, 4);
            }
            catch
            {
            }
        }
    }
}
