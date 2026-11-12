using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ChargerDrama
{
    internal enum Mood { Neutral, Happy, Laughing, Relieved, Giggle, Crying, Sad, Weak, Dying, Angry, Grumpy, Surprised }

    internal enum EyeShape { Open, Wide, Happy, Closed, Angry, Sad, HalfLid, Droopy }
    internal enum MouthShape { SmallSmile, Smile, Laugh, Frown, Wavy, O, Flat }
    internal enum BrowShape { None, Angry, Sad, Raised }

    // Everything needed to draw one frame of the mascot.
    internal sealed class MascotFrame
    {
        public float T;
        public float Scale = 1f, Alpha = 1f, SquashX = 1f, SquashY = 1f, Bob, ShakeX, Tilt;
        public EyeShape Eyes = EyeShape.Open;
        public MouthShape Mouth = MouthShape.SmallSmile;
        public BrowShape Brows = BrowShape.None;
        public float Blink, PupilX, PupilY, MouthOpen;
        public float ArmL = 15f, ArmR = 15f;          // degrees from hanging straight down; 180 = straight up
        public float Level = 0.8f;
        public bool Charging, Tears, Sweat, Steam, Sparkles, Exclaim, Blush, Muted, Pale, RedTint, Bolt;
        public string Bubble;
        public float BubbleAlpha;
    }

    // "Volty": a little battery with a face. Pure drawing, no state.
    internal static class MascotRenderer
    {
        public const int Width = 300, Height = 240;      // logical canvas (96 dpi)
        public const float AX = 236f, AY = 228f;         // the mascot's feet

        private static readonly Color Ink = Color.FromArgb(43, 45, 66);
        private static readonly Color CasingTop = Color.FromArgb(255, 249, 232);
        private static readonly Color CasingBottom = Color.FromArgb(241, 228, 195);
        private static readonly Color Lid = Color.FromArgb(232, 214, 176);
        private static readonly RectangleF Body = new RectangleF(-36f, -114f, 72f, 98f);

        private static float alpha = 1f;

        private static Color A(Color c) { return Color.FromArgb(Clamp255(c.A * alpha), c.R, c.G, c.B); }
        private static Color A(int a, int r, int g, int b) { return Color.FromArgb(Clamp255(a * alpha), r, g, b); }
        private static Color A(int r, int g, int b) { return A(255, r, g, b); }
        private static int Clamp255(float v) { return v < 0 ? 0 : v > 255 ? 255 : (int)v; }

        public static void Draw(Graphics g, MascotFrame f, float dpi)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            g.Clear(Color.Transparent);
            g.ResetTransform();
            g.ScaleTransform(dpi, dpi);

            if (f.Alpha > 0.01f && f.Scale > 0.01f) DrawMascot(g, f);

            // the bubble goes on top so steam and sparkles never cover the text
            if (f.BubbleAlpha > 0.01f && !string.IsNullOrEmpty(f.Bubble))
            {
                alpha = f.BubbleAlpha * Math.Min(1f, f.Alpha * 1.5f);
                DrawBubble(g, f.Bubble);
            }
        }

        private static void DrawMascot(Graphics g, MascotFrame f)
        {
            alpha = f.Alpha;
            var outer = g.Save();
            g.TranslateTransform(AX + f.ShakeX, AY);
            DrawShadow(g, f);
            g.TranslateTransform(0, f.Bob);
            g.RotateTransform(f.Tilt);
            g.ScaleTransform(f.Scale * f.SquashX, f.Scale * f.SquashY);

            DrawArm(g, -1, f.ArmL);
            DrawArm(g, 1, f.ArmR);
            DrawLegs(g);
            DrawTerminal(g);
            DrawBody(g, f);
            DrawFace(g, f);
            DrawEffects(g, f);
            g.Restore(outer);
        }

        // ---- body ---------------------------------------------------------------------
        private static void DrawShadow(Graphics g, MascotFrame f)
        {
            float lift = Math.Max(0f, -f.Bob);
            float w = 64f * f.Scale * (1f - Math.Min(0.5f, lift / 30f));
            using (var b = new SolidBrush(A((int)(55 * (1f - Math.Min(0.6f, lift / 25f))), 0, 0, 0)))
                g.FillEllipse(b, -w / 2f, -5f, w, 9f);
        }

        private static void DrawLegs(Graphics g)
        {
            using (var b = new SolidBrush(A(Ink)))
            {
                g.FillRectangle(b, -18f, -20f, 10f, 15f);
                g.FillRectangle(b, 8f, -20f, 10f, 15f);
                g.FillEllipse(b, -25f, -8f, 19f, 9f);
                g.FillEllipse(b, 6f, -8f, 19f, 9f);
            }
        }

        private static void DrawArm(Graphics g, int side, float angle)
        {
            double a = angle * Math.PI / 180.0;
            var sx = side * 32f; var sy = -64f;
            var hx = sx + side * (float)Math.Sin(a) * 25f;
            var hy = sy + (float)Math.Cos(a) * 25f;
            using (var p = new Pen(A(Ink), 7.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(p, sx, sy, hx, hy);
            using (var b = new SolidBrush(A(CasingTop)))
            using (var p = new Pen(A(Ink), 2.5f))
            {
                g.FillEllipse(b, hx - 6.5f, hy - 6.5f, 13f, 13f);
                g.DrawEllipse(p, hx - 6.5f, hy - 6.5f, 13f, 13f);
            }
        }

        private static void DrawTerminal(Graphics g)
        {
            using (var path = RoundRect(new RectangleF(-13f, -123f, 26f, 12f), 4f))
            using (var b = new SolidBrush(A(200, 204, 214)))
            using (var p = new Pen(A(Ink), 3f))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
        }

        private static void DrawBody(Graphics g, MascotFrame f)
        {
            using (var body = RoundRect(Body, 20f))
            {
                using (var casing = new LinearGradientBrush(new PointF(0, Body.Top), new PointF(0, Body.Bottom), A(CasingTop), A(CasingBottom)))
                    g.FillPath(casing, body);

                var clip = g.Clip;
                g.SetClip(body, CombineMode.Intersect);

                // charge level, like liquid inside the battery
                float level = Math.Max(0.06f, Math.Min(1f, f.Level));
                float top = Body.Bottom - Body.Height * level;
                Color c = level > 0.5f ? Color.FromArgb(52, 199, 89) : level > 0.2f ? Color.FromArgb(255, 204, 0) : Color.FromArgb(255, 69, 58);
                using (var liquid = new GraphicsPath())
                {
                    float waveAmp = f.Charging ? 2.4f : 0.8f;
                    var pts = new System.Collections.Generic.List<PointF>();
                    for (float x = Body.Left - 2; x <= Body.Right + 2; x += 3f)
                        pts.Add(new PointF(x, top + (float)Math.Sin(x * 0.16f + f.T * (f.Charging ? 4.5f : 1.5f)) * waveAmp));
                    pts.Add(new PointF(Body.Right + 2, Body.Bottom + 2));
                    pts.Add(new PointF(Body.Left - 2, Body.Bottom + 2));
                    liquid.AddPolygon(pts.ToArray());
                    using (var lb = new LinearGradientBrush(new PointF(0, top - 3), new PointF(0, Body.Bottom), A(Lighten(c, 0.25f)), A(c)))
                        g.FillPath(lb, liquid);
                }
                if (f.Charging)
                {
                    using (var bub = new SolidBrush(A(130, 255, 255, 255)))
                    {
                        float span = Math.Max(8f, Body.Bottom - top - 4f);
                        for (int i = 0; i < 5; i++)
                        {
                            float y = Body.Bottom - 3f - ((f.T * 18f + i * 13.7f) % span);
                            float x = -24f + i * 12f + (float)Math.Sin(f.T * 3f + i) * 2f;
                            g.FillEllipse(bub, x - 2f, y - 2f, 4.4f, 4.4f);
                        }
                    }
                }
                if (f.Pale) using (var b = new SolidBrush(A(110, 190, 196, 205))) g.FillPath(b, body);
                if (f.RedTint) using (var b = new SolidBrush(A(60, 255, 60, 40))) g.FillPath(b, body);

                g.Clip = clip;

                using (var hl = RoundRect(new RectangleF(-29f, -106f, 7f, 60f), 3.5f))
                using (var b = new SolidBrush(A(95, 255, 255, 255)))
                    g.FillPath(b, hl);
                using (var p = new Pen(A(Ink), 3.5f))
                    g.DrawPath(p, body);
            }
        }

        // ---- face ---------------------------------------------------------------------
        private static void DrawFace(Graphics g, MascotFrame f)
        {
            if (f.Blush)
                using (var b = new SolidBrush(A(110, 255, 120, 140)))
                {
                    g.FillEllipse(b, -31f, -68f, 13f, 7f);
                    g.FillEllipse(b, 18f, -68f, 13f, 7f);
                }
            DrawEye(g, f, -1);
            DrawEye(g, f, 1);
            DrawBrows(g, f.Brows);
            DrawMouth(g, f);
        }

        private static void DrawEye(Graphics g, MascotFrame f, int side)
        {
            float cx = side * 15f, cy = -84f;
            var shape = f.Eyes;
            bool blinkable = shape == EyeShape.Open || shape == EyeShape.Wide || shape == EyeShape.Sad || shape == EyeShape.HalfLid || shape == EyeShape.Angry;
            if (blinkable && f.Blink > 0.75f) shape = EyeShape.Closed;

            using (var ink = new Pen(A(Ink), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                if (shape == EyeShape.Happy) { g.DrawArc(ink, cx - 8f, cy - 3f, 16f, 13f, 180f, 180f); return; }
                if (shape == EyeShape.Closed) { g.DrawArc(ink, cx - 8f, cy - 6f, 16f, 11f, 0f, 180f); return; }
            }

            float w = shape == EyeShape.Wide ? 23f : 20f;
            float h = (shape == EyeShape.Wide ? 27f : 24f) * (blinkable ? 1f - f.Blink * 0.8f : 1f);
            var eye = new RectangleF(cx - w / 2f, cy - h / 2f, w, h);
            using (var white = new SolidBrush(A(Color.White)))
                g.FillEllipse(white, eye);

            float pr = shape == EyeShape.Wide ? 4f : 5.2f;
            float px = cx + f.PupilX * (w / 2f - pr - 1.5f);
            float py = cy + f.PupilY * (h / 2f - pr - 1.5f);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(eye);
                var clip = g.Clip;
                g.SetClip(path, CombineMode.Intersect);
                using (var pupil = new SolidBrush(A(30, 30, 46)))
                    g.FillEllipse(pupil, px - pr, py - pr, pr * 2f, pr * 2f);
                using (var shine = new SolidBrush(A(Color.White)))
                    g.FillEllipse(shine, px - pr * 0.55f - 1f, py - pr * 0.55f - 1f, pr * 0.7f, pr * 0.7f);

                using (var lid = new SolidBrush(A(Lid)))
                using (var lidLine = new Pen(A(Ink), 2.2f))
                {
                    if (shape == EyeShape.HalfLid || shape == EyeShape.Droopy)
                    {
                        float cover = shape == EyeShape.Droopy ? 0.68f : 0.45f;
                        float ly = eye.Top + eye.Height * cover;
                        g.FillRectangle(lid, eye.Left - 1, eye.Top - 1, eye.Width + 2, ly - eye.Top + 1);
                        g.DrawLine(lidLine, eye.Left, ly, eye.Right, ly);
                    }
                    else if (shape == EyeShape.Angry)
                    {
                        // lid slanting down towards the nose
                        float inner = side < 0 ? eye.Right : eye.Left, outer = side < 0 ? eye.Left : eye.Right;
                        var poly = new[] { new PointF(outer, eye.Top - 2), new PointF(inner, eye.Top - 2), new PointF(inner, cy + 1f), new PointF(outer, cy - 7f) };
                        g.FillPolygon(lid, poly);
                        g.DrawLine(lidLine, outer, cy - 7f, inner, cy + 1f);
                    }
                }
                g.Clip = clip;
            }
            using (var ink = new Pen(A(Ink), 2.2f))
                g.DrawEllipse(ink, eye);
        }

        private static void DrawBrows(Graphics g, BrowShape brows)
        {
            if (brows == BrowShape.None) return;
            using (var p = new Pen(A(Ink), 3.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float outer = side * 25f, inner = side * 7f;
                    switch (brows)
                    {
                        case BrowShape.Angry: g.DrawLine(p, outer, -105f, inner, -98f); break;
                        case BrowShape.Sad: g.DrawLine(p, outer, -98f, inner, -105f); break;
                        case BrowShape.Raised: g.DrawArc(p, Math.Min(outer, inner), -110f, 18f, 8f, 200f, 140f); break;
                    }
                }
            }
        }

        private static void DrawMouth(Graphics g, MascotFrame f)
        {
            float open = f.MouthOpen;
            using (var ink = new Pen(A(Ink), 2.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var dark = new SolidBrush(A(95, 25, 40)))
            {
                switch (f.Mouth)
                {
                    case MouthShape.Laugh:
                    {
                        float h = 10f + 7f * open;
                        using (var m = new GraphicsPath())
                        {
                            m.AddLine(-14f, -64f, 14f, -64f);
                            m.AddArc(-14f, -64f - h, 28f, h * 2f, 0f, 180f);
                            m.CloseFigure();
                            g.FillPath(dark, m);
                            var clip = g.Clip;
                            g.SetClip(m, CombineMode.Intersect);
                            using (var tongue = new SolidBrush(A(255, 110, 130)))
                                g.FillEllipse(tongue, -8f, -64f + h - 7f, 16f, 10f);
                            g.Clip = clip;
                            g.DrawPath(ink, m);
                        }
                        break;
                    }
                    case MouthShape.O:
                    {
                        float w = 10f + 4f * open, h = 12f + 6f * open;
                        g.FillEllipse(dark, -w / 2f, -60f - h / 2f, w, h);
                        g.DrawEllipse(ink, -w / 2f, -60f - h / 2f, w, h);
                        break;
                    }
                    default:
                        if (open > 0.08f)
                        {
                            float w = f.Mouth == MouthShape.Smile ? 18f : 14f, h = 4f + 11f * open;
                            g.FillEllipse(dark, -w / 2f, -60f - h / 2f, w, h);
                            g.DrawEllipse(ink, -w / 2f, -60f - h / 2f, w, h);
                        }
                        else if (f.Mouth == MouthShape.SmallSmile) g.DrawArc(ink, -7f, -66f, 14f, 9f, 20f, 140f);
                        else if (f.Mouth == MouthShape.Smile) g.DrawArc(ink, -11f, -69f, 22f, 13f, 15f, 150f);
                        else if (f.Mouth == MouthShape.Frown) g.DrawArc(ink, -11f, -60f, 22f, 13f, 200f, 140f);
                        else if (f.Mouth == MouthShape.Flat) g.DrawLine(ink, -8f, -60f, 8f, -60f);
                        else if (f.Mouth == MouthShape.Wavy)
                        {
                            var pts = new PointF[17];
                            for (int i = 0; i < pts.Length; i++)
                                pts[i] = new PointF(-12f + i * 1.5f, -60f + (float)Math.Sin(i * 0.8) * 2.2f);
                            g.DrawCurve(ink, pts, 0.5f);
                        }
                        break;
                }
            }
        }

        // ---- effects ------------------------------------------------------------------
        private static void DrawEffects(Graphics g, MascotFrame f)
        {
            float t = f.T;
            if (f.Tears)
                using (var b = new SolidBrush(A(200, 90, 180, 255)))
                    for (int side = -1; side <= 1; side += 2)
                        for (int k = 0; k < 2; k++)
                        {
                            float p = (float)((t * 1.4 + k * 0.5) % 1.0);
                            float x = side * (21f + p * 6f), y = -76f + p * 52f;
                            using (var drop = Drop(x, y, 4.5f + p * 1.5f))
                            using (var fade = new SolidBrush(A((int)(200 * (1f - p * 0.7f)), 90, 180, 255)))
                                g.FillPath(fade, drop);
                        }
            if (f.Sweat)
            {
                float y = -104f + (float)Math.Sin(t * 2.5) * 1.5f;
                using (var drop = Drop(33f, y, 6f))
                using (var b = new SolidBrush(A(230, 150, 210, 255)))
                using (var p = new Pen(A(Ink), 1.6f))
                {
                    g.FillPath(b, drop);
                    g.DrawPath(p, drop);
                }
            }
            if (f.Steam)
                using (var p = new Pen(A(120, 43, 45, 66), 1.4f))
                    for (int side = -1; side <= 1; side += 2)
                        for (int i = 0; i < 3; i++)
                        {
                            float k = (float)((t * 0.9 + i / 3.0) % 1.0);
                            float r = 4f + k * 6f, x = side * (26f + k * 12f), y = -120f - k * 30f;
                            using (var b = new SolidBrush(A((int)(210 * (1f - k)), 245, 245, 250)))
                                g.FillEllipse(b, x - r, y - r, r * 2f, r * 2f);
                            g.DrawEllipse(p, x - r, y - r, r * 2f, r * 2f);
                        }
            if (f.Sparkles)
                for (int i = 0; i < 5; i++)
                {
                    double ang = t * 1.6 + i * Math.PI * 2 / 5;
                    float r = 60f + (float)Math.Sin(t * 3 + i) * 5f;
                    float x = (float)Math.Cos(ang) * r, y = -66f + (float)Math.Sin(ang) * r * 0.75f;
                    float s = 5f + 2.5f * (float)Math.Sin(t * 7 + i * 1.3);
                    using (var star = Star(x, y, s))
                    using (var b = new SolidBrush(A(230, 255, 204, 0)))
                    using (var p = new Pen(A(150, 160, 110, 0), 1f))
                    {
                        g.FillPath(b, star);
                        g.DrawPath(p, star);
                    }
                }
            if (f.Bolt)
            {
                float y = -146f + (float)Math.Sin(t * 2.2) * 2.5f;
                int a = (int)(170 + 85 * Math.Sin(t * 3.0));
                using (var bolt = BoltPath(0f, y, 0.62f))
                using (var b = new SolidBrush(A(a, 255, 204, 0)))
                using (var p = new Pen(A(a, 43, 45, 66), 2f))
                {
                    g.FillPath(b, bolt);
                    g.DrawPath(p, bolt);
                }
            }
            if (f.Exclaim)
                using (var b = new SolidBrush(A(255, 80, 60)))
                using (var p = new Pen(A(Ink), 2f))
                using (var stem = RoundRect(new RectangleF(-4f, -170f, 8f, 21f), 4f))
                {
                    g.FillPath(b, stem); g.DrawPath(p, stem);
                    g.FillEllipse(b, -4f, -145f, 8f, 8f); g.DrawEllipse(p, -4f, -145f, 8f, 8f);
                }
            if (f.Muted)
            {
                float cx = 34f, cy = -130f;
                using (var b = new SolidBrush(A(Color.White)))
                using (var p = new Pen(A(Ink), 2f))
                using (var red = new Pen(A(230, 50, 40), 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                using (var ink = new SolidBrush(A(Ink)))
                {
                    g.FillEllipse(b, cx - 11f, cy - 11f, 22f, 22f);
                    g.DrawEllipse(p, cx - 11f, cy - 11f, 22f, 22f);
                    g.FillPolygon(ink, new[] { new PointF(cx - 6f, cy - 3f), new PointF(cx - 3f, cy - 3f), new PointF(cx + 2f, cy - 7f), new PointF(cx + 2f, cy + 7f), new PointF(cx - 3f, cy + 3f), new PointF(cx - 6f, cy + 3f) });
                    g.DrawLine(red, cx - 7f, cy + 7f, cx + 7f, cy - 7f);
                }
            }
        }

        // ---- speech bubble --------------------------------------------------------------
        private static void DrawBubble(Graphics g, string text)
        {
            using (var font = new Font("Segoe UI Semibold", 13f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                var size = g.MeasureString(text, font, 172, fmt);
                float w = Math.Max(60f, size.Width + 24f), h = size.Height + 16f;
                float right = AX - 20f, bottom = AY - 128f;
                var rect = new RectangleF(right - w, Math.Max(4f, bottom - h), w, Math.Min(h, bottom - 4f));
                using (var path = BubblePath(rect, new PointF(AX - 32f, AY - 106f)))
                {
                    using (var shadow = new SolidBrush(A(45, 0, 0, 0)))
                    {
                        g.TranslateTransform(2f, 3f);
                        g.FillPath(shadow, path);
                        g.TranslateTransform(-2f, -3f);
                    }
                    using (var b = new SolidBrush(A(Color.White)))
                    using (var p = new Pen(A(Ink), 2.4f) { LineJoin = LineJoin.Round })
                    {
                        g.FillPath(b, path);
                        g.DrawPath(p, path);
                    }
                }
                using (var tb = new SolidBrush(A(30, 30, 46)))
                    g.DrawString(text, font, tb, rect, fmt);
            }
        }

        private static GraphicsPath BubblePath(RectangleF r, PointF tip)
        {
            float rad = 12f, d = rad * 2f;
            float tailRight = Math.Min(r.Right - rad - 2f, tip.X + 4f), tailLeft = tailRight - 16f;
            var p = new GraphicsPath();
            p.AddArc(r.Left, r.Top, d, d, 180f, 90f);
            p.AddArc(r.Right - d, r.Top, d, d, 270f, 90f);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            p.AddLine(tailRight, r.Bottom, tip.X, tip.Y);
            p.AddLine(tip.X, tip.Y, tailLeft, r.Bottom);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90f, 90f);
            p.CloseFigure();
            return p;
        }

        // ---- shapes -------------------------------------------------------------------
        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            float d = radius * 2f;
            var p = new GraphicsPath();
            p.AddArc(r.Left, r.Top, d, d, 180f, 90f);
            p.AddArc(r.Right - d, r.Top, d, d, 270f, 90f);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90f, 90f);
            p.CloseFigure();
            return p;
        }

        private static GraphicsPath Drop(float x, float y, float r)
        {
            var p = new GraphicsPath();
            p.AddArc(x - r, y - r, r * 2f, r * 2f, -30f, 240f);
            p.AddLine(x - r * 0.87f, y - r * 0.5f, x, y - r * 2.3f);
            p.CloseFigure();
            return p;
        }

        private static GraphicsPath Star(float x, float y, float s)
        {
            var pts = new PointF[8];
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                float r = i % 2 == 0 ? s : s * 0.35f;
                pts[i] = new PointF(x + (float)Math.Sin(a) * r, y - (float)Math.Cos(a) * r);
            }
            var p = new GraphicsPath();
            p.AddPolygon(pts);
            return p;
        }

        public static GraphicsPath BoltPath(float cx, float cy, float k)
        {
            var src = new[] { new PointF(19, 1), new PointF(5, 18), new PointF(14, 18), new PointF(10, 31), new PointF(27, 12), new PointF(18, 12), new PointF(23, 1) };
            var pts = new PointF[src.Length];
            for (int i = 0; i < src.Length; i++) pts[i] = new PointF(cx + (src[i].X - 16f) * k, cy + (src[i].Y - 16f) * k);
            var p = new GraphicsPath();
            p.AddPolygon(pts);
            return p;
        }

        private static Color Lighten(Color c, float k)
        {
            return Color.FromArgb(c.A, (int)(c.R + (255 - c.R) * k), (int)(c.G + (255 - c.G) * k), (int)(c.B + (255 - c.B) * k));
        }
    }

    // How each mood looks and moves.
    internal static class MascotPoses
    {
        public static MascotFrame Build(Mood mood, float e, float t, bool talking, float level, bool charging)
        {
            var f = new MascotFrame { T = t, Level = level, Charging = charging };
            f.Bob = (float)Math.Sin(t * 2.2) * 1.2f;
            f.ArmL = f.ArmR = 16f + (float)Math.Sin(t * 1.7) * 4f;
            float talk = talking ? 0.3f + 0.7f * Math.Abs((float)Math.Sin(t * 13.0 + Math.Sin(t * 4.7) * 1.6)) : 0f;
            f.MouthOpen = talk;

            switch (mood)
            {
                case Mood.Neutral:
                    f.Mouth = MouthShape.SmallSmile;
                    break;
                case Mood.Happy:
                    f.Mouth = MouthShape.Smile; f.Blush = true; f.Bolt = charging;
                    break;
                case Mood.Laughing:
                case Mood.Relieved:
                {
                    float speed = mood == Mood.Laughing ? 9f : 6.5f;
                    f.Eyes = EyeShape.Happy; f.Mouth = MouthShape.Laugh; f.Blush = true; f.Sparkles = true;
                    f.MouthOpen = talking ? talk : 0.5f + 0.5f * Math.Abs((float)Math.Sin(t * speed));
                    f.Bob = -Math.Abs((float)Math.Sin(t * speed)) * 8f;
                    f.SquashY = 1f - 0.05f * (1f - Math.Abs((float)Math.Sin(t * speed)));
                    f.SquashX = 2f - f.SquashY;
                    f.ArmL = 150f + (float)Math.Sin(t * 12) * 22f;
                    f.ArmR = 150f + (float)Math.Sin(t * 12 + Math.PI) * 22f;
                    break;
                }
                case Mood.Giggle:
                {
                    float k = (float)Math.Exp(-e * 5.0);
                    f.Eyes = EyeShape.Happy; f.Mouth = MouthShape.Laugh; f.Blush = true; f.Sparkles = e < 1.2f;
                    f.MouthOpen = talking ? talk : 0.3f;
                    f.SquashY = 1f - 0.22f * k * (float)Math.Cos(e * 22.0);
                    f.SquashX = 1f + 0.18f * k * (float)Math.Cos(e * 22.0);
                    f.ArmL = f.ArmR = 60f + (float)Math.Sin(t * 10) * 15f;
                    break;
                }
                case Mood.Crying:
                    f.Eyes = EyeShape.Sad; f.Brows = BrowShape.Sad; f.Mouth = MouthShape.Frown; f.Tears = true;
                    f.PupilY = 0.7f; f.ArmL = f.ArmR = 6f;
                    f.ShakeX = talking ? (float)Math.Sin(t * 32) * 1.1f : 0f;
                    break;
                case Mood.Sad:
                    f.Eyes = EyeShape.Sad; f.Brows = BrowShape.Sad; f.Mouth = MouthShape.Frown;
                    f.PupilY = 0.6f; f.ArmL = f.ArmR = 5f;
                    break;
                case Mood.Weak:
                    f.Eyes = EyeShape.HalfLid; f.Brows = BrowShape.Sad; f.Mouth = MouthShape.Wavy; f.Sweat = true;
                    f.PupilY = 0.5f; f.ArmL = f.ArmR = 3f; f.SquashY = 0.97f;
                    f.Bob = 1f + (float)Math.Sin(t * 1.2) * 2f;
                    break;
                case Mood.Dying:
                    f.Eyes = EyeShape.Droopy; f.Brows = BrowShape.Sad; f.Mouth = MouthShape.O; f.Sweat = true; f.Pale = true;
                    f.PupilY = 0.8f; f.ArmL = f.ArmR = 0f; f.SquashY = 0.94f;
                    f.Tilt = (float)Math.Sin(t * 1.3) * 5f;
                    f.MouthOpen = talking ? talk * 0.6f : 0f;
                    break;
                case Mood.Angry:
                    f.Eyes = EyeShape.Angry; f.Brows = BrowShape.Angry; f.Mouth = MouthShape.Frown; f.Steam = true; f.RedTint = true;
                    f.ShakeX = (e < 2.5f || talking) ? (float)Math.Sin(t * 45) * 1.6f : 0f;
                    f.ArmL = 135f + (float)Math.Sin(t * 20) * 12f;
                    f.ArmR = 135f + (float)Math.Sin(t * 20 + 1.5) * 12f;
                    break;
                case Mood.Grumpy:
                    f.Eyes = EyeShape.Angry; f.Brows = BrowShape.Angry; f.Mouth = MouthShape.Flat;
                    f.ArmL = f.ArmR = 28f;
                    break;
                case Mood.Surprised:
                    f.Eyes = EyeShape.Wide; f.Brows = BrowShape.Raised; f.Mouth = MouthShape.O; f.Exclaim = e < 2.2f;
                    f.Bob = -14f * (float)Math.Exp(-e * 4.0) * Math.Abs((float)Math.Cos(e * 9.0));
                    f.ArmL = f.ArmR = e < 1.5f ? 145f : 40f;
                    break;
            }
            if (talking && f.Mouth != MouthShape.Laugh && mood != Mood.Dying) f.MouthOpen = talk;
            return f;
        }

        public static float EaseOutBack(float p)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float q = p - 1f;
            return 1f + c3 * q * q * q + c1 * q * q;
        }
    }

    // A borderless, always-on-top, per-pixel transparent window that never steals focus.
    internal sealed class LayeredWindow : Form
    {
        public LayeredWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "Charger Drama";
            Cursor = Cursors.Hand;
            TopMost = true;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80000 | 0x80 | 0x8 | 0x08000000; // LAYERED | TOOLWINDOW | TOPMOST | NOACTIVATE
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x21) { m.Result = (IntPtr)3; return; } // WM_MOUSEACTIVATE -> MA_NOACTIVATE
            base.WndProc(ref m);
        }

        public void SetBitmap(Bitmap bmp, Point pos)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero, old = IntPtr.Zero;
            try
            {
                hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
                old = SelectObject(memDc, hBitmap);
                var size = new SIZE { cx = bmp.Width, cy = bmp.Height };
                var src = new POINT();
                var dst = new POINT { x = pos.X, y = pos.Y };
                var blend = new BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
                UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, 2);
            }
            finally
            {
                if (old != IntPtr.Zero) SelectObject(memDc, old);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        public void MoveTo(Point pos)
        {
            SetWindowPos(Handle, new IntPtr(-1), pos.X, pos.Y, 0, 0, 0x0001 | 0x0010); // TOPMOST, NOSIZE | NOACTIVATE
        }

        public void KeepOnTop()
        {
            SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); // NOSIZE | NOMOVE | NOACTIVATE
        }

        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    }

    // The mascot on screen: moods, speech bubble, power on/off animations, eyes that follow the mouse,
    // dragging, and poking.
    internal sealed class Mascot : IDisposable
    {
        private readonly Settings settings;
        private readonly Func<bool> isTalking;
        private readonly Action onPoke;
        private readonly ContextMenuStrip menu;
        private readonly LayeredWindow win = new LayeredWindow();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Random rng = new Random();
        private readonly float dpi;
        private readonly Bitmap canvas;
        private Point pos;

        private Mood baseMood = Mood.Neutral;
        private float level = 0.8f;
        private bool charging, muted, suppressed;

        private bool reacting;
        private Mood reactMood;
        private float reactStart, reactEnd;
        private bool reactMuted;

        private string bubble;
        private float bubbleEnd, bubbleAlpha;

        private int power; // 0 none, 1 powering on, -1 powering off
        private float powerStart;
        private float showAlpha;  // for "only when something happens" mode

        private float nextBlink = 2f, blinkStart = -10f;
        private float pupilX, pupilY;
        private int tickCount;
        private float lastTopMost;

        private bool mouseDown, dragging;
        private Point downCursor, downPos;

        public Mascot(Settings settings, Func<bool> isTalking, Action onPoke, ContextMenuStrip menu)
        {
            this.settings = settings;
            this.isTalking = isTalking;
            this.onPoke = onPoke;
            this.menu = menu;
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) dpi = g.DpiX / 96f;
            canvas = new Bitmap((int)Math.Ceiling(MascotRenderer.Width * dpi), (int)Math.Ceiling(MascotRenderer.Height * dpi), PixelFormat.Format32bppArgb);
            pos = settings.HasMascotPosition ? new Point(settings.MascotX, settings.MascotY) : DefaultPosition();
            if (!IsOnScreen(pos)) pos = DefaultPosition();

            win.MouseDown += OnMouseDown;
            win.MouseMove += OnMouseMove;
            win.MouseUp += OnMouseUp;
            win.Show();
            win.Hide();
        }

        private float Now { get { return (float)clock.Elapsed.TotalSeconds; } }

        public void SetContext(float level, bool charging, Mood baseMood, bool muted)
        {
            this.level = level;
            this.charging = charging;
            this.baseMood = baseMood;
            this.muted = muted;
        }

        public void React(Mood mood, string caption, bool silent)
        {
            float now = Now;
            reacting = true;
            reactMood = mood;
            reactStart = now;
            reactMuted = silent;
            reactEnd = now + (silent ? 2.5f + Math.Min(5f, (caption ?? "").Length * 0.06f) : 2.5f);
            bubble = silent ? caption + "\n(muted - you're in a call)" : caption;
            bubbleEnd = reactEnd + 1.2f;
        }

        public void Say(string text, float seconds)
        {
            bubble = text;
            bubbleEnd = Now + seconds;
        }

        public void PowerOn() { power = 1; powerStart = Now; }
        public void PowerOff() { power = -1; powerStart = Now; }

        public void SetSuppressed(bool value) { suppressed = value; }

        public void ResetPosition()
        {
            pos = DefaultPosition();
            settings.MascotX = settings.MascotY = int.MinValue;
            settings.Save();
            win.MoveTo(pos);
        }

        public void Tick()
        {
            try { TickCore(); }
            catch (Exception ex) { AppPaths.LogError("Mascot.Tick", ex); }
        }

        private void TickCore()
        {
            float now = Now;
            tickCount++;
            bool talking = isTalking();

            if (reacting)
            {
                if (talking && !reactMuted) { reactEnd = Math.Max(reactEnd, now + 1.0f); bubbleEnd = Math.Max(bubbleEnd, reactEnd + 1.2f); }
                if (now > reactEnd) reacting = false;
            }
            float bubbleTarget = (bubble != null && now < bubbleEnd) ? 1f : 0f;
            bubbleAlpha += Math.Sign(bubbleTarget - bubbleAlpha) * Math.Min(Math.Abs(bubbleTarget - bubbleAlpha), 0.16f);
            if (bubbleAlpha <= 0f && bubbleTarget == 0f) bubble = null;

            bool poweringOn = power == 1 && now - powerStart < 1.4f;
            bool poweringOff = power == -1;
            if (power == 1 && !poweringOn) power = 0;

            // visibility
            bool wanted;
            if (settings.MascotMode == MascotModes.Hidden || suppressed) wanted = false;
            else if (settings.MascotMode == MascotModes.Always) wanted = true;
            else wanted = reacting || bubble != null || poweringOn;
            if (poweringOff && settings.MascotMode != MascotModes.Hidden && !suppressed && showAlpha > 0f) wanted = true;
            showAlpha += Math.Sign((wanted ? 1f : 0f) - showAlpha) * Math.Min(Math.Abs((wanted ? 1f : 0f) - showAlpha), 0.08f);
            if (showAlpha <= 0f)
            {
                if (win.Visible) win.Hide();
                return;
            }
            if (!win.Visible) { win.Show(); win.KeepOnTop(); }
            if (now - lastTopMost > 3f) { win.KeepOnTop(); lastTopMost = now; }

            // blinking and eyes following the mouse cursor
            if (now >= nextBlink) { blinkStart = now; nextBlink = now + 2.2f + (float)rng.NextDouble() * 3.5f; }
            float be = now - blinkStart;
            float blink = be < 0.14f ? 1f - Math.Abs(be / 0.07f - 1f) : 0f;
            var cur = Cursor.Position;
            float ex = pos.X + MascotRenderer.AX * dpi, ey = pos.Y + (MascotRenderer.AY - 84f) * dpi;
            float dx = cur.X - ex, dy = cur.Y - ey, dist = (float)Math.Sqrt(dx * dx + dy * dy);
            float reach = Math.Min(1f, dist / (140f * dpi));
            float tx = dist > 1f ? dx / dist * reach : 0f, ty = dist > 1f ? dy / dist * reach : 0f;
            pupilX += (tx - pupilX) * 0.35f;
            pupilY += (ty - pupilY) * 0.35f;

            bool active = reacting || bubbleAlpha > 0f || power != 0 || dragging || talking || charging || blink > 0f || showAlpha < 1f
                          || (baseMood != Mood.Neutral && baseMood != Mood.Happy);
            if (!active && tickCount % 3 != 0) return;  // idle on battery: ~8 frames per second

            Mood mood = reacting ? reactMood : baseMood;
            float moodE = reacting ? now - reactStart : now;
            var f = MascotPoses.Build(mood, moodE, now, talking && !reactMuted, level, charging);
            f.Blink = blink;
            f.PupilX = Clamp(f.PupilX * 0.6f + pupilX, -1f, 1f);
            f.PupilY = Clamp(f.PupilY * 0.6f + pupilY, -1f, 1f);
            f.Muted = muted;
            f.Bubble = bubble;
            f.BubbleAlpha = bubbleAlpha;
            f.Alpha = showAlpha;

            if (poweringOn)
            {
                float pe = now - powerStart, p = Clamp(pe / 0.55f, 0f, 1f);
                f.Scale = Math.Max(0.01f, MascotPoses.EaseOutBack(p));
                if (pe < 0.45f) f.Eyes = EyeShape.Closed;
                f.Sparkles = true;
            }
            else if (poweringOff)
            {
                float pe = now - powerStart, p = Clamp(pe / 0.9f, 0f, 1f);
                f.Eyes = EyeShape.Closed; f.Mouth = MouthShape.Flat; f.MouthOpen = 0f;
                f.ArmL = f.ArmR = 0f; f.Sparkles = false; f.Exclaim = false;
                f.Scale = Math.Max(0.01f, 1f - p * p * 0.9f);
                f.Alpha *= 1f - p * p;
                f.Bob += p * 8f;
                f.BubbleAlpha = 0f;
            }

            using (var g = Graphics.FromImage(canvas))
                MascotRenderer.Draw(g, f, dpi);
            win.SetBitmap(canvas, pos);
        }

        // ---- mouse: drag to move, click to poke, right-click for the menu ------------------
        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            mouseDown = true;
            dragging = false;
            downCursor = Cursor.Position;
            downPos = pos;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!mouseDown) return;
            var c = Cursor.Position;
            int dx = c.X - downCursor.X, dy = c.Y - downCursor.Y;
            if (!dragging && dx * dx + dy * dy > 25) dragging = true;
            if (dragging)
            {
                pos = new Point(downPos.X + dx, downPos.Y + dy);
                win.MoveTo(pos);
            }
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) { menu.Show(Cursor.Position); return; }
            if (!mouseDown) return;
            mouseDown = false;
            if (dragging)
            {
                dragging = false;
                if (!IsOnScreen(pos)) { pos = DefaultPosition(); win.MoveTo(pos); }
                settings.MascotX = pos.X;
                settings.MascotY = pos.Y;
                settings.Save();
            }
            else if (onPoke != null) onPoke();
        }

        // ---- placement --------------------------------------------------------------------
        private Point DefaultPosition()
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            return new Point(wa.Right - canvas.Width + (int)(16 * dpi), wa.Bottom - canvas.Height + (int)(4 * dpi));
        }

        // the mascot itself (not the empty bubble area) must be visible on some screen
        private bool IsOnScreen(Point p)
        {
            var body = new Rectangle(p.X + (int)((MascotRenderer.AX - 30) * dpi), p.Y + (int)((MascotRenderer.AY - 110) * dpi), (int)(60 * dpi), (int)(100 * dpi));
            foreach (var s in Screen.AllScreens)
                if (s.WorkingArea.IntersectsWith(body)) return true;
            return false;
        }

        private static float Clamp(float v, float lo, float hi) { return v < lo ? lo : v > hi ? hi : v; }

        // Draws one still frame (for the self-test and the app icon).
        public static Bitmap RenderStill(Mood mood, float level, bool charging, string bubble, float t, float scale)
        {
            var bmp = new Bitmap((int)(MascotRenderer.Width * scale), (int)(MascotRenderer.Height * scale), PixelFormat.Format32bppArgb);
            var f = MascotPoses.Build(mood, 0.4f, t, false, level, charging);
            f.Bubble = bubble;
            f.BubbleAlpha = bubble == null ? 0f : 1f;
            using (var g = Graphics.FromImage(bmp)) MascotRenderer.Draw(g, f, scale);
            return bmp;
        }

        public void Dispose()
        {
            win.Close();
            win.Dispose();
            canvas.Dispose();
        }
    }
}
