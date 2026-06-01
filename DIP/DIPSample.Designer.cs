namespace DIP
{
    partial class DIPSample
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.ToolStripMenuItem iPToolStripMenuItem;
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.stStripLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.oFileDlg = new System.Windows.Forms.OpenFileDialog();
            this.saveFileDialog1 = new System.Windows.Forms.SaveFileDialog();
            this.rGBtoGrayToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.histogramToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showHistogramToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.histogramEqualizationGammaValueToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.histogramEqualizationLinearToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.interpolationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.nearestNeighborInterpolationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bilinearInterpolationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rotationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.segmentationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.otsusMethodToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.edgeDetectionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.neighborhoodProcessingToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bitPlanesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.averagingFilterToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.gaussianFiltersToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            iPToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip1.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // statusStrip1
            // 
            this.statusStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.stStripLabel});
            this.statusStrip1.Location = new System.Drawing.Point(0, 548);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Padding = new System.Windows.Forms.Padding(2, 0, 19, 0);
            this.statusStrip1.Size = new System.Drawing.Size(1023, 26);
            this.statusStrip1.TabIndex = 0;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // stStripLabel
            // 
            this.stStripLabel.Name = "stStripLabel";
            this.stStripLabel.Size = new System.Drawing.Size(151, 20);
            this.stStripLabel.Text = "toolStripStatusLabel1";
            // 
            // menuStrip1
            // 
            this.menuStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            iPToolStripMenuItem,
            this.histogramToolStripMenuItem,
            this.interpolationToolStripMenuItem,
            this.neighborhoodProcessingToolStripMenuItem,
            this.rotationToolStripMenuItem,
            this.segmentationToolStripMenuItem,
            this.edgeDetectionToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Padding = new System.Windows.Forms.Padding(5, 2, 0, 2);
            this.menuStrip1.Size = new System.Drawing.Size(1023, 28);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(46, 24);
            this.fileToolStripMenuItem.Text = "&File";
            this.fileToolStripMenuItem.Click += new System.EventHandler(this.fileToolStripMenuItem_Click);
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new System.Drawing.Size(128, 26);
            this.openToolStripMenuItem.Text = "&Open";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
            // 
            // oFileDlg
            // 
            this.oFileDlg.FileName = "openFileDialog1";
            // 
            // rGBtoGrayToolStripMenuItem
            // 
            this.rGBtoGrayToolStripMenuItem.Name = "rGBtoGrayToolStripMenuItem";
            this.rGBtoGrayToolStripMenuItem.Size = new System.Drawing.Size(224, 26);
            this.rGBtoGrayToolStripMenuItem.Text = "RGBtoGray";
            this.rGBtoGrayToolStripMenuItem.Click += new System.EventHandler(this.RGBtoGrayToolStripMenuItem_Click);
            // 
            // iPToolStripMenuItem
            // 
            iPToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.rGBtoGrayToolStripMenuItem,
            this.bitPlanesToolStripMenuItem});
            iPToolStripMenuItem.Name = "iPToolStripMenuItem";
            iPToolStripMenuItem.Size = new System.Drawing.Size(35, 24);
            iPToolStripMenuItem.Text = "&IP";
            // 
            // histogramToolStripMenuItem
            // 
            this.histogramToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.showHistogramToolStripMenuItem,
            this.histogramEqualizationGammaValueToolStripMenuItem,
            this.histogramEqualizationLinearToolStripMenuItem});
            this.histogramToolStripMenuItem.Name = "histogramToolStripMenuItem";
            this.histogramToolStripMenuItem.Size = new System.Drawing.Size(93, 24);
            this.histogramToolStripMenuItem.Text = "Histogram";
            // 
            // showHistogramToolStripMenuItem
            // 
            this.showHistogramToolStripMenuItem.Name = "showHistogramToolStripMenuItem";
            this.showHistogramToolStripMenuItem.Size = new System.Drawing.Size(224, 26);
            this.showHistogramToolStripMenuItem.Text = "Show_Histogram";
            // 
            // histogramEqualizationGammaValueToolStripMenuItem
            // 
            this.histogramEqualizationGammaValueToolStripMenuItem.Name = "histogramEqualizationGammaValueToolStripMenuItem";
            this.histogramEqualizationGammaValueToolStripMenuItem.Size = new System.Drawing.Size(350, 26);
            this.histogramEqualizationGammaValueToolStripMenuItem.Text = "Histogram Equalization(Gamma Value)";
            // 
            // histogramEqualizationLinearToolStripMenuItem
            // 
            this.histogramEqualizationLinearToolStripMenuItem.Name = "histogramEqualizationLinearToolStripMenuItem";
            this.histogramEqualizationLinearToolStripMenuItem.Size = new System.Drawing.Size(350, 26);
            this.histogramEqualizationLinearToolStripMenuItem.Text = "Histogram Equalization(Linear)";
            // 
            // interpolationToolStripMenuItem
            // 
            this.interpolationToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.nearestNeighborInterpolationToolStripMenuItem,
            this.bilinearInterpolationToolStripMenuItem});
            this.interpolationToolStripMenuItem.Name = "interpolationToolStripMenuItem";
            this.interpolationToolStripMenuItem.Size = new System.Drawing.Size(109, 24);
            this.interpolationToolStripMenuItem.Text = "Interpolation";
            // 
            // nearestNeighborInterpolationToolStripMenuItem
            // 
            this.nearestNeighborInterpolationToolStripMenuItem.Name = "nearestNeighborInterpolationToolStripMenuItem";
            this.nearestNeighborInterpolationToolStripMenuItem.Size = new System.Drawing.Size(300, 26);
            this.nearestNeighborInterpolationToolStripMenuItem.Text = "Nearest Neighbor Interpolation";
            // 
            // bilinearInterpolationToolStripMenuItem
            // 
            this.bilinearInterpolationToolStripMenuItem.Name = "bilinearInterpolationToolStripMenuItem";
            this.bilinearInterpolationToolStripMenuItem.Size = new System.Drawing.Size(300, 26);
            this.bilinearInterpolationToolStripMenuItem.Text = "Bilinear Interpolation";
            // 
            // rotationToolStripMenuItem
            // 
            this.rotationToolStripMenuItem.Name = "rotationToolStripMenuItem";
            this.rotationToolStripMenuItem.Size = new System.Drawing.Size(80, 24);
            this.rotationToolStripMenuItem.Text = "Rotation";
            // 
            // segmentationToolStripMenuItem
            // 
            this.segmentationToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.otsusMethodToolStripMenuItem});
            this.segmentationToolStripMenuItem.Name = "segmentationToolStripMenuItem";
            this.segmentationToolStripMenuItem.Size = new System.Drawing.Size(116, 24);
            this.segmentationToolStripMenuItem.Text = "Segmentation";
            // 
            // otsusMethodToolStripMenuItem
            // 
            this.otsusMethodToolStripMenuItem.Name = "otsusMethodToolStripMenuItem";
            this.otsusMethodToolStripMenuItem.Size = new System.Drawing.Size(224, 26);
            this.otsusMethodToolStripMenuItem.Text = "Otsu’s Method";
            // 
            // edgeDetectionToolStripMenuItem
            // 
            this.edgeDetectionToolStripMenuItem.Name = "edgeDetectionToolStripMenuItem";
            this.edgeDetectionToolStripMenuItem.Size = new System.Drawing.Size(126, 24);
            this.edgeDetectionToolStripMenuItem.Text = "Edge Detection";
            // 
            // neighborhoodProcessingToolStripMenuItem
            // 
            this.neighborhoodProcessingToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.averagingFilterToolStripMenuItem,
            this.gaussianFiltersToolStripMenuItem});
            this.neighborhoodProcessingToolStripMenuItem.Name = "neighborhoodProcessingToolStripMenuItem";
            this.neighborhoodProcessingToolStripMenuItem.Size = new System.Drawing.Size(195, 24);
            this.neighborhoodProcessingToolStripMenuItem.Text = "Neighborhood Processing";
            // 
            // bitPlanesToolStripMenuItem
            // 
            this.bitPlanesToolStripMenuItem.Name = "bitPlanesToolStripMenuItem";
            this.bitPlanesToolStripMenuItem.Size = new System.Drawing.Size(224, 26);
            this.bitPlanesToolStripMenuItem.Text = "Bit Planes";
            // 
            // averagingFilterToolStripMenuItem
            // 
            this.averagingFilterToolStripMenuItem.Name = "averagingFilterToolStripMenuItem";
            this.averagingFilterToolStripMenuItem.Size = new System.Drawing.Size(224, 26);
            this.averagingFilterToolStripMenuItem.Text = "Averaging Filter";
            // 
            // gaussianFiltersToolStripMenuItem
            // 
            this.gaussianFiltersToolStripMenuItem.Name = "gaussianFiltersToolStripMenuItem";
            this.gaussianFiltersToolStripMenuItem.Size = new System.Drawing.Size(224, 26);
            this.gaussianFiltersToolStripMenuItem.Text = "Gaussian Filters";
            // 
            // DIPSample
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1023, 574);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.Name = "DIPSample";
            this.Text = "DIPSample";
            this.Load += new System.EventHandler(this.DIPSample_Load);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel stStripLabel;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.OpenFileDialog oFileDlg;
        private System.Windows.Forms.SaveFileDialog saveFileDialog1;
        private System.Windows.Forms.ToolStripMenuItem rGBtoGrayToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem histogramToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showHistogramToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem histogramEqualizationGammaValueToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem histogramEqualizationLinearToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem interpolationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem nearestNeighborInterpolationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bilinearInterpolationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem rotationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem segmentationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bitPlanesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem neighborhoodProcessingToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem otsusMethodToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem edgeDetectionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem averagingFilterToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem gaussianFiltersToolStripMenuItem;
    }
}