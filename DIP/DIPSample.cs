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
            this.stStripLabel.Text = "就緒 (Ready)";

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
                ToolStripMenuItem btnLap = new ToolStripMenuItem("拉普拉斯濾波 (8-Neighbors)");
                btnLap.Click += (s, e) => ApplyFilter(2);
                this.neighborhoodProcessingToolStripMenuItem.DropDownItems.Add(btnLap);
                BindTooltip(btnLap, "利用 8 鄰域拉普拉斯算子對影像進行高頻細節增強與銳化處理。");

                ToolStripMenuItem btnLog = new ToolStripMenuItem("高斯-拉普拉斯 (LoG)");
                btnLog.Click += (s, e) => ApplyFilter(3);
                this.neighborhoodProcessingToolStripMenuItem.DropDownItems.Add(btnLog);
                BindTooltip(btnLog, "結合高斯平滑與拉普拉斯二階微分，先抑噪再精確提取二階邊緣極值。");

                ToolStripMenuItem btnHB = new ToolStripMenuItem("反銳化遮罩 / 高提升 (Unsharp Masking / High-Boost)");
                btnHB.Click += (s, e) => ApplyFilter(4);
                this.neighborhoodProcessingToolStripMenuItem.DropDownItems.Add(btnHB);
                BindTooltip(btnHB, "高提升濾波，透過原圖減去模糊影像生成細節遮罩，可按權重疊加回原圖以調整銳化強度。");

                ToolStripMenuItem btnCustom = new ToolStripMenuItem("自訂 3x3 / 5x5 濾波器 (Custom Filter)...");
                btnCustom.Click += (s, e) => ApplyCustomFilter();
                this.neighborhoodProcessingToolStripMenuItem.DropDownItems.Add(btnCustom);
                BindTooltip(btnCustom, "自訂 3x3 或 5x5 的濾波器核心矩陣、除數與偏移量，對灰階影像進行鄰域卷積運算。");

                ToolStripMenuItem btnMedian = new ToolStripMenuItem("中位數濾波 (Median Filter)...");
                btnMedian.Click += (s, e) => ApplyMedianFilter();
                this.neighborhoodProcessingToolStripMenuItem.DropDownItems.Add(btnMedian);
                BindTooltip(btnMedian, "非線性空間低通濾波器，利用鄰域中位數去噪，對消除椒鹽雜訊極為有效，且能保護影像邊緣。");
            }

            // Dynamically add Sobel and Canny to Edge Detection menu
            if (this.edgeDetectionToolStripMenuItem != null)
            {
                ToolStripMenuItem btnSobel = new ToolStripMenuItem("Sobel 算子 (Sobel Operator)");
                btnSobel.Click += (s, e) => ApplyEdge(0);
                this.edgeDetectionToolStripMenuItem.DropDownItems.Add(btnSobel);
                BindTooltip(btnSobel, "使用 3x3 Sobel 算子進行一階微分，計算 X 與 Y 方向的亮度差以獲取邊緣強度。");

                ToolStripMenuItem btnCanny = new ToolStripMenuItem("Canny 邊緣偵測 (Canny Edge Detector)");
                btnCanny.Click += (s, e) => ApplyEdge(1);
                this.edgeDetectionToolStripMenuItem.DropDownItems.Add(btnCanny);
                BindTooltip(btnCanny, "高精度邊緣偵測，包含高斯濾波抑噪、計算梯度、非極大值抑制與雙閾值遲滯邊緣連接。");
            }

            // Dynamically create a new top-level menu for Hough Line/Circle Detection!
            ToolStripMenuItem houghMenu = new ToolStripMenuItem("霍夫偵測 (Hough Detection)");
            ToolStripMenuItem btnHoughLine = new ToolStripMenuItem("霍夫直線偵測 (Hough Line Detection)");
            btnHoughLine.Click += (s, e) => ApplyHoughLine();
            BindTooltip(btnHoughLine, "使用累積空間的極坐標投票機制，從 8bpp Indexed 灰階影像的邊緣中檢測直線。");

            ToolStripMenuItem btnHoughCircle = new ToolStripMenuItem("霍夫圓形偵測 (Hough Circle Detection)");
            btnHoughCircle.Click += (s, e) => ApplyHoughCircle();
            BindTooltip(btnHoughCircle, "使用梯度向量輔助的 3D 累積投票與局部最大值抑制，從 8bpp Indexed 灰階影像中檢測圓形輪廓。");

            houghMenu.DropDownItems.Add(btnHoughLine);
            houghMenu.DropDownItems.Add(btnHoughCircle);
            this.menuStrip1.Items.Add(houghMenu);

            // Wire the click event for Show Histogram

            // Grayscale equalization
            this.histogramEqualizationLinearToolStripMenuItem.Click += (s, e) => ApplyHistogramEqualization();
            // Combined Brightness, Contrast & Gamma transform (Linear & Non-Linear)
            this.histogramEqualizationGammaValueToolStripMenuItem.Click += (s, e) => ApplyBrightnessContrastGamma();
            this.histogramEqualizationGammaValueToolStripMenuItem.Text = "亮度對比與 Gamma 調整 (線性與非線性)";

            // Initialize Custom ToolTip and Timer for 1.5 seconds Hover
            hoverTimer.Interval = 1500;
            hoverTimer.Tick += HoverTimer_Tick;
            customToolTip.AutoPopDelay = 32767; // set max delay

            // Bind tooltips for sub-menus
            BindTooltip(this.openToolStripMenuItem, "載入 JPEG、BMP 或 PNG 格式的影像檔案至工作區中。");
            BindTooltip(this.rGBtoGrayToolStripMenuItem, "採用 BT.601 標準公式將 BGR 彩色影像轉換為灰階影像。包含 24-bit 預覽與標準 8-bit 輸出子選項。");
            BindTooltip(this.rGBtoGray24bitToolStripMenuItem, "採用 BT.601 標準公式將彩色影像轉換為 24-bit 灰階影像（R=G=B 通道相同但仍為 24-bit 格式），便於一般顯示預覽。");
            BindTooltip(this.rGBtoGray8bitToolStripMenuItem, "將影像轉換為標準的 8-bit 灰階格式 (Format8bppIndexed)，支援二值化、空間濾波與霍夫線圓偵測等特殊演算法。");
            BindTooltip(this.averagingFilterToolStripMenuItem, "空間低通濾波器，利用鄰域均值模糊影像，可平滑細小雜訊但會使邊緣稍微朦朧。");
            BindTooltip(this.gaussianFiltersToolStripMenuItem, "空間低通濾波器，採用高斯加權權重模板，進行更自然的影像平滑防噪處理。");
            BindTooltip(this.bitPlanesToolStripMenuItem, "將 8 位元灰階影像拆解為 8 個獨立的二進位位元平面，高位元平面包含主要結構，低位元平面包含細微雜訊。");
            BindTooltip(this.histogramEqualizationLinearToolStripMenuItem, "計算累積機率分佈函數 (CDF) 以自動拉伸灰階範圍，顯著提升低對比影像的整體亮暗細節。");
            BindTooltip(this.histogramEqualizationGammaValueToolStripMenuItem, "調整亮度與對比度，並支援非線性 Gamma 冪律變換，修正影像的感光曲線，支援預覽圖滑鼠平移拖曳操作。");
            BindTooltip(this.rotationToolStripMenuItem, "支援最近鄰與雙線性插值，可自由設定映射模式、旋轉角度、背景填色及原圖融入，並防範邊界裁切問題。");
            BindTooltip(this.nearestNeighborInterpolationToolStripMenuItem, "影像幾何縮放的最近鄰插值法，運算速度極快，但放大時邊緣會產生明顯的鋸齒與馬賽克感。");
            BindTooltip(this.bilinearInterpolationToolStripMenuItem, "影像幾何縮放的雙線性插值法，透過鄰近 4 點像素的距離加權內插，放大影像邊緣平滑，細節較柔和。");
            BindTooltip(this.manualThresholdToolStripMenuItem, "僅支援灰階影像，手動設定 0 到 255 的門檻值，將影像劃分為黑 (0) 與白 (255) 兩大區塊，具備即時預覽。");
            BindTooltip(this.otsusMethodToolStripMenuItem, "僅支援灰階影像，透過最大類間變異數演算法自動尋找最佳分割閾值，精確分離前景與背景黑白交界。");

            // Bind tooltips for top-level menu titles/headers
            BindTooltip(this.fileToolStripMenuItem, "開啟與管理影像檔案，載入本機圖片以進行後續數位影像處理。");
            BindTooltip(this.histogramToolStripMenuItem, "直方圖分析與處理，包含顯示色彩統計圖與影像對比直方圖等化。");
            BindTooltip(this.interpolationToolStripMenuItem, "改變影像幾何尺寸與解析度，提供最近鄰插值與雙線性插值縮放演算法。");
            BindTooltip(this.neighborhoodProcessingToolStripMenuItem, "鄰域卷積空間濾波器，包含平均模糊、高斯平滑、拉普拉斯銳化、LoG 邊緣增強、高提升濾波以及自訂 3x3 與 5x5 核心濾波。");
            BindTooltip(this.segmentationToolStripMenuItem, "將影像前景與背景分離，提供手動設定閾值二值化與大津法自適應最佳閥值分割。");
            BindTooltip(this.edgeDetectionToolStripMenuItem, "分析影像亮度梯度的極值，提取物體邊緣輪廓，包含 Sobel 算子與高精度 Canny 演算法。");
            BindTooltip(houghMenu, "利用參數空間投票機制，從二值化邊緣影像中提取直線或圓形等幾何圖形輪廓。");

            // Locate and bind Basic Processing top-level menu (iPToolStripMenuItem is local in designer)
            foreach (ToolStripItem item in this.menuStrip1.Items)
            {
                if (item.Text != null && item.Text.Contains("基本處理"))
                {
                    BindTooltip(item, "影像基礎亮度與位元處理，包含彩色轉灰階、位元平面切片及亮度與對比調整。");
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
                lblStats.Text = "沒有作用中的影像 (No active image)";
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
                lblStats.Text = "沒有作用中的影像 (No active image)";
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
                "影像尺寸 (Image Size): {0} x {1}\n" +
                "格式 (Format): {2}\n" +
                "總像素 (Total Pixels): {3:N0}\n\n" +
                "--- 統計 (Statistics) ---\n" +
                "平均亮度 (Mean Intensity): {4:F2}\n" +
                "中位亮度 (Median Intensity): {5}\n" +
                "標準差 (Std Deviation): {6:F2}",
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
                baseStats += "\n\n--- 影像生成設定 (Image Settings) ---\n" + paramStr;
            }
            if (!string.IsNullOrEmpty(descStr))
            {
                baseStats += "\n\n--- 演算法原理說明 (Algorithm Docs) ---\n" + descStr;
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
            ShowNewImage(grayBmp, "灰階影像 (Grayscale 24-bit)",
                "套用演算法: 影像轉灰階 (24-bit)",
                "灰階轉換是將彩色影像（通常為 BGR 通道）透過特定權重公式轉換為單一亮度值的過程。本系統採用 ITU-R BT.601 標準公式：Y = 0.299 * R + 0.587 * G + 0.114 * B 進行計算。此選項輸出為 24-bit / 32-bit 彩色格式（R=G=B），便於一般顯示預覽。注意：24-bit 灰階直接儲存 BGR 數值，而 8-bit 灰階透過調色盤索引間接表示，兩者在 GDI+ 色彩管理、捨入誤差與調色盤對齊的影響下，直方圖數值可能會產生微小偏差。");
        }

        private void RGBtoGray8bitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            Bitmap gray8bpp = ConvertTo8bppGrayscale(activeChild.pBitmap);
            ShowNewImage(gray8bpp, "灰階影像 (Grayscale 8-bit)",
                "套用演算法: 影像轉灰階 (8-bit)",
                "灰階轉換是將彩色影像轉換為單一亮度灰度值的過程。此選項採用 Format8bppIndexed 格式輸出，並寫入線性漸變的 256 色灰階調色盤。以標準加權平均公式：Y = 0.299 * R + 0.587 * G + 0.114 * B 進行計算，能完整保留灰階細節且完全符合後續二值化、空間濾波與霍夫線圓偵測等特殊演算法格式需求。注意：24-bit 灰階直接儲存 BGR 數值，而 8-bit 灰階透過調色盤索引間接表示，兩者在 GDI+ 色彩管理、捨入誤差與調色盤對齊的影響下，直方圖數值可能會產生微小偏差。");
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
                ShowNewImage(gray8bpp, "自動轉換為灰階 (8-bit)", 
                    "套用演算法: 轉換為灰階圖像 (8-bit)", 
                    "由於該演算法需要標準 8-bit 灰階影像輸入，系統已自動為您將影像轉換為 Format8bppIndexed 灰階格式。");
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
            ShowNewImage(newBmp, "直方圖等化 (Histogram Equalized)",
                "套用演算法: 直方圖等化 (Histogram Equalization)",
                "直方圖等化是一種利用影像累積亮度分佈函數 (CDF) 作為映射曲線的影像增強技術。它能將影像的亮度直方圖均勻拉伸到整個 0~255 的區間，從而顯著提升影像的整體對比度與暗部細節，非常適合用於光線分佈過度集中或曝光不足的影像。");
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
                paramText = "套用演算法: 平均濾波 (Mean Filter)\n核心大小: 3 x 3\n除數 (Divisor): 9.0\n偏移量 (Offset): 0.0";
                algoDescText = "平均濾波器是一種線性空間低通濾波器。它的核心係數皆為 1，並將計算後的鄰域總和除以 9。此濾波器以鄰域像素的平均值取代中心像素值，可平滑影像中的隨機細小雜訊，但代價是會使影像的邊緣和細節變得模糊。";
            }
            else if (filterType == 1)
            {
                paramText = "套用演算法: 高斯濾波 (Gaussian Filter)\n核心大小: 3 x 3\n除數 (Divisor): 16.0\n偏移量 (Offset): 0.0";
                algoDescText = "高斯濾波器是一種平滑線性低通濾波器，其核心權重符合二維高斯分佈（中心點權重最大，向外呈鐘形衰減）。相較於平均濾波，高斯濾波能在去除高頻隨機雜訊的同時，更自然地保留影像的邊緣與細部特徵。";
            }
            else if (filterType == 2)
            {
                paramText = "套用演算法: 拉普拉斯銳化 (Laplacian Sharpening)\n核心大小: 3 x 3\n除數 (Divisor): 1.0\n偏移量 (Offset): 0.0";
                algoDescText = "拉普拉斯算子是一種二階微分算子，對影像中的亮度急劇變化處（即邊緣與線條）非常敏感。本功能採用 8 鄰域拉普拉斯算子進行卷積，計算中心像素與其周圍 8 個鄰域像素的二次差值，並將產生的邊緣細節疊加回原圖，從而顯著增強邊緣對比度，使影像更加銳利清晰。";
            }
            else if (filterType == 3)
            {
                paramText = "套用演算法: 高斯-拉普拉斯濾波 (LoG Filter)\n核心大小: 5 x 5\n除數 (Divisor): 1.0\n偏移量 (Offset): 128.0";
                algoDescText = "高斯-拉普拉斯算子 (LoG) 結合了高斯低通平滑與拉普拉斯二階微分。由於拉普拉斯算子對噪訊極為敏感，LoG 演算法會先使用 5x5 的高斯核對影像進行平滑去噪，隨後再利用二階微分提取精確的邊緣極值點。它常用於影像中的特徵點偵測與多尺度邊緣分析。由於微分結果有正有負，輸出時疊加了 128 的偏移量以便於視覺化顯示。";
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
                ShowNewImage(gray8bpp, "自動轉換為灰階 (8-bit)", 
                    "套用演算法: 轉換為灰階圖像 (8-bit)", 
                    "由於該演算法需要標準 8-bit 灰階影像輸入，系統已自動為您將影像轉換為 Format8bppIndexed 灰階格式。");
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
            if (ParamDialog.ShowScaleSliderDialog("影像縮放 (Image Scaling)", "輸入縮放百分比 (Enter scaling percentage, 10% to 500%):", 100, out scalePercent))
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
                string scaleMethod = mode == 0 ? "最近鄰插值 (Nearest Neighbor)" : "雙線性插值 (Bilinear Interpolation)";
                ShowNewImage(newBmp, string.Format("縮放 {0}% ({1})", scalePercent, mode == 0 ? "最近鄰 (Nearest)" : "雙線性 (Bilinear)"),
                    string.Format("套用演算法: 影像幾何縮放 (Image Scaling)\n縮放比例: {0}%\n插值模式: {1}", scalePercent, scaleMethod),
                    "影像幾何縮放使用重採樣機制變更影像的物理尺寸。最近鄰插值直接對應到最接近的原始像素點，速度最快，但放大時會出現明顯的馬賽克方塊鋸齒；雙線性插值對目標座標周圍的 2x2 像素進行雙向距離加權插值，影像放大後邊緣平滑。底層進行了像素坐標 Clamp 與中心對齊補償，避免邊界像素溢位與黑邊偏移。");
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
                ShowNewImage(gray8bpp, "自動轉換為灰階 (8-bit)", 
                    "套用演算法: 轉換為灰階圖像 (8-bit)", 
                    "由於該演算法需要標準 8-bit 灰階影像輸入，系統已自動為您將影像轉換為 Format8bppIndexed 灰階格式。");
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
            ShowNewImage(newBmp, "大津法二值化 (Otsu Threshold Binarized)",
                "套用演算法: 大津法自動二值化 (Otsu Threshold)",
                "大津法 (Otsu) 是一種基於統計學的自動閾值計算演算法。它藉由遍歷 0~255 的所有可能閾值，計算將影像分割為前景與背景兩類時的『類間變異數 (Between-Class Variance)』。當類間變異數最大時，該閾值即為最佳二值化分割點，能最有效率地在背景與前景亮度差異明顯時分離目標物。");
        }

        private void ApplyManualThreshold()
        {
            MSForm activeChild = this.ActiveMdiChild as MSForm;
            if (activeChild == null) return;

            Bitmap bmpToUse = activeChild.pBitmap;
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
                    "套用演算法: Sobel 邊緣偵測 (Sobel Operator)",
                    "Sobel 算子是一種常用的一階微分邊緣算子。它利用兩個 3x3 卷積核分別對影像的水平 (Gx) 與垂直 (Gy) 方向進行差分運算，再透過 G = sqrt(Gx^2 + Gy^2) 近似計算像素的梯度幅值。此演算法具備一定的平滑抑噪能力，可產生明亮清晰的邊緣圖像，最外圈邊緣施加 Zero Padding 保護以維持原始影像物理尺寸。");
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
                ShowNewImage(gray8bpp, "自動轉換為灰階 (8-bit)", 
                    "套用演算法: 轉換為灰階圖像 (8-bit)", 
                    "由於該演算法需要標準 8-bit 灰階影像輸入，系統已自動為您將影像轉換為 Format8bppIndexed 灰階格式。");
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
                ShowNewImage(gray8bpp, "自動轉換為灰階 (8-bit)", 
                    "套用演算法: 轉換為灰階圖像 (8-bit)", 
                    "由於該演算法需要標準 8-bit 灰階影像輸入，系統已自動為您將影像轉換為 Format8bppIndexed 灰階格式。");
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
