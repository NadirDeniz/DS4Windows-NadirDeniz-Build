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

namespace APP
{
    /// <summary>
    /// Soft radial center-exit shaping: eases the first tiny movements away from center.
    /// Deterministic and stateless &mdash; the output depends only on the current stick magnitude,
    /// never on approach speed, approach direction, poll rate, or stick history. The shaping is a
    /// single C1-continuous, monotonic magnitude curve so it introduces no per-frame switching,
    /// no stair-stepping, and no magnitude discontinuities. Angle is always preserved.
    /// Runs after deadzone removal. On RS it runs after soft anti-deadzone (when enabled)
    /// and before in-game remapper, micro-velocity, and dynamic/micro-aim.
    /// </summary>
    internal static class SoftCenterExitProcessor
    {
        public static void ResetDevice(int device)
        {
            // Stateless: nothing to reset. Kept for call-site compatibility.
        }

        /// <summary>
        /// Apply center-exit shaping on normalized stick vector; preserves angle.
        /// </summary>
        /// <param name="deltaTimeSeconds">Unused (kept for signature stability); shaping is time-independent.</param>
        public static void ApplyRadial(
            int device, int stickId,
            ref double normX, ref double normY,
            SoftCenterExitInfo info,
            double deltaTimeSeconds)
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

            double outputMag = ComputeShapedMagnitude(mag, info);
            double scale = outputMag / mag;
            normX *= scale;
            normY *= scale;
        }

        public static void ApplyToState(
            int device, int stickId,
            SoftCenterExitInfo info,
            double deltaTimeSeconds,
            ref DS4State dState)
        {
            if (!info.enabled)
            {
                return;
            }

            double normX;
            double normY;
            ReadStickNormalized(device, stickId, ref dState, out normX, out normY);

            ApplyRadial(device, stickId, ref normX, ref normY, info, deltaTimeSeconds);

            WriteStickNormalized(device, stickId, ref dState, normX, normY);
        }

        /// <summary>
        /// Maps the stick magnitude through a single, monotonic, C1-continuous easing curve that
        /// compresses motion below <see cref="SoftCenterExitInfo.range"/> and is the identity above it.
        ///
        /// For magnitude m and t = m / range (m &lt; range) the shaped value is range * H(t), where
        /// H is the cubic Hermite with H(0)=0, H(1)=1, H'(1)=1, and H'(0)=k. This guarantees:
        ///   * value continuity at the boundary (H(1)=1 =&gt; shaped(range)=range),
        ///   * slope continuity at the boundary (H'(1)=1 matches the identity slope, so no kink),
        ///   * monotonicity for k in [0,1] (no stair-stepping),
        ///   * compression near center (H(t) &le; t), with k smaller =&gt; softer center.
        /// The softening slope k is derived from the legacy exponent (k = 1/exponent), so exponent=1
        /// is a pure identity and larger exponents soften the center more.
        /// </summary>
        public static double ComputeShapedMagnitude(
            double magnitude, SoftCenterExitInfo info)
        {
            magnitude = Math.Clamp(magnitude, 0.0, 1.0);
            if (magnitude <= 1e-9)
            {
                return 0.0;
            }

            double range = Math.Clamp(info.range, 0.001, 0.95);
            if (magnitude >= range)
            {
                return magnitude;
            }

            double exponent = Math.Clamp(info.exponent, 0.5, 8.0);
            double k = Math.Clamp(1.0 / exponent, 0.0, 1.0);

            double t = magnitude / range;
            double t2 = t * t;
            double t3 = t2 * t;

            // H(t) = (-t^3 + 2t^2) + k*(t^3 - 2t^2 + t)
            double h = (-t3 + (2.0 * t2)) + (k * (t3 - (2.0 * t2) + t));
            return h * range;
        }

        private static void ReadStickNormalized(
            int device, int stickId, ref DS4State dState,
            out double normX, out double normY)
        {
            if (dState.HasFloatNormStick)
            {
                HighPrecisionStickPipeline.GetStickNormalized(
                    dState, stickId, out normX, out normY);
                return;
            }

            byte stickX = stickId == 0 ? dState.LX : dState.RX;
            byte stickY = stickId == 0 ? dState.LY : dState.RY;
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

        private static void WriteStickNormalized(
            int device, int stickId, ref DS4State dState,
            double normX, double normY)
        {
            if (dState.HasFloatNormStick ||
                True16BitStickPipeline.IsActiveForDevice(device))
            {
                HighPrecisionStickPipeline.SetStickNormalized(dState, stickId, normX, normY);
            }

            if (!True16BitStickPipeline.ShouldDeferByteSync(dState))
            {
                HighPrecisionStickPipeline.SyncBytesFromNormalized(
                    dState, stickId, normX, normY, out byte outX, out byte outY);
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
}
