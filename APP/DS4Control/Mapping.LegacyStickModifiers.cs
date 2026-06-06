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
using static APP.Global;

namespace APP
{
    public partial class Mapping
    {
        /// <summary>
        /// Stock APP byte path: sensitivity → square (if enabled) → output curve.
        /// Preserved for StickModifierOrder LegacyCompatible (0).
        /// </summary>
        private static void ApplyLegacyCompatibleByteModifiers(
            int device, ref DS4State dState,
            StickDeadZoneInfo lsMod, StickDeadZoneInfo rsMod)
        {
            // Only apply deprecated Sensitivity modifier for Radial DZ
            if (lsMod.deadzoneType == StickDeadZoneInfo.DeadZoneType.Radial)
            {
                double lsSens = getLSSens(device);
                if (lsSens != 1.0)
                {
                    dState.LX = (byte)Global.Clamp(0, lsSens * (dState.LX - 128.0) + 128.0, 255);
                    dState.LY = (byte)Global.Clamp(0, lsSens * (dState.LY - 128.0) + 128.0, 255);
                }
            }

            // Only apply deprecated Sensitivity modifier for Radial DZ
            if (rsMod.deadzoneType == StickDeadZoneInfo.DeadZoneType.Radial)
            {
                double rsSens = getRSSens(device);
                if (rsSens != 1.0)
                {
                    dState.RX = (byte)Global.Clamp(0, rsSens * (dState.RX - 128.0) + 128.0, 255);
                    dState.RY = (byte)Global.Clamp(0, rsSens * (dState.RY - 128.0) + 128.0, 255);
                }
            }

            SquareStickInfo squStk = GetSquareStickInfo(device);
            if (squStk.lsMode && (dState.LX != 128 || dState.LY != 128))
            {
                double capX = dState.LX >= 128 ? 127.0 : 128.0;
                double capY = dState.LY >= 128 ? 127.0 : 128.0;
                double tempX = (dState.LX - 128.0) / capX;
                double tempY = (dState.LY - 128.0) / capY;
                DS4SquareStick sqstick = outSqrStk[device];
                sqstick.current.x = tempX; sqstick.current.y = tempY;
                sqstick.CircleToSquare(squStk.lsRoundness);
                tempX = sqstick.current.x < -1.0 ? -1.0 : sqstick.current.x > 1.0
                    ? 1.0 : sqstick.current.x;
                tempY = sqstick.current.y < -1.0 ? -1.0 : sqstick.current.y > 1.0
                    ? 1.0 : sqstick.current.y;
                dState.LX = (byte)(tempX * capX + 128.0);
                dState.LY = (byte)(tempY * capY + 128.0);
            }

            int lsOutCurveMode = getLsOutCurveMode(device);
            if (lsOutCurveMode > 0 && (dState.LX != 128 || dState.LY != 128))
            {
                double tempRatioX = 0.0, tempRatioY = 0.0;
                double capX = 0.0, capY = 0.0;
                if (lsMod.deadzoneType == StickDeadZoneInfo.DeadZoneType.Radial)
                {
                    double r = Math.Atan2(-(dState.LY - 128.0), (dState.LX - 128.0));
                    double maxOutXRatio = Math.Abs(Math.Cos(r));
                    double maxOutYRatio = Math.Abs(Math.Sin(r));
                    double sideX = dState.LX - 128; double sideY = dState.LY - 128.0;
                    capX = dState.LX >= 128 ? maxOutXRatio * 127.0 : maxOutXRatio * 128.0;
                    capY = dState.LY >= 128 ? maxOutYRatio * 127.0 : maxOutYRatio * 128.0;
                    double absSideX = Math.Abs(sideX); double absSideY = Math.Abs(sideY);
                    if (absSideX > capX) capX = absSideX;
                    if (absSideY > capY) capY = absSideY;
                    tempRatioX = capX > 0 ? (dState.LX - 128.0) / capX : 0;
                    tempRatioY = capY > 0 ? (dState.LY - 128.0) / capY : 0;
                }
                else if (lsMod.deadzoneType == StickDeadZoneInfo.DeadZoneType.Axial)
                {
                    capX = dState.LX >= 128 ? 127.0 : 128.0;
                    capY = dState.LY >= 128 ? 127.0 : 128.0;
                    tempRatioX = (dState.LX - 128.0) / capX;
                    tempRatioY = (dState.LY - 128.0) / capY;
                }

                double signX = tempRatioX >= 0.0 ? 1.0 : -1.0;
                double signY = tempRatioY >= 0.0 ? 1.0 : -1.0;

                if (lsOutCurveMode == 1)
                {
                    double absX = Math.Abs(tempRatioX);
                    double absY = Math.Abs(tempRatioY);
                    double outputX = 0.0;
                    double outputY = 0.0;

                    if (absX <= 0.4) outputX = 0.8 * absX;
                    else if (absX <= 0.75) outputX = absX - 0.08;
                    else if (absX > 0.75) outputX = (absX * 1.32) - 0.32;

                    if (absY <= 0.4) outputY = 0.8 * absY;
                    else if (absY <= 0.75) outputY = absY - 0.08;
                    else if (absY > 0.75) outputY = (absY * 1.32) - 0.32;

                    dState.LX = (byte)(outputX * signX * capX + 128.0);
                    dState.LY = (byte)(outputY * signY * capY + 128.0);
                }
                else if (lsOutCurveMode == 2)
                {
                    double outputX = tempRatioX * tempRatioX;
                    double outputY = tempRatioY * tempRatioY;
                    dState.LX = (byte)(outputX * signX * capX + 128.0);
                    dState.LY = (byte)(outputY * signY * capY + 128.0);
                }
                else if (lsOutCurveMode == 3)
                {
                    double outputX = tempRatioX * tempRatioX * tempRatioX;
                    double outputY = tempRatioY * tempRatioY * tempRatioY;
                    dState.LX = (byte)(outputX * capX + 128.0);
                    dState.LY = (byte)(outputY * capY + 128.0);
                }
                else if (lsOutCurveMode == 4)
                {
                    double absX = Math.Abs(tempRatioX);
                    double absY = Math.Abs(tempRatioY);
                    double outputX = absX * (absX - 2.0);
                    double outputY = absY * (absY - 2.0);
                    dState.LX = (byte)(-1.0 * outputX * signX * capX + 128.0);
                    dState.LY = (byte)(-1.0 * outputY * signY * capY + 128.0);
                }
                else if (lsOutCurveMode == 5)
                {
                    double innerX = Math.Abs(tempRatioX) - 1.0;
                    double innerY = Math.Abs(tempRatioY) - 1.0;
                    double outputX = innerX * innerX * innerX + 1.0;
                    double outputY = innerY * innerY * innerY + 1.0;
                    dState.LX = (byte)(1.0 * outputX * signX * capX + 128.0);
                    dState.LY = (byte)(1.0 * outputY * signY * capY + 128.0);
                }
                else if (lsOutCurveMode == 6)
                {
                    if (lsMod.deadzoneType == StickDeadZoneInfo.DeadZoneType.Radial)
                    {
                        double maxX = (dState.LX >= 128 ? 127 : 128);
                        double maxY = (dState.LY >= 128 ? 127 : 128);
                        byte tempOutX = (byte)(tempRatioX * maxX + 128.0);
                        byte tempOutY = (byte)(tempRatioY * maxY + 128.0);

                        byte tempX = lsOutBezierCurveObj[device].arrayBezierLUT[tempOutX];
                        byte tempY = lsOutBezierCurveObj[device].arrayBezierLUT[tempOutY];

                        double tempRatioOutX = (tempX - 128.0) / maxX;
                        double tempRatioOutY = (tempY - 128.0) / maxY;

                        dState.LX = (byte)(tempRatioOutX * capX + 128);
                        dState.LY = (byte)(tempRatioOutY * capY + 128);
                    }
                    else if (lsMod.deadzoneType == StickDeadZoneInfo.DeadZoneType.Axial)
                    {
                        dState.LX = lsOutBezierCurveObj[device].arrayBezierLUT[dState.LX];
                        dState.LY = lsOutBezierCurveObj[device].arrayBezierLUT[dState.LY];
                    }
                }
            }

            if (squStk.rsMode && (dState.RX != 128 || dState.RY != 128))
            {
                double capX = dState.RX >= 128 ? 127.0 : 128.0;
                double capY = dState.RY >= 128 ? 127.0 : 128.0;
                double tempX = (dState.RX - 128.0) / capX;
                double tempY = (dState.RY - 128.0) / capY;
                DS4SquareStick sqstick = outSqrStk[device];
                sqstick.current.x = tempX; sqstick.current.y = tempY;
                sqstick.CircleToSquare(squStk.rsRoundness);
                tempX = sqstick.current.x < -1.0 ? -1.0 : sqstick.current.x > 1.0
                    ? 1.0 : sqstick.current.x;
                tempY = sqstick.current.y < -1.0 ? -1.0 : sqstick.current.y > 1.0
                    ? 1.0 : sqstick.current.y;
                dState.RX = (byte)(tempX * capX + 128.0);
                dState.RY = (byte)(tempY * capY + 128.0);
            }

            int rsOutCurveMode = getRsOutCurveMode(device);
            if (rsOutCurveMode > 0 && (dState.RX != 128 || dState.RY != 128))
            {
                double tempRatioX = 0.0, tempRatioY = 0.0;
                double capX = 0.0, capY = 0.0;
                if (rsMod.deadzoneType == StickDeadZoneInfo.DeadZoneType.Radial)
                {
                    double r = Math.Atan2(-(dState.RY - 128.0), (dState.RX - 128.0));
                    double maxOutXRatio = Math.Abs(Math.Cos(r));
                    double maxOutYRatio = Math.Abs(Math.Sin(r));
                    double sideX = dState.RX - 128; double sideY = dState.RY - 128.0;
                    capX = dState.RX >= 128 ? maxOutXRatio * 127.0 : maxOutXRatio * 128.0;
                    capY = dState.RY >= 128 ? maxOutYRatio * 127.0 : maxOutYRatio * 128.0;
                    double absSideX = Math.Abs(sideX); double absSideY = Math.Abs(sideY);
                    if (absSideX > capX) capX = absSideX;
                    if (absSideY > capY) capY = absSideY;
                    tempRatioX = capX > 0 ? (dState.RX - 128.0) / capX : 0;
                    tempRatioY = capY > 0 ? (dState.RY - 128.0) / capY : 0;
                }
                else if (rsMod.deadzoneType == StickDeadZoneInfo.DeadZoneType.Axial)
                {
                    capX = dState.RX >= 128 ? 127.0 : 128.0;
                    capY = dState.RY >= 128 ? 127.0 : 128.0;
                    tempRatioX = (dState.RX - 128.0) / capX;
                    tempRatioY = (dState.RY - 128.0) / capY;
                }

                double signX = tempRatioX >= 0.0 ? 1.0 : -1.0;
                double signY = tempRatioY >= 0.0 ? 1.0 : -1.0;

                if (rsOutCurveMode == 1)
                {
                    double absX = Math.Abs(tempRatioX);
                    double absY = Math.Abs(tempRatioY);
                    double outputX = 0.0;
                    double outputY = 0.0;

                    if (absX <= 0.4) outputX = 0.8 * absX;
                    else if (absX <= 0.75) outputX = absX - 0.08;
                    else if (absX > 0.75) outputX = (absX * 1.32) - 0.32;

                    if (absY <= 0.4) outputY = 0.8 * absY;
                    else if (absY <= 0.75) outputY = absY - 0.08;
                    else if (absY > 0.75) outputY = (absY * 1.32) - 0.32;

                    dState.RX = (byte)(outputX * signX * capX + 128.0);
                    dState.RY = (byte)(outputY * signY * capY + 128.0);
                }
                else if (rsOutCurveMode == 2)
                {
                    double outputX = tempRatioX * tempRatioX;
                    double outputY = tempRatioY * tempRatioY;
                    dState.RX = (byte)(outputX * signX * capX + 128.0);
                    dState.RY = (byte)(outputY * signY * capY + 128.0);
                }
                else if (rsOutCurveMode == 3)
                {
                    double outputX = tempRatioX * tempRatioX * tempRatioX;
                    double outputY = tempRatioY * tempRatioY * tempRatioY;
                    dState.RX = (byte)(outputX * capX + 128.0);
                    dState.RY = (byte)(outputY * capY + 128.0);
                }
                else if (rsOutCurveMode == 4)
                {
                    double absX = Math.Abs(tempRatioX);
                    double absY = Math.Abs(tempRatioY);
                    double outputX = absX * (absX - 2.0);
                    double outputY = absY * (absY - 2.0);
                    dState.RX = (byte)(-1.0 * outputX * signX * capX + 128.0);
                    dState.RY = (byte)(-1.0 * outputY * signY * capY + 128.0);
                }
                else if (rsOutCurveMode == 5)
                {
                    double innerX = Math.Abs(tempRatioX) - 1.0;
                    double innerY = Math.Abs(tempRatioY) - 1.0;
                    double outputX = innerX * innerX * innerX + 1.0;
                    double outputY = innerY * innerY * innerY + 1.0;
                    dState.RX = (byte)(1.0 * outputX * signX * capX + 128.0);
                    dState.RY = (byte)(1.0 * outputY * signY * capY + 128.0);
                }
                else if (rsOutCurveMode == 6)
                {
                    if (rsMod.deadzoneType == StickDeadZoneInfo.DeadZoneType.Radial)
                    {
                        double maxX = (dState.RX >= 128 ? 127 : 128);
                        double maxY = (dState.RY >= 128 ? 127 : 128);
                        byte tempOutX = (byte)(tempRatioX * maxX + 128.0);
                        byte tempOutY = (byte)(tempRatioY * maxY + 128.0);

                        byte tempX = rsOutBezierCurveObj[device].arrayBezierLUT[tempOutX];
                        byte tempY = rsOutBezierCurveObj[device].arrayBezierLUT[tempOutY];

                        double tempRatioOutX = (tempX - 128.0) / maxX;
                        double tempRatioOutY = (tempY - 128.0) / maxY;

                        dState.RX = (byte)(tempRatioOutX * capX + 128);
                        dState.RY = (byte)(tempRatioOutY * capY + 128);
                    }
                    else if (rsMod.deadzoneType == StickDeadZoneInfo.DeadZoneType.Axial)
                    {
                        dState.RX = rsOutBezierCurveObj[device].arrayBezierLUT[dState.RX];
                        dState.RY = rsOutBezierCurveObj[device].arrayBezierLUT[dState.RY];
                    }
                }
            }
        }
    }
}
