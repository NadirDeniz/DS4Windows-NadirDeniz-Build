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
    /// Applies sensitivity, optional square stick, and output curve in a configurable order.
    /// Each stage runs at most once per frame; square stick is skipped when disabled in profile.
    /// </summary>
    internal static class StickModifierOrderPipeline
    {
        public static StickModifierOrderMode ClampOrder(int rawOrder)
        {
            if (rawOrder <= (int)StickModifierOrderMode.LegacyCompatible)
            {
                return StickModifierOrderMode.LegacyCompatible;
            }

            if (rawOrder >= (int)StickModifierOrderMode.CurveBeforeSquare)
            {
                return StickModifierOrderMode.CurveBeforeSquare;
            }

            return (StickModifierOrderMode)rawOrder;
        }

        public static void ApplyToNormalizedStick(
            int device,
            int stickId,
            StickDeadZoneInfo mod,
            StickModifierOrderMode order,
            ref double normX,
            ref double normY,
            double sensitivity,
            bool applyRadialSensitivity,
            bool squareStickEnabled,
            double squareRoundness,
            DS4SquareStick squareStick,
            int outputCurveMode,
            BezierCurve outputBezier)
        {
            order = ClampOrder((int)order);

            switch (order)
            {
                case StickModifierOrderMode.CurveBeforeSensitivity:
                    RunOutputCurve(mod, ref normX, ref normY, outputCurveMode, outputBezier);
                    RunRadialSensitivity(ref normX, ref normY, sensitivity, applyRadialSensitivity);
                    RunSquareStick(ref normX, ref normY, squareStickEnabled, squareRoundness, squareStick);
                    break;

                case StickModifierOrderMode.CurveBeforeSquare:
                    RunRadialSensitivity(ref normX, ref normY, sensitivity, applyRadialSensitivity);
                    RunOutputCurve(mod, ref normX, ref normY, outputCurveMode, outputBezier);
                    RunSquareStick(ref normX, ref normY, squareStickEnabled, squareRoundness, squareStick);
                    break;

                default:
                    RunRadialSensitivity(ref normX, ref normY, sensitivity, applyRadialSensitivity);
                    RunSquareStick(ref normX, ref normY, squareStickEnabled, squareRoundness, squareStick);
                    RunOutputCurve(mod, ref normX, ref normY, outputCurveMode, outputBezier);
                    break;
            }
        }

        private static void RunRadialSensitivity(
            ref double normX, ref double normY,
            double sensitivity, bool applyRadialSensitivity)
        {
            if (!applyRadialSensitivity || Math.Abs(sensitivity - 1.0) < 1e-9)
            {
                return;
            }

            HighPrecisionStickPipeline.ApplyRadialSensitivity(
                ref normX, ref normY, sensitivity);
        }

        private static void RunSquareStick(
            ref double normX, ref double normY,
            bool squareStickEnabled, double squareRoundness, DS4SquareStick squareStick)
        {
            if (!squareStickEnabled ||
                (Math.Abs(normX) < 1e-9 && Math.Abs(normY) < 1e-9))
            {
                return;
            }

            HighPrecisionStickPipeline.ApplySquareStick(
                ref normX, ref normY, squareStick, squareRoundness);
        }

        private static void RunOutputCurve(
            StickDeadZoneInfo mod,
            ref double normX, ref double normY,
            int outputCurveMode, BezierCurve outputBezier)
        {
            if (outputCurveMode <= 0 ||
                (Math.Abs(normX) < 1e-9 && Math.Abs(normY) < 1e-9))
            {
                return;
            }

            if (mod.deadzoneType == StickDeadZoneInfo.DeadZoneType.Radial)
            {
                HighPrecisionStickPipeline.ApplyOutputCurveRadial(
                    ref normX, ref normY, outputCurveMode, outputBezier);
            }
            else
            {
                HighPrecisionStickPipeline.ApplyOutputCurveAxial(
                    ref normX, ref normY, outputCurveMode, outputBezier);
            }
        }

        /// <summary>
        /// Reordered legacy path: float modifiers then sync bytes (order != LegacyCompatible only).
        /// </summary>
        public static void ApplyReorderedLegacyStick(
            int device,
            int stickId,
            ref DS4State dState,
            StickDeadZoneInfo mod,
            StickModifierOrderMode order,
            SquareStickInfo squareStickInfo,
            DS4SquareStick squareStick,
            int outputCurveMode,
            BezierCurve outputBezier)
        {
            byte stickX = stickId == 0 ? dState.LX : dState.RX;
            byte stickY = stickId == 0 ? dState.LY : dState.RY;

            if (stickX == 128 && stickY == 128)
            {
                return;
            }

            double normX;
            double normY;
            if (True16BitStickPipeline.IsActiveForDevice(device))
            {
                True16BitStickPipeline.BytesToInternal16Normalized(stickX, stickY, out normX, out normY);
            }
            else
            {
                StickInputProcessor.BytesToNormalized(stickX, stickY, out normX, out normY);
            }

            bool squareEnabled = stickId == 0 ? squareStickInfo.lsMode : squareStickInfo.rsMode;
            double roundness = stickId == 0 ? squareStickInfo.lsRoundness : squareStickInfo.rsRoundness;
            double sensitivity = stickId == 0 ? Global.getLSSens(device) : Global.getRSSens(device);
            bool applySens = mod.deadzoneType == StickDeadZoneInfo.DeadZoneType.Radial;

            ApplyToNormalizedStick(
                device, stickId, mod, order,
                ref normX, ref normY,
                sensitivity, applySens,
                squareEnabled, roundness, squareStick,
                outputCurveMode, outputBezier);

            if (dState.UseTrue16BitStickPipeline || dState.HasFloatNormStick)
            {
                HighPrecisionStickPipeline.SetStickNormalized(dState, stickId, normX, normY);
            }

            if (!True16BitStickPipeline.ShouldDeferByteSync(dState))
            {
                StickInputProcessor.NormalizedToStickBytes(
                    normX, normY, out stickX, out stickY);

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
        }
    }
}
