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

namespace APP
{
    internal struct DS4Vector2
    {
        public double x;
        public double y;

        public DS4Vector2(double x, double y)
        {
            this.x = x;
            this.y = y;
        }
    }

    internal class DS4SquareStick
    {
        public DS4Vector2 current;
        public DS4Vector2 squared;

        public DS4SquareStick()
        {
            current = new DS4Vector2(0.0, 0.0);
            squared = new DS4Vector2(0.0, 0.0);
        }

        // Modification of squared stick routine documented
        // at http://theinstructionlimit.com/squaring-the-thumbsticks
        public void CircleToSquare(double roundness)
        {
            const double PiOverFour = Math.PI / 4.0;

            double angle = Math.Atan2(current.y, -current.x);
            angle += Math.PI;
            double cosAng = Math.Cos(angle);
            if (angle <= PiOverFour || angle > 7.0 * PiOverFour)
            {
                double tempVal = 1.0 / cosAng;
                squared.x = current.x * tempVal;
                squared.y = current.y * tempVal;
            }
            else if (angle > PiOverFour && angle <= 3.0 * PiOverFour)
            {
                double tempVal = 1.0 / Math.Sin(angle);
                squared.x = current.x * tempVal;
                squared.y = current.y * tempVal;
            }
            else if (angle > 3.0 * PiOverFour && angle <= 5.0 * PiOverFour)
            {
                double tempVal = -1.0 / cosAng;
                squared.x = current.x * tempVal;
                squared.y = current.y * tempVal;
            }
            else if (angle > 5.0 * PiOverFour && angle <= 7.0 * PiOverFour)
            {
                double tempVal = -1.0 / Math.Sin(angle);
                squared.x = current.x * tempVal;
                squared.y = current.y * tempVal;
            }
            else
            {
                return;
            }

            double length = current.x / cosAng;
            double factor = Math.Pow(length, roundness);
            current.x += (squared.x - current.x) * factor;
            current.y += (squared.y - current.y) * factor;
        }
    }
}
