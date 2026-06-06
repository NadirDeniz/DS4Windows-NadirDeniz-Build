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

using static APP.Global;

namespace APP
{
    public partial class Mapping
    {
        private static bool GetDynamicPhysicsResponse(int device, int stickId)
        {
            if (stickId == 0)
            {
                return getLSDynamicPhysicsResponse(device);
            }

            return getRSDynamicPhysicsResponse(device);
        }

        private static void ApplyPredictiveJitterPreDeadzone(
            int device, ref DS4State cState, double stickDeltaTime)
        {
            if (PredictiveJitterCleanerProcessor.IsEnabledForStick(device, 0))
            {
                PredictiveJitterCleanerProcessor.Apply(
                    device, 0, ref cState.LX, ref cState.LY, stickDeltaTime);
            }

            if (PredictiveJitterCleanerProcessor.IsEnabledForStick(device, 1))
            {
                PredictiveJitterCleanerProcessor.Apply(
                    device, 1, ref cState.RX, ref cState.RY, stickDeltaTime);
            }
        }

        private static void SyncStickBytesIfAllowed(
            ref DS4State dState, int stickId, double normX, double normY,
            out byte outX, out byte outY)
        {
            HighPrecisionStickPipeline.SyncBytesFromNormalized(
                dState, stickId, normX, normY, out outX, out outY);
        }
    }
}
