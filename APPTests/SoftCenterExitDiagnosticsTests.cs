using System;
using System.Collections.Generic;
using APP;

namespace APPTests
{
    /// <summary>
    /// Diagnostic / property tests for Soft Center Exit (SCE) on the CONTROLLER analog path.
    ///
    /// These characterize the stutter near center caused by SCE's per-frame velocity-bypass
    /// hard switch, which toggles the output between the RAW magnitude (when "moving") and the
    /// SHAPED magnitude (when "settled") based on noisy instantaneous stick speed.
    ///
    /// The asserts below express the DESIRED behavior:
    ///   * a constant physical position must produce a constant output (no approach-speed / history dependence)
    ///   * a monotonically increasing physical magnitude must produce a monotonically increasing output (no sawtooth)
    ///   * diagonals must be preserved at all magnitudes
    /// They FAIL against the current velocity-bypass implementation and PASS once SCE is made a
    /// single, deterministic, C1-continuous magnitude curve.
    ///
    /// All tests use the high-precision float path (PreciseLX/LY, 16-bit) so quantization is not a
    /// confound; the deadzone is set to a near-identity radial remap to isolate SCE.
    /// </summary>
    [TestClass]
    public class SoftCenterExitDiagnosticsTests
    {
        private static void Configure(int dev, bool sceEnabled)
        {
            Global.setHighPrecisionStickOutput(dev, true);
            Global.setLsOutCurveMode(dev, 0); // Linear curve => curve stage is a no-op
            Global.GetSquareStickInfo(dev).lsMode = false;
            Global.LSSens[dev] = 1.0;

            var ls = Global.GetLSDeadInfo(dev);
            ls.deadzoneType = StickDeadZoneInfo.DeadZoneType.Radial;
            ls.deadZone = 0;        // near-identity remap so SCE sees the true small magnitude
            ls.antiDeadZone = 0;
            ls.maxZone = 100;
            ls.maxOutput = 100.0;
            ls.outerDeadzone = 0;
            ls.fuzz = 0;
            ls.advancedRadialProcessing = false;
            ls.dynamicCenterCalibration = false;
            ls.dynamicDeadzoneScaling = false;
            ls.adaptiveAntiDeadzone = false;

            var sce = Global.GetLSSoftCenterExitInfo(dev);
            sce.enabled = sceEnabled;
            sce.range = SoftCenterExitInfo.DEFAULT_RANGE;             // 0.08
            sce.exponent = SoftCenterExitInfo.DEFAULT_EXPONENT;       // 1.8
            sce.velocityBypass = SoftCenterExitInfo.DEFAULT_VELOCITY_BYPASS; // 0.20
        }

