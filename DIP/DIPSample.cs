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
        public static unsafe extern void calculate_histogram(int* f, int w, int h, int d, int* histGray);

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
        private PictureBox picHistogram;
        private Label lblSidebarTitle;
        private Label lblStats;
        private int[] currentHistData = new int[256];

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
            this.stStripLabel.Text = "Ready";

            InitializeSidebar();
            RegisterEvents();
        }

        private void InitializeSidebar()
        {
            this.panelSidebar = new Panel();
            this.picHistogram = new PictureBox();
            this.lblSidebarTitle = new Label();
            this.lblStats = new Label();

            // panelSidebar
            this.panelSidebar.Dock = DockStyle.Right;
            this.panelSidebar.Width = 280;
            this.panelSidebar.BackColor = Color.FromArgb(30, 30, 35);
            this.panelSidebar.Padding = new Padding(15);

            // lblSidebarTitle
            this.lblSidebarTitle.Text = "Grayscale Histogram";
            this.lblSidebarTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            this.lblSidebarTitle.ForeColor = Color.White;
            this.lblSidebarTitle.Dock = DockStyle.Top;
            this.lblSidebarTitle.Height = 35;

            // picHistogram
            this.picHistogram.Dock = DockStyle.Top;
            this.picHistogram.Height = 200;
            this.picHistogram.BackColor = Color.FromArgb(20, 20, 22);
            this.picHistogram.Paint += new PaintEventHandler(picHistogram_Paint);

            // lblStats
            this.lblStats.Dock = DockStyle.Fill;
            this.lblStats.Font = new Font("Segoe UI", 9F);
            this.lblStats.ForeColor = Color.LightGray;
            this.lblStats.Padding = new Padding(0, 15, 0, 0);
            this.lblStats.Text = "No active image";

            // Add controls to panelSidebar
            this.panelSidebar.Controls.Add(this.lblStats);
            this.panelSidebar.Controls.Add(this.picHistogram);
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
                if (item.Text == "&IP" || item.Name == "iPToolStripMenuItem")
                {
                    ipMenu = item as ToolStripMenuItem;
                    break;
                }
            }
            if (ipMenu != null)
            {
                ToolStripMenuItem btnBC = new ToolStripMenuItem("Brightness and Contrast");
                btnBC.Click += (s, e) => ApplyBrightnessContrast();
                ipMenu.DropDownItems.Add(btnBC);
            }

            // Dynamically add Laplacian, LoG, High Boost to Neighborhood menu
            if (this.neighborhoodProcessingToolStripMenuItem != null)
            {
                ToolStripMenuItem btnLap = new ToolStripMenuItem("Laplacian Filter (8-Neighbors)");
                btnLap.Click += (s, e) => ApplyFilter(2);
                this.neighborhoodProcessingToolStripMenuItem.DropDownItems.Add(btnLap);

                ToolStripMenuItem btnLog = new ToolStripMenuItem("Laplacian of Gaussian (LoG)");
                btnLog.Click += (s, e) => ApplyFilter(3);
                this.neighborhoodProcessingToolStripMenuItem.DropDownItems.Add(btnLog);

                ToolStripMenuItem btnHB = new ToolStripMenuItem("Unsharp Masking / High-Boost");
                btnHB.Click += (s, e) => ApplyFilter(4);
                this.neighborhoodProcessingToolStripMenuItem.DropDownItems.Add(btnHB);
            }

            // Dynamically add Sobel and Canny to Edge Detection menu
            if (this.edgeDetectionToolStripMenuItem != null)
            {
                ToolStripMenuItem btnSobel = new ToolStripMenuItem("Sobel Operator");
                btnSobel.Click += (s, e) => ApplyEdge(0);
                this.edgeDetectionToolStripMenuItem.DropDownItems.Add(btnSobel);

                ToolStripMenuItem btnCanny = new ToolStripMenuItem("Canny Edge Detector");
                btnCanny.Click += (s, e) => ApplyEdge(1);
                this.edgeDetectionToolStripMenuItem.DropDownItems.Add(btnCanny);
            }

            // Dynamically create a new top-level menu for Hough Line/Circle Detection!
            ToolStripMenuItem houghMenu = new ToolStripMenuItem("Hough Detection");
            ToolStripMenuItem btnHoughLine = new ToolStripMenuItem("Hough Line Detection");
            btnHoughLine.Click += (s, e) => ApplyHoughLine();
            ToolStripMenuItem btnHoughCircle = new ToolStripMenuItem("Hough Circle Detection");
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
            this.histogramEqualizationGammaValueToolStripMenuItem.Text = "Gamma Power-Law Transform";
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            oFileDlg.CheckFileExists = true;
            oFileDlg.CheckPathExists = true;
            oFileDlg.Title = "Open File - DIP Sample";
            oFileDlg.ValidateNames = true;
            oFileDlg.Filter = "bmp files (*.bmp)|*.bmp";
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
                lblStats.Text = "No active image";
                Array.Clear(currentHistData, 0, 256);
                picHistogram.Invalidate();
                return;
            }

            Bitmap bmp = activeChild.pBitmap;
            int tempW = bmp.Width;
            int tempH = bmp.Height;
            int d = 0;
            PixelFormat pf = new PixelFormat();
            ColorPalette pal = null;

            int[] f = dyn_bmp2array(bmp, ref d, ref pf, ref pal);

            int[] hist = new int[256];
            unsafe
            {
                fixed (int* f0 = f) fixed (int* h0 = hist)
                {
                    calculate_histogram(f0, tempW, tempH, d, h0);
                }
            }

            Array.Copy(hist, currentHistData, 256);

            // Statistics
            double sum = 0;
            long total = tempW * tempH;
            for (int i = 0; i < 256; i++) sum += (double)hist[i] * i;
            double mean = sum / total;

            double varSum = 0;
            for (int i = 0; i < 256; i++) varSum += (double)hist[i] * Math.Pow(i - mean, 2);
            double stdDev = Math.Sqrt(varSum / total);

            int median = 127;
            long cum = 0;
            long half = total / 2;
            for (int i = 0; i < 256; i++)
            {
                cum += hist[i];
                if (cum >= half) { median = i; break; }
            }

            lblStats.Text = string.Format(
                "Image Size: {0} x {1}\n" +
                "Format: {2}\n" +
                "Total Pixels: {3:N0}\n\n" +
                "--- Statistics ---\n" +
                "Mean Intensity: {4:F2}\n" +
                "Median Intensity: {5}\n" +
                "Std Deviation: {6:F2}",
                tempW, tempH, pf.ToString(), total, mean, median, stdDev
            );

            picHistogram.Invalidate();
        }

        private void picHistogram_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int width = picHistogram.Width;
            int height = picHistogram.Height;

            int maxVal = 0;
            for (int i = 0; i < 256; i++)
            {
                if (currentHistData[i] > maxVal) maxVal = currentHistData[i];
            }

            if (maxVal == 0)
            {
                g.Clear(Color.FromArgb(20, 20, 22));
                return;
            }

            g.Clear(Color.FromArgb(20, 20, 22));

            // Grid lines
            using (Pen gridPen = new Pen(Color.FromArgb(40, 40, 45), 1))
            {
                for (int i = 1; i < 4; i++)
                {
                    int x = i * width / 4;
                    g.DrawLine(gridPen, x, 0, x, height);
                    int y = i * height / 4;
                    g.DrawLine(gridPen, 0, y, width, y);
                }
            }

            // Silver-blue dynamic gradient brush for modern UI look
            using (System.Drawing.Drawing2D.LinearGradientBrush brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                new Rectangle(0, 0, width, height),
                Color.FromArgb(100, 149, 237),
                Color.FromArgb(30, 144, 255),
                90F))
            {
                System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath();
                path.StartFigure();
                path.AddLine(0, height, 0, height);

                for (int i = 0; i < 256; i++)
                {
                    float x = (float)i / 255 * (width - 2);
                    float y = height - ((float)currentHistData[i] / maxVal * (height - 10));
                    path.AddLine(x, y, x, y);
                }

                path.AddLine(width, height, 0, height);
                path.CloseFigure();

                g.FillPath(brush, path);
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
            ShowNewImage(grayBmp, "Grayscale Image");
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
                bitPlaneSliceForm.Text = "Bit-Plane Preview";
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
                ShowNewImage(newBmp, string.Format("B&C (a={0:F1}, b={1})", alpha, beta));
            }
        }

        private void ApplyGammaCorrection()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            int gammaVal = 10;
            if (ParamDialog.ShowSliderDialog("Gamma Correction", "Enter Gamma scale (1 to 30, representing 0.1 to 3.0):", 1, 30, 10, out gammaVal))
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
                ShowNewImage(newBmp, string.Format("Gamma Correction (g={0:F1})", gamma));
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
            ShowNewImage(newBmp, "Histogram Equalized");
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
                filterName = "Mean Filter 3x3";
            }
            else if (filterType == 1) // Gaussian Filter 3x3
            {
                kernel = new double[] { 1, 2, 1, 2, 4, 2, 1, 2, 1 };
                divisor = 16.0;
                filterName = "Gaussian Filter 3x3";
            }
            else if (filterType == 2) // Laplacian Filter 3x3 (8-Neighbors)
            {
                kernel = new double[] { -1, -1, -1, -1, 8, -1, -1, -1, -1 };
                divisor = 1.0;
                filterName = "Laplacian Sharpening";
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
                filterName = "LoG Filter 5x5";
            }
            else if (filterType == 4) // High-Boost filter
            {
                int weightVal = 12;
                if (ParamDialog.ShowSliderDialog("High Boost Filter", "Enter scale A (10 to 30, representing 1.0 to 3.0):", 10, 30, 12, out weightVal))
                {
                    double A = (double)weightVal / 10.0;
                    double center = 8.0 + (A - 1.0) * 9.0;
                    kernel = new double[] { -1, -1, -1, -1, center, -1, -1, -1, -1 };
                    divisor = 1.0;
                    filterName = string.Format("High-Boost Filter (A={0:F1})", A);
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
            if (ParamDialog.ShowSliderDialog("Image Scaling", "Enter scaling percentage (10% to 500%):", 10, 500, 200, out scalePercent))
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
                ShowNewImage(newBmp, string.Format("Scaled {0}% ({1})", scalePercent, mode == 0 ? "Nearest" : "Bilinear"));
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
                ShowNewImage(newBmp, string.Format("Rotated {0}° ({1})", angle, mode == 0 ? "Nearest" : "Bilinear"));
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
            ShowNewImage(newBmp, "Otsu Threshold Binarized");
        }

        private void ApplyManualThreshold()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            int T = 128;
            if (ParamDialog.ShowSliderDialog("Manual Threshold", "Enter threshold value (0 to 255):", 0, 255, 128, out T))
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
                ShowNewImage(newBmp, "Threshold Binarized (T=" + T + ")");
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
                name = "Sobel Edges";
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
                    name = string.Format("Canny Edges (L={0:F0}, H={1:F0})", low, high);
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
            if (ParamDialog.ShowSliderDialog("Hough Line Detection", "Enter Accumulator Threshold (e.g. 20 to 150):", 5, 300, 50, out houghThresh))
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
                ShowNewImage(newBmp, "Hough Lines (Thresh=" + houghThresh + ")");
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
                ShowNewImage(newBmp, string.Format("Hough Circles (R:{0}-{1}, T={2})", rMin, rMax, thresh));
            }
        }
    }
}
