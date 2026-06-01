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

## 4. 詳細函式結構與 API 介面定義 (API Specification)

所有像素核心皆在 C++ DLL 中以 C 語言格式導出，以下為對應的介面與 C# `DllImport` 宣告。

### 4.1 影像轉灰階與位元切面 (Grayscale & Bit-Plane Slicing)
* **C++ Signature:**
  ```cpp
  // 影像轉灰階 (標準加權平均公式: Y = 0.299R + 0.587G + 0.114B)
  __declspec(dllexport) void encode_gray(int* f, int w, int h, int d, int* g);

  // 位元切面提取 (給定8bit灰階影像，滑桿動態選擇 plane 0~7)
  __declspec(dllexport) void bit_plane_slice(int* f, int w, int h, int d, int* g, int plane);
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
  // 計算灰階直方圖數據 (histGray大小為 256)
  __declspec(dllexport) void calculate_histogram(int* f, int w, int h, int d, int* histGray);

  // 灰階直方圖均衡化
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

## 5. C# 端 P/Invoke 完整宣告 (NativeMethods.cs)

在 C# 專案中將建立 `NativeMethods.cs`，其宣告如下：

```csharp
using System;
using System.Runtime.InteropServices;

namespace DIP
{
    internal static class NativeMethods
    {
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
    }
}
```
