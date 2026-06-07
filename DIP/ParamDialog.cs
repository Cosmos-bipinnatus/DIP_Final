using System;
using System.Drawing;
using System.Windows.Forms;

namespace DIP
{
    public partial class ParamDialog : Form
    {
        public ParamDialog()
        {
            InitializeComponent();
            this.BackColor = SystemColors.Control;
            this.ForeColor = Color.Black;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9F);
        }

        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(380, 240);
            this.Text = "參數輸入 (Parameter Input)";
        }

        // 1. Single Slider Dialog (e.g. Bit Plane, Manual Threshold, Hough Threshold)
        public static bool ShowSliderDialog(string title, string labelText, int min, int max, int defaultVal, out int result)
        {
            result = defaultVal;
            using (ParamDialog dlg = new ParamDialog())
            {
                dlg.Text = title;
                dlg.ClientSize = new Size(380, 160);

                Label lbl = new Label { Text = labelText, Location = new Point(20, 20), AutoSize = true, ForeColor = Color.Black };
                Label lblVal = new Label { Text = defaultVal.ToString(), Location = new Point(320, 50), Size = new Size(40, 20), ForeColor = Color.RoyalBlue, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
                TrackBar bar = new TrackBar { Minimum = min, Maximum = max, Value = defaultVal, Location = new Point(20, 50), Size = new Size(290, 45), TickStyle = TickStyle.None };
                bar.ForeColor = Color.Black;
                bar.ValueChanged += (s, e) => { lblVal.Text = bar.Value.ToString(); };

                Button btnOk = new Button { Text = "確定 (OK)", DialogResult = DialogResult.OK, Location = new Point(160, 110), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(230, 230, 235), ForeColor = Color.Black };
                Button btnCancel = new Button { Text = "取消 (Cancel)", DialogResult = DialogResult.Cancel, Location = new Point(265, 110), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderColor = Color.FromArgb(200, 200, 200) }, ForeColor = Color.FromArgb(64, 64, 64) };

                dlg.Controls.AddRange(new Control[] { lbl, lblVal, bar, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    result = bar.Value;
                    return true;
                }
                return false;
            }
        }

        // 1b. Non-linear Scale Slider Dialog where 100% is mapped to the middle (value 100 in 0~200 range)
        public static bool ShowScaleSliderDialog(string title, string labelText, int defaultVal, out int result)
        {
            result = defaultVal;
            using (ParamDialog dlg = new ParamDialog())
            {
                dlg.Text = title;
                dlg.ClientSize = new Size(380, 160);

                Label lbl = new Label { Text = labelText, Location = new Point(20, 20), AutoSize = true, ForeColor = Color.Black };
                Label lblVal = new Label { Text = defaultVal.ToString(), Location = new Point(320, 50), Size = new Size(40, 20), ForeColor = Color.RoyalBlue, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
                
                // Map defaultVal to trackbar value (0~200, center is 100)
                int initialBarVal = 100;
                if (defaultVal <= 100)
                {
                    // 10 -> 0, 100 -> 100
                    initialBarVal = (int)Math.Round((defaultVal - 10) / 90.0 * 100.0);
                }
                else
                {
                    // 100 -> 100, 500 -> 200
                    initialBarVal = 100 + (int)Math.Round((defaultVal - 100) / 400.0 * 100.0);
                }
                if (initialBarVal < 0) initialBarVal = 0;
                if (initialBarVal > 200) initialBarVal = 200;

                TrackBar bar = new TrackBar { Minimum = 0, Maximum = 200, Value = initialBarVal, Location = new Point(20, 50), Size = new Size(290, 45), TickStyle = TickStyle.None };
                bar.ForeColor = Color.Black;

                // Function to map bar value to actual scale percentage
                Func<int, int> mapVal = (v) => {
                    if (v <= 100)
                    {
                        return 10 + (int)Math.Round(v * 0.9);
                    }
                    else
                    {
                        return 100 + (int)Math.Round((v - 100) * 4.0);
                    }
                };

                bar.ValueChanged += (s, e) => { 
                    lblVal.Text = mapVal(bar.Value).ToString(); 
                };

                Button btnOk = new Button { Text = "確定 (OK)", DialogResult = DialogResult.OK, Location = new Point(160, 110), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(230, 230, 235), ForeColor = Color.Black };
                Button btnCancel = new Button { Text = "取消 (Cancel)", DialogResult = DialogResult.Cancel, Location = new Point(265, 110), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderColor = Color.FromArgb(200, 200, 200) }, ForeColor = Color.FromArgb(64, 64, 64) };

                dlg.Controls.AddRange(new Control[] { lbl, lblVal, bar, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    result = mapVal(bar.Value);
                    return true;
                }
                return false;
            }
        }

        // 2. Brightness & Contrast Dual Slider Dialog
        public static bool ShowBrightnessContrastDialog(out double alpha, out int beta)
        {
            alpha = 1.0;
            beta = 0;
            using (ParamDialog dlg = new ParamDialog())
            {
                dlg.Text = "亮度與對比 (Brightness & Contrast)";
                dlg.ClientSize = new Size(420, 220);

                // Brightness
                Label lblBright = new Label { Text = "亮度偏移量 (Beta)", Location = new Point(20, 20), AutoSize = true, ForeColor = Color.Black };
                Label lblBrightVal = new Label { Text = "0", Location = new Point(360, 40), Size = new Size(45, 20), ForeColor = Color.RoyalBlue, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
                TrackBar barBright = new TrackBar { Minimum = -255, Maximum = 255, Value = 0, Location = new Point(20, 40), Size = new Size(330, 40), TickStyle = TickStyle.None };
                barBright.ValueChanged += (s, e) => { lblBrightVal.Text = barBright.Value.ToString(); };

                // Contrast
                Label lblContrast = new Label { Text = "對比係數 (Alpha)", Location = new Point(20, 95), AutoSize = true, ForeColor = Color.Black };
                Label lblContrastVal = new Label { Text = "1.0", Location = new Point(360, 115), Size = new Size(45, 20), ForeColor = Color.RoyalBlue, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
                TrackBar barContrast = new TrackBar { Minimum = 1, Maximum = 30, Value = 10, Location = new Point(20, 115), Size = new Size(330, 40), TickStyle = TickStyle.None };
                barContrast.ValueChanged += (s, e) => { lblContrastVal.Text = ((double)barContrast.Value / 10.0).ToString("F1"); };

                Button btnOk = new Button { Text = "套用 (Apply)", DialogResult = DialogResult.OK, Location = new Point(200, 175), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 144, 255), ForeColor = Color.White };
                Button btnCancel = new Button { Text = "取消 (Cancel)", DialogResult = DialogResult.Cancel, Location = new Point(305, 175), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderColor = Color.FromArgb(200, 200, 200) }, ForeColor = Color.FromArgb(64, 64, 64) };

                dlg.Controls.AddRange(new Control[] { lblBright, lblBrightVal, barBright, lblContrast, lblContrastVal, barContrast, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    beta = barBright.Value;
                    alpha = (double)barContrast.Value / 10.0;
                    return true;
                }
                return false;
            }
        }

        // 3. Rotation Parameter Dialog (Angle and Interpolation mode)
        public static bool ShowRotationDialog(out double angle, out int mode)
        {
            angle = 0.0;
            mode = 1; // Default to Bilinear
            using (ParamDialog dlg = new ParamDialog())
            {
                dlg.Text = "影像旋轉 (Image Rotation)";
                dlg.ClientSize = new Size(380, 210);

                Label lblAngle = new Label { Text = "旋轉角度 (Degrees)", Location = new Point(20, 20), AutoSize = true, ForeColor = Color.Black };
                Label lblAngleVal = new Label { Text = "0", Location = new Point(320, 45), Size = new Size(45, 20), ForeColor = Color.RoyalBlue, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
                TrackBar barAngle = new TrackBar { Minimum = -180, Maximum = 180, Value = 0, Location = new Point(20, 45), Size = new Size(290, 40), TickStyle = TickStyle.None };
                barAngle.ValueChanged += (s, e) => { lblAngleVal.Text = barAngle.Value.ToString(); };

                Label lblMode = new Label { Text = "插值模式 (Interpolation):", Location = new Point(20, 105), AutoSize = true, ForeColor = Color.Black };
                RadioButton radNN = new RadioButton { Text = "最近鄰 (Nearest)", Location = new Point(150, 100), Size = new Size(130, 24), ForeColor = Color.Black };
                RadioButton radBilinear = new RadioButton { Text = "雙線性 (Bilinear)", Checked = true, Location = new Point(280, 100), Size = new Size(80, 24), ForeColor = Color.Black };

                Button btnOk = new Button { Text = "旋轉 (Rotate)", DialogResult = DialogResult.OK, Location = new Point(160, 160), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 144, 255), ForeColor = Color.White };
                Button btnCancel = new Button { Text = "取消 (Cancel)", DialogResult = DialogResult.Cancel, Location = new Point(265, 160), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderColor = Color.FromArgb(200, 200, 200) }, ForeColor = Color.FromArgb(64, 64, 64) };

                dlg.Controls.AddRange(new Control[] { lblAngle, lblAngleVal, barAngle, lblMode, radNN, radBilinear, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    angle = barAngle.Value;
                    mode = radBilinear.Checked ? 1 : 0;
                    return true;
                }
                return false;
            }
        }

        // 4. Hough Circle Settings Dialog
        public static bool ShowHoughCircleDialog(out int rMin, out int rMax, out int threshold)
        {
            rMin = 10;
            rMax = 100;
            threshold = 50;
            using (ParamDialog dlg = new ParamDialog())
            {
                dlg.Text = "霍夫圓形偵測參數 (Hough Circle Detection)";
                dlg.ClientSize = new Size(420, 270);

                Label lblMin = new Label { Text = "最小半徑 (Min Radius, pixels)", Location = new Point(20, 20), AutoSize = true, ForeColor = Color.Black };
                NumericUpDown numMin = new NumericUpDown { Minimum = 2, Maximum = 1000, Value = 10, Location = new Point(200, 18), Size = new Size(80, 25), BackColor = Color.White, ForeColor = Color.Black };

                Label lblMax = new Label { Text = "最大半徑 (Max Radius, pixels)", Location = new Point(20, 65), AutoSize = true, ForeColor = Color.Black };
                NumericUpDown numMax = new NumericUpDown { Minimum = 2, Maximum = 1000, Value = 100, Location = new Point(200, 63), Size = new Size(80, 25), BackColor = Color.White, ForeColor = Color.Black };

                Label lblThresh = new Label { Text = "霍夫投票門檻 (Hough Voting Threshold)", Location = new Point(20, 115), AutoSize = true, ForeColor = Color.Black };
                Label lblThreshVal = new Label { Text = "50", Location = new Point(360, 135), Size = new Size(45, 20), ForeColor = Color.RoyalBlue, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
                TrackBar barThresh = new TrackBar { Minimum = 5, Maximum = 300, Value = 50, Location = new Point(20, 135), Size = new Size(330, 40), TickStyle = TickStyle.None };
                barThresh.ValueChanged += (s, e) => { lblThreshVal.Text = barThresh.Value.ToString(); };

                Button btnOk = new Button { Text = "偵測 (Detect)", DialogResult = DialogResult.OK, Location = new Point(200, 220), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 144, 255), ForeColor = Color.White };
                Button btnCancel = new Button { Text = "取消 (Cancel)", DialogResult = DialogResult.Cancel, Location = new Point(305, 220), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderColor = Color.FromArgb(200, 200, 200) }, ForeColor = Color.FromArgb(64, 64, 64) };

                dlg.Controls.AddRange(new Control[] { lblMin, numMin, lblMax, numMax, lblThresh, lblThreshVal, barThresh, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    rMin = (int)numMin.Value;
                    rMax = (int)numMax.Value;
                    threshold = barThresh.Value;
                    return true;
                }
                return false;
            }
        }

        // 5. Dynamic Bitplane Scrolling Form is now implemented via BitPlaneSliceForm class.

        // 6. Canny Threshold Settings Dialog
        public static bool ShowCannyDialog(out double low, out double high)
        {
            low = 30.0;
            high = 90.0;
            using (ParamDialog dlg = new ParamDialog())
            {
                dlg.Text = "Canny 邊緣參數 (Canny Edge Parameters)";
                dlg.ClientSize = new Size(380, 200);

                Label lblLow = new Label { Text = "低門檻 (Low Threshold)", Location = new Point(20, 20), AutoSize = true, ForeColor = Color.Black };
                Label lblLowVal = new Label { Text = "30", Location = new Point(320, 45), Size = new Size(45, 20), ForeColor = Color.RoyalBlue, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
                TrackBar barLow = new TrackBar { Minimum = 1, Maximum = 200, Value = 30, Location = new Point(20, 45), Size = new Size(290, 40), TickStyle = TickStyle.None };
                barLow.ValueChanged += (s, e) => { lblLowVal.Text = barLow.Value.ToString(); };

                Label lblHigh = new Label { Text = "高門檻 (High Threshold)", Location = new Point(20, 100), AutoSize = true, ForeColor = Color.Black };
                Label lblHighVal = new Label { Text = "90", Location = new Point(320, 125), Size = new Size(45, 20), ForeColor = Color.RoyalBlue, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
                TrackBar barHigh = new TrackBar { Minimum = 2, Maximum = 300, Value = 90, Location = new Point(20, 125), Size = new Size(290, 40), TickStyle = TickStyle.None };
                barHigh.ValueChanged += (s, e) => { lblHighVal.Text = barHigh.Value.ToString(); };

                Button btnOk = new Button { Text = "確定 (OK)", DialogResult = DialogResult.OK, Location = new Point(160, 160), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 144, 255), ForeColor = Color.White };
                Button btnCancel = new Button { Text = "取消 (Cancel)", DialogResult = DialogResult.Cancel, Location = new Point(265, 160), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderColor = Color.FromArgb(200, 200, 200) }, ForeColor = Color.FromArgb(64, 64, 64) };

                dlg.Controls.AddRange(new Control[] { lblLow, lblLowVal, barLow, lblHigh, lblHighVal, barHigh, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    low = barLow.Value;
                    high = barHigh.Value;
                    return true;
                }
                return false;
            }
        }
        // 7. Custom 3x3 / 5x5 Filter Dialog
        public static bool ShowCustomFilterDialog(out double[] kernel, out int kSize, out double divisor, out double offset)
        {
            kernel = null;
            kSize = 3;
            divisor = 1.0;
            offset = 0.0;

            using (ParamDialog dlg = new ParamDialog())
            {
                dlg.Text = "自訂濾波器 (Custom Filter)";
                dlg.ClientSize = new Size(450, 310);

                Label lblInstructions = new Label { Text = "選擇核心大小並輸入係數 (Select kernel size & enter coefficients):", Location = new Point(20, 15), AutoSize = true, ForeColor = Color.Black };
                RadioButton rad3x3 = new RadioButton { Text = "3x3 核心", Checked = true, Location = new Point(20, 40), Size = new Size(100, 24), ForeColor = Color.Black };
                RadioButton rad5x5 = new RadioButton { Text = "5x5 核心", Location = new Point(130, 40), Size = new Size(100, 24), ForeColor = Color.Black };

                TextBox[,] txtGrid = new TextBox[5, 5];
                int startX = 20, startY = 70;
                int boxW = 40, boxH = 23;
                int gapX = 8, gapY = 8;

                for (int r = 0; r < 5; r++)
                {
                    for (int c = 0; c < 5; c++)
                    {
                        TextBox tb = new TextBox
                        {
                            Size = new Size(boxW, boxH),
                            Location = new Point(startX + c * (boxW + gapX), startY + r * (boxH + gapY)),
                            Text = (r == 2 && c == 2) ? "1" : "0",
                            TextAlign = HorizontalAlignment.Center,
                            BackColor = Color.White,
                            ForeColor = Color.Black
                        };
                        txtGrid[r, c] = tb;
                        dlg.Controls.Add(tb);
                    }
                }

                Label lblDivisor = new Label { Text = "除數 (Divisor):", Location = new Point(280, 70), AutoSize = true, ForeColor = Color.Black };
                TextBox txtDivisor = new TextBox { Text = "1.0", Location = new Point(280, 90), Size = new Size(130, 23), BackColor = Color.White, ForeColor = Color.Black };

                Label lblOffset = new Label { Text = "偏移量 (Offset):", Location = new Point(280, 130), AutoSize = true, ForeColor = Color.Black };
                TextBox txtOffset = new TextBox { Text = "0.0", Location = new Point(280, 150), Size = new Size(130, 23), BackColor = Color.White, ForeColor = Color.Black };

                Button btnOk = new Button { Text = "確定 (OK)", Location = new Point(220, 250), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 144, 255), ForeColor = Color.White };
                Button btnCancel = new Button { Text = "取消 (Cancel)", DialogResult = DialogResult.Cancel, Location = new Point(325, 250), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderColor = Color.FromArgb(200, 200, 200) }, ForeColor = Color.FromArgb(64, 64, 64) };

                Action updateGridVisibility = () =>
                {
                    bool is3 = rad3x3.Checked;
                    for (int r = 0; r < 5; r++)
                    {
                        for (int c = 0; c < 5; c++)
                        {
                            bool visible = !is3 || (r >= 1 && r <= 3 && c >= 1 && c <= 3);
                            txtGrid[r, c].Visible = visible;
                        }
                    }
                };

                rad3x3.CheckedChanged += (s, e) => updateGridVisibility();
                rad5x5.CheckedChanged += (s, e) => updateGridVisibility();
                updateGridVisibility();

                double[] parsedKernel = null;
                int parsedSize = 3;
                double parsedDiv = 1.0;
                double parsedOff = 0.0;

                btnOk.Click += (s, e) =>
                {
                    if (!double.TryParse(txtDivisor.Text, out parsedDiv))
                    {
                        MessageBox.Show("請輸入正確的除數數值！", "輸入錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    if (parsedDiv == 0.0)
                    {
                        MessageBox.Show("除數不可為 0！", "輸入錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if (!double.TryParse(txtOffset.Text, out parsedOff))
                    {
                        MessageBox.Show("請輸入正確的偏移量數值！", "輸入錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    parsedSize = rad3x3.Checked ? 3 : 5;
                    parsedKernel = new double[parsedSize * parsedSize];
                    int count = 0;

                    for (int r = 0; r < 5; r++)
                    {
                        for (int c = 0; c < 5; c++)
                        {
                            if (parsedSize == 3 && (r < 1 || r > 3 || c < 1 || c > 3))
                            {
                                continue;
                            }
                            double cellVal;
                            if (!double.TryParse(txtGrid[r, c].Text, out cellVal))
                            {
                                MessageBox.Show(string.Format("核心矩陣第 {0} 行第 {1} 列輸入有誤，請輸入數值！", r + 1, c + 1), "輸入錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                            parsedKernel[count++] = cellVal;
                        }
                    }

                    dlg.DialogResult = DialogResult.OK;
                };

                dlg.Controls.AddRange(new Control[] { lblInstructions, rad3x3, rad5x5, lblDivisor, txtDivisor, lblOffset, txtOffset, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    kernel = parsedKernel;
                    kSize = parsedSize;
                    divisor = parsedDiv;
                    offset = parsedOff;
                    return true;
                }
                return false;
            }
        }
    }
}
