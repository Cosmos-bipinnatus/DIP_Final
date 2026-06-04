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
   * **功能:** 提取 8-bit 灰階影像的單一位元面，支援保留原始權重與二值化放大雙模式，用於影像結構分析或隱寫術（如 B0 隱水印）提取。
   * **參數:**
     * `int* f`: 輸入影像一維像素陣列指標
     * `int w`, `int h`: 影像寬度、高度
     * `int d`: 深度通道數（`1` 或 `3`）
     * `int* g`: 輸出二值化/原始權重影像陣列指標
     * `int plane`: 欲提取的位元面索引（`0` $\sim$ `7`）
     * `int binarize`: 控制旗標（`0`: 輸出原始位元權重 $0$ 或 $2^{\text{plane}}$；`1`: 輸出二值化放大 $0$ 或 $255$）
   * **UI 整合與安全攔截機制:**
     * **合併式 UI 視窗**: 將滑桿 TrackBar、二值化 CheckBox 與預覽 PictureBox 整合在同一個 `BitPlaneSliceForm` MDI 子視窗中，解決了原先視窗焦點被頻繁奪取導致無法拖曳滑桿的 Bug。
     * **實質灰階檢查 (ALERT 報錯)**: C# 前端點選時會先檢查影像內容是否為灰階。若為彩色影像（即 $d > 1$ 且存在 $R \neq G$ 或 $G \neq B$ 的像素），會彈出 MessageBox 報錯攔截；若為 $d == 1$ 或已經執行過 RGB 轉灰階（$d==3$ 但 $R=G=B$），則安全放行。

### 2.2 亮度對比與 Gamma 調整 (`adjust_brightness_contrast`)
* **功能:** 整合式影像亮度、對比度與 Gamma 冪律校正調整。透過 `BrightnessContrastGammaForm` MDI 子視窗，提供線性/非線性雙模式即時預覽操作。
* **參數:**
  * `double alpha`: 對比度係數（$\alpha > 1.0$ 增加對比；$0.0 < \alpha < 1.0$ 降低對比。若 $\alpha < 0.0$ 則觸發 **Gamma 冪律校正**，伽馬值為 $-\alpha$）。
  * `int beta`: 亮度偏移量（$\beta > 0$ 變亮；$\beta < 0$ 變暗）。
* **核心對比度公式 (繞過 127.5 中心平移):**
  $$g(x,y) = clip\Big(\alpha \times \big(f(x,y) - 127.5\big) + 127.5 + \beta\Big)$$
* **整合式 UI 特性 (`BrightnessContrastGammaForm`):**
  * **線性模式**：Alpha 對比度（0.1~3.0，滑桿 1.0 居中）+ Beta 亮度（±255），含等效 Gamma 動態算式推導（$\gamma_{eq} = -\log_2(0.5\alpha + \beta/255)$）。
  * **非線性 Gamma 冪律模式**：Gamma 範圍 0.1~10.0，滑桿預設 1.0 居中。分段線性映射：左半 [0.1, 1.0]，右半 [1.0, 10.0]。
  * **曲線預覽圖互動操作**：線性模式點擊可平移直線，拖曳可調整 Alpha（水平）/ Beta（垂直）；非線性模式拖曳可讓 Gamma 曲線穿透滑鼠位置。
  * **「確定」按鈕**：輸出處理後影像至新 MDI 子視窗；**「取消」按鈕**：關閉視窗不輸出；**「重置預設值」按鈕**：依模式重置所有參數。

### 2.3 直方圖計算與等化 (Histogram & Equalization)
1. **統計直方圖數據 (`calculate_histogram`)**
   * **功能:** 高速統計影像各通道在 $[0, 255]$ 灰階強度的分佈數量。
   * **參數:**
     * `int* histB`: 藍色通道（或單通道灰階影像）的直方圖指標（長度 256）。
     * `int* histG`: 綠色通道的直方圖指標（長度 256，若為單通道可為空或不計算）。
     * `int* histR`: 紅色通道的直方圖指標（長度 256，若為單通道可為空或不計算）。
2. **直方圖等化 (`histogram_equalization`)**
   * **功能:** 計算影像的累計分佈函數 (CDF)，實現單通道灰階與三通道 BGR 影像之直方圖均衡化。
   * **參數:**
     * `int* f`: 輸入影像一維像素陣列指標
     * `int w`, `int h`: 影像寬度、高度
     * `int d`: 深度通道數（`1` 或 `3`）
     * `int* g`: 輸出等化後影像陣列指標

