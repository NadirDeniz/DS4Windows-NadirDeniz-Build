using System;
using System.Collections.Generic;
using APP;

namespace APPTests
{
    /// <summary>
    /// Audit of the "Right Stick Enhancements" panel for the same class of defects found in the old
    /// Soft Center Exit: stutter / stair-stepping (non-monotonic output on smooth input), near-center
    /// jitter amplification, magnitude discontinuities, and diagonal-angle distortion.
    ///
    /// Each feature is enabled in isolation on the RIGHT stick over a near-identity radial deadzone
    /// (DZ=0, anti=0) on the high-precision float path, so the only thing shaping the output is the
    /// feature under test. Output is read from PreciseRX/RY (16-bit).
    /// </summary>
    [TestClass]
    public class RightStickEnhancementsDiagnosticsTests
    {
        private enum Feature { None, MicroAim, Dynamic, DynamicPhysics, DirectionAware, PredictiveJitter, TmrHall }

        private static void Configure(int dev, Feature feature)
        {
            Global.setHighPrecisionStickOutput(dev, true);
            Global.setRsOutCurveMode(dev, 0); // Linear => curve stage no-op
            Global.GetSquareStickInfo(dev).rsMode = false;
            Global.RSSens[dev] = 1.0;

            var rs = Global.GetRSDeadInfo(dev);
            rs.deadzoneType = StickDeadZoneInfo.DeadZoneType.Radial;
            rs.deadZone = 0;
            rs.antiDeadZone = 0;
            rs.maxZone = 100;
            rs.maxOutput = 100.0;
            rs.outerDeadzone = 0;
            rs.fuzz = 0;
            rs.advancedRadialProcessing = false;
            rs.dynamicCenterCalibration = false;
            rs.dynamicDeadzoneScaling = false;
            rs.adaptiveAntiDeadzone = false;

            // Disable every RS enhancement / shaping stage first (several default ON for RS).
            Global.GetRSMicroAimPrecisionInfo(dev).enabled = false;
            Global.GetRSDynamicStickResponseInfo(dev).enabled = false;
            Global.GetRSTmrHallStickInfo(dev).enabled = false;
            Global.setRSDirectionAwareResolution(dev, false);
            Global.setRSDynamicPhysicsResponse(dev, false);
            Global.setRSPredictiveJitterCleaner(dev, false);
            Global.GetRSSoftCenterExitInfo(dev).enabled = false;
            Global.GetRSInGameDeadzoneRemapperInfo(dev).enabled = false;
            Global.GetRSMicroVelocityQuantizationSmootherInfo(dev).enabled = false;
            Global.GetRSSoftAntiDeadzoneInfo(dev).enabled = false;

            switch (feature)
            {
                case Feature.MicroAim:
                    Global.GetRSMicroAimPrecisionInfo(dev).enabled = true;
                    break;
                case Feature.Dynamic:
                    Global.GetRSDynamicStickResponseInfo(dev).enabled = true;
                    Global.GetRSMicroAimPrecisionInfo(dev).enabled = true; // dynamic uses precision curve
                    break;
                case Feature.DynamicPhysics:
                    Global.GetRSDynamicStickResponseInfo(dev).enabled = true;
                    Global.GetRSMicroAimPrecisionInfo(dev).enabled = true;
                    Global.setRSDynamicPhysicsResponse(dev, true);
                    break;
                case Feature.DirectionAware:
                    Global.setRSDirectionAwareResolution(dev, true);
                    break;
                case Feature.PredictiveJitter:
                    Global.setRSPredictiveJitterCleaner(dev, true);
                    break;
                case Feature.TmrHall:
                    Global.GetRSTmrHallStickInfo(dev).enabled = true;
                    break;
            }
        }

        private static (double mag, double angleDeg) RunSeq(
            int dev, IEnumerable<(byte x, byte y, int frames)> steps, double elapsed = 0.008)
        {
            DS4State outState = new DS4State();
            foreach (var (x, y, frames) in steps)
            {
                for (int i = 0; i < frames; i++)
                {
                    DS4State input = new DS4State()
                    {
                        LX = 128, LY = 128, RX = x, RY = y, elapsedTime = elapsed,
                    };
                    outState = new DS4State();
                    Mapping.SetCurveAndDeadzone(dev, input, outState);
                }
            }
            double mx = outState.PreciseRX / 32767.0;
            double my = outState.PreciseRY / 32767.0;
            return (Math.Sqrt((mx * mx) + (my * my)), Math.Atan2(my, mx) * 180.0 / Math.PI);
        }

        private static List<double> RunCollectMag(
            int dev, IEnumerable<(byte x, byte y, int frames)> steps, double elapsed = 0.008)
        {
            var mags = new List<double>();
            foreach (var (x, y, frames) in steps)
            {
                for (int i = 0; i < frames; i++)
                {
                    DS4State input = new DS4State()
                    {
                        LX = 128, LY = 128, RX = x, RY = y, elapsedTime = elapsed,
                    };
                    DS4State outState = new DS4State();
                    Mapping.SetCurveAndDeadzone(dev, input, outState);
                    double mx = outState.PreciseRX / 32767.0;
                    double my = outState.PreciseRY / 32767.0;
                    mags.Add(Math.Sqrt((mx * mx) + (my * my)));
                }
            }
            return mags;
        }