        private static (double mag, double angleDeg) RunSequence(
            int dev, IEnumerable<(byte x, byte y, int frames)> steps, double elapsed = 0.008)
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
                        elapsedTime = elapsed,
                    };
                    outState = new DS4State();
                    Mapping.SetCurveAndDeadzone(dev, input, outState);
                }
            }

            double mx = outState.PreciseLX / 32767.0;
            double my = outState.PreciseLY / 32767.0;
            double mag = Math.Sqrt((mx * mx) + (my * my));
            double ang = Math.Atan2(my, mx) * 180.0 / Math.PI;
            return (mag, ang);
        }

        private static List<double> RunCollectMagnitudes(
            int dev, IEnumerable<(byte x, byte y, int frames)> steps, double elapsed = 0.008)
        {
            var mags = new List<double>();
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
                        elapsedTime = elapsed,
                    };
                    DS4State outState = new DS4State();
                    Mapping.SetCurveAndDeadzone(dev, input, outState);
                    double mx = outState.PreciseLX / 32767.0;
                    double my = outState.PreciseLY / 32767.0;
                    mags.Add(Math.Sqrt((mx * mx) + (my * my)));
                }
            }

            return mags;
        }

        /// <summary>
        /// Sanity: SCE must actually alter the near-center output (otherwise the other tests would
        /// pass trivially). A small held magnitude inside the range is compressed by SCE.
        /// </summary>
        [TestMethod]
        public void SCE_IsActive_ChangesNearCenterOutput()
        {
            int dev = 0;
            byte tx = 131, ty = 125; // dx=+3, dy=+3 => ~0.033 magnitude, deep inside range (0.08)

            Configure(dev, sceEnabled: false);
            var off = RunSequence(dev, new (byte, byte, int)[]
            {
                ((byte)128, (byte)128, 20),
                (tx, ty, 20),
            });

            Configure(dev, sceEnabled: true);
            var on = RunSequence(dev, new (byte, byte, int)[]
            {
                ((byte)128, (byte)128, 20),
                (tx, ty, 20),
            });

            Assert.IsTrue(on.mag < off.mag && (off.mag - on.mag) > 0.002,
                $"SCE should compress (soften) near-center magnitude. off={off.mag:F4} on={on.mag:F4}");
        }

        /// <summary>
        /// THE CORE BUG: a constant physical stick position must yield a constant output magnitude,
        /// regardless of how it was approached. With the velocity-bypass switch, a single fast jump
        /// (high speed => bypass => RAW) and a settled hold (speed ~0 => SHAPED) at the SAME position
        /// produce different magnitudes. This is the stutter source during micro-corrections.
        /// </summary>
        [TestMethod]
        public void SCE_ConstantPosition_IsApproachSpeedIndependent()
        {
            int dev = 1;
            byte tx = 133, ty = 123; // ~0.056 magnitude, inside range

            Configure(dev, sceEnabled: true);
            var settled = RunSequence(dev, new (byte, byte, int)[]
            {
                ((byte)128, (byte)128, 25),
                (tx, ty, 40), // held => last frames have ~0 speed => SHAPED
            });

            Configure(dev, sceEnabled: true);
            var jumped = RunSequence(dev, new (byte, byte, int)[]
            {
                ((byte)128, (byte)128, 25),
                (tx, ty, 1), // single jump => high speed => bypass => RAW
            });

            double diff = Math.Abs(jumped.mag - settled.mag);
            Assert.IsTrue(diff < 0.002,
                $"Same physical position produced different output by approach speed (SCE velocity-bypass). " +
                $"settled={settled.mag:F4} jumped={jumped.mag:F4} diff={diff:F4}");
        }

        /// <summary>
        /// Control: with SCE OFF the same comparison is trivially approach-independent. Confirms the
        /// harness/deadzone are themselves velocity-independent and the difference above is purely SCE.
        /// </summary>
        [TestMethod]
        public void SCE_Off_ConstantPosition_IsApproachSpeedIndependent()
        {
            int dev = 2;
            byte tx = 133, ty = 123;

            Configure(dev, sceEnabled: false);
            var settled = RunSequence(dev, new (byte, byte, int)[]
            {
                ((byte)128, (byte)128, 25),
                (tx, ty, 40),
            });
            Configure(dev, sceEnabled: false);
            var jumped = RunSequence(dev, new (byte, byte, int)[]
            {
                ((byte)128, (byte)128, 25),
                (tx, ty, 1),
            });

            Assert.IsTrue(Math.Abs(jumped.mag - settled.mag) < 0.002,
                $"Baseline (SCE off) must be approach-independent. settled={settled.mag:F4} jumped={jumped.mag:F4}");
        }

        /// <summary>
        /// A monotonically increasing physical magnitude (slow outward drag) must produce a
        /// monotonically non-decreasing output. The velocity-bypass toggles RAW (on the frame the
        /// input steps) vs SHAPED (on the held frames), so output sawtooths up-then-down => stutter.
        /// </summary>
        [TestMethod]
        public void SCE_SlowOutwardDrag_IsMonotonic()
        {
            int dev = 3;

            var steps = new List<(byte, byte, int)>
            {
                ((byte)128, (byte)128, 20),
            };
            // Drag straight up from center past the range boundary, 1 byte code at a time,
            // holding each position for a few frames (simulates a slow deliberate push).
            for (int k = 1; k <= 18; k++)
            {
                steps.Add(((byte)128, (byte)(128 - k), 4));
            }

            Configure(dev, sceEnabled: true);
            var mags = RunCollectMagnitudes(dev, steps);

            double worstDrop = 0.0;
            for (int i = 1; i < mags.Count; i++)
            {
                double drop = mags[i - 1] - mags[i];
                if (drop > worstDrop) worstDrop = drop;
            }

            Assert.IsTrue(worstDrop < 2e-4,
                $"Output magnitude dropped while physical input increased (SCE stutter/sawtooth). " +
                $"worstDrop={worstDrop:F5}");
        }

        /// <summary>
        /// Diagonals must be preserved at a small in-range magnitude, both when settled and when
        /// jumped (i.e. independent of which transfer branch is taken).
        /// </summary>
        [TestMethod]
        public void SCE_DiagonalPreserved_NearCenter()
        {
            int dev = 4;
            byte tx = 133, ty = 123; // 45 deg, ~0.056 magnitude

            Configure(dev, sceEnabled: true);
            var settled = RunSequence(dev, new (byte, byte, int)[]
            {
                ((byte)128, (byte)128, 25),
                (tx, ty, 40),
            });
            Configure(dev, sceEnabled: true);
            var jumped = RunSequence(dev, new (byte, byte, int)[]
            {
                ((byte)128, (byte)128, 25),
                (tx, ty, 1),
            });

            Assert.IsTrue(Math.Abs(settled.angleDeg - 45.0) < 2.0,
                $"Settled diagonal angle off: {settled.angleDeg:F2}");
            Assert.IsTrue(Math.Abs(jumped.angleDeg - 45.0) < 2.0,
                $"Jumped diagonal angle off: {jumped.angleDeg:F2}");
        }

        /// <summary>
        /// Strict monotonicity across the ENTIRE stick range for every supported exponent
        /// (worst case k = 1/8). Sweeps a 45-degree ray from center to full deflection one input
        /// code at a time; output magnitude must never decrease and must span the full range.
        /// </summary>
        [TestMethod]
        public void SCE_FullRange_StrictlyMonotonic_AllExponents()
        {
            int dev = 5;
            foreach (double exponent in new[] { 1.0, 1.8, 4.0, 8.0 })
            {
                Configure(dev, sceEnabled: true);
                Global.GetLSSoftCenterExitInfo(dev).exponent = exponent;

                var steps = new List<(byte, byte, int)> { ((byte)128, (byte)128, 5) };
                for (int d = 1; d <= 127; d++)
                {
                    steps.Add(((byte)(128 + d), (byte)(128 - d), 2)); // exact 45 deg ray
                }

                var mags = RunCollectMagnitudes(dev, steps);

                double worstDrop = 0.0;
                for (int i = 1; i < mags.Count; i++)
                {
                    double drop = mags[i - 1] - mags[i];
                    if (drop > worstDrop) worstDrop = drop;
                }

                Assert.IsTrue(worstDrop < 2e-4,
                    $"Non-monotonic at exponent={exponent}: worstDrop={worstDrop:F6}");
                Assert.IsTrue(mags[mags.Count - 1] - mags[0] > 0.9,
                    $"Sweep did not cover full range at exponent={exponent}: " +
                    $"first={mags[0]:F3} last={mags[mags.Count - 1]:F3}");
            }
        }

        /// <summary>
        /// Angle preservation across the entire range: along an exact 45-degree input ray the output
        /// angle must remain 45 degrees at every magnitude (the shaping is a single positive radial
        /// scale, so direction is mathematically invariant).
        /// </summary>
        [TestMethod]
        public void SCE_AnglePreserved_FullRange()
        {
            int dev = 6;
            Configure(dev, sceEnabled: true);
            Global.GetLSSoftCenterExitInfo(dev).exponent = SoftCenterExitInfo.DEFAULT_EXPONENT;

            double worstAngleErr = 0.0;
            for (int d = 1; d <= 127; d++)
            {
                var r = RunSequence(dev, new (byte, byte, int)[]
                {
                    ((byte)128, (byte)128, 3),
                    ((byte)(128 + d), (byte)(128 - d), 3),
                });
                double err = Math.Abs(r.angleDeg - 45.0);
                if (err > worstAngleErr) worstAngleErr = err;
            }

            Assert.IsTrue(worstAngleErr < 1.5,
                $"Angle deviated from the 45 deg input ray (max err={worstAngleErr:F2} deg).");
        }

        // ---- Measurement harness (writes a quantitative report; not a pass/fail behavior gate) ----

        private static void ConfigureDz(int dev, bool sceEnabled, int deadZone, int antiDead,
            double exponent)
        {
            Global.setHighPrecisionStickOutput(dev, true);
            Global.setLsOutCurveMode(dev, 0);
            Global.GetSquareStickInfo(dev).lsMode = false;
            Global.LSSens[dev] = 1.0;

            var ls = Global.GetLSDeadInfo(dev);
            ls.deadzoneType = StickDeadZoneInfo.DeadZoneType.Radial;
            ls.deadZone = deadZone;
            ls.antiDeadZone = antiDead;
            ls.maxZone = 100;
            ls.maxOutput = 100.0;
            ls.outerDeadzone = 0;
            ls.fuzz = 0;
            ls.advancedRadialProcessing = false;
            ls.dynamicCenterCalibration = false;
            ls.dynamicDeadzoneScaling = false;
            ls.adaptiveAntiDeadzone = false;

            var sce = Global.GetLSSoftCenterExitInfo(dev);
            sce.enabled = sceEnabled;
            sce.range = SoftCenterExitInfo.DEFAULT_RANGE;
            sce.exponent = exponent;
            sce.velocityBypass = SoftCenterExitInfo.DEFAULT_VELOCITY_BYPASS;
        }

        // Sweep a vertical (single-axis) ray; return per-input-code (travelFraction, outputMagnitude).
        private static List<(double travel, double outMag)> Sweep(int dev)
        {
            var pts = new List<(double, double)>();
            for (int d = 1; d <= 127; d++)
            {
                var r = RunSequence(dev, new (byte, byte, int)[]
                {
                    ((byte)128, (byte)128, 3),
                    ((byte)128, (byte)(128 - d), 4),
                });
                pts.Add((d / 127.0, r.mag));
            }
            return pts;
        }

        private static double AvgInBand(List<(double travel, double outMag)> pts, double maxTravel)
        {
            double sum = 0.0; int n = 0;
            foreach (var p in pts)
            {
                if (p.travel <= maxTravel) { sum += p.outMag; n++; }
            }
            return n > 0 ? sum / n : 0.0;
        }

        [TestMethod]
        public void SCE_Measure_DumpReport()
        {
            var sb = new System.Text.StringBuilder();
            void Report(string name, int dev, int dz, int anti, double exp, List<(double, double)> off)
            {
                ConfigureDz(dev, true, dz, anti, exp);
                var on = Sweep(dev);
                double[] bands = { 0.05, 0.10, 0.15 };
                sb.AppendLine($"== {name} (DZ={dz}, antiDead={anti}, exponent={exp}) ==");
                foreach (var b in bands)
                {
                    double offAvg = AvgInBand(off, b);
                    double onAvg = AvgInBand(on, b);
                    double gain = offAvg > 1e-9 ? onAvg / offAvg : 1.0;
                    sb.AppendLine(
                        $"  first {b * 100,4:F0}% travel: OFF avg={offAvg:F4}  SCE avg={onAvg:F4}  gain={gain * 100:F1}%");
                }
            }

            // Scenario A: shipped defaults (DZ=10, antiDead=20)
            int devA = 0;
            ConfigureDz(devA, false, 10, 20, 1.8);
            var offA = Sweep(devA);
            Report("A: DEFAULT deadzone", devA, 10, 20, 1.8, offA);

            // Scenario B: SCE-meaningful (no deadzone, no anti-deadzone) at several exponents
            int devB = 1;
            ConfigureDz(devB, false, 0, 0, 1.8);
            var offB = Sweep(devB);
            Report("B: raw (DZ=0, anti=0), exp=1.4", devB, 0, 0, 1.4, offB);
            Report("B: raw (DZ=0, anti=0), exp=1.8 [default]", devB, 0, 0, 1.8, offB);
            Report("B: raw (DZ=0, anti=0), exp=2.5", devB, 0, 0, 2.5, offB);

            // Scenario C: small deadzone, no anti-deadzone (plausible "raw feel" config)
            int devC = 2;
            ConfigureDz(devC, false, 3, 0, 1.8);
            var offC = Sweep(devC);
            Report("C: DZ=3, anti=0, exp=1.8", devC, 3, 0, 1.8, offC);

            string path = System.IO.Path.Combine(
                System.AppContext.BaseDirectory, "sce_measurements.txt");
            System.IO.File.WriteAllText(path, sb.ToString());
            System.Console.WriteLine(sb.ToString());
            System.Console.WriteLine($"WROTE: {path}");
        }

        /// <summary>Hermite SCE must ignore velocityBypass (legacy field).</summary>
        [TestMethod]
        public void SCEVelocityBypass_IsRuntimeNoOp()
        {
            double AvgMag(int dev, byte x, byte y)
            {
                var steps = new List<(byte, byte, int)> { ((byte)128, (byte)128, 5), (x, y, 90) };
                var mags = new List<double>();
                foreach (var (sx, sy, frames) in steps)
                {
                    for (int i = 0; i < frames; i++)
                    {
                        DS4State input = new DS4State()
                        {
                            LX = sx, LY = sy, RX = 128, RY = 128, elapsedTime = 0.008,
                        };
                        DS4State outState = new DS4State();
                        Mapping.SetCurveAndDeadzone(dev, input, outState);
                        double mx = outState.PreciseLX / 32767.0;
                        double my = outState.PreciseLY / 32767.0;
                        mags.Add(Math.Sqrt(mx * mx + my * my));
                    }
                }
                double sum = 0.0; int n = 0;
                for (int i = mags.Count - 60; i < mags.Count; i++) { sum += mags[i]; n++; }
                return sum / n;
            }

            Configure(0, sceEnabled: true);
            Global.GetLSSoftCenterExitInfo(0).velocityBypass = 0.05;
            Configure(1, sceEnabled: true);
            Global.GetLSSoftCenterExitInfo(1).velocityBypass = 0.95;

            byte x = 145, y = 112; // small diagonal in SCE range
            Assert.AreEqual(AvgMag(0, x, y), AvgMag(1, x, y), 2e-4,
                "velocityBypass must not affect Hermite SCE output.");
        }

        [TestMethod]
        public void ProfileEditor_DoesNotExposeVelocityBypassInUI()
        {
            string xamlPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                System.AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "APP", "DS4Forms", "ProfileEditor.xaml"));
            Assert.IsTrue(System.IO.File.Exists(xamlPath), $"ProfileEditor.xaml not found at {xamlPath}");
            string xaml = System.IO.File.ReadAllText(xamlPath);
            Assert.IsFalse(xaml.Contains("SoftCenterExitVelocityBypass", StringComparison.OrdinalIgnoreCase),
                "SCE velocityBypass must not appear in ProfileEditor UI bindings.");
            Assert.IsFalse(xaml.Contains("Velocity Bypass", StringComparison.OrdinalIgnoreCase),
                "SCE velocityBypass label must not appear in ProfileEditor.");
        }
    }
}