### 2.4 空間濾波器與邊界補零 (`spatial_filter`)
* **功能:** 執行通用 2D 卷積空間濾波，邊界處理統一採用**補零邊界 (Zero Padding)**。
* **參數:**
  * `double* kernel`: 扁平化的一維雙精度浮點數濾波核心（如 3x3 核心大小為 9；5x5 核心大小為 25）
  * `int kSize`: 核心邊長（通常為 3 或 5）
  * `double divisor`: 權重除數（如均值平滑為 `9.0`，高斯平滑為 `16.0`）
  * `double offset`: 亮度偏移值（LoG 濾波可設為 `128.0` 以顯示負值邊緣）

### 2.5 幾何縮放與反向映射旋轉 (Geometry Transformations)
1. **影像縮放 (`scale_image`)**
   * **功能:** 影像幾何縮放，支援最近鄰 (Nearest Neighbor) 與雙線性 (Bilinear) 插值。
   * **參數:**
     * `int* f`: 輸入影像一維像素陣列指標
     * `int w`, `int h`: 原始影像寬度、高度
     * `int d`: 深度通道數
     * `int* g`: 輸出縮放影像陣列指標
     * `int newW`, `int newH`: 縮放後新影像大小
     * `int mode`: 插值模式（`0`: 最近鄰 Nearest Neighbor, `1`: 雙線性 Bilinear）
   * **核心安全機制與 UI 特性:**
     * **座標 Clamp 與中心對齊 $\pm 0.5$ 像素平移**: 在 C++ `scale_image` 核心演算法中，為消除最近鄰與雙線性的網格像素漂移與邊界浮點定址越界崩潰，導入了嚴格的坐標範圍夾緊防護（Clamp）與對齊修正。
     * **分段線性滑桿置中設計**: C# 滑桿輸入範圍為 0~200，當 `value = 100` 時映射至 `100%`（預設，位於滑桿物理正中央），`0~100` 線性映射至 `10%~100%`，`100~200` 線性映射至 `100%~500%`，大幅提升操作手感。
2. **影像旋轉 (`rotate_image`)**
   * **功能:** 影像旋轉，支援自動擴充畫布（影像不被切掉），並將背景區域填滿指定色彩。
   * **參數:**
     * `int* f`: 輸入影像一維像素陣列指標
     * `int w`, `int h`: 原始影像寬高
     * `int d`: 深度通道數
     * `int* g`: 輸出旋轉影像陣列指標
     * `int newW`, `int newH`: 旋轉後新影像畫布大小
     * `double angle_deg`: 旋轉角度（角度制，$0.0 \sim 359.0$）
     * `int mode`: 插值與映射模式（`0`: 反向最近鄰, `1`: 反向雙線性, `2`: 正向映射）
     * `int bg_r`, `int bg_g`, `int bg_b`, `int bg_a`: 填補背景的 R、G、B、A (Alpha) 通道色彩數值
    * **UI 整合與歷史記憶機制:**
      * **自訂背景色記憶與共享**: 使用靜態變數 `lastCustomBgColor` 跨對話框與輸出結果視窗共享選色紀錄。
      * **首開自動彈出調色盤**: 在預覽對話框（`RotateImageForm`）與結果視窗（`MSForm`）中，若使用者勾選「自訂」且先前無歷史選色紀錄時，均會自動彈出 ColorDialog 調色盤，確保兩端的操作行為與邏輯一致。
      * **1:1 PictureBox 尺寸與自適應置中（零裁切）**: 將結果視窗中的 `pictureBox1` 設為 `Dock=None`，並強制其尺寸與圖片完全 1:1 貼合，徹底免除了系統非工作區計算誤差或滾動條導致的裁切。透過重寫 `OnSizeChanged` 與 `LayoutControls()` 實現圖片在工作區內水平與垂直自適應置中。
      * **背景無縫融合與預覽區覆蓋**: 當**未勾選**「融入原始影像」時，選擇黑色、白色、中間值或自訂色彩時，背景會與選擇透明時相同，均覆蓋整個預覽區（PictureBox/Form 視窗）；而**已勾選**「融入原始影像」時，背景色則僅覆蓋影像之 $W \times H$ 邊界，其餘外部視窗區域維持系統預設灰色。若選擇「透明」背景，視窗其餘空白部分也會平鋪棋盤底圖。


