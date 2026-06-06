using System;
using System.Collections.Generic;
using APP;

namespace APPTests
{
    /// <summary>
    /// Verifies the Right Stick "Soft Anti-Deadzone" feature: that it is applied as a SMOOTH
    /// alternative to the classic radial anti-deadzone, that the new smoothstep soft-knee does not
    /// introduce pixel-skipping / early quantization, and that it preserves angle / diagonals /
    /// monotonicity and adds no temporal lag.
    ///
    /// All runs use the RIGHT stick on the high-precision float path with a near-identity radial
    /// deadzone and a linear output curve, so the only shaping is the stage under test. The 16-bit
    /// output is read from PreciseRX/RY. Magnitudes are averaged over held frames to cancel the
    /// history-dependent sub-pixel dither of the final output quantizer.
    /// </summary>
    [TestClass]
    public class SoftAntiDeadzoneDiagnosticsTests
    {
        private const int Size = 95;
        private const double Ramp = 0.90;
        private const double Exp = 2.00;

        // ---- Reference curve math (mirrors SoftAntiDeadzoneProcessor; internal -> duplicated here) ----

        private static double Clamp01(double v) => Math.Clamp(v, 0.0, 1.0);

        /// <summary>Previous shipped curve: smootherstep(t^exp) soft-knee.</summary>
        private static double PrevCurve(double input, int size, double ramp, double exp)
        {
            input = Clamp01(input);
            if (input <= 1e-9) return 0.0;
            double rz = Math.Clamp(ramp, 0.01, 0.95);
            double target = Math.Clamp(size * 0.01, 0.0, 0.95);
            double e = Math.Clamp(exp, 0.5, 8.0);
            double classic = target + (1.0 - target) * input;
            if (input >= rz) return classic;
            double t = input / rz;
            double s = Math.Pow(t, e);
            double w = s * s * s * (s * (s * 6.0 - 15.0) + 10.0);
            return input + w * (classic - input);
        }

        /// <summary>New curve: smoothstep(t^exp) soft-knee (lower peak slope), blended to classic.</summary>
        private static double NewCurve(double input, int size, double ramp, double exp)
        {
            input = Clamp01(input);
            if (input <= 1e-9) return 0.0;
            double rz = Math.Clamp(ramp, 0.01, 0.95);
            double target = Math.Clamp(size * 0.01, 0.0, 0.95);
            double e = Math.Clamp(exp, 0.5, 8.0);
            double classic = target + (1.0 - target) * input;
            if (input >= rz) return classic;
            double t = input / rz;
            double s = Math.Pow(t, e);
            double w = s * s * (3.0 - 2.0 * s);
            return input + w * (classic - input);
        }

        /// <summary>Soft Center Exit Hermite magnitude curve (mirrors SoftCenterExitProcessor).</summary>
        private static double SceCurve(double mag, double range, double exp)
        {
            mag = Clamp01(mag);
            if (mag <= 1e-9) return 0.0;
            double r = Math.Clamp(range, 0.001, 0.95);
            if (mag >= r) return mag;
            double k = Math.Clamp(1.0 / Math.Clamp(exp, 0.5, 8.0), 0.0, 1.0);
            double t = mag / r;
            double t2 = t * t, t3 = t2 * t;
            double h = (-t3 + 2.0 * t2) + k * (t3 - 2.0 * t2 + t);
            return h * r;
        }

        // ---- Pipeline harness ----

