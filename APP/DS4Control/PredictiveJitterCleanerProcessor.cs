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
    /// Single-frame predictive jitter suppression (no multi-frame EMA).
    /// Runs on calibrated bytes after TMR / before deadzone.
    /// </summary>
    internal static class PredictiveJitterCleanerProcessor
    {
        private const double CENTER = 128.0;
        private const double MAX_RADIUS = 127.0;
        private const int STICK_COUNT = 2;
        private const double MIN_DELTA_TIME = 0.0005;
        private const double MAX_DELTA_TIME = 0.05;
        private const double DEFAULT_DELTA_TIME = 1.0 / 125.0;

        private const double CENTER_MAG_THRESHOLD = 0.045;
        private const double LOW_SPEED_THRESHOLD = 0.55;
        private const double FLICK_SPEED_THRESHOLD = 2.8;
        private const double DIRECTION_CHANGE_THRESHOLD = 0.42;
        private const double JITTER_SUPPRESS = 0.72;

        private static readonly JitterState[][] stickState =
            new JitterState[Global.TEST_PROFILE_ITEM_COUNT][];

        static PredictiveJitterCleanerProcessor()
        {
            for (int i = 0; i < Global.TEST_PROFILE_ITEM_COUNT; i++)
            {
                stickState[i] = new JitterState[STICK_COUNT];
            }
        }

        private struct JitterState
        {
            public double PrevNormX;
            public double PrevNormY;
            public bool Initialized;
        }

        public static bool IsEnabledForStick(int device, int stickId)
        {
            return stickId == 0
                ? Global.getLSPredictiveJitterCleaner(device)
                : Global.getRSPredictiveJitterCleaner(device);
        }

        public static void Apply(
            int device, int stickId,
            ref byte stickX, ref byte stickY,
            double deltaTimeSeconds)
        {
            if (!IsEnabledForStick(device, stickId))
            {
                return;
            }

            StickInputProcessor.BytesToNormalized(
                stickX, stickY, out double normX, out double normY);

            ref JitterState state = ref stickState[device][stickId];
            double dt = NormalizeDeltaTime(deltaTimeSeconds);

            if (!state.Initialized)
            {
                state.PrevNormX = normX;
                state.PrevNormY = normY;
                state.Initialized = true;
                return;
            }

            double deltaX = normX - state.PrevNormX;
            double deltaY = normY - state.PrevNormY;
            double speed = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)) / dt;
            double mag = Math.Sqrt((normX * normX) + (normY * normY));

            double prevAngle = Math.Atan2(state.PrevNormY, state.PrevNormX);
            double angle = Math.Atan2(normY, normX);
            double angleDelta = Math.Abs(WrapAngleDelta(angle - prevAngle));

            bool intentionalMove = speed >= FLICK_SPEED_THRESHOLD ||
                angleDelta >= DIRECTION_CHANGE_THRESHOLD ||
                mag >= 0.82;

            if (!intentionalMove &&
                (mag <= CENTER_MAG_THRESHOLD || speed <= LOW_SPEED_THRESHOLD))
            {
                // Jitter has no real momentum: pull this sample back toward the previous committed
                // value, shrinking the per-frame deviation by JITTER_SUPPRESS. This suppresses
                // electrical noise without adding multi-frame latency (intentional movement already
                // bypassed above). The previous form extrapolated forward and amplified the jitter.
                normX -= deltaX * JITTER_SUPPRESS;
                normY -= deltaY * JITTER_SUPPRESS;
            }

            state.PrevNormX = normX;
            state.PrevNormY = normY;

            StickInputProcessor.NormalizedToStickBytes(
                normX, normY, out stickX, out stickY);
        }

        public static void ResetDevice(int device)
        {
            for (int i = 0; i < STICK_COUNT; i++)
            {
                stickState[device][i].Initialized = false;
                stickState[device][i].PrevNormX = stickState[device][i].PrevNormY = 0.0;
            }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double WrapAngleDelta(double delta)
        {
            while (delta > Math.PI)
            {
                delta -= Math.PI * 2.0;
            }

            while (delta < -Math.PI)
            {
                delta += Math.PI * 2.0;
            }

            return delta;
        }
    }
}