### 2.6 二值化門檻分割 (Segmentation)
1. **手動固定門檻分割 (`manual_threshold`)**
   * **功能:** 全域手動固定門檻二值化分割，依據給定的門檻值 $T$ 將影像二值化。
   * **參數:**
     * `int* f`: 輸入影像一維像素陣列指標
     * `int w`, `int h`: 影像寬高
     * `int d`: 深度通道數
     * `int* g`: 輸出二值化影像陣列指標
     * `int T`: 使用者設定門檻值 ($0 \le T \le 255$)
   * **UI 整合與安全攔截機制:**
     * **合併式 UI 預覽視窗 (`ManualThresholdForm`)**: 符合 UI 設計規範「上方為影像預覽 PictureBox，下方為參數調整操作 Panel」之 MDI 整合式表單。
     * **即時預覽 (Real-time Preview) & 直方圖連動**: 拖曳閥值滑桿（0~255，預設 128）時，將即時調用 C++ 演算法更新預覽，並連動更新右側直方圖側邊欄與統計面板。
     * **實質灰階檢查 (ALERT 攔截)**: 點選時會先檢查影像內容是否為灰階（檢查所有像素 $R=G=B$）。若為彩色影像（即 $d>1$ 且存在 $R \neq G$ 或 $G \neq B$ 像素），會彈出 MessageBox 報錯攔截，提示使用者應先執行轉灰階功能；若為灰階（或已轉灰階影像）則安全放行。
 2. **Otsu 自動最佳門檻分割 (`otsu_threshold`)**
    * **功能:** 透過最大類間變異數演算法，自適應計算出最佳分割閾值 T_best，並將影像二值化。
    * **參數:**
      * `int* f`: 輸入影像一維像素陣列指標
      * `int w`, `int h`: 影像寬高
      * `int d`: 深度通道數
      * `int* g`: 輸出二值化影像陣列指標
    * **UI 整合與安全攔截機制:**
      * **實質灰階檢查 (ALERT 攔截)**: C# 前端對彩色影像進行攔截防護並彈出警告，確保輸入資料符合單通道或實質灰階。
      * **最佳閾值自動運算**: 直方圖與統計面板自動載入並進行類間變異數最大值 t 疊代搜尋，完成分割後輸出至新視窗。


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
5. **測試亮度對比與 Gamma 調整:** 在 `影像處理(IP)` 選單中點擊「亮度對比與 Gamma 調整 (線性與非線性)」：
   * **線性模式**：拖曳 Alpha 滑桿（1.0 在中央）調整對比度，拖曳 Beta 滑桿調整亮度，觀察等效 Gamma 公式即時推導。
   * **非線性模式**：切換 RadioButton 至 Gamma 冪律，拖曳 Gamma 滑桿（1.0 在中央，範圍 0.1~10.0）觀察冪律曲線變化。
   * **曲線圖互動**：在右側曲線預覽圖中點擊/拖曳，驗證滑桿數值是否即時聯動。
   * 點擊「確定」按鈕輸出至新視窗，或「取消」關閉不輸出。
6. **測試幾何旋轉與背景色記憶:** 點擊 `Rotation`，設定角度為 $45$ 度，選擇 `Bilinear` 插值。
   * 勾選「自訂背景色」，若為首次開啟會彈出色彩調色盤，請選擇一個非黑色（如綠色）的顏色。
   * 建立一幅背景為該自訂色彩填滿的新影像。
   * 關閉該旋轉子視窗，再次點擊 `Rotation`，再次勾選「自訂」，驗證系統直接載入剛才選擇的綠色，未再次彈出調色盤（若點擊「選擇顏色」按鈕仍可手動變更顏色）。
7. **測試影像幾何縮放:** 點擊 `幾何轉換(Geometry)` -> `影像縮放(Image Scaling)`：
   * 彈出 `Scale Form`，此時預覽滑桿完美位於正中央，標示為 100%。
   * 向左拖曳至 10% 或向右拖曳至 500%，選擇插值模式（最近鄰/雙線性），點擊確定後輸出新影像。
   * 驗證輸出影像的長寬是否按比例縮放，且雙線性插值無鋸齒崩潰。
