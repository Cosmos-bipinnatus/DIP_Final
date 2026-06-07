using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace DIP
{
    public class MedianFilterForm : Form
    {
        private DIPSample mainForm;
        private Bitmap originalBmp;
        private Bitmap processedBmp;

        internal ToolStripStatusLabel pf1;

        private PictureBox pictureBox1;
        private Panel panelBottom;

        private RadioButton rdb1x1;
        private RadioButton rdb3x3;
        private RadioButton rdb5x5;
        private GroupBox grpKernel;

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
                int kSize = GetSelectedKernelSize();
                return string.Format("[即時預覽調整中]\n核心大小: {0} x {1}", kSize, kSize);
            }
        }

        public string ImageAlgorithmDescription
        {
            get
            {
                return "中位數濾波是一種非線性空間鄰域低通濾波器。它將滑動窗口內的鄰域像素亮度值進行排序，並將中位數賦予中心像素。此演算法在消除椒鹽雜訊（隨機產生的黑白亮暗點）時表現極佳，且相較於均值濾波，能更完整地保護影像的邊緣輪廓，不造成嚴重的模糊現象。";
            }
        }

        public MedianFilterForm(DIPSample mainForm, Bitmap originalBmp)
        {
            this.mainForm = mainForm;
            this.originalBmp = originalBmp;

            InitializeUI();
            UpdateImage();
        }

        private void InitializeUI()
        {
            this.Text = "中位數濾波預覽 (Median Filter Preview)";

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

            // 3. GroupBox for Kernel Selection
            grpKernel = new GroupBox
            {
                Text = "濾波器核心大小 (Kernel Size)",
                Location = new Point(15, 10),
                Size = new Size(300, 55),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            rdb1x1 = new RadioButton
            {
                Text = "1 x 1 (無濾波)",
                Location = new Point(15, 22),
                Size = new Size(95, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Tag = 1
            };
            rdb1x1.CheckedChanged += KernelSize_CheckedChanged;

            rdb3x3 = new RadioButton
            {
                Text = "3 x 3 (預設)",
                Location = new Point(115, 22),
                Size = new Size(90, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Checked = true,
                Tag = 3
            };
            rdb3x3.CheckedChanged += KernelSize_CheckedChanged;

            rdb5x5 = new RadioButton
            {
                Text = "5 x 5 (模糊)",
                Location = new Point(210, 22),
                Size = new Size(80, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Tag = 5
            };
            rdb5x5.CheckedChanged += KernelSize_CheckedChanged;

            grpKernel.Controls.AddRange(new Control[] { rdb1x1, rdb3x3, rdb5x5 });

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
                grpKernel,
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

        private void KernelSize_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton rdb = sender as RadioButton;
            if (rdb != null && rdb.Checked)
            {
                UpdateImage();
            }
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            isUpdating = true;
            try
            {
                rdb3x3.Checked = true;
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
                int kSize = GetSelectedKernelSize();
                string title = string.Format("中位數濾波 {0}x{0}", kSize);
                Bitmap outputBmp = processedBmp.Clone(new Rectangle(0, 0, processedBmp.Width, processedBmp.Height), processedBmp.PixelFormat);
                string paramText = string.Format("套用演算法: 中位數濾波 (Median Filter)\n核心大小: {0} x {1}", kSize, kSize);
                mainForm.ShowNewImage(outputBmp, title, paramText, ImageAlgorithmDescription);
            }
            this.Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private int GetSelectedKernelSize()
        {
            if (rdb1x1.Checked) return 1;
            if (rdb5x5.Checked) return 5;
            return 3;
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

            int kSize = GetSelectedKernelSize();

            unsafe
            {
                fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                {
                    DIPSample.median_filter(f0, w, h, d, g0, kSize);
                }
            }

            if (processedBmp != null) processedBmp.Dispose();
            processedBmp = DIPSample.dyn_array2bmp(gArray, w, h, d, pf, pal);

            if (pf1 != null)
            {
                pf1.Text = string.Format("中位數濾波調整: 核心大小={0}x{0}", kSize);
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
