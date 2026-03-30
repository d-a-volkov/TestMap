namespace TestMap
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            gMapControl = new GMap.NET.WindowsForms.GMapControl();
            toolStrip1 = new ToolStrip();
            toolStripButton_New = new ToolStripButton();
            toolStripButton_Clear = new ToolStripButton();
            toolStripButton_Load = new ToolStripButton();
            toolStripButton_Save = new ToolStripButton();
            openFileDialog1 = new OpenFileDialog();
            saveFileDialog1 = new SaveFileDialog();
            toolStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // gMapControl
            // 
            gMapControl.Bearing = 0F;
            gMapControl.CanDragMap = true;
            gMapControl.Dock = DockStyle.Fill;
            gMapControl.EmptyTileColor = Color.Navy;
            gMapControl.GrayScaleMode = false;
            gMapControl.HelperLineOption = GMap.NET.WindowsForms.HelperLineOptions.DontShow;
            gMapControl.LevelsKeepInMemory = 5;
            gMapControl.Location = new Point(0, 25);
            gMapControl.Margin = new Padding(4, 3, 4, 3);
            gMapControl.MarkersEnabled = true;
            gMapControl.MaxZoom = 2;
            gMapControl.MinZoom = 2;
            gMapControl.MouseWheelZoomEnabled = true;
            gMapControl.MouseWheelZoomType = GMap.NET.MouseWheelZoomType.MousePositionAndCenter;
            gMapControl.Name = "gMapControl";
            gMapControl.NegativeMode = false;
            gMapControl.PolygonsEnabled = true;
            gMapControl.RetryLoadTile = 0;
            gMapControl.RoutesEnabled = true;
            gMapControl.ScaleMode = GMap.NET.WindowsForms.ScaleModes.Integer;
            gMapControl.SelectedAreaFillColor = Color.FromArgb(33, 65, 105, 225);
            gMapControl.ShowTileGridLines = false;
            gMapControl.Size = new Size(933, 494);
            gMapControl.TabIndex = 0;
            gMapControl.Zoom = 0D;
            // 
            // toolStrip1
            // 
            toolStrip1.Items.AddRange(new ToolStripItem[] { toolStripButton_New, toolStripButton_Clear, toolStripButton_Load, toolStripButton_Save });
            toolStrip1.Location = new Point(0, 0);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Size = new Size(933, 25);
            toolStrip1.TabIndex = 1;
            toolStrip1.Text = "toolStrip1";
            // 
            // toolStripButton_New
            // 
            toolStripButton_New.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStripButton_New.Image = (Image)resources.GetObject("toolStripButton_New.Image");
            toolStripButton_New.ImageTransparentColor = Color.Magenta;
            toolStripButton_New.Name = "toolStripButton_New";
            toolStripButton_New.Size = new Size(35, 22);
            toolStripButton_New.Text = "New";
            toolStripButton_New.Click += toolStripButton_New_Click;
            // 
            // toolStripButton_Clear
            // 
            toolStripButton_Clear.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStripButton_Clear.Image = (Image)resources.GetObject("toolStripButton_Clear.Image");
            toolStripButton_Clear.ImageTransparentColor = Color.Magenta;
            toolStripButton_Clear.Name = "toolStripButton_Clear";
            toolStripButton_Clear.Size = new Size(38, 22);
            toolStripButton_Clear.Text = "Clear";
            toolStripButton_Clear.Click += toolStripButton_Clear_Click;
            // 
            // toolStripButton_Load
            // 
            toolStripButton_Load.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStripButton_Load.Image = (Image)resources.GetObject("toolStripButton_Load.Image");
            toolStripButton_Load.ImageTransparentColor = Color.Magenta;
            toolStripButton_Load.Name = "toolStripButton_Load";
            toolStripButton_Load.Size = new Size(37, 22);
            toolStripButton_Load.Text = "Load";
            toolStripButton_Load.Click += toolStripButton_Load_Click;
            // 
            // toolStripButton_Save
            // 
            toolStripButton_Save.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStripButton_Save.Image = (Image)resources.GetObject("toolStripButton_Save.Image");
            toolStripButton_Save.ImageTransparentColor = Color.Magenta;
            toolStripButton_Save.Name = "toolStripButton_Save";
            toolStripButton_Save.Size = new Size(35, 22);
            toolStripButton_Save.Text = "Save";
            toolStripButton_Save.Click += toolStripButton_Save_Click;
            // 
            // openFileDialog1
            // 
            openFileDialog1.Filter = "json|*.json";
            // 
            // saveFileDialog1
            // 
            saveFileDialog1.Filter = "json|*.json";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(933, 519);
            Controls.Add(gMapControl);
            Controls.Add(toolStrip1);
            Margin = new Padding(4, 3, 4, 3);
            Name = "MainForm";
            Text = "OSM Route Viewer";
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ToolStrip toolStrip1;
        private ToolStripButton toolStripButton_Clear;
        private ToolStripButton toolStripButton_Load;
        private ToolStripButton toolStripButton_New;
        private OpenFileDialog openFileDialog1;
        private ToolStripButton toolStripButton_Save;
        private SaveFileDialog saveFileDialog1;
    }
}