8. **測試手動門檻二值化與 Alert 攔截:** 點擊 `影像處理(IP)` -> `手動門檻二值化(Manual Thresholding)`：
   * 若目前為彩色影像，應跳出 Alert 警示 MessageBox「請先將影像轉換為灰階再進行此操作！」，並攔截阻擋。
   * 若為灰階影像（或先執行 `encode_gray` 轉為灰階後），則順利打開 `ManualThresholdForm`。
   * 拖曳閥值滑桿（預設 128），上方影像應即時預覽二值化，且右側直方圖也同步動態更新。點擊確定後輸出二值化影像。
 9. **測試霍夫線/圓偵測:** 點擊 `Hough Detection` 進行檢測，結果影像應會直接以紅色鮮明地圈出偵測到的直線與圓圈。

### 3.3 貼士說明與技術文件 (ToolTips & docs.md)
* **選單與標題貼士功能**: 系統在 Menu 每個功能選項及所有選單標題（如檔案、直方圖、幾何縮放等）上新增了滑鼠懸停 1.5 秒自動顯示貼士說明的功能。貼士在滑鼠懸停期間不會自動超時關閉（AutoPopDelay 設為最大值 32.7 秒），滑鼠移開或點選時即刻隱藏。
* **演算法技術文件 (`docs.md`)**: 於專案根目錄下建立了詳細的技術說明書 [docs.md](file:///c:/Users/user/Documents/Projects/DIP_Final/docs.md)，詳盡記載各已完成演算法之數學模型（如 BT.601 公式、累積 CDF 映射、類間變異數推導與 Gamma 非線性冪律模型等）、各函式指標參數物理意義，以及 C++ 底層的 LUT 查找表等實作細節。

---

## 4. Git 儲存庫提交與團隊協作指南 (Git Push & Collaboration Instructions)

為便於團隊成員共同編輯與開發 C++ 影像處理演算法，本專案已將 **C++ DLL 原始碼（`DIP_proc` 目錄）納入 Git 版本控制與追蹤範圍**。

### 4.1 設定 .gitignore
我們在專案根目錄的 `.gitignore` 中配置了規則，保留 C++ 程式碼與專案檔，但排除了編譯產生的暫存檔與快取（含 `.exp`、`.lib`、`.pdb`、`Thumbs.db`、`bin/`、`obj/` 等），同時忽略了 VS Code 私人的設定檔，以防干擾其他使用 Visual Studio 開發的成員：
```text
.vs/
Debug/
Release/
x64/
ipch/
*.suo
*.user
*.ncb
Thumbs.db

# C# build output & intermediate (保留 DIP_proc.dll 供部署使用)
DIP/bin/
DIP/obj/

# C++ DLL build artifacts (排除編譯中間產物，僅保留根目錄 DIP_proc.dll)
DIP_proc/Debug/
DIP_proc/Release/
DIP_proc/x64/
DIP/DIP_proc.exp
DIP/DIP_proc.lib
DIP/DIP_proc.pdb

# 保留根目錄的 DIP_proc.dll（供 P/Invoke 載入使用）
!DIP/DIP_proc.dll

# 排除 VS Code 工作區設定檔 (Exclude VS Code settings)
.vscode/

# 排除已棄用的舊版表單檔案 (Deprecated legacy form files)
DIP/BrightnessContrastForm.cs
DIP/GammaCorrectionForm.cs
```

### 4.2 提交並推送至 Git (Git Push)
請在 PowerShell 中執行以下 Git 指令，將所有專案檔案（包含 C++ 原始碼與 C# 專案）提交並推送至共用遠端儲存庫：
```powershell
# 將變更加入暫存區 (包含 C# 與 C++ 原始碼，不含 .vscode)
git add .

# 提交變更
git commit -m "feat: 整合亮度對比與Gamma調整視窗 (BrightnessContrastGammaForm)"

# 推送至您的遠端倉庫 (以您的實際分支為準，例如 main)
git push origin main
```
這樣就能確保團隊成員皆能拉取最新的 C++ 程式碼進行本機編譯與整合開發！

---

## 5. Standard Images 測試影像與演算法配對指南

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