        private static void Configure(
            int dev,
            bool softEnabled, int size, double ramp, double exponent,
            int classicAntiDead = 0,
            bool softCenterExit = false)
        {
            Global.setHighPrecisionStickOutput(dev, true);
            Global.setRsOutCurveMode(dev, 0);
            Global.GetSquareStickInfo(dev).rsMode = false;
            Global.RSSens[dev] = 1.0;

            var rs = Global.GetRSDeadInfo(dev);
            rs.deadzoneType = StickDeadZoneInfo.DeadZoneType.Radial;
            rs.deadZone = 0;
            rs.antiDeadZone = classicAntiDead;
            rs.maxZone = 100;
            rs.maxOutput = 100.0;
            rs.outerDeadzone = 0;
            rs.fuzz = 0;
            rs.advancedRadialProcessing = false;
            rs.dynamicCenterCalibration = false;
            rs.dynamicDeadzoneScaling = false;
            rs.adaptiveAntiDeadzone = false;

            Global.GetRSMicroAimPrecisionInfo(dev).enabled = false;
            Global.GetRSDynamicStickResponseInfo(dev).enabled = false;
            Global.GetRSTmrHallStickInfo(dev).enabled = false;
            Global.setRSDirectionAwareResolution(dev, false);
            Global.setRSDynamicPhysicsResponse(dev, false);
            Global.setRSPredictiveJitterCleaner(dev, false);
            Global.GetRSInGameDeadzoneRemapperInfo(dev).enabled = false;
            Global.GetRSMicroVelocityQuantizationSmootherInfo(dev).enabled = false;

            Global.GetRSSoftCenterExitInfo(dev).enabled = softCenterExit;

            var soft = Global.GetRSSoftAntiDeadzoneInfo(dev);
            soft.enabled = softEnabled;
            soft.compensationSize = size;
            soft.rampZone = ramp;
            soft.exponent = exponent;
        }

        private static List<double> RunCollectMag(
            int dev, IEnumerable<(byte x, byte y, int frames)> steps, double elapsed = 0.008)
        {
            var mags = new List<double>();
            foreach (var (x, y, frames) in steps)
            {
                for (int i = 0; i < frames; i++)
                {
                    DS4State input = new DS4State() { LX = 128, LY = 128, RX = x, RY = y, elapsedTime = elapsed };
                    DS4State outState = new DS4State();
                    Mapping.SetCurveAndDeadzone(dev, input, outState);
                    double mx = outState.PreciseRX / 32767.0;
                    double my = outState.PreciseRY / 32767.0;
                    mags.Add(Math.Sqrt(mx * mx + my * my));
                }
            }
            return mags;
        }

        private static double AvgMag(int dev, byte rx, byte ry)
        {
            var mags = RunCollectMag(dev, new (byte, byte, int)[] { (rx, ry, 90) });
            double sum = 0.0; int n = 0;
            for (int i = mags.Count - 60; i < mags.Count; i++) { sum += mags[i]; n++; }
            return sum / n;
        }

        private static (double x, double y) AvgVec(int dev, byte rx, byte ry)
        {
            DS4State outState = new DS4State();
            double sx = 0.0, sy = 0.0; int n = 0;
            for (int i = 0; i < 90; i++)
            {
                DS4State input = new DS4State() { LX = 128, LY = 128, RX = rx, RY = ry, elapsedTime = 0.008 };
                outState = new DS4State();
                Mapping.SetCurveAndDeadzone(dev, input, outState);
                if (i >= 30) { sx += outState.PreciseRX / 32767.0; sy += outState.PreciseRY / 32767.0; n++; }
            }
            return (sx / n, sy / n);
        }

        private static IEnumerable<(byte rx, byte ry)> Grid(int step = 17)
        {
            for (int dx = -119; dx <= 119; dx += step)
                for (int dy = -119; dy <= 119; dy += step)
                {
                    if (dx == 0 && dy == 0) continue;
                    yield return ((byte)Math.Clamp(128 + dx, 0, 255), (byte)Math.Clamp(128 + dy, 0, 255));
                }
        }

        // ---------------------------- Behavior gates ----------------------------

        [TestMethod]
        public void Enabled_IsMonotonic()
        {
            int dev = 0;
            Configure(dev, true, Size, Ramp, Exp);
            var steps = new List<(byte, byte, int)> { ((byte)128, (byte)128, 5) };
            for (int d = 1; d <= 100; d++) steps.Add(((byte)(128 + d), (byte)(128 - d), 2));
            var mags = RunCollectMag(dev, steps);
            double worstDrop = 0.0;
            for (int i = 12; i < mags.Count; i++) worstDrop = Math.Max(worstDrop, mags[i - 1] - mags[i]);
            Assert.IsTrue(worstDrop < 2e-3, $"Non-monotonic: worstDrop={worstDrop:F6}");
        }

