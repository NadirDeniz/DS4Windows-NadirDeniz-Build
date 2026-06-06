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
    /// TMR / Hall-effect noise gate: zero-latency hysteresis on stick delta only.
    /// No frame averaging, no EMA, no delayed smoothing — rejects electrical jitter
    /// while intentional movement passes through on the same frame.
    /// </summary>
    internal static class TmrHallStickProcessor
    {
        private const byte CENTER = 128;
        private const double MAX_RADIUS = 127.0;
        private const int STICK_COUNT = 2;
        private const double MIN_DELTA_TIME = 0.0005;
        private const double MAX_DELTA_TIME = 0.05;
        private const double DEFAULT_DELTA_TIME = 1.0 / 125.0;

        private static readonly CommitState[][] commitState =
            new CommitState[Global.TEST_PROFILE_ITEM_COUNT][];

        static TmrHallStickProcessor()
        {
            for (int i = 0; i < Global.TEST_PROFILE_ITEM_COUNT; i++)
            {
                commitState[i] = new CommitState[STICK_COUNT];
                for (int s = 0; s < STICK_COUNT; s++)
                {
                    commitState[i][s].CommittedX = CENTER;
                    commitState[i][s].CommittedY = CENTER;
                }
            }
        }

        private struct CommitState
        {
            public byte CommittedX;
            public byte CommittedY;
            public double CommittedNormX;
            public double CommittedNormY;
            public bool Initialized;
        }

        public static void Apply(
            int device, int stickId,
            ref byte stickX, ref byte stickY,
            double deltaTimeSeconds,
            TmrHallStickInfo info)
        {
            if (!info.enabled)
            {
                return;
            }

            ref CommitState state = ref commitState[device][stickId];
            double dt = NormalizeDeltaTime(deltaTimeSeconds);

            ToNormalized(stickX, stickY, out double normX, out double normY);
            double magnitude = Math.Sqrt((normX * normX) + (normY * normY));

            if (!state.Initialized)
            {
                CommitRaw(stickX, stickY, normX, normY, ref state);
                return;
            }

            double deltaX = stickX - state.CommittedX;
            double deltaY = stickY - state.CommittedY;
            double deltaMag = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));

            double normDeltaX = normX - state.CommittedNormX;
            double normDeltaY = normY - state.CommittedNormY;
            double normDeltaMag = Math.Sqrt((normDeltaX * normDeltaX) + (normDeltaY * normDeltaY));
            double instantSpeed = normDeltaMag / dt;

            if (ShouldPassThroughRaw(stickX, stickY, magnitude, deltaMag, normDeltaMag, instantSpeed, info))
            {
                CommitRaw(stickX, stickY, normX, normY, ref state);
                return;
            }

            if (magnitude <= info.centerSnapRadius)
            {
                stickX = CENTER;
                stickY = CENTER;
                state.CommittedX = CENTER;
                state.CommittedY = CENTER;
                state.CommittedNormX = 0.0;
                state.CommittedNormY = 0.0;
            }
            else
            {
                stickX = state.CommittedX;
                stickY = state.CommittedY;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldPassThroughRaw(
            byte stickX, byte stickY,
            double magnitude, double deltaMag, double normDeltaMag,
            double instantSpeed, TmrHallStickInfo info)
        {
            if (stickX == 0 || stickX == 255 || stickY == 0 || stickY == 255)
            {
                return true;
            }

            if (instantSpeed >= info.intentSpeedThreshold)
            {
                return true;
            }

            if (normDeltaMag >= info.intentNormDeltaThreshold)
            {
                return true;
            }

            double adaptiveThreshold = ComputeAdaptiveJitterThreshold(magnitude, info);
            return deltaMag >= adaptiveThreshold;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ComputeAdaptiveJitterThreshold(double magnitude, TmrHallStickInfo info)
        {
            if (info.centerStabilityRadius <= 0.0 || info.centerThresholdBoost <= 0.0)
            {
                return info.baseJitterThreshold;
            }

            double centerRatio = Math.Clamp(magnitude / info.centerStabilityRadius, 0.0, 1.0);
            double centerFactor = 1.0 + (info.centerThresholdBoost * (1.0 - centerRatio));
            return info.baseJitterThreshold * centerFactor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CommitRaw(
            byte stickX, byte stickY,
            double normX, double normY,
            ref CommitState state)
        {
            if (Math.Sqrt((normX * normX) + (normY * normY)) <= 0.0001)
            {
                state.CommittedX = CENTER;
                state.CommittedY = CENTER;
                state.CommittedNormX = 0.0;
                state.CommittedNormY = 0.0;
            }
            else
            {
                state.CommittedX = stickX;
                state.CommittedY = stickY;
                state.CommittedNormX = normX;
                state.CommittedNormY = normY;
            }

            state.Initialized = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ToNormalized(byte stickX, byte stickY, out double normX, out double normY)
        {
            normX = (stickX - CENTER) / MAX_RADIUS;
            normY = -(stickY - CENTER) / MAX_RADIUS;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double NormalizeDeltaTime(double deltaTimeSeconds)
        {
            if (deltaTimeSeconds < MIN_DELTA_TIME || double.IsNaN(deltaTimeSeconds))
            {
                return DEFAULT_DELTA_TIME;
            }

            return Math.Min(deltaTimeSeconds, MAX_DELTA_TIME);
        }

        public static void ResetDevice(int device)
        {
            for (int i = 0; i < STICK_COUNT; i++)
            {
                ref CommitState state = ref commitState[device][i];
                state.CommittedX = CENTER;
                state.CommittedY = CENTER;
                state.CommittedNormX = 0.0;
                state.CommittedNormY = 0.0;
                state.Initialized = false;
            }
        }
    }
}
