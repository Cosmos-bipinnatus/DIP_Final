# DIP-MDI 影像處理專案開發與架構設計書 (PLAN.md) - 最終確定版

本文件為根據使用者選定的功能與技術細節所編寫的最終開發規格書。本設計書著重於 **詳細的功能數學原理解釋**、**系統架構優化** 以及 **C++/C# 的 UML 介面對接合約**，確保專案的高速整合。

---

## 1. 系統整體架構 (System Architecture)

本專案採用 **混合型二層級架構**：
* **前端 UI 層 (C# .NET Windows Forms):** 負責 MDI 多視窗管理、外部檔案讀寫、UI 控制元件（軌道條滑桿、下拉選單）、以及動態渲染右側直方圖側邊欄 (Sidebar)。
* **後端運算層 (C++ Native DLL - `dip_proc.dll`):** 負責所有高運算量、像素級的影像處理演算法。原始碼已模組化拆分為五個專職檔案（`color_ops.cpp`、`intensity_ops.cpp`、`geometry_ops.cpp`、`threshold_ops.cpp`、`filter_edge_ops.cpp`）與公用標頭檔 `image_lib.h`。透過 P/Invoke 將 C# 傳遞的像素指標直接進行高速處理，確保處理大圖也能即時預覽。

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
* **雙線性插值 (Bilinear):** 取坐標周圍四個鄰近像素進行加權雙線性內插，邊緣極度平滑，品質高。

#### 2.2.3 自訂背景色記憶與共享機制
* **全域歷史紀錄共享**: 將 `RotateImageForm.lastCustomBgColor` 提升為 `internal static` 級別，以便在對話框與輸出結果視窗（`MSForm`）兩端跨視窗共享使用者的最後選色。
* **雙端首次選色彈出**: 在預覽視窗或輸出後的結果視窗中，當使用者第一次點選「自訂」且先前無歷史選色紀錄時，均會自動彈出 `ColorDialog` 供使用者進行選色。
* **1:1 PictureBox 置中與背景融合（零裁切）**:
  為防止系統非工作區計算誤差或滾動條引起的對稱裁切，`MSForm` 內的 `pictureBox1` 設為 `Dock=None` 且強制尺寸精準與圖片完全 1:1 貼合。透過 `OnSizeChanged` 實現圖片在工作區內水平與垂直置中。
  當**未勾選**「融入原始影像」時，選擇黑色、白色、中間值或自訂色彩時，背景會與選擇透明時相同，均覆蓋整個預覽區（PictureBox/Form 視窗）；而**已勾選**「融入原始影像」時，背景色則僅覆蓋影像之 $W \times H$ 邊界，其餘外部視窗區域維持系統預設灰色。若選擇「透明」背景，視窗其餘空白部分也會平鋪棋盤底圖。

### 2.3 影像幾何縮放模式 (Image Scaling)
影像幾何縮放（Image Scaling）是用於調整影像解析度大小的基本幾何變換。在本系統中，我們提供最近鄰 (Nearest Neighbor) 與雙線性 (Bilinear) 兩種插值法：

1. **反向映射坐標計算**:
   設原影像尺寸為 $W \times H$，縮放後的新影像尺寸為 $W_{new} \times H_{new}$。
   我們自新影像坐標 $(x', y')$ 反向映射至原影像的實數坐標 $(x, y)$：
   $$x = (x' + 0.5) \times \frac{W}{W_{new}} - 0.5$$
   $$y = (y' + 0.5) \times \frac{H}{H_{new}} - 0.5$$
   * 加上並減去 $0.5$ 是為了對齊像素中心點（Center Alignment），能有效減少邊緣像素的網格偏移。

2. **最近鄰插值 (Nearest Neighbor)**:
   反向映射坐標 $(x, y)$ 被四捨五入到最接近的整數：
   $$x_{src} = clip(round(x), 0, W-1)$$
   $$y_{src} = clip(round(y), 0, H-1)$$
   * 像素值直接複製為 $f(x_{src}, y_{src})$。此方法執行速度極快，但在放大時會產生明顯的馬賽克或鋸齒效應 (Aliasing)。

3. **雙線性插值 (Bilinear)**:
   取反向映射坐標 $(x, y)$ 的四個相鄰整數像素：
   $$x_1 = clip(\lfloor x \rfloor, 0, W-1), \quad x_2 = clip(x_1 + 1, 0, W-1)$$
   $$y_1 = clip(\lfloor y \rfloor, 0, H-1), \quad y_2 = clip(y_1 + 1, 0, H-1)$$
   並計算其在水平與垂直方向的分數部分偏移：
   $$dx = x - \lfloor x \rfloor, \quad dy = y - \lfloor y \rfloor$$
   最終的插值結果為對這四個相鄰像素的雙線性加權：
   $$g(x', y') = (1-dx)(1-dy) \cdot f(x_1, y_1) + dx(1-dy) \cdot f(x_2, y_1) + (1-dx)dy \cdot f(x_1, y_2) + dx\,dy \cdot f(x_2, y_2)$$
   * **安全防護 (Clamp)**: 由於雙線性運算會讀取 $(x_2, y_2)$，若映射坐標正好落在影像最邊緣處，加上 1 將會導致越界。必須在取值前對 $x_1, x_2, y_1, y_2$ 實施嚴格的邊界 `clip` 防護，防止 `AccessViolationException` 崩潰。

4. **分段線性滑桿置中映射**:
   為了讓預設的 `100%`（等大縮放）剛好位於滑桿的物理正中央，我們在 C# 中實現了分段線性映射。滑桿值範圍為 `0` $\sim$ `200`，預設為 `100`。
   設滑桿當前值為 $S$，則縮放百分比 $P$ 計算如下：
   * 當 $S \le 100$ 時，對應 $10\% \sim 100\%$ 的縮放率：
     $$P = 10 + S \times 0.9$$
   * 當 $S > 100$ 時，對應 $100\% \sim 500\%$ 的縮放率：
     $$P = 100 + (S - 100) \times 4.0$$

### 2.4 手動門檻二值化 (Manual Thresholding)
二值化（Binarization）是影像分割（Segmentation）中最簡單且廣泛應用的一種技術。

1. **二值化基本公式**:
   將灰階影像 $f(x,y)$ 根據設定之門檻值 $T$ 轉換成黑白二值影像：
   $$g(x,y) = \begin{cases} 255 & \text{if } f(x,y) \ge T \\ 0 & \text{if } f(x,y) < T \end{cases}$$

2. **彩色影像自動處理與實質灰階檢查**:
   根據專案安全設計規範，二值化演算法應僅針對「灰階影像」操作。
   * **灰階防護**: C# 前端首先進行實質灰階檢查。若偵測到影像通道數 $d > 1$ 且存在 $R \neq G$ 或 $G \neq B$ 像素，即為彩色影像，系統會跳出 MessageBox 攔截並提示「請先將影像轉換為灰階再進行此操作！」。
   * **C++ 加權平均處理**: 在 C++ `manual_threshold` 層，若傳入的影像仍為多通道 $d \ge 3$，會使用標準 BT.601 權重公式 $Y = 0.299R + 0.587G + 0.114B$ 來動態計算每個像素的灰階值，再與閾值 $T$ 進行比較，保障執行健全性。

3. **MDI 預覽視窗 (`ManualThresholdForm`)**:
   符合「上方 PictureBox 預覽，下方參數 Panel 控制」版面規範。當使用者拖曳 TrackBar（0~255，預設 128）時，將即時重繪預覽影像，並動態重新統計直方圖數值更新至右側 Sidebar 直方圖與統計面板中。

### 2.5 大津法自動最佳門檻分割 (Otsu Thresholding)
大津二值化演算法藉由分析灰階直方圖，自動尋找最佳二值化分割閾值 T_best。
1. **最大化類間變異數公式**:
   將影像像素按閾值 t 分為背景類 C1 與前景類 C2。
   類間變異數為：sigma_b^2(t) = w1(t) * w2(t) * (mu1(t) - mu2(t))^2
   其中 w1(t) 與 w2(t) 分別為背景與前景的累積像素機率權重；mu1(t) 與 mu2(t) 分別為背景與前景的灰階均值。最佳閾值 T_best 即為使 sigma_b^2(t) 達到最大值時的 t。
2. **C# 前端實質灰階檢查**:
   與手動二值化及位元平面切片一致，Otsu 演算法僅支援灰階影像。當對彩色影像執行大津法時，C# 前端會檢查其像素一致性，若為彩色影像則彈出警示 MessageBox 予以攔截阻擋，維護系統健全度。

### 2.6 Sobel 邊緣偵測 (Sobel Edge Detection)
Sobel 運算子利用 $3 \times 3$ 卷積模板，在水平和垂直方向上計算像素亮度的一階偏導數，進而合成梯度振幅以提取影像邊緣。
1. **卷積與振幅公式**:
   * 水平差分分量 $g_x$：
     $$g_x = -p_{00} + p_{02} - 2p_{10} + 2p_{12} - p_{20} + p_{22}$$
   * 垂直差分分量 $g_y$：
     $$g_y = -p_{00} - 2p_{01} - p_{02} + p_{20} + 2p_{21} + p_{22}$$
   * 梯度振幅近似公式：
     $$M(x, y) = |g_x| + |g_y| \quad (\text{限制於 } [0, 255])$$
2. **邊界 Zero Padding 安全防禦**:
   * 為了防止 $3 \times 3$ 鄰域在影像四周最外層 1 像素寬度的區域取值越界，C++ 核心在進行邊緣檢測時，直接將邊界像素強制設為 `0`（黑邊）。這既達成了標準的 Zero Padding 邊緣處理，又徹底杜絕了非法記憶體存取導致的崩潰。
3. **免資料拷貝優化**:
   * 移除了最初將輸入陣列 `f` 完全拷貝給 `g` 的低效步驟。直接計算內部 Sobel 梯度並寫入 `g`，並對四周邊界像素單獨重置為 `0`。這消除了大圖處理時不必要的記憶體讀寫，顯著增強了快取命中率與運算速度。

---

## 3. 右側直方圖側邊欄 (Sidebar Panel) 與位元切面 UI 設計


1. **直方圖側邊欄：**
   * 主視窗右側配置一常駐 Panel。
   * **僅展示灰階直方圖：** 當載入彩色影像時，在 C++ 統計直方圖前會先自動轉為灰階，再統計 $[0, 255]$ 灰階強度分佈，由 C# 使用平滑漸層填充繪製。
2. **位元切面滑桿 (Bit-plane Slider)：**
   * 當使用者對 8-bit 灰階影像進行位元面切割時，彈出一個含有 **TrackBar 滑桿 (範圍 0 ~ 7)** 的小視窗。
   * 隨著使用者拖曳滑桿，即時在視窗中更新並顯示該位元面 ($b_0$ 至 $b_7$) 的二值化結果。
3. **選單與標題貼士功能 (ToolTips)**:
   * 游標在各核心功能選項及所有選單標題上懸停 1.5 秒後，自動彈出顯示功能之技術貼士。
   * 將 `AutoPopDelay` 設為最大值 `32767`，保證只要游標停留在選單上就不會超時隱藏；移開或點擊則即時關閉。
4. **詳細技術知識文件 (`docs.md`)**:
   * 建立並維護 [docs.md](file:///c:/Users/user/Documents/Projects/DIP_Final/docs.md) 記載已實作完成之各演算法詳細數學公式（BT.601 公式、累積 CDF 映射、類間變異數推導與 Gamma 非線性冪律模型等）、各函式指標參數物理意義，以及 C++ 底層的 LUT 查找表優化設計。

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