        [TestMethod]
        public void Enabled_PreservesAngle()
        {
            Configure(2, true, Size, Ramp, Exp);
            foreach (var (rx, ry) in Grid())
            {
                double inAng = Math.Atan2(-(ry - 128), rx - 128) * 180.0 / Math.PI;
                var (ox, oy) = AvgVec(2, rx, ry);
                if (Math.Sqrt(ox * ox + oy * oy) < 0.02) continue;
                double outAng = Math.Atan2(oy, ox) * 180.0 / Math.PI;
                double diff = Math.Abs(((outAng - inAng + 540.0) % 360.0) - 180.0);
                Assert.IsTrue(diff < 2.0, $"Angle distorted at rx={rx} ry={ry}: in={inAng:F2} out={outAng:F2}");
            }
        }

        [TestMethod]
        public void Enabled_PreservesDiagonalRatio()
        {
            Configure(3, true, Size, Ramp, Exp);
            foreach (int d in new[] { 20, 40, 60, 90 })
            {
                var (ox, oy) = AvgVec(3, (byte)(128 + d), (byte)(128 - d));
                Assert.IsTrue(Math.Abs(Math.Abs(ox) - Math.Abs(oy)) < 5e-3,
                    $"Diagonal ratio broken at d={d}: ox={ox:F4} oy={oy:F4}");
            }
        }

        /// <summary>
        /// Near-center (input 0..0.20) the curve must EASE IN (per-code output step non-decreasing)
        /// with no step spiking far above its neighbours: no pixel-skipping during micro aim.
        /// </summary>
        [TestMethod]
        public void Enabled_NoPixelSkipping_NearCenter()
        {
            int dev = 4;
            Configure(dev, true, size: 30, ramp: 0.45, exponent: 1.5);
            // input mag for code d (+x) ~= d/127; 0.20 ~ code 25.
            var vals = new List<double>();
            for (int d = 0; d <= 25; d++) vals.Add(AvgMag(dev, (byte)(128 + d), 128));

            double prevStep = 0.0;
            for (int i = 1; i < vals.Count; i++)
            {
                double step = vals[i] - vals[i - 1];
                Assert.IsTrue(step > 0, $"Non-monotonic step at code {i}.");
                if (i >= 2)
                {
                    // ease-in: each step may only grow gently, never jump (no skip).
                    Assert.IsTrue(step <= prevStep * 1.6 + 5e-4,
                        $"Pixel-skip at code {i}: step={step:F5} prevStep={prevStep:F5}");
                }
                prevStep = step;
            }
        }

        /// <summary>Second difference of the output (delta change) stays bounded across the ramp.</summary>
        [TestMethod]
        public void Enabled_OutputDeltaIsSmooth()
        {
            int dev = 5;
            Configure(dev, true, size: 30, ramp: 0.45, exponent: 1.5);
            var vals = new List<double>();
            for (int d = 0; d <= 60; d++) vals.Add(AvgMag(dev, (byte)(128 + d), 128));

            var deltas = new List<double>();
            for (int i = 1; i < vals.Count; i++) deltas.Add(vals[i] - vals[i - 1]);

            double maxStep = 0.0;
            foreach (double dlt in deltas) maxStep = Math.Max(maxStep, dlt);
            double worstChange = 0.0;
            for (int i = 1; i < deltas.Count; i++)
                worstChange = Math.Max(worstChange, Math.Abs(deltas[i] - deltas[i - 1]));

            // Delta-change must be a small fraction of the largest step (no discontinuity).
            Assert.IsTrue(worstChange < maxStep * 0.5,
                $"Output delta not smooth: worstChange={worstChange:F6} maxStep={maxStep:F6}");
        }

        /// <summary>
        /// No early quantization: consecutive input codes produce strictly increasing DISTINCT 16-bit
        /// output (no collapse to repeated values), and the output tracks the continuous curve finer
        /// than the 8-bit output grid (~1/255), proving the float pipeline runs end-to-end.
        /// </summary>
        [TestMethod]
        public void Enabled_NoEarlyQuantization()
        {
            int dev = 6;
            int size = 30; double ramp = 0.45, exp = 1.5;
            Configure(dev, true, size, ramp, exp);

            double prev = -1.0;
            for (int d = 1; d <= 40; d++)
            {
                double m = AvgMag(dev, (byte)(128 + d), 128);
                Assert.IsTrue(m > prev + 1e-5, $"Output not strictly increasing per code at d={d} (quantization collapse).");
                double expected = NewCurve(d / 127.0, size, ramp, exp);
                Assert.AreEqual(expected, m, 2.5e-3,
                    $"Output coarser than float curve at d={d}: expected={expected:F5} actual={m:F5}");
                prev = m;
            }
        }

