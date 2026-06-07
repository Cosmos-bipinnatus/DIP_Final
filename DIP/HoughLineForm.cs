using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace DIP
{
    public class HoughLineForm : Form
    {
        private DIPSample mainForm;
        private Bitmap originalBmp;
        private Bitmap processedBmp;

        internal ToolStripStatusLabel pf1;

        private PictureBox pictureBox1;
        private Panel panelBottom;

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
                return string.Format("[即時預覽調整中]\n投票閾值: {0}\n繪製顏色: {1}", trackBarThreshold.Value, GetSelectedColor().Name);
            }
        }

        public string ImageAlgorithmDescription
        {
            get
            {
                return "利用極坐標對偶原理 (rho = x * cos(theta) + y * sin(theta)) 將影像空間中的邊緣點映射到 (rho, theta) 參數累積空間。當多個邊緣點共線時，其對應的曲線會在累積空間中交於一點並形成局部極大值。檢測大於投票門檻的點即可提取出影像中的直線輪廓。";
            }
        }

        public HoughLineForm(DIPSample mainForm, Bitmap originalBmp)
        {
            this.mainForm = mainForm;
            this.originalBmp = originalBmp;

            // Set default color based on format: grayscale default is Red, color default is Black
            this.customLineColor = (originalBmp.PixelFormat == PixelFormat.Format8bppIndexed) ? Color.Red : Color.Black;

            InitializeUI();
            UpdateImage();
        }

        private void InitializeUI()
        {
            this.Text = "霍夫直線偵測預覽";

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
            int initialHeight = previewH + 180; // Panel height is 180 to avoid clipping

            this.ClientSize = new Size(initialWidth, initialHeight);
            this.MinimumSize = new Size(540, 220);
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
                Height = 180,
                Dock = DockStyle.Bottom,
                BackColor = SystemColors.Control,
                Padding = new Padding(10)
            };

            // 3. Threshold Title & TrackBar & Value Label
            lblThresholdTitle = new Label
            {
                Text = "累加器門檻 (Threshold):",
                Location = new Point(15, 15),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            trackBarThreshold = new TrackBar
            {
                Minimum = 10,
                Maximum = 290,
                Value = 150, // Center position (10 + 290) / 2 = 150
                Location = new Point(170, 10),
                Width = 220,
                TickStyle = TickStyle.None
            };
            trackBarThreshold.ValueChanged += TrackBarThreshold_ValueChanged;

            lblThresholdValue = new Label
            {
                Text = "150",
                Location = new Point(400, 15),
                Size = new Size(40, 20),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.RoyalBlue,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 4. Line Color Controls
            lblLineColorCaption = new Label
            {
                Text = "線條顏色 (Line Color):",
                Location = new Point(15, 55),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            panelLineColorGroup = new Panel
            {
                Location = new Point(170, 52),
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

            // 5. Output Option Controls
            lblOutputOptionCaption = new Label
            {
                Text = "輸出選項 (Output):",
                Location = new Point(15, 95),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            panelOutputGroup = new Panel
            {
                Location = new Point(170, 92),
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

            // 6. Reset Button
            btnReset = new Button
            {
                Text = "重置預設值",
                Location = new Point(15, 135),
                Size = new Size(110, 28),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnReset.Click += BtnReset_Click;

            // 7. OK and Cancel Buttons
            btnOK = new Button
            {
                Text = "確定",
                Location = new Point(310, 135),
                Size = new Size(80, 28),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(400, 135),
                Size = new Size(80, 28),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnCancel.Click += BtnCancel_Click;

            // Add all controls to Panel
            panelBottom.Controls.AddRange(new Control[] {
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
            trackBarThreshold.Value = 150;
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

                string title = string.Format("霍夫直線 (Threshold={0})", trackBarThreshold.Value);

                MSForm childForm = new MSForm();
                childForm.MdiParent = mainForm;
                childForm.pf1 = this.pf1;
                childForm.Text = title;

                // Pass variables to MSForm for interactive Hough rendering
                childForm.isHoughOutput = true;
                childForm.isHoughCircle = false;
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

                childForm.ImageInfoParameters = string.Format("套用演算法: 霍夫直線偵測 (Hough Line Detection)\n投票閾值: {0}\n繪製顏色: {1}", 
                    trackBarThreshold.Value, GetSelectedColor().Name);
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

            int T = trackBarThreshold.Value;
            Color lineCol = GetSelectedColor();

            unsafe
            {
                fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                {
                    DIPSample.detect_lines_hough(f0, w, h, d, g0, T, lineCol.R, lineCol.G, lineCol.B);
                }
            }

            if (processedBmp != null) processedBmp.Dispose();
            processedBmp = DIPSample.dyn_array2bmp(gArray, w, h, d, pf, pal);

            if (pf1 != null)
            {
                pf1.Text = string.Format("霍夫直線偵測調整: Threshold={0}", T);
            }

            // Preview always draws the line
            if (radioBlend.Checked)
            {
                pictureBox1.Image = processedBmp;
            }
            else
            {
                // In overlay mode, we show the processed preview, but output will be clean
                pictureBox1.Image = processedBmp;
            }
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