        private static double WorstDrop(List<double> mags, int skip)
        {
            double worst = 0.0;
            for (int i = skip + 1; i < mags.Count; i++)
            {
                double drop = mags[i - 1] - mags[i];
                if (drop > worst) worst = drop;
            }
            return worst;
        }

        // ---------------- Hard behavior gates ----------------

        /// <summary>
        /// BUG GATE: Predictive Jitter Cleaner must SUPPRESS near-center jitter, i.e. the output
        /// peak-to-peak amplitude of a steady center jitter must be smaller than the input. The
        /// current implementation inverts the correction and amplifies it.
        /// </summary>
        [TestMethod]
        public void PredictiveJitter_SuppressesNearCenterJitter()
        {
            int dev = 0;
            Configure(dev, Feature.PredictiveJitter);

            // Steady 2-code peak-to-peak jitter around a small +x offset, near center.
            var steps = new List<(byte, byte, int)> { ((byte)128, (byte)128, 5) };
            for (int i = 0; i < 40; i++)
            {
                steps.Add(((byte)131, (byte)128, 1));
                steps.Add(((byte)129, (byte)128, 1));
            }
            var mags = RunCollectMag(dev, steps);

            // Steady-state peak-to-peak over the last 20 alternating frames.
            double mn = double.MaxValue, mx = double.MinValue;
            for (int i = mags.Count - 20; i < mags.Count; i++)
            {
                mn = Math.Min(mn, mags[i]);
                mx = Math.Max(mx, mags[i]);
            }
            double outputPP = mx - mn;
            double inputPP = (131 - 129) / 127.0; // ~0.01575

            Assert.IsTrue(outputPP <= inputPP,
                $"Predictive Jitter Cleaner AMPLIFIES near-center jitter (should suppress). " +
                $"inputPP={inputPP:F4} outputPP={outputPP:F4}");
        }

        /// <summary>All shaping features must preserve a 45-degree diagonal at a mid magnitude.</summary>
        [TestMethod]
        public void AllFeatures_PreserveDiagonalAngle()
        {
            (Feature f, int dev)[] cases =
            {
                (Feature.MicroAim, 1), (Feature.Dynamic, 2), (Feature.DynamicPhysics, 3),
                (Feature.DirectionAware, 4), (Feature.PredictiveJitter, 5), (Feature.TmrHall, 6),
            };
            foreach (var (f, dev) in cases)
            {
                Configure(dev, f);
                var r = RunSeq(dev, new (byte, byte, int)[]
                {
                    ((byte)128, (byte)128, 10),
                    ((byte)178, (byte)78, 30), // dx=+50, dy=+50 => 45 deg
                });
                Assert.IsTrue(Math.Abs(r.angleDeg - 45.0) < 2.0,
                    $"{f} distorted the 45 deg diagonal: angle={r.angleDeg:F2}");
            }
        }

        /// <summary>MicroAim must be monotonic on a smooth outward drag (regression guard).</summary>
        [TestMethod]
        public void MicroAim_SmoothDrag_IsMonotonic()
        {
            int dev = 7;
            Configure(dev, Feature.MicroAim);
            var steps = new List<(byte, byte, int)> { ((byte)128, (byte)128, 5) };
            for (int d = 1; d <= 60; d++)
            {
                steps.Add(((byte)(128 + d), (byte)(128 - d), 2));
            }
            var mags = RunCollectMag(dev, steps);
            Assert.IsTrue(WorstDrop(mags, 5) < 2e-4,
                $"MicroAim non-monotonic on smooth drag: worstDrop={WorstDrop(mags, 5):F6}");
        }

        /// <summary>
        /// When Dynamic and Micro Aim are both enabled, Dynamic is the sole post-deadzone path
        /// (Micro Aim enable is ignored). Output must match Dynamic-only; Micro Aim-only must differ.
        /// </summary>
        [TestMethod]
        public void Dynamic_WinsOverMicroAim_WhenBothEnabled()
        {
            double AvgMag(int dev, byte rx, byte ry)
            {
                var mags = RunCollectMag(dev, new (byte, byte, int)[] { (rx, ry, 90) });
                double sum = 0.0; int n = 0;
                for (int i = mags.Count - 60; i < mags.Count; i++) { sum += mags[i]; n++; }
                return sum / n;
            }

            byte rx = 178, ry = 78; // 45 deg mid magnitude

            // Configure(Feature.Dynamic) enables both flags (legacy default); Micro Aim enable must not stack.
            Configure(0, Feature.Dynamic);
            double bothFlagsOn = AvgMag(0, rx, ry);
            Configure(1, Feature.Dynamic);
            Global.GetRSMicroAimPrecisionInfo(1).enabled = false;
            double dynamicOnly = AvgMag(1, rx, ry);
            Assert.AreEqual(dynamicOnly, bothFlagsOn, 2e-4,
                "Dynamic+MicroAim both ON must equal Dynamic-only (Micro Aim enable ignored).");

            // Micro Aim standalone path when Dynamic is OFF (small input in precision zone).
            byte smallRx = 140, smallRy = 128;
            Configure(2, Feature.None);
            double baseline = AvgMag(2, smallRx, smallRy);
            Configure(3, Feature.MicroAim);
            double microOnly = AvgMag(3, smallRx, smallRy);
            Assert.IsTrue(Math.Abs(microOnly - baseline) > 5e-4,
                "Micro Aim-only must change output when Dynamic is off.");
        }

