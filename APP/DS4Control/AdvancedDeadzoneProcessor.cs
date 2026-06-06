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
    /// Radial deadzone with circular geometry, independent inner/outer zones,
    /// adaptive anti-deadzone, dynamic center calibration, and velocity-based
    /// inner-zone scaling. Preserves stick angle for diagonal consistency.
    /// </summary>
    internal static class AdvancedDeadzoneProcessor
    {
        private const double CENTER = 128.0;
        private const double MAX_RADIUS = 127.0;
        private const int STICK_COUNT = 2;
        private const double MIN_DELTA_TIME = 0.0005;
        private const double MAX_DELTA_TIME = 0.05;
        private const double DEFAULT_DELTA_TIME = 1.0 / 125.0;
        private const double IDLE_MAG_THRESHOLD = 0.06;
        private const double IDLE_SPEED_THRESHOLD = 0.35;
        private const double FAST_SPEED_REFERENCE = 7.5;
        private const double INNER_SCALE_SLOW = 1.10;
        private const double INNER_SCALE_FAST = 0.88;

        private static readonly StickRuntimeState[][] stickState =
            new StickRuntimeState[Global.TEST_PROFILE_ITEM_COUNT][];

        static AdvancedDeadzoneProcessor()
        {
            for (int i = 0; i < Global.TEST_PROFILE_ITEM_COUNT; i++)
            {
                stickState[i] = new StickRuntimeState[STICK_COUNT];
            }
        }

        private struct StickRuntimeState
        {
            public double CenterOffsetX;
            public double CenterOffsetY;
            public double PrevMag;
            public bool CenterInitialized;
        }

        public static void ProcessRadial(
            int device, int stickId,
            byte inX, byte inY,
            StickDeadZoneInfo mod,
            double deltaTimeSeconds,
            bool skipClassicAntiDeadzone,
            out double normX, out double normY)
        {
            double dx = inX - CENTER;
            double dy = -(inY - CENTER);

            ref StickRuntimeState runtime = ref stickState[device][stickId];
            if (mod.dynamicCenterCalibration)
            {
                ApplyDynamicCenterCalibration(
                    ref runtime, ref dx, ref dy, mod.centerCalStrength, deltaTimeSeconds);
            }

            double mag = Math.Sqrt((dx * dx) + (dy * dy));
            double magNorm = mag / MAX_RADIUS;

            double innerNorm = Math.Clamp(mod.deadZone / MAX_RADIUS, 0.0, 0.98);
            double outerNorm = Math.Clamp(mod.maxZone / 100.0, innerNorm + 0.01, 1.0);
            double outerRingNorm = Math.Clamp(mod.outerDeadzone / MAX_RADIUS, 0.0, outerNorm - innerNorm - 0.01);
            double activeOuterNorm = Math.Max(innerNorm + 0.01, outerNorm - outerRingNorm);

            if (mod.dynamicDeadzoneScaling)
            {
                innerNorm = ApplyDynamicInnerScale(
                    ref runtime, innerNorm, magNorm, deltaTimeSeconds);
            }

            if (magNorm <= 1e-9)
            {
                normX = normY = 0.0;
                runtime.PrevMag = 0.0;
                return;
            }

            double angle = Math.Atan2(dy, dx);
            double remappedMag = RemapCircularMagnitude(
                magNorm, innerNorm, activeOuterNorm, mod.innerZoneSoftness);

            if (remappedMag <= 1e-9)
            {
                normX = normY = 0.0;
                runtime.PrevMag = magNorm;
                return;
            }

            // RemapCircularMagnitude returns a value in [0, activeOuterNorm - innerNorm], so the
            // normalized output must be divided by that active range (NOT by activeOuterNorm) to
            // reach 1.0 at full tilt. Dividing by activeOuterNorm capped the maximum output at the
            // active range, e.g. a deadzone or sub-100 maxZone would prevent ever reaching 100%.
            double activeRange = activeOuterNorm - innerNorm;
            double outputT = activeRange > 1e-9 ? remappedMag / activeRange : 0.0;
            outputT = Math.Clamp(outputT, 0.0, 1.0);

            if (mod.maxOutput != 100.0 || mod.maxOutputForce)
            {
                outputT = Math.Min(outputT, mod.maxOutput / 100.0);
            }

            if (!skipClassicAntiDeadzone)
            {
                outputT = ApplyAntiDeadzone(outputT, mod, mod.adaptiveAntiDeadzone);
            }

            double outMagNorm = outputT;
            double outDx = Math.Cos(angle) * outMagNorm * MAX_RADIUS;
            double outDy = Math.Sin(angle) * outMagNorm * MAX_RADIUS;

            if (mod.verticalScale != StickDeadZoneInfo.DEFAULT_VERTICAL_SCALE)
            {
                outDy *= mod.verticalScale / 100.0;
            }

            normX = outDx / MAX_RADIUS;
            normY = outDy / MAX_RADIUS;
            runtime.PrevMag = magNorm;
        }

        /// <summary>Advanced radial deadzone on an established normalized vector (true 16-bit path).</summary>
        public static void ProcessRadialFromNormalized(
            int device, int stickId,
            ref double normX, ref double normY,
            StickDeadZoneInfo mod,
            double deltaTimeSeconds,
            bool skipClassicAntiDeadzone)
        {
            double dx = normX * MAX_RADIUS;
            double dy = normY * MAX_RADIUS;

            ref StickRuntimeState runtime = ref stickState[device][stickId];
            if (mod.dynamicCenterCalibration)
            {
                ApplyDynamicCenterCalibration(
                    ref runtime, ref dx, ref dy, mod.centerCalStrength, deltaTimeSeconds);
            }

            double mag = Math.Sqrt((dx * dx) + (dy * dy));
            double magNorm = mag / MAX_RADIUS;

            double innerNorm = Math.Clamp(mod.deadZone / MAX_RADIUS, 0.0, 0.98);
            double outerNorm = Math.Clamp(mod.maxZone / 100.0, innerNorm + 0.01, 1.0);
            double outerRingNorm = Math.Clamp(mod.outerDeadzone / MAX_RADIUS, 0.0, outerNorm - innerNorm - 0.01);
            double activeOuterNorm = Math.Max(innerNorm + 0.01, outerNorm - outerRingNorm);

            if (mod.dynamicDeadzoneScaling)
            {
                innerNorm = ApplyDynamicInnerScale(
                    ref runtime, innerNorm, magNorm, deltaTimeSeconds);
            }

            if (magNorm <= 1e-9)
            {
                normX = normY = 0.0;
                runtime.PrevMag = 0.0;
                return;
            }

            double angle = Math.Atan2(dy, dx);
            double remappedMag = RemapCircularMagnitude(
                magNorm, innerNorm, activeOuterNorm, mod.innerZoneSoftness);

            if (remappedMag <= 1e-9)
            {
                normX = normY = 0.0;
                runtime.PrevMag = magNorm;
                return;
            }

            double activeRange = activeOuterNorm - innerNorm;
            double outputT = activeRange > 1e-9 ? remappedMag / activeRange : 0.0;
            outputT = Math.Clamp(outputT, 0.0, 1.0);

            if (mod.maxOutput != 100.0 || mod.maxOutputForce)
            {
                outputT = Math.Min(outputT, mod.maxOutput / 100.0);
            }

            if (!skipClassicAntiDeadzone)
            {
                outputT = ApplyAntiDeadzone(outputT, mod, mod.adaptiveAntiDeadzone);
            }

            double outMagNorm = outputT;
            double outDx = Math.Cos(angle) * outMagNorm * MAX_RADIUS;
            double outDy = Math.Sin(angle) * outMagNorm * MAX_RADIUS;

            if (mod.verticalScale != StickDeadZoneInfo.DEFAULT_VERTICAL_SCALE)
            {
                outDy *= mod.verticalScale / 100.0;
            }

            normX = outDx / MAX_RADIUS;
            normY = outDy / MAX_RADIUS;
            runtime.PrevMag = magNorm;
        }

        /// <summary>Axial path with soft inner edge and independent outer cap (per axis).</summary>
        public static void ProcessAxialAxis(
            int device, int stickId, bool isYAxis,
            byte inValue,
            StickDeadZoneInfo.AxisDeadZoneInfo axisInfo,
            StickDeadZoneInfo mod,
            double deltaTimeSeconds,
            out double normValue)
        {
            double offset = inValue - CENTER;
            if (isYAxis)
            {
                offset = -(inValue - CENTER);
            }

            ref StickRuntimeState runtime = ref stickState[device][stickId];
            if (mod.dynamicCenterCalibration)
            {
                double centerComponent = isYAxis ? -runtime.CenterOffsetY : runtime.CenterOffsetX;
                offset -= centerComponent;
                UpdateAxialCenter(ref runtime, isYAxis, inValue, mod.centerCalStrength);
            }

            double cap = offset >= 0.0 ? 127.0 : 128.0;
            double magNorm = Math.Abs(offset / cap);

            double innerNorm = Math.Clamp(axisInfo.deadZone / MAX_RADIUS, 0.0, 0.98);
            double outerNorm = Math.Clamp(axisInfo.maxZone / 100.0, innerNorm + 0.01, 1.0);
            double outerRingNorm = Math.Clamp(mod.outerDeadzone / MAX_RADIUS, 0.0, outerNorm - innerNorm - 0.01);
            double activeOuterNorm = Math.Max(innerNorm + 0.01, outerNorm - outerRingNorm);

            double remapped = RemapCircularMagnitude(
                magNorm, innerNorm, activeOuterNorm, mod.innerZoneSoftness);

            if (remapped <= 1e-9)
            {
                normValue = 0.0;
                return;
            }

            // Normalize by the active range so full tilt reaches 1.0 (see ProcessRadial); dividing
            // by activeOuterNorm capped the per-axis output at the active range when a deadzone or
            // sub-100 maxZone was configured.
            double activeRange = activeOuterNorm - innerNorm;
            double outputT = activeRange > 1e-9
                ? Math.Clamp(remapped / activeRange, 0.0, 1.0)
                : 0.0;
            if (axisInfo.maxOutput != 100.0)
            {
                outputT = Math.Min(outputT, axisInfo.maxOutput / 100.0);
            }

            double baseAnti = axisInfo.antiDeadZone * 0.01;
            double antiEff = mod.adaptiveAntiDeadzone
                ? baseAnti * SmoothStep(outputT)
                : baseAnti;
            outputT = antiEff + ((1.0 - antiEff) * outputT);

            normValue = Math.Sign(offset) * outputT;
        }

        private static void ApplyDynamicCenterCalibration(
            ref StickRuntimeState runtime,
            ref double dx, ref double dy,
            double strength,
            double deltaTimeSeconds)
        {
            double magNorm = Math.Sqrt(dx * dx + dy * dy) / MAX_RADIUS;
            double dt = NormalizeDeltaTime(deltaTimeSeconds);
            double speed = Math.Abs(magNorm - runtime.PrevMag) / dt;

            if (magNorm < IDLE_MAG_THRESHOLD && speed < IDLE_SPEED_THRESHOLD)
            {
                double alpha = Math.Clamp(strength * dt * 125.0, 0.02, 0.45);
                if (!runtime.CenterInitialized)
                {
                    runtime.CenterOffsetX = dx;
                    runtime.CenterOffsetY = dy;
                    runtime.CenterInitialized = true;
                }
                else
                {
                    runtime.CenterOffsetX += (dx - runtime.CenterOffsetX) * alpha;
                    runtime.CenterOffsetY += (dy - runtime.CenterOffsetY) * alpha;
                }
            }

            dx -= runtime.CenterOffsetX;
            dy -= runtime.CenterOffsetY;
        }

        private static void UpdateAxialCenter(
            ref StickRuntimeState runtime, bool isYAxis, byte inValue, double strength)
        {
            double target = inValue - CENTER;
            double alpha = Math.Clamp(strength * 0.15, 0.02, 0.35);
            if (isYAxis)
            {
                runtime.CenterOffsetY += (target - runtime.CenterOffsetY) * alpha;
            }
            else
            {
                runtime.CenterOffsetX += (target - runtime.CenterOffsetX) * alpha;
            }

            runtime.CenterInitialized = true;
        }

        private static double ApplyDynamicInnerScale(
            ref StickRuntimeState runtime,
            double innerNorm,
            double magNorm,
            double deltaTimeSeconds)
        {
            double dt = NormalizeDeltaTime(deltaTimeSeconds);
            double speed = Math.Abs(magNorm - runtime.PrevMag) / dt;
            double speedT = Math.Clamp(speed / FAST_SPEED_REFERENCE, 0.0, 1.0);
            double scale = INNER_SCALE_SLOW + ((INNER_SCALE_FAST - INNER_SCALE_SLOW) * speedT);
            return Math.Clamp(innerNorm * scale, 0.0, 0.98);
        }

        /// <summary>
        /// Map input magnitude through inner deadzone (soft) and outer boundary without changing angle.
        /// </summary>
        private static double RemapCircularMagnitude(
            double magNorm, double innerNorm, double outerNorm, double softness)
        {
            softness = Math.Clamp(softness, 0.0, 0.45);
            double range = outerNorm - innerNorm;
            if (range < 1e-6)
            {
                return 0.0;
            }

            if (magNorm >= outerNorm)
            {
                return outerNorm - innerNorm;
            }

            double softEdge = innerNorm * (1.0 - softness);
            if (magNorm <= softEdge)
            {
                return 0.0;
            }

            double linearInput;
            if (magNorm <= innerNorm)
            {
                double softRange = innerNorm - softEdge;
                double t = (magNorm - softEdge) / softRange;
                linearInput = SmoothStep(t) * (magNorm - softEdge);
            }
            else
            {
                linearInput = magNorm - innerNorm;
            }

            return Math.Clamp(linearInput, 0.0, range);
        }

        private static double ApplyAntiDeadzone(
            double outputT, StickDeadZoneInfo mod, bool adaptive)
        {
            double baseAnti = Math.Clamp(mod.antiDeadZone * 0.01, 0.0, 0.95);
            if (baseAnti <= 1e-9)
            {
                return outputT;
            }

            double antiEff = adaptive
                ? baseAnti * SmoothStep(outputT)
                : baseAnti;

            return antiEff + ((1.0 - antiEff) * outputT);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double SmoothStep(double t)
        {
            t = Math.Clamp(t, 0.0, 1.0);
            return t * t * (3.0 - (2.0 * t));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double NormalizeDeltaTime(double deltaTimeSeconds)
        {
            if (deltaTimeSeconds < MIN_DELTA_TIME)
            {
                return DEFAULT_DELTA_TIME;
            }

            return Math.Clamp(deltaTimeSeconds, MIN_DELTA_TIME, MAX_DELTA_TIME);
        }

        public static void ResetDevice(int device)
        {
            for (int i = 0; i < STICK_COUNT; i++)
            {
                stickState[device][i].CenterOffsetX = 0.0;
                stickState[device][i].CenterOffsetY = 0.0;
                stickState[device][i].PrevMag = 0.0;
                stickState[device][i].CenterInitialized = false;
            }
        }
    }
}
