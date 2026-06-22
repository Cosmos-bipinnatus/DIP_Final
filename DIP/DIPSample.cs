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
        public static unsafe extern void bit_plane_slice(int* f, int w, int h, int d, int* g, int plane, int binarize);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void adjust_brightness_contrast(int* f, int w, int h, int d, int* g, double alpha, int beta);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void calculate_histogram(int* f, int w, int h, int d, int* histB, int* histG, int* histR);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void histogram_equalization(int* f, int w, int h, int d, int* g);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void convolution_filter(int* f, int w, int h, int d, int* g, double[] kernel, int kSize, double divisor, double offset);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void scale_image(int* f, int w, int h, int d, int* g, int newW, int newH, int mode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void rotate_image(int* f, int w, int h, int d, int* g, int newW, int newH, double angle_deg, int mode, int bg_r, int bg_g, int bg_b, int bg_a);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void manual_threshold(int* f, int w, int h, int d, int* g, int T);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void otsu_threshold(int* f, int w, int h, int d, int* g);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void detect_sobel(int* f, int w, int h, int d, int* g);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void detect_canny(int* f, int w, int h, int d, int* g, double lowThresh, double highThresh);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void detect_lines_hough(int* f, int w, int h, int d, int* g, int houghThreshold, int lineR, int lineG, int lineB);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void detect_circles_hough(int* f, int w, int h, int d, int* g, int rMin, int rMax, int houghThreshold, int lineR, int lineG, int lineB);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void median_filter(int* f, int w, int h, int d, int* g, int kSize);

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
        private ToolTip customToolTip = new ToolTip();
        private Timer hoverTimer = new Timer();
        private ToolStripItem currentHoveredItem = null;
        private string currentHoveredText = "";

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

            try
            {
                InitializeSidebar();
                RegisterEvents();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Exception during Form Load: " + ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
            this.lblSidebarTitle.Text = "Grayscale Histogram";
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
            this.lblStats.Text = "No active image";

            // Add controls to panelSidebar (reverse addition order for docking layout)
            this.panelSidebar.Controls.Add(this.lblStats);
            this.panelSidebar.Controls.Add(this.picHistR);
            this.panelSidebar.Controls.Add(this.picHistG);
            this.panelSidebar.Controls.Add(this.picHistB);
            this.panelSidebar.Controls.Add(this.lblSidebarTitle);

            // Add sidebar to form
            this.Controls.Add(this.panelSidebar);

            // Enforce correct docking layer order (Z-order) to prevent panelSidebar from overlapping menuStrip1 or statusStrip1
            this.menuStrip1.SendToBack();
            this.statusStrip1.SendToBack();
            this.panelSidebar.BringToFront();

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
            this.manualThresholdToolStripMenuItem.Click += (s, e) => ApplyManualThreshold();
            this.bitPlanesToolStripMenuItem.Click += (s, e) => TriggerBitPlanes();
            this.averagingFilterToolStripMenuItem.Click += (s, e) => ApplyFilter(0); // Mean
            this.gaussianFiltersToolStripMenuItem.Click += (s, e) => ApplyFilter(1); // Gaussian

            // Neighborhood menu additions below

            // Dynamically add Laplacian, LoG, High Boost to Neighborhood menu
            if (this.neighborhoodProcessingToolStripMenuItem != null)
            {
                ToolStripMenuItem btnLap = new ToolStripMenuItem("Laplacian Filter (8-Neighbors)");
                btnLap.Click += (s, e) => ApplyFilter(2);
                this.neighborhoodProcessingToolStripMenuItem.DropDownItems.Add(btnLap);
                BindTooltip(btnLap, "Enhances high-frequency details and sharpens the image using an 8-neighborhood Laplacian operator.");

                ToolStripMenuItem btnLog = new ToolStripMenuItem("Laplacian of Gaussian (LoG)");
                btnLog.Click += (s, e) => ApplyFilter(3);
                this.neighborhoodProcessingToolStripMenuItem.DropDownItems.Add(btnLog);
                BindTooltip(btnLog, "Combines Gaussian smoothing with Laplacian second-order differentiation to suppress noise before extracting second-order edge extrema.");

                ToolStripMenuItem btnHB = new ToolStripMenuItem("Unsharp Masking / High-Boost Filter");
                btnHB.Click += (s, e) => ApplyFilter(4);
                this.neighborhoodProcessingToolStripMenuItem.DropDownItems.Add(btnHB);
                BindTooltip(btnHB, "High-boost filtering. Generates a detail mask by subtracting the blurred image from the original, and overlays it back based on weight to adjust sharpening strength.");

                ToolStripMenuItem btnCustom = new ToolStripMenuItem("Custom 3x3 / 5x5 Filter...");
                btnCustom.Click += (s, e) => ApplyCustomFilter();
                this.neighborhoodProcessingToolStripMenuItem.DropDownItems.Add(btnCustom);
                BindTooltip(btnCustom, "Applies neighborhood convolution on grayscale images using a custom 3x3 or 5x5 filter kernel matrix, divisor, and offset.");

                ToolStripMenuItem btnMedian = new ToolStripMenuItem("Median Filter...");
                btnMedian.Click += (s, e) => ApplyMedianFilter();
                this.neighborhoodProcessingToolStripMenuItem.DropDownItems.Add(btnMedian);
                BindTooltip(btnMedian, "A non-linear spatial low-pass filter that removes noise using the neighborhood median. Highly effective against salt-and-pepper noise while preserving edges.");
            }

            // Dynamically add Sobel and Canny to Edge Detection menu
            if (this.edgeDetectionToolStripMenuItem != null)
            {
                ToolStripMenuItem btnSobel = new ToolStripMenuItem("Sobel Operator");
                btnSobel.Click += (s, e) => ApplyEdge(0);
                this.edgeDetectionToolStripMenuItem.DropDownItems.Add(btnSobel);
                BindTooltip(btnSobel, "Calculates edge strength by performing first-order differentiation using a 3x3 Sobel operator in X and Y directions.");

                ToolStripMenuItem btnCanny = new ToolStripMenuItem("Canny Edge Detector");
                btnCanny.Click += (s, e) => ApplyEdge(1);
                this.edgeDetectionToolStripMenuItem.DropDownItems.Add(btnCanny);
                BindTooltip(btnCanny, "High-precision edge detection, including Gaussian noise filtering, gradient calculation, non-maximum suppression, and dual-threshold hysteresis edge tracking.");
            }

            // Dynamically create a new top-level menu for Hough Line/Circle Detection!
            ToolStripMenuItem houghMenu = new ToolStripMenuItem("Hough Detection");
            ToolStripMenuItem btnHoughLine = new ToolStripMenuItem("Hough Line Detection");
            btnHoughLine.Click += (s, e) => ApplyHoughLine();
            BindTooltip(btnHoughLine, "Detects straight lines from edges of 8bpp Indexed grayscale images using a polar coordinate voting mechanism in Hough accumulative space.");

            ToolStripMenuItem btnHoughCircle = new ToolStripMenuItem("Hough Circle Detection");
            btnHoughCircle.Click += (s, e) => ApplyHoughCircle();
            BindTooltip(btnHoughCircle, "Detects circular contours from 8bpp Indexed grayscale images using gradient-vector-assisted 3D accumulative voting and local maximum suppression.");

            houghMenu.DropDownItems.Add(btnHoughLine);
            houghMenu.DropDownItems.Add(btnHoughCircle);
            this.menuStrip1.Items.Add(houghMenu);

            // Wire the click event for Show Histogram

            // Grayscale equalization
            this.histogramEqualizationLinearToolStripMenuItem.Click += (s, e) => ApplyHistogramEqualization();
            // Combined Brightness, Contrast & Gamma transform (Linear & Non-Linear)
            this.histogramEqualizationGammaValueToolStripMenuItem.Click += (s, e) => ApplyBrightnessContrastGamma();
            this.histogramEqualizationGammaValueToolStripMenuItem.Text = "Brightness, Contrast & Gamma Adjustment";

            // Initialize Custom ToolTip and Timer for 1.5 seconds Hover
            hoverTimer.Interval = 1500;
            hoverTimer.Tick += HoverTimer_Tick;
            customToolTip.AutoPopDelay = 32767; // set max delay

            // Bind tooltips for sub-menus
            BindTooltip(this.openToolStripMenuItem, "Loads JPEG, BMP, or PNG format image files into the workspace.");
            BindTooltip(this.rGBtoGrayToolStripMenuItem, "Converts BGR color images to grayscale using the BT.601 standard formula. Includes 24-bit preview and standard 8-bit output sub-options.");
            BindTooltip(this.rGBtoGray24bitToolStripMenuItem, "Converts color images to 24-bit grayscale (R=G=B channels are identical but in a 24-bit format) using the BT.601 standard formula for standard preview.");
            BindTooltip(this.rGBtoGray8bitToolStripMenuItem, "Converts the image to standard 8-bit grayscale format (Format8bppIndexed), supporting thresholding, spatial filtering, Hough line/circle detection, and other algorithms.");
            BindTooltip(this.averagingFilterToolStripMenuItem, "A spatial low-pass filter that blurs the image using neighborhood mean. Smooths fine noise but slightly blurs edges.");
            BindTooltip(this.gaussianFiltersToolStripMenuItem, "A spatial low-pass filter that uses a Gaussian-weighted template for more natural image smoothing and noise reduction.");
            BindTooltip(this.bitPlanesToolStripMenuItem, "Decomposes an 8-bit grayscale image into 8 independent binary bit planes. Higher bit planes contain main structure, while lower bit planes contain fine noise.");
            BindTooltip(this.histogramEqualizationLinearToolStripMenuItem, "Calculates the Cumulative Distribution Function (CDF) to automatically stretch the grayscale range, significantly enhancing overall light/dark details in low-contrast images.");
            BindTooltip(this.histogramEqualizationGammaValueToolStripMenuItem, "Adjusts brightness and contrast, and supports non-linear Gamma power-law transformation to correct the image's sensitivity curve. Supports mouse dragging/panning on preview.");
            BindTooltip(this.rotationToolStripMenuItem, "Supports nearest neighbor and bilinear interpolation. Freely configure mapping mode, rotation angle, background color, and original image blending while preventing boundary clipping.");
            BindTooltip(this.nearestNeighborInterpolationToolStripMenuItem, "Nearest neighbor interpolation for image scaling. Extremely fast, but produces visible jagged edges and pixelation when scaled up.");
            BindTooltip(this.bilinearInterpolationToolStripMenuItem, "Bilinear interpolation for image scaling. Interpolates by weighting distance from 4 neighboring pixels. Produces smooth edges and softer details when scaled up.");
            BindTooltip(this.manualThresholdToolStripMenuItem, "Supports grayscale images only. Manually sets a threshold from 0 to 255 to segment the image into black (0) and white (255) regions, featuring real-time preview.");
            BindTooltip(this.otsusMethodToolStripMenuItem, "Supports grayscale images only. Automatically finds the optimal threshold using Otsu's method to accurately separate foreground and background.");

            // Bind tooltips for top-level menu titles/headers
            BindTooltip(this.fileToolStripMenuItem, "Opens and manages image files, loading local images for digital image processing.");
            BindTooltip(this.histogramToolStripMenuItem, "Histogram analysis and processing, including displaying color histograms and contrast equalization.");
            BindTooltip(this.interpolationToolStripMenuItem, "Changes image geometric dimensions and resolution, providing nearest neighbor and bilinear interpolation scaling algorithms.");
            BindTooltip(this.neighborhoodProcessingToolStripMenuItem, "Neighborhood convolution spatial filters, including average blur, Gaussian smoothing, Laplacian sharpening, LoG edge enhancement, high-boost, and custom 3x3 & 5x5 kernels.");
            BindTooltip(this.segmentationToolStripMenuItem, "Segments the foreground from the background, providing manual thresholding and Otsu's adaptive thresholding.");
            BindTooltip(this.edgeDetectionToolStripMenuItem, "Analyzes image brightness gradient extrema to extract object contours, including Sobel operators and high-precision Canny algorithm.");
            BindTooltip(houghMenu, "Extracts geometric shapes like lines or circles from binarized edge images using a parameter space voting mechanism.");

            // Locate and bind Basic Processing top-level menu (iPToolStripMenuItem is local in designer)
            foreach (ToolStripItem item in this.menuStrip1.Items)
            {
                if (item.Text != null && item.Text.Contains("Basic"))
                {
                    BindTooltip(item, "Basic image intensity and bit processing, including color-to-grayscale, bit plane slicing, and brightness/contrast adjustments.");
                    break;
                }
            }
        }

        private void BindTooltip(ToolStripItem item, string text)
        {
            if (item == null) return;
            item.MouseEnter += (s, e) => {
                hoverTimer.Stop();
                customToolTip.Hide(menuStrip1);
                currentHoveredItem = item;
                currentHoveredText = text;
                hoverTimer.Start();
            };
            item.MouseLeave += (s, e) => {
                hoverTimer.Stop();
                customToolTip.Hide(menuStrip1);
                if (currentHoveredItem == item)
                {
                    currentHoveredItem = null;
                    currentHoveredText = "";
                }
            };
            item.Click += (s, e) => {
                hoverTimer.Stop();
                customToolTip.Hide(menuStrip1);
            };
        }

        private void HoverTimer_Tick(object sender, EventArgs e)
        {
            hoverTimer.Stop();
            if (currentHoveredItem != null && !string.IsNullOrEmpty(currentHoveredText))
            {
                Point cursorPoint = Cursor.Position;
                Point relativePoint = menuStrip1.PointToClient(cursorPoint);
                customToolTip.Show(currentHoveredText, menuStrip1, relativePoint.X + 15, relativePoint.Y + 15, 32767);
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            oFileDlg.CheckFileExists = true;
            oFileDlg.CheckPathExists = true;
            oFileDlg.Title = "Open File - DIP Sample";
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
        internal int[] dyn_bmp2array(Bitmap myBitmap, ref int ByteDepth, ref PixelFormat pixelFormat, ref ColorPalette palette)
        {
            Bitmap tempBitmap = myBitmap;
            bool converted = false;

            // Check if pixel format is supported (must be 8bpp, 24bpp, or 32bpp)
            if (myBitmap.PixelFormat != PixelFormat.Format8bppIndexed &&
                myBitmap.PixelFormat != PixelFormat.Format24bppRgb &&
                myBitmap.PixelFormat != PixelFormat.Format32bppArgb)
            {
                tempBitmap = new Bitmap(myBitmap.Width, myBitmap.Height, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(tempBitmap))
                {
                    g.DrawImage(myBitmap, new Rectangle(0, 0, myBitmap.Width, myBitmap.Height));
                }
                converted = true;
            }

            BitmapData byteArray = tempBitmap.LockBits(new Rectangle(0, 0, tempBitmap.Width, tempBitmap.Height),
                                          ImageLockMode.ReadOnly,
                                          tempBitmap.PixelFormat);
            pixelFormat = tempBitmap.PixelFormat;
            palette = tempBitmap.Palette;
            ByteDepth = Image.GetPixelFormatSize(tempBitmap.PixelFormat) / 8;
            if (ByteDepth < 1) ByteDepth = 1;

            int Width = tempBitmap.Width;
            int Height = tempBitmap.Height;
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
            tempBitmap.UnlockBits(byteArray);

            if (converted)
            {
                tempBitmap.Dispose();
            }

            return ImgData;
        }

        internal static Bitmap dyn_array2bmp(int[] ImgData, int Width, int Height, int ByteDepth, PixelFormat pixelFormat, ColorPalette palette)
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

        private static Bitmap checkerBmp = null;
        public static Bitmap GetCheckerboardBitmap()
        {
            if (checkerBmp == null)
            {
                checkerBmp = new Bitmap(16, 16);
                using (Graphics g = Graphics.FromImage(checkerBmp))
                {
                    g.Clear(Color.White);
                    using (SolidBrush grayBrush = new SolidBrush(Color.FromArgb(240, 240, 240)))
                    {
                        g.FillRectangle(grayBrush, 0, 0, 8, 8);
                        g.FillRectangle(grayBrush, 8, 8, 8, 8);
                    }
                }
            }
            return checkerBmp;
        }

        public static void CopyImageToClipboard(Image img)
        {
            if (img == null) return;
            DataObject dataObject = new DataObject();
            dataObject.SetData(DataFormats.Bitmap, true, img);
            try
            {
                System.IO.MemoryStream ms = new System.IO.MemoryStream();
                img.Save(ms, ImageFormat.Png);
                dataObject.SetData("PNG", false, ms);
            }
            catch { }
            Clipboard.SetDataObject(dataObject, true);
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

            Form activeChild = this.ActiveMdiChild;
            if (activeChild == null)
            {
                lblStats.Text = "No active image";
                Array.Clear(currentHistDataB, 0, 256);
                Array.Clear(currentHistDataG, 0, 256);
                Array.Clear(currentHistDataR, 0, 256);
                picHistB.Invalidate();
                picHistG.Invalidate();
                picHistR.Invalidate();
                return;
            }

            Bitmap bmp = null;
            if (activeChild is MSForm msForm)
            {
                bmp = msForm.pBitmap;
            }
            else if (activeChild is BitPlaneSliceForm bpsForm)
            {
                bmp = bpsForm.ProcessedBitmap;
            }
            else if (activeChild is BrightnessContrastGammaForm bcgForm)
            {
                bmp = bcgForm.ProcessedBitmap;
            }
            else if (activeChild is RotateImageForm rotForm)
            {
                bmp = rotForm.ProcessedBitmap;
            }
            else if (activeChild is ManualThresholdForm mtForm)
            {
                bmp = mtForm.ProcessedBitmap;
            }
            else if (activeChild is CannyForm cannyForm)
            {
                bmp = cannyForm.ProcessedBitmap;
            }
            else if (activeChild is HoughLineForm hlForm)
            {
                bmp = hlForm.ProcessedBitmap;
            }
            else if (activeChild is HoughCircleForm hcForm)
            {
                bmp = hcForm.ProcessedBitmap;
            }

            if (bmp == null)
            {
                lblStats.Text = "No active image";
                Array.Clear(currentHistDataB, 0, 256);
                Array.Clear(currentHistDataG, 0, 256);
                Array.Clear(currentHistDataR, 0, 256);
                picHistB.Invalidate();
                picHistG.Invalidate();
                picHistR.Invalidate();
                return;
            }

            int tempW = bmp.Width;
            int tempH = bmp.Height;
            int d = 0;
            PixelFormat pf = new PixelFormat();
            ColorPalette pal = null;

            int[] f = dyn_bmp2array(bmp, ref d, ref pf, ref pal);

            int[] histB = new int[256];
            int[] histG = new int[256];
            int[] histR = new int[256];

            int[] mask = null;
            if (activeChild is RotateImageForm rotF)
            {
                if (rotF.IsTransparentOrNotBlended())
                {
                    mask = rotF.BackgroundMask;
                }
            }

            // Perform dynamic channel consistency check to verify if the 3 or 4 channels are identical (real grayscale)
            bool isActuallyGray = (d == 1);
            if (d == 3 || d == 4)
            {
                isActuallyGray = true;
                for (int i = 0; i < f.Length; i += d)
                {
                    if (d == 4 && f[i + 3] == 0) continue; // Skip transparent pixels in consistency check
                    if (f[i] != f[i + 1] || f[i + 1] != f[i + 2])
                    {
                        isActuallyGray = false;
                        break;
                    }
                }
            }

            if (!isActuallyGray)
            {
                lblSidebarTitle.Text = "BGR Histogram";
                picHistB.Height = 120;
                picHistG.Visible = true;
                picHistR.Visible = true;
            }
            else
            {
                lblSidebarTitle.Text = "Grayscale Histogram";
                picHistB.Height = 200;
                picHistG.Visible = false;
                picHistR.Visible = false;
            }

            if (mask != null)
            {
                // Calculate histogram using mask in C# (to exclude rotated background)
                if (d == 1)
                {
                    for (int i = 0; i < f.Length; i++)
                    {
                        if (i < mask.Length && mask[i] == 1)
                        {
                            int val = f[i];
                            if (val >= 0 && val <= 255)
                            {
                                histB[val]++;
                            }
                        }
                    }
                    Array.Copy(histB, histG, 256);
                    Array.Copy(histB, histR, 256);
                }
                else if (d == 3 || d == 4)
                {
                    for (int i = 0; i < f.Length; i += d)
                    {
                        int pixelIdx = i / d;
                        if (pixelIdx < mask.Length && mask[pixelIdx] == 1)
                        {
                            if (d == 4 && f[i + 3] == 0) continue; // Skip transparent
                            int b = f[i + 0];
                            int g_val = f[i + 1];
                            int r = f[i + 2];
                            if (b >= 0 && b <= 255) histB[b]++;
                            if (g_val >= 0 && g_val <= 255) histG[g_val]++;
                            if (r >= 0 && r <= 255) histR[r]++;
                        }
                    }
                }
            }
            else
            {
                // General path: call C++ calculate_histogram
                unsafe
                {
                    fixed (int* f0 = f) fixed (int* hB = histB) fixed (int* hG = histG) fixed (int* hR = histR)
                    {
                        calculate_histogram(f0, tempW, tempH, d, hB, hG, hR);
                    }
                }
            }

            Array.Copy(histB, currentHistDataB, 256);
            Array.Copy(histG, currentHistDataG, 256);
            Array.Copy(histR, currentHistDataR, 256);

            // Statistics (exact grayscale representation)
            double sum = 0;
            long total = 0;

            double mean = 0;
            double stdDev = 0;
            int median = 127;

            if (isActuallyGray)
            {
                total = 0;
                for (int i = 0; i < 256; i++) total += histB[i];
                if (total <= 0) total = 1;

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
            else // Color BGR/ARGB
            {
                // Calculate grayscale values in C# to get accurate stats
                int[] histY = new int[256];
                if (mask != null)
                {
                    for (int i = 0; i < f.Length; i += d)
                    {
                        int pixelIdx = i / d;
                        if (pixelIdx < mask.Length && mask[pixelIdx] == 1)
                        {
                            if (d == 4 && f[i + 3] == 0) continue; // Skip transparent
                            int b = f[i + 0];
                            int g_val = f[i + 1];
                            int r = f[i + 2];
                            int gray = (int)(r * 0.299 + g_val * 0.587 + b * 0.114);
                            if (gray >= 0 && gray <= 255) histY[gray]++;
                        }
                    }
                }
                else
                {
                    for (int y = 0; y < tempH; y++)
                    {
                        for (int x = 0; x < tempW; x++)
                        {
                            int idx = (y * tempW + x) * d;
                            if (d == 4 && f[idx + 3] == 0) continue; // Skip transparent
                            int b = f[idx + 0];
                            int g_val = f[idx + 1];
                            int r = f[idx + 2];
                            int gray = (int)(r * 0.299 + g_val * 0.587 + b * 0.114);
                            if (gray >= 0 && gray <= 255) histY[gray]++;
                        }
                    }
                }

                total = 0;
                for (int i = 0; i < 256; i++) total += histY[i];
                if (total <= 0) total = 1;

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

            string baseStats = string.Format(
                "Image Size: {0} x {1}\n" +
                "Format: {2}\n" +
                "Total Pixels: {3:N0}\n\n" +
                "--- Statistics ---\n" +
                "Mean Intensity: {4:F2}\n" +
                "Median Intensity: {5}\n" +
                "Std Deviation: {6:F2}",
                tempW, tempH, pf.ToString(), total, mean, median, stdDev
            );

            string paramStr = "";
            string descStr = "";

            if (activeChild != null)
            {
                try
                {
                    var paramProp = activeChild.GetType().GetProperty("ImageInfoParameters", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (paramProp != null)
                    {
                        paramStr = paramProp.GetValue(activeChild, null) as string;
                    }
                    var descProp = activeChild.GetType().GetProperty("ImageAlgorithmDescription", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (descProp != null)
                    {
                        descStr = descProp.GetValue(activeChild, null) as string;
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(paramStr))
            {
                baseStats += "\n\n--- Image Settings ---\n" + paramStr;
            }
            if (!string.IsNullOrEmpty(descStr))
            {
                baseStats += "\n\n--- Algorithm Docs ---\n" + descStr;
            }

            lblStats.Text = baseStats;

            picHistB.Invalidate();
            if (!isActuallyGray)
            {
                picHistG.Invalidate();
                picHistR.Invalidate();
            }

            // No Alpha histogram updating needed
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
        internal void ShowNewImage(Bitmap bmp, string title, string imageParams = "", string algoDesc = "")
        {
            MSForm childForm = new MSForm();
            childForm.MdiParent = this;
            childForm.pf1 = stStripLabel;
            childForm.pBitmap = bmp;
            childForm.Text = title;
            if (!string.IsNullOrEmpty(imageParams))
            {
                childForm.ImageInfoParameters = imageParams;
            }
            if (!string.IsNullOrEmpty(algoDesc))
            {
                childForm.ImageAlgorithmDescription = algoDesc;
            }
            childForm.Show();
            UpdateHistogram();
        }

        private void RGBtoGray24bitToolStripMenuItem_Click(object sender, EventArgs e)
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
            ShowNewImage(grayBmp, "Grayscale Image (24-bit)",
                "Algorithm: RGB to Grayscale (24-bit)",
                "Grayscale conversion is the process of converting color images (typically BGR channels) into single luminance values. The system adopts the ITU-R BT.601 standard formula: Y = 0.299 * R + 0.587 * G + 0.114 * B. This option outputs 24-bit/32-bit color format (R=G=B) for preview convenience. Note: 24-bit grayscale directly stores BGR values, whereas 8-bit grayscale indirectly references index colors, which might lead to small histogram differences due to GDI+ color management, rounding errors, and palette alignments.");
        }

        private void RGBtoGray8bitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            Bitmap gray8bpp = ConvertTo8bppGrayscale(activeChild.pBitmap);
            ShowNewImage(gray8bpp, "Grayscale Image (8-bit)",
                "Algorithm: RGB to Grayscale (8-bit)",
                "Grayscale conversion is the process of converting color images into single luminance values. This option outputs in Format8bppIndexed with a linear 256-color palette. The standard formula Y = 0.299 * R + 0.587 * G + 0.114 * B is used, preserving gray details and fitting the format requirements of binarization, spatial filtering, and Hough detection algorithms. Note: 24-bit grayscale directly stores BGR values, whereas 8-bit grayscale indirectly references index colors, which might lead to small histogram differences due to GDI+ color management, rounding errors, and palette alignments.");
        }

        private Bitmap ConvertTo8bppGrayscale(Bitmap src)
        {
            if (src.PixelFormat == PixelFormat.Format8bppIndexed)
            {
                return (Bitmap)src.Clone();
            }

            int tempW = src.Width;
            int tempH = src.Height;
            
            Bitmap dest = new Bitmap(tempW, tempH, PixelFormat.Format8bppIndexed);
            
            ColorPalette pal = dest.Palette;
            for (int i = 0; i < 256; i++)
            {
                pal.Entries[i] = Color.FromArgb(i, i, i);
            }
            dest.Palette = pal;
            
            int srcD = 0;
            PixelFormat srcPf = new PixelFormat();
            ColorPalette srcPal = null;
            int[] fArray = dyn_bmp2array(src, ref srcD, ref srcPf, ref srcPal);
            
            BitmapData destData = dest.LockBits(new Rectangle(0, 0, tempW, tempH), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            
            int destStride = destData.Stride;
            int destSkip = destStride - tempW;
            
            unsafe
            {
                byte* destPtr = (byte*)destData.Scan0;
                if (srcD == 1)
                {
                    for (int y = 0; y < tempH; y++)
                    {
                        for (int x = 0; x < tempW; x++)
                        {
                            *destPtr = (byte)fArray[y * tempW + x];
                            destPtr++;
                        }
                        destPtr += destSkip;
                    }
                }
                else if (srcD == 3 || srcD == 4)
                {
                    for (int y = 0; y < tempH; y++)
                    {
                        for (int x = 0; x < tempW; x++)
                        {
                            int idx = (y * tempW + x) * srcD;
                            int b = fArray[idx + 0];
                            int g = fArray[idx + 1];
                            int r = fArray[idx + 2];
                            
                            int gray = (int)(r * 0.299 + g * 0.587 + b * 0.114);
                            if (gray < 0) gray = 0;
                            if (gray > 255) gray = 255;
                            
                            *destPtr = (byte)gray;
                            destPtr++;
                        }
                        destPtr += destSkip;
                    }
                }
            }
            
            dest.UnlockBits(destData);
            return dest;
        }

        private bool IsImageActuallyGrayscale(Bitmap bmp)
        {
            if (bmp == null) return false;
            int d = 0;
            PixelFormat pf = new PixelFormat();
            ColorPalette pal = null;
            int[] f = dyn_bmp2array(bmp, ref d, ref pf, ref pal);

            if (d == 1) return true;
            if (d == 3 || d == 4)
            {
                for (int i = 0; i < f.Length; i += d)
                {
                    if (d == 4 && f[i + 3] == 0) continue; // Skip transparent pixels in consistency check
                    if (f[i] != f[i + 1] || f[i + 1] != f[i + 2])
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        private void TriggerBitPlanes()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            Bitmap bmpToUse = activeChild.pBitmap;
            if (!IsImageActuallyGrayscale(bmpToUse) || bmpToUse.PixelFormat != PixelFormat.Format8bppIndexed)
            {
                Bitmap gray8bpp = ConvertTo8bppGrayscale(bmpToUse);
                ShowNewImage(gray8bpp, "Auto Grayscale Conversion (8-bit)", 
                    "Algorithm: Convert to Grayscale (8-bit)", 
                    "Since this algorithm requires a standard 8-bit grayscale image input, the system has automatically converted the image to Format8bppIndexed format for you.");
                activeChild = this.ActiveMdiChild as MSForm;
                if (activeChild != null) bmpToUse = activeChild.pBitmap;
                else bmpToUse = gray8bpp;
            }

            BitPlaneSliceForm sliceForm = new BitPlaneSliceForm(this, bmpToUse);
            sliceForm.pf1 = this.stStripLabel;
            sliceForm.MdiParent = this;
            sliceForm.Show();
        }

        private void ApplyBrightnessContrastGamma()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            BrightnessContrastGammaForm bcgForm = new BrightnessContrastGammaForm(this, activeChild.pBitmap);
            bcgForm.pf1 = this.stStripLabel;
            bcgForm.MdiParent = this;
            bcgForm.Show();
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
            ShowNewImage(newBmp, "Histogram Equalized",
                "Algorithm: Histogram Equalization",
                "Histogram equalization is an image enhancement technique that uses the Cumulative Distribution Function (CDF) as a mapping curve. It stretches the intensity histogram evenly across the entire 0-255 interval, significantly improving overall contrast and dark details, which is highly suitable for images with concentrated illumination or under-exposure.");
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
                filterName = "Laplacian Sharpening 3x3";
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
                HighBoostFilterForm hbForm = new HighBoostFilterForm(this, bmp);
                hbForm.pf1 = this.stStripLabel;
                hbForm.MdiParent = this;
                hbForm.Show();
                return;
            }

            unsafe
            {
                fixed (int* f0 = fArray) fixed (int* g0 = gArray)
                {
                    convolution_filter(f0, tempW, tempH, d, g0, kernel, kSize, divisor, offset);
                }
            }

            Bitmap newBmp = dyn_array2bmp(gArray, tempW, tempH, d, pf, pal);
            
            string paramText = "";
            string algoDescText = "";
            if (filterType == 0)
            {
                paramText = "Algorithm: Mean Filter\nKernel Size: 3 x 3\nDivisor: 9.0\nOffset: 0.0";
                algoDescText = "A mean filter is a linear spatial low-pass filter. All of its kernel coefficients are 1, dividing the neighborhood sum by 9. It replaces the center pixel value with the average of neighboring pixel values to smooth out random fine noise, at the cost of blurring image edges and details.";
            }
            else if (filterType == 1)
            {
                paramText = "Algorithm: Gaussian Filter\nKernel Size: 3 x 3\nDivisor: 16.0\nOffset: 0.0";
                algoDescText = "A Gaussian filter is a smooth linear low-pass filter with weights conforming to a 2D Gaussian distribution (maximum weight at the center, tapering off like a bell curve). Compared to mean filtering, Gaussian filtering removes high-frequency noise while preserving image edges and details more naturally.";
            }
            else if (filterType == 2)
            {
                paramText = "Algorithm: Laplacian Sharpening\nKernel Size: 3 x 3\nDivisor: 1.0\nOffset: 0.0";
                algoDescText = "The Laplacian operator is a second-order differential operator that is highly sensitive to abrupt brightness variations (i.e. edges and lines). This function performs convolution using an 8-neighborhood Laplacian kernel to compute second-order difference values, overlaying edge details back onto the original image to enhance edge contrast and sharpen clarity.";
            }
            else if (filterType == 3)
            {
                paramText = "Algorithm: LoG Filter (Laplacian of Gaussian)\nKernel Size: 5 x 5\nDivisor: 1.0\nOffset: 128.0";
                algoDescText = "The Laplacian of Gaussian (LoG) operator combines Gaussian low-pass smoothing with Laplacian second-order differentiation. Since the Laplacian operator is extremely noise-sensitive, LoG uses a 5x5 Gaussian kernel to smooth and denoise before extracting precise edge extrema. It is widely used in feature point detection and multi-scale edge analysis. A bias offset of 128 is added to visualize positive and negative values.";
            }

            ShowNewImage(newBmp, filterName, paramText, algoDescText);
        }

        private void ApplyCustomFilter()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            Bitmap bmpToUse = activeChild.pBitmap;
            if (!IsImageActuallyGrayscale(bmpToUse) || bmpToUse.PixelFormat != PixelFormat.Format8bppIndexed)
            {
                Bitmap gray8bpp = ConvertTo8bppGrayscale(bmpToUse);
                ShowNewImage(gray8bpp, "Auto Grayscale Conversion (8-bit)", 
                    "Algorithm: Convert to Grayscale (8-bit)", 
                    "Since this algorithm requires a standard 8-bit grayscale image input, the system has automatically converted the image to Format8bppIndexed format for you.");
                activeChild = this.ActiveMdiChild as MSForm;
                if (activeChild != null) bmpToUse = activeChild.pBitmap;
                else bmpToUse = gray8bpp;
            }

            CustomFilterForm customForm = new CustomFilterForm(this, bmpToUse);
            customForm.pf1 = this.stStripLabel;
            customForm.MdiParent = this;
            customForm.Show();
        }

        private void ApplyScaling(int mode)
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            int scalePercent = 100;
            if (ParamDialog.ShowScaleSliderDialog("Image Scaling", "Enter scaling percentage (10% to 500%):", 100, out scalePercent))
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
                string scaleMethod = mode == 0 ? "Nearest Neighbor" : "Bilinear Interpolation";
                ShowNewImage(newBmp, string.Format("Scale {0}% ({1})", scalePercent, mode == 0 ? "Nearest" : "Bilinear"),
                    string.Format("Algorithm: Image Scaling\nScale Ratio: {0}%\nInterpolation Mode: {1}", scalePercent, scaleMethod),
                    "Image scaling uses a resampling mechanism to change the physical size of an image. Nearest neighbor interpolation maps directly to the closest original pixel, which is the fastest method, but produces visible jagged edges and pixelation when scaled up. Bilinear interpolation performs two-way distance weighted interpolation on surrounding 2x2 pixels, resulting in smooth edges. Coordinates are clamped and aligned at the center to prevent boundary overflows and black borders.");
            }
        }

        private void ApplyRotation()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            RotateImageForm rotForm = new RotateImageForm(this, activeChild.pBitmap);
            rotForm.pf1 = this.stStripLabel;
            rotForm.MdiParent = this;
            rotForm.Show();
        }

        private void ApplyOtsu()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            Bitmap bmpToUse = activeChild.pBitmap;
            if (!IsImageActuallyGrayscale(bmpToUse) || bmpToUse.PixelFormat != PixelFormat.Format8bppIndexed)
            {
                Bitmap gray8bpp = ConvertTo8bppGrayscale(bmpToUse);
                ShowNewImage(gray8bpp, "Auto Grayscale Conversion (8-bit)", 
                    "Algorithm: Convert to Grayscale (8-bit)", 
                    "Since this algorithm requires a standard 8-bit grayscale image input, the system has automatically converted the image to Format8bppIndexed format for you.");
                activeChild = this.ActiveMdiChild as MSForm;
                if (activeChild != null) bmpToUse = activeChild.pBitmap;
                else bmpToUse = gray8bpp;
            }

            Bitmap bmp = bmpToUse;
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
            ShowNewImage(newBmp, "Otsu Threshold Binarized",
                "Algorithm: Otsu Thresholding",
                "Otsu's method is a statistics-based automatic threshold selection algorithm. It iterates through all possible thresholds from 0 to 255 to compute the between-class variance when partitioning pixels into foreground and background. When the between-class variance is maximized, it selects this threshold as the optimal binarization point to separate the target object from the background.");
        }

        private void ApplyManualThreshold()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            Bitmap bmpToUse = activeChild.pBitmap;
            if (!IsImageActuallyGrayscale(bmpToUse) || bmpToUse.PixelFormat != PixelFormat.Format8bppIndexed)
            {
                Bitmap gray8bpp = ConvertTo8bppGrayscale(bmpToUse);
                ShowNewImage(gray8bpp, "Auto Grayscale Conversion (8-bit)", 
                    "Algorithm: Convert to Grayscale (8-bit)", 
                    "Since this algorithm requires a standard 8-bit grayscale image input, the system has automatically converted the image to Format8bppIndexed format for you.");
                activeChild = this.ActiveMdiChild as MSForm;
                if (activeChild != null) bmpToUse = activeChild.pBitmap;
                else bmpToUse = gray8bpp;
            }

            ManualThresholdForm mtForm = new ManualThresholdForm(this, bmpToUse);
            mtForm.pf1 = this.stStripLabel;
            mtForm.MdiParent = this;
            mtForm.Show();
        }

        private void ApplyEdge(int mode)
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            Bitmap bmpToUse = activeChild.pBitmap;
            int tempW = bmpToUse.Width;
            int tempH = bmpToUse.Height;
            int d = 0;
            PixelFormat pf = new PixelFormat();
            ColorPalette pal = null;
            int[] fArray = dyn_bmp2array(bmpToUse, ref d, ref pf, ref pal);
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
                if (!IsImageActuallyGrayscale(bmpToUse) || bmpToUse.PixelFormat != PixelFormat.Format8bppIndexed)
                {
                    Bitmap gray8bpp = ConvertTo8bppGrayscale(bmpToUse);
                    ShowNewImage(gray8bpp, "自動轉換為灰階 (8-bit)", 
                        "套用演算法: 轉換為灰階圖像 (8-bit)", 
                        "由於該演算法需要標準 8-bit 灰階影像輸入，系統已自動為您將影像轉換為 Format8bppIndexed 灰階格式。");
                    activeChild = this.ActiveMdiChild as MSForm;
                    if (activeChild != null) bmpToUse = activeChild.pBitmap;
                    else bmpToUse = gray8bpp;
                }

                CannyForm cannyForm = new CannyForm(this, bmpToUse);
                cannyForm.pf1 = this.stStripLabel;
                cannyForm.MdiParent = this;
                cannyForm.Show();
                return;
            }

            Bitmap newBmp = dyn_array2bmp(gArray, tempW, tempH, d, pf, pal);
            if (mode == 0)
            {
                ShowNewImage(newBmp, name,
                    "Algorithm: Sobel Edge Detection",
                    "The Sobel operator is a first-order differential edge detector. It performs differentiation using two 3x3 convolution kernels in the horizontal (Gx) and vertical (Gy) directions, calculating gradient magnitude as G = sqrt(Gx^2 + Gy^2). It possesses some smoothing capabilities to suppress noise and produces bright, clear edges, employing zero padding on the outermost boundaries to keep the original size.");
            }
            else
            {
                ShowNewImage(newBmp, name);
            }
        }

        private void ApplyHoughLine()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            Bitmap bmpToUse = activeChild.pBitmap;
            int d = 0;
            PixelFormat pf = new PixelFormat();
            ColorPalette pal = null;
            dyn_bmp2array(bmpToUse, ref d, ref pf, ref pal);

            if (d != 1)
            {
                Bitmap gray8bpp = ConvertTo8bppGrayscale(bmpToUse);
                ShowNewImage(gray8bpp, "Auto Grayscale Conversion (8-bit)", 
                    "Algorithm: Convert to Grayscale (8-bit)", 
                    "Since this algorithm requires a standard 8-bit grayscale image input, the system has automatically converted the image to Format8bppIndexed format for you.");
                activeChild = this.ActiveMdiChild as MSForm;
                if (activeChild != null) bmpToUse = activeChild.pBitmap;
                else bmpToUse = gray8bpp;
            }

            HoughLineForm hlForm = new HoughLineForm(this, bmpToUse);
            hlForm.pf1 = this.stStripLabel;
            hlForm.MdiParent = this;
            hlForm.Show();
        }

        private void ApplyHoughCircle()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            Bitmap bmpToUse = activeChild.pBitmap;
            int d = 0;
            PixelFormat pf = new PixelFormat();
            ColorPalette pal = null;
            dyn_bmp2array(bmpToUse, ref d, ref pf, ref pal);

            if (d != 1)
            {
                Bitmap gray8bpp = ConvertTo8bppGrayscale(bmpToUse);
                ShowNewImage(gray8bpp, "Auto Grayscale Conversion (8-bit)", 
                    "Algorithm: Convert to Grayscale (8-bit)", 
                    "Since this algorithm requires a standard 8-bit grayscale image input, the system has automatically converted the image to Format8bppIndexed format for you.");
                activeChild = this.ActiveMdiChild as MSForm;
                if (activeChild != null) bmpToUse = activeChild.pBitmap;
                else bmpToUse = gray8bpp;
            }

            HoughCircleForm hcForm = new HoughCircleForm(this, bmpToUse);
            hcForm.pf1 = this.stStripLabel;
            hcForm.MdiParent = this;
            hcForm.Show();
        }

        private void ApplyMedianFilter()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            MedianFilterForm mForm = new MedianFilterForm(this, activeChild.pBitmap);
            mForm.pf1 = this.stStripLabel;
            mForm.MdiParent = this;
            mForm.Show();
        }
    }
}
