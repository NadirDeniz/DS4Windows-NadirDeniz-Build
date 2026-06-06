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
    /// Float-normalized stick processing (-1..1 per axis). Bytes are synced only
    /// for bindings/UI; XInput uses <see cref="StickOutputQuantizer"/> at the end.
    /// </summary>
    internal static class HighPrecisionStickPipeline
    {
        public static bool IsActiveForDevice(int device)
        {
            return Global.getHighPrecisionStickOutput(device);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetStickNormalized(
            DS4State state, int stickId, double normX, double normY)
        {
            if (stickId == 0)
            {
                state.FloatNormLX = normX;
                state.FloatNormLY = normY;
            }
            else
            {
                state.FloatNormRX = normX;
                state.FloatNormRY = normY;
            }

            state.HasFloatNormStick = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetStickNormalized(
            DS4State state, int stickId, out double normX, out double normY)
        {
            if (stickId == 0)
            {
                normX = state.FloatNormLX;
                normY = state.FloatNormLY;
            }
            else
            {
                normX = state.FloatNormRX;
                normY = state.FloatNormRY;
            }
        }

        public static void LoadNormalizedFromBytes(
            byte stickX, byte stickY, out double normX, out double normY)
        {
            MicroAimPrecisionProcessor.HighPrecisionNormalize(
                stickX, stickY, out normX, out normY);
        }

        public static void LoadNormalizedFromBytes(
            int device, byte stickX, byte stickY, out double normX, out double normY)
        {
            if (True16BitStickPipeline.IsActiveForDevice(device))
            {
                True16BitStickPipeline.BytesToInternal16Normalized(
                    stickX, stickY, out normX, out normY);
            }
            else
            {
                LoadNormalizedFromBytes(stickX, stickY, out normX, out normY);
            }
        }

        public static void SyncBytesFromNormalized(
            double normX, double normY, out byte outX, out byte outY)
        {
            StickInputProcessor.NormalizedToStickBytes(normX, normY, out outX, out outY);
        }

        /// <summary>Sync bytes unless true 16-bit path defers quantization to finalize.</summary>
        public static void SyncBytesFromNormalized(
            DS4State dState, int stickId,
            double normX, double normY,
            out byte outX, out byte outY)
        {
            if (True16BitStickPipeline.ShouldDeferByteSync(dState))
            {
                outX = stickId == 0 ? dState.LX : dState.RX;
                outY = stickId == 0 ? dState.LY : dState.RY;
                return;
            }

            SyncBytesFromNormalized(normX, normY, out outX, out outY);
        }

        public static void ApplyRadialSensitivity(
            ref double normX, ref double normY, double sensitivity)
        {
            if (Math.Abs(sensitivity - 1.0) < 1e-9)
            {
                return;
            }

            normX = Math.Clamp(normX * sensitivity, -1.0, 1.0);
            normY = Math.Clamp(normY * sensitivity, -1.0, 1.0);
        }

        public static void ApplySquareStick(
            ref double normX, ref double normY,
            DS4SquareStick sqstick, double roundness)
        {
            if (Math.Abs(normX) < 1e-9 && Math.Abs(normY) < 1e-9)
            {
                return;
            }

            sqstick.current.x = normX;
            sqstick.current.y = normY;
            sqstick.CircleToSquare(roundness);
            normX = Math.Clamp(sqstick.current.x, -1.0, 1.0);
            normY = Math.Clamp(sqstick.current.y, -1.0, 1.0);
        }

        public static void ApplyOutputCurveRadial(
            ref double normX, ref double normY,
            int curveMode, BezierCurve bezier)
        {
            if (curveMode <= 0)
            {
                return;
            }

            if (Math.Abs(normX) < 1e-9 && Math.Abs(normY) < 1e-9)
            {
                return;
            }

            // Radial deadzone: apply the output curve to the stick MAGNITUDE and keep the
            // direction (angle) intact. Applying the curve to each axis independently would
            // shrink the weaker axis far more than the stronger one, pulling diagonal input
            // toward the nearest cardinal axis (i.e. diagonals "collapse" to X/Y).
            double mag = Math.Sqrt((normX * normX) + (normY * normY));
            if (mag < 1e-9)
            {
                return;
            }

            mag = Math.Min(mag, 1.0);
            double newMag;

            if (curveMode == 6 && bezier != null && bezier.arrayBezierLUT != null)
            {
                byte tempOut = (byte)Math.Clamp(
                    (int)Math.Round(mag * 127.0 + 128.0, MidpointRounding.AwayFromZero), 0, 255);
                byte curved = bezier.arrayBezierLUT[tempOut];
                newMag = (curved - 128.0) / 127.0;
            }
            else
            {
                newMag = ApplyScalarCurve(mag, curveMode);
            }

            double scale = newMag / mag;
            normX = Math.Clamp(normX * scale, -1.0, 1.0);
            normY = Math.Clamp(normY * scale, -1.0, 1.0);
        }

        public static void ApplyOutputCurveAxial(
            ref double normX, ref double normY,
            int curveMode, BezierCurve bezier)
        {
            if (curveMode <= 0)
            {
                return;
            }

            ApplyScalarCurveToAxis(ref normX, curveMode, bezier);
            ApplyScalarCurveToAxis(ref normY, curveMode, bezier);
        }

        private static void ApplyScalarCurveToAxis(
            ref double norm, int curveMode, BezierCurve bezier)
        {
            if (Math.Abs(norm) < 1e-9)
            {
                return;
            }

            if (curveMode == 6 && bezier != null)
            {
                byte tempOut = (byte)Math.Clamp(
                    (int)Math.Round(norm * 127.0 + 128.0, MidpointRounding.AwayFromZero), 0, 255);
                byte curved = bezier.arrayBezierLUT[tempOut];
                norm = (curved - 128.0) / 127.0;
                return;
            }

            double sign = Math.Sign(norm);
            norm = ApplyScalarCurve(Math.Abs(norm), curveMode) * sign;
        }

        private static double ApplyScalarCurve(double abs, int curveMode)
        {
            abs = Math.Clamp(abs, 0.0, 1.0);
            switch (curveMode)
            {
                case 1:
                    if (abs <= 0.4) return 0.8 * abs;
                    if (abs <= 0.75) return abs - 0.08;
                    return (abs * 1.32) - 0.32;
                case 2:
                    return abs * abs;
                case 3:
                    return abs * abs * abs;
                case 4:
                    return -1.0 * abs * (abs - 2.0);
                case 5:
                    {
                        double inner = abs - 1.0;
                        return inner * inner * inner + 1.0;
                    }
                default:
                    return abs;
            }
        }

        public static void FinalizeStickToState(
            int device, int stickId, ref DS4State dState,
            double normX, double normY)
        {
            StickOutputQuantizer.FinalizeStickPair(
                device, stickId, normX, normY,
                out short preciseX, out short preciseY);

            if (stickId == 0)
            {
                dState.PreciseLX = preciseX;
                dState.PreciseLY = preciseY;
                SyncBytesFromNormalized(normX, normY, out dState.LX, out dState.LY);
            }
            else
            {
                dState.PreciseRX = preciseX;
                dState.PreciseRY = preciseY;
                SyncBytesFromNormalized(normX, normY, out dState.RX, out dState.RY);
            }

            SetStickNormalized(dState, stickId, normX, normY);
            dState.UsePreciseStickOutput = true;
        }

        public static void FinalizeFromProcessorFloatState(
            int device, int stickId, ref DS4State dState)
        {
            if (MicroAimPrecisionProcessor.TryGetFloatState(
                    device, stickId, out double normX, out double normY))
            {
                FinalizeStickToState(device, stickId, ref dState, normX, normY);
                return;
            }

            byte stickX = stickId == 0 ? dState.LX : dState.RX;
            byte stickY = stickId == 0 ? dState.LY : dState.RY;
            LoadNormalizedFromBytes(stickX, stickY, out normX, out normY);
            FinalizeStickToState(device, stickId, ref dState, normX, normY);
        }

        public static void ResetDevice(int device)
        {
            StickOutputQuantizer.ResetDevice(device);
        }
    }
}
