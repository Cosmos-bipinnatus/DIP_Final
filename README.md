# DIP-MDI 數位影像處理系統開發與測試說明書 (README.md)

本專案是一個採用 **C# WinForms MDI 前端介面** 與 **高效能 C++ Native DLL (`dip_proc.dll`)** 混合架構的「數位影像處理 (DIP)」多視窗視窗應用程式。

此專案已針對原範例範本中「強制正方形影像」與「記憶體定址越界」等重大 Bug 進行了徹底重構，原生支援**任意解析度與長寬比的長方形 BMP 影像**，並配備常駐的**動態灰階直方圖側邊欄 (Sidebar)** 與 **即時影像數據統計面板**。

---

## 1. 系統架構與資料流向

本系統由兩個核心組件組成：
* **前端 UI 層 (`DIP` 專案 - C# .NET):** 負責視窗版面管理 (MDI)、BMP 檔案讀寫、滑桿/選單等參數控制 UI，以及動態 GDI+ 漸層直方圖與統計數據渲染。
* **後端運算層 (`DIP_proc` 專案 - C++ DLL):** 負責所有密集像素級的高效率影像處理演算法。

### 🔄 標準定址定址公式 (Row-Major Row-Major Addressing)
為支援任意長寬比的長方形影像，一維整數陣列中的像素尋址統一採用以下公式：
$$\text{Index} = (y \times W + x) \times d + c$$
* $y$: 垂直座標（列索引，$0 \le y < H$）
* $x$: 水平座標（行索引，$0 \le x < W$）
* $W$: 影像寬度 (Width)
* $d$: 深度（24bpp 影像為 `3` (B, G, R)，8bpp 灰階影像為 `1`）
* $c$: 通道索引（$0 \le c < d$）

---

## 2. C++ DLL 演算法函式介面與參數規格 (C++ API Contract)

以下為後端運算層所導出的關鍵函式結構，所有函式皆採用 `__declspec(dllexport) extern "C"` 宣告以進行 C# P/Invoke 調用。

### 2.1 影像灰階與位元面 (Grayscale & Bit-Plane Slicing)
1. **影像轉灰階 (`encode_gray`)**
   * **功能:** 將 BGR 彩色影像以標準加權平均法轉換為灰階：$Y = 0.299R + 0.587G + 0.114B$。
   * **參數:**
     * `int* f`: 輸入影像一維像素陣列指標
     * `int w`, `int h`: 影像寬度、高度
     * `int d`: 深度通道數 (3)
     * `int* g`: 輸出灰階影像陣列指標
2. **位元切面提取 (`bit_plane_slice`)**
   * **功能:** 提取 8-bit 灰階影像的單一位元面，輸出二值化結果（0 或 255）。
   * **參數:**
     * `int plane`: 欲提取的位元面索引（`0` $\sim$ `7`，可透過 C# Slider 動態拖曳即時預覽）

### 2.2 亮度與對比度調整 (`adjust_brightness_contrast`)
* **功能:** 調整影像明暗度與對比度。
* **參數:**
  * `double alpha`: 對比度係數（$\alpha > 1.0$ 增加對比；$0.0 < \alpha < 1.0$ 降低對比。若 $\alpha < 0.0$ 則觸發 **Gamma 冪律校正**，伽馬值為 $-\alpha$）。
  * `int beta`: 亮度偏移量（$\beta > 0$ 變亮；$\beta < 0$ 變暗）。
* **核心對比度公式 (繞過 127.5 中心平移):**
  $$g(x,y) = clip\Big(\alpha \times \big(f(x,y) - 127.5\big) + 127.5 + \beta\Big)$$

### 2.3 直方圖計算與等化 (Histogram & Equalization)
1. **統計直方圖數據 (`calculate_histogram`)**
   * **功能:** 高速統計影像各通道在 $[0, 255]$ 灰階強度的分佈數量。
   * **參數:**
     * `int* histB`: 藍色通道（或單通道灰階影像）的直方圖指標（長度 256）。
     * `int* histG`: 綠色通道的直方圖指標（長度 256，若為單通道可為空或不計算）。
     * `int* histR`: 紅色通道的直方圖指標（長度 256，若為單通道可為空或不計算）。
2. **直方圖等化 (`histogram_equalization`)**
   * **功能:** 計算影像的累計分佈函數 (CDF)，實現灰階影像直方圖均衡化。

### 2.4 空間濾波器與邊界補零 (`spatial_filter`)
* **功能:** 執行通用 2D 卷積空間濾波，邊界處理統一採用**補零邊界 (Zero Padding)**。
* **參數:**
  * `double* kernel`: 扁平化的一維雙精度浮點數濾波核心（如 3x3 核心大小為 9；5x5 核心大小為 25）
  * `int kSize`: 核心邊長（通常為 3 或 5）
  * `double divisor`: 權重除數（如均值平滑為 `9.0`，高斯平滑為 `16.0`）
  * `double offset`: 亮度偏移值（LoG 濾波可設為 `128.0` 以顯示負值邊緣）

### 2.5 幾何縮放與反向映射旋轉 (Geometry Transformations)
1. **影像縮放 (`scale_image`)**
   * **參數:**
     * `int newW`, `int newH`: 縮放後新影像大小
     * `int mode`: 插值模式（`0`: 最近鄰 Nearest Neighbor, `1`: 雙線性 Bilinear）
2. **影像旋轉 (`rotate_image`)**
   * **功能:** 影像旋轉，保證背景填滿（補零變黑），自動擴充畫布（影像不被切掉）。採用三步驟轉換：位移到原點 $\rightarrow$ 旋轉矩陣 $\rightarrow$ 平移回原位。
   * **參數:**
     * `double angle_deg`: 旋轉角度（角度制，$-180.0 \sim 180.0$）
     * `int mode`: 插值模式（`0`: 最近鄰, `1`: 雙線性）

### 2.6 二值化門檻分割 (Segmentation)
1. **手動固定門檻分割 (`manual_threshold`)**
   * **參數:** `int T` (使用者設定門檻值，$0 \le T \le 255$)。
2. **Otsu 自動最佳門檻分割 (`otsu_threshold`)**
   * **功能:** 自動計算類間方差最大化的最佳閾值並進行 binarization。

### 2.7 邊緣與線/圓圖形偵測 (Edges & Hough Detection)
1. **Sobel 算子 (`detect_sobel`)**
2. **Canny 邊緣檢測算子 (`detect_canny`)**
   * **功能:** 包含高斯平滑、梯度強度/方向計算、非極大值抑制 (NMS) 與雙閾值滯後追踪 4 步驟。
   * **參數:** `double lowThresh` (低閾值), `double highThresh` (高閾值)。
3. **霍夫直線檢測 (`detect_lines_hough`)**
   * **功能:** 邊緣化後進行參數空間 $(\rho, \theta)$ 累加投票，並直接在輸出影像上以 **顯眼紅色** 繪製疊加直線。
   * **參數:** `int houghThreshold` (投票閾值)。
4. **霍夫圓形檢測 (`detect_circles_hough`)**
   * **功能:** 自動尋找圓形，並以 **紅色圓圈** 疊加標記。
   * **參數:** `int rMin`, `int rMax` (半徑範圍), `int houghThreshold` (投票閾值)。

---

## 3. 編譯與測試流程 (Build & Test Flow)

為了確保專案編譯正常，您可以使用 Visual Studio GUI，或者直接使用終端機指令（本專案已重定向為 VS 18.0 / VS 2026 的原生 `v145` 工具集）：

### 3.1 終端機編譯 (MSBuild)
在專案根目錄下啟動 PowerShell，執行以下指令完成完整方案編譯：
```powershell
# 執行編譯（會自動清理並重新建置 C++ DLL 與 C# 執行檔）
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" "DIP VerB.sln" -restore /t:Build /p:Configuration=Debug /p:Platform=x86
```

> [!TIP]
> **本機無 .NET Framework 4.8 Targeting Pack 的解決方案**
> 專案已整合 `Microsoft.NETFramework.ReferenceAssemblies` NuGet 套件，若您的本機電腦（或乾淨的 CI/CD、VS Code 等開發環境）未安裝 .NET 4.8 軟體開發套件，在建置時加上 `-restore` 參數，MSBuild 便會自動還原並下載所需的 4.8 API 宣告，確保無痛編譯成功。

編譯成功後，產物如下：
* **C++ DLL:** `DIP/bin/x86/Debug/DIP_proc.dll`
* **C# 執行檔:** `DIP/bin/x86/Debug/DIP.exe`

### 3.2 功能測試流程 (Testing Flow)
1. **啟動程式:** 執行 `DIP/bin/x86/Debug/DIP.exe`。
2. **開啟影像:** 點擊選單 `File` -> `Open`，選擇任意長方形或正方形的 BMP 影像（如標準 Lena 或自訂長方形圖片）。
3. **直方圖側邊欄與淺色主題驗證:** 影像開啟後，右側 Sidebar 應自動呈現簡潔單色的直方圖與統計數值：
   * **彩色影像 (BGR)**：從上至下分層顯示藍、綠、紅三個實心通道直方圖（高度各為 `120`）。
   * **灰階影像 (d=1 或實質灰階 R=G=B)**：自動偵測並收縮為單一深灰色的灰階直方圖（高度為 `200`）。
   * 側邊欄背景與所有彈出的互動式參數調校視窗（如亮度對比、位元平面滾動視窗等）均呈現一致的淺色主題。
   * 切換不同的圖片子視窗，直方圖與統計值應即時動態更新。
4. **測試 Bit-Plane 滾動預覽:** 點擊 `IP` -> `Bit Planes`，在彈出的滑桿視窗中從 0 到 7 拖曳。左側預覽視窗應會**實時同步**呈現各個 bitplane 的二值化效果。
5. **測試幾何旋轉:** 點擊 `Rotation`，設定角度為 $45$ 度，選擇 `Bilinear` 插值。系統應會建立一幅背景為黑色填滿、且影像四周完全沒有被切掉的旋轉後新影像。
6. **測試霍夫線/圓偵測:** 點擊 `Hough Detection` 進行檢測，結果影像應會直接以紅色鮮明地圈出偵測到的直線與圓圈。

---

## 4. Git 儲存庫提交與團隊協作指南 (Git Push & Collaboration Instructions)

為便於團隊成員共同編輯與開發 C++ 影像處理演算法，本專案已將 **C++ DLL 原始碼（`DIP_proc` 目錄）納入 Git 版本控制與追蹤範圍**。

### 4.1 設定 .gitignore
我們在專案根目錄的 `.gitignore` 中配置了規則，保留 C++ 程式碼與專案檔，但排除了編譯產生的暫存檔與快取，同時忽略了 VS Code 私人的設定檔，以防干擾其他使用 Visual Studio 開發的成員：
```text
.vs/
Debug/
Release/
x64/
ipch/
*.suo
*.user
*.ncb
DIP/bin/Debug/DIP.pdb
DIP/obj/

# 排除 C++ DLL 的編譯產物與暫存檔 (Exclude C++ DLL build output and temp files)
DIP_proc/Debug/
DIP_proc/Release/
DIP_proc/x64/

!DIP/bin/Debug/DIP_proc.dll
!DIP/DIP_proc.dll

# 排除 VS Code 工作區設定檔 (Exclude VS Code settings)
.vscode/
```

### 4.2 提交並推送至 Git (Git Push)
請在 PowerShell 中執行以下 Git 指令，將所有專案檔案（包含 C++ 原始碼與 C# 專案）提交並推送至共用遠端儲存庫：
```powershell
# 將變更加入暫存區 (包含 C# 與 C++ 原始碼，不含 .vscode)
git add .

# 提交變更
git commit -m "feat: 整理 C++ 導出巨集為 dllexport 並納入 C++ 原始碼共用追蹤"

# 推送至您的遠端倉庫 (以您的實際分支為準，例如 main)
git push origin main
```
這樣就能確保團隊成員皆能拉取最新的 C++ 程式碼進行本機編譯與整合開發！
