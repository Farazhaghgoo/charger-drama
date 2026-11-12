using System;
using System.IO;

namespace ChargerDrama
{
    /// <summary>
    /// Procedurally synthesized power-up / power-down sound effects (no audio files).
    /// Output: complete RIFF/WAVE bytes, PCM 44100 Hz, 16-bit, mono.
    /// </summary>
    internal static class SoundFx
    {
        private const int Rate = 44100;
        private const double TwoPi = Math.PI * 2.0;
        private const double TargetPeak = 0.70; // about -3 dBFS

        private static byte[] _powerUp;
        private static byte[] _powerDown;
        private static readonly object Gate = new object();

        public static byte[] PowerUpWav()
        {
            lock (Gate)
            {
                if (_powerUp == null) _powerUp = ToWav(BuildPowerUp());
                return (byte[])_powerUp.Clone();
            }
        }

        public static byte[] PowerDownWav()
        {
            lock (Gate)
            {
                if (_powerDown == null) _powerDown = ToWav(BuildPowerDown());
                return (byte[])_powerDown.Clone();
            }
        }

        // ------------------------------------------------------------------
        // Power up: ~1.3 s rising whine + crackle + two-note chime
        // ------------------------------------------------------------------
        private static double[] BuildPowerUp()
        {
            const double total = 1.30;
            const double sweepLen = 1.00;
            const double f0 = 150.0, f1 = 1400.0;
            int n = (int)(total * Rate);
            double[] buf = new double[n];
            Random rng = new Random(1337);

            // Whine
            double phase = 0.0, trem = 0.0;
            double ratio = f1 / f0;
            int sweepN = (int)(sweepLen * Rate);
            for (int i = 0; i < sweepN && i < n; i++)
            {
                double t = (double)i / Rate;
                double u = t / sweepLen;
                double f = f0 * Math.Pow(ratio, u);
                phase += TwoPi * f / Rate;

                double tremRate = 5.0 + 25.0 * u * u;
                trem += TwoPi * tremRate / Rate;
                double tremGain = 1.0 - 0.22 * (0.5 + 0.5 * Math.Sin(trem));

                // buzzy timbre: sine + softened odd harmonics (saw/square-like)
                double s = Math.Sin(phase)
                         + 0.30 * Math.Sin(3.0 * phase)
                         + 0.16 * Math.Sin(5.0 * phase)
                         + 0.09 * Math.Sin(7.0 * phase);
                s *= 0.62;

                double amp = 0.35 + 0.65 * Math.Pow(u, 0.8);
                // fade the whine out just before the chime
                double tail = 1.0;
                double fadeStart = sweepLen - 0.04;
                if (t > fadeStart) tail = 0.5 * (1.0 + Math.Cos(Math.PI * (t - fadeStart) / 0.04));

                buf[i] += s * amp * tremGain * tail * 0.55;
            }

            // Sparse crackle: short high-passed noise bursts
            int crackleEnd = (int)(0.98 * Rate);
            int pos = (int)(0.06 * Rate);
            while (pos < crackleEnd)
            {
                int len = 25 + rng.Next(50);
                double level = 0.05 + 0.08 * rng.NextDouble();
                double prev = 0.0;
                for (int k = 0; k < len && pos + k < n; k++)
                {
                    double w = rng.NextDouble() * 2.0 - 1.0;
                    double hp = w - prev; // crude high-pass
                    prev = w;
                    double env = Math.Exp(-5.0 * k / len);
                    buf[pos + k] += hp * 0.5 * env * level;
                }
                // gap 20 .. 130 ms
                pos += len + (int)((0.02 + 0.11 * rng.NextDouble()) * Rate);
            }

            // Ready chime: E6 then B6
            AddChime(buf, 1.000, 1318.5, 0.34, 0.25);
            AddChime(buf, 1.060, 1975.5, 0.30, 0.25);

            ApplyEnvelope(buf, 0.015, 0.060);
            Normalize(buf);
            return buf;
        }

