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
    /// Soft radial anti-deadzone compensation: ramps output toward a game-deadzone
    /// threshold without the instant jump of classic anti-deadzone.
    /// Runs after physical deadzone removal, before output curves.
    /// </summary>
    internal static class SoftAntiDeadzoneProcessor
    {
        public static bool ShouldSkipClassicAntiDead(int device, int stickId)
        {
            if (stickId != 1)
            {
                return false;
            }

            return Global.GetRSSoftAntiDeadzoneInfo(device).enabled ||
                InGameDeadzoneRemapperProcessor.ShouldSkipClassicAntiDead(device, stickId);
        }

        /// <summary>
        /// Apply soft compensation on normalized stick vector; preserves angle.
        /// </summary>
        public static void ApplyRadial(
            ref double normX, ref double normY,
            SoftAntiDeadzoneInfo info)
        {
            if (!info.enabled)
            {
                return;
            }

            double mag = Math.Sqrt((normX * normX) + (normY * normY));
            if (mag <= 1e-9)
            {
                normX = normY = 0.0;
                return;
            }

            double inputT = Math.Clamp(mag, 0.0, 1.0);
            double outputT = ComputeSoftOutputMagnitude(inputT, info);
            outputT = Math.Clamp(outputT, 0.0, 1.0);

            double scale = outputT / mag;
            normX *= scale;
            normY *= scale;
        }

        public static void ApplyToState(
            int device, int stickId,
            StickDeadZoneInfo mod,
            SoftAntiDeadzoneInfo info,
            ref DS4State dState)
        {
            if (!info.enabled ||
                stickId != 1 ||
                mod.deadzoneType != StickDeadZoneInfo.DeadZoneType.Radial ||
                InGameDeadzoneRemapperProcessor.ShouldSkipClassicAntiDead(device, stickId))
            {
                return;
            }

            double normX;
            double normY;
            if (dState.HasFloatNormStick)
            {
                HighPrecisionStickPipeline.GetStickNormalized(
                    dState, stickId, out normX, out normY);
            }
            else
            {
                byte stickX = dState.RX;
                byte stickY = dState.RY;
                if (True16BitStickPipeline.IsActiveForDevice(device))
                {
                    True16BitStickPipeline.BytesToInternal16Normalized(
                        stickX, stickY, out normX, out normY);
                }
                else
                {
                    StickInputProcessor.BytesToNormalized(stickX, stickY, out normX, out normY);
                }
            }

            ApplyRadial(ref normX, ref normY, info);

            if (dState.HasFloatNormStick ||
                True16BitStickPipeline.IsActiveForDevice(device))
            {
                HighPrecisionStickPipeline.SetStickNormalized(dState, stickId, normX, normY);
            }

            if (!True16BitStickPipeline.ShouldDeferByteSync(dState))
            {
                HighPrecisionStickPipeline.SyncBytesFromNormalized(
                    dState, stickId, normX, normY, out byte outX, out byte outY);
                dState.RX = outX;
                dState.RY = outY;
            }
        }

        /// <summary>
        /// Soft alternative to classic radial anti-deadzone. Classic anti-dead is
        ///     classic(input) = target + (1 - target) * input
        /// which jumps instantly to <c>target</c> at the smallest input. This version eases that
        /// floor in over a ramp zone with a smoothstep soft-knee so center exit feels velvety:
        ///   target   = compensationSize / 100        (anti-dead floor, e.g. 0.30 for size 30)
        ///   t        = input / rampZone              (0..1 inside the ramp zone)
        ///   shaped   = t ^ exponent                  (exponent positions the knee)
        ///   w(shaped)= shaped^2 * (3 - 2*shaped)     (smoothstep: w'(0)=w'(1)=0)
        ///   input <  rampZone: out = lerp(input, classic(input), w)  = input + w*target*(1-input)
        ///   input >= rampZone: out = classic(input)
        /// smoothstep is used in preference to smootherstep: both share the endpoint properties below,
        /// but smoothstep has a lower maximum derivative (1.5 vs 1.875), which lowers the curve's peak
        /// local slope at the knee. Since the physical stick is only 8-bit, a lower peak slope means
        /// each input code maps to a smaller output jump in the steepest region -> less in-game
        /// pixel-skipping / stair-stepping during micro aim, with no loss of the anti-dead effect.
        /// Why blend toward the classic line (instead of target*w + (1-target)*input): the lerp form
        /// keeps out >= input everywhere, so the curve is always a genuine lift and never dips below
        /// the raw signal near center (which target*w would do for high target / wide ramp).
        /// Properties: out(0)=0 (true center preserved, no drift), out(1)=1, strictly monotonic
        /// (derivative >= 1 - target > 0), slope at center = 1 (no jump / no "tak"), and C1 at the
        /// rampZone handoff (both sides have slope 1 - target). Stateless: output depends only on the
        /// current magnitude (no frame history) so it adds zero input lag. Angle is preserved by the
        /// caller, which rescales the input vector by out/|input|.
        /// </summary>
        public static double ComputeSoftOutputMagnitude(
            double inputT, SoftAntiDeadzoneInfo info)
        {
            inputT = Math.Clamp(inputT, 0.0, 1.0);
            if (inputT <= 1e-9)
            {
                return 0.0;
            }

            double rampZone = Math.Clamp(info.rampZone, 0.01, 0.95);
            double targetT = Math.Clamp(info.compensationSize * 0.01, 0.0, 0.95);
            double exponent = Math.Clamp(info.exponent, 0.5, 8.0);

            double classic = targetT + ((1.0 - targetT) * inputT);

            if (inputT >= rampZone)
            {
                return classic;
            }

            double t = inputT / rampZone;
            double shaped = Math.Pow(t, exponent);
            double weight = shaped * shaped * (3.0 - (2.0 * shaped));
            return inputT + (weight * (classic - inputT));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Lerp(double from, double to, double t)
        {
            return from + ((to - from) * Math.Clamp(t, 0.0, 1.0));
        }
    }
}
