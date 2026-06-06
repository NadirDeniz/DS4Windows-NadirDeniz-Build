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
    /// Direction-aware polar refinement after deadzone / pre-curve modifiers.
    /// Preserves vector angle and magnitude (no diagonal speed bias).
    /// </summary>
    internal static class DirectionAwareResolutionProcessor
    {
        private const int POLAR_QUANT_LEVELS = 65536;
        private const double MIN_MAG = 1e-9;

        public static bool IsEnabledForStick(int device, int stickId)
        {
            return stickId == 0
                ? Global.getLSDirectionAwareResolution(device)
                : Global.getRSDirectionAwareResolution(device);
        }

        public static void ApplyToState(int device, int stickId, ref DS4State dState)
        {
            if (!IsEnabledForStick(device, stickId))
            {
                return;
            }

            if (dState.HasFloatNormStick)
            {
                HighPrecisionStickPipeline.GetStickNormalized(
                    dState, stickId, out double normX, out double normY);
                Apply(ref normX, ref normY);
                HighPrecisionStickPipeline.SetStickNormalized(dState, stickId, normX, normY);

                if (!True16BitStickPipeline.ShouldDeferByteSync(dState))
                {
                    HighPrecisionStickPipeline.SyncBytesFromNormalized(
                        normX, normY,
                        out byte outX, out byte outY);
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

                return;
            }

            byte stickX = stickId == 0 ? dState.LX : dState.RX;
            byte stickY = stickId == 0 ? dState.LY : dState.RY;
            StickInputProcessor.BytesToNormalized(stickX, stickY, out double nx, out double ny);
            Apply(ref nx, ref ny);
            StickInputProcessor.NormalizedToStickBytes(nx, ny, out stickX, out stickY);

            if (stickId == 0)
            {
                dState.LX = stickX;
                dState.LY = stickY;
            }
            else
            {
                dState.RX = stickX;
                dState.RY = stickY;
            }
        }

        /// <summary>
        /// Refine polar representation at 16-bit granularity; rescale to original magnitude.
        /// </summary>
        public static void Apply(ref double normX, ref double normY)
        {
            double mag = Math.Sqrt((normX * normX) + (normY * normY));
            if (mag <= MIN_MAG)
            {
                normX = normY = 0.0;
                return;
            }

            double angle = Math.Atan2(normY, normX);
            double magQ = Math.Round(mag * POLAR_QUANT_LEVELS) / POLAR_QUANT_LEVELS;
            double angQ = Math.Round(angle * POLAR_QUANT_LEVELS) / POLAR_QUANT_LEVELS;

            double refinedX = Math.Cos(angQ) * magQ;
            double refinedY = Math.Sin(angQ) * magQ;
            double refinedMag = Math.Sqrt((refinedX * refinedX) + (refinedY * refinedY));

            if (refinedMag > MIN_MAG)
            {
                double preserveScale = mag / refinedMag;
                normX = refinedX * preserveScale;
                normY = refinedY * preserveScale;
            }
            else
            {
                normX = Math.Cos(angle) * mag;
                normY = Math.Sin(angle) * mag;
            }

            normX = Math.Clamp(normX, -1.0, 1.0);
            normY = Math.Clamp(normY, -1.0, 1.0);
        }
    }
}
