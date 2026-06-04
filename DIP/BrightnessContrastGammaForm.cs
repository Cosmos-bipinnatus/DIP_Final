using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace DIP
{
    public class BrightnessContrastGammaForm : Form
    {
        private DIPSample mainForm;
        private Bitmap originalBmp;
        private Bitmap processedBmp;

        internal ToolStripStatusLabel pf1;

        private PictureBox pictureBox1;
        private Panel panelBottom;

        // Mode RadioButtons
        private RadioButton rdoLinear;
        private RadioButton rdoNonLinear;

        // Reset Button
        private Button btnReset;
        private Button btnOK;
        private Button btnCancel;

        // Linear controls (亮度與對比調整)
        private Label lblAlphaTitle;
        private Label lblAlphaValue;
        private Label lblBetaTitle;
        private Label lblBetaValue;
        private TrackBar trackBarAlpha;
        private TrackBar trackBarBeta;
        private PictureBox picLinearCurve;
        private Label lblFormulaLinear;

        // Non-Linear controls (Gamma 冪律轉換)
        private Label lblGammaTitle;
        private Label lblGammaValue;
        private TrackBar trackBarGamma;
        private PictureBox picGammaCurve;
        private Label lblFormulaNonLinear;

        private bool isUpdatingControls = false;

        // Linear curve drag state
        private bool isDraggingLinearCurve = false;
        private int lastLinearMouseX;
        private int lastLinearMouseY;

        private double GetAlphaValue()
        {
            int val = trackBarAlpha.Value;
            if (val <= 100)
            {
                return 0.1 + 0.9 * (val / 100.0);
            }
            else
            {
                return 1.0 + 2.0 * ((val - 100) / 100.0);
            }
        }

        private double GetGammaValue()
        {
            int val = trackBarGamma.Value;
            if (val <= 100)
            {
                // 0->0.1, 100->1.0  (linear interpolation)
                return 0.1 + 0.9 * (val / 100.0);
            }
            else
            {
                // 100->1.0, 200->10.0  (linear interpolation)
                return 1.0 + 9.0 * ((val - 100) / 100.0);
            }
        }

        public Bitmap ProcessedBitmap
        {
            get { return (this.IsDisposed || this.Disposing) ? null : processedBmp; }
        }

        public BrightnessContrastGammaForm(DIPSample mainForm, Bitmap originalBmp)
        {
            this.mainForm = mainForm;
            this.originalBmp = originalBmp;

            InitializeUI();
            UpdateImage();
        }

        private void InitializeUI()
        {
            this.Text = "亮度對比與 Gamma 調整預覽";

            int initialWidth = Math.Max(originalBmp.Width, 540);
            int initialHeight = originalBmp.Height + 200;

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
                BackColor = SystemColors.Control
            };
            pictureBox1.MouseMove += PictureBox1_MouseMove;

            // 2. Bottom Panel
            panelBottom = new Panel
            {
                Height = 200,
                Dock = DockStyle.Bottom,
                BackColor = SystemColors.Control,
                Padding = new Padding(10)
            };

            // 3. Mode RadioButtons
            rdoLinear = new RadioButton
            {
                Text = "亮度與對比調整 (線性)",
                Location = new Point(15, 10),
                Size = new Size(220, 24),
                Checked = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            rdoLinear.CheckedChanged += Mode_CheckedChanged;

            rdoNonLinear = new RadioButton
            {
                Text = "Gamma 冪律轉換 (非線性)",
                Location = new Point(240, 10),
                Size = new Size(220, 24),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };

            // 4. Reset Button (Commonly placed at bottom-left)
            btnReset = new Button
            {
                Text = "重置預設值",
                Location = new Point(15, 125),
                Size = new Size(110, 25),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnReset.Click += BtnReset_Click;

            // OK and Cancel Buttons (Placed below the curve picturebox)
            btnOK = new Button
            {
                Text = "確定",
                Location = new Point(415, 125),
                Size = new Size(70, 25),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(415, 160),
                Size = new Size(70, 25),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnCancel.Click += BtnCancel_Click;

            // 5. Linear Controls (亮度與對比調整)
            lblAlphaTitle = new Label
            {
                Text = "對比度 (Alpha):",
                Location = new Point(15, 45),
                Size = new Size(100, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            trackBarAlpha = new TrackBar
            {
                Minimum = 0,
                Maximum = 200,
                Value = 100, // Default 1.0 (Middle)
                Location = new Point(120, 40),
                Width = 220,
                TickStyle = TickStyle.None
            };
            trackBarAlpha.ValueChanged += TrackBarAlpha_ValueChanged;

            lblAlphaValue = new Label
            {
                Text = "1.0",
                Location = new Point(355, 45),
                Size = new Size(40, 20),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.RoyalBlue,
                TextAlign = ContentAlignment.MiddleLeft
            };

            lblBetaTitle = new Label
            {
                Text = "亮度 (Beta):",
                Location = new Point(15, 80),
                Size = new Size(100, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            trackBarBeta = new TrackBar
            {
                Minimum = -255,
                Maximum = 255,
                Value = 0, // Default 0
                Location = new Point(120, 75),
                Width = 220,
                TickStyle = TickStyle.None
            };
            trackBarBeta.ValueChanged += TrackBarBeta_ValueChanged;

            lblBetaValue = new Label
            {
                Text = "0",
                Location = new Point(355, 80),
                Size = new Size(40, 20),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.RoyalBlue,
                TextAlign = ContentAlignment.MiddleLeft
            };

            picLinearCurve = new PictureBox
            {
                Location = new Point(415, 40),
                Size = new Size(70, 70),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            picLinearCurve.Paint += PicLinearCurve_Paint;
            picLinearCurve.MouseDown += PicLinearCurve_MouseDown;
            picLinearCurve.MouseMove += PicLinearCurve_MouseMove;

            lblFormulaLinear = new Label
            {
                Text = "線性公式: y = 1.0 * x + (0)",
                Location = new Point(140, 125),
                Size = new Size(265, 70),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Italic),
                ForeColor = Color.DarkSlateGray
            };

            // 6. Non-Linear (Gamma) Controls
            lblGammaTitle = new Label
            {
                Text = "Gamma 比例 (γ):",
                Location = new Point(15, 55),
                Size = new Size(110, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };

            trackBarGamma = new TrackBar
            {
                Minimum = 0,
                Maximum = 200,
                Value = 100, // Default 1.0 (Center)
                Location = new Point(120, 50),
                Width = 220,
                TickStyle = TickStyle.None,
                Visible = false
            };
            trackBarGamma.ValueChanged += (s, e) =>
            {
                lblGammaValue.Text = string.Format("{0:F2}", GetGammaValue());
                UpdateImage();
            };

            lblGammaValue = new Label
            {
                Text = "1.0",
                Location = new Point(355, 55),
                Size = new Size(40, 20),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.RoyalBlue,
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };

            picGammaCurve = new PictureBox
            {
                Location = new Point(415, 40),
                Size = new Size(70, 70),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };
            picGammaCurve.Paint += PicGammaCurve_Paint;
            picGammaCurve.MouseDown += PicGammaCurve_MouseDown;
            picGammaCurve.MouseMove += PicGammaCurve_MouseMove;

            lblFormulaNonLinear = new Label
            {
                Text = "非線性公式: y = 255 * (x / 255.0) ^ 1.0",
                Location = new Point(140, 125),
                Size = new Size(265, 70),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Italic),
                ForeColor = Color.DarkSlateGray,
                Visible = false
            };

            // Add all controls to Panel
            panelBottom.Controls.AddRange(new Control[] {
                rdoLinear, rdoNonLinear, btnReset, btnOK, btnCancel,
                lblAlphaTitle, trackBarAlpha, lblAlphaValue,
                lblBetaTitle, trackBarBeta, lblBetaValue, picLinearCurve, lblFormulaLinear,
                lblGammaTitle, trackBarGamma, lblGammaValue, picGammaCurve, lblFormulaNonLinear
            });

            this.Controls.Add(pictureBox1);
            this.Controls.Add(panelBottom);

            InitializeContextMenu();

            this.DoubleBuffered = true;
        }

        private void InitializeContextMenu()
        {
            ContextMenuStrip imageContextMenu = new ContextMenuStrip();

            ToolStripMenuItem copyItem = new ToolStripMenuItem("複製圖片 (Copy Image)");
            copyItem.Click += (s, e) => {
                if (pictureBox1.Image != null)
                {
                    DIPSample.CopyImageToClipboard(pictureBox1.Image);
                    MessageBox.Show("圖片已複製到剪貼簿 (Image copied to clipboard)", "訊息 (Info)", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            imageContextMenu.Items.Add(copyItem);

            ToolStripMenuItem saveItem = new ToolStripMenuItem("另存圖片 (Save Image...)");
            saveItem.Click += (s, e) => {
                if (pictureBox1.Image != null)
                {
                    using (SaveFileDialog sfd = new SaveFileDialog())
                    {
                        sfd.Filter = "PNG 影像 (*.png)|*.png|BMP 影像 (*.bmp)|*.bmp|JPEG 影像 (*.jpg)|*.jpg";
                        sfd.DefaultExt = "png";
                        sfd.Title = "另存圖片 (Save Image)";
                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            ImageFormat format = ImageFormat.Png;
                            string ext = System.IO.Path.GetExtension(sfd.FileName).ToLower();
                            if (ext == ".bmp") format = ImageFormat.Bmp;
                            else if (ext == ".jpg" || ext == ".jpeg") format = ImageFormat.Jpeg;
                            
                            pictureBox1.Image.Save(sfd.FileName, format);
                        }
                    }
                }
            };
            imageContextMenu.Items.Add(saveItem);

            pictureBox1.ContextMenuStrip = imageContextMenu;
        }

        private void Mode_CheckedChanged(object sender, EventArgs e)
        {
            bool isLinear = rdoLinear.Checked;

            // Toggle visibility of Linear controls
            lblAlphaTitle.Visible = isLinear;
            trackBarAlpha.Visible = isLinear;
            lblAlphaValue.Visible = isLinear;
            lblBetaTitle.Visible = isLinear;
            trackBarBeta.Visible = isLinear;
            lblBetaValue.Visible = isLinear;
            picLinearCurve.Visible = isLinear;
            lblFormulaLinear.Visible = isLinear;

            // Toggle visibility of Non-Linear controls
            lblGammaTitle.Visible = !isLinear;
            trackBarGamma.Visible = !isLinear;
            lblGammaValue.Visible = !isLinear;
            picGammaCurve.Visible = !isLinear;
            lblFormulaNonLinear.Visible = !isLinear;

            UpdateImage();
        }

        private void TrackBarAlpha_ValueChanged(object sender, EventArgs e)
        {
            if (isUpdatingControls) return;
            isUpdatingControls = true;

            double alpha = GetAlphaValue();
            lblAlphaValue.Text = string.Format("{0:F1}", alpha);

            isUpdatingControls = false;
            UpdateImage();
        }

        private void TrackBarBeta_ValueChanged(object sender, EventArgs e)
        {
            if (isUpdatingControls) return;
            isUpdatingControls = true;

            int betaVal = trackBarBeta.Value;
            lblBetaValue.Text = betaVal.ToString();

            isUpdatingControls = false;
            UpdateImage();
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            if (rdoLinear.Checked)
            {
                isUpdatingControls = true;
                trackBarAlpha.Value = 100; // 1.0 (Middle)
                lblAlphaValue.Text = "1.0";
                trackBarBeta.Value = 0;
                lblBetaValue.Text = "0";
                isUpdatingControls = false;
            }
            else
            {
                trackBarGamma.Value = 100; // 1.0 (Center)
                lblGammaValue.Text = "1.00";
            }
            UpdateImage();
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            if (processedBmp != null)
            {
                string title = rdoLinear.Checked 
                    ? string.Format("亮度對比調整(線性, a={0:F1}, b={1})", GetAlphaValue(), trackBarBeta.Value)
                    : string.Format("Gamma轉換(非線性, g={0:F2})", GetGammaValue());
                Bitmap outputBmp = processedBmp.Clone(new Rectangle(0, 0, processedBmp.Width, processedBmp.Height), processedBmp.PixelFormat);
                mainForm.ShowNewImage(outputBmp, title);
            }
            this.Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void PicLinearCurve_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDraggingLinearCurve = true;
                lastLinearMouseX = e.X;
                lastLinearMouseY = e.Y;

                // On first click: translate the line so it passes through the clicked point
                TranslateLinearToPoint(e.X, e.Y);
            }
        }

        private void PicLinearCurve_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && isDraggingLinearCurve)
            {
                int dx = e.X - lastLinearMouseX;
                int dy = e.Y - lastLinearMouseY;
                lastLinearMouseX = e.X;
                lastLinearMouseY = e.Y;

                if (dx == 0 && dy == 0) return;
                DragLinearByDelta(dx, dy);
            }
            else
            {
                isDraggingLinearCurve = false;
            }
        }

        private void TranslateLinearToPoint(int mx, int my)
        {
            // Translate the current line y=alpha*x+beta so it passes through the clicked (nx, ny)
            int w = picLinearCurve.Width;
            int h = picLinearCurve.Height;
            if (w <= 0 || h <= 0) return;

            double nx = (double)mx / (w - 1); // normalized x in [0, 1] representing input pixel 0~255
            double ny = 1.0 - (double)my / (h - 1); // normalized y in [0, 1] representing output pixel 0~255
            nx = Math.Min(Math.Max(nx, 0.0), 1.0);
            ny = Math.Min(Math.Max(ny, 0.0), 1.0);

            // Current alpha stays the same; adjust beta so line passes through (nx*255, ny*255)
            double alpha = GetAlphaValue();
            double inputVal = nx * 255.0;
            double outputVal = ny * 255.0;
            // y = alpha * x + beta => beta = y - alpha * x
            int beta = (int)Math.Round(outputVal - alpha * inputVal);
            beta = Math.Min(Math.Max(beta, -255), 255);

            if (isUpdatingControls) return;
            isUpdatingControls = true;

            trackBarBeta.Value = beta;
            lblBetaValue.Text = beta.ToString();

            isUpdatingControls = false;
            UpdateImage();
        }

        private void DragLinearByDelta(int dx, int dy)
        {
            // dx: horizontal mouse movement -> adjust Alpha (contrast)
            // dy: vertical mouse movement -> adjust Beta (brightness), note: up is negative dy
            int w = picLinearCurve.Width;
            int h = picLinearCurve.Height;
            if (w <= 0 || h <= 0) return;

            if (isUpdatingControls) return;
            isUpdatingControls = true;

            // Adjust Alpha based on dx (sensitivity: full width = full alpha range)
            if (dx != 0)
            {
                double alphaDelta = (2.9 / (double)w) * dx; // proportional to curve width
                double currentAlpha = GetAlphaValue();
                double newAlpha = Math.Min(Math.Max(currentAlpha + alphaDelta, 0.1), 3.0);

                int alphaTrackVal;
                if (newAlpha <= 1.0)
                {
                    alphaTrackVal = (int)Math.Round((newAlpha - 0.1) / 0.9 * 100.0);
                }
                else
                {
                    alphaTrackVal = 100 + (int)Math.Round((newAlpha - 1.0) / 2.0 * 100.0);
                }
                alphaTrackVal = Math.Min(Math.Max(alphaTrackVal, 0), 200);
                trackBarAlpha.Value = alphaTrackVal;
                lblAlphaValue.Text = string.Format("{0:F1}", newAlpha);
            }

            // Adjust Beta based on dy (sensitivity: full height = full beta range)
            // Mouse up (negative dy) -> increase brightness (positive beta delta)
            if (dy != 0)
            {
                double betaDelta = -(510.0 / (double)h) * dy; // negative because mouse Y is inverted
                int currentBeta = trackBarBeta.Value;
                int newBeta = (int)Math.Round(currentBeta + betaDelta);
                newBeta = Math.Min(Math.Max(newBeta, -255), 255);
                trackBarBeta.Value = newBeta;
                lblBetaValue.Text = newBeta.ToString();
            }

            isUpdatingControls = false;
            UpdateImage();
        }

        private void PicGammaCurve_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                UpdateGammaFromMouse(e.X, e.Y);
            }
        }

        private void PicGammaCurve_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                UpdateGammaFromMouse(e.X, e.Y);
            }
        }

        private void UpdateGammaFromMouse(int mx, int my)
        {
            int w = picGammaCurve.Width;
            int h = picGammaCurve.Height;
            if (w <= 0 || h <= 0) return;

            double nx = Math.Min(Math.Max((double)mx / w, 0.05), 0.95);
            double ny = Math.Min(Math.Max(1.0 - (double)my / h, 0.05), 0.95);

            double gamma = Math.Log(ny) / Math.Log(nx);
            gamma = Math.Min(Math.Max(gamma, 0.1), 10.0);

            if (isUpdatingControls) return;
            isUpdatingControls = true;

            // Reverse-map gamma to trackbar value (0-200, center=100 at gamma=1.0)
            int gammaTrackVal;
            if (gamma <= 1.0)
            {
                gammaTrackVal = (int)Math.Round((gamma - 0.1) / 0.9 * 100.0);
            }
            else
            {
                gammaTrackVal = 100 + (int)Math.Round((gamma - 1.0) / 9.0 * 100.0);
            }
            gammaTrackVal = Math.Min(Math.Max(gammaTrackVal, 0), 200);

            trackBarGamma.Value = gammaTrackVal;
            lblGammaValue.Text = string.Format("{0:F2}", GetGammaValue());

            isUpdatingControls = false;
            UpdateImage();
        }

        private void PicLinearCurve_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int w = picLinearCurve.Width;
            int h = picLinearCurve.Height;

            // 1. Draw dashed diagonal benchmark (y = x)
            using (Pen dashPen = new Pen(Color.FromArgb(220, 220, 220), 1))
            {
                dashPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawLine(dashPen, 0, h - 1, w - 1, 0);
            }

            // 2. Get current values
            double alpha = GetAlphaValue();
            double beta = trackBarBeta.Value;

            // 3. Draw active linear stretching line
            PointF[] points = new PointF[w];
            for (int x = 0; x < w; x++)
            {
                double normX = (double)x / (w - 1) * 255.0;
                double normY = alpha * normX + beta;
                if (normY < 0) normY = 0;
                if (normY > 255) normY = 255;

                float drawY = (float)((1.0 - (normY / 255.0)) * (h - 1));
                points[x] = new PointF(x, drawY);
            }

            using (Pen curvePen = new Pen(Color.RoyalBlue, 2))
            {
                g.DrawLines(curvePen, points);
            }
        }

        private void PicGammaCurve_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int w = picGammaCurve.Width;
            int h = picGammaCurve.Height;

            // 1. Draw dashed diagonal benchmark (gamma = 1.0)
            using (Pen dashPen = new Pen(Color.FromArgb(220, 220, 220), 1))
            {
                dashPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawLine(dashPen, 0, h - 1, w - 1, 0);
            }

            // 2. Draw active gamma curve
            double gamma = GetGammaValue();
            PointF[] points = new PointF[w];
            for (int x = 0; x < w; x++)
            {
                double normX = (double)x / (w - 1);
                double normY = Math.Pow(normX, gamma);
                float drawY = (float)((1.0 - normY) * (h - 1));
                points[x] = new PointF(x, drawY);
            }

            using (Pen curvePen = new Pen(Color.RoyalBlue, 2))
            {
                g.DrawLines(curvePen, points);
            }
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

            if (rdoLinear.Checked)
            {
                // Linear Mode
                double alpha = GetAlphaValue();
                int beta = trackBarBeta.Value;

                unsafe
                {
                    fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                    {
                        DIPSample.adjust_brightness_contrast(f0, w, h, d, g0, alpha, beta);
                    }
                }

                if (processedBmp != null) processedBmp.Dispose();
                processedBmp = DIPSample.dyn_array2bmp(gArray, w, h, d, pf, pal);

                if (pf1 != null)
                {
                    pf1.Text = string.Format("線性調整: alpha={0:F1}, beta={1}", alpha, beta);
                }

                if (lblFormulaLinear != null)
                {
                    double v = 0.5 * alpha + (double)beta / 255.0;
                    string formulaText;
                    if (v > 0.0)
                    {
                        double eqGamma = -Math.Log(v) / Math.Log(2.0);
                        if (eqGamma < 0.05) eqGamma = 0.05;
                        formulaText = string.Format("等效 gamma = -log2(0.5 * {0:F1} + ({1})/255.0)\n" +
                                                    "           = -log2({2:F2}) = {3:F2}", alpha, beta, v, eqGamma);
                    }
                    else
                    {
                        formulaText = string.Format("等效 gamma = -log2(0.5 * {0:F1} + ({1})/255.0)\n" +
                                                    "           = -log2({2:F2}) = N/A (值 <= 0)", alpha, beta, v);
                    }
                    lblFormulaLinear.Text = string.Format("線性公式: y = {0:F1} * x + ({1})\n{2}", alpha, beta, formulaText);
                }

                if (picLinearCurve != null)
                {
                    picLinearCurve.Invalidate();
                }
            }
            else
            {
                // Non-Linear Gamma Mode
                double gamma = GetGammaValue();

                unsafe
                {
                    fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                    {
                        DIPSample.adjust_brightness_contrast(f0, w, h, d, g0, -gamma, 0);
                    }
                }

                if (processedBmp != null) processedBmp.Dispose();
                processedBmp = DIPSample.dyn_array2bmp(gArray, w, h, d, pf, pal);

                if (pf1 != null)
                {
                    pf1.Text = string.Format("非線性 Gamma 校正: gamma={0:F2}", gamma);
                }

                if (lblFormulaNonLinear != null)
                {
                    lblFormulaNonLinear.Text = string.Format("非線性公式: y = 255 * (x / 255.0) ^ {0:F2}", gamma);
                }

                if (picGammaCurve != null)
                {
                    picGammaCurve.Invalidate();
                }
            }

            pictureBox1.Image = processedBmp;
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
                catch { }
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
