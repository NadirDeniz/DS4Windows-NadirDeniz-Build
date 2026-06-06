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
    /// Reduces visible micro-aim stepping from coarse game camera quantization using
    /// radial magnitude error diffusion — not temporal input smoothing.
    /// Runs after in-game deadzone remapper, before dynamic/micro-aim.
    /// </summary>
    internal static class MicroVelocityQuantizationSmootherProcessor
    {
        private const double MIN_DELTA_TIME = 0.0005;
        private const double MAX_DELTA_TIME = 0.05;
        private const double DEFAULT_DELTA_TIME = 1.0 / 125.0;
        private const double DEFAULT_VELOCITY_BYPASS = 0.30;
        private const double MIN_STEP_SIZE = 1e-5;
        private const double RESIDUAL_CLAMP_FACTOR = 2.0;

        private struct StickRuntimeState
        {
            public double MagResidual;
            public double PrevNormX;
            public double PrevNormY;
            public bool Initialized;
        }

        private static readonly StickRuntimeState[][] stickState =
            new StickRuntimeState[Global.TEST_PROFILE_ITEM_COUNT][];

        static MicroVelocityQuantizationSmootherProcessor()
        {
            for (int i = 0; i < Global.TEST_PROFILE_ITEM_COUNT; i++)
            {
                stickState[i] = new StickRuntimeState[2];
            }
        }

        public static void ResetDevice(int device)
        {
            if (device < 0 || device >= Global.TEST_PROFILE_ITEM_COUNT)
            {
                return;
            }

            for (int i = 0; i < 2; i++)
            {
                stickState[device][i] = default;
            }
        }

        public static void ApplyRadial(
            int device, int stickId,
            ref double normX, ref double normY,
            MicroVelocityQuantizationSmootherInfo info,
            double deltaTimeSeconds)
        {
            if (!info.enabled)
            {
                return;
            }

            double mag = Math.Sqrt((normX * normX) + (normY * normY));
            ref StickRuntimeState runtime = ref stickState[device][stickId];
            double deltaTime = ClampDeltaTime(deltaTimeSeconds);

            if (!runtime.Initialized)
            {
                runtime.PrevNormX = normX;
                runtime.PrevNormY = normY;
                runtime.Initialized = true;
            }

            double range = Math.Clamp(
                info.range,
                MicroVelocityQuantizationSmootherInfo.MIN_RANGE,
                MicroVelocityQuantizationSmootherInfo.MAX_RANGE);
            double strength = Math.Clamp(
                info.strength,
                MicroVelocityQuantizationSmootherInfo.MIN_STRENGTH,
                MicroVelocityQuantizationSmootherInfo.MAX_STRENGTH);

            if (mag <= 1e-9)
            {
                runtime.MagResidual = 0.0;
                runtime.PrevNormX = normX;
                runtime.PrevNormY = normY;
                normX = normY = 0.0;
                return;
            }

            bool bypass = mag >= range ||
                ShouldBypassForVelocity(normX, normY, runtime, deltaTime);

            if (bypass)
            {
                runtime.MagResidual = 0.0;
                runtime.PrevNormX = normX;
                runtime.PrevNormY = normY;
                return;
            }

            runtime.PrevNormX = normX;
            runtime.PrevNormY = normY;

            double stepSize = Math.Max(MIN_STEP_SIZE, range * strength);
            double desiredMag = Math.Clamp(mag, 0.0, 1.0);
            double zoneBlend = ComputeZoneBlend(desiredMag, range);
            double outputMag = ApplyQuantizationDiffusion(
                desiredMag, ref runtime.MagResidual, stepSize);
            outputMag = desiredMag + ((outputMag - desiredMag) * zoneBlend);
            outputMag = Math.Clamp(outputMag, 0.0, 1.0);

            double scale = outputMag / mag;
            normX *= scale;
            normY *= scale;
        }

        public static void ApplyToState(
            int device, int stickId,
            StickDeadZoneInfo mod,
            MicroVelocityQuantizationSmootherInfo info,
            double deltaTimeSeconds,
            ref DS4State dState)
        {
            if (!info.enabled ||
                stickId != 1 ||
                mod.deadzoneType != StickDeadZoneInfo.DeadZoneType.Radial)
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
        /// Error-diffusion style magnitude quantization: carries fractional remainder
        /// between frames without averaging past inputs.
        /// </summary>
        public static double ApplyQuantizationDiffusion(
            double desiredMag, ref double magResidual, double stepSize)
        {
            desiredMag = Math.Clamp(desiredMag, 0.0, 1.0);
            if (desiredMag <= 1e-9)
            {
                magResidual = 0.0;
                return 0.0;
            }

            double adjusted = desiredMag + magResidual;
            double steps = adjusted / stepSize;
            long roundedSteps = (long)Math.Round(steps, MidpointRounding.AwayFromZero);
            double quantized = roundedSteps * stepSize;
            magResidual = adjusted - quantized;

            double residualClamp = stepSize * RESIDUAL_CLAMP_FACTOR;
            magResidual = Math.Clamp(magResidual, -residualClamp, residualClamp);

            return Math.Clamp(quantized, 0.0, 1.0);
        }

        /// <summary>
        /// Fades diffusion in near the outer edge of the active range to avoid a seam.
        /// </summary>
        public static double ComputeZoneBlend(double magnitude, double range)
        {
            magnitude = Math.Clamp(magnitude, 0.0, range);
            if (range <= 1e-9)
            {
                return 0.0;
            }

            double taperStart = range * 0.65;
            if (magnitude <= taperStart)
            {
                return 1.0;
            }

            double t = (range - magnitude) / Math.Max(range - taperStart, 1e-6);
            return SmoothStep(Math.Clamp(t, 0.0, 1.0));
        }

        private static bool ShouldBypassForVelocity(
            double normX, double normY,
            StickRuntimeState runtime,
            double deltaTime)
        {
            double deltaX = normX - runtime.PrevNormX;
            double deltaY = normY - runtime.PrevNormY;
            double displacement = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            double instantSpeed = displacement / deltaTime;
            return instantSpeed > DEFAULT_VELOCITY_BYPASS;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double SmoothStep(double t)
        {
            t = Math.Clamp(t, 0.0, 1.0);
            return t * t * (3.0 - (2.0 * t));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ClampDeltaTime(double deltaTimeSeconds)
        {
            if (deltaTimeSeconds < MIN_DELTA_TIME)
            {
                return DEFAULT_DELTA_TIME;
            }

            return Math.Min(deltaTimeSeconds, MAX_DELTA_TIME);
        }
    }
}
