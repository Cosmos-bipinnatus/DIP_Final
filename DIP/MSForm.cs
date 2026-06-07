using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Imaging;

namespace DIP
{
    public partial class MSForm : Form
    {
        internal Bitmap pBitmap;
        internal ToolStripStatusLabel pf1;
        int w, h;
        private ContextMenuStrip imageContextMenu;

        private Bitmap originalTransparentBmp;
        private int medianB = 128;
        private int medianG = 128;
        private int medianR = 128;
        private Color customBgColor = Color.FromArgb(240, 240, 240);

        internal bool isRotatedOutput = false;
        internal bool initialBlend = false;
        internal string initialBgType = "Transparent";
        internal Color initialCustomColor = Color.FromArgb(240, 240, 240);

        // Hough variables
        internal bool isHoughOutput = false;
        internal bool isHoughCircle = false;
        internal int houghThreshold = 50;
        internal int houghRMin = 10;
        internal int houghRMax = 80;
        internal Bitmap cleanHoughBmp = null;
        internal Bitmap bakedHoughBmp = null;

        private Panel panelBottomBg;
        private Panel panelControlsContainer;
        private RadioButton radioBgTransparent;
        private RadioButton radioBgBlack;
        private RadioButton radioBgWhite;
        private RadioButton radioBgGray;
        private RadioButton radioBgRed; // For Hough
        private RadioButton radioBgCustom;
        private Panel panelCustomColorPreview;
        private CheckBox chkBlendBg;

        public MSForm()
        {
            InitializeComponent();
        }

        private void InitializeContextMenu()
        {
            imageContextMenu = new ContextMenuStrip();

            ToolStripMenuItem copyItem = new ToolStripMenuItem("複製圖片 (Copy Image)");
            copyItem.Click += CopyItem_Click;
            imageContextMenu.Items.Add(copyItem);

            ToolStripMenuItem saveItem = new ToolStripMenuItem("另存圖片 (Save Image...)");
            saveItem.Click += SaveItem_Click;
            imageContextMenu.Items.Add(saveItem);

            pictureBox1.ContextMenuStrip = imageContextMenu;
        }

        private void CopyItem_Click(object sender, EventArgs e)
        {
            if (pBitmap != null)
            {
                DIPSample.CopyImageToClipboard(pBitmap);
                MessageBox.Show("圖片已複製到剪貼簿 (Image copied to clipboard)", "訊息 (Info)", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void SaveItem_Click(object sender, EventArgs e)
        {
            if (pBitmap != null)
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
                        
                        pBitmap.Save(sfd.FileName, format);
                    }
                }
            }
        }

        private void MSForm_Load(object sender, EventArgs e)
        {
            bmp_dip(pBitmap, pictureBox1);
            pf1.Text = "尺寸 (Width, Height)=(" + pBitmap.Width + "," + pBitmap.Height + ")";
            w = pBitmap.Width;
            h = pBitmap.Height;

            if (pBitmap.PixelFormat == PixelFormat.Format32bppArgb || isRotatedOutput)
            {
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.MaximizeBox = true;

                originalTransparentBmp = (Bitmap)pBitmap.Clone();
                CalculateOriginalMedians();
                InitializeBgPanel();
                ApplyInitialSettings();

                int targetClientWidth = Math.Max(pBitmap.Width, 425);
                int targetClientHeight = pBitmap.Height + 45;
                this.ClientSize = new Size(targetClientWidth, targetClientHeight);
                this.MinimumSize = new Size(425, 150);
            }
            else if (isHoughOutput)
            {
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.MaximizeBox = true;

                InitializeHoughPanel();
                ApplyInitialHoughSettings();

                int targetClientWidth = Math.Max(pBitmap.Width, 425);
                int targetClientHeight = pBitmap.Height + 45;
                this.ClientSize = new Size(targetClientWidth, targetClientHeight);
                this.MinimumSize = new Size(425, 150);
            }

            InitializeContextMenu();
        }

        private void ApplyInitialSettings()
        {
            customBgColor = initialCustomColor;
            panelCustomColorPreview.BackColor = customBgColor;

            chkBlendBg.Checked = initialBlend;

            if (initialBgType == "Transparent") radioBgTransparent.Checked = true;
            else if (initialBgType == "Black") radioBgBlack.Checked = true;
            else if (initialBgType == "White") radioBgWhite.Checked = true;
            else if (initialBgType == "Gray") radioBgGray.Checked = true;
            else if (initialBgType == "Custom") radioBgCustom.Checked = true;
        }