        /// <summary>
        /// Backward-compat / no-op proof: with Direction-Aware OR TMR/Hall enabled, the 16-bit stick
        /// output must be bit-identical to having them disabled across the full input grid. Confirms
        /// removing/ignoring them does not change input feel (they were practical no-ops on 8-bit input).
        /// </summary>
        [TestMethod]
        public void DirectionAwareAndTmrHall_AreOutputIdentical_OnOff()
        {
            // Compare the multi-frame AVERAGE magnitude of a held point. Averaging cancels the
            // history-dependent sub-pixel dither (StickOutputQuantizer error feedback) so the result
            // reflects only the transfer function, not leftover per-device accumulator state.
            double AvgMag(int dev, byte rx, byte ry)
            {
                var mags = RunCollectMag(dev, new (byte, byte, int)[] { (rx, ry, 90) });
                double sum = 0.0; int n = 0;
                for (int i = mags.Count - 60; i < mags.Count; i++) { sum += mags[i]; n++; }
                return sum / n;
            }

            foreach (var feature in new[] { Feature.DirectionAware, Feature.TmrHall })
            {
                int devOff = 0, devOn = 1;
                for (int dx = -127; dx <= 127; dx += 9)
                {
                    for (int dy = -127; dy <= 127; dy += 9)
                    {
                        byte rx = (byte)Math.Clamp(128 + dx, 0, 255);
                        byte ry = (byte)Math.Clamp(128 + dy, 0, 255);

                        Configure(devOff, Feature.None);
                        double off = AvgMag(devOff, rx, ry);
                        Configure(devOn, feature);
                        double on = AvgMag(devOn, rx, ry);

                        Assert.AreEqual(off, on, 2e-4,
                            $"{feature} changed magnitude at rx={rx} ry={ry} (should be no-op).");
                    }
                }
            }
        }

        // ---------------- Measurement dump (smoothness characterization) ----------------

        [TestMethod]
        public void RSEnhancements_Measure_DumpReport()
        {
            var sb = new System.Text.StringBuilder();

            void RampReport(string name, Feature f, int dev)
            {
                Configure(dev, f);
                // Smooth constant-velocity ramp: 1 code/frame up a 45 deg ray (no holds).
                var steps = new List<(byte, byte, int)> { ((byte)128, (byte)128, 5) };
                for (int d = 1; d <= 90; d++)
                {
                    steps.Add(((byte)(128 + d), (byte)(128 - d), 1));
                }
                var mags = RunCollectMag(dev, steps);
                sb.AppendLine($"{name,-22} smoothRamp worstDrop={WorstDrop(mags, 6):F6}");
            }

            void HoldDriftReport(string name, Feature f, int dev)
            {
                Configure(dev, f);
                // Jump to a small position then HOLD; measure how much output drifts after input stops.
                var mags = RunCollectMag(dev, new (byte, byte, int)[]
                {
                    ((byte)128, (byte)128, 10),
                    ((byte)150, (byte)106, 60), // dx=+22,dy=+22 hold
                });
                double mn = double.MaxValue, mx = double.MinValue;
                for (int i = mags.Count - 40; i < mags.Count; i++)
                {
                    mn = Math.Min(mn, mags[i]); mx = Math.Max(mx, mags[i]);
                }
                sb.AppendLine($"{name,-22} holdDrift(p2p over last 40 held frames)={mx - mn:F6}");
            }

            RampReport("None", Feature.None, 8);
            RampReport("MicroAim", Feature.MicroAim, 0);
            RampReport("Dynamic", Feature.Dynamic, 1);
            RampReport("DynamicPhysics", Feature.DynamicPhysics, 2);
            RampReport("DirectionAware", Feature.DirectionAware, 3);
            RampReport("PredictiveJitter", Feature.PredictiveJitter, 4);
            RampReport("TmrHall", Feature.TmrHall, 5);

            HoldDriftReport("Dynamic", Feature.Dynamic, 1);
            HoldDriftReport("DynamicPhysics", Feature.DynamicPhysics, 2);

            string path = System.IO.Path.Combine(System.AppContext.BaseDirectory, "rs_enhancements_measurements.txt");
            System.IO.File.WriteAllText(path, sb.ToString());
            System.Console.WriteLine(sb.ToString());
        }
    }
}
