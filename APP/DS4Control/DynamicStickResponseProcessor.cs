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
    /// Velocity-adaptive stick response: precision curve when slow, balanced at medium
    /// speed, near-linear when fast. Uses stick speed, acceleration, and displacement history.
    /// </summary>
    internal static class DynamicStickResponseProcessor
    {
        private const double CENTER = 128.0;
        private const double MAX_RADIUS = 127.0;
        private const int STICK_COUNT = 2;
        private const int SPEED_HISTORY_LEN = 8;
        private const double MIN_DELTA_TIME = 0.0005;
        private const double MAX_DELTA_TIME = 0.05;
        private const double DEFAULT_DELTA_TIME = 1.0 / 125.0;

        private static readonly VelocityState[][] velocityState =
            new VelocityState[Global.TEST_PROFILE_ITEM_COUNT][];

        static DynamicStickResponseProcessor()
        {
            for (int i = 0; i < Global.TEST_PROFILE_ITEM_COUNT; i++)
            {
                velocityState[i] = new VelocityState[STICK_COUNT];
                for (int s = 0; s < STICK_COUNT; s++)
                {
                    velocityState[i][s].SpeedHistory = new double[SPEED_HISTORY_LEN];
                }
            }
        }

        private struct VelocityState
        {
            public double PrevNormX;
            public double PrevNormY;
            public double PrevSpeed;
            public double SmoothedSpeed;
            public double PeakSpeed;
            public double[] SpeedHistory;
            public int HistoryIndex;
            public int HistoryCount;
            public bool Initialized;
            public double PrevAngle;
            public double SmoothedPhysicsBlend;
            public bool PhysicsInitialized;
        }

        public static void ApplyPostDeadzone(
            int device, int stickId,
            byte stickX, byte stickY,
            double deltaTimeSeconds,
            DynamicStickResponseInfo dynamicInfo,
            MicroAimPrecisionInfo precisionInfo,
            out byte outX, out byte outY)
        {
            outX = stickX;
            outY = stickY;

            if (!dynamicInfo.enabled)
            {
                return;
            }

            MicroAimPrecisionProcessor.HighPrecisionNormalize(
                stickX, stickY, out double normX, out double normY);

            double dt = NormalizeDeltaTime(deltaTimeSeconds);
            double speedBlend = ComputeSpeedBlend(device, stickId, normX, normY, dt, dynamicInfo);
            ApplyVelocityAdaptiveCurve(
                ref normX, ref normY, speedBlend, dynamicInfo, precisionInfo);

            MicroAimPrecisionProcessor.StoreFloatState(device, stickId, normX, normY);
            StickInputProcessor.NormalizedToStickBytes(normX, normY, out outX, out outY);
        }

        public static void ApplyPostDeadzoneFloat(
            int device, int stickId,
            ref double normX, ref double normY,
            double deltaTimeSeconds,
            DynamicStickResponseInfo dynamicInfo,
            MicroAimPrecisionInfo precisionInfo)
        {
            ApplyPostDeadzoneFloat(
                device, stickId, ref normX, ref normY, deltaTimeSeconds,
                dynamicInfo, precisionInfo, usePhysics: false);
        }

        public static void ApplyPostDeadzoneFloat(
            int device, int stickId,
            ref double normX, ref double normY,
            double deltaTimeSeconds,
            DynamicStickResponseInfo dynamicInfo,
            MicroAimPrecisionInfo precisionInfo,
            bool usePhysics)
        {
            if (!dynamicInfo.enabled)
            {
                return;
            }

            double dt = NormalizeDeltaTime(deltaTimeSeconds);
            double speedBlend = usePhysics
                ? ComputePhysicsSpeedBlend(device, stickId, normX, normY, dt, dynamicInfo)
                : ComputeSpeedBlend(device, stickId, normX, normY, dt, dynamicInfo);
            ApplyVelocityAdaptiveCurve(
                ref normX, ref normY, speedBlend, dynamicInfo, precisionInfo);
            MicroAimPrecisionProcessor.StoreFloatState(device, stickId, normX, normY);
        }

        public static void ApplyPostDeadzoneAxial(
            int device, int stickId,
            byte stickX, byte stickY,
            double deltaTimeSeconds,
            DynamicStickResponseInfo dynamicInfo,
            MicroAimPrecisionInfo precisionInfo,
            out byte outX, out byte outY)
        {
            ApplyPostDeadzone(
                device, stickId, stickX, stickY, deltaTimeSeconds,
                dynamicInfo, precisionInfo, out outX, out outY);
        }

        public static void FinalizePreciseOutput(
            int device, int stickId,
            byte stickX, byte stickY,
            out short preciseX, out short preciseY)
        {
            MicroAimPrecisionProcessor.FinalizeSubPixelOutput(
                device, stickId, stickX, stickY, out preciseX, out preciseY);
        }

        public static void ResetDevice(int device)
        {
            for (int i = 0; i < STICK_COUNT; i++)
            {
                ref VelocityState state = ref velocityState[device][i];
                state.Initialized = false;
                state.PhysicsInitialized = false;
                state.PrevNormX = state.PrevNormY = 0.0;
                state.PrevSpeed = state.SmoothedSpeed = state.PeakSpeed = 0.0;
                state.PrevAngle = state.SmoothedPhysicsBlend = 0.0;
                state.HistoryIndex = state.HistoryCount = 0;
                if (state.SpeedHistory != null)
                {
                    Array.Clear(state.SpeedHistory, 0, state.SpeedHistory.Length);
                }
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

        private static double ComputeSpeedBlend(
            int device, int stickId,
            double normX, double normY,
            double deltaTime,
            DynamicStickResponseInfo info)
        {
            ref VelocityState state = ref velocityState[device][stickId];

            if (!state.Initialized)
            {
                state.PrevNormX = normX;
                state.PrevNormY = normY;
                state.PrevSpeed = 0.0;
                state.SmoothedSpeed = 0.0;
                state.PeakSpeed = 0.0;
                state.Initialized = true;
                return 0.0;
            }

            double deltaX = normX - state.PrevNormX;
            double deltaY = normY - state.PrevNormY;
            double displacement = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            double instantSpeed = displacement / deltaTime;

            double acceleration = (instantSpeed - state.PrevSpeed) / deltaTime;
            state.SmoothedSpeed = (state.SmoothedSpeed * info.speedSmoothing) +
                (instantSpeed * (1.0 - info.speedSmoothing));

            state.PeakSpeed = Math.Max(instantSpeed,
                state.PeakSpeed * Math.Pow(info.peakDecay, deltaTime * 1000.0));

            state.SpeedHistory[state.HistoryIndex] = instantSpeed;
            state.HistoryIndex = (state.HistoryIndex + 1) % SPEED_HISTORY_LEN;
            if (state.HistoryCount < SPEED_HISTORY_LEN)
            {
                state.HistoryCount++;
            }

            double historyAvg = 0.0;
            for (int i = 0; i < state.HistoryCount; i++)
            {
                historyAvg += state.SpeedHistory[i];
            }
            historyAvg /= state.HistoryCount;

            double effectiveSpeed = Math.Max(state.SmoothedSpeed,
                (historyAvg * info.historyWeight) + (state.PeakSpeed * (1.0 - info.historyWeight)));

            state.PrevNormX = normX;
            state.PrevNormY = normY;
            state.PrevSpeed = instantSpeed;

            double slowThreshold = info.slowSpeedThreshold;
            double fastThreshold = Math.Max(info.fastSpeedThreshold, slowThreshold + 0.001);
            double speedT = (effectiveSpeed - slowThreshold) / (fastThreshold - slowThreshold);
            speedT = SmoothStep(Math.Clamp(speedT, 0.0, 1.0));

            double accelInfluence = 0.0;
            if (acceleration > 0.0)
            {
                accelInfluence = Math.Min(acceleration * info.accelerationGain, info.maxAccelBoost);
            }

            return Math.Clamp(speedT + accelInfluence, 0.0, 1.0);
        }

        /// <summary>
        /// Physics-aware blend: magnitude, velocity, acceleration, direction change, angular velocity.
        /// Damps spikes on fast direction reversals; inertia on blend transitions.
        /// </summary>
        private static double ComputePhysicsSpeedBlend(
            int device, int stickId,
            double normX, double normY,
            double deltaTime,
            DynamicStickResponseInfo info)
        {
            double baseBlend = ComputeSpeedBlend(device, stickId, normX, normY, deltaTime, info);
            ref VelocityState state = ref velocityState[device][stickId];

            double mag = Math.Sqrt((normX * normX) + (normY * normY));
            double angle = Math.Atan2(normY, normX);
            double angVel = 0.0;

            if (state.PhysicsInitialized)
            {
                angVel = Math.Abs(WrapAngleDelta(angle - state.PrevAngle)) / deltaTime;
            }

            state.PrevAngle = angle;

            double directionSpike = angVel > 2.5 ? Math.Min((angVel - 2.5) * 0.12, 0.45) : 0.0;
            double magPrecisionBias = mag < 0.22 ? (0.22 - mag) * 0.35 : 0.0;
            double targetBlend = baseBlend - directionSpike - magPrecisionBias;
            targetBlend = Math.Clamp(targetBlend, 0.0, 1.0);

            double inertia = 0.14 + (0.38 * (1.0 - Math.Min(mag, 1.0)));
            if (!state.PhysicsInitialized)
            {
                state.SmoothedPhysicsBlend = targetBlend;
                state.PhysicsInitialized = true;
            }
            else
            {
                state.SmoothedPhysicsBlend += (targetBlend - state.SmoothedPhysicsBlend) * inertia;
            }

            return Math.Clamp(state.SmoothedPhysicsBlend, 0.0, 1.0);
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

        private static void ApplyVelocityAdaptiveCurve(
            ref double normX, ref double normY,
            double speedBlend,
            DynamicStickResponseInfo dynamicInfo,
            MicroAimPrecisionInfo precisionInfo)
        {
            double mag = Math.Sqrt((normX * normX) + (normY * normY));
            if (mag <= 1e-9)
            {
                normX = normY = 0.0;
                return;
            }

            double precisionMag = MicroAimPrecisionProcessor.ApplyAdaptiveLowRangeCurve(mag, precisionInfo);
            double balancedMag = ApplyBalancedCurve(mag, dynamicInfo, precisionInfo);
            double linearMag = mag;

            double outMag;
            if (speedBlend <= 0.5)
            {
                double t = speedBlend * 2.0;
                outMag = precisionMag + ((balancedMag - precisionMag) * SmoothStep(t));
            }
            else
            {
                double t = (speedBlend - 0.5) * 2.0;
                outMag = balancedMag + ((linearMag - balancedMag) * SmoothStep(t));
            }

            double scale = outMag / mag;
            normX *= scale;
            normY *= scale;
        }

        private static double ApplyBalancedCurve(
            double magnitude, DynamicStickResponseInfo dynamicInfo, MicroAimPrecisionInfo precisionInfo)
        {
            return MicroAimPrecisionProcessor.ApplyAdaptiveLowRangeCurveParams(
                magnitude,
                precisionInfo.precisionZone * dynamicInfo.balancedZoneScale,
                precisionInfo.transitionZone * dynamicInfo.balancedZoneScale,
                precisionInfo.precisionExponent * dynamicInfo.balancedExponentScale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double SmoothStep(double t)
        {
            t = Math.Clamp(t, 0.0, 1.0);
            return t * t * (3.0 - (2.0 * t));
        }
    }
}
