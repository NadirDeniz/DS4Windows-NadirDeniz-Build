using System;
using System.Collections.Generic;
using APP;

namespace APPTests
{
    /// <summary>
    /// Diagnostic / characterization tests for the default-on advanced radial deadzone.
    /// These DO NOT change production behavior; they prove WHERE diagonal/circular motion
    /// becomes distorted with a Linear curve + Radial deadzone on the default settings
    /// (advancedRadialProcessing = dynamicCenterCalibration = dynamicDeadzoneScaling = true).
    ///
    /// Each test contrasts the advanced path against the legacy radial path (which is the
    /// upstream-equivalent static, angle-preserving, velocity-independent remap) using the
    /// SAME physical input sequence, isolating the dynamic sub-stages as the distortion source.
    /// </summary>
    [TestClass]
    public class RadialDeadzoneDiagnosticsTests
    {
        private static void Configure(int dev, bool advanced)
        {
            Global.setHighPrecisionStickOutput(dev, true);
            Global.setLsOutCurveMode(dev, 0); // Linear curve => curve stage is a no-op
            Global.GetSquareStickInfo(dev).lsMode = false;
            Global.LSSens[dev] = 1.0;

            var ls = Global.GetLSDeadInfo(dev);
            ls.deadzoneType = StickDeadZoneInfo.DeadZoneType.Radial;
            ls.deadZone = 10;       // default
            ls.antiDeadZone = 20;   // default
            ls.maxZone = 100;       // default
            ls.maxOutput = 100.0;
            ls.outerDeadzone = 0;
            ls.innerZoneSoftness = 0.18;
            ls.centerCalStrength = 0.38;

            ls.advancedRadialProcessing = advanced;
            // The dynamic sub-features only matter on the advanced path; mirror the defaults.
            ls.dynamicCenterCalibration = advanced;
            ls.dynamicDeadzoneScaling = advanced;
            ls.adaptiveAntiDeadzone = advanced;
        }

        private static (double mag, double angleDeg, short plx, short ply) RunSequence(
            int dev, IEnumerable<(byte x, byte y, int frames)> steps)
        {
            DS4State outState = new DS4State();
            foreach (var (x, y, frames) in steps)
            {
                for (int i = 0; i < frames; i++)
                {
                    DS4State input = new DS4State()
                    {
                        LX = x,
                        LY = y,
                        RX = 128,
                        RY = 128,
                        elapsedTime = 0.008, // 125 Hz
                    };
                    outState = new DS4State();
                    Mapping.SetCurveAndDeadzone(dev, input, outState);
                }
            }

            double mx = outState.PreciseLX / 32767.0;
            double my = outState.PreciseLY / 32767.0;
            double mag = Math.Sqrt((mx * mx) + (my * my));
            double ang = Math.Atan2(my, mx) * 180.0 / Math.PI;
            return (mag, ang, outState.PreciseLX, outState.PreciseLY);
        }

        /// <summary>
        /// Dynamic center calibration learns a per-axis center offset whenever the stick sits
        /// near center (magnitude &lt; 6%) and slow. A small directional lean held near center is
        /// mislearned as "center" and then SUBTRACTED from every later sample, rotating the angle
        /// of subsequent diagonal input. The legacy path keeps the true 45 degrees.
        /// </summary>
        [TestMethod]
        public void Diag_DynamicCenterCalibration_RotatesDiagonalAngle()
        {
            int dev = 7;

            // True center -> hold a tiny right-lean near center -> physical 45 deg up-right.
            // (138,118): dx=+10 (right), dy=+10 (up, since BytesToNormalized negates Y) => 45 deg.
            var seq = new (byte, byte, int)[]
            {
                ((byte)128, (byte)128, 25),
                ((byte)133, (byte)128, 45), // small lean (<6%) gets absorbed as center on advanced
                ((byte)138, (byte)118, 12),
            };

            Configure(dev, advanced: false);
            var legacy = RunSequence(dev, seq);

            Configure(dev, advanced: true);
            var advanced = RunSequence(dev, seq);

            double legacyErr = Math.Abs(legacy.angleDeg - 45.0);
            double advancedErr = Math.Abs(advanced.angleDeg - 45.0);

            // Legacy keeps the diagonal essentially exact; the advanced path rotates it by several
            // degrees purely from the mislearned center offset (enough to feel "diagonal is wrong").
            Assert.IsTrue(legacyErr < 1.5,
                $"Legacy radial should preserve the 45 deg diagonal. angle={legacy.angleDeg:F1} err={legacyErr:F1}");
            Assert.IsTrue(advancedErr > 4.0,
                $"Advanced path rotates the diagonal via learned center offset. " +
                $"legacyAngle={legacy.angleDeg:F1} advancedAngle={advanced.angleDeg:F1} advancedErr={advancedErr:F1}");
        }

