namespace Yw_BK
{
    partial class FrmRemind
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmRemind));
            this.gbOther = new System.Windows.Forms.GroupBox();
            this.txbZdTimeOut = new System.Windows.Forms.TextBox();
            this.labZdTimeOut = new System.Windows.Forms.Label();
            this.ckbZdTimeOut = new System.Windows.Forms.CheckBox();
            this.txbDsTimeOut = new System.Windows.Forms.TextBox();
            this.labDsTimeOut = new System.Windows.Forms.Label();
            this.ckbDsTimeOut = new System.Windows.Forms.CheckBox();
            this.gbConfig = new System.Windows.Forms.GroupBox();
            this.ckbSendMsg = new System.Windows.Forms.CheckBox();
            this.txbPort = new System.Windows.Forms.TextBox();
            this.labPort = new System.Windows.Forms.Label();
            this.txbIP = new System.Windows.Forms.TextBox();
            this.labIP = new System.Windows.Forms.Label();
            this.gbMain = new System.Windows.Forms.GroupBox();
            this.txbZdClose = new System.Windows.Forms.TextBox();
            this.ckbZdClose = new System.Windows.Forms.CheckBox();
            this.labZdClose = new System.Windows.Forms.Label();
            this.ckbAbnormal = new System.Windows.Forms.CheckBox();
            this.txbDj = new System.Windows.Forms.TextBox();
            this.txbXb = new System.Windows.Forms.TextBox();
            this.txbZp = new System.Windows.Forms.TextBox();
            this.txbDs = new System.Windows.Forms.TextBox();
            this.txbZd = new System.Windows.Forms.TextBox();
            this.ckbDj = new System.Windows.Forms.CheckBox();
            this.labDj = new System.Windows.Forms.Label();
            this.ckbXb = new System.Windows.Forms.CheckBox();
            this.labXb = new System.Windows.Forms.Label();
            this.ckbZp = new System.Windows.Forms.CheckBox();
            this.labZp = new System.Windows.Forms.Label();
            this.ckbDs = new System.Windows.Forms.CheckBox();
            this.labDs = new System.Windows.Forms.Label();
            this.ckbLinked = new System.Windows.Forms.CheckBox();
            this.ckbZd = new System.Windows.Forms.CheckBox();
            this.labZd = new System.Windows.Forms.Label();
            this.btnSaveConfig = new System.Windows.Forms.Button();
            this.gbOther.SuspendLayout();
            this.gbConfig.SuspendLayout();
            this.gbMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // gbOther
            // 
            resources.ApplyResources(this.gbOther, "gbOther");
            this.gbOther.Controls.Add(this.txbZdTimeOut);
            this.gbOther.Controls.Add(this.labZdTimeOut);
            this.gbOther.Controls.Add(this.ckbZdTimeOut);
            this.gbOther.Controls.Add(this.txbDsTimeOut);
            this.gbOther.Controls.Add(this.labDsTimeOut);
            this.gbOther.Controls.Add(this.ckbDsTimeOut);
            this.gbOther.Name = "gbOther";
            this.gbOther.TabStop = false;
            // 
            // txbZdTimeOut
            // 
            resources.ApplyResources(this.txbZdTimeOut, "txbZdTimeOut");
            this.txbZdTimeOut.Name = "txbZdTimeOut";
            this.txbZdTimeOut.TextChanged += new System.EventHandler(this.TxbTextChanged);
            // 
            // labZdTimeOut
            // 
            resources.ApplyResources(this.labZdTimeOut, "labZdTimeOut");
            this.labZdTimeOut.Name = "labZdTimeOut";
            // 
            // ckbZdTimeOut
            // 
            resources.ApplyResources(this.ckbZdTimeOut, "ckbZdTimeOut");
            this.ckbZdTimeOut.Name = "ckbZdTimeOut";
            this.ckbZdTimeOut.UseVisualStyleBackColor = true;
            this.ckbZdTimeOut.CheckedChanged += new System.EventHandler(this.CkbCheckedChanged);
            // 
            // txbDsTimeOut
            // 
            resources.ApplyResources(this.txbDsTimeOut, "txbDsTimeOut");
            this.txbDsTimeOut.Name = "txbDsTimeOut";
            this.txbDsTimeOut.TextChanged += new System.EventHandler(this.TxbTextChanged);
            // 
            // labDsTimeOut
            // 
            resources.ApplyResources(this.labDsTimeOut, "labDsTimeOut");
            this.labDsTimeOut.Name = "labDsTimeOut";
            // 
            // ckbDsTimeOut
            // 
            resources.ApplyResources(this.ckbDsTimeOut, "ckbDsTimeOut");
            this.ckbDsTimeOut.Name = "ckbDsTimeOut";
            this.ckbDsTimeOut.UseVisualStyleBackColor = true;
            this.ckbDsTimeOut.CheckedChanged += new System.EventHandler(this.CkbCheckedChanged);
            // 
            // gbConfig
            // 
            resources.ApplyResources(this.gbConfig, "gbConfig");
            this.gbConfig.Controls.Add(this.ckbSendMsg);
            this.gbConfig.Controls.Add(this.txbPort);
            this.gbConfig.Controls.Add(this.labPort);
            this.gbConfig.Controls.Add(this.txbIP);
            this.gbConfig.Controls.Add(this.labIP);
            this.gbConfig.Name = "gbConfig";
            this.gbConfig.TabStop = false;
            // 
            // ckbSendMsg
            // 
            resources.ApplyResources(this.ckbSendMsg, "ckbSendMsg");
            this.ckbSendMsg.Name = "ckbSendMsg";
            this.ckbSendMsg.UseVisualStyleBackColor = true;
            this.ckbSendMsg.CheckedChanged += new System.EventHandler(this.CkbCheckedChanged);
            // 
            // txbPort
            // 
            resources.ApplyResources(this.txbPort, "txbPort");
            this.txbPort.Name = "txbPort";
            this.txbPort.TextChanged += new System.EventHandler(this.TxbTextChanged);
            // 
            // labPort
            // 
            resources.ApplyResources(this.labPort, "labPort");
            this.labPort.Name = "labPort";
            // 
            // txbIP
            // 
            resources.ApplyResources(this.txbIP, "txbIP");
            this.txbIP.Name = "txbIP";
            this.txbIP.TextChanged += new System.EventHandler(this.TxbTextChanged);
            // 
            // labIP
            // 
            resources.ApplyResources(this.labIP, "labIP");
            this.labIP.Name = "labIP";
            // 
            // gbMain
            // 
            resources.ApplyResources(this.gbMain, "gbMain");
            this.gbMain.Controls.Add(this.txbZdClose);
            this.gbMain.Controls.Add(this.ckbZdClose);
            this.gbMain.Controls.Add(this.labZdClose);
            this.gbMain.Controls.Add(this.ckbAbnormal);
            this.gbMain.Controls.Add(this.txbDj);
            this.gbMain.Controls.Add(this.txbXb);
            this.gbMain.Controls.Add(this.txbZp);
            this.gbMain.Controls.Add(this.txbDs);
            this.gbMain.Controls.Add(this.txbZd);
            this.gbMain.Controls.Add(this.ckbDj);
            this.gbMain.Controls.Add(this.labDj);
            this.gbMain.Controls.Add(this.ckbXb);
            this.gbMain.Controls.Add(this.labXb);
            this.gbMain.Controls.Add(this.ckbZp);
            this.gbMain.Controls.Add(this.labZp);
            this.gbMain.Controls.Add(this.ckbDs);
            this.gbMain.Controls.Add(this.labDs);
            this.gbMain.Controls.Add(this.ckbLinked);
            this.gbMain.Controls.Add(this.ckbZd);
            this.gbMain.Controls.Add(this.labZd);
            this.gbMain.Name = "gbMain";
            this.gbMain.TabStop = false;
            // 
            // txbZdClose
            // 
            resources.ApplyResources(this.txbZdClose, "txbZdClose");
            this.txbZdClose.Name = "txbZdClose";
            this.txbZdClose.TextChanged += new System.EventHandler(this.TxbTextChanged);
            // 
            // ckbZdClose
            // 
            resources.ApplyResources(this.ckbZdClose, "ckbZdClose");
            this.ckbZdClose.Name = "ckbZdClose";
            this.ckbZdClose.UseVisualStyleBackColor = true;
            this.ckbZdClose.CheckedChanged += new System.EventHandler(this.CkbCheckedChanged);
            // 
            // labZdClose
            // 
            resources.ApplyResources(this.labZdClose, "labZdClose");
            this.labZdClose.Name = "labZdClose";
            // 
            // ckbAbnormal
            // 
            resources.ApplyResources(this.ckbAbnormal, "ckbAbnormal");
            this.ckbAbnormal.Name = "ckbAbnormal";
            this.ckbAbnormal.UseVisualStyleBackColor = true;
            this.ckbAbnormal.CheckedChanged += new System.EventHandler(this.CkbCheckedChanged);
            // 
            // txbDj
            // 
            resources.ApplyResources(this.txbDj, "txbDj");
            this.txbDj.Name = "txbDj";
            this.txbDj.TextChanged += new System.EventHandler(this.TxbTextChanged);
            // 
            // txbXb
            // 
            resources.ApplyResources(this.txbXb, "txbXb");
            this.txbXb.Name = "txbXb";
            this.txbXb.TextChanged += new System.EventHandler(this.TxbTextChanged);
            // 
            // txbZp
            // 
            resources.ApplyResources(this.txbZp, "txbZp");
            this.txbZp.Name = "txbZp";
            this.txbZp.TextChanged += new System.EventHandler(this.TxbTextChanged);
            // 
            // txbDs
            // 
            resources.ApplyResources(this.txbDs, "txbDs");
            this.txbDs.Name = "txbDs";
            this.txbDs.TextChanged += new System.EventHandler(this.TxbTextChanged);
            // 
            // txbZd
            // 
            resources.ApplyResources(this.txbZd, "txbZd");
            this.txbZd.Name = "txbZd";
            this.txbZd.TextChanged += new System.EventHandler(this.TxbTextChanged);
            // 
            // ckbDj
            // 
            resources.ApplyResources(this.ckbDj, "ckbDj");
            this.ckbDj.Name = "ckbDj";
            this.ckbDj.UseVisualStyleBackColor = true;
            this.ckbDj.CheckedChanged += new System.EventHandler(this.CkbCheckedChanged);
            // 
            // labDj
            // 
            resources.ApplyResources(this.labDj, "labDj");
            this.labDj.Name = "labDj";
            // 
            // ckbXb
            // 
            resources.ApplyResources(this.ckbXb, "ckbXb");
            this.ckbXb.Name = "ckbXb";
            this.ckbXb.UseVisualStyleBackColor = true;
            this.ckbXb.CheckedChanged += new System.EventHandler(this.CkbCheckedChanged);
            // 
            // labXb
            // 
            resources.ApplyResources(this.labXb, "labXb");
            this.labXb.Name = "labXb";
            // 
            // ckbZp
            // 
            resources.ApplyResources(this.ckbZp, "ckbZp");
            this.ckbZp.Name = "ckbZp";
            this.ckbZp.UseVisualStyleBackColor = true;
            this.ckbZp.CheckedChanged += new System.EventHandler(this.CkbCheckedChanged);
            // 
            // labZp
            // 
            resources.ApplyResources(this.labZp, "labZp");
            this.labZp.Name = "labZp";
            // 
            // ckbDs
            // 
            resources.ApplyResources(this.ckbDs, "ckbDs");
            this.ckbDs.Name = "ckbDs";
            this.ckbDs.UseVisualStyleBackColor = true;
            this.ckbDs.CheckedChanged += new System.EventHandler(this.CkbCheckedChanged);
            // 
            // labDs
            // 
            resources.ApplyResources(this.labDs, "labDs");
            this.labDs.Name = "labDs";
            // 
            // ckbLinked
            // 
            resources.ApplyResources(this.ckbLinked, "ckbLinked");
            this.ckbLinked.Name = "ckbLinked";
            this.ckbLinked.UseVisualStyleBackColor = true;
            this.ckbLinked.CheckedChanged += new System.EventHandler(this.CkbLinked_CheckedChanged);
            // 
            // ckbZd
            // 
            resources.ApplyResources(this.ckbZd, "ckbZd");
            this.ckbZd.Name = "ckbZd";
            this.ckbZd.UseVisualStyleBackColor = true;
            this.ckbZd.CheckedChanged += new System.EventHandler(this.CkbCheckedChanged);
            // 
            // labZd
            // 
            resources.ApplyResources(this.labZd, "labZd");
            this.labZd.Name = "labZd";
            // 
            // btnSaveConfig
            // 
            resources.ApplyResources(this.btnSaveConfig, "btnSaveConfig");
            this.btnSaveConfig.ForeColor = System.Drawing.SystemColors.ControlText;
            this.btnSaveConfig.Name = "btnSaveConfig";
            this.btnSaveConfig.UseVisualStyleBackColor = true;
            this.btnSaveConfig.Click += new System.EventHandler(this.BtnSaveConfig_Click);
            // 
            // FrmRemind
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.btnSaveConfig);
            this.Controls.Add(this.gbMain);
            this.Controls.Add(this.gbOther);
            this.Controls.Add(this.gbConfig);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "FrmRemind";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FrmRemind_FormClosing);
            this.Load += new System.EventHandler(this.FrmRemind_Load);
            this.TextChanged += new System.EventHandler(this.FrmRemind_TextChanged);
            this.gbOther.ResumeLayout(false);
            this.gbOther.PerformLayout();
            this.gbConfig.ResumeLayout(false);
            this.gbConfig.PerformLayout();
            this.gbMain.ResumeLayout(false);
            this.gbMain.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox gbOther;
        private System.Windows.Forms.TextBox txbDsTimeOut;
        private System.Windows.Forms.Label labDsTimeOut;
        private System.Windows.Forms.CheckBox ckbDsTimeOut;
        private System.Windows.Forms.GroupBox gbConfig;
        private System.Windows.Forms.CheckBox ckbSendMsg;
        private System.Windows.Forms.TextBox txbPort;
        private System.Windows.Forms.Label labPort;
        private System.Windows.Forms.TextBox txbIP;
        private System.Windows.Forms.Label labIP;
        private System.Windows.Forms.GroupBox gbMain;
        private System.Windows.Forms.CheckBox ckbLinked;
        private System.Windows.Forms.CheckBox ckbZd;
        private System.Windows.Forms.Label labZd;
        private System.Windows.Forms.TextBox txbZd;
        private System.Windows.Forms.Button btnSaveConfig;
        private System.Windows.Forms.CheckBox ckbZp;
        private System.Windows.Forms.Label labZp;
        private System.Windows.Forms.TextBox txbZp;
        private System.Windows.Forms.CheckBox ckbDs;
        private System.Windows.Forms.Label labDs;
        private System.Windows.Forms.TextBox txbDs;
        private System.Windows.Forms.CheckBox ckbDj;
        private System.Windows.Forms.Label labDj;
        private System.Windows.Forms.TextBox txbDj;
        private System.Windows.Forms.CheckBox ckbXb;
        private System.Windows.Forms.Label labXb;
        private System.Windows.Forms.TextBox txbXb;
        private System.Windows.Forms.CheckBox ckbAbnormal;
        private System.Windows.Forms.TextBox txbZdClose;
        private System.Windows.Forms.CheckBox ckbZdClose;
        private System.Windows.Forms.Label labZdClose;
        private TextBox txbZdTimeOut;
        private Label labZdTimeOut;
        private CheckBox ckbZdTimeOut;
    }
}