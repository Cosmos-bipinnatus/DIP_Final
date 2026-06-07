using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace DIP
{
    public class HighBoostFilterForm : Form
    {
        private DIPSample mainForm;
        private Bitmap originalBmp;
        private Bitmap processedBmp;

        internal ToolStripStatusLabel pf1;

        private PictureBox pictureBox1;
        private Panel panelBottom;

        private TrackBar trackBarA;
        private Label lblATitle;
        private Label lblAValue;

        private Button btnReset;
        private Button btnOK;
        private Button btnCancel;

        private bool isUpdating = false;

        public Bitmap ProcessedBitmap
        {
            get { return (this.IsDisposed || this.Disposing) ? null : processedBmp; }
        }

        public string ImageInfoParameters
        {
            get
            {
                double A = (double)trackBarA.Value / 10.0;
                return string.Format("[即時預覽調整中]\n提升係數 (A): {0:F1}", A);
            }
        }

        public string ImageAlgorithmDescription
        {
            get
            {
                return "高提升濾波 (High-boost Filtering) 是一種基於反銳化遮罩 (Unsharp Masking) 的影像增強技術。它透過原圖減去模糊化影像產生高頻邊緣細節的『細節遮罩』，並將遮罩乘以權重後疊加回原圖：g = A * 原圖 - 模糊圖 = (A-1) * 原圖 + (原圖 - 模糊圖)。當 A=1 時等同於一般增強，A>1 時則進一步保留背景明暗層次，並強化圖像銳利度。";
            }
        }

        public HighBoostFilterForm(DIPSample mainForm, Bitmap originalBmp)
        {
            this.mainForm = mainForm;
            this.originalBmp = originalBmp;

            InitializeUI();
            UpdateImage();
        }

        private void InitializeUI()
        {
            this.Text = "高提升濾波預覽 (High-boost Filter Preview)";

            int initialWidth = Math.Max(originalBmp.Width, 580);
            int initialHeight = originalBmp.Height + 150;

            this.ClientSize = new Size(initialWidth, initialHeight);
            this.MinimumSize = new Size(580, 200);
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
                Height = 110,
                Dock = DockStyle.Bottom,
                BackColor = SystemColors.Control,
                Padding = new Padding(10)
            };

            // 3. Slider controls for Coefficient A
            lblATitle = new Label
            {
                Text = "提升係數 A (Scale A, 1.0 ~ 3.0):",
                Location = new Point(15, 20),
                Size = new Size(185, 20),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            trackBarA = new TrackBar
            {
                Minimum = 10,
                Maximum = 30,
                Value = 12, // Default A = 1.2
                Location = new Point(205, 15),
                Width = 220,
                TickStyle = TickStyle.None
            };
            trackBarA.ValueChanged += TrackBarA_ValueChanged;

            lblAValue = new Label
            {
                Text = "1.2",
                Location = new Point(435, 20),
                Size = new Size(40, 20),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.RoyalBlue,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 4. Action Buttons
            btnReset = new Button
            {
                Text = "重置預設值",
                Location = new Point(15, 72),
                Size = new Size(110, 28),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnReset.Click += BtnReset_Click;

            btnOK = new Button
            {
                Text = "確定",
                Location = new Point(340, 72),
                Size = new Size(80, 28),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(430, 72),
                Size = new Size(80, 28),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnCancel.Click += BtnCancel_Click;

            // Add controls to panel
            panelBottom.Controls.AddRange(new Control[] {
                lblATitle, trackBarA, lblAValue,
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

        private void TrackBarA_ValueChanged(object sender, EventArgs e)
        {
            if (isUpdating) return;
            double A = (double)trackBarA.Value / 10.0;
            lblAValue.Text = A.ToString("F1");
            UpdateImage();
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            isUpdating = true;
            try
            {
                trackBarA.Value = 12;
                lblAValue.Text = "1.2";
                UpdateImage();
            }
            finally
            {
                isUpdating = false;
            }
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            if (processedBmp != null)
            {
                double A = (double)trackBarA.Value / 10.0;
                string title = string.Format("高提升濾波 (High-Boost, A={0:F1})", A);
                Bitmap outputBmp = processedBmp.Clone(new Rectangle(0, 0, processedBmp.Width, processedBmp.Height), processedBmp.PixelFormat);
                string paramText = string.Format("套用演算法: 高提升濾波 (High-boost Filter)\n提升係數 (A): {0:F1}", A);
                mainForm.ShowNewImage(outputBmp, title, paramText, ImageAlgorithmDescription);
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
            int d = 0;
            PixelFormat pf = new PixelFormat();
            ColorPalette pal = null;

            int[] fArray = mainForm.dyn_bmp2array(originalBmp, ref d, ref pf, ref pal);
            int[] gArray = new int[w * h * d];

            double A = (double)trackBarA.Value / 10.0;
            double center = 8.0 + (A - 1.0) * 9.0;
            double[] kernel = new double[] { -1, -1, -1, -1, center, -1, -1, -1, -1 };
            int kSize = 3;
            double divisor = 1.0;
            double offset = 0.0;

            unsafe
            {
                fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                {
                    DIPSample.convolution_filter(f0, w, h, d, g0, kernel, kSize, divisor, offset);
                }
            }

            if (processedBmp != null) processedBmp.Dispose();
            processedBmp = DIPSample.dyn_array2bmp(gArray, w, h, d, pf, pal);

            if (pf1 != null)
            {
                pf1.Text = string.Format("高提升濾波調整: A={0:F1}", A);
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
                    pf1.Text = string.Format("({0},{1})=({2},{3},{4})", imgX, imgY, pixel.R, pixel.G, pixel.B);
                }
                catch { }
            }
        }
    }
}