        private void CalculateOriginalMedians()
        {
            if (pBitmap == null) return;
            DIPSample mainForm = this.MdiParent as DIPSample;
            if (mainForm == null) return;

            int d = 0;
            PixelFormat pf = new PixelFormat();
            ColorPalette pal = null;
            int[] f = mainForm.dyn_bmp2array(pBitmap, ref d, ref pf, ref pal);

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

        private void InitializeBgPanel()
        {
            panelBottomBg = new Panel
            {
                Height = 45,
                Dock = DockStyle.Bottom,
                BackColor = SystemColors.Control,
                Padding = new Padding(5)
            };

            panelControlsContainer = new Panel
            {
                Size = new Size(425, 40),
                BackColor = Color.Transparent
            };

            radioBgTransparent = new RadioButton
            {
                Text = "透明",
                Location = new Point(0, 8),
                Size = new Size(55, 24),
                Checked = true
            };
            radioBgTransparent.CheckedChanged += RadioBg_CheckedChanged;

            radioBgBlack = new RadioButton
            {
                Text = "黑色",
                Location = new Point(55, 8),
                Size = new Size(55, 24)
            };
            radioBgBlack.CheckedChanged += RadioBg_CheckedChanged;

            radioBgWhite = new RadioButton
            {
                Text = "白色",
                Location = new Point(110, 8),
                Size = new Size(55, 24)
            };
            radioBgWhite.CheckedChanged += RadioBg_CheckedChanged;

            radioBgGray = new RadioButton
            {
                Text = "中間值",
                Location = new Point(165, 8),
                Size = new Size(65, 24)
            };
            radioBgGray.CheckedChanged += RadioBg_CheckedChanged;

            radioBgCustom = new RadioButton
            {
                Text = "自訂",
                Location = new Point(230, 8),
                Size = new Size(55, 24)
            };
            radioBgCustom.CheckedChanged += RadioBg_CheckedChanged;

            panelCustomColorPreview = new Panel
            {
                Location = new Point(285, 11),
                Size = new Size(18, 18),
                BackColor = customBgColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            panelCustomColorPreview.Click += PanelCustomColorPreview_Click;

            chkBlendBg = new CheckBox
            {
                Text = "融入原始影像",
                Location = new Point(315, 8),
                Size = new Size(110, 24),
                Checked = false,
                Enabled = true
            };
            chkBlendBg.CheckedChanged += ChkBlendBg_CheckedChanged;

            panelControlsContainer.Controls.AddRange(new Control[] {
                radioBgTransparent, radioBgBlack, radioBgWhite,
                radioBgGray, radioBgCustom, panelCustomColorPreview, chkBlendBg
            });

            panelBottomBg.Controls.Add(panelControlsContainer);

            panelBottomBg.SizeChanged += (s, e) => {
                panelControlsContainer.Left = (panelBottomBg.Width - panelControlsContainer.Width) / 2;
                panelControlsContainer.Top = (panelBottomBg.Height - panelControlsContainer.Height) / 2;
            };

            // Force initial position
            panelControlsContainer.Left = (panelBottomBg.Width - panelControlsContainer.Width) / 2;
            panelControlsContainer.Top = (panelBottomBg.Height - panelControlsContainer.Height) / 2;

            this.Controls.Add(panelBottomBg);
            panelBottomBg.BringToFront();
        }

        private void PanelCustomColorPreview_Click(object sender, EventArgs e)
        {
            radioBgCustom.Checked = true;
            ChooseCustomColor();
        }

        private void RadioBg_CheckedChanged(object sender, EventArgs e)
        {
            if (radioBgCustom.Checked && !RotateImageForm.lastCustomBgColor.HasValue)
            {
                ChooseCustomColor();
            }
            UpdateBgRendering();
        }

        private void ChooseCustomColor()
        {
            using (ColorDialog cd = new ColorDialog())
            {
                cd.Color = customBgColor;
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    customBgColor = cd.Color;
                    RotateImageForm.lastCustomBgColor = cd.Color; // Update shared color history
                    panelCustomColorPreview.BackColor = customBgColor;
                    UpdateBgRendering();
                }
            }
        }

        private void ChkBlendBg_CheckedChanged(object sender, EventArgs e)
        {
            UpdateBgRendering();
        }

        private void UpdateBgRendering()
        {
            if (originalTransparentBmp == null) return;

            Color bgCol = Color.Transparent;
            if (radioBgBlack.Checked) bgCol = Color.Black;
            else if (radioBgWhite.Checked) bgCol = Color.White;
            else if (radioBgGray.Checked) bgCol = Color.FromArgb(medianR, medianG, medianB);
            else if (radioBgCustom.Checked) bgCol = customBgColor;

            // Determine if the actual bitmap pixels should be transparent (Alpha = 0)
            // or opaque (Alpha = 255)
            bool makeBitmapTransparent = radioBgTransparent.Checked || !chkBlendBg.Checked;

            // Dispose old pBitmap (if it is not the original one)
            if (pBitmap != originalTransparentBmp && pBitmap != null)
            {
                pBitmap.Dispose();
            }

            // Dispose old pictureBox1.Image (if it is not the original one or pBitmap)
            if (pictureBox1.Image != originalTransparentBmp && pictureBox1.Image != pBitmap && pictureBox1.Image != null)
            {
                pictureBox1.Image.Dispose();
            }

            if (makeBitmapTransparent)
            {
                pBitmap = (Bitmap)originalTransparentBmp.Clone();
                pictureBox1.Image = pBitmap;
            }
            else
            {
                Bitmap bakedBmp = new Bitmap(originalTransparentBmp.Width, originalTransparentBmp.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bakedBmp))
                {
                    g.Clear(bgCol);
                    g.DrawImage(originalTransparentBmp, 0, 0);
                }

                pBitmap = bakedBmp;
                pictureBox1.Image = pBitmap;
            }

            // Apply consistent background display for both PictureBox and Form (outside image)
            if (radioBgTransparent.Checked)
            {
                pictureBox1.BackgroundImage = DIPSample.GetCheckerboardBitmap();
                pictureBox1.BackgroundImageLayout = ImageLayout.Tile;
                pictureBox1.BackColor = Color.Transparent;

                this.BackgroundImage = DIPSample.GetCheckerboardBitmap();
                this.BackgroundImageLayout = ImageLayout.Tile;
                this.BackColor = SystemColors.Control;
            }
            else
            {
                pictureBox1.BackgroundImage = null;
                this.BackgroundImage = null;

                if (!chkBlendBg.Checked)
                {
                    pictureBox1.BackColor = bgCol;
                    this.BackColor = bgCol;
                }
                else
                {
                    pictureBox1.BackColor = SystemColors.Control;
                    this.BackColor = SystemColors.Control;
                }
            }

            DIPSample mainForm = this.MdiParent as DIPSample;
            if (mainForm != null) mainForm.UpdateHistogram();
        }

