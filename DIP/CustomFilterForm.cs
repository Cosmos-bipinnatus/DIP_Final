using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace DIP
{
    public class CustomFilterForm : Form
    {
        private DIPSample mainForm;
        private Bitmap originalBmp;
        private Bitmap processedBmp;

        internal ToolStripStatusLabel pf1;

        private PictureBox pictureBox1;
        private Panel panelBottom;

        private RadioButton rdb3x3;
        private RadioButton rdb5x5;
        private GroupBox grpSize;

        private TextBox[,] txtGrid = new TextBox[5, 5];
        private TextBox txtDivisor;
        private TextBox txtOffset;

        private Label lblDivisor;
        private Label lblOffset;

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
                int kSize = rdb3x3.Checked ? 3 : 5;
                double divisor = 1.0;
                double.TryParse(txtDivisor.Text, out divisor);
                double offset = 0.0;
                double.TryParse(txtOffset.Text, out offset);
                
                string kernelStr = "";
                if (kSize == 3)
                {
                    kernelStr = string.Format(
                        "[  {0}  {1}  {2}  ]\n" +
                        "[  {3}  {4}  {5}  ]\n" +
                        "[  {6}  {7}  {8}  ]",
                        txtGrid[1, 1].Text, txtGrid[1, 2].Text, txtGrid[1, 3].Text,
                        txtGrid[2, 1].Text, txtGrid[2, 2].Text, txtGrid[2, 3].Text,
                        txtGrid[3, 1].Text, txtGrid[3, 2].Text, txtGrid[3, 3].Text
                    );
                }
                else
                {
                    kernelStr = string.Format(
                        "[  {0}  {1}  {2}  {3}  {4}  ]\n" +
                        "[  {5}  {6}  {7}  {8}  {9}  ]\n" +
                        "[  {10}  {11}  {12}  {13}  {14}  ]\n" +
                        "[  {15}  {16}  {17}  {18}  {19}  ]\n" +
                        "[  {20}  {21}  {22}  {23}  {24}  ]",
                        txtGrid[0, 0].Text, txtGrid[0, 1].Text, txtGrid[0, 2].Text, txtGrid[0, 3].Text, txtGrid[0, 4].Text,
                        txtGrid[1, 0].Text, txtGrid[1, 1].Text, txtGrid[1, 2].Text, txtGrid[1, 3].Text, txtGrid[1, 4].Text,
                        txtGrid[2, 0].Text, txtGrid[2, 1].Text, txtGrid[2, 2].Text, txtGrid[2, 3].Text, txtGrid[2, 4].Text,
                        txtGrid[3, 0].Text, txtGrid[3, 1].Text, txtGrid[3, 2].Text, txtGrid[3, 3].Text, txtGrid[3, 4].Text,
                        txtGrid[4, 0].Text, txtGrid[4, 1].Text, txtGrid[4, 2].Text, txtGrid[4, 3].Text, txtGrid[4, 4].Text
                    );
                }
                return string.Format("[即時預覽調整中]\n核心大小: {0} x {1}\n除數 (Divisor): {2}\n偏移量 (Offset): {3}\n核心權重:\n{4}", 
                    kSize, kSize, divisor, offset, kernelStr);
            }
        }

        public string ImageAlgorithmDescription
        {
            get
            {
                return "自訂空間濾波器允許使用者手動指定 3x3 或 5x5 的卷積核心係數，並搭配除數 (Divisor) 與偏移量 (Offset)。影像運算時，核心矩陣以滑動視窗方式與每個像素鄰域相乘求和，除以除數並加上偏移量後輸出。透過自訂係數，使用者可自由實現均值模糊、邊緣提取、浮雕、高頻增強等各式不同的卷積效果。";
            }
        }

        public CustomFilterForm(DIPSample mainForm, Bitmap originalBmp)
        {
            this.mainForm = mainForm;
            this.originalBmp = originalBmp;

            InitializeUI();
            ResetToDefaultValues();
            UpdateImage();
        }

        private void InitializeUI()
        {
            this.Text = "自訂空間濾波器預覽 (Custom Spatial Filter Preview)";

            int initialWidth = Math.Max(originalBmp.Width, 580);
            int initialHeight = originalBmp.Height + 290; // Expanded to accommodate the 280px panel

            this.ClientSize = new Size(initialWidth, initialHeight);
            this.MinimumSize = new Size(580, 340);
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
                Height = 250,
                Dock = DockStyle.Bottom,
                BackColor = SystemColors.Control,
                Padding = new Padding(10)
            };

            // 3. Left Controls (Size & Divisor / Offset)
            grpSize = new GroupBox
            {
                Text = "核心大小 (Size)",
                Location = new Point(15, 10),
                Size = new Size(150, 75),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            rdb3x3 = new RadioButton
            {
                Text = "3 x 3 核心",
                Location = new Point(15, 20),
                Size = new Size(120, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Checked = true
            };
            rdb3x3.CheckedChanged += Size_CheckedChanged;

            rdb5x5 = new RadioButton
            {
                Text = "5 x 5 核心",
                Location = new Point(15, 45),
                Size = new Size(120, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            rdb5x5.CheckedChanged += Size_CheckedChanged;

            grpSize.Controls.AddRange(new Control[] { rdb3x3, rdb5x5 });

            lblDivisor = new Label
            {
                Text = "除數 (Divisor):",
                Location = new Point(15, 100),
                Size = new Size(150, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            txtDivisor = new TextBox
            {
                Text = "1.0",
                Location = new Point(15, 120),
                Size = new Size(150, 23),
                TextAlign = HorizontalAlignment.Center
            };
            txtDivisor.LostFocus += TextBox_LostFocus;
            txtDivisor.KeyDown += TextBox_KeyDown;

            lblOffset = new Label
            {
                Text = "偏移量 (Offset):",
                Location = new Point(15, 155),
                Size = new Size(150, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            txtOffset = new TextBox
            {
                Text = "0.0",
                Location = new Point(15, 175),
                Size = new Size(150, 23),
                TextAlign = HorizontalAlignment.Center
            };
            txtOffset.LostFocus += TextBox_LostFocus;
            txtOffset.KeyDown += TextBox_KeyDown;

            // 4. Middle Grid (5x5 TextBox grid)
            int cellW = 38;
            int cellH = 22;
            int spacing = 5;
            int gridStartX = 190;
            int gridStartY = 20;

            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    txtGrid[r, c] = new TextBox
                    {
                        Size = new Size(cellW, cellH),
                        Location = new Point(gridStartX + c * (cellW + spacing), gridStartY + r * (cellH + spacing)),
                        Text = "0",
                        TextAlign = HorizontalAlignment.Center
                    };
                    txtGrid[r, c].LostFocus += TextBox_LostFocus;
                    txtGrid[r, c].KeyDown += TextBox_KeyDown;
                    panelBottom.Controls.Add(txtGrid[r, c]);
                }
            }

            // 5. Right Action Buttons
            btnReset = new Button
            {
                Text = "重置預設值",
                Location = new Point(430, 25),
                Size = new Size(120, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnReset.Click += BtnReset_Click;

            btnOK = new Button
            {
                Text = "確定",
                Location = new Point(430, 75),
                Size = new Size(120, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(430, 125),
                Size = new Size(120, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnCancel.Click += BtnCancel_Click;

            // Add remaining controls to bottom panel
            panelBottom.Controls.AddRange(new Control[] {
                grpSize, lblDivisor, txtDivisor, lblOffset, txtOffset,
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

        private void Size_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton rdb = sender as RadioButton;
            if (rdb != null && rdb.Checked)
            {
                int size = rdb3x3.Checked ? 3 : 5;
                SwitchKernelSize(size);
                UpdateImage();
            }
        }

        private void SwitchKernelSize(int size)
        {
            isUpdating = true;
            try
            {
                for (int r = 0; r < 5; r++)
                {
                    for (int c = 0; c < 5; c++)
                    {
                        if (size == 3)
                        {
                            // Outer border (row 0, 4 and col 0, 4) disabled in 3x3 mode
                            if (r == 0 || r == 4 || c == 0 || c == 4)
                            {
                                txtGrid[r, c].ReadOnly = true;
                                txtGrid[r, c].BackColor = SystemColors.InactiveBorder;
                                txtGrid[r, c].Text = "0";
                            }
                            else
                            {
                                txtGrid[r, c].ReadOnly = false;
                                txtGrid[r, c].BackColor = SystemColors.Window;
                            }
                        }
                        else // 5x5 mode
                        {
                            txtGrid[r, c].ReadOnly = false;
                            txtGrid[r, c].BackColor = SystemColors.Window;
                        }
                    }
                }
            }
            finally
            {
                isUpdating = false;
            }
        }

        private void TextBox_LostFocus(object sender, EventArgs e)
        {
            if (isUpdating) return;
            UpdateImage();
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true; // Prevent beep sound
                UpdateImage();
            }
        }

        private void ResetToDefaultValues()
        {
            isUpdating = true;
            try
            {
                rdb3x3.Checked = true;
                txtDivisor.Text = "1.0";
                txtOffset.Text = "0.0";

                for (int r = 0; r < 5; r++)
                {
                    for (int c = 0; c < 5; c++)
                    {
                        // Identity 3x3 filter as default (center cell is 1.0, others are 0.0)
                        if (r == 2 && c == 2)
                        {
                            txtGrid[r, c].Text = "1.0";
                        }
                        else
                        {
                            txtGrid[r, c].Text = "0.0";
                        }
                    }
                }
                SwitchKernelSize(3);
            }
            finally
            {
                isUpdating = false;
            }
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            ResetToDefaultValues();
            UpdateImage();
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            if (processedBmp != null)
            {
                int kSize = rdb3x3.Checked ? 3 : 5;
                string title = string.Format("自訂 {0}x{0} 濾波器", kSize);
                Bitmap outputBmp = processedBmp.Clone(new Rectangle(0, 0, processedBmp.Width, processedBmp.Height), processedBmp.PixelFormat);
                string paramText = ImageInfoParameters.Replace("[即時預覽調整中]", "套用演算法: 自訂空間濾波器 (Custom Filter)");
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

            int kSize = rdb3x3.Checked ? 3 : 5;
            double[] kernel = new double[kSize * kSize];
            double divisor = 1.0;
            double offset = 0.0;

            // Safe parsing of user input values
            if (!double.TryParse(txtDivisor.Text, out divisor) || divisor == 0.0)
            {
                if (pf1 != null) pf1.Text = "錯誤: 除數必須是有效的非零數字！";
                return;
            }

            if (!double.TryParse(txtOffset.Text, out offset))
            {
                if (pf1 != null) pf1.Text = "錯誤: 偏移量必須是有效的數字！";
                return;
            }

            if (kSize == 3)
            {
                int idx = 0;
                for (int r = 1; r <= 3; r++)
                {
                    for (int c = 1; c <= 3; c++)
                    {
                        if (!double.TryParse(txtGrid[r, c].Text, out kernel[idx++]))
                        {
                            if (pf1 != null) pf1.Text = string.Format("錯誤: 核心矩陣第 {0} 行第 {1} 列輸入無效！", r, c);
                            return;
                        }
                    }
                }
            }
            else // 5x5
            {
                int idx = 0;
                for (int r = 0; r < 5; r++)
                {
                    for (int c = 0; c < 5; c++)
                    {
                        if (!double.TryParse(txtGrid[r, c].Text, out kernel[idx++]))
                        {
                            if (pf1 != null) pf1.Text = string.Format("錯誤: 核心矩陣第 {0} 行第 {1} 列輸入無效！", r + 1, c + 1);
                            return;
                        }
                    }
                }
            }

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
                pf1.Text = string.Format("自訂濾波器調整: 核心大小={0}x{0}, 除數={1}, 偏移={2}", kSize, divisor, offset);
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
