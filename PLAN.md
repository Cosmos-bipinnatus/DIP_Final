# DIP-MDI 影像處理專案開發與架構設計書 (PLAN.md) - 最終確定版

本文件為根據使用者選定的功能與技術細節所編寫的最終開發規格書。本設計書著重於 **詳細的功能數學原理解釋**、**系統架構優化** 以及 **C++/C# 的 UML 介面對接合約**，確保專案的高速整合。

---

## 1. 系統整體架構 (System Architecture)

本專案採用 **混合型二層級架構**：
* **前端 UI 層 (C# .NET Windows Forms):** 負責 MDI 多視窗管理、外部檔案讀寫、UI 控制元件（軌道條滑桿、下拉選單）、以及動態渲染右側直方圖側邊欄 (Sidebar)。
* **後端運算層 (C++ Native DLL - `dip_proc.dll`):** 負責所有高運算量、像素級的影像處理演算法。透過 P/Invoke 將 C# 傳遞的像素指標直接進行高速處理，確保處理大圖也能即時預覽。

### 🔄 資料流向與定址規範 (Memory Addressing)
影像資料以一維整數陣列 `int[]` 進行雙向傳遞。定址公式採用 **Row-Major 行優先** 標準：
$$\text{Index} = (y \times W + x) \times d + c$$
其中：
* $y$: 垂直座標（列索引，$0 \le y < H$）
* $x$: 水平座標（行索引，$0 \le x < W$）
* $W$: 影像寬度 (Width)，$H$: 影像高度 (Height)
* $d$: 深度（24bpp 影像為 `3` (B, G, R)，8bpp 灰階影像為 `1`）
* $c$: 通道索引（$0 \le c < d$）

---

## 2. 核心功能技術細節與原理解釋

### 2.1 亮度與對比調整 (Brightness & Contrast Adjustment)
亮度與對比調整是基礎的點處理 (Point Processing) 技術。我們將在專案中採用**對比與亮度協同調整模型**：

1. **亮度調整 (Brightness)：**
   調整影像是整體變亮或變暗。其原理是為每個像素的灰階值加上或減去一個常數偏移量 $\beta$：
   $$g(x,y) = clip(f(x,y) + \beta)$$
   * 當 $\beta > 0$ 時，影像整體變亮；當 $\beta < 0$ 時，影像整體變暗。
   * $clip(\cdot)$ 函數確保運算結果限制在 $[0, 255]$ 之間，防止灰階溢位。

2. **對比調整 (Contrast)：**
   對比是指影像中亮部與暗部的對比度。提高對比度會讓亮的地方更亮、暗的地方更暗，拉大動態範圍。為了防止在調整對比時造成整體影像是過度曝光或變暗，我們採用**繞過中心灰階值 (127.5) 進行縮放**的先進公式：
   $$g(x,y) = clip\Big(\alpha \times \big(f(x,y) - 127.5\big) + 127.5 + \beta\Big)$$
   * $\alpha$: 對比度乘數 ($\alpha > 1$ 增加對比，$0 < \alpha < 1$ 降低對比)。
   * 先減去 $127.5$ 是將像素值平移到以中心灰階為零點，再進行 $\alpha$ 倍縮放，最後加回 $127.5$ 並加上亮度偏移 $\beta$，能達到最自然的對比度拉伸效果。

### 2.1.1 整合式 UI 視窗 (`BrightnessContrastGammaForm`)
原先分離的 `BrightnessContrastForm`（亮度與對比）與 `GammaCorrectionForm`（Gamma 校正）已合併為單一整合式 MDI 子視窗 `BrightnessContrastGammaForm`，提供以下特性：

1. **線性 / 非線性雙模式切換**：透過 RadioButton 即時切換，顯示/隱藏對應的控制項群組。
2. **線性模式 UI**：
   * Alpha 對比度滑桿（0.1~3.0，1.0 居中映射 `trackBarAlpha` 0~200，中間點 100）。
   * Beta 亮度滑桿（-255 ~ +255）。
   * 等效 Gamma 動態算式推導：$\gamma_{eq} = -\log_2(0.5\alpha + \beta/255)$，含中間計算步驟顯示。
   * 線性折線圖預覽（`picLinearCurve`），支援滑鼠點擊平移直線與拖曳增量調整 Alpha/Beta。
3. **非線性 Gamma 冪律模式 UI**：
   * Gamma 滑桿（0.1~10.0，1.0 居中映射 `trackBarGamma` 0~200，中間點 100）。分段線性映射：左半 [0.1, 1.0]，右半 [1.0, 10.0]。
   * Gamma 冪律曲線預覽（`picGammaCurve`），支援滑鼠拖曳穿透控制 $\gamma = \ln(ny)/\ln(nx)$。
4. **按鈕群組**：
   * **「確定」**：輸出處理後 Bitmap 至新 MDI 子視窗。
   * **「取消」**：關閉視窗不輸出。
   * **「重置預設值」**：依當前模式重置所有參數至預設。

---

### 2.2 影像旋轉模式 (Image Rotation)
為了確保影像旋轉後**背景填滿（補零變黑）且影像不被切掉（畫布自動擴展）**，我們實作以下精準的旋轉運算：

#### 2.2.1 旋轉三步驟流水線 (Pipeline)
旋轉運算以影像中心點為旋轉樞軸 (Pivot)，管線順序為：
$$\text{位移到原點} \longrightarrow \text{矩陣旋轉} \longrightarrow \text{返回原位}$$

1. **畫布大小計算 (Auto-canvas Sizing)：**
   若原圖寬高為 $W, H$，旋轉角度為 $\theta$ (弧度制)。為了完整包容旋轉後的影像，新畫布的寬高 $W_{new}, H_{new}$ 計算如下：
   $$W_{new} = |W \cos \theta| + |H \sin \theta|$$
   $$H_{new} = |W \sin \theta| + |H \cos \theta|$$

2. **反向映射定址與三步驟平移 (Backward Mapping)：**
   為避免旋轉產生空洞點，我們從新畫布的坐標 $(x', y')$ 反向尋找原圖坐標 $(x, y)$：
   * **步驟 1 (位移到原點):** 將新畫布坐標平移到新畫布中心：
     $$x'_c = x' - \frac{W_{new}}{2}, \quad y'_c = y' - \frac{H_{new}}{2}$$
   * **步驟 2 (逆向旋轉):** 將中心坐標乘上逆向旋轉矩陣：
     $$\begin{bmatrix} x_c \\ y_c \end{bmatrix} = \begin{bmatrix} \cos \theta & \sin \theta \\ -\sin \theta & \cos \theta \end{bmatrix} \begin{bmatrix} x'_c \\ y'_c \end{bmatrix}$$
     即：
     $$x_c = x'_c \cos \theta + y'_c \sin \theta$$
     $$y_c = -x'_c \sin \theta + y'_c \cos \theta$$
   * **步驟 3 (返回原位):** 將中心坐標平移回原圖坐標系：
     $$x = x_c + \frac{W}{2}, \quad y = y_c + \frac{H}{2}$$

3. **邊界檢查與背景填滿：**
   若計算出的原圖坐標 $(x, y)$ 落在 $[0, W-1] \times [0, H-1]$ 之外，則新畫布對應像素設定為背景色（黑色，0）；若在範圍內，則進行插值。

#### 2.2.2 插值選項 (Interpolation Options)
* **最近鄰插值 (Nearest Neighbor):** 像素值直接取最靠近的整數坐標點 $f(round(x), round(y))$。速度極快，但邊緣會有鋸齒。
* **雙線性插值 (Bilinear):** 取坐標周圍四個鄰近像素進行加權雙線性內插，邊緣極度平滑，品質高。

---

## 3. 右側直方圖側邊欄 (Sidebar Panel) 與位元切面 UI 設計

1. **直方圖側邊欄：**
   * 主視窗右側配置一常駐 Panel。
   * **僅展示灰階直方圖：** 當載入彩色影像時，在 C++ 統計直方圖前會先自動轉為灰階，再統計 $[0, 255]$ 灰階強度分佈，由 C# 使用平滑漸層填充繪製。
2. **位元切面滑桿 (Bit-plane Slider)：**
   * 當使用者對 8-bit 灰階影像進行位元面切割時，彈出一個含有 **TrackBar 滑桿 (範圍 0 ~ 7)** 的小視窗。
   * 隨著使用者拖曳滑桿，即時在視窗中更新並顯示該位元面 ($b_0$ 至 $b_7$) 的二值化結果。

---

所有像素核心皆在 C++ DLL 中以 C 語言格式導出，以下為對應的介面與 C# `DllImport` 宣告。

> [!IMPORTANT]
> **C++ 導出語法規範 (Export Syntax Specification)**
> 為了簡化開發與團隊協作，C++ 原始碼中**不使用**自訂的 `DIPPROC_API` 巨集或 `dllimport` 宣告。所有的 C++ 函式皆直接使用標準的 `__declspec(dllexport) void` 進行導出，並將所有導出函式包裹在 `extern "C" { ... }` 區塊中，以確保導出名稱為乾淨的 C 語言格式（防止名稱修飾 Name Mangling）。

### 4.1 影像轉灰階與位元切面 (Grayscale & Bit-Plane Slicing)
* **C++ Signature:**
  ```cpp
  // 影像轉灰階 (標準加權平均公式: Y = 0.299R + 0.587G + 0.114B)
  __declspec(dllexport) void encode_gray(int* f, int w, int h, int d, int* g);

  // 位元切面提取 (給定 8-bit 灰階影像，滑桿動態選擇 plane 0~7，支援原始權重與二值化放大雙模式)
  __declspec(dllexport) void bit_plane_slice(int* f, int w, int h, int d, int* g, int plane, int binarize);
  ```

### 4.2 亮度與對比調整 (Brightness & Contrast)
* **C++ Signature:**
  ```cpp
  // 亮度與對比調整 (包含繞過中心點 127.5 的先進對比度公式與亮度偏移)
  __declspec(dllexport) void adjust_brightness_contrast(int* f, int w, int h, int d, int* g, double alpha, int beta);
  ```

### 4.3 直方圖計算與等化 (Histogram & Equalization)
* **C++ Signature:**
  ```cpp
  // 計算直方圖數據 (支援 3 通道 BGR 獨立統計，單通道灰階時僅統計第一通道。B, G, R 直方圖陣列長度皆為 256)
  __declspec(dllexport) void calculate_histogram(int* f, int w, int h, int d, int* histB, int* histG, int* histR);

  // 直方圖等化 (支援單通道與 3 通道 BGR 的獨立 CDF 直方圖均衡化)
  __declspec(dllexport) void histogram_equalization(int* f, int w, int h, int d, int* g);
  ```

### 4.4 空間濾波器與邊界處理 (Spatial Filters & Boundary Handling)
* **C++ Signature:**
  ```cpp
  // 卷積空間濾波 (邊界處理一律採用補零邊界 Zero Padding)
  __declspec(dllexport) void spatial_filter(int* f, int w, int h, int d, int* g, double* kernel, int kSize, double divisor, double offset);
  ```
  * **濾波器核心核心定義：**
    * **均值平滑 (Mean Filter):** $3 \times 3$ 全 `1` 矩陣，`divisor = 9.0`, `offset = 0.0`
    * **高斯平滑 (Gaussian Filter):** $3 \times 3$ 高斯核心（如 $[1, 2, 1; 2, 4, 2; 1, 2, 1]$），`divisor = 16.0`, `offset = 0.0`
    * **拉普拉斯銳化 (Laplacian - 8鄰域):** 核心為 $[-1, -1, -1; -1, 8, -1; -1, -1, -1]$，`divisor = 1.0`, `offset = 0.0` (或加回原圖)
    * **高斯-拉普拉斯算子 (LoG):** $5 \times 5$ 常用 LoG 核心（如 $[0, 0, -1, 0, 0; 0, -1, -2, -1, 0; -1, -2, 16, -2, -1; 0, -1, -2, -1, 0; 0, 0, -1, 0, 0]$）
    * **反銳化遮罩與高提升濾波 (High-boost):** $g = A \cdot f - f_{blur}$。核心直接以線性運算實現。

### 4.5 影像幾何縮放 (Image Scaling)
* **C++ Signature:**
  ```cpp
  // 影像幾何縮放 (Nearest Neighbor & Bilinear)
  __declspec(dllexport) void scale_image(int* f, int w, int h, int d, int* g, int newW, int newH, int mode);
  ```
  * `mode`: `0` 為 Nearest Neighbor, `1` 為 Bilinear。

### 4.6 影像旋轉 (Image Rotation)
* **C++ Signature:**
  ```cpp
  // 影像任意角度旋轉 (具備畫布自動擴充、背景黑色填滿、三步驟變換、插值選擇)
  __declspec(dllexport) void rotate_image(int* f, int w, int h, int d, int* g, int newW, int newH, double angle_deg, int mode);
  ```
  * `mode`: `0` 為 Nearest Neighbor, `1` 為 Bilinear。

### 4.7 閾值分割 (Image Segmentation)
* **C++ Signature:**
  ```cpp
  // 全域手動固定門檻分割 (門檻值 T 由 C# 介面傳入)
  __declspec(dllexport) void manual_threshold(int* f, int w, int h, int d, int* g, int T);

  // Otsu 自動最佳門檻分割
  __declspec(dllexport) void otsu_threshold(int* f, int w, int h, int d, int* g);
  ```

### 4.8 邊緣檢測與霍夫偵測 (Edges & Hough Detection)
* **C++ Signature:**
  ```cpp
  // Sobel 邊緣檢測算子
  __declspec(dllexport) void detect_sobel(int* f, int w, int h, int d, int* g);

  // Canny 邊緣檢測算子 (標準4步驟: 降噪 -> 梯度方向 -> 非極大值抑制 -> 雙閾值滯後追踪)
  __declspec(dllexport) void detect_canny(int* f, int w, int h, int d, int* g, double lowThresh, double highThresh);

  // 霍夫直線檢測 (邊緣化後投票，並在輸出圖 g 上以顯眼紅色疊加繪製直線)
  __declspec(dllexport) void detect_lines_hough(int* f, int w, int h, int d, int* g, int houghThreshold);

  // 霍夫圓形檢測 (偵測圓形，並在輸出圖 g 上以顯眼紅色繪製圓形邊緣)
  __declspec(dllexport) void detect_circles_hough(int* f, int w, int h, int d, int* g, int rMin, int rMax, int houghThreshold);
  ```

---

## 5. C# 端 P/Invoke 完整宣告 (DIP/DIPSample.cs)

在 C# 專案中，所有的 P/Invoke 宣告直接置於 `DIP/DIPSample.cs` 的頂部（而非原本設計的 `NativeMethods.cs`），以方便 MDI 主程式直接呼叫。其宣告如下：

```csharp
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
```

---

## 6. Standard Images 測試影像與演算法配對指南

專案架構中的 `Standard Images` 資料夾包含豐富的影像資源（分布於 `ISample00`、`ISample01`、`ISample02` 中）。為了方便各演算法的開發與精確驗證，以下整理出最合適的測試影像對照表與測試目的：

| 演算法功能 | 適合測試的影像類型 | 推薦的測試影像 | 測試目的 / 驗證目標 |
| :--- | :--- | :--- | :--- |
| **影像轉灰階** <br>(`encode_gray`) | 24-bit 彩色影像 (BGR) | `ISample02/RGB_iris.bmp` <br> `ISample02/Lena256_24bits.bmp` | 驗證三通道像素點能依 $Y = 0.299R + 0.587G + 0.114B$ 精準轉為灰階，且不發生溢位或偏色。 |
| **位元平面切片** <br>(`bit_plane_slice`) | 8-bit 灰階影像（包含專門設計的位元測試圖） | `ISample02/Bitplanes.bmp` <br> `ISample02/Lena256.bmp` <br> `ISample02/cameraman.bmp` | 驗證 0~7 位元面的二值化/原始權重提取。`Bitplanes.bmp` 的色塊在特定 bit 會有規則的黑白條紋與區塊，是極佳的直觀測試圖。彩色圖應觸發 UI 攔截警示。 |
| **亮度與對比調整** <br>(`adjust_brightness_contrast`) | 低對比度、亮度不均勻的影像，或曝光不足影像 | `ISample00/blurry_moon.tif` <br> `ISample00/pout.tif` <br> `ISample01/Lena256.bmp` | 測試 $\alpha$（對比）與 $\beta$（亮度）調整。特別是低對比度的月球和 pout 影像，能在此演算法調整後呈現更清晰的細節。 |
| **直方圖計算與等化** <br>(`calculate_histogram`<br>`histogram_equalization`) | 直方圖分佈窄（低對比）的灰階或彩色影像 | `ISample00/blurry_moon.tif` <br> `ISample00/pout.tif` <br> `ISample01/Lena256.bmp` <br> `ISample02/cameraman.bmp` | 驗證等化後能拉伸直方圖，使 CDF 分佈均勻。右側 Sidebar 在切換影像時應即時繪製對應通道之直方圖。 |
| **空間濾波器** <br>(`spatial_filter`) | 1. 含噪影像 (椒鹽或高斯雜訊) <br> 2. 模糊或需銳化的影像 | **平滑/降噪：** `ISample01/Lena256_salt.bmp` (椒鹽雜訊), `ISample01/Pepper256.BMP` <br> **銳化/邊緣：** `ISample02/cameraman.bmp` | 驗證均值/高斯濾波器的去噪效果，以及拉普拉斯/LoG 核心的銳化邊緣提取。同時需驗證邊界處理為 Zero Padding，觀察影像邊緣是否會變黑或有平滑過渡。 |
| **幾何縮放** <br>(`scale_image`) | 具有規律、高頻條紋或細緻輪廓的影像 | `ISample01/h.BMP` (水平) <br> `ISample01/v.BMP` (垂直) <br> `ISample01/d.BMP` (對角) <br> `ISample02/cameraman.bmp` | 比對「最近鄰插值」產生的鋸齒狀 (Aliasing) 效果與「雙線性插值」的平滑插值效果。 |
| **幾何旋轉** <br>(`rotate_image`) | 具有格狀或條紋之影像，或具明顯方向性的影像 | `ISample01/h.BMP` <br> `ISample01/v.BMP` <br> `ISample01/d.BMP` <br> `ISample02/cameraman.bmp` | 驗證自動畫布擴展功能（影像旋轉不被切除），以及背景是否以黑色 (0) 補零填滿。同時比對兩種插值法的邊緣細緻度。 |
| **閾值分割** <br>(`manual_threshold`<br>`otsu_threshold`) | 前景與背景有強烈對比且直方圖具雙峰特性的影像 | `ISample00/rice.bmp` (黑底白米) <br> `ISample00/bacteria.bmp` (細菌) <br> `ISample00/coins.tif` (硬幣) | 驗證手動閥值 $T$ 切割的效果，以及 Otsu 自動閥值演算法是否能精準找到黑白交界的最優閥值。 |
| **邊緣檢測** <br>(`detect_sobel`<br>`detect_canny`) | 輪廓清晰、有漸進灰階變化的影像 | `ISample02/cameraman.bmp` <br> `ISample01/Lena256.bmp` | 觀察 Sobel 單純梯度計算產生的寬邊緣，與 Canny（非極大值抑制與雙閾值滯後追踪）產生的單像素極細邊緣之品質差異。 |
| **霍夫直線檢測** <br>(`detect_lines_hough`) | 包含明顯直線、格線結構的影像 | `ISample01/h.BMP` <br> `ISample01/v.BMP` <br> `ISample01/d.BMP` <br> `ISample00/small-squares.bmp` | 驗證演算法是否能在參數空間 $(\rho, \theta)$ 正確投票，並在輸出圖中疊加繪製紅色直線。 |
| **霍夫圓形檢測** <br>(`detect_circles_hough`) | 包含多個完整圓形或氣泡的影像 | `ISample00/bubbles.bmp` <br> `ISample00/circles.tif` <br> `ISample00/coins.tif` | 驗證 $(\alpha, \beta, r)$ 投票是否能準確標定圓心與半徑，並在影像中以紅色圓形圈出。 |

