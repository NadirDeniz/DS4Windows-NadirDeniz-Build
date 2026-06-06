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
using System.Runtime.CompilerServices;

namespace APP
{
    /// <summary>
    /// Optional 16-bit-equivalent internal stick path. Requires <see cref="Global.getHighPrecisionStickOutput"/>.
    /// Lifts 8-bit HID samples onto a 16-bit grid early; avoids byte round-trips until XInput finalize.
    /// </summary>
    internal static class True16BitStickPipeline
    {
        /// <summary>Maps one 8-bit axis step to ~16-bit spacing (257 ≈ 65535/255).</summary>
        public const double BYTE_TO_INT16_SCALE = 257.0;
        public const double INT16_MAX = 32767.0;

        public static bool IsActiveForDevice(int device)
        {
            return Global.getTrue16BitStickPipeline(device) &&
                Global.getHighPrecisionStickOutput(device);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BytesToInternal16Normalized(
            byte stickX, byte stickY, out double normX, out double normY)
        {
            double dx = (stickX - 128.0) * BYTE_TO_INT16_SCALE;
            double dy = -((stickY - 128.0) * BYTE_TO_INT16_SCALE);
            normX = dx / INT16_MAX;
            normY = dy / INT16_MAX;
            normX = Math.Clamp(normX, -1.0, 1.0);
            normY = Math.Clamp(normY, -1.0, 1.0);
        }

        public static void BeginStickFromCalibratedBytes(
            int device, ref DS4State dState, int stickId, byte stickX, byte stickY)
        {
            BytesToInternal16Normalized(stickX, stickY, out double normX, out double normY);
            HighPrecisionStickPipeline.SetStickNormalized(dState, stickId, normX, normY);
            dState.UseTrue16BitStickPipeline = true;
        }

        public static void ProcessRadialDeadzone(
            int device, int stickId,
            byte inX, byte inY,
            StickDeadZoneInfo mod,
            double deltaTimeSeconds,
            ref DS4State dState)
        {
            BytesToInternal16Normalized(inX, inY, out double normX, out double normY);

            bool skipClassicAntiDead =
                SoftAntiDeadzoneProcessor.ShouldSkipClassicAntiDead(device, stickId);

            if (mod.advancedRadialProcessing)
            {
                AdvancedDeadzoneProcessor.ProcessRadialFromNormalized(
                    device, stickId, ref normX, ref normY, mod, deltaTimeSeconds,
                    skipClassicAntiDead);
            }
            else
            {
                StickInputProcessor.ProcessRadialDeadzoneOnNormalized(
                    mod, ref normX, ref normY, skipClassicAntiDead);
            }

            HighPrecisionStickPipeline.SetStickNormalized(dState, stickId, normX, normY);
            dState.UseTrue16BitStickPipeline = true;
        }

        public static void ProcessAxialDeadzone(
            int device, int stickId,
            byte inX, byte inY,
            StickDeadZoneInfo mod,
            double deltaTimeSeconds,
            ref DS4State dState)
        {
            StickInputProcessor.ProcessAxialDeadzoneFloat(
                device, stickId, inX, inY, mod, deltaTimeSeconds,
                out double normX, out double normY);
            HighPrecisionStickPipeline.SetStickNormalized(dState, stickId, normX, normY);
            dState.UseTrue16BitStickPipeline = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldDeferByteSync(DS4State dState)
        {
            return dState.UseTrue16BitStickPipeline;
        }

        public static void SyncBytesForBindingsIfDeferred(
            DS4State dState, int stickId, double normX, double normY)
        {
            if (!dState.UseTrue16BitStickPipeline)
            {
                return;
            }

            HighPrecisionStickPipeline.SyncBytesFromNormalized(
                normX, normY, out byte outX, out byte outY);

            if (stickId == 0)
            {
                dState.LX = outX;
                dState.LY = outY;
            }
            else
            {
                dState.RX = outX;
                dState.RY = outY;
            }
        }
    }
}