        /// <summary>Soft Anti-Deadzone + Soft Center Exit: finite, monotonic, smooth (no stutter profile).</summary>
        [TestMethod]
        public void Enabled_WithSCE_NoStutterProfile()
        {
            int dev = 7;
            Configure(dev, true, size: 30, ramp: 0.45, exponent: 1.5, classicAntiDead: 0, softCenterExit: true);
            var steps = new List<(byte, byte, int)> { ((byte)128, (byte)128, 5) };
            for (int d = 1; d <= 100; d++) steps.Add(((byte)(128 + d), (byte)(128 - d), 2));
            var mags = RunCollectMag(dev, steps);

            foreach (double m in mags)
                Assert.IsFalse(double.IsNaN(m) || double.IsInfinity(m), "NaN/Infinity produced.");
            double worstDrop = 0.0;
            for (int i = 12; i < mags.Count; i++) worstDrop = Math.Max(worstDrop, mags[i - 1] - mags[i]);
            Assert.IsTrue(worstDrop < 2e-3, $"SoftAD+SCE not monotonic: worstDrop={worstDrop:F6}");
        }

        /// <summary>
        /// Stateless / no temporal lag: the response to a held input is reached on the first frame
        /// after the jump (no multi-frame ramp), and is independent of how the input was approached.
        /// </summary>
        [TestMethod]
        public void Enabled_DoesNotAddTemporalLag()
        {
            int dev = 8;
            Configure(dev, true, size: 30, ramp: 0.45, exponent: 1.5);
            byte tx = (byte)(128 + 30);

            // Steady held value (dither-averaged).
            double steady = AvgMag(dev, tx, 128);

            // Single frame immediately after jumping from center.
            DS4State outState = new DS4State();
            for (int i = 0; i < 20; i++)
            {
                outState = new DS4State();
                Mapping.SetCurveAndDeadzone(dev, new DS4State { LX = 128, LY = 128, RX = 128, RY = 128, elapsedTime = 0.008 }, outState);
            }
            outState = new DS4State();
            Mapping.SetCurveAndDeadzone(dev, new DS4State { LX = 128, LY = 128, RX = tx, RY = 128, elapsedTime = 0.008 }, outState);
            double firstFrame = Math.Abs(outState.PreciseRX / 32767.0);
            Assert.IsTrue(Math.Abs(firstFrame - steady) < 8e-3,
                $"Temporal lag detected: firstFrame={firstFrame:F5} steady={steady:F5}");

            // Approach independence: from high vs from center, settled output must match.
            DS4State o2 = new DS4State();
            for (int i = 0; i < 40; i++)
            {
                o2 = new DS4State();
                byte src = i < 20 ? (byte)250 : tx;
                Mapping.SetCurveAndDeadzone(dev, new DS4State { LX = 128, LY = 128, RX = src, RY = 128, elapsedTime = 0.008 }, o2);
            }
            double fromHigh = Math.Abs(o2.PreciseRX / 32767.0);
            Assert.IsTrue(Math.Abs(fromHigh - steady) < 8e-3,
                $"Approach-dependent output (stateful): fromHigh={fromHigh:F5} steady={steady:F5}");
        }