        /// <summary>
        /// Dynamic deadzone scaling resizes the inner deadzone by stick SPEED every frame
        /// (slow => larger, fast => smaller). The SAME physical position therefore produces a
        /// different output magnitude depending on how it was approached — a non-deterministic,
        /// stair-stepping-prone response. The legacy path is velocity-independent.
        /// </summary>
        [TestMethod]
        public void Diag_DynamicDeadzoneScaling_OutputDependsOnApproachVelocity()
        {
            int dev = 6;
            byte tx = 142, ty = 114; // dx=+14, dy=+14 => 45 deg, just above the deadzone

            Configure(dev, advanced: true);
            var advSettled = RunSequence(dev, new (byte, byte, int)[]
            {
                ((byte)128, (byte)128, 25),
                (tx, ty, 40), // held => speed ~0 => larger inner deadzone => smaller output
            });
            Configure(dev, advanced: true);
            var advJumped = RunSequence(dev, new (byte, byte, int)[]
            {
                ((byte)128, (byte)128, 25),
                (tx, ty, 1), // single big jump => high speed => smaller inner deadzone => larger output
            });
            double advDiff = Math.Abs(advJumped.mag - advSettled.mag);

            Configure(dev, advanced: false);
            var legSettled = RunSequence(dev, new (byte, byte, int)[]
            {
                ((byte)128, (byte)128, 25),
                (tx, ty, 40),
            });
            Configure(dev, advanced: false);
            var legJumped = RunSequence(dev, new (byte, byte, int)[]
            {
                ((byte)128, (byte)128, 25),
                (tx, ty, 1),
            });
            double legDiff = Math.Abs(legJumped.mag - legSettled.mag);

            Assert.IsTrue(legDiff < 0.002,
                $"Legacy radial output must be velocity-independent. settled={legSettled.mag:F4} jumped={legJumped.mag:F4} diff={legDiff:F4}");
            Assert.IsTrue(advDiff > 0.01,
                $"Advanced output depends on approach velocity (same position, different magnitude). " +
                $"settled={advSettled.mag:F4} jumped={advJumped.mag:F4} diff={advDiff:F4}");
        }

        // Mirrors the shipped defaults: advanced radial geometry ON (soft inner zone) but the
        // distorting adaptive sub-features take whatever DEFAULT_* the build ships with.
        private static void ConfigureDefaults(int dev)
        {
            Configure(dev, advanced: true);
            var ls = Global.GetLSDeadInfo(dev);
            ls.dynamicCenterCalibration = StickDeadZoneInfo.DEFAULT_DYNAMIC_CENTER_CAL;
            ls.dynamicDeadzoneScaling = StickDeadZoneInfo.DEFAULT_DYNAMIC_DZ_SCALING;
            ls.adaptiveAntiDeadzone = StickDeadZoneInfo.DEFAULT_ADAPTIVE_ANTIDEAD;
        }

        /// <summary>
        /// Fix verification: with the shipped defaults, the advanced radial path must preserve the
        /// diagonal angle (no learned-center rotation) AND be velocity-independent (no stair-step).
        /// Fails if the distorting dynamic sub-features are ever re-enabled by default.
        /// </summary>
        [TestMethod]
        public void Diag_DefaultConfig_PreservesDiagonalAndIsDeterministic()
        {
            Assert.IsFalse(StickDeadZoneInfo.DEFAULT_DYNAMIC_CENTER_CAL,
                "Dynamic center calibration must be OFF by default (rotates diagonals).");
            Assert.IsFalse(StickDeadZoneInfo.DEFAULT_DYNAMIC_DZ_SCALING,
                "Dynamic deadzone scaling must be OFF by default (velocity-dependent output).");

            int dev = 4;

            ConfigureDefaults(dev);
            var angleProbe = RunSequence(dev, new (byte, byte, int)[]
            {
                ((byte)128, (byte)128, 25),
                ((byte)133, (byte)128, 45),
                ((byte)138, (byte)118, 12),
            });
            Assert.IsTrue(Math.Abs(angleProbe.angleDeg - 45.0) < 2.0,
                $"Default config rotated the diagonal: angle={angleProbe.angleDeg:F2}");

            byte tx = 142, ty = 114;
            ConfigureDefaults(dev);
            var settled = RunSequence(dev, new (byte, byte, int)[]
            {
                ((byte)128, (byte)128, 25),
                (tx, ty, 40),
            });
            ConfigureDefaults(dev);
            var jumped = RunSequence(dev, new (byte, byte, int)[]
            {
                ((byte)128, (byte)128, 25),
                (tx, ty, 1),
            });
            Assert.IsTrue(Math.Abs(jumped.mag - settled.mag) < 0.002,
                $"Default config output depends on approach velocity: settled={settled.mag:F4} jumped={jumped.mag:F4}");
        }

        /// <summary>
        /// Control: with all dynamic features OFF (legacy radial), a steady diagonal hold both
        /// preserves the angle AND reaches a deterministic magnitude — confirming the distortion
        /// is specifically the dynamic sub-stages, not the radial geometry or the rest of the pipe.
        /// </summary>
        [TestMethod]
        public void Diag_LegacyRadial_PreservesDiagonalCleanly()
        {
            int dev = 5;
            Configure(dev, advanced: false);
            var r = RunSequence(dev, new (byte, byte, int)[]
            {
                ((byte)128, (byte)128, 10),
                ((byte)200, (byte)56, 20), // dx=+72, dy=+72 => 45 deg
            });

            Assert.IsTrue(Math.Abs(r.angleDeg - 45.0) < 3.0,
                $"Legacy radial diagonal angle off: {r.angleDeg:F2}");
            Assert.IsTrue(r.mag > 0.4,
                $"Legacy radial diagonal magnitude unexpectedly low: {r.mag:F3}");
        }
    }
}
