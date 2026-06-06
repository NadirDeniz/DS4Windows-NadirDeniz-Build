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
    /// Game-deadzone-aware radial remapper: lifts post-deadzone stick output just above the
    /// game's internal deadzone threshold while preserving micro-aim and full deflection.
    /// Runs after physical deadzone removal and soft center exit, before dynamic/micro-aim.
    /// </summary>
    internal static class InGameDeadzoneRemapperProcessor
    {
        public static bool ShouldSkipClassicAntiDead(int device, int stickId)
        {
            if (stickId != 1)
            {
                return false;
            }

            return Global.GetRSInGameDeadzoneRemapperInfo(device).enabled;
        }

        /// <summary>
        /// Apply in-game deadzone remapping on normalized stick vector; preserves angle.
        /// </summary>
        public static void ApplyRadial(
            ref double normX, ref double normY,
            InGameDeadzoneRemapperInfo info)
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
            double outputT = ComputeRemappedMagnitude(inputT, info);
            outputT = Math.Clamp(outputT, 0.0, 1.0);

            double scale = outputT / mag;
            normX *= scale;
            normY *= scale;
        }

        public static void ApplyToState(
            int device, int stickId,
            StickDeadZoneInfo mod,
            InGameDeadzoneRemapperInfo info,
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

            ApplyRadial(ref normX, ref normY, info);

            WriteStickNormalized(device, stickId, ref dState, normX, normY);
        }

        /// <summary>
        /// g = game deadzone; shaped = pow(m, exponent); out = g + shaped * (1 - g).
        /// Optional soft entry eases from zero into the remapped curve over entryRange.
        /// </summary>
        public static double ComputeRemappedMagnitude(
            double inputT, InGameDeadzoneRemapperInfo info)
        {
            inputT = Math.Clamp(inputT, 0.0, 1.0);
            if (inputT <= 1e-9)
            {
                return 0.0;
            }

            double gameDeadzone = Math.Clamp(
                info.gameDeadzoneSize,
                InGameDeadzoneRemapperInfo.MIN_GAME_DEADZONE_SIZE,
                InGameDeadzoneRemapperInfo.MAX_GAME_DEADZONE_SIZE);
            double exponent = Math.Clamp(
                info.exponent,
                InGameDeadzoneRemapperInfo.MIN_EXPONENT,
                InGameDeadzoneRemapperInfo.MAX_EXPONENT);

            double shaped = Math.Pow(inputT, exponent);
            double remapped = gameDeadzone + (shaped * (1.0 - gameDeadzone));

            if (info.softEntry)
            {
                double entryRange = Math.Clamp(
                    info.entryRange,
                    InGameDeadzoneRemapperInfo.MIN_ENTRY_RANGE,
                    InGameDeadzoneRemapperInfo.MAX_ENTRY_RANGE);

                if (inputT < entryRange)
                {
                    double t = inputT / entryRange;
                    t = SmoothStep(t);
                    remapped *= t;
                }
            }

            return Math.Clamp(remapped, 0.0, 1.0);
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
    }
}