        private static void AddChime(double[] buf, double startSec, double freq, double gain, double decaySec)
        {
            int start = (int)(startSec * Rate);
            int len = buf.Length - start;
            // tiny onset ramp (2 ms) to avoid a click at note start
            int ramp = (int)(0.002 * Rate);
            for (int i = 0; i < len; i++)
            {
                double t = (double)i / Rate;
                // exponential decay: about -40 dB at decaySec * 1.0 (tau = decay/4.6)
                double env = Math.Exp(-t * 4.6 / decaySec);
                if (i < ramp) env *= (double)i / ramp;
                double p = TwoPi * freq * t;
                double s = Math.Sin(p) + 0.22 * Math.Sin(2.0 * p) + 0.08 * Math.Sin(3.0 * p);
                buf[start + i] += s * env * gain;
            }
        }

        // ------------------------------------------------------------------
        // Power down: ~1.0 s falling whine, slowing tremolo, low thump
        // ------------------------------------------------------------------
        private static double[] BuildPowerDown()
        {
            const double total = 1.00;
            const double sweepLen = 0.85;
            const double f0 = 900.0, f1 = 60.0;
            int n = (int)(total * Rate);
            double[] buf = new double[n];

            double phase = 0.0, trem = 0.0;
            double ratio = f1 / f0;
            int sweepN = (int)(sweepLen * Rate);
            for (int i = 0; i < sweepN && i < n; i++)
            {
                double t = (double)i / Rate;
                double u = t / sweepLen;
                double f = f0 * Math.Pow(ratio, u);
                phase += TwoPi * f / Rate;

                double tremRate = 24.0 - 20.0 * Math.Sqrt(u); // slows down
                trem += TwoPi * tremRate / Rate;
                double tremGain = 1.0 - (0.18 + 0.12 * u) * (0.5 + 0.5 * Math.Sin(trem));

                double s = Math.Sin(phase)
                         + 0.26 * Math.Sin(3.0 * phase)
                         + 0.12 * Math.Sin(5.0 * phase);
                s *= 0.70;

                // volume fades as it deflates
                double amp = Math.Pow(1.0 - u, 1.3);
                buf[i] += s * amp * tremGain * 0.75;
            }

            // soft low thump
            int ts = (int)(0.76 * Rate);
            int tramp = (int)(0.004 * Rate);
            for (int i = 0; ts + i < n; i++)
            {
                double t = (double)i / Rate;
                double f = 68.0 - 14.0 * Math.Min(1.0, t / 0.15); // 68 -> 54 Hz
                double env = Math.Exp(-t / 0.07);
                if (i < tramp) env *= (double)i / tramp;
                buf[ts + i] += Math.Sin(TwoPi * f * t) * env * 0.55;
            }

            ApplyEnvelope(buf, 0.012, 0.045);
            Normalize(buf);
            return buf;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        private static void ApplyEnvelope(double[] buf, double attackSec, double releaseSec)
        {
            int n = buf.Length;
            int a = (int)(attackSec * Rate);
            int r = (int)(releaseSec * Rate);
            for (int i = 0; i < a && i < n; i++)
                buf[i] *= 0.5 * (1.0 - Math.Cos(Math.PI * i / a));        // 0 at i = 0
            for (int i = 0; i < r && i < n; i++)
            {
                int idx = n - 1 - i;                                      // 0 at last sample
                buf[idx] *= 0.5 * (1.0 - Math.Cos(Math.PI * i / r));
            }
        }

        private static void Normalize(double[] buf)
        {
            double peak = 0.0;
            for (int i = 0; i < buf.Length; i++)
            {
                double a = Math.Abs(buf[i]);
                if (a > peak) peak = a;
            }
            if (peak < 1e-9) return;
            double g = TargetPeak / peak;
            for (int i = 0; i < buf.Length; i++) buf[i] *= g;
        }

        private static byte[] ToWav(double[] samples)
        {
            int dataLen = samples.Length * 2;
            using (MemoryStream ms = new MemoryStream(44 + dataLen))
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                w.Write(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                w.Write(36 + dataLen);
                w.Write(new byte[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
                w.Write(new byte[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                w.Write(16);
                w.Write((short)1);          // PCM
                w.Write((short)1);          // mono
                w.Write(Rate);
                w.Write(Rate * 2);          // byte rate
                w.Write((short)2);          // block align
                w.Write((short)16);         // bits
                w.Write(new byte[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                w.Write(dataLen);
                for (int i = 0; i < samples.Length; i++)
                {
                    double v = Math.Round(samples[i] * 32767.0);
                    if (v > 32767.0) v = 32767.0;
                    if (v < -32768.0) v = -32768.0;
                    w.Write((short)v);
                }
                w.Flush();
                return ms.ToArray();
            }
        }
    }
}
