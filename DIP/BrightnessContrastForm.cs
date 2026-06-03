using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace DIP
{
    public class BrightnessContrastForm : Form
    {
        private DIPSample mainForm;
        private Bitmap originalBmp;
        private Bitmap processedBmp;

        internal ToolStripStatusLabel pf1;

        private PictureBox pictureBox1;
        private Panel panelBottom;
        private TrackBar trackBarAlpha;
        private TrackBar trackBarBeta;
        private Label lblAlphaTitle;
        private Label lblAlphaValue;
        private Label lblBetaTitle;
        private Label lblBetaValue;
        private Label lblFormula;
        private CheckBox chkKeepGammaOne;
        private bool isUpdatingControls = false;

        public Bitmap ProcessedBitmap
        {
            get { return (this.IsDisposed || this.Disposing) ? null : processedBmp; }
        }

        public BrightnessContrastForm(DIPSample mainForm, Bitmap originalBmp)
        {
            this.mainForm = mainForm;
            this.originalBmp = originalBmp;

            InitializeUI();
            UpdateImage();
        }

        private void InitializeUI()
        {
            this.Text = "亮度與對比調整預覽 (Brightness & Contrast)";

            int initialWidth = Math.Max(originalBmp.Width, 480);
            int initialHeight = originalBmp.Height + 160;

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
                Height = 160,
                Dock = DockStyle.Bottom,
                BackColor = SystemColors.Control,
                Padding = new Padding(10)
            };

            // 3. Alpha (Contrast) Controls
            lblAlphaTitle = new Label
            {
                Text = "對比度 (Alpha):",
                Location = new Point(15, 15),
                Size = new Size(100, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            trackBarAlpha = new TrackBar
            {
                Minimum = 1,
                Maximum = 30,
                Value = 10, // Default 1.0
                Location = new Point(120, 10),
                Width = 260,
                TickStyle = TickStyle.None
            };
            trackBarAlpha.ValueChanged += (s, e) =>
            {
                if (isUpdatingControls) return;
                isUpdatingControls = true;

                double alpha = trackBarAlpha.Value / 10.0;
                lblAlphaValue.Text = string.Format("{0:F1}", alpha);

                if (chkKeepGammaOne.Checked)
                {
                    // For equivalent gamma = 1.0 at midpoint:
                    // 0.5 * alpha + beta / 255.0 = 0.5 => beta = 127.5 * (1.0 - alpha)
                    int betaVal = (int)Math.Round(127.5 * (1.0 - alpha));
                    if (betaVal < -255) betaVal = -255;
                    if (betaVal > 255) betaVal = 255;
                    trackBarBeta.Value = betaVal;
                    lblBetaValue.Text = betaVal.ToString();
                }

                isUpdatingControls = false;
                UpdateImage();
            };

            lblAlphaValue = new Label
            {
                Text = "1.0",
                Location = new Point(390, 15),
                Size = new Size(50, 20),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.RoyalBlue,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 4. Beta (Brightness) Controls
            lblBetaTitle = new Label
            {
                Text = "亮度 (Beta):",
                Location = new Point(15, 55),
                Size = new Size(100, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            trackBarBeta = new TrackBar
            {
                Minimum = -255,
                Maximum = 255,
                Value = 0, // Default 0
                Location = new Point(120, 50),
                Width = 260,
                TickStyle = TickStyle.None
            };
            trackBarBeta.ValueChanged += (s, e) =>
            {
                if (isUpdatingControls) return;
                isUpdatingControls = true;

                int betaVal = trackBarBeta.Value;
                lblBetaValue.Text = betaVal.ToString();

                if (chkKeepGammaOne.Checked)
                {
                    // alpha = 1.0 - beta / 127.5
                    double alphaVal = 1.0 - ((double)betaVal / 127.5);
                    int alphaTick = (int)Math.Round(alphaVal * 10.0);
                    if (alphaTick < 1) alphaTick = 1;
                    if (alphaTick > 30) alphaTick = 30;
                    trackBarAlpha.Value = alphaTick;
                    lblAlphaValue.Text = string.Format("{0:F1}", alphaTick / 10.0);
                }

                isUpdatingControls = false;
                UpdateImage();
            };

            lblBetaValue = new Label
            {
                Text = "0",
                Location = new Point(390, 55),
                Size = new Size(50, 20),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.RoyalBlue,
                TextAlign = ContentAlignment.MiddleLeft
            };

            chkKeepGammaOne = new CheckBox
            {
                Text = "保持等效 Gamma 為 1.0 (Keep Gamma = 1.0)",
                Location = new Point(15, 90),
                Size = new Size(350, 24),
                Checked = false,
                ForeColor = Color.Black
            };
            chkKeepGammaOne.CheckedChanged += (s, e) =>
            {
                if (chkKeepGammaOne.Checked)
                {
                    if (isUpdatingControls) return;
                    isUpdatingControls = true;

                    double alpha = trackBarAlpha.Value / 10.0;
                    int betaVal = (int)Math.Round(127.5 * (1.0 - alpha));
                    if (betaVal < -255) betaVal = -255;
                    if (betaVal > 255) betaVal = 255;
                    trackBarBeta.Value = betaVal;
                    lblBetaValue.Text = betaVal.ToString();

                    isUpdatingControls = false;
                    UpdateImage();
                }
            };

            lblFormula = new Label
            {
                Text = "運算式: y = 1.0 * x + 0 (等效 gamma = 1.0)",
                Location = new Point(15, 125),
                Size = new Size(400, 25),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Italic),
                ForeColor = Color.DarkSlateGray
            };

            panelBottom.Controls.AddRange(new Control[] {
                lblAlphaTitle, trackBarAlpha, lblAlphaValue,
                lblBetaTitle, trackBarBeta, lblBetaValue,
                chkKeepGammaOne, lblFormula
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

            double alpha = trackBarAlpha.Value / 10.0;
            int beta = trackBarBeta.Value;

            unsafe
            {
                fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                {
                    DIPSample.adjust_brightness_contrast(f0, w, h, d, g0, alpha, beta);
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
                pf1.Text = string.Format("亮度與對比: alpha={0:F1}, beta={1}", alpha, beta);
            }

            if (lblFormula != null)
            {
                double v = 0.5 * alpha + (double)beta / 255.0;
                string gammaStr;
                if (v > 0.0)
                {
                    double eqGamma = -Math.Log(v) / Math.Log(2.0);
                    if (eqGamma < 0.05) eqGamma = 0.05;
                    gammaStr = string.Format("{0:F2}", eqGamma);
                }
                else
                {
                    gammaStr = "N/A (太暗)";
                }
                lblFormula.Text = string.Format("運算式: y = {0:F1} * x + ({1})  (等效 gamma = {2})", alpha, beta, gammaStr);
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
