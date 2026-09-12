using System.Drawing;
using System.Drawing.Drawing2D;

namespace PhotoStudioApp.UI
{
    /// <summary>Small vector icons drawn directly with GDI+ instead of emoji characters
    /// (🔒 ⟳ 🖨 etc). Windows 7 has no color-emoji font, so those characters render as empty
    /// boxes/missing glyphs there - these bitmaps look correct on every supported Windows
    /// version since they don't depend on any particular font being installed.</summary>
    public static class IconGlyphs
    {
        /// <summary>Padlock icon (used for "Clôturer"/locked-history buttons).</summary>
        public static Bitmap Lock(int size, Color? color = null)
        {
            var c = color ?? Color.White;
            var bmp = new Bitmap(size, size);
            using (var g = BeginDraw(bmp))
            using (var pen = new Pen(c, System.Math.Max(1.2f, size / 10f)))
            using (var brush = new SolidBrush(c))
            {
                float bodyTop = size * 0.45f;
                float bodyHeight = size * 0.45f;
                float bodyLeft = size * 0.18f;
                float bodyWidth = size * 0.64f;
                var body = new RectangleF(bodyLeft, bodyTop, bodyWidth, bodyHeight);

                // Shackle (the U-shaped arc on top).
                float shackleLeft = size * 0.30f;
                float shackleWidth = size * 0.40f;
                float shackleTop = size * 0.08f;
                float shackleHeight = size * 0.45f;
                g.DrawArc(pen, shackleLeft, shackleTop, shackleWidth, shackleHeight, 180, 180);
                g.DrawLine(pen, shackleLeft, shackleTop + shackleHeight / 2, shackleLeft, bodyTop + pen.Width / 2);
                g.DrawLine(pen, shackleLeft + shackleWidth, shackleTop + shackleHeight / 2, shackleLeft + shackleWidth, bodyTop + pen.Width / 2);

                // Body.
                using (var path = RoundedRect(body, size * 0.10f))
                    g.FillPath(brush, path);

                // Keyhole.
                using (var keyBrush = new SolidBrush(color == null ? Color.FromArgb(0, 112, 122) : InvertForKeyhole(c)))
                {
                    float khSize = size * 0.12f;
                    float khX = size * 0.5f - khSize / 2;
                    float khY = bodyTop + bodyHeight * 0.28f;
                    g.FillEllipse(keyBrush, khX, khY, khSize, khSize);
                    g.FillRectangle(keyBrush, size * 0.47f, khY + khSize * 0.6f, size * 0.06f, bodyHeight * 0.32f);
                }
            }
            return bmp;
        }

        /// <summary>Circular "refresh"/"reload" arrow icon (used for Actualiser buttons).</summary>
        public static Bitmap Refresh(int size, Color? color = null)
        {
            var c = color ?? Theme.Primary;
            var bmp = new Bitmap(size, size);
            using (var g = BeginDraw(bmp))
            using (var pen = new Pen(c, System.Math.Max(1.4f, size / 8f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var brush = new SolidBrush(c))
            {
                float margin = size * 0.16f;
                var rect = new RectangleF(margin, margin, size - 2 * margin, size - 2 * margin);
                g.DrawArc(pen, rect, -30, 270);

                // Arrowhead at the end of the arc (approx. position at angle 240deg from center).
                PointF center = new PointF(size / 2f, size / 2f);
                float radius = rect.Width / 2f;
                double angleRad = System.Math.PI * 240 / 180.0;
                PointF tip = new PointF(
                    center.X + radius * (float)System.Math.Cos(angleRad),
                    center.Y + radius * (float)System.Math.Sin(angleRad));

                float headLen = size * 0.22f;
                var p1 = new PointF(tip.X - headLen * 0.9f, tip.Y - headLen * 0.25f);
                var p2 = new PointF(tip.X - headLen * 0.1f, tip.Y - headLen * 0.95f);
                var p3 = new PointF(tip.X + headLen * 0.35f, tip.Y - headLen * 0.15f);
                g.FillPolygon(brush, new[] { p1, p2, p3 });
            }
            return bmp;
        }

        /// <summary>Printer icon (used for Imprimer buttons).</summary>
        public static Bitmap Printer(int size, Color? color = null)
        {
            var c = color ?? Color.White;
            var bmp = new Bitmap(size, size);
            using (var g = BeginDraw(bmp))
            using (var brush = new SolidBrush(c))
            using (var pen = new Pen(c, System.Math.Max(1f, size / 12f)))
            {
                // Top paper sheet sticking out.
                float paperW = size * 0.5f;
                float paperX = (size - paperW) / 2f;
                g.FillRectangle(brush, paperX, size * 0.06f, paperW, size * 0.28f);

                // Printer body.
                float bodyY = size * 0.30f;
                float bodyH = size * 0.36f;
                using (var path = RoundedRect(new RectangleF(size * 0.08f, bodyY, size * 0.84f, bodyH), size * 0.08f))
                    g.FillPath(brush, path);

                // Output tray slot (cut a gap using background-colored rectangle isn't reliable on
                // transparent bitmaps, so just draw the tray as a separate lighter shape below).
                float trayY = bodyY + bodyH - size * 0.02f;
                float trayW = size * 0.62f;
                float trayX = (size - trayW) / 2f;
                g.FillRectangle(brush, trayX, trayY, trayW, size * 0.30f);
            }
            return bmp;
        }

        private static Graphics BeginDraw(Bitmap bmp)
        {
            var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            return g;
        }

        private static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Color InvertForKeyhole(Color c) => Color.FromArgb(255, 255 - c.R, 255 - c.G, 255 - c.B);
    }
}
