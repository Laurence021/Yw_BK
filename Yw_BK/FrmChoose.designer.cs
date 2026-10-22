namespace Yw_BK
{
    partial class FrmChoose
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmChoose));
            gbSelectSource = new GroupBox();
            btnRadar = new Button();
            btnMyStake = new Button();
            btnSBO = new Button();
            btnSB = new Button();
            btnJZ = new Button();
            btn365ZD = new Button();
            btnPIN = new Button();
            btnXB = new Button();
            gbSelectSource.SuspendLayout();
            SuspendLayout();
            // 
            // gbSelectSource
            // 
            gbSelectSource.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            gbSelectSource.Controls.Add(btnRadar);
            gbSelectSource.Controls.Add(btnMyStake);
            gbSelectSource.Controls.Add(btnSBO);
            gbSelectSource.Controls.Add(btnSB);
            gbSelectSource.Controls.Add(btnJZ);
            gbSelectSource.Controls.Add(btn365ZD);
            gbSelectSource.Controls.Add(btnPIN);
            gbSelectSource.Controls.Add(btnXB);
            gbSelectSource.Font = new Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point);
            gbSelectSource.Location = new Point(12, 12);
            gbSelectSource.Name = "gbSelectSource";
            gbSelectSource.Size = new Size(162, 297);
            gbSelectSource.TabIndex = 1;
            gbSelectSource.TabStop = false;
            gbSelectSource.Text = "選擇來源";
            // 
            // btnRadar
            // 
            btnRadar.Font = new Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point);
            btnRadar.Location = new Point(6, 258);
            btnRadar.Name = "btnRadar";
            btnRadar.Size = new Size(148, 30);
            btnRadar.TabIndex = 9;
            btnRadar.Tag = "9";
            btnRadar.Text = "Radar";
            btnRadar.UseVisualStyleBackColor = true;
            btnRadar.Click += Button_Click;
            // 
            // btnMyStake
            // 
            btnMyStake.Font = new Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point);
            btnMyStake.Location = new Point(6, 224);
            btnMyStake.Name = "btnMyStake";
            btnMyStake.Size = new Size(148, 30);
            btnMyStake.TabIndex = 8;
            btnMyStake.Tag = "8";
            btnMyStake.Text = "MyStake";
            btnMyStake.UseVisualStyleBackColor = true;
            btnMyStake.Click += Button_Click;
            // 
            // btnSBO
            // 
            btnSBO.Font = new Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point);
            btnSBO.Location = new Point(6, 190);
            btnSBO.Name = "btnSBO";
            btnSBO.Size = new Size(148, 30);
            btnSBO.TabIndex = 7;
            btnSBO.Tag = "7";
            btnSBO.Text = "SBO(NBA)";
            btnSBO.UseVisualStyleBackColor = true;
            btnSBO.Click += Button_Click;
            // 
            // btnSB
            // 
            btnSB.Font = new Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point);
            btnSB.Location = new Point(6, 156);
            btnSB.Name = "btnSB";
            btnSB.Size = new Size(148, 30);
            btnSB.TabIndex = 6;
            btnSB.Tag = "6";
            btnSB.Text = "SB";
            btnSB.UseVisualStyleBackColor = true;
            btnSB.Click += Button_Click;
            // 
            // btnJZ
            // 
            btnJZ.Font = new Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point);
            btnJZ.Location = new Point(6, 122);
            btnJZ.Name = "btnJZ";
            btnJZ.Size = new Size(148, 30);
            btnJZ.TabIndex = 5;
            btnJZ.Tag = "5";
            btnJZ.Text = "山";
            btnJZ.UseVisualStyleBackColor = true;
            btnJZ.Click += Button_Click;
            // 
            // btn365ZD
            // 
            btn365ZD.Font = new Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point);
            btn365ZD.Location = new Point(6, 88);
            btn365ZD.Name = "btn365ZD";
            btn365ZD.Size = new Size(148, 30);
            btn365ZD.TabIndex = 3;
            btn365ZD.Tag = "3";
            btn365ZD.Text = "365ZD";
            btn365ZD.UseVisualStyleBackColor = true;
            btn365ZD.Click += Button_Click;
            // 
            // btnPIN
            // 
            btnPIN.Font = new Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point);
            btnPIN.Location = new Point(6, 20);
            btnPIN.Name = "btnPIN";
            btnPIN.Size = new Size(148, 30);
            btnPIN.TabIndex = 1;
            btnPIN.Tag = "1";
            btnPIN.Text = "PIN";
            btnPIN.UseVisualStyleBackColor = true;
            btnPIN.Click += Button_Click;
            // 
            // btnXB
            // 
            btnXB.Font = new Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point);
            btnXB.Location = new Point(6, 54);
            btnXB.Name = "btnXB";
            btnXB.Size = new Size(148, 30);
            btnXB.TabIndex = 2;
            btnXB.Tag = "2";
            btnXB.Text = "XB";
            btnXB.UseVisualStyleBackColor = true;
            btnXB.Click += Button_Click;
            // 
            // FrmChoose
            // 
            AutoScaleDimensions = new SizeF(6F, 12F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(186, 351);
            ControlBox = false;
            Controls.Add(gbSelectSource);
            Font = new Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FrmChoose";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "FrmChoose";
            gbSelectSource.ResumeLayout(false);
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox gbSelectSource;
        private System.Windows.Forms.Button btnPIN;
        private System.Windows.Forms.Button btnXB;
        private System.Windows.Forms.Button btn365ZD;
        private System.Windows.Forms.Button btnJZ;
        private System.Windows.Forms.Button btnSB;
        private System.Windows.Forms.Button btnSBO;
        private System.Windows.Forms.Button btnMyStake;
        private System.Windows.Forms.Button btnRadar;
    }
}