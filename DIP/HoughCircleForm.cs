using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace DIP
{
    public class HoughCircleForm : Form
    {
        private DIPSample mainForm;
        private Bitmap originalBmp;
        private Bitmap processedBmp;

        internal ToolStripStatusLabel pf1;

        private PictureBox pictureBox1;
        private Panel panelBottom;

        private TrackBar trackBarRMin;
        private Label lblRMinTitle;
        private Label lblRMinValue;

        private TrackBar trackBarRMax;
        private Label lblRMaxTitle;
        private Label lblRMaxValue;

        private TrackBar trackBarThreshold;
        private Label lblThresholdTitle;
        private Label lblThresholdValue;

        // Custom Color Controls
        private Label lblLineColorCaption;
        private Panel panelLineColorGroup;
        private RadioButton radioLineBlack;
        private RadioButton radioLineWhite;
        private RadioButton radioLineRed;
        private RadioButton radioLineCustom;
        private Panel panelCustomColorPreview;
        private Color customLineColor = Color.Red; // Grayscale default is Red

        // Output Option Controls
        private Label lblOutputOptionCaption;
        private Panel panelOutputGroup;
        private RadioButton radioBlend;
        private RadioButton radioOverlay;

        private Button btnReset;
        private Button btnOK;
        private Button btnCancel;

        public Bitmap ProcessedBitmap
        {
            get { return (this.IsDisposed || this.Disposing) ? null : processedBmp; }
        }

        public string ImageInfoParameters
        {
            get
            {
                return string.Format("[即時預覽調整中]\n半徑範圍: {0} ~ {1} 像素\n投票閾值: {2}\n繪製顏色: {3}", 
                    trackBarRMin.Value, trackBarRMax.Value, trackBarThreshold.Value, GetSelectedColor().Name);
            }
        }

        public string ImageAlgorithmDescription
        {
            get
            {
                return "使用三維參數累積空間 (x_c, y_c, r) 進行投票。為了大幅提高效率，底層結合了 Sobel 梯度方向向量投影，使邊緣點僅沿著梯度方向及其反方向進行一維線段投票，大幅減少無用累積。當局部投票數大於門檻值時即判定圓心與半徑存在。";
            }
        }

        public HoughCircleForm(DIPSample mainForm, Bitmap originalBmp)
        {
            this.mainForm = mainForm;
            this.originalBmp = originalBmp;

            // Default color: Red for grayscale, Black for color
            this.customLineColor = (originalBmp.PixelFormat == PixelFormat.Format8bppIndexed) ? Color.Red : Color.Black;

            InitializeUI();
            UpdateImage();
        }

        private void InitializeUI()
        {
            this.Text = "霍夫圓形偵測預覽";

            int W = originalBmp.Width;
            int H = originalBmp.Height;
            int maxPreviewW = 800;
            int maxPreviewH = 500;
            int previewW = W;
            int previewH = H;

            double ratioX = (double)maxPreviewW / W;
            double ratioY = (double)maxPreviewH / H;
            double ratio = Math.Min(ratioX, ratioY);

            bool needZoom = false;
            if (ratio < 1.0)
            {
                previewW = (int)(W * ratio);
                previewH = (int)(H * ratio);
                needZoom = true;
            }

            int initialWidth = Math.Max(previewW, 540);
            int initialHeight = previewH + 225; // Compacted bottom panel height

            this.ClientSize = new Size(initialWidth, initialHeight);
            this.MinimumSize = new Size(540, 260);
            this.FormBorderStyle = FormBorderStyle.Fixed3D;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9F);

            // 1. PictureBox
            pictureBox1 = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = needZoom ? PictureBoxSizeMode.Zoom : PictureBoxSizeMode.CenterImage,
                BackColor = SystemColors.Control
            };
            pictureBox1.MouseMove += PictureBox1_MouseMove;

            // 2. Bottom Panel
            panelBottom = new Panel
            {
                Height = 225,
                Dock = DockStyle.Bottom,
                BackColor = SystemColors.Control,
                Padding = new Padding(10)
            };

            // 3. RMin Title & TrackBar & Label (Y = 10)
            lblRMinTitle = new Label
            {
                Text = "最小半徑 (Min Radius):",
                Location = new Point(15, 10),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            trackBarRMin = new TrackBar
            {
                Minimum = 2,
                Maximum = 100,
                Value = 51, // Centered
                Location = new Point(170, 5),
                Width = 220,
                TickStyle = TickStyle.None
            };
            trackBarRMin.ValueChanged += TrackBarRMin_ValueChanged;

            lblRMinValue = new Label
            {
                Text = "51",
                Location = new Point(400, 10),
                Size = new Size(40, 20),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.RoyalBlue,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 4. RMax Title & TrackBar & Label (Y = 45)
            lblRMaxTitle = new Label
            {
                Text = "最大半徑 (Max Radius):",
                Location = new Point(15, 45),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            trackBarRMax = new TrackBar
            {
                Minimum = 2,
                Maximum = 200,
                Value = 101, // Centered
                Location = new Point(170, 40),
                Width = 220,
                TickStyle = TickStyle.None
            };
            trackBarRMax.ValueChanged += TrackBarRMax_ValueChanged;

            lblRMaxValue = new Label
            {
                Text = "101",
                Location = new Point(400, 45),
                Size = new Size(40, 20),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.RoyalBlue,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 5. Threshold Title & TrackBar & Label (Y = 80)
            lblThresholdTitle = new Label
            {
                Text = "累加器門檻 (Threshold):",
                Location = new Point(15, 80),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            trackBarThreshold = new TrackBar
            {
                Minimum = 10,
                Maximum = 90,
                Value = 50, // Centered
                Location = new Point(170, 75),
                Width = 220,
                TickStyle = TickStyle.None
            };
            trackBarThreshold.ValueChanged += TrackBarThreshold_ValueChanged;

            lblThresholdValue = new Label
            {
                Text = "50",
                Location = new Point(400, 80),
                Size = new Size(40, 20),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.RoyalBlue,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 6. Line Color Controls (Y = 115)
            lblLineColorCaption = new Label
            {
                Text = "線條顏色 (Line Color):",
                Location = new Point(15, 115),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            panelLineColorGroup = new Panel
            {
                Location = new Point(170, 112),
                Size = new Size(340, 28),
                BackColor = Color.Transparent
            };

            radioLineRed = new RadioButton
            {
                Text = "紅色",
                Location = new Point(5, 2),
                Size = new Size(55, 24),
                Checked = (originalBmp.PixelFormat == PixelFormat.Format8bppIndexed)
            };
            radioLineRed.CheckedChanged += RadioLineColor_CheckedChanged;

            radioLineBlack = new RadioButton
            {
                Text = "黑色",
                Location = new Point(65, 2),
                Size = new Size(55, 24),
                Checked = (originalBmp.PixelFormat != PixelFormat.Format8bppIndexed)
            };
            radioLineBlack.CheckedChanged += RadioLineColor_CheckedChanged;

            radioLineWhite = new RadioButton
            {
                Text = "白色",
                Location = new Point(125, 2),
                Size = new Size(55, 24)
            };
            radioLineWhite.CheckedChanged += RadioLineColor_CheckedChanged;

            radioLineCustom = new RadioButton
            {
                Text = "自訂",
                Location = new Point(185, 2),
                Size = new Size(55, 24)
            };
            radioLineCustom.CheckedChanged += RadioLineColor_CheckedChanged;

            panelCustomColorPreview = new Panel
            {
                Location = new Point(245, 4),
                Size = new Size(18, 18),
                BackColor = customLineColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            panelCustomColorPreview.Click += PanelCustomColorPreview_Click;

            panelLineColorGroup.Controls.AddRange(new Control[] {
                radioLineRed, radioLineBlack, radioLineWhite, radioLineCustom, panelCustomColorPreview
            });

            // 7. Output Option Controls (Y = 150)
            lblOutputOptionCaption = new Label
            {
                Text = "輸出選項 (Output):",
                Location = new Point(15, 150),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            panelOutputGroup = new Panel
            {
                Location = new Point(170, 147),
                Size = new Size(340, 28),
                BackColor = Color.Transparent
            };

            radioBlend = new RadioButton
            {
                Text = "融入原圖",
                Location = new Point(5, 2),
                Size = new Size(100, 24),
                Checked = true
            };
            radioBlend.CheckedChanged += (s, e) => UpdateImage();

            radioOverlay = new RadioButton
            {
                Text = "懸浮檢視",
                Location = new Point(110, 2),
                Size = new Size(100, 24)
            };
            radioOverlay.CheckedChanged += (s, e) => UpdateImage();

            panelOutputGroup.Controls.AddRange(new Control[] { radioBlend, radioOverlay });

            // 8. Reset Button (Y = 185)
            btnReset = new Button
            {
                Text = "重置預設值",
                Location = new Point(15, 185),
                Size = new Size(110, 28),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnReset.Click += BtnReset_Click;

            // 9. OK and Cancel Buttons (Y = 185)
            btnOK = new Button
            {
                Text = "確定",
                Location = new Point(310, 185),
                Size = new Size(80, 28),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(400, 185),
                Size = new Size(80, 28),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnCancel.Click += BtnCancel_Click;

            // Add all controls to Panel
            panelBottom.Controls.AddRange(new Control[] {
                lblRMinTitle, trackBarRMin, lblRMinValue,
                lblRMaxTitle, trackBarRMax, lblRMaxValue,
                lblThresholdTitle, trackBarThreshold, lblThresholdValue,
                lblLineColorCaption, panelLineColorGroup,
                lblOutputOptionCaption, panelOutputGroup,
                btnReset, btnOK, btnCancel
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

        private void TrackBarRMin_ValueChanged(object sender, EventArgs e)
        {
            if (trackBarRMin.Value > trackBarRMax.Value)
            {
                trackBarRMax.Value = trackBarRMin.Value;
            }
            lblRMinValue.Text = trackBarRMin.Value.ToString();
            UpdateImage();
        }

        private void TrackBarRMax_ValueChanged(object sender, EventArgs e)
        {
            if (trackBarRMax.Value < trackBarRMin.Value)
            {
                trackBarRMin.Value = trackBarRMax.Value;
            }
            lblRMaxValue.Text = trackBarRMax.Value.ToString();
            UpdateImage();
        }

        private void TrackBarThreshold_ValueChanged(object sender, EventArgs e)
        {
            int threshVal = trackBarThreshold.Value;
            lblThresholdValue.Text = threshVal.ToString();
            UpdateImage();
        }

        private void RadioLineColor_CheckedChanged(object sender, EventArgs e)
        {
            UpdateImage();
        }

        private void PanelCustomColorPreview_Click(object sender, EventArgs e)
        {
            radioLineCustom.Checked = true;
            ChooseCustomColor();
        }

        private void ChooseCustomColor()
        {
            using (ColorDialog cd = new ColorDialog())
            {
                cd.Color = customLineColor;
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    customLineColor = cd.Color;
                    panelCustomColorPreview.BackColor = customLineColor;
                    UpdateImage();
                }
            }
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            trackBarRMin.Value = 51;
            trackBarRMax.Value = 101;
            trackBarThreshold.Value = 50;
            radioLineRed.Checked = (originalBmp.PixelFormat == PixelFormat.Format8bppIndexed);
            radioLineBlack.Checked = (originalBmp.PixelFormat != PixelFormat.Format8bppIndexed);
            radioLineWhite.Checked = false;
            radioLineCustom.Checked = false;
            customLineColor = (originalBmp.PixelFormat == PixelFormat.Format8bppIndexed) ? Color.Red : Color.Black;
            panelCustomColorPreview.BackColor = customLineColor;
            radioBlend.Checked = true;
        }

        private Color GetSelectedColor()
        {
            if (radioLineBlack.Checked) return Color.Black;
            if (radioLineWhite.Checked) return Color.White;
            if (radioLineRed.Checked) return Color.Red;
            return customLineColor;
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            if (processedBmp != null)
            {
                // Generate clean 24bpp original image
                int w = originalBmp.Width;
                int h = originalBmp.Height;
                Bitmap cleanBmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
                using (Graphics gr = Graphics.FromImage(cleanBmp))
                {
                    gr.DrawImage(originalBmp, new Rectangle(0, 0, w, h));
                }

                string title = string.Format("霍夫圓形 (R:{0}-{1}, Threshold={2})", trackBarRMin.Value, trackBarRMax.Value, trackBarThreshold.Value);

                MSForm childForm = new MSForm();
                childForm.MdiParent = mainForm;
                childForm.pf1 = this.pf1;
                childForm.Text = title;

                // Pass variables to MSForm for interactive Hough rendering
                childForm.isHoughOutput = true;
                childForm.isHoughCircle = true;
                childForm.houghRMin = trackBarRMin.Value;
                childForm.houghRMax = trackBarRMax.Value;
                childForm.houghThreshold = trackBarThreshold.Value;
                childForm.cleanHoughBmp = cleanBmp;
                childForm.bakedHoughBmp = (Bitmap)processedBmp.Clone();
                childForm.initialBlend = radioBlend.Checked;

                // Set initial color type
                if (radioLineRed.Checked) childForm.initialBgType = "Red";
                else if (radioLineBlack.Checked) childForm.initialBgType = "Black";
                else if (radioLineWhite.Checked) childForm.initialBgType = "White";
                else childForm.initialBgType = "Custom";

                childForm.initialCustomColor = customLineColor;

                // MSForm will load default based on initialBlend
                childForm.pBitmap = radioBlend.Checked ? childForm.bakedHoughBmp : childForm.cleanHoughBmp;

                childForm.ImageInfoParameters = string.Format("套用演算法: 霍夫圓形偵測 (Hough Circle Detection)\n半徑範圍: {0} ~ {1} 像素\n投票閾值: {2}\n繪製顏色: {3}", 
                    trackBarRMin.Value, trackBarRMax.Value, trackBarThreshold.Value, GetSelectedColor().Name);
                childForm.ImageAlgorithmDescription = ImageAlgorithmDescription;

                childForm.Show();
            }
            this.Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void UpdateImage()
        {
            if (originalBmp == null) return;

            int w = originalBmp.Width;
            int h = originalBmp.Height;

            // Force converting to 24bppRgb if 8bpp Indexed to ensure line drawing supports color
            Bitmap workingBmp = originalBmp;
            bool tempCreated = false;
            if (originalBmp.PixelFormat == PixelFormat.Format8bppIndexed)
            {
                workingBmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
                using (Graphics gr = Graphics.FromImage(workingBmp))
                {
                    gr.DrawImage(originalBmp, new Rectangle(0, 0, w, h));
                }
                tempCreated = true;
            }

            int d = 0;
            PixelFormat pf = new PixelFormat();
            ColorPalette pal = null;

            int[] fArray = mainForm.dyn_bmp2array(workingBmp, ref d, ref pf, ref pal);
            int[] gArray = new int[w * h * d];

            int rMin = trackBarRMin.Value;
            int rMax = trackBarRMax.Value;
            int T = trackBarThreshold.Value;
            Color lineCol = GetSelectedColor();

            unsafe
            {
                fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                {
                    DIPSample.detect_circles_hough(f0, w, h, d, g0, rMin, rMax, T, lineCol.R, lineCol.G, lineCol.B);
                }
            }

            if (processedBmp != null) processedBmp.Dispose();
            processedBmp = DIPSample.dyn_array2bmp(gArray, w, h, d, pf, pal);

            if (pf1 != null)
            {
                pf1.Text = string.Format("霍夫圓形偵測調整: R={0}-{1}, Threshold={2}", rMin, rMax, T);
            }

            pictureBox1.Image = processedBmp;
            mainForm.UpdateHistogram();

            if (tempCreated) workingBmp.Dispose();
        }

        private void PictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (processedBmp == null || pf1 == null) return;

            int imgW = processedBmp.Width;
            int imgH = processedBmp.Height;
            int pbW = pictureBox1.Width;
            int pbH = pictureBox1.Height;

            int imgX = 0;
            int imgY = 0;

            if (pictureBox1.SizeMode == PictureBoxSizeMode.Zoom)
            {
                double ratio = Math.Min((double)pbW / imgW, (double)pbH / imgH);
                double displayW = imgW * ratio;
                double displayH = imgH * ratio;
                double offsetX = (pbW - displayW) / 2.0;
                double offsetY = (pbH - displayH) / 2.0;
                imgX = (int)((e.X - offsetX) / ratio);
                imgY = (int)((e.Y - offsetY) / ratio);
            }
            else
            {
                int offsetX = (pbW - imgW) / 2;
                int offsetY = (pbH - imgH) / 2;
                imgX = e.X - offsetX;
                imgY = e.Y - offsetY;
            }

            if (imgX >= 0 && imgX < imgW && imgY >= 0 && imgY < imgH)
            {
                try
                {
                    Color pixel = processedBmp.GetPixel(imgX, imgY);
                    pf1.Text = "(" + imgX + "," + imgY + ")=(" + pixel.R + "," + pixel.G + "," + pixel.B + ")";
                }
                catch { }
            }
        }
    }
}
