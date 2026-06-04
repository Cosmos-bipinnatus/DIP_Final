using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace DIP
{
    public class RotateImageForm : Form
    {
        private DIPSample mainForm;
        private Bitmap originalBmp;
        private Bitmap processedBmp;

        public Bitmap ProcessedBitmap
        {
            get { return (this.IsDisposed || this.Disposing) ? null : processedBmp; }
        }

        internal ToolStripStatusLabel pf1;

        // UI Controls
        private PictureBox pictureBox1;
        private Panel panelBottom;
        private TrackBar trackBarAngle;
        private TextBox txtAngle;
        private Label lblAngleCaption;
        private Label lblDegreeSign;
        private Label lblMappingCaption;
        private Panel panelMappingGroup;
        private RadioButton radioBackward;
        private RadioButton radioForward;
        private Label lblInterpCaption;
        private Panel panelInterpGroup;
        private RadioButton radioNearest;
        private RadioButton radioBilinear;

        // Background Color Controls
        private Label lblBgColorCaption;
        private Panel panelBgGroup;
        private RadioButton radioBgTransparent;
        private RadioButton radioBgBlack;
        private RadioButton radioBgWhite;
        private RadioButton radioBgGray;
        private RadioButton radioBgCustom;
        private Panel panelCustomColorPreview;
        private static Color? lastCustomBgColor = null;
        private Color customBgColor = Color.FromArgb(240, 240, 240);

        private Label lblSizeInfo;
        private Button btnApply;
        private Button btnCancel;
        private Button btnReset;

        private CheckBox chkBlendBg;
        private int medianB = 128;
        private int medianG = 128;
        private int medianR = 128;

        public int[] BackgroundMask { get; private set; }

        // Prevent recursive update from TrackBar <-> TextBox sync
        private bool isUpdating = false;

        public RotateImageForm(DIPSample mainForm, Bitmap originalBmp)
        {
            this.mainForm = mainForm;
            this.originalBmp = originalBmp;

            if (lastCustomBgColor.HasValue)
            {
                this.customBgColor = lastCustomBgColor.Value;
            }

            CalculateOriginalMedians();

            InitializeUI();
            UpdatePreview();
        }

        private void CalculateOriginalMedians()
        {
            if (originalBmp == null) return;
            int d = 0;
            PixelFormat pf = new PixelFormat();
            ColorPalette pal = null;
            int[] f = mainForm.dyn_bmp2array(originalBmp, ref d, ref pf, ref pal);

            int[] histB = new int[256];
            int[] histG = new int[256];
            int[] histR = new int[256];

            if (d == 1)
            {
                for (int i = 0; i < f.Length; i++)
                {
                    int val = f[i];
                    if (val >= 0 && val <= 255) histB[val]++;
                }
                Array.Copy(histB, histG, 256);
                Array.Copy(histB, histR, 256);
            }
            else if (d == 3 || d == 4)
            {
                for (int i = 0; i < f.Length; i += d)
                {
                    if (d == 4 && f[i + 3] == 0) continue; // Skip transparent pixels in median calculation
                    int b = f[i + 0];
                    int g_val = f[i + 1];
                    int r = f[i + 2];
                    if (b >= 0 && b <= 255) histB[b]++;
                    if (g_val >= 0 && g_val <= 255) histG[g_val]++;
                    if (r >= 0 && r <= 255) histR[r]++;
                }
            }

            medianB = GetMedianFromHist(histB);
            medianG = GetMedianFromHist(histG);
            medianR = GetMedianFromHist(histR);
        }

        private int GetMedianFromHist(int[] hist)
        {
            long total = 0;
            for (int i = 0; i < 256; i++) total += hist[i];
            if (total <= 0) return 128;
            long cum = 0;
            long half = total / 2;
            for (int i = 0; i < 256; i++)
            {
                cum += hist[i];
                if (cum >= half) return i;
            }
            return 128;
        }

        private void InitializeUI()
        {
            this.Text = "影像旋轉 (Image Rotation)";
            this.Font = new Font("Segoe UI", 9F);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.DoubleBuffered = true;

            // Calculate max canvas size at startup (diagonal)
            int srcW = originalBmp.Width;
            int srcH = originalBmp.Height;
            int maxSize = (int)Math.Ceiling(Math.Sqrt(srcW * srcW + srcH * srcH));

            // Set window size large enough to hold the maximum rotated size at 1:1 scale
            int initialWidth = Math.Max(maxSize, 520);
            int initialHeight = maxSize + 255;
            this.ClientSize = new Size(initialWidth, initialHeight);
            this.MinimumSize = new Size(520, 460);

            // ── PictureBox (Fill) ──
            pictureBox1 = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.FromArgb(128, 128, 128)
            };
            pictureBox1.MouseMove += PictureBox1_MouseMove;
            InitializeContextMenu();

            // ── Bottom Panel ──
            panelBottom = new Panel
            {
                Height = 255,
                Dock = DockStyle.Bottom,
                BackColor = SystemColors.Control,
                Padding = new Padding(10)
            };

            int y = 10;

            // Row 1: Angle (TrackBar + TextBox)
            lblAngleCaption = new Label
            {
                Text = "旋轉角度 (Angle):",
                Location = new Point(15, y + 2),
                Size = new Size(120, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            trackBarAngle = new TrackBar
            {
                Minimum = 0,
                Maximum = 359,
                Value = 0,
                Location = new Point(140, y - 2),
                Width = 220,
                TickStyle = TickStyle.None,
                SmallChange = 1,
                LargeChange = 15
            };
            trackBarAngle.ValueChanged += TrackBarAngle_ValueChanged;

            txtAngle = new TextBox
            {
                Text = "0",
                Location = new Point(370, y),
                Size = new Size(60, 24),
                TextAlign = HorizontalAlignment.Right
            };
            txtAngle.KeyDown += TxtAngle_KeyDown;
            txtAngle.Leave += TxtAngle_Leave;

            lblDegreeSign = new Label
            {
                Text = "°",
                Location = new Point(432, y + 2),
                Size = new Size(20, 20),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold)
            };

            y += 38;

            // Row 2: Mapping Mode (Forward / Backward)
            lblMappingCaption = new Label
            {
                Text = "映射模式 (Mapping):",
                Location = new Point(15, y + 2),
                Size = new Size(135, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            panelMappingGroup = new Panel
            {
                Location = new Point(150, y),
                Size = new Size(340, 28),
                BackColor = Color.Transparent
            };

            radioBackward = new RadioButton
            {
                Text = "反向映射 (Backward)",
                Location = new Point(5, 2),
                Size = new Size(150, 24),
                Checked = true
            };
            radioBackward.CheckedChanged += RadioMapping_CheckedChanged;

            radioForward = new RadioButton
            {
                Text = "正向映射 (Forward)",
                Location = new Point(175, 2),
                Size = new Size(150, 24),
                Checked = false
            };
            radioForward.CheckedChanged += RadioMapping_CheckedChanged;

            panelMappingGroup.Controls.AddRange(new Control[] { radioBackward, radioForward });

            y += 32;

            // Row 3: Interpolation Mode (Nearest / Bilinear)
            lblInterpCaption = new Label
            {
                Text = "插值模式 (Interpolation):",
                Location = new Point(15, y + 2),
                Size = new Size(160, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            panelInterpGroup = new Panel
            {
                Location = new Point(170, y),
                Size = new Size(340, 28),
                BackColor = Color.Transparent
            };

            radioNearest = new RadioButton
            {
                Text = "最近鄰 (Nearest Neighbor)",
                Location = new Point(5, 2),
                Size = new Size(170, 24),
                Checked = false
            };
            radioNearest.CheckedChanged += RadioInterp_CheckedChanged;

            radioBilinear = new RadioButton
            {
                Text = "雙線性 (Bilinear)",
                Location = new Point(195, 2),
                Size = new Size(120, 24),
                Checked = true
            };
            radioBilinear.CheckedChanged += RadioInterp_CheckedChanged;

            panelInterpGroup.Controls.AddRange(new Control[] { radioNearest, radioBilinear });

            y += 32;

            // Row 4: Background Color (Black / White / Gray / Custom)
            lblBgColorCaption = new Label
            {
                Text = "背景顏色 (Background):",
                Location = new Point(15, y + 2),
                Size = new Size(130, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            panelBgGroup = new Panel
            {
                Location = new Point(150, y),
                Size = new Size(360, 28),
                BackColor = Color.Transparent
            };

            radioBgTransparent = new RadioButton
            {
                Text = "透明",
                Location = new Point(5, 2),
                Size = new Size(60, 24),
                Checked = true
            };
            radioBgTransparent.CheckedChanged += RadioBgColor_CheckedChanged;

            radioBgBlack = new RadioButton
            {
                Text = "黑色",
                Location = new Point(65, 2),
                Size = new Size(60, 24),
                Checked = false
            };
            radioBgBlack.CheckedChanged += RadioBgColor_CheckedChanged;

            radioBgWhite = new RadioButton
            {
                Text = "白色",
                Location = new Point(125, 2),
                Size = new Size(60, 24),
                Checked = false
            };
            radioBgWhite.CheckedChanged += RadioBgColor_CheckedChanged;

            radioBgGray = new RadioButton
            {
                Text = "中間值",
                Location = new Point(185, 2),
                Size = new Size(75, 24),
                Checked = false
            };
            radioBgGray.CheckedChanged += RadioBgColor_CheckedChanged;

            radioBgCustom = new RadioButton
            {
                Text = "自訂",
                Location = new Point(260, 2),
                Size = new Size(60, 24),
                Checked = false
            };
            radioBgCustom.CheckedChanged += RadioBgColor_CheckedChanged;

            panelCustomColorPreview = new Panel
            {
                Location = new Point(320, 4),
                Size = new Size(18, 18),
                BackColor = customBgColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            panelCustomColorPreview.Click += PanelCustomColorPreview_Click;

            panelBgGroup.Controls.AddRange(new Control[] { radioBgTransparent, radioBgBlack, radioBgWhite, radioBgGray, radioBgCustom, panelCustomColorPreview });

            y += 32;

            // Row 4.5: Blend Background CheckBox
            chkBlendBg = new CheckBox
            {
                Text = "將背景顏色融入原始影像 (Blend background into image)",
                Location = new Point(15, y),
                Size = new Size(400, 24),
                Checked = true,
                Enabled = true
            };
            chkBlendBg.CheckedChanged += (s, e) => UpdatePreview();

            y += 28;

            // Row 5: Size Info
            lblSizeInfo = new Label
            {
                Text = string.Format("影像尺寸: 原始 {0}×{1} → 旋轉後 {0}×{1}", originalBmp.Width, originalBmp.Height),
                Location = new Point(15, y),
                Size = new Size(460, 20),
                ForeColor = Color.FromArgb(80, 80, 80)
            };

            y += 25;

            // Row 6: Buttons
            btnApply = new Button
            {
                Text = "確定 (Apply)",
                Location = new Point(100, y),
                Size = new Size(100, 30),
                FlatStyle = FlatStyle.System
            };
            btnApply.Click += BtnApply_Click;

            btnCancel = new Button
            {
                Text = "取消 (Cancel)",
                Location = new Point(215, y),
                Size = new Size(100, 30),
                FlatStyle = FlatStyle.System
            };
            btnCancel.Click += (s, e) => this.Close();

            btnReset = new Button
            {
                Text = "重置預設值 (Reset)",
                Location = new Point(330, y),
                Size = new Size(130, 30),
                FlatStyle = FlatStyle.System
            };
            btnReset.Click += BtnReset_Click;

            // Add controls to panel
            panelBottom.Controls.AddRange(new Control[]
            {
                lblAngleCaption, trackBarAngle, txtAngle, lblDegreeSign,
                lblMappingCaption, panelMappingGroup,
                lblInterpCaption, panelInterpGroup,
                lblBgColorCaption, panelBgGroup,
                chkBlendBg,
                lblSizeInfo,
                btnApply, btnCancel, btnReset
            });

            // Add to form (order matters for docking: bottom first, then fill)
            this.Controls.Add(pictureBox1);
            this.Controls.Add(panelBottom);
        }

        // ==========================================
        // Event Handlers
        // ==========================================

        private void TrackBarAngle_ValueChanged(object sender, EventArgs e)
        {
            if (isUpdating) return;
            isUpdating = true;
            txtAngle.Text = trackBarAngle.Value.ToString();
            isUpdating = false;
            UpdatePreview();
        }

        private void TxtAngle_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ApplyTextAngle();
            }
        }

        private void TxtAngle_Leave(object sender, EventArgs e)
        {
            ApplyTextAngle();
        }

        private void ApplyTextAngle()
        {
            if (isUpdating) return;
            isUpdating = true;

            double val;
            if (double.TryParse(txtAngle.Text, out val))
            {
                // Clamp to 0~359
                while (val < 0) val += 360;
                while (val >= 360) val -= 360;
                txtAngle.Text = val.ToString("F1");

                // Sync trackbar to nearest integer
                int intVal = (int)Math.Round(val);
                if (intVal > 359) intVal = 359;
                if (intVal < 0) intVal = 0;
                trackBarAngle.Value = intVal;
            }
            else
            {
                txtAngle.Text = trackBarAngle.Value.ToString();
            }

            isUpdating = false;
            UpdatePreview();
        }

        private void RadioMapping_CheckedChanged(object sender, EventArgs e)
        {
            // Disable interpolation selection when Forward mapping is selected
            bool isBackward = radioBackward.Checked;
            radioNearest.Enabled = isBackward;
            radioBilinear.Enabled = isBackward;
            lblInterpCaption.Enabled = isBackward;

            UpdatePreview();
        }

        private void RadioInterp_CheckedChanged(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            if (processedBmp != null)
            {
                double angle = GetCurrentAngle();
                int mode = GetCurrentMode();
                string modeStr = mode == 2 ? "正向 (Forward)" :
                                 mode == 1 ? "雙線性 (Bilinear)" : "最近鄰 (Nearest)";

                // Generate clean transparent rotated 32bpp Argb bitmap
                Bitmap transparentBmp = GenerateTransparentRotatedBitmap();

                MSForm childForm = new MSForm();
                childForm.MdiParent = mainForm;
                childForm.pf1 = this.pf1;
                childForm.pBitmap = transparentBmp;
                childForm.Text = string.Format("旋轉 {0:F1}° — {1}", angle, modeStr);

                // Pass initial settings
                childForm.isRotatedOutput = true;
                childForm.initialBlend = chkBlendBg.Checked;
                
                if (radioBgTransparent.Checked) childForm.initialBgType = "Transparent";
                else if (radioBgBlack.Checked) childForm.initialBgType = "Black";
                else if (radioBgWhite.Checked) childForm.initialBgType = "White";
                else if (radioBgGray.Checked) childForm.initialBgType = "Gray";
                else childForm.initialBgType = "Custom";

                childForm.initialCustomColor = customBgColor;

                childForm.Show();
            }
            this.Close();
        }

        private Bitmap GenerateTransparentRotatedBitmap()
        {
            double angle = GetCurrentAngle();
            int mode = GetCurrentMode();

            // Force Format32bppArgb for transparency
            Bitmap bmpToProcess = ConvertBitmapFormat(originalBmp, PixelFormat.Format32bppArgb);
            
            int srcW = bmpToProcess.Width;
            int srcH = bmpToProcess.Height;
            int d = 4;
            PixelFormat pf = PixelFormat.Format32bppArgb;
            ColorPalette pal = bmpToProcess.Palette;

            int[] fArray = mainForm.dyn_bmp2array(bmpToProcess, ref d, ref pf, ref pal);
            bmpToProcess.Dispose();

            double rad = angle * Math.PI / 180.0;
            int newW = (int)Math.Ceiling(Math.Abs(srcW * Math.Cos(rad)) + Math.Abs(srcH * Math.Sin(rad)));
            int newH = (int)Math.Ceiling(Math.Abs(srcW * Math.Sin(rad)) + Math.Abs(srcH * Math.Cos(rad)));
            if (newW < 2) newW = 2;
            if (newH < 2) newH = 2;

            int[] gArray = new int[newW * newH * d];

            int bg_r = 0, bg_g = 0, bg_b = 0, bg_a = 0; // Force transparent background

            unsafe
            {
                fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                {
                    DIPSample.rotate_image(f0, srcW, srcH, d, g0, newW, newH, angle, mode, bg_r, bg_g, bg_b, bg_a);
                }
            }

            return DIPSample.dyn_array2bmp(gArray, newW, newH, d, pf, pal);
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            isUpdating = true;
            trackBarAngle.Value = 0;
            txtAngle.Text = "0";
            radioBackward.Checked = true;
            radioBilinear.Checked = true;

            // Reset background color to Transparent (透明)
            radioBgTransparent.Checked = true;
            chkBlendBg.Checked = true;
            chkBlendBg.Enabled = true;
            customBgColor = Color.FromArgb(240, 240, 240);
            panelCustomColorPreview.BackColor = customBgColor;

            isUpdating = false;
            UpdatePreview();
        }

        private void RadioBgColor_CheckedChanged(object sender, EventArgs e)
        {
            if (radioBgCustom.Checked && !lastCustomBgColor.HasValue)
            {
                ChooseCustomColor();
            }
            UpdatePreview();
        }

        private void PanelCustomColorPreview_Click(object sender, EventArgs e)
        {
            radioBgCustom.Checked = true;
            ChooseCustomColor();
        }

        private void ChooseCustomColor()
        {
            using (ColorDialog cd = new ColorDialog())
            {
                cd.Color = customBgColor;
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    customBgColor = cd.Color;
                    lastCustomBgColor = cd.Color; // Record history
                    panelCustomColorPreview.BackColor = customBgColor;
                    UpdatePreview();
                }
            }
        }

        private void GetBgColorRGBA(out int r, out int g, out int b, out int a)
        {
            if (radioBgTransparent.Checked)
            {
                r = 0; g = 0; b = 0;
                a = 0; // Always transparent background
            }
            else if (radioBgBlack.Checked)
            {
                r = 0; g = 0; b = 0; a = chkBlendBg.Checked ? 255 : 0;
            }
            else if (radioBgWhite.Checked)
            {
                r = 255; g = 255; b = 255; a = chkBlendBg.Checked ? 255 : 0;
            }
            else if (radioBgGray.Checked)
            {
                r = medianR; g = medianG; b = medianB; a = chkBlendBg.Checked ? 255 : 0;
            }
            else // Custom
            {
                r = customBgColor.R;
                g = customBgColor.G;
                b = customBgColor.B;
                a = chkBlendBg.Checked ? 255 : 0;
            }
        }

        // ==========================================
        // Core Preview Update
        // ==========================================

        private double GetCurrentAngle()
        {
            double angle;
            if (double.TryParse(txtAngle.Text, out angle))
            {
                return angle;
            }
            return (double)trackBarAngle.Value;
        }

        private int GetCurrentMode()
        {
            if (radioForward.Checked)
                return 2; // Forward Mapping
            else if (radioBilinear.Checked)
                return 1; // Backward + Bilinear
            else
                return 0; // Backward + Nearest
        }

        private Bitmap ConvertBitmapFormat(Bitmap src, PixelFormat targetFormat)
        {
            if (src.PixelFormat == targetFormat)
            {
                return (Bitmap)src.Clone();
            }
            Bitmap dest = new Bitmap(src.Width, src.Height, targetFormat);
            using (Graphics g = Graphics.FromImage(dest))
            {
                g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height));
            }
            return dest;
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

        private void UpdatePreview()
        {
            if (originalBmp == null) return;

            double angle = GetCurrentAngle();
            int mode = GetCurrentMode();

            // Detect if we need format conversion
            bool targetTransparent = radioBgTransparent.Checked || !chkBlendBg.Checked;
            Bitmap bmpToProcess = originalBmp;
            bool needsDispose = false;

            if (targetTransparent)
            {
                bmpToProcess = ConvertBitmapFormat(originalBmp, PixelFormat.Format32bppArgb);
                needsDispose = true;
            }
            else if (originalBmp.PixelFormat == PixelFormat.Format8bppIndexed)
            {
                int tr, tg, tb, ta;
                GetBgColorRGBA(out tr, out tg, out tb, out ta);
                bool isBgColorGrayscale = (tr == tg && tg == tb);
                if (!isBgColorGrayscale)
                {
                    bmpToProcess = ConvertBitmapFormat(originalBmp, PixelFormat.Format24bppRgb);
                    needsDispose = true;
                }
            }

            int srcW = bmpToProcess.Width;
            int srcH = bmpToProcess.Height;
            int d = 0;
            PixelFormat pf = new PixelFormat();
            ColorPalette pal = null;

            int[] fArray = mainForm.dyn_bmp2array(bmpToProcess, ref d, ref pf, ref pal);

            if (needsDispose)
            {
                bmpToProcess.Dispose();
            }

            // Calculate new canvas dimensions (auto-expand bounding box)
            double rad = angle * Math.PI / 180.0;
            int newW = (int)Math.Ceiling(Math.Abs(srcW * Math.Cos(rad)) + Math.Abs(srcH * Math.Sin(rad)));
            int newH = (int)Math.Ceiling(Math.Abs(srcW * Math.Sin(rad)) + Math.Abs(srcH * Math.Cos(rad)));
            if (newW < 2) newW = 2;
            if (newH < 2) newH = 2;

            int[] gArray = new int[newW * newH * d];

            int bg_r, bg_g, bg_b, bg_a;
            GetBgColorRGBA(out bg_r, out bg_g, out bg_b, out bg_a);

            // Update PictureBox background color/image to match transparency settings
            if (radioBgTransparent.Checked)
            {
                pictureBox1.BackgroundImage = DIPSample.GetCheckerboardBitmap();
                pictureBox1.BackgroundImageLayout = ImageLayout.Tile;
                pictureBox1.BackColor = Color.Transparent;
            }
            else
            {
                pictureBox1.BackgroundImage = null;
                pictureBox1.BackColor = Color.FromArgb(bg_r, bg_g, bg_b);
            }

            unsafe
            {
                fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                {
                    DIPSample.rotate_image(f0, srcW, srcH, d, g0, newW, newH, angle, mode, bg_r, bg_g, bg_b, bg_a);
                }
            }

            // Calculate Background Mask
            int[] mask = new int[newW * newH];
            double cos_t = Math.Cos(rad);
            double sin_t = Math.Sin(rad);
            double cx_src = srcW / 2.0;
            double cy_src = srcH / 2.0;
            double cx_dst = newW / 2.0;
            double cy_dst = newH / 2.0;

            if (mode == 2) // Forward
            {
                for (int y = 0; y < srcH; y++)
                {
                    for (int x = 0; x < srcW; x++)
                    {
                        double dx = x - cx_src;
                        double dy = y - cy_src;
                        double dst_xf = dx * cos_t + dy * sin_t + cx_dst;
                        double dst_yf = -dx * sin_t + dy * cos_t + cy_dst;
                        int ix = (int)Math.Floor(dst_xf + 0.5);
                        int iy = (int)Math.Floor(dst_yf + 0.5);
                        if (ix >= 0 && ix < newW && iy >= 0 && iy < newH)
                        {
                            mask[iy * newW + ix] = 1;
                        }
                    }
                }
            }
            else if (mode == 0) // Backward Nearest
            {
                for (int yp = 0; yp < newH; yp++)
                {
                    for (int xp = 0; xp < newW; xp++)
                    {
                        double dx = xp - cx_dst;
                        double dy = yp - cy_dst;
                        double src_x = dx * cos_t - dy * sin_t + cx_src;
                        double src_y = dx * sin_t + dy * cos_t + cy_src;
                        int ix = (int)Math.Floor(src_x + 0.5);
                        int iy = (int)Math.Floor(src_y + 0.5);
                        if (ix >= 0 && ix < srcW && iy >= 0 && iy < srcH)
                        {
                            mask[yp * newW + xp] = 1;
                        }
                    }
                }
            }
            else if (mode == 1) // Backward Bilinear
            {
                for (int yp = 0; yp < newH; yp++)
                {
                    for (int xp = 0; xp < newW; xp++)
                    {
                        double dx = xp - cx_dst;
                        double dy = yp - cy_dst;
                        double src_x = dx * cos_t - dy * sin_t + cx_src;
                        double src_y = dx * sin_t + dy * cos_t + cy_src;
                        int x0 = (int)Math.Floor(src_x);
                        int y0 = (int)Math.Floor(src_y);
                        int x1 = x0 + 1;
                        int y1 = y0 + 1;
                        if (x0 >= 0 && x1 < srcW && y0 >= 0 && y1 < srcH)
                        {
                            mask[yp * newW + xp] = 1;
                        }
                    }
                }
            }
            BackgroundMask = mask;

            // Dispose old processed bitmap
            if (processedBmp != null)
            {
                processedBmp.Dispose();
            }
            processedBmp = DIPSample.dyn_array2bmp(gArray, newW, newH, d, pf, pal);
            pictureBox1.Image = processedBmp;

            // Update size info label
            lblSizeInfo.Text = string.Format(
                "影像尺寸: 原始 {0}×{1} → 旋轉後 {2}×{3}",
                srcW, srcH, newW, newH);

            // Update status bar
            if (pf1 != null)
            {
                string modeStr = mode == 2 ? "Forward" :
                                 mode == 1 ? "Bilinear" : "Nearest";
                pf1.Text = string.Format("旋轉: {0:F1}° | {1} | {2}×{3}",
                    angle, modeStr, newW, newH);
            }

            // Trigger histogram update
            mainForm.UpdateHistogram();
        }

        // ==========================================
        // Mouse Tracking (pixel info on hover)
        // ==========================================

        private void PictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (processedBmp == null || pf1 == null) return;

            // Map mouse position to image coordinates for CenterImage SizeMode
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
                    // Ignore GDI+ coordinate exceptions
                }
            }
            else
            {
                pf1.Text = "尺寸 (Width, Height)=(" + imgW + "," + imgH + ")";
            }
        }

        // ==========================================
        // Cleanup
        // ==========================================

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
