using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DIP
{
    public partial class DIPSample : Form
    {
        // ==========================================
        // C++ DLL P/Invoke Declarations
        // ==========================================
        private const string DllName = "dip_proc.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void encode_gray(int* f, int w, int h, int d, int* g);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void bit_plane_slice(int* f, int w, int h, int d, int* g, int plane);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void adjust_brightness_contrast(int* f, int w, int h, int d, int* g, double alpha, int beta);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void calculate_histogram(int* f, int w, int h, int d, int* histB, int* histG, int* histR);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void histogram_equalization(int* f, int w, int h, int d, int* g);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void spatial_filter(int* f, int w, int h, int d, int* g, double[] kernel, int kSize, double divisor, double offset);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void scale_image(int* f, int w, int h, int d, int* g, int newW, int newH, int mode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void rotate_image(int* f, int w, int h, int d, int* g, int newW, int newH, double angle_deg, int mode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void manual_threshold(int* f, int w, int h, int d, int* g, int T);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void otsu_threshold(int* f, int w, int h, int d, int* g);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void detect_sobel(int* f, int w, int h, int d, int* g);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void detect_canny(int* f, int w, int h, int d, int* g, double lowThresh, double highThresh);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void detect_lines_hough(int* f, int w, int h, int d, int* g, int houghThreshold);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void detect_circles_hough(int* f, int w, int h, int d, int* g, int rMin, int rMax, int houghThreshold);

        // ==========================================
        // Sidebar UI Elements and State variables
        // ==========================================
        private Panel panelSidebar;
        private PictureBox picHistB;
        private PictureBox picHistG;
        private PictureBox picHistR;
        private Label lblSidebarTitle;
        private Label lblStats;
        private int[] currentHistDataB = new int[256];
        private int[] currentHistDataG = new int[256];
        private int[] currentHistDataR = new int[256];

        Bitmap NpBitmap;
        int w, h;

        public DIPSample()
        {
            InitializeComponent();
        }

        private void DIPSample_Load(object sender, EventArgs e)
        {
            this.IsMdiContainer = true;
            this.WindowState = FormWindowState.Maximized;
            this.stStripLabel.Text = "就緒 (Ready)";

            InitializeSidebar();
            RegisterEvents();
        }

        private void InitializeSidebar()
        {
            this.panelSidebar = new Panel();
            this.picHistB = new PictureBox();
            this.picHistG = new PictureBox();
            this.picHistR = new PictureBox();
            this.lblSidebarTitle = new Label();
            this.lblStats = new Label();

            // panelSidebar (Light system color)
            this.panelSidebar.Dock = DockStyle.Right;
            this.panelSidebar.Width = 280;
            this.panelSidebar.BackColor = SystemColors.Control;
            this.panelSidebar.Padding = new Padding(15);

            // lblSidebarTitle (Dark text)
            this.lblSidebarTitle.Text = "灰階直方圖 (Grayscale Histogram)";
            this.lblSidebarTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            this.lblSidebarTitle.ForeColor = Color.FromArgb(33, 37, 41);
            this.lblSidebarTitle.Dock = DockStyle.Top;
            this.lblSidebarTitle.Height = 35;

            // picHistB (Blue / Grayscale)
            this.picHistB.Dock = DockStyle.Top;
            this.picHistB.Height = 200;
            this.picHistB.BackColor = Color.White;
            this.picHistB.Paint += new PaintEventHandler(picHistogram_Paint);

            // picHistG (Green)
            this.picHistG.Dock = DockStyle.Top;
            this.picHistG.Height = 120;
            this.picHistG.BackColor = Color.White;
            this.picHistG.Visible = false;
            this.picHistG.Paint += new PaintEventHandler(picHistogram_Paint);

            // picHistR (Red)
            this.picHistR.Dock = DockStyle.Top;
            this.picHistR.Height = 120;
            this.picHistR.BackColor = Color.White;
            this.picHistR.Visible = false;
            this.picHistR.Paint += new PaintEventHandler(picHistogram_Paint);

            // lblStats (Dark text)
            this.lblStats.Dock = DockStyle.Fill;
            this.lblStats.Font = new Font("Segoe UI", 9F);
            this.lblStats.ForeColor = Color.FromArgb(50, 50, 50);
            this.lblStats.Padding = new Padding(0, 15, 0, 0);
            this.lblStats.Text = "沒有作用中的影像 (No active image)";

            // Add controls to panelSidebar (reverse addition order for docking layout)
            this.panelSidebar.Controls.Add(this.lblStats);
            this.panelSidebar.Controls.Add(this.picHistR);
            this.panelSidebar.Controls.Add(this.picHistG);
            this.panelSidebar.Controls.Add(this.picHistB);
            this.panelSidebar.Controls.Add(this.lblSidebarTitle);

            // Add sidebar to form
            this.Controls.Add(this.panelSidebar);

            // Hook MdiChildActivate to update histogram dynamically
            this.MdiChildActivate += new EventHandler(DIPSample_MdiChildActivate);
        }

        private void RegisterEvents()
        {
            // Register existing menu click events
            this.nearestNeighborInterpolationToolStripMenuItem.Click += (s, e) => ApplyScaling(0);
            this.bilinearInterpolationToolStripMenuItem.Click += (s, e) => ApplyScaling(1);
            this.rotationToolStripMenuItem.Click += (s, e) => ApplyRotation();
            this.otsusMethodToolStripMenuItem.Click += (s, e) => ApplyOtsu();
            this.bitPlanesToolStripMenuItem.Click += (s, e) => TriggerBitPlanes();
            this.averagingFilterToolStripMenuItem.Click += (s, e) => ApplyFilter(0); // Mean
            this.gaussianFiltersToolStripMenuItem.Click += (s, e) => ApplyFilter(1); // Gaussian

            // Dynamically find IP Menu and add Brightness & Contrast
            ToolStripMenuItem ipMenu = null;
            foreach (ToolStripItem item in this.menuStrip1.Items)
            {
                if (item.Text == "影像處理 (IP)" || item.Name == "iPToolStripMenuItem")
                {
                    ipMenu = item as ToolStripMenuItem;
                    break;
                }
            }
            if (ipMenu != null)
            {
                ToolStripMenuItem btnBC = new ToolStripMenuItem("亮度與對比 (Brightness and Contrast)");
                btnBC.Click += (s, e) => ApplyBrightnessContrast();
                ipMenu.DropDownItems.Add(btnBC);
            }

            // Dynamically add Laplacian, LoG, High Boost to Neighborhood menu
            if (this.neighborhoodProcessingToolStripMenuItem != null)
            {
                ToolStripMenuItem btnLap = new ToolStripMenuItem("拉普拉斯濾波 (8-Neighbors)");
                btnLap.Click += (s, e) => ApplyFilter(2);
                this.neighborhoodProcessingToolStripMenuItem.DropDownItems.Add(btnLap);

                ToolStripMenuItem btnLog = new ToolStripMenuItem("高斯-拉普拉斯 (LoG)");
                btnLog.Click += (s, e) => ApplyFilter(3);
                this.neighborhoodProcessingToolStripMenuItem.DropDownItems.Add(btnLog);

                ToolStripMenuItem btnHB = new ToolStripMenuItem("反銳化遮罩 / 高提升 (Unsharp Masking / High-Boost)");
                btnHB.Click += (s, e) => ApplyFilter(4);
                this.neighborhoodProcessingToolStripMenuItem.DropDownItems.Add(btnHB);
            }

            // Dynamically add Sobel and Canny to Edge Detection menu
            if (this.edgeDetectionToolStripMenuItem != null)
            {
                ToolStripMenuItem btnSobel = new ToolStripMenuItem("Sobel 算子 (Sobel Operator)");
                btnSobel.Click += (s, e) => ApplyEdge(0);
                this.edgeDetectionToolStripMenuItem.DropDownItems.Add(btnSobel);

                ToolStripMenuItem btnCanny = new ToolStripMenuItem("Canny 邊緣偵測 (Canny Edge Detector)");
                btnCanny.Click += (s, e) => ApplyEdge(1);
                this.edgeDetectionToolStripMenuItem.DropDownItems.Add(btnCanny);
            }

            // Dynamically create a new top-level menu for Hough Line/Circle Detection!
            ToolStripMenuItem houghMenu = new ToolStripMenuItem("霍夫偵測 (Hough Detection)");
            ToolStripMenuItem btnHoughLine = new ToolStripMenuItem("霍夫直線偵測 (Hough Line Detection)");
            btnHoughLine.Click += (s, e) => ApplyHoughLine();
            ToolStripMenuItem btnHoughCircle = new ToolStripMenuItem("霍夫圓形偵測 (Hough Circle Detection)");
            btnHoughCircle.Click += (s, e) => ApplyHoughCircle();

            houghMenu.DropDownItems.Add(btnHoughLine);
            houghMenu.DropDownItems.Add(btnHoughCircle);
            this.menuStrip1.Items.Add(houghMenu);

            // Wire the click event for Show Histogram
            this.showHistogramToolStripMenuItem.Click += (s, e) => {
                this.panelSidebar.Visible = !this.panelSidebar.Visible;
                UpdateHistogram();
            };

            // Grayscale equalization
            this.histogramEqualizationLinearToolStripMenuItem.Click += (s, e) => ApplyHistogramEqualization();
            // Gamma value equalisation trigger
            this.histogramEqualizationGammaValueToolStripMenuItem.Click += (s, e) => ApplyGammaCorrection();
            this.histogramEqualizationGammaValueToolStripMenuItem.Text = "Gamma 冪律轉換 (Gamma Power-Law Transform)";
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            oFileDlg.CheckFileExists = true;
            oFileDlg.CheckPathExists = true;
            oFileDlg.Title = "開啟檔案 (Open File) - DIP Sample";
            oFileDlg.ValidateNames = true;
            oFileDlg.Filter = "BMP 檔案 (*.bmp)|*.bmp";
            oFileDlg.FileName = "";

            if (oFileDlg.ShowDialog() == DialogResult.OK)
            {
                MSForm childForm = new MSForm();
                childForm.MdiParent = this;
                childForm.pf1 = stStripLabel;
                NpBitmap = bmp_read(oFileDlg);
                childForm.pBitmap = NpBitmap;
                w = NpBitmap.Width;
                h = NpBitmap.Height;
                childForm.Show();
                UpdateHistogram();
            }
        }

        private Bitmap bmp_read(OpenFileDialog oFileDlg)
        {
            string fileloc = oFileDlg.FileName;
            Bitmap pBitmap = new Bitmap(fileloc);
            return pBitmap;
        }

        private void fileToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        // ==========================================
        // Corrected Image conversions supporting Rectangular Images
        // ==========================================
        private int[] dyn_bmp2array(Bitmap myBitmap, ref int ByteDepth, ref PixelFormat pixelFormat, ref ColorPalette palette)
        {
            BitmapData byteArray = myBitmap.LockBits(new Rectangle(0, 0, myBitmap.Width, myBitmap.Height),
                                          ImageLockMode.ReadOnly,
                                          myBitmap.PixelFormat);
            pixelFormat = myBitmap.PixelFormat;
            palette = myBitmap.Palette;
            ByteDepth = Image.GetPixelFormatSize(myBitmap.PixelFormat) / 8;
            if (ByteDepth < 1) ByteDepth = 1;

            int Width = myBitmap.Width;
            int Height = myBitmap.Height;
            int[] ImgData = new int[Width * Height * ByteDepth];
            int ByteOfSkip = byteArray.Stride - Width * ByteDepth;

            unsafe
            {
                byte* imgPtr = (byte*)(byteArray.Scan0);
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        for (int c = 0; c < ByteDepth; c++)
                        {
                            ImgData[(y * Width + x) * ByteDepth + c] = (int)*(imgPtr + c);
                        }
                        imgPtr += ByteDepth;
                    }
                    imgPtr += ByteOfSkip;
                }
            }
            myBitmap.UnlockBits(byteArray);
            return ImgData;
        }

        private static Bitmap dyn_array2bmp(int[] ImgData, int Width, int Height, int ByteDepth, PixelFormat pixelFormat, ColorPalette palette)
        {
            Bitmap myBitmap = new Bitmap(Width, Height, pixelFormat);
            BitmapData byteArray = myBitmap.LockBits(new Rectangle(0, 0, Width, Height),
                                           ImageLockMode.WriteOnly,
                                           pixelFormat);
            try
            {
                myBitmap.Palette = palette;
            }
            catch { }

            int ByteOfSkip = byteArray.Stride - Width * ByteDepth;
            unsafe
            {
                byte* imgPtr = (byte*)byteArray.Scan0;
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        for (int c = 0; c < ByteDepth; c++)
                        {
                            *(imgPtr + c) = (byte)ImgData[(y * Width + x) * ByteDepth + c];
                        }
                        imgPtr += ByteDepth;
                    }
                    imgPtr += ByteOfSkip;
                }
            }
            myBitmap.UnlockBits(byteArray);
            return myBitmap;
        }

        // ==========================================
        // Sidebar Dynamic rendering
        // ==========================================
        private void DIPSample_MdiChildActivate(object sender, EventArgs e)
        {
            UpdateHistogram();
        }

        public void UpdateHistogram()
        {
            if (this.panelSidebar == null || !this.panelSidebar.Visible) return;

            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null || activeChild.pBitmap == null)
            {
                lblStats.Text = "沒有作用中的影像 (No active image)";
                Array.Clear(currentHistDataB, 0, 256);
                Array.Clear(currentHistDataG, 0, 256);
                Array.Clear(currentHistDataR, 0, 256);
                picHistB.Invalidate();
                picHistG.Invalidate();
                picHistR.Invalidate();
                return;
            }

            Bitmap bmp = activeChild.pBitmap;
            int tempW = bmp.Width;
            int tempH = bmp.Height;
            int d = 0;
            PixelFormat pf = new PixelFormat();
            ColorPalette pal = null;

            int[] f = dyn_bmp2array(bmp, ref d, ref pf, ref pal);

            int[] histB = new int[256];
            int[] histG = new int[256];
            int[] histR = new int[256];

            // Perform dynamic channel consistency check to verify if the 3 channels are identical (real grayscale)
            bool isActuallyGray = (d == 1);
            if (d == 3)
            {
                isActuallyGray = true;
                for (int i = 0; i < f.Length; i += 3)
                {
                    if (f[i] != f[i + 1] || f[i + 1] != f[i + 2])
                    {
                        isActuallyGray = false;
                        break;
                    }
                }
            }

            if (!isActuallyGray)
            {
                lblSidebarTitle.Text = "BGR 直方圖 (BGR Histograms)";
                picHistB.Height = 120;
                picHistG.Visible = true;
                picHistR.Visible = true;
            }
            else
            {
                lblSidebarTitle.Text = "灰階直方圖 (Grayscale Histogram)";
                picHistB.Height = 200;
                picHistG.Visible = false;
                picHistR.Visible = false;
            }

            unsafe
            {
                fixed (int* f0 = f) fixed (int* hB = histB) fixed (int* hG = histG) fixed (int* hR = histR)
                {
                    calculate_histogram(f0, tempW, tempH, d, hB, hG, hR);
                }
            }

            Array.Copy(histB, currentHistDataB, 256);
            Array.Copy(histG, currentHistDataG, 256);
            Array.Copy(histR, currentHistDataR, 256);

            // Statistics (exact grayscale representation)
            double sum = 0;
            long total = tempW * tempH;
            double mean = 0;
            double stdDev = 0;
            int median = 127;

            if (isActuallyGray)
            {
                for (int i = 0; i < 256; i++) sum += (double)histB[i] * i;
                mean = sum / total;

                double varSum = 0;
                for (int i = 0; i < 256; i++) varSum += (double)histB[i] * Math.Pow(i - mean, 2);
                stdDev = Math.Sqrt(varSum / total);

                long cum = 0;
                long half = total / 2;
                for (int i = 0; i < 256; i++)
                {
                    cum += histB[i];
                    if (cum >= half) { median = i; break; }
                }
            }
            else // d == 3 (Color BGR)
            {
                // Calculate grayscale values in C# to get accurate stats
                int[] histY = new int[256];
                for (int y = 0; y < tempH; y++)
                {
                    for (int x = 0; x < tempW; x++)
                    {
                        int idx = (y * tempW + x) * 3;
                        int b = f[idx + 0];
                        int g_val = f[idx + 1];
                        int r = f[idx + 2];
                        int gray = (int)(r * 0.299 + g_val * 0.587 + b * 0.114);
                        if (gray >= 0 && gray <= 255) histY[gray]++;
                    }
                }

                for (int i = 0; i < 256; i++) sum += (double)histY[i] * i;
                mean = sum / total;

                double varSum = 0;
                for (int i = 0; i < 256; i++) varSum += (double)histY[i] * Math.Pow(i - mean, 2);
                stdDev = Math.Sqrt(varSum / total);

                long cum = 0;
                long half = total / 2;
                for (int i = 0; i < 256; i++)
                {
                    cum += histY[i];
                    if (cum >= half) { median = i; break; }
                }
            }

            lblStats.Text = string.Format(
                "影像尺寸 (Image Size): {0} x {1}\n" +
                "格式 (Format): {2}\n" +
                "總像素 (Total Pixels): {3:N0}\n\n" +
                "--- 統計 (Statistics) ---\n" +
                "平均亮度 (Mean Intensity): {4:F2}\n" +
                "中位亮度 (Median Intensity): {5}\n" +
                "標準差 (Std Deviation): {6:F2}",
                tempW, tempH, pf.ToString(), total, mean, median, stdDev
            );

            picHistB.Invalidate();
            if (!isActuallyGray)
            {
                picHistG.Invalidate();
                picHistR.Invalidate();
            }
        }

        private void picHistogram_Paint(object sender, PaintEventArgs e)
        {
            PictureBox pic = sender as PictureBox;
            if (pic == null) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int width = pic.Width;
            int height = pic.Height;

            int[] data = null;
            Color drawColor = Color.DimGray;
            string channelName = "";

            if (pic == picHistB)
            {
                data = currentHistDataB;
                if (picHistG != null && picHistG.Visible)
                {
                    drawColor = Color.FromArgb(66, 165, 245); // Soft blue
                    channelName = "Blue Channel";
                }
                else
                {
                    drawColor = Color.FromArgb(120, 120, 120); // Grayscale (neutral dark gray)
                    channelName = "Grayscale";
                }
            }
            else if (pic == picHistG)
            {
                data = currentHistDataG;
                drawColor = Color.FromArgb(102, 187, 106); // Soft green
                channelName = "Green Channel";
            }
            else if (pic == picHistR)
            {
                data = currentHistDataR;
                drawColor = Color.FromArgb(239, 83, 80); // Soft red
                channelName = "Red Channel";
            }

            int maxVal = 0;
            if (data != null)
            {
                for (int i = 0; i < 256; i++)
                {
                    if (data[i] > maxVal) maxVal = data[i];
                }
            }

            g.Clear(Color.White);

            if (maxVal == 0)
            {
                return;
            }

            // Grid lines (light gray)
            using (Pen gridPen = new Pen(Color.FromArgb(230, 230, 235), 1))
            {
                for (int i = 1; i < 4; i++)
                {
                    int x = i * width / 4;
                    g.DrawLine(gridPen, x, 0, x, height);
                    int y = i * height / 4;
                    g.DrawLine(gridPen, 0, y, width, y);
                }
            }

            // Draw solid histogram curve/fill
            using (SolidBrush brush = new SolidBrush(drawColor))
            {
                System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath();
                path.StartFigure();
                path.AddLine(0, height, 0, height);

                for (int i = 0; i < 256; i++)
                {
                    float x = (float)i / 255 * (width - 2);
                    float y = height - ((float)data[i] / maxVal * (height - 10));
                    path.AddLine(x, y, x, y);
                }

                path.AddLine(width, height, 0, height);
                path.CloseFigure();

                g.FillPath(brush, path);
            }

            // Draw channel text indicator in the corner
            using (Font textFont = new Font("Segoe UI", 9F, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(150, 60, 60, 60)))
            {
                g.DrawString(channelName, textFont, textBrush, new PointF(10, 8));
            }
        }

        // ==========================================
        // Event click handling helper methods
        // ==========================================
        private void ShowNewImage(Bitmap bmp, string title)
        {
            MSForm childForm = new MSForm();
            childForm.MdiParent = this;
            childForm.pf1 = stStripLabel;
            childForm.pBitmap = bmp;
            childForm.Text = title;
            childForm.Show();
            UpdateHistogram();
        }

        private void RGBtoGrayToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            Bitmap bmp = activeChild.pBitmap;
            int tempW = bmp.Width;
            int tempH = bmp.Height;
            int d = 0;
            PixelFormat pf = new PixelFormat();
            ColorPalette pal = null;

            int[] fArray = dyn_bmp2array(bmp, ref d, ref pf, ref pal);
            int[] gArray = new int[tempW * tempH * d];

            unsafe
            {
                fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                {
                    encode_gray(f0, tempW, tempH, d, g0);
                }
            }

            Bitmap grayBmp = dyn_array2bmp(gArray, tempW, tempH, d, pf, pal);
            ShowNewImage(grayBmp, "灰階影像 (Grayscale Image)");
        }

        private void TriggerBitPlanes()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;
            ParamDialog.ShowBitPlaneSlider(this, activeChild.pBitmap);
        }

        private MSForm bitPlaneSliceForm = null;
        public void ApplyBitPlaneSlice(Bitmap originalBmp, int plane)
        {
            int tempW = originalBmp.Width;
            int tempH = originalBmp.Height;
            int d = 0;
            PixelFormat pf = new PixelFormat();
            ColorPalette pal = null;
            int[] fArray = dyn_bmp2array(originalBmp, ref d, ref pf, ref pal);
            int[] gArray = new int[tempW * tempH * d];

            unsafe
            {
                fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                {
                    bit_plane_slice(f0, tempW, tempH, d, g0, plane);
                }
            }

            Bitmap bitPlaneBmp = dyn_array2bmp(gArray, tempW, tempH, d, pf, pal);

            if (bitPlaneSliceForm == null || bitPlaneSliceForm.IsDisposed)
            {
                bitPlaneSliceForm = new MSForm();
                bitPlaneSliceForm.MdiParent = this;
                bitPlaneSliceForm.pf1 = stStripLabel;
                bitPlaneSliceForm.Text = "位元平面預覽 (Bit-Plane Preview)";
            }

            bitPlaneSliceForm.pBitmap = bitPlaneBmp;
            bitPlaneSliceForm.Show();
            bitPlaneSliceForm.Invalidate();
            UpdateHistogram();
        }

        private void ApplyBrightnessContrast()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            double alpha;
            int beta;
            if (ParamDialog.ShowBrightnessContrastDialog(out alpha, out beta))
            {
                Bitmap bmp = activeChild.pBitmap;
                int tempW = bmp.Width;
                int tempH = bmp.Height;
                int d = 0;
                PixelFormat pf = new PixelFormat();
                ColorPalette pal = null;
                int[] fArray = dyn_bmp2array(bmp, ref d, ref pf, ref pal);
                int[] gArray = new int[tempW * tempH * d];

                unsafe
                {
                    fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                    {
                        adjust_brightness_contrast(f0, tempW, tempH, d, g0, alpha, beta);
                    }
                }

                Bitmap newBmp = dyn_array2bmp(gArray, tempW, tempH, d, pf, pal);
                ShowNewImage(newBmp, string.Format("亮度/對比 (B&C, a={0:F1}, b={1})", alpha, beta));
            }
        }

        private void ApplyGammaCorrection()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            int gammaVal = 10;
            if (ParamDialog.ShowSliderDialog("Gamma 校正 (Gamma Correction)", "輸入 Gamma 比例 (Enter Gamma scale, 1 to 30, representing 0.1 to 3.0):", 1, 30, 10, out gammaVal))
            {
                double gamma = (double)gammaVal / 10.0;
                Bitmap bmp = activeChild.pBitmap;
                int tempW = bmp.Width;
                int tempH = bmp.Height;
                int d = 0;
                PixelFormat pf = new PixelFormat();
                ColorPalette pal = null;
                int[] fArray = dyn_bmp2array(bmp, ref d, ref pf, ref pal);
                int[] gArray = new int[tempW * tempH * d];

                unsafe
                {
                    fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                    {
                        // Signal C++ to use Gamma by passing negative alpha
                        adjust_brightness_contrast(f0, tempW, tempH, d, g0, -gamma, 0);
                    }
                }

                Bitmap newBmp = dyn_array2bmp(gArray, tempW, tempH, d, pf, pal);
                ShowNewImage(newBmp, string.Format("Gamma 校正 (Gamma Correction, g={0:F1})", gamma));
            }
        }

        private void ApplyHistogramEqualization()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            Bitmap bmp = activeChild.pBitmap;
            int tempW = bmp.Width;
            int tempH = bmp.Height;
            int d = 0;
            PixelFormat pf = new PixelFormat();
            ColorPalette pal = null;
            int[] fArray = dyn_bmp2array(bmp, ref d, ref pf, ref pal);
            int[] gArray = new int[tempW * tempH * d];

            unsafe
            {
                fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                {
                    histogram_equalization(f0, tempW, tempH, d, g0);
                }
            }

            Bitmap newBmp = dyn_array2bmp(gArray, tempW, tempH, d, pf, pal);
            ShowNewImage(newBmp, "直方圖等化 (Histogram Equalized)");
        }

        private void ApplyFilter(int filterType)
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            Bitmap bmp = activeChild.pBitmap;
            int tempW = bmp.Width;
            int tempH = bmp.Height;
            int d = 0;
            PixelFormat pf = new PixelFormat();
            ColorPalette pal = null;
            int[] fArray = dyn_bmp2array(bmp, ref d, ref pf, ref pal);
            int[] gArray = new int[tempW * tempH * d];

            double[] kernel = null;
            int kSize = 3;
            double divisor = 1.0;
            double offset = 0.0;
            string filterName = "";

            if (filterType == 0) // Mean Filter 3x3
            {
                kernel = new double[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 };
                divisor = 9.0;
                filterName = "平均濾波 3x3 (Mean Filter)";
            }
            else if (filterType == 1) // Gaussian Filter 3x3
            {
                kernel = new double[] { 1, 2, 1, 2, 4, 2, 1, 2, 1 };
                divisor = 16.0;
                filterName = "高斯濾波 3x3 (Gaussian Filter)";
            }
            else if (filterType == 2) // Laplacian Filter 3x3 (8-Neighbors)
            {
                kernel = new double[] { -1, -1, -1, -1, 8, -1, -1, -1, -1 };
                divisor = 1.0;
                filterName = "拉普拉斯銳化 (Laplacian Sharpening)";
            }
            else if (filterType == 3) // LoG Filter 5x5
            {
                kernel = new double[] {
                     0,  0, -1,  0,  0,
                     0, -1, -2, -1,  0,
                    -1, -2, 16, -2, -1,
                     0, -1, -2, -1,  0,
                     0,  0, -1,  0,  0
                };
                kSize = 5;
                divisor = 1.0;
                offset = 128.0;
                filterName = "LoG 濾波 5x5 (LoG Filter)";
            }
            else if (filterType == 4) // High-Boost filter
            {
                int weightVal = 12;
                if (ParamDialog.ShowSliderDialog("高提升濾波 (High-Boost Filter)", "輸入 A 係數 (Enter scale A, 10 to 30, representing 1.0 to 3.0):", 10, 30, 12, out weightVal))
                {
                    double A = (double)weightVal / 10.0;
                    double center = 8.0 + (A - 1.0) * 9.0;
                    kernel = new double[] { -1, -1, -1, -1, center, -1, -1, -1, -1 };
                    divisor = 1.0;
                    filterName = string.Format("高提升濾波 (High-Boost, A={0:F1})", A);
                }
                else return;
            }

            unsafe
            {
                fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                {
                    spatial_filter(f0, tempW, tempH, d, g0, kernel, kSize, divisor, offset);
                }
            }

            Bitmap newBmp = dyn_array2bmp(gArray, tempW, tempH, d, pf, pal);
            ShowNewImage(newBmp, filterName);
        }

        private void ApplyScaling(int mode)
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            int scalePercent = 200;
            if (ParamDialog.ShowSliderDialog("影像縮放 (Image Scaling)", "輸入縮放百分比 (Enter scaling percentage, 10% to 500%):", 10, 500, 200, out scalePercent))
            {
                double scale = (double)scalePercent / 100.0;
                Bitmap bmp = activeChild.pBitmap;
                int tempW = bmp.Width;
                int tempH = bmp.Height;
                int d = 0;
                PixelFormat pf = new PixelFormat();
                ColorPalette pal = null;
                int[] fArray = dyn_bmp2array(bmp, ref d, ref pf, ref pal);

                int newW = (int)(tempW * scale);
                int newH = (int)(tempH * scale);
                if (newW < 2) newW = 2;
                if (newH < 2) newH = 2;

                int[] gArray = new int[newW * newH * d];

                unsafe
                {
                    fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                    {
                        scale_image(f0, tempW, tempH, d, g0, newW, newH, mode);
                    }
                }

                Bitmap newBmp = dyn_array2bmp(gArray, newW, newH, d, pf, pal);
                ShowNewImage(newBmp, string.Format("縮放 {0}% ({1})", scalePercent, mode == 0 ? "最近鄰 (Nearest)" : "雙線性 (Bilinear)"));
            }
        }

        private void ApplyRotation()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            double angle;
            int mode;
            if (ParamDialog.ShowRotationDialog(out angle, out mode))
            {
                Bitmap bmp = activeChild.pBitmap;
                int tempW = bmp.Width;
                int tempH = bmp.Height;
                int d = 0;
                PixelFormat pf = new PixelFormat();
                ColorPalette pal = null;
                int[] fArray = dyn_bmp2array(bmp, ref d, ref pf, ref pal);

                // Compute bounding box dimensions to prevent clipping
                double rad = Math.Abs(angle * Math.PI / 180.0);
                int newW = (int)Math.Ceiling(tempW * Math.Cos(rad) + tempH * Math.Sin(rad));
                int newH = (int)Math.Ceiling(tempW * Math.Sin(rad) + tempH * Math.Cos(rad));
                if (newW < 2) newW = 2;
                if (newH < 2) newH = 2;

                int[] gArray = new int[newW * newH * d];

                unsafe
                {
                    fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                    {
                        rotate_image(f0, tempW, tempH, d, g0, newW, newH, angle, mode);
                    }
                }

                Bitmap newBmp = dyn_array2bmp(gArray, newW, newH, d, pf, pal);
                ShowNewImage(newBmp, string.Format("旋轉 {0}° ({1})", angle, mode == 0 ? "最近鄰 (Nearest)" : "雙線性 (Bilinear)"));
            }
        }

        private void ApplyOtsu()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            Bitmap bmp = activeChild.pBitmap;
            int tempW = bmp.Width;
            int tempH = bmp.Height;
            int d = 0;
            PixelFormat pf = new PixelFormat();
            ColorPalette pal = null;
            int[] fArray = dyn_bmp2array(bmp, ref d, ref pf, ref pal);
            int[] gArray = new int[tempW * tempH * d];

            unsafe
            {
                fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                {
                    otsu_threshold(f0, tempW, tempH, d, g0);
                }
            }

            Bitmap newBmp = dyn_array2bmp(gArray, tempW, tempH, d, pf, pal);
            ShowNewImage(newBmp, "大津法二值化 (Otsu Threshold Binarized)");
        }

        private void ApplyManualThreshold()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            int T = 128;
            if (ParamDialog.ShowSliderDialog("手動門檻 (Manual Threshold)", "輸入門檻值 (Enter threshold value, 0 to 255):", 0, 255, 128, out T))
            {
                Bitmap bmp = activeChild.pBitmap;
                int tempW = bmp.Width;
                int tempH = bmp.Height;
                int d = 0;
                PixelFormat pf = new PixelFormat();
                ColorPalette pal = null;
                int[] fArray = dyn_bmp2array(bmp, ref d, ref pf, ref pal);
                int[] gArray = new int[tempW * tempH * d];

                unsafe
                {
                    fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                    {
                        manual_threshold(f0, tempW, tempH, d, g0, T);
                    }
                }

                Bitmap newBmp = dyn_array2bmp(gArray, tempW, tempH, d, pf, pal);
                ShowNewImage(newBmp, "門檻二值化 (Threshold Binarized, T=" + T + ")");
            }
        }

        private void ApplyEdge(int mode)
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            Bitmap bmp = activeChild.pBitmap;
            int tempW = bmp.Width;
            int tempH = bmp.Height;
            int d = 0;
            PixelFormat pf = new PixelFormat();
            ColorPalette pal = null;
            int[] fArray = dyn_bmp2array(bmp, ref d, ref pf, ref pal);
            int[] gArray = new int[tempW * tempH * d];

            string name = "";
            if (mode == 0) // Sobel
            {
                unsafe
                {
                    fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                    {
                        detect_sobel(f0, tempW, tempH, d, g0);
                    }
                }
                name = "Sobel 邊緣 (Sobel Edges)";
            }
            else if (mode == 1) // Canny
            {
                double low = 30.0;
                double high = 90.0;
                if (ParamDialog.ShowCannyDialog(out low, out high))
                {
                    unsafe
                    {
                        fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                        {
                            detect_canny(f0, tempW, tempH, d, g0, low, high);
                        }
                    }
                    name = string.Format("Canny 邊緣 (Canny Edges, L={0:F0}, H={1:F0})", low, high);
                }
                else return;
            }

            Bitmap newBmp = dyn_array2bmp(gArray, tempW, tempH, d, pf, pal);
            ShowNewImage(newBmp, name);
        }

        private void ApplyHoughLine()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            int houghThresh = 50;
            if (ParamDialog.ShowSliderDialog("霍夫直線偵測 (Hough Line Detection)", "輸入累加器門檻 (Enter Accumulator Threshold, e.g. 20 to 150):", 5, 300, 50, out houghThresh))
            {
                Bitmap bmp = activeChild.pBitmap;
                int tempW = bmp.Width;
                int tempH = bmp.Height;
                int d = 0;
                PixelFormat pf = new PixelFormat();
                ColorPalette pal = null;
                int[] fArray = dyn_bmp2array(bmp, ref d, ref pf, ref pal);
                int[] gArray = new int[tempW * tempH * d];

                unsafe
                {
                    fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                    {
                        detect_lines_hough(f0, tempW, tempH, d, g0, houghThresh);
                    }
                }

                Bitmap newBmp = dyn_array2bmp(gArray, tempW, tempH, d, pf, pal);
                ShowNewImage(newBmp, "霍夫直線 (Hough Lines, Thresh=" + houghThresh + ")");
            }
        }

        private void ApplyHoughCircle()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            int rMin, rMax, thresh;
            if (ParamDialog.ShowHoughCircleDialog(out rMin, out rMax, out thresh))
            {
                Bitmap bmp = activeChild.pBitmap;
                int tempW = bmp.Width;
                int tempH = bmp.Height;
                int d = 0;
                PixelFormat pf = new PixelFormat();
                ColorPalette pal = null;
                int[] fArray = dyn_bmp2array(bmp, ref d, ref pf, ref pal);
                int[] gArray = new int[tempW * tempH * d];

                unsafe
                {
                    fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                    {
                        detect_circles_hough(f0, tempW, tempH, d, g0, rMin, rMax, thresh);
                    }
                }

                Bitmap newBmp = dyn_array2bmp(gArray, tempW, tempH, d, pf, pal);
                ShowNewImage(newBmp, string.Format("霍夫圓形 (Hough Circles, R:{0}-{1}, T={2})", rMin, rMax, thresh));
            }
        }
    }
}
