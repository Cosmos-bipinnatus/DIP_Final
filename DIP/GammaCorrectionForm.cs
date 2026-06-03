using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace DIP
{
    public class GammaCorrectionForm : Form
    {
        private DIPSample mainForm;
        private Bitmap originalBmp;
        private Bitmap processedBmp;

        internal ToolStripStatusLabel pf1;

        private PictureBox pictureBox1;
        private Panel panelBottom;
        private TrackBar trackBarGamma;
        private Label lblGammaTitle;
        private Label lblGammaValue;
        private Label lblFormula;
        private PictureBox picGammaCurve;

        public Bitmap ProcessedBitmap
        {
            get { return (this.IsDisposed || this.Disposing) ? null : processedBmp; }
        }

        public GammaCorrectionForm(DIPSample mainForm, Bitmap originalBmp)
        {
            this.mainForm = mainForm;
            this.originalBmp = originalBmp;

            InitializeUI();
            UpdateImage();
        }

        private void InitializeUI()
        {
            this.Text = "Gamma 校正預覽 (Gamma Correction)";

            int initialWidth = Math.Max(originalBmp.Width, 510);
            int initialHeight = originalBmp.Height + 110;

            this.ClientSize = new Size(initialWidth, initialHeight);
            this.FormBorderStyle = FormBorderStyle.Fixed3D;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9F);

            // 1. PictureBox
            pictureBox1 = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.Black
            };
            pictureBox1.MouseMove += PictureBox1_MouseMove;

            // 2. Bottom Panel
            panelBottom = new Panel
            {
                Height = 110,
                Dock = DockStyle.Bottom,
                BackColor = SystemColors.Control,
                Padding = new Padding(10)
            };

            // 3. Gamma Controls
            lblGammaTitle = new Label
            {
                Text = "Gamma 比例 (γ):",
                Location = new Point(15, 20),
                Size = new Size(110, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            trackBarGamma = new TrackBar
            {
                Minimum = 1,
                Maximum = 30,
                Value = 10, // Default 1.0
                Location = new Point(120, 15),
                Width = 220,
                TickStyle = TickStyle.None
            };
            trackBarGamma.ValueChanged += (s, e) =>
            {
                lblGammaValue.Text = string.Format("{0:F1}", trackBarGamma.Value / 10.0);
                UpdateImage();
            };

            lblGammaValue = new Label
            {
                Text = "1.0",
                Location = new Point(355, 20),
                Size = new Size(40, 20),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.RoyalBlue,
                TextAlign = ContentAlignment.MiddleLeft
            };

            picGammaCurve = new PictureBox
            {
                Location = new Point(415, 10),
                Size = new Size(70, 70),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            picGammaCurve.Paint += PicGammaCurve_Paint;

            lblFormula = new Label
            {
                Text = "運算式: y = 255 * (x / 255.0) ^ 1.0",
                Location = new Point(15, 65),
                Size = new Size(400, 20),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Italic),
                ForeColor = Color.DarkSlateGray
            };

            panelBottom.Controls.AddRange(new Control[] {
                lblGammaTitle, trackBarGamma, lblGammaValue,
                picGammaCurve, lblFormula
            });

            this.Controls.Add(pictureBox1);
            this.Controls.Add(panelBottom);

            this.DoubleBuffered = true;
        }

        private void UpdateImage()
        {
            if (originalBmp == null) return;

            int w = originalBmp.Width;
            int h = originalBmp.Height;
            int d = 0;
            PixelFormat pf = new PixelFormat();
            ColorPalette pal = null;

            int[] fArray = mainForm.dyn_bmp2array(originalBmp, ref d, ref pf, ref pal);
            int[] gArray = new int[w * h * d];

            double gamma = trackBarGamma.Value / 10.0;

            unsafe
            {
                fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                {
                    // Pass negative gamma to trigger C++ Gamma correction
                    DIPSample.adjust_brightness_contrast(f0, w, h, d, g0, -gamma, 0);
                }
            }

            if (processedBmp != null)
            {
                processedBmp.Dispose();
            }
            processedBmp = DIPSample.dyn_array2bmp(gArray, w, h, d, pf, pal);
            pictureBox1.Image = processedBmp;

            if (pf1 != null)
            {
                pf1.Text = string.Format("Gamma 校正: gamma={0:F1}", gamma);
            }

            if (lblFormula != null)
            {
                lblFormula.Text = string.Format("運算式: y = 255 * (x / 255.0) ^ {0:F1}", gamma);
            }

            if (picGammaCurve != null)
            {
                picGammaCurve.Invalidate();
            }

            mainForm.UpdateHistogram();
        }

        private void PictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (processedBmp == null || pf1 == null) return;

            int imgW = processedBmp.Width;
            int imgH = processedBmp.Height;
            int pbW = pictureBox1.Width;
            int pbH = pictureBox1.Height;

            int offsetX = (pbW - imgW) / 2;
            int offsetY = (pbH - imgH) / 2;

            int imgX = e.X - offsetX;
            int imgY = e.Y - offsetY;

            if (imgX >= 0 && imgX < imgW && imgY >= 0 && imgY < imgH)
            {
                try
                {
                    Color pixel = processedBmp.GetPixel(imgX, imgY);
                    pf1.Text = "(" + imgX + "," + imgY + ")=(" + pixel.R + "," + pixel.G + "," + pixel.B + ")";
                }
                catch
                {
                }
            }
            else
            {
                pf1.Text = "尺寸 (Width, Height)=(" + imgW + "," + imgH + ")";
            }
        }

        private void PicGammaCurve_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int w = picGammaCurve.Width;
            int h = picGammaCurve.Height;

            // Draw a light gray dashed diagonal line (gamma = 1.0 benchmark)
            using (Pen dashPen = new Pen(Color.FromArgb(220, 220, 220), 1))
            {
                dashPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawLine(dashPen, 0, h - 1, w - 1, 0);
            }

            // Get current gamma
            double gamma = trackBarGamma.Value / 10.0;

            // Generate curve points
            PointF[] points = new PointF[w];
            for (int x = 0; x < w; x++)
            {
                double normX = (double)x / (w - 1);
                double normY = Math.Pow(normX, gamma);
                float drawY = (float)((1.0 - normY) * (h - 1));
                points[x] = new PointF(x, drawY);
            }

            // Draw curve in blue
            using (Pen curvePen = new Pen(Color.RoyalBlue, 2))
            {
                g.DrawLines(curvePen, points);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (processedBmp != null)
            {
                pictureBox1.Image = null;
                processedBmp.Dispose();
                processedBmp = null;
            }
            base.OnFormClosed(e);
            mainForm.UpdateHistogram();
        }
    }
}
