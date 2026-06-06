using APP;

namespace APPTests
{
    [TestClass]
    public class OutputCurveEffectTests
    {
        private const int Dev = 8;

        private static (byte lx, short plx) RunHalfRight(int curveMode, string customDef)
        {
            Global.setLsOutCurveMode(Dev, 0);
            if (curveMode == 6)
            {
                Global.lsOutBezierCurveObj[Dev].InitBezierCurve(
                    customDef, BezierCurve.AxisType.LSRS, true);
            }
            Global.setLsOutCurveMode(Dev, curveMode);

            DS4State outState = new DS4State();
            // Settle a few frames (some processors keep per-frame state)
            for (int i = 0; i < 8; i++)
            {
                DS4State input = new DS4State()
                {
                    LX = 192,
                    LY = 128,
                    RX = 128,
                    RY = 128,
                    elapsedTime = 0.008,
                };
                outState = new DS4State();
                Mapping.SetCurveAndDeadzone(Dev, input, outState);
            }

            return (outState.LX, outState.PreciseLX);
        }

        private static (byte lx, byte ly, short plx, short ply) RunDiagonal(
            int curveMode, byte inX, byte inY)
        {
            Global.setLsOutCurveMode(Dev, curveMode);

            DS4State outState = new DS4State();
            for (int i = 0; i < 8; i++)
            {
                DS4State input = new DS4State()
                {
                    LX = inX,
                    LY = inY,
                    RX = 128,
                    RY = 128,
                    elapsedTime = 0.008,
                };
                outState = new DS4State();
                Mapping.SetCurveAndDeadzone(Dev, input, outState);
            }

            return (outState.LX, outState.LY, outState.PreciseLX, outState.PreciseLY);
        }

        private static (byte lx, byte ly, short plx, short ply) RunDiagonalCustom(
            string customDef, byte inX, byte inY)
        {
            Global.setLsOutCurveMode(Dev, 0);
            Global.lsOutBezierCurveObj[Dev].InitBezierCurve(
                customDef, BezierCurve.AxisType.LSRS, true);
            return RunDiagonal(6, inX, inY);
        }

        [TestMethod]
        public void Axial_YAxisNotInverted()
        {
            Global.setHighPrecisionStickOutput(Dev, true);
            Global.setLsOutCurveMode(Dev, 0);
            var ls = Global.GetLSDeadInfo(Dev);
            Global.GetSquareStickInfo(Dev).lsMode = false;
            ls.advancedRadialProcessing = false;
            ls.deadzoneType = StickDeadZoneInfo.DeadZoneType.Axial;

            try
            {
                // Down-right input: X byte > center (right), Y byte > center (down).
                var dr = RunDiagonal(0, 210, 210);
                // Right => positive X. Down => negative Y (math convention).
                Assert.IsTrue(dr.plx > 1000, $"X lost: plx={dr.plx} ply={dr.ply}");
                Assert.IsTrue(dr.ply < -1000, $"Y axis inverted on axial deadzone: plx={dr.plx} ply={dr.ply} (expected ply < 0 for 'down')");
                // Symmetric 45-degree input should map to roughly symmetric magnitudes.
                Assert.IsTrue(System.Math.Abs(System.Math.Abs(dr.plx) - System.Math.Abs(dr.ply)) < 2000,
                    $"Axial diagonal asymmetric: plx={dr.plx} ply={dr.ply}");
            }
            finally
            {
                ls.deadzoneType = StickDeadZoneInfo.DeadZoneType.Radial;
            }
        }

        // Full tilt must reach (near) full output regardless of deadzone/maxZone settings.
        // A deadzone or a sub-100 maxZone only changes WHERE saturation begins, never the
        // achievable maximum. Several processors under-scaled this, capping max output.
        private void AssertFullOutputReachable(bool advanced, int deadZone, int maxZone)
        {
            Global.setHighPrecisionStickOutput(Dev, true);
            Global.setLsOutCurveMode(Dev, 0);
            var ls = Global.GetLSDeadInfo(Dev);
            Global.GetSquareStickInfo(Dev).lsMode = false;

            int savedDz = ls.deadZone;
            int savedMax = ls.maxZone;
            bool savedAdv = ls.advancedRadialProcessing;
            var savedType = ls.deadzoneType;
            try
            {
                ls.deadzoneType = StickDeadZoneInfo.DeadZoneType.Radial;
                ls.advancedRadialProcessing = advanced;
                ls.deadZone = deadZone;
                ls.maxZone = maxZone;

                var full = RunDiagonal(0, 255, 128);
                Assert.IsTrue(full.plx > 32000,
                    $"Full tilt did not reach full output (advanced={advanced}, dz={deadZone}, maxZone={maxZone}): plx={full.plx}");
            }
            finally
            {
                ls.deadZone = savedDz;
                ls.maxZone = savedMax;
                ls.advancedRadialProcessing = savedAdv;
                ls.deadzoneType = savedType;
            }
        }

