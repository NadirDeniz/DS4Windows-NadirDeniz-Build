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
    /// Stick processing helpers for Clean Input Mode and legacy paths.
    /// Clean Input pipeline: raw HID → user deadzone → user anti-deadzone →
    /// user curve → user sensitivity → precise output conversion.
    /// Hidden processing (fuzz, snapback, 0.99 fudge, per-axis normalization) is bypassed.
    /// </summary>
    internal static class StickInputProcessor
    {
        private const double CENTER = 128.0;
        private const double MAX_RADIUS = 127.0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ToStickByte(double value)
        {
            return (byte)Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ScaleNormalizedToXInput(double normalized)
        {
            normalized = Math.Clamp(normalized, -1.0, 1.0);
            return (short)Math.Clamp(
                (int)Math.Round(normalized * 32767.0, MidpointRounding.AwayFromZero),
                -32768, 32767);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BytesToNormalized(byte stickX, byte stickY,
            out double normX, out double normY)
        {
            double dx = stickX - CENTER;
            double dy = -(stickY - CENTER);
            normX = dx / MAX_RADIUS;
            normY = dy / MAX_RADIUS;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NormalizedToStickBytes(
            double normX, double normY, out byte outX, out byte outY)
        {
            outX = ToStickByte(CENTER + (normX * MAX_RADIUS));
            outY = ToStickByte(CENTER - (normY * MAX_RADIUS));
        }

        /// <summary>Radial deadzone in normalized float space (no byte quantization).</summary>
        public static void ProcessRadialDeadzoneFloat(
            int device, int stickId,
            byte inX, byte inY,
            StickDeadZoneInfo mod,
            double deltaTimeSeconds,
            out double normX, out double normY)
        {
            bool skipClassicAntiDead =
                SoftAntiDeadzoneProcessor.ShouldSkipClassicAntiDead(device, stickId);

            if (mod.advancedRadialProcessing)
            {
                AdvancedDeadzoneProcessor.ProcessRadial(
                    device, stickId, inX, inY, mod, deltaTimeSeconds,
                    skipClassicAntiDead, out normX, out normY);
                return;
            }

            ProcessRadialDeadzoneFloatLegacy(
                inX, inY, mod, skipClassicAntiDead, out normX, out normY);
        }

        public static void ProcessRadialDeadzoneFloatLegacy(
            byte inX, byte inY,
            StickDeadZoneInfo mod,
            bool skipClassicAntiDead,
            out double normX, out double normY)
        {
            double dx = inX - CENTER;
            double dy = -(inY - CENTER);

            double mag = Math.Sqrt((dx * dx) + (dy * dy));
            int deadZone = mod.deadZone;
            double maxZoneMag = (mod.maxZone / 100.0) * MAX_RADIUS;

            if (deadZone > 0 && mag <= deadZone)
            {
                normX = normY = 0.0;
                return;
            }

            if (mag > maxZoneMag)
            {
                mag = maxZoneMag;
            }

            double normalized;
            if (deadZone > 0)
            {
                normalized = (mag - deadZone) / (maxZoneMag - deadZone);
            }
            else
            {
                normalized = mag / maxZoneMag;
            }

            normalized = Math.Clamp(normalized, 0.0, 1.0);

            double outNorm;
            if (skipClassicAntiDead)
            {
                outNorm = normalized;
            }
            else
            {
                double antiDead = mod.antiDeadZone * 0.01;
                outNorm = antiDead + ((1.0 - antiDead) * normalized);
            }

            if (mod.maxOutput != 100.0 || mod.maxOutputForce)
            {
                outNorm = Math.Min(outNorm, mod.maxOutput / 100.0);
            }

            double outMag = outNorm * MAX_RADIUS;
            double outDx = 0.0;
            double outDy = 0.0;

            if (mag > 0.001)
            {
                outDx = (dx / mag) * outMag;
                outDy = (dy / mag) * outMag;
            }

            if (mod.verticalScale != StickDeadZoneInfo.DEFAULT_VERTICAL_SCALE)
            {
                outDy *= mod.verticalScale / 100.0;
            }

            normX = outDx / MAX_RADIUS;
            normY = outDy / MAX_RADIUS;
        }

        /// <summary>Legacy radial deadzone on an established normalized vector (16-bit path).</summary>
        public static void ProcessRadialDeadzoneOnNormalized(
            StickDeadZoneInfo mod, ref double normX, ref double normY,
            bool skipClassicAntiDead)
        {
            double magNorm = Math.Sqrt((normX * normX) + (normY * normY));
            if (magNorm <= 1e-9)
            {
                normX = normY = 0.0;
                return;
            }

            double angle = Math.Atan2(normY, normX);
            int deadZone = mod.deadZone;
            double maxZoneMag = (mod.maxZone / 100.0);

            if (deadZone > 0 && magNorm <= (deadZone / MAX_RADIUS))
            {
                normX = normY = 0.0;
                return;
            }

            double innerDeadNorm = deadZone / MAX_RADIUS;
            if (magNorm > maxZoneMag)
            {
                magNorm = maxZoneMag;
            }

            double normalized;
            if (deadZone > 0)
            {
                normalized = (magNorm - innerDeadNorm) / (maxZoneMag - innerDeadNorm);
            }
            else
            {
                normalized = magNorm / maxZoneMag;
            }

            normalized = Math.Clamp(normalized, 0.0, 1.0);

            double outNorm;
            if (skipClassicAntiDead)
            {
                outNorm = normalized;
            }
            else
            {
                double antiDead = mod.antiDeadZone * 0.01;
                outNorm = antiDead + ((1.0 - antiDead) * normalized);
            }

            if (mod.maxOutput != 100.0 || mod.maxOutputForce)
            {
                outNorm = Math.Min(outNorm, mod.maxOutput / 100.0);
            }

            // outNorm is already the normalized [0,1] output (1.0 at maxZone). Scaling it again by
            // maxZoneMag would cap the maximum output below full, e.g. maxZone=80 would never let
            // the stick exceed 0.8. Use outNorm directly so full tilt reaches full output.
            double outMagNorm = outNorm;
            normX = Math.Cos(angle) * outMagNorm;
            normY = Math.Sin(angle) * outMagNorm;

            if (mod.verticalScale != StickDeadZoneInfo.DEFAULT_VERTICAL_SCALE)
            {
                normY *= mod.verticalScale / 100.0;
            }

            normX = Math.Clamp(normX, -1.0, 1.0);
            normY = Math.Clamp(normY, -1.0, 1.0);
        }

        public static void ProcessAxialDeadzoneOnNormalized(
            int device, int stickId,
            StickDeadZoneInfo mod,
            double deltaTimeSeconds,
            ref double normX, ref double normY)
        {
            byte pseudoX = ToStickByte(CENTER + (normX * MAX_RADIUS));
            byte pseudoY = ToStickByte(CENTER - (normY * MAX_RADIUS));
            ProcessAxialDeadzoneFloat(
                device, stickId, pseudoX, pseudoY, mod, deltaTimeSeconds,
                out double outNormX, out double outNormY);
            normX = outNormX;
            normY = outNormY;
        }

        public static void ProcessAxialDeadzoneFloat(
            int device, int stickId,
            byte inX, byte inY,
            StickDeadZoneInfo mod,
            double deltaTimeSeconds,
            out double normX, out double normY)
        {
            if (mod.advancedRadialProcessing)
            {
                AdvancedDeadzoneProcessor.ProcessAxialAxis(
                    device, stickId, false, inX, mod.xAxisDeadInfo, mod, deltaTimeSeconds, out normX);
                AdvancedDeadzoneProcessor.ProcessAxialAxis(
                    device, stickId, true, inY, mod.yAxisDeadInfo, mod, deltaTimeSeconds, out normY);
                return;
            }

            ProcessAxialAxisFloat(inX, mod.xAxisDeadInfo, out normX);
            ProcessAxialAxisFloat(inY, mod.yAxisDeadInfo, out normY);

            // ProcessAxialAxisFloat returns a byte-space value where a byte above center maps to a
            // positive result. For Y a byte above center means "down", but the high-precision
            // pipeline uses the math convention where up is positive (see BytesToNormalized, which
            // negates Y). Negate here so the axial path matches that convention and the advanced
            // radial path (which already inverts Y internally); otherwise the vertical axis flips.
            normY = -normY;
        }

        public static void ProcessAxialAxisFloat(
            byte inValue,
            StickDeadZoneInfo.AxisDeadZoneInfo axisInfo,
            out double normValue)
        {
            normValue = (inValue - CENTER) / MAX_RADIUS;

            if (axisInfo.deadZone <= 0 && axisInfo.antiDeadZone <= 0 &&
                axisInfo.maxZone == 100 && axisInfo.maxOutput == 100.0)
            {
                return;
            }

            int distVal = Math.Abs(inValue - 128);
            if (axisInfo.deadZone > 0 && distVal <= axisInfo.deadZone)
            {
                normValue = 0.0;
                return;
            }

            if (!((axisInfo.deadZone > 0 && distVal > axisInfo.deadZone) ||
                axisInfo.antiDeadZone > 0 || axisInfo.maxZone != 100 || axisInfo.maxOutput != 100.0))
            {
                return;
            }

            double maxAxisValue = inValue >= CENTER ? 127.0 : -128.0;
            double ratio = axisInfo.maxZone / 100.0;
            double maxOutRatio = axisInfo.maxOutput / 100.0;

            double maxZoneNegValue = (ratio * -128.0) + CENTER;
            double maxZonePosValue = (ratio * 127.0) + CENTER;
            double maxZone = inValue >= CENTER ? (maxZonePosValue - CENTER) : (maxZoneNegValue - CENTER);

            double tempDead = axisInfo.deadZone > 0 ? ((axisInfo.deadZone / 127.0) * maxAxisValue) : 0.0;
            double currentVal = Global.Clamp(maxZoneNegValue, inValue, maxZonePosValue);
            double tempOutput = (currentVal - CENTER - tempDead) / (maxZone - tempDead);

            if (axisInfo.maxOutput != 100.0)
            {
                tempOutput = Math.Min(Math.Max(tempOutput, 0.0), maxOutRatio);
            }

            double tempAntiDeadPercent = axisInfo.antiDeadZone > 0 ? axisInfo.antiDeadZone * 0.01 : 0.0;

            if (tempOutput > 0.0)
            {
                double offset = (((1.0 - tempAntiDeadPercent) * tempOutput + tempAntiDeadPercent)) * maxAxisValue;
                normValue = offset / MAX_RADIUS;
            }
            else
            {
                normValue = 0.0;
            }
        }

        /// <summary>
        /// Applies only explicit profile deadzone settings on stick magnitude.
        /// No hidden per-axis normalization or output expansion fudge.
        /// </summary>
        public static void ProcessRadialStickClean(
            int device, int stickId,
            byte inX, byte inY,
            StickDeadZoneInfo mod,
            double deltaTimeSeconds,
            out byte outX, out byte outY,
            out short preciseX, out short preciseY)
        {
            if (mod.advancedRadialProcessing)
            {
                ProcessRadialDeadzoneFloat(
                    device, stickId, inX, inY, mod, deltaTimeSeconds,
                    out double radialNormX, out double radialNormY);
                NormalizedToStickBytes(radialNormX, radialNormY, out outX, out outY);
                preciseX = ScaleNormalizedToXInput(radialNormX);
                preciseY = ScaleNormalizedToXInput(radialNormY);
                return;
            }

            bool skipClassicAntiDead =
                SoftAntiDeadzoneProcessor.ShouldSkipClassicAntiDead(device, stickId);

            double dx = inX - CENTER;
            double dy = -(inY - CENTER);

            double mag = Math.Sqrt((dx * dx) + (dy * dy));
            int deadZone = mod.deadZone;
            double maxZoneMag = (mod.maxZone / 100.0) * MAX_RADIUS;

            if (deadZone > 0 && mag <= deadZone)
            {
                outX = outY = 128;
                preciseX = preciseY = 0;
                return;
            }

            if (mag > maxZoneMag)
            {
                mag = maxZoneMag;
            }

            double normalized;
            if (deadZone > 0)
            {
                normalized = (mag - deadZone) / (maxZoneMag - deadZone);
            }
            else
            {
                normalized = mag / maxZoneMag;
            }

            normalized = Math.Clamp(normalized, 0.0, 1.0);

            double outNorm;
            if (skipClassicAntiDead)
            {
                outNorm = normalized;
            }
            else
            {
                double antiDead = mod.antiDeadZone * 0.01;
                outNorm = antiDead + ((1.0 - antiDead) * normalized);
            }

            if (mod.maxOutput != 100.0 || mod.maxOutputForce)
            {
                outNorm = Math.Min(outNorm, mod.maxOutput / 100.0);
            }

            double outMag = outNorm * MAX_RADIUS;
            double outDx = 0.0;
            double outDy = 0.0;

            if (mag > 0.001)
            {
                outDx = (dx / mag) * outMag;
                outDy = (dy / mag) * outMag;
            }

            if (mod.verticalScale != StickDeadZoneInfo.DEFAULT_VERTICAL_SCALE)
            {
                outDy *= mod.verticalScale / 100.0;
            }

            double normX = outDx / MAX_RADIUS;
            double normY = outDy / MAX_RADIUS;

            NormalizedToStickBytes(normX, normY, out outX, out outY);
            preciseX = ScaleNormalizedToXInput(normX);
            preciseY = ScaleNormalizedToXInput(normY);
        }

        /// <summary>
        /// Legacy per-axis radial deadzone path with improved final rounding.
        /// </summary>
        public static void ProcessRadialStickLegacy(
            int device, int stickId,
            byte inX, byte inY,
            StickDeadZoneInfo mod,
            out byte outX, out byte outY)
        {
            outX = inX;
            outY = inY;

            int deadZone = mod.deadZone;
            int antiDead = mod.antiDeadZone;
            int maxZone = mod.maxZone;
            double maxOutput = mod.maxOutput;
            double verticalScale = mod.verticalScale;
            bool interpret = antiDead > 0 || maxZone != 100 || maxOutput != 100.0 ||
                mod.maxOutputForce ||
                verticalScale != StickDeadZoneInfo.DEFAULT_VERTICAL_SCALE;

            if (deadZone <= 0 && !interpret)
            {
                return;
            }

            double squared = Math.Pow(inX - CENTER, 2) + Math.Pow(inY - CENTER, 2);
            double deadzoneSquared = Math.Pow(deadZone, 2);

            if (deadZone > 0 && squared <= deadzoneSquared)
            {
                outX = outY = 128;
                return;
            }

            if (!((deadZone > 0 && squared > deadzoneSquared) || interpret))
            {
                return;
            }

            double r = Math.Atan2(-(inY - CENTER), inX - CENTER);
            double maxXValue = inX >= CENTER ? 127.0 : -128.0;
            double maxYValue = inY >= CENTER ? 127.0 : -128.0;
            double ratio = maxZone / 100.0;
            double maxOutRatio = maxOutput / 100.0;

            double maxZoneXNegValue = (ratio * -128.0) + CENTER;
            double maxZoneXPosValue = (ratio * 127.0) + CENTER;
            double maxZoneYNegValue = maxZoneXNegValue;
            double maxZoneYPosValue = maxZoneXPosValue;
            double maxZoneX = inX >= CENTER ? (maxZoneXPosValue - CENTER) : (maxZoneXNegValue - CENTER);
            double maxZoneY = inY >= CENTER ? (maxZoneYPosValue - CENTER) : (maxZoneYNegValue - CENTER);

            double tempXDead = 0.0;
            double tempYDead = 0.0;
            double tempOutputX = 0.0;
            double tempOutputY = 0.0;

            if (deadZone > 0)
            {
                tempXDead = Math.Abs(Math.Cos(r)) * (deadZone / 127.0) * maxXValue;
                tempYDead = Math.Abs(Math.Sin(r)) * (deadZone / 127.0) * maxYValue;

                if (squared > deadzoneSquared)
                {
                    double currentX = Global.Clamp(maxZoneXNegValue, inX, maxZoneXPosValue);
                    double currentY = Global.Clamp(maxZoneYNegValue, inY, maxZoneYPosValue);
                    tempOutputX = (currentX - CENTER - tempXDead) / (maxZoneX - tempXDead);
                    tempOutputY = (currentY - CENTER - tempYDead) / (maxZoneY - tempYDead);
                }
            }
            else
            {
                double currentX = Global.Clamp(maxZoneXNegValue, inX, maxZoneXPosValue);
                double currentY = Global.Clamp(maxZoneYNegValue, inY, maxZoneYPosValue);
                tempOutputX = (currentX - CENTER) / maxZoneX;
                tempOutputY = (currentY - CENTER) / maxZoneY;
            }

            if (verticalScale != StickDeadZoneInfo.DEFAULT_VERTICAL_SCALE)
            {
                tempOutputY = Math.Min(Math.Max(tempOutputY * (verticalScale / 100.0), 0.0), 1.0);
            }

            if (maxOutput != 100.0 || mod.maxOutputForce)
            {
                double maxOutXRatio = Math.Abs(Math.Cos(r)) * maxOutRatio;
                maxOutXRatio = Math.Min(maxOutXRatio / 0.99, 1.0);

                double maxOutYRatio = Math.Abs(Math.Sin(r)) * maxOutRatio;
                maxOutYRatio = Math.Min(maxOutYRatio / 0.99, 1.0);

                tempOutputX = Math.Min(Math.Max(tempOutputX, 0.0), maxOutXRatio);
                tempOutputY = Math.Min(Math.Max(tempOutputY, 0.0), maxOutYRatio);
            }

            bool skipClassicAntiDead =
                SoftAntiDeadzoneProcessor.ShouldSkipClassicAntiDead(device, stickId);

            double tempXAntiDeadPercent = 0.0;
            double tempYAntiDeadPercent = 0.0;
            if (antiDead > 0 && !skipClassicAntiDead)
            {
                tempXAntiDeadPercent = (antiDead * 0.01) * Math.Abs(Math.Cos(r));
                tempYAntiDeadPercent = (antiDead * 0.01) * Math.Abs(Math.Sin(r));
            }

            if (tempOutputX > 0.0)
            {
                outX = ToStickByte((((1.0 - tempXAntiDeadPercent) * tempOutputX + tempXAntiDeadPercent)) * maxXValue + CENTER);
            }
            else
            {
                outX = 128;
            }

            if (tempOutputY > 0.0)
            {
                outY = ToStickByte((((1.0 - tempYAntiDeadPercent) * tempOutputY + tempYAntiDeadPercent)) * maxYValue + CENTER);
            }
            else
            {
                outY = 128;
            }
        }

        public static void ProcessAxialStickClean(
            int device, int stickId,
            byte inX, byte inY,
            StickDeadZoneInfo mod,
            double deltaTimeSeconds,
            out byte outX, out byte outY,
            out short preciseX, out short preciseY)
        {
            if (mod.advancedRadialProcessing)
            {
                ProcessAxialDeadzoneFloat(
                    device, stickId, inX, inY, mod, deltaTimeSeconds,
                    out double normX, out double normY);
                NormalizedToStickBytes(normX, normY, out outX, out outY);
                preciseX = ScaleNormalizedToXInput(normX);
                preciseY = ScaleNormalizedToXInput(normY);
                return;
            }

            ProcessAxialAxisClean(inX, mod.xAxisDeadInfo, out outX, out preciseX);
            ProcessAxialAxisClean(inY, mod.yAxisDeadInfo, out outY, out preciseY);
        }

        public static void ProcessAxialAxisClean(
            byte inValue,
            StickDeadZoneInfo.AxisDeadZoneInfo axisInfo,
            out byte outValue,
            out short preciseValue)
        {
            outValue = inValue;
            preciseValue = 0;

            if (axisInfo.deadZone <= 0 && axisInfo.antiDeadZone <= 0 &&
                axisInfo.maxZone == 100 && axisInfo.maxOutput == 100.0)
            {
                return;
            }

            int distVal = Math.Abs(inValue - 128);
            if (axisInfo.deadZone > 0 && distVal <= axisInfo.deadZone)
            {
                outValue = 128;
                return;
            }

            if (!((axisInfo.deadZone > 0 && distVal > axisInfo.deadZone) ||
                axisInfo.antiDeadZone > 0 || axisInfo.maxZone != 100 || axisInfo.maxOutput != 100.0))
            {
                return;
            }

            double maxAxisValue = inValue >= CENTER ? 127.0 : -128.0;
            double ratio = axisInfo.maxZone / 100.0;
            double maxOutRatio = axisInfo.maxOutput / 100.0;

            double maxZoneNegValue = (ratio * -128.0) + CENTER;
            double maxZonePosValue = (ratio * 127.0) + CENTER;
            double maxZone = inValue >= CENTER ? (maxZonePosValue - CENTER) : (maxZoneNegValue - CENTER);

            double tempDead = axisInfo.deadZone > 0 ? ((axisInfo.deadZone / 127.0) * maxAxisValue) : 0.0;
            double currentVal = Global.Clamp(maxZoneNegValue, inValue, maxZonePosValue);
            double tempOutput = (currentVal - CENTER - tempDead) / (maxZone - tempDead);

            if (axisInfo.maxOutput != 100.0)
            {
                tempOutput = Math.Min(Math.Max(tempOutput, 0.0), maxOutRatio);
            }

            double tempAntiDeadPercent = axisInfo.antiDeadZone > 0 ? axisInfo.antiDeadZone * 0.01 : 0.0;

            if (tempOutput > 0.0)
            {
                double offset = (((1.0 - tempAntiDeadPercent) * tempOutput + tempAntiDeadPercent)) * maxAxisValue;
                outValue = ToStickByte(offset + CENTER);
                preciseValue = ScaleNormalizedToXInput(offset / MAX_RADIUS);
            }
            else
            {
                outValue = 128;
            }
        }

        public static void ProcessAxialAxisLegacy(
            byte inValue,
            StickDeadZoneInfo.AxisDeadZoneInfo axisInfo,
            out byte outValue)
        {
            outValue = inValue;

            if (axisInfo.deadZone <= 0 && axisInfo.antiDeadZone <= 0 &&
                axisInfo.maxZone == 100 && axisInfo.maxOutput == 100.0)
            {
                return;
            }

            int distVal = Math.Abs(inValue - 128);
            if (axisInfo.deadZone > 0 && distVal <= axisInfo.deadZone)
            {
                outValue = 128;
                return;
            }

            if (!((axisInfo.deadZone > 0 && distVal > axisInfo.deadZone) ||
                axisInfo.antiDeadZone > 0 || axisInfo.maxZone != 100 || axisInfo.maxOutput != 100.0))
            {
                return;
            }

            double maxAxisValue = inValue >= CENTER ? 127.0 : -128.0;
            double ratio = axisInfo.maxZone / 100.0;
            double maxOutRatio = axisInfo.maxOutput / 100.0;

            double maxZoneNegValue = (ratio * -128.0) + CENTER;
            double maxZonePosValue = (ratio * 127.0) + CENTER;
            double maxZone = inValue >= CENTER ? (maxZonePosValue - CENTER) : (maxZoneNegValue - CENTER);

            double tempDead = axisInfo.deadZone > 0 ? ((axisInfo.deadZone / 127.0) * maxAxisValue) : 0.0;
            double currentVal = Global.Clamp(maxZoneNegValue, inValue, maxZonePosValue);
            double tempOutput = (currentVal - CENTER - tempDead) / (maxZone - tempDead);

            if (axisInfo.maxOutput != 100.0)
            {
                maxOutRatio = Math.Min(maxOutRatio / 0.99, 1.0);
                tempOutput = Math.Min(Math.Max(tempOutput, 0.0), maxOutRatio);
            }

            double tempAntiDeadPercent = axisInfo.antiDeadZone > 0 ? axisInfo.antiDeadZone * 0.01 : 0.0;
            if (tempOutput > 0.0)
            {
                outValue = ToStickByte((((1.0 - tempAntiDeadPercent) * tempOutput + tempAntiDeadPercent)) * maxAxisValue + CENTER);
            }
            else
            {
                outValue = 128;
            }
        }

        public static void UpdatePreciseStickFromBytes(byte stickX, byte stickY, out short preciseX, out short preciseY)
        {
            BytesToNormalized(stickX, stickY, out double normX, out double normY);
            preciseX = ScaleNormalizedToXInput(normX);
            preciseY = ScaleNormalizedToXInput(normY);
        }
    }
}