        private Bitmap bmp_read(OpenFileDialog oFileDlg)
        {
            Bitmap pBitmap;
            string fileloc = oFileDlg.FileName;
            pBitmap = new Bitmap(fileloc);
            w = pBitmap.Width;
            h = pBitmap.Height;
            return pBitmap;
        }

        private void bmp_dip(Bitmap pBitmap, PictureBox pictureBox1)
        {
            this.Width = pBitmap.Width + (this.Width - this.ClientRectangle.Width);
            this.Height = pBitmap.Height + (this.Height - this.ClientRectangle.Height);
            pictureBox1.Image = pBitmap;
            LayoutControls();

            if (pBitmap.PixelFormat == PixelFormat.Format32bppArgb)
            {
                pictureBox1.BackgroundImage = DIPSample.GetCheckerboardBitmap();
                pictureBox1.BackgroundImageLayout = ImageLayout.Tile;
                pictureBox1.BackColor = Color.Transparent;
            }
            else
            {
                pictureBox1.BackgroundImage = null;
                pictureBox1.BackColor = SystemColors.Control;
            }
        }

        private void bmp_disp(Bitmap pBitmap, PictureBox pictureBox2)
        {
            pictureBox2.Image = pBitmap;
        }

        private void bmp_write(Bitmap pBitmap, SaveFileDialog sFileDlg)
        {
            sFileDlg.Filter = "BMP 影像 (Bitmap Image)|*.bmp";
            sFileDlg.Title = "儲存影像檔案 (Save an Image File)";
            sFileDlg.ShowDialog();
            if (sFileDlg.FileName != "")
            {
                pBitmap.Save(sFileDlg.FileName);
            }
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (pBitmap == null || pf1 == null) return;

            int imgW = pBitmap.Width;
            int imgH = pBitmap.Height;

            if (e.X >= 0 && e.X < imgW && e.Y >= 0 && e.Y < imgH)
            {
                try
                {
                    Color pixel = pBitmap.GetPixel(e.X, e.Y);
                    pf1.Text = "(" + e.X + "," + e.Y + ")" +
                                "=(" + pixel.R.ToString() + "," + pixel.G.ToString() + "," + pixel.B.ToString() + ")";
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

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            LayoutControls();
        }

        private void LayoutControls()
        {
            if (pBitmap == null || pictureBox1 == null) return;

            pictureBox1.Width = pBitmap.Width;
            pictureBox1.Height = pBitmap.Height;

            // Center horizontally
            pictureBox1.Left = (this.ClientSize.Width - pictureBox1.Width) / 2;

            // Center vertically in available height
            int panelHeight = (panelBottomBg != null && panelBottomBg.Visible) ? panelBottomBg.Height : 0;
            int availableHeight = this.ClientSize.Height - panelHeight;
            pictureBox1.Top = (availableHeight - pictureBox1.Height) / 2;

            // Prevent top overflow for standard images to align top boundary if window is small
            if (panelBottomBg == null && pictureBox1.Top < 0)
            {
                pictureBox1.Top = 0;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (pBitmap != null)
            {
                this.BeginInvoke((MethodInvoker)delegate
                {
                    int targetClientWidth, targetClientHeight;
                    if (panelBottomBg != null)
                    {
                        targetClientWidth = Math.Max(pBitmap.Width, 425);
                        targetClientHeight = pBitmap.Height + 45;
                    }
                    else
                    {
                        targetClientWidth = pBitmap.Width;
                        targetClientHeight = pBitmap.Height;
                    }
                    this.ClientSize = new Size(targetClientWidth, targetClientHeight);
                });
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (originalTransparentBmp != null)
            {
                originalTransparentBmp.Dispose();
                originalTransparentBmp = null;
            }
            if (cleanHoughBmp != null)
            {
                cleanHoughBmp.Dispose();
                cleanHoughBmp = null;
            }
            if (bakedHoughBmp != null)
            {
                bakedHoughBmp.Dispose();
                bakedHoughBmp = null;
            }
            if (pBitmap != null)
            {
                pictureBox1.Image = null;
                if (pBitmap != cleanHoughBmp && pBitmap != bakedHoughBmp)
                {
                    pBitmap.Dispose();
                }
                pBitmap = null;
            }
            base.OnFormClosed(e);
            DIPSample mainForm = this.MdiParent as DIPSample;
            if (mainForm != null) mainForm.UpdateHistogram();
        }

        private void InitializeHoughPanel()
        {
            panelBottomBg = new Panel
            {
                Height = 45,
                Dock = DockStyle.Bottom,
                BackColor = SystemColors.Control,
                Padding = new Padding(5)
            };

            panelControlsContainer = new Panel
            {
                Size = new Size(425, 40),
                BackColor = Color.Transparent
            };

            radioBgRed = new RadioButton
            {
                Text = "紅色",
                Location = new Point(0, 8),
                Size = new Size(55, 24)
            };
            radioBgRed.CheckedChanged += RadioHoughColor_CheckedChanged;

            radioBgBlack = new RadioButton
            {
                Text = "黑色",
                Location = new Point(55, 8),
                Size = new Size(55, 24)
            };
            radioBgBlack.CheckedChanged += RadioHoughColor_CheckedChanged;

            radioBgWhite = new RadioButton
            {
                Text = "白色",
                Location = new Point(110, 8),
                Size = new Size(55, 24)
            };
            radioBgWhite.CheckedChanged += RadioHoughColor_CheckedChanged;

            radioBgCustom = new RadioButton
            {
                Text = "自訂",
                Location = new Point(165, 8),
                Size = new Size(55, 24)
            };
            radioBgCustom.CheckedChanged += RadioHoughColor_CheckedChanged;

            panelCustomColorPreview = new Panel
            {
                Location = new Point(220, 11),
                Size = new Size(18, 18),
                BackColor = customBgColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            panelCustomColorPreview.Click += PanelCustomHoughColorPreview_Click;

            chkBlendBg = new CheckBox
            {
                Text = "融入原始影像",
                Location = new Point(250, 8),
                Size = new Size(110, 24),
                Checked = true
            };
            chkBlendBg.CheckedChanged += ChkBlendHough_CheckedChanged;

            panelControlsContainer.Controls.AddRange(new Control[] {
                radioBgRed, radioBgBlack, radioBgWhite,
                radioBgCustom, panelCustomColorPreview, chkBlendBg
            });

            panelBottomBg.Controls.Add(panelControlsContainer);

            panelBottomBg.SizeChanged += (s, e) => {
                panelControlsContainer.Left = (panelBottomBg.Width - panelControlsContainer.Width) / 2;
                panelControlsContainer.Top = (panelBottomBg.Height - panelControlsContainer.Height) / 2;
            };

            panelControlsContainer.Left = (panelBottomBg.Width - panelControlsContainer.Width) / 2;
            panelControlsContainer.Top = (panelBottomBg.Height - panelControlsContainer.Height) / 2;

            this.Controls.Add(panelBottomBg);
            panelBottomBg.BringToFront();
        }

        private void ApplyInitialHoughSettings()
        {
            customBgColor = initialCustomColor;
            panelCustomColorPreview.BackColor = customBgColor;
            chkBlendBg.Checked = initialBlend;

            if (initialBgType == "Red") radioBgRed.Checked = true;
            else if (initialBgType == "Black") radioBgBlack.Checked = true;
            else if (initialBgType == "White") radioBgWhite.Checked = true;
            else if (initialBgType == "Custom") radioBgCustom.Checked = true;
        }

        private void RadioHoughColor_CheckedChanged(object sender, EventArgs e)
        {
            UpdateHoughRendering();
        }

        private void PanelCustomHoughColorPreview_Click(object sender, EventArgs e)
        {
            radioBgCustom.Checked = true;
            ChooseCustomHoughColor();
        }

        private void ChooseCustomHoughColor()
        {
            using (ColorDialog cd = new ColorDialog())
            {
                cd.Color = customBgColor;
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    customBgColor = cd.Color;
                    RotateImageForm.lastCustomBgColor = cd.Color; // Share color history
                    panelCustomColorPreview.BackColor = customBgColor;
                    UpdateHoughRendering();
                }
            }
        }

        private void ChkBlendHough_CheckedChanged(object sender, EventArgs e)
        {
            UpdateHoughRendering();
        }

        private void UpdateHoughRendering()
        {
            if (cleanHoughBmp == null) return;

            bool showLines = chkBlendBg.Checked;

            Color lineCol = Color.Red;
            if (radioBgBlack.Checked) lineCol = Color.Black;
            else if (radioBgWhite.Checked) lineCol = Color.White;
            else if (radioBgRed.Checked) lineCol = Color.Red;
            else if (radioBgCustom.Checked) lineCol = customBgColor;

            DIPSample mainForm = this.MdiParent as DIPSample;
            if (mainForm != null)
            {
                int w = cleanHoughBmp.Width;
                int h = cleanHoughBmp.Height;
                int d = 0;
                PixelFormat pf = new PixelFormat();
                ColorPalette pal = null;

                int[] fArray = mainForm.dyn_bmp2array(cleanHoughBmp, ref d, ref pf, ref pal);
                int[] gArray = new int[w * h * d];

                unsafe
                {
                    fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                    {
                        if (isHoughCircle)
                        {
                            DIPSample.detect_circles_hough(f0, w, h, d, g0, houghRMin, houghRMax, houghThreshold, lineCol.R, lineCol.G, lineCol.B);
                        }
                        else
                        {
                            DIPSample.detect_lines_hough(f0, w, h, d, g0, houghThreshold, lineCol.R, lineCol.G, lineCol.B);
                        }
                    }
                }

                if (bakedHoughBmp != null) bakedHoughBmp.Dispose();
                bakedHoughBmp = DIPSample.dyn_array2bmp(gArray, w, h, d, pf, pal);

                if (pBitmap != cleanHoughBmp && pBitmap != bakedHoughBmp && pBitmap != null)
                {
                    pBitmap.Dispose();
                }

                if (showLines)
                {
                    pBitmap = (Bitmap)bakedHoughBmp.Clone();
                }
                else
                {
                    pBitmap = (Bitmap)cleanHoughBmp.Clone();
                }

                pictureBox1.Image = bakedHoughBmp;
            }

            DIPSample mainApp = this.MdiParent as DIPSample;
            if (mainApp != null) mainApp.UpdateHistogram();
        }
    }
}
