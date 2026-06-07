using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace DIP
{
    public class CannyForm : Form
    {
        private DIPSample mainForm;
        private Bitmap originalBmp;
        private Bitmap processedBmp;

        internal ToolStripStatusLabel pf1;

        private PictureBox pictureBox1;
        private Panel panelBottom;

        private TrackBar trackBarLow;
        private TrackBar trackBarHigh;
        
        private Label lblLowTitle;
        private Label lblHighTitle;
        private Label lblLowValue;
        private Label lblHighValue;

        private Button btnReset;
        private Button btnOK;
        private Button btnCancel;

        private bool isUpdating = false;

        public Bitmap ProcessedBitmap
        {
            get { return (this.IsDisposed || this.Disposing) ? null : processedBmp; }
        }

        public CannyForm(DIPSample mainForm, Bitmap originalBmp)
        {
            this.mainForm = mainForm;
            this.originalBmp = originalBmp;

            InitializeUI();
            UpdateImage();
        }

        private void InitializeUI()
        {
            this.Text = "Canny 邊緣檢測預覽";

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
                Height = 140,
                Dock = DockStyle.Bottom,
                BackColor = SystemColors.Control,
                Padding = new Padding(10)
            };

            // 3. Low Threshold UI Controls
            lblLowTitle = new Label
            {
                Text = "低門檻值 (Low Thresh):",
                Location = new Point(15, 20),
                Size = new Size(160, 20),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            trackBarLow = new TrackBar
            {
                Minimum = 0,
                Maximum = 255,
                Value = 30, // Default Canny low threshold
                Location = new Point(180, 15),
                Width = 240,
                TickStyle = TickStyle.None
            };
            trackBarLow.ValueChanged += TrackBarLow_ValueChanged;

            lblLowValue = new Label
            {
                Text = "30",
                Location = new Point(430, 20),
                Size = new Size(40, 20),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.RoyalBlue,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 4. High Threshold UI Controls
            lblHighTitle = new Label
            {
                Text = "高門檻值 (High Thresh):",
                Location = new Point(15, 55),
                Size = new Size(160, 20),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            trackBarHigh = new TrackBar
            {
                Minimum = 0,
                Maximum = 255,
                Value = 90, // Default Canny high threshold
                Location = new Point(180, 50),
                Width = 240,
                TickStyle = TickStyle.None
            };
            trackBarHigh.ValueChanged += TrackBarHigh_ValueChanged;

            lblHighValue = new Label
            {
                Text = "90",
                Location = new Point(430, 55),
                Size = new Size(40, 20),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.Crimson,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 5. Action Buttons
            btnReset = new Button
            {
                Text = "重置預設值",
                Location = new Point(15, 95),
                Size = new Size(110, 28),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnReset.Click += BtnReset_Click;

            btnOK = new Button
            {
                Text = "確定",
                Location = new Point(340, 95),
                Size = new Size(80, 28),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(430, 95),
                Size = new Size(80, 28),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnCancel.Click += BtnCancel_Click;

            // Add controls to panel
            panelBottom.Controls.AddRange(new Control[] {
                lblLowTitle, trackBarLow, lblLowValue,
                lblHighTitle, trackBarHigh, lblHighValue,
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

        private void TrackBarLow_ValueChanged(object sender, EventArgs e)
        {
            if (isUpdating) return;
            isUpdating = true;
            try
            {
                if (trackBarLow.Value > trackBarHigh.Value)
                {
                    trackBarHigh.Value = trackBarLow.Value;
                }
                lblLowValue.Text = trackBarLow.Value.ToString();
                lblHighValue.Text = trackBarHigh.Value.ToString();
                UpdateImage();
            }
            finally
            {
                isUpdating = false;
            }
        }

        private void TrackBarHigh_ValueChanged(object sender, EventArgs e)
        {
            if (isUpdating) return;
            isUpdating = true;
            try
            {
                if (trackBarHigh.Value < trackBarLow.Value)
                {
                    trackBarLow.Value = trackBarHigh.Value;
                }
                lblLowValue.Text = trackBarLow.Value.ToString();
                lblHighValue.Text = trackBarHigh.Value.ToString();
                UpdateImage();
            }
            finally
            {
                isUpdating = false;
            }
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            isUpdating = true;
            try
            {
                trackBarLow.Value = 30;
                trackBarHigh.Value = 90;
                lblLowValue.Text = "30";
                lblHighValue.Text = "90";
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
                string title = string.Format("Canny 邊緣 (Low={0}, High={1})", trackBarLow.Value, trackBarHigh.Value);
                Bitmap outputBmp = processedBmp.Clone(new Rectangle(0, 0, processedBmp.Width, processedBmp.Height), processedBmp.PixelFormat);
                mainForm.ShowNewImage(outputBmp, title);
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

            double lowVal = trackBarLow.Value;
            double highVal = trackBarHigh.Value;

            unsafe
            {
                fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                {
                    DIPSample.detect_canny(f0, w, h, d, g0, lowVal, highVal);
                }
            }

            if (processedBmp != null) processedBmp.Dispose();
            processedBmp = DIPSample.dyn_array2bmp(gArray, w, h, d, pf, pal);

            if (pf1 != null)
            {
                pf1.Text = string.Format("Canny 邊緣檢測調整: Low={0}, High={1}", lowVal, highVal);
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
        }
    }
}