        /// <summary>New curve has a lower peak local slope than the previous one (less skip) while
        /// remaining a genuine lift (out >= input) over the small-input region.</summary>
        [TestMethod]
        public void Enabled_SmootherThanPreviousCurve()
        {
            int size = 30; double ramp = 0.45, exp = 1.5;
            double peakPrev = 0, peakNew = 0, pp = 0, pn = 0;
            for (double inp = 0.001; inp <= ramp; inp += 0.001)
            {
                double p = PrevCurve(inp, size, ramp, exp);
                double n = NewCurve(inp, size, ramp, exp);
                peakPrev = Math.Max(peakPrev, (p - pp) / 0.001);
                peakNew = Math.Max(peakNew, (n - pn) / 0.001);
                pp = p; pn = n;
                Assert.IsTrue(n >= inp - 1e-9, $"New curve dipped below input at {inp}: {n:F5}");
            }
            Assert.IsTrue(peakNew < peakPrev,
                $"New curve peak slope not lower: prev={peakPrev:F3} new={peakNew:F3}");
        }

        [TestMethod]
        public void Disabled_BehaviorUnchanged()
        {
            foreach (var (rx, ry) in Grid())
            {
                Configure(0, softEnabled: false, size: 12, ramp: 0.25, exponent: 2.2, classicAntiDead: 20);
                double a = AvgMag(0, rx, ry);
                Configure(1, softEnabled: false, size: Size, ramp: Ramp, exponent: Exp, classicAntiDead: 20);
                double b = AvgMag(1, rx, ry);
                Assert.AreEqual(a, b, 2e-4, $"Disabled must ignore params at rx={rx} ry={ry}.");
            }
        }

        [TestMethod]
        public void Enabled_ChangesMagnitude_IsFelt()
        {
            double maxDelta = 0.0;
            foreach (var (rx, ry) in Grid())
            {
                Configure(0, softEnabled: false, size: Size, ramp: Ramp, exponent: Exp, classicAntiDead: 0);
                double off = AvgMag(0, rx, ry);
                Configure(1, softEnabled: true, size: Size, ramp: Ramp, exponent: Exp, classicAntiDead: 0);
                double on = AvgMag(1, rx, ry);
                maxDelta = Math.Max(maxDelta, Math.Abs(on - off));
            }
            Assert.IsTrue(maxDelta > 0.05, $"Imperceptible (maxDelta={maxDelta:F4}).");
        }

        [TestMethod]
        public void Enabled_DoesNotDoubleApplyWithClassicAntiDead()
        {
            foreach (var (rx, ry) in Grid())
            {
                Configure(0, softEnabled: true, size: Size, ramp: Ramp, exponent: Exp, classicAntiDead: 0);
                double a = AvgMag(0, rx, ry);
                Configure(1, softEnabled: true, size: Size, ramp: Ramp, exponent: Exp, classicAntiDead: 45);
                double b = AvgMag(1, rx, ry);
                Assert.AreEqual(a, b, 2e-4, $"Classic anti-dead leaked (double-apply) at rx={rx} ry={ry}.");
            }
        }

        // ---------------------------- Diagnostics dump ----------------------------

        /// <summary>Table: input, softADOutput, afterSCEOutput, delta, deltaChange (Size=30, Ramp=0.40, Exp=1.5).</summary>
        [TestMethod]
        public void Diagnostics_DeltaProfile_NearCenter()
        {
            int size = 30; double ramp = 0.40, exp = 1.5;
            double sceRange = 0.08, sceExp = 1.4;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Soft Anti-Deadzone + SCE  Size={size} Ramp={ramp} Exp={exp}  (SCE range={sceRange} exp={sceExp})");
            sb.AppendLine("input | softADOutput | afterSCEOutput | delta(SCE) | deltaChange");
            double prevAfter = 0, prevDelta = 0;
            for (double inp = 0.0; inp <= 0.2001; inp += 0.005)
            {
                double soft = NewCurve(inp, size, ramp, exp);
                double after = SceCurve(soft, sceRange, sceExp);
                double delta = inp == 0 ? 0 : after - prevAfter;
                double dchg = inp <= 0.005 ? 0 : delta - prevDelta;
                sb.AppendLine($"{inp,5:F3} | {soft,12:F5} | {after,14:F5} | {delta,10:F5} | {dchg,11:F5}");
                prevAfter = after; prevDelta = delta;
            }
            string path = System.IO.Path.Combine(System.AppContext.BaseDirectory, "soft_antidead_delta_profile.txt");
            System.IO.File.WriteAllText(path, sb.ToString());
            System.Console.WriteLine(sb.ToString());
        }
    }
}
