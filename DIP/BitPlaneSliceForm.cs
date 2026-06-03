using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace DIP
{
    public class BitPlaneSliceForm : Form
    {
        private DIPSample mainForm;
        private Bitmap originalBmp;
        private Bitmap processedBmp;

        internal ToolStripStatusLabel pf1;

        private PictureBox pictureBox1;
        private Panel panelBottom;
        private TrackBar trackBarPlane;
        private Label lblPlaneValue;
        private CheckBox chkBinarize;

        public BitPlaneSliceForm(DIPSample mainForm, Bitmap originalBmp)
        {
            this.mainForm = mainForm;
            this.originalBmp = originalBmp;

            InitializeUI();
            UpdateSlice();
        }

        private void InitializeUI()
        {
            this.Text = "位元平面預覽 (Consolidated Bit-Plane Slicing)";
            
            // Adjust window client size based on image plus the bottom panel
            int initialWidth = Math.Max(originalBmp.Width, 400);
            int initialHeight = originalBmp.Height + 85;
            
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
                Height = 85,
                Dock = DockStyle.Bottom,
                BackColor = SystemColors.Control,
                Padding = new Padding(10)
            };

            // 3. TrackBar (0 to 7)
            trackBarPlane = new TrackBar
            {
                Minimum = 0,
                Maximum = 7,
                Value = 0, // Default to 0 for steganography B0 testing
                Location = new Point(15, 10),
                Width = 260,
                TickStyle = TickStyle.BottomRight,
                TickFrequency = 1
            };
            trackBarPlane.ValueChanged += (s, e) =>
            {
                lblPlaneValue.Text = "b" + trackBarPlane.Value;
                UpdateSlice();
            };

            // 4. Label showing active plane
            lblPlaneValue = new Label
            {
                Text = "b0",
                Location = new Point(285, 12),
                Size = new Size(40, 25),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.RoyalBlue
            };

            // 5. CheckBox for binarization selection
            chkBinarize = new CheckBox
            {
                Text = "二值化放大 (Binarize for Visualization)",
                Location = new Point(20, 50),
                Size = new Size(320, 24),
                Checked = false, // Default: raw weights
                ForeColor = Color.Black
            };
            chkBinarize.CheckedChanged += (s, e) => UpdateSlice();

            panelBottom.Controls.AddRange(new Control[] { trackBarPlane, lblPlaneValue, chkBinarize });
            this.Controls.Add(pictureBox1);
            this.Controls.Add(panelBottom);

            // Double buffering to prevent flicker
            this.DoubleBuffered = true;
        }

        private void UpdateSlice()
        {
            if (originalBmp == null) return;

            int w = originalBmp.Width;
            int h = originalBmp.Height;
            int d = 0;
            PixelFormat pf = new PixelFormat();
            ColorPalette pal = null;

            // Convert original Bitmap to array
            int[] fArray = mainForm.dyn_bmp2array(originalBmp, ref d, ref pf, ref pal);
            int[] gArray = new int[w * h * d];

            int plane = trackBarPlane.Value;
            int binarize = chkBinarize.Checked ? 1 : 0;

            unsafe
            {
                fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                {
                    DIPSample.bit_plane_slice(f0, w, h, d, g0, plane, binarize);
                }
            }

            // Convert processed array back to Bitmap
            if (processedBmp != null)
            {
                processedBmp.Dispose();
            }
            processedBmp = DIPSample.dyn_array2bmp(gArray, w, h, d, pf, pal);
            pictureBox1.Image = processedBmp;

            // Update status bar text if set
            if (pf1 != null)
            {
                pf1.Text = "位元平面: b" + plane + (binarize == 1 ? " (二值化)" : " (原始權重)");
            }

            // Trigger histogram update
            mainForm.UpdateHistogram();
        }

        private void PictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (processedBmp == null || pf1 == null) return;

            // Map coordinates because SizeMode = CenterImage
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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (processedBmp != null)
            {
                processedBmp.Dispose();
            }
            base.OnFormClosed(e);
            
            // Refresh main window histogram stats
            mainForm.UpdateHistogram();
        }
    }
}
