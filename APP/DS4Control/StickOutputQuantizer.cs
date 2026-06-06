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
    /// Final-stage XInput quantization with sub-pixel accumulation.
    /// All upstream stick math should stay in normalized double until here.
    /// </summary>
    internal static class StickOutputQuantizer
    {
        private const int AXIS_COUNT = 4;
        private static readonly double[][] subPixelRemainder =
            new double[Global.TEST_PROFILE_ITEM_COUNT][];

        static StickOutputQuantizer()
        {
            for (int i = 0; i < Global.TEST_PROFILE_ITEM_COUNT; i++)
            {
                subPixelRemainder[i] = new double[AXIS_COUNT];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short QuantizeNormalized(int device, int axisIndex, double normalized)
        {
            normalized = Math.Clamp(normalized, -1.0, 1.0);
            ref double remainder = ref subPixelRemainder[device][axisIndex];

            double exact = (normalized * 32767.0) + remainder;
            int rounded = (int)Math.Round(exact, MidpointRounding.AwayFromZero);
            rounded = Math.Clamp(rounded, -32768, 32767);
            remainder = exact - rounded;
            return (short)rounded;
        }

        public static void FinalizeStickPair(
            int device, int stickId,
            double normX, double normY,
            out short preciseX, out short preciseY)
        {
            int axisBase = stickId == 0 ? 0 : 2;
            preciseX = QuantizeNormalized(device, axisBase, normX);
            preciseY = QuantizeNormalized(device, axisBase + 1, normY);
        }

        public static void ResetDevice(int device)
        {
            for (int i = 0; i < AXIS_COUNT; i++)
            {
                subPixelRemainder[device][i] = 0.0;
            }
        }
    }
}
