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
    /// Adaptive low-range stick response for micro-aim precision.
    /// Pipeline stage: after user deadzone/anti-deadzone, before user output curves.
    /// First ~15% prioritizes controllability; transitions to linear by ~20%.
    /// </summary>
    internal static class MicroAimPrecisionProcessor
    {
        private const double CENTER = 128.0;
        private const double MAX_RADIUS = 127.0;
        private const int STICK_COUNT = 2;
        private const int AXIS_COUNT = 4;

        // [device][stick 0=LS,1=RS] normalized output after micro-aim curve
        private static readonly MicroAimStickFloatState[][] floatState =
            new MicroAimStickFloatState[Global.TEST_PROFILE_ITEM_COUNT][];

        // [device][0=LX,1=LY,2=RX,3=RY] sub-pixel remainder for int16 output
        private static readonly double[][] subPixelRemainder =
            new double[Global.TEST_PROFILE_ITEM_COUNT][];

        static MicroAimPrecisionProcessor()
        {
            for (int i = 0; i < Global.TEST_PROFILE_ITEM_COUNT; i++)
            {
                floatState[i] = new MicroAimStickFloatState[STICK_COUNT];
                subPixelRemainder[i] = new double[AXIS_COUNT];
            }
        }

        private struct MicroAimStickFloatState
        {
            public double NormX;
            public double NormY;
            public bool Active;
        }

        /// <summary>
        /// Adaptive curve: precision zone (power law), smooth blend to linear, then linear.
        /// </summary>
        public static void FinalizeSubPixelOutput(
            int device, int stickId,
            byte stickX, byte stickY,
            out short preciseX, out short preciseY)
        {
            int axisBase = stickId == 0 ? 0 : 2;
            HighPrecisionNormalize(stickX, stickY, out double normX, out double normY);
            preciseX = ScaleWithSubPixel(device, axisBase, normX);
            preciseY = ScaleWithSubPixel(device, axisBase + 1, normY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ApplyAdaptiveLowRangeCurve(double magnitude, MicroAimPrecisionInfo info)
        {
            return ApplyAdaptiveLowRangeCurveParams(
                magnitude, info.precisionZone, info.transitionZone, info.precisionExponent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ApplyAdaptiveLowRangeCurveParams(
            double magnitude, double precisionEnd, double transitionEnd, double exponent)
        {
            magnitude = Math.Clamp(magnitude, 0.0, 1.0);
            if (magnitude <= 0.0)
            {
                return 0.0;
            }

            if (precisionEnd <= 0.0 || transitionEnd <= precisionEnd)
            {
                return magnitude;
            }

            if (magnitude <= precisionEnd)
            {
                double ratio = magnitude / precisionEnd;
                return precisionEnd * Math.Pow(ratio, exponent);
            }

            if (magnitude <= transitionEnd)
            {
                double powerValue = precisionEnd * Math.Pow(magnitude / precisionEnd, exponent);
                double blend = SmoothStep((magnitude - precisionEnd) / (transitionEnd - precisionEnd));
                return powerValue + ((magnitude - powerValue) * blend);
            }

            return magnitude;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double SmoothStep(double t)
        {
            t = Math.Clamp(t, 0.0, 1.0);
            return t * t * (3.0 - (2.0 * t));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void HighPrecisionNormalize(byte stickX, byte stickY,
            out double normX, out double normY)
        {
            double dx = stickX - CENTER;
            double dy = -(stickY - CENTER);
            normX = dx / MAX_RADIUS;
            normY = dy / MAX_RADIUS;
        }

        public static void ApplyToNormalizedVector(
            ref double normX, ref double normY, MicroAimPrecisionInfo info)
        {
            double mag = Math.Sqrt((normX * normX) + (normY * normY));
            if (mag <= 1e-9)
            {
                normX = normY = 0.0;
                return;
            }

            double newMag = ApplyAdaptiveLowRangeCurve(mag, info);
            double scale = newMag / mag;
            normX *= scale;
            normY *= scale;
        }

        public static bool TryGetFloatState(
            int device, int stickId, out double normX, out double normY)
        {
            ref MicroAimStickFloatState state = ref floatState[device][stickId];
            if (!state.Active)
            {
                normX = normY = 0.0;
                return false;
            }

            normX = state.NormX;
            normY = state.NormY;
            return true;
        }

        public static void StoreFloatState(
            int device, int stickId, double normX, double normY)
        {
            ref MicroAimStickFloatState state = ref floatState[device][stickId];
            state.NormX = normX;
            state.NormY = normY;
            state.Active = Math.Abs(normX) > 1e-9 || Math.Abs(normY) > 1e-9;
        }

        public static void ApplyPostDeadzoneFloat(
            int device, int stickId,
            ref double normX, ref double normY,
            MicroAimPrecisionInfo info)
        {
            if (!info.enabled)
            {
                floatState[device][stickId].Active = false;
                return;
            }

            ApplyToNormalizedVector(ref normX, ref normY, info);
            StoreFloatState(device, stickId, normX, normY);
        }

        public static void ApplyPostDeadzone(
            int device, int stickId,
            byte stickX, byte stickY,
            MicroAimPrecisionInfo info,
            out byte outX, out byte outY)
        {
            outX = stickX;
            outY = stickY;

            if (!info.enabled)
            {
                floatState[device][stickId].Active = false;
                return;
            }

            HighPrecisionNormalize(stickX, stickY, out double normX, out double normY);
            ApplyPostDeadzoneFloat(device, stickId, ref normX, ref normY, info);

            StickInputProcessor.NormalizedToStickBytes(normX, normY, out outX, out outY);
        }

        public static void ApplyPostDeadzoneAxial(
            int device, int stickId,
            byte stickX, byte stickY,
            MicroAimPrecisionInfo info,
            out byte outX, out byte outY)
        {
            outX = stickX;
            outY = stickY;

            if (!info.enabled)
            {
                floatState[device][stickId].Active = false;
                return;
            }

            ProcessAxialAxis(stickX, out outX, info);
            ProcessAxialAxis(stickY, out outY, info);

            HighPrecisionNormalize(outX, outY, out double normX, out double normY);
            ref MicroAimStickFloatState state = ref floatState[device][stickId];
            state.NormX = normX;
            state.NormY = normY;
            state.Active = normX != 0.0 || normY != 0.0;
        }

        private static void ProcessAxialAxis(byte inValue, out byte outValue, MicroAimPrecisionInfo info)
        {
            outValue = inValue;
            double offset = inValue - CENTER;
            if (Math.Abs(offset) < 1e-9)
            {
                return;
            }

            double cap = offset >= 0.0 ? 127.0 : 128.0;
            double normalized = Math.Abs(offset / cap);
            double remapped = ApplyAdaptiveLowRangeCurve(normalized, info);
            double outOffset = Math.Sign(offset) * remapped * Math.Abs(cap);
            outValue = StickInputProcessor.ToStickByte(CENTER + outOffset);
        }

        /// <summary>
        /// Final high-precision XInput conversion with sub-pixel accumulation.
        /// </summary>
        public static void FinalizePreciseOutput(
            int device, int stickId,
            byte stickX, byte stickY,
            MicroAimPrecisionInfo info,
            out short preciseX, out short preciseY)
        {
            int axisBase = stickId == 0 ? 0 : 2;

            if (!info.enabled)
            {
                StickInputProcessor.UpdatePreciseStickFromBytes(
                    stickX, stickY, out preciseX, out preciseY);
                return;
            }

            HighPrecisionNormalize(stickX, stickY, out double normX, out double normY);
            preciseX = ScaleWithSubPixel(device, axisBase, normX);
            preciseY = ScaleWithSubPixel(device, axisBase + 1, normY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static short ScaleWithSubPixel(int device, int axisIndex, double normalized)
        {
            return StickOutputQuantizer.QuantizeNormalized(device, axisIndex, normalized);
        }

        public static void ResetDevice(int device)
        {
            for (int i = 0; i < STICK_COUNT; i++)
            {
                floatState[device][i].Active = false;
                floatState[device][i].NormX = 0.0;
                floatState[device][i].NormY = 0.0;
            }

            for (int i = 0; i < AXIS_COUNT; i++)
            {
                subPixelRemainder[device][i] = 0.0;
            }
        }
    }
}