        [TestMethod]
        public void MaxOutput_Legacy_Deadzone_ReachesFull()
        {
            AssertFullOutputReachable(advanced: false, deadZone: 25, maxZone: 100);
        }

        [TestMethod]
        public void MaxOutput_Legacy_MaxZone_ReachesFull()
        {
            AssertFullOutputReachable(advanced: false, deadZone: 0, maxZone: 80);
        }

        [TestMethod]
        public void MaxOutput_Advanced_Deadzone_ReachesFull()
        {
            AssertFullOutputReachable(advanced: true, deadZone: 25, maxZone: 100);
        }

        [TestMethod]
        public void MaxOutput_Advanced_MaxZone_ReachesFull()
        {
            AssertFullOutputReachable(advanced: true, deadZone: 0, maxZone: 80);
        }

        [TestMethod]
        public void Diagonal_IsPreserved()
        {
            Global.setHighPrecisionStickOutput(Dev, true);

            // 45-degree diagonal toward down-right (both axes equally deflected).
            var linear = RunDiagonal(0, 210, 210);
            var cubic = RunDiagonal(3, 210, 210);

            Assert.IsTrue(System.Math.Abs(linear.plx) > 1000 && System.Math.Abs(linear.ply) > 1000,
                $"Linear diagonal collapsed to an axis: lx={linear.lx} ly={linear.ly} plx={linear.plx} ply={linear.ply}");
            Assert.IsTrue(System.Math.Abs(cubic.plx) > 1000 && System.Math.Abs(cubic.ply) > 1000,
                $"Cubic diagonal collapsed to an axis: lx={cubic.lx} ly={cubic.ly} plx={cubic.plx} ply={cubic.ply}");
        }

        [TestMethod]
        public void Diagonal_AcrossConfigs()
        {
            Global.setHighPrecisionStickOutput(Dev, true);
            Global.setLsOutCurveMode(Dev, 0);
            var ls = Global.GetLSDeadInfo(Dev);
            var sq = Global.GetSquareStickInfo(Dev);

            string report = "";

            ls.deadzoneType = StickDeadZoneInfo.DeadZoneType.Radial;
            ls.advancedRadialProcessing = false;
            sq.lsMode = false;

            // Off-axis diagonal: X strong, Y mild. The output curve must keep the
            // direction (X/Y ratio) close to the input ratio, regardless of curve shape.
            var linNon45 = RunDiagonal(0, 210, 150);
            double linRatio = System.Math.Abs((double)linNon45.plx / linNon45.ply);

            var cubNon45 = RunDiagonal(3, 210, 150);
            double cubRatio = System.Math.Abs((double)cubNon45.plx / cubNon45.ply);

            var customNon45 = RunDiagonalCustom("0.90, 0.10, 1.00, 1.00", 210, 150);
            double customRatio = System.Math.Abs((double)customNon45.plx / customNon45.ply);

            report = $"linearRatio={linRatio:F2} cubicRatio={cubRatio:F2} customRatio={customRatio:F2}";

            // Allow some tolerance, but a collapse-to-axis bug produces ratios 10x+ off.
            Assert.IsTrue(System.Math.Abs(cubRatio - linRatio) < 1.5,
                $"Cubic curve distorted diagonal direction. {report}");
            Assert.IsTrue(System.Math.Abs(customRatio - linRatio) < 1.5,
                $"Custom curve distorted diagonal direction. {report}");
        }

        [TestMethod]
        public void OutputCurve_ChangesStickOutput()
        {
            Global.setHighPrecisionStickOutput(Dev, true);

            var linear = RunHalfRight(0, null);
            var cubic = RunHalfRight(3, null);
            var custom = RunHalfRight(6, "0.90, 0.10, 1.00, 1.00");

            int mode = Global.getLsOutCurveMode(Dev);
            BezierCurve obj = Global.lsOutBezierCurveObj[Dev];
            byte lutAt192 = obj?.arrayBezierLUT == null ? (byte)0 : obj.arrayBezierLUT[192];

            Assert.AreNotEqual(linear.plx, cubic.plx,
                $"Cubic preset had no effect: linear plx={linear.plx} cubic plx={cubic.plx}");
            Assert.AreNotEqual(linear.plx, custom.plx,
                $"Custom curve had no effect: linear plx={linear.plx} custom plx={custom.plx} (lx {linear.lx} vs {custom.lx}) | mode={mode} lutNull={(obj?.arrayBezierLUT == null)} lut[192]={lutAt192}");
        }
    }
}
