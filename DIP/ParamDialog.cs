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
            this.BackColor = Color.FromArgb(35, 35, 40);
            this.ForeColor = Color.White;
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

                Label lbl = new Label { Text = labelText, Location = new Point(20, 20), AutoSize = true, ForeColor = Color.White };
                Label lblVal = new Label { Text = defaultVal.ToString(), Location = new Point(320, 50), Size = new Size(40, 20), ForeColor = Color.CornflowerBlue, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
                TrackBar bar = new TrackBar { Minimum = min, Maximum = max, Value = defaultVal, Location = new Point(20, 50), Size = new Size(290, 45), TickStyle = TickStyle.None };
                bar.ValueChanged += (s, e) => { lblVal.Text = bar.Value.ToString(); };

                Button btnOk = new Button { Text = "確定 (OK)", DialogResult = DialogResult.OK, Location = new Point(160, 110), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60, 60, 70), ForeColor = Color.White };
                Button btnCancel = new Button { Text = "取消 (Cancel)", DialogResult = DialogResult.Cancel, Location = new Point(265, 110), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderColor = Color.FromArgb(60, 60, 70) }, ForeColor = Color.LightGray };

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
                Label lblBright = new Label { Text = "亮度偏移量 (Beta)", Location = new Point(20, 20), AutoSize = true };
                Label lblBrightVal = new Label { Text = "0", Location = new Point(360, 40), Size = new Size(45, 20), ForeColor = Color.CornflowerBlue, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
                TrackBar barBright = new TrackBar { Minimum = -255, Maximum = 255, Value = 0, Location = new Point(20, 40), Size = new Size(330, 40), TickStyle = TickStyle.None };
                barBright.ValueChanged += (s, e) => { lblBrightVal.Text = barBright.Value.ToString(); };

                // Contrast
                Label lblContrast = new Label { Text = "對比係數 (Alpha)", Location = new Point(20, 95), AutoSize = true };
                Label lblContrastVal = new Label { Text = "1.0", Location = new Point(360, 115), Size = new Size(45, 20), ForeColor = Color.CornflowerBlue, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
                TrackBar barContrast = new TrackBar { Minimum = 1, Maximum = 30, Value = 10, Location = new Point(20, 115), Size = new Size(330, 40), TickStyle = TickStyle.None };
                barContrast.ValueChanged += (s, e) => { lblContrastVal.Text = ((double)barContrast.Value / 10.0).ToString("F1"); };

                Button btnOk = new Button { Text = "套用 (Apply)", DialogResult = DialogResult.OK, Location = new Point(200, 175), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 144, 255), ForeColor = Color.White };
                Button btnCancel = new Button { Text = "取消 (Cancel)", DialogResult = DialogResult.Cancel, Location = new Point(305, 175), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderColor = Color.FromArgb(60, 60, 70) }, ForeColor = Color.LightGray };

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

                Label lblAngle = new Label { Text = "旋轉角度 (Degrees)", Location = new Point(20, 20), AutoSize = true };
                Label lblAngleVal = new Label { Text = "0", Location = new Point(320, 45), Size = new Size(45, 20), ForeColor = Color.CornflowerBlue, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
                TrackBar barAngle = new TrackBar { Minimum = -180, Maximum = 180, Value = 0, Location = new Point(20, 45), Size = new Size(290, 40), TickStyle = TickStyle.None };
                barAngle.ValueChanged += (s, e) => { lblAngleVal.Text = barAngle.Value.ToString(); };

                Label lblMode = new Label { Text = "插值模式 (Interpolation):", Location = new Point(20, 105), AutoSize = true };
                RadioButton radNN = new RadioButton { Text = "最近鄰 (Nearest)", Location = new Point(150, 100), Size = new Size(130, 24), ForeColor = Color.White };
                RadioButton radBilinear = new RadioButton { Text = "雙線性 (Bilinear)", Checked = true, Location = new Point(280, 100), Size = new Size(80, 24), ForeColor = Color.White };

                Button btnOk = new Button { Text = "旋轉 (Rotate)", DialogResult = DialogResult.OK, Location = new Point(160, 160), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 144, 255), ForeColor = Color.White };
                Button btnCancel = new Button { Text = "取消 (Cancel)", DialogResult = DialogResult.Cancel, Location = new Point(265, 160), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderColor = Color.FromArgb(60, 60, 70) }, ForeColor = Color.LightGray };

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

                Label lblMin = new Label { Text = "最小半徑 (Min Radius, pixels)", Location = new Point(20, 20), AutoSize = true };
                NumericUpDown numMin = new NumericUpDown { Minimum = 2, Maximum = 1000, Value = 10, Location = new Point(200, 18), Size = new Size(80, 25), BackColor = Color.FromArgb(50, 50, 60), ForeColor = Color.White };

                Label lblMax = new Label { Text = "最大半徑 (Max Radius, pixels)", Location = new Point(20, 65), AutoSize = true };
                NumericUpDown numMax = new NumericUpDown { Minimum = 2, Maximum = 1000, Value = 100, Location = new Point(200, 63), Size = new Size(80, 25), BackColor = Color.FromArgb(50, 50, 60), ForeColor = Color.White };

                Label lblThresh = new Label { Text = "霍夫投票門檻 (Hough Voting Threshold)", Location = new Point(20, 115), AutoSize = true };
                Label lblThreshVal = new Label { Text = "50", Location = new Point(360, 135), Size = new Size(45, 20), ForeColor = Color.CornflowerBlue, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
                TrackBar barThresh = new TrackBar { Minimum = 5, Maximum = 300, Value = 50, Location = new Point(20, 135), Size = new Size(330, 40), TickStyle = TickStyle.None };
                barThresh.ValueChanged += (s, e) => { lblThreshVal.Text = barThresh.Value.ToString(); };

                Button btnOk = new Button { Text = "偵測 (Detect)", DialogResult = DialogResult.OK, Location = new Point(200, 220), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 144, 255), ForeColor = Color.White };
                Button btnCancel = new Button { Text = "取消 (Cancel)", DialogResult = DialogResult.Cancel, Location = new Point(305, 220), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderColor = Color.FromArgb(60, 60, 70) }, ForeColor = Color.LightGray };

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

        // 5. Dynamic Bitplane Scrolling Form
        public static void ShowBitPlaneSlider(DIPSample mainForm, Bitmap originalBmp)
        {
            Form form = new Form
            {
                Text = "位元平面切割檢視器 (Bit Plane Slicing Viewer)",
                ClientSize = new Size(380, 140),
                BackColor = Color.FromArgb(35, 35, 40),
                ForeColor = Color.White,
                FormBorderStyle = FormBorderStyle.FixedToolWindow,
                StartPosition = FormStartPosition.CenterParent,
                Font = new Font("Segoe UI", 9F)
            };

            Label lbl = new Label { Text = "選擇位元平面 (Select Bit-Plane, 0 to 7):", Location = new Point(20, 20), AutoSize = true };
            Label lblVal = new Label { Text = "7", Location = new Point(320, 45), Size = new Size(40, 20), ForeColor = Color.CornflowerBlue, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            TrackBar bar = new TrackBar { Minimum = 0, Maximum = 7, Value = 7, Location = new Point(20, 45), Size = new Size(290, 40), TickStyle = TickStyle.None };

            bar.ValueChanged += (s, e) =>
            {
                lblVal.Text = bar.Value.ToString();
                mainForm.ApplyBitPlaneSlice(originalBmp, bar.Value);
            };

            form.Controls.AddRange(new Control[] { lbl, lblVal, bar });

            // Apply plane 7 initially
            mainForm.ApplyBitPlaneSlice(originalBmp, 7);

            form.Show();
        }

        // 6. Canny Threshold Settings Dialog
        public static bool ShowCannyDialog(out double low, out double high)
        {
            low = 30.0;
            high = 90.0;
            using (ParamDialog dlg = new ParamDialog())
            {
                dlg.Text = "Canny 邊緣參數 (Canny Edge Parameters)";
                dlg.ClientSize = new Size(380, 200);

                Label lblLow = new Label { Text = "低門檻 (Low Threshold)", Location = new Point(20, 20), AutoSize = true };
                Label lblLowVal = new Label { Text = "30", Location = new Point(320, 45), Size = new Size(45, 20), ForeColor = Color.CornflowerBlue, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
                TrackBar barLow = new TrackBar { Minimum = 1, Maximum = 200, Value = 30, Location = new Point(20, 45), Size = new Size(290, 40), TickStyle = TickStyle.None };
                barLow.ValueChanged += (s, e) => { lblLowVal.Text = barLow.Value.ToString(); };

                Label lblHigh = new Label { Text = "高門檻 (High Threshold)", Location = new Point(20, 100), AutoSize = true };
                Label lblHighVal = new Label { Text = "90", Location = new Point(320, 125), Size = new Size(45, 20), ForeColor = Color.CornflowerBlue, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
                TrackBar barHigh = new TrackBar { Minimum = 2, Maximum = 300, Value = 90, Location = new Point(20, 125), Size = new Size(290, 40), TickStyle = TickStyle.None };
                barHigh.ValueChanged += (s, e) => { lblHighVal.Text = barHigh.Value.ToString(); };

                Button btnOk = new Button { Text = "確定 (OK)", DialogResult = DialogResult.OK, Location = new Point(160, 160), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 144, 255), ForeColor = Color.White };
                Button btnCancel = new Button { Text = "取消 (Cancel)", DialogResult = DialogResult.Cancel, Location = new Point(265, 160), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderColor = Color.FromArgb(60, 60, 70) }, ForeColor = Color.LightGray };

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
    }
}
