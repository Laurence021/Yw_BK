namespace Yw_BK
{
    partial class FrmEntrance
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmEntrance));
            this.gbSelectSource = new System.Windows.Forms.GroupBox();
            this.btnS5 = new System.Windows.Forms.Button();
            this.btnS3 = new System.Windows.Forms.Button();
            this.btnS1 = new System.Windows.Forms.Button();
            this.btnS4 = new System.Windows.Forms.Button();
            this.btnS2 = new System.Windows.Forms.Button();
            this.gbSelectSource.SuspendLayout();
            this.SuspendLayout();
            // 
            // gbSelectSource
            // 
            resources.ApplyResources(this.gbSelectSource, "gbSelectSource");
            this.gbSelectSource.Controls.Add(this.btnS5);
            this.gbSelectSource.Controls.Add(this.btnS3);
            this.gbSelectSource.Controls.Add(this.btnS1);
            this.gbSelectSource.Controls.Add(this.btnS4);
            this.gbSelectSource.Controls.Add(this.btnS2);
            this.gbSelectSource.Name = "gbSelectSource";
            this.gbSelectSource.TabStop = false;
            // 
            // btnS5
            // 
            resources.ApplyResources(this.btnS5, "btnS5");
            this.btnS5.Name = "btnS5";
            this.btnS5.Tag = "5";
            this.btnS5.UseVisualStyleBackColor = true;
            this.btnS5.Click += new System.EventHandler(this.Button_Click);
            // 
            // btnS3
            // 
            resources.ApplyResources(this.btnS3, "btnS3");
            this.btnS3.Name = "btnS3";
            this.btnS3.Tag = "3";
            this.btnS3.UseVisualStyleBackColor = true;
            this.btnS3.Click += new System.EventHandler(this.Button_Click);
            // 
            // btnS1
            // 
            resources.ApplyResources(this.btnS1, "btnS1");
            this.btnS1.Name = "btnS1";
            this.btnS1.Tag = "1";
            this.btnS1.UseVisualStyleBackColor = true;
            this.btnS1.Click += new System.EventHandler(this.Button_Click);
            // 
            // btnS4
            // 
            resources.ApplyResources(this.btnS4, "btnS4");
            this.btnS4.Name = "btnS4";
            this.btnS4.Tag = "4";
            this.btnS4.UseVisualStyleBackColor = true;
            this.btnS4.Click += new System.EventHandler(this.Button_Click);
            // 
            // btnS2
            // 
            resources.ApplyResources(this.btnS2, "btnS2");
            this.btnS2.Name = "btnS2";
            this.btnS2.Tag = "2";
            this.btnS2.UseVisualStyleBackColor = true;
            this.btnS2.Click += new System.EventHandler(this.Button_Click);
            // 
            // FrmEntrance
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ControlBox = false;
            this.Controls.Add(this.gbSelectSource);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Name = "FrmEntrance";
            this.gbSelectSource.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox gbSelectSource;
        private System.Windows.Forms.Button btnS3;
        private System.Windows.Forms.Button btnS1;
        private System.Windows.Forms.Button btnS4;
        private System.Windows.Forms.Button btnS2;
        private System.Windows.Forms.Button btnS5;
    }
}