namespace Yw_BK
{
    partial class FrmOddsTest
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmOddsTest));
            this.btnSave = new System.Windows.Forms.Button();
            this.ckbTest = new System.Windows.Forms.CheckBox();
            this.ckbOpen = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // btnSave
            // 
            resources.ApplyResources(this.btnSave, "btnSave");
            this.btnSave.Name = "btnSave";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.BtnSave_Click);
            // 
            // ckbTest
            // 
            resources.ApplyResources(this.ckbTest, "ckbTest");
            this.ckbTest.Name = "ckbTest";
            this.ckbTest.UseVisualStyleBackColor = true;
            this.ckbTest.CheckedChanged += new System.EventHandler(this.CkbTest_CheckedChanged);
            // 
            // ckbOpen
            // 
            resources.ApplyResources(this.ckbOpen, "ckbOpen");
            this.ckbOpen.Name = "ckbOpen";
            this.ckbOpen.UseVisualStyleBackColor = true;
            // 
            // FrmOddsTest
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.ckbOpen);
            this.Controls.Add(this.ckbTest);
            this.Controls.Add(this.btnSave);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FrmOddsTest";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Load += new System.EventHandler(this.FrmOddsTest_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.CheckBox ckbTest;
        private System.Windows.Forms.CheckBox ckbOpen;
    }
}