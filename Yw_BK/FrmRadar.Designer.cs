namespace Yw_BK
{
    partial class FrmRadar
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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmRadar));
            labUrl = new Label();
            labDate = new Label();
            labStatusView = new Label();
            gbStatusView = new GroupBox();
            btnMatch = new Button();
            btnOddsTotal = new Button();
            btnResetBuffer = new Button();
            btnUpdate = new Button();
            btnFollow = new Button();
            btnLogin = new Button();
            gbMainCtrl = new GroupBox();
            tmrTick = new System.Windows.Forms.Timer(components);
            labServerStatus = new ToolStripStatusLabel();
            labStatus = new ToolStripStatusLabel();
            txbUrl = new TextBox();
            labStatusText = new ToolStripStatusLabel();
            labSecondText = new ToolStripStatusLabel();
            ssBottomLab = new StatusStrip();
            labSecond = new ToolStripStatusLabel();
            tsmiMinimized = new ToolStripMenuItem();
            tsmiHideFrm = new ToolStripMenuItem();
            tsmiSuffix = new ToolStripMenuItem();
            tsmiNameReplace = new ToolStripMenuItem();
            tsmiExclude = new ToolStripMenuItem();
            tsmiTransfer = new ToolStripMenuItem();
            tsmiProxy = new ToolStripMenuItem();
            tsmiUser = new ToolStripMenuItem();
            tsmiRemind = new ToolStripMenuItem();
            miFunction = new ToolStripMenuItem();
            tsmiFilterMode = new ToolStripMenuItem();
            tsmiLeagueReplace = new ToolStripMenuItem();
            tsmiConfig = new ToolStripMenuItem();
            msMenu = new MenuStrip();
            gbStatusView.SuspendLayout();
            gbMainCtrl.SuspendLayout();
            ssBottomLab.SuspendLayout();
            msMenu.SuspendLayout();
            SuspendLayout();
            // 
            // labUrl
            // 
            resources.ApplyResources(labUrl, "labUrl");
            labUrl.Name = "labUrl";
            // 
            // labDate
            // 
            resources.ApplyResources(labDate, "labDate");
            labDate.ForeColor = SystemColors.ButtonShadow;
            labDate.Name = "labDate";
            // 
            // labStatusView
            // 
            labStatusView.Image = Properties.Resources.Img_BK;
            resources.ApplyResources(labStatusView, "labStatusView");
            labStatusView.Name = "labStatusView";
            // 
            // gbStatusView
            // 
            gbStatusView.Controls.Add(labDate);
            gbStatusView.Controls.Add(labStatusView);
            resources.ApplyResources(gbStatusView, "gbStatusView");
            gbStatusView.Name = "gbStatusView";
            gbStatusView.TabStop = false;
            // 
            // btnMatch
            // 
            resources.ApplyResources(btnMatch, "btnMatch");
            btnMatch.Name = "btnMatch";
            btnMatch.Tag = "";
            btnMatch.UseVisualStyleBackColor = true;
            btnMatch.Click += BtnMatch_Click;
            // 
            // btnOddsTotal
            // 
            resources.ApplyResources(btnOddsTotal, "btnOddsTotal");
            btnOddsTotal.Name = "btnOddsTotal";
            btnOddsTotal.UseVisualStyleBackColor = true;
            btnOddsTotal.Click += BtnOddsTotal_Click;
            // 
            // btnResetBuffer
            // 
            resources.ApplyResources(btnResetBuffer, "btnResetBuffer");
            btnResetBuffer.Name = "btnResetBuffer";
            btnResetBuffer.UseVisualStyleBackColor = true;
            btnResetBuffer.Click += BtnResetBuffer_Click;
            // 
            // btnUpdate
            // 
            resources.ApplyResources(btnUpdate, "btnUpdate");
            btnUpdate.Name = "btnUpdate";
            btnUpdate.UseVisualStyleBackColor = true;
            btnUpdate.Click += BtnUpdate_Click;
            // 
            // btnFollow
            // 
            resources.ApplyResources(btnFollow, "btnFollow");
            btnFollow.Name = "btnFollow";
            btnFollow.UseVisualStyleBackColor = true;
            btnFollow.TextChanged += BtnFollow_TextChanged;
            btnFollow.Click += BtnFollow_Click;
            // 
            // btnLogin
            // 
            resources.ApplyResources(btnLogin, "btnLogin");
            btnLogin.Name = "btnLogin";
            btnLogin.UseVisualStyleBackColor = true;
            btnLogin.Click += BtnLogin_Click;
            // 
            // gbMainCtrl
            // 
            gbMainCtrl.Controls.Add(btnMatch);
            gbMainCtrl.Controls.Add(btnOddsTotal);
            gbMainCtrl.Controls.Add(btnResetBuffer);
            gbMainCtrl.Controls.Add(btnUpdate);
            gbMainCtrl.Controls.Add(btnFollow);
            gbMainCtrl.Controls.Add(btnLogin);
            resources.ApplyResources(gbMainCtrl, "gbMainCtrl");
            gbMainCtrl.Name = "gbMainCtrl";
            gbMainCtrl.TabStop = false;
            // 
            // tmrTick
            // 
            tmrTick.Enabled = true;
            tmrTick.Interval = 1000;
            tmrTick.Tick += TmrTick_Tick;
            // 
            // labServerStatus
            // 
            resources.ApplyResources(labServerStatus, "labServerStatus");
            labServerStatus.Name = "labServerStatus";
            // 
            // labStatus
            // 
            resources.ApplyResources(labStatus, "labStatus");
            labStatus.ForeColor = Color.Red;
            labStatus.Name = "labStatus";
            labStatus.Spring = true;
            labStatus.TextChanged += LabStatus_TextChanged;
            // 
            // txbUrl
            // 
            resources.ApplyResources(txbUrl, "txbUrl");
            txbUrl.Name = "txbUrl";
            txbUrl.TabStop = false;
            txbUrl.Validated += TxbUrl_Validated;
            // 
            // labStatusText
            // 
            resources.ApplyResources(labStatusText, "labStatusText");
            labStatusText.Name = "labStatusText";
            // 
            // labSecondText
            // 
            resources.ApplyResources(labSecondText, "labSecondText");
            labSecondText.Name = "labSecondText";
            // 
            // ssBottomLab
            // 
            ssBottomLab.BackColor = Color.Pink;
            ssBottomLab.ImageScalingSize = new Size(24, 24);
            ssBottomLab.Items.AddRange(new ToolStripItem[] { labSecondText, labSecond, labStatusText, labStatus, labServerStatus });
            resources.ApplyResources(ssBottomLab, "ssBottomLab");
            ssBottomLab.Name = "ssBottomLab";
            ssBottomLab.RenderMode = ToolStripRenderMode.Professional;
            ssBottomLab.ShowItemToolTips = true;
            ssBottomLab.SizingGrip = false;
            // 
            // labSecond
            // 
            resources.ApplyResources(labSecond, "labSecond");
            labSecond.ForeColor = Color.Red;
            labSecond.Name = "labSecond";
            labSecond.Spring = true;
            // 
            // tsmiMinimized
            // 
            tsmiMinimized.Name = "tsmiMinimized";
            resources.ApplyResources(tsmiMinimized, "tsmiMinimized");
            tsmiMinimized.Click += TsmiMinimized_Click;
            // 
            // tsmiHideFrm
            // 
            tsmiHideFrm.Name = "tsmiHideFrm";
            resources.ApplyResources(tsmiHideFrm, "tsmiHideFrm");
            tsmiHideFrm.Click += TsmiHideFrm_Click;
            // 
            // tsmiSuffix
            // 
            tsmiSuffix.Name = "tsmiSuffix";
            resources.ApplyResources(tsmiSuffix, "tsmiSuffix");
            tsmiSuffix.Click += ToolStripMenuItem_Click;
            // 
            // tsmiNameReplace
            // 
            tsmiNameReplace.Name = "tsmiNameReplace";
            resources.ApplyResources(tsmiNameReplace, "tsmiNameReplace");
            tsmiNameReplace.Click += ToolStripMenuItem_Click;
            // 
            // tsmiExclude
            // 
            tsmiExclude.Name = "tsmiExclude";
            resources.ApplyResources(tsmiExclude, "tsmiExclude");
            tsmiExclude.Click += ToolStripMenuItem_Click;
            // 
            // tsmiTransfer
            // 
            tsmiTransfer.Name = "tsmiTransfer";
            resources.ApplyResources(tsmiTransfer, "tsmiTransfer");
            tsmiTransfer.Click += ToolStripMenuItem_Click;
            // 
            // tsmiProxy
            // 
            tsmiProxy.Name = "tsmiProxy";
            resources.ApplyResources(tsmiProxy, "tsmiProxy");
            tsmiProxy.Click += ToolStripMenuItem_Click;
            // 
            // tsmiUser
            // 
            tsmiUser.Name = "tsmiUser";
            resources.ApplyResources(tsmiUser, "tsmiUser");
            tsmiUser.Click += ToolStripMenuItem_Click;
            // 
            // tsmiRemind
            // 
            tsmiRemind.Name = "tsmiRemind";
            resources.ApplyResources(tsmiRemind, "tsmiRemind");
            tsmiRemind.Click += ToolStripMenuItem_Click;
            // 
            // miFunction
            // 
            miFunction.DropDownItems.AddRange(new ToolStripItem[] { tsmiRemind, tsmiUser, tsmiProxy, tsmiSuffix, tsmiTransfer, tsmiExclude, tsmiFilterMode, tsmiLeagueReplace, tsmiNameReplace });
            miFunction.Name = "miFunction";
            resources.ApplyResources(miFunction, "miFunction");
            // 
            // tsmiFilterMode
            // 
            tsmiFilterMode.Name = "tsmiFilterMode";
            resources.ApplyResources(tsmiFilterMode, "tsmiFilterMode");
            tsmiFilterMode.Click += ToolStripMenuItem_Click;
            // 
            // tsmiLeagueReplace
            // 
            tsmiLeagueReplace.Name = "tsmiLeagueReplace";
            resources.ApplyResources(tsmiLeagueReplace, "tsmiLeagueReplace");
            tsmiLeagueReplace.Click += ToolStripMenuItem_Click;
            // 
            // tsmiConfig
            // 
            tsmiConfig.Name = "tsmiConfig";
            resources.ApplyResources(tsmiConfig, "tsmiConfig");
            tsmiConfig.Click += ToolStripMenuItem_Click;
            // 
            // msMenu
            // 
            msMenu.BackColor = Color.Pink;
            msMenu.ImageScalingSize = new Size(24, 24);
            msMenu.Items.AddRange(new ToolStripItem[] { tsmiConfig, miFunction, tsmiHideFrm, tsmiMinimized });
            resources.ApplyResources(msMenu, "msMenu");
            msMenu.Name = "msMenu";
            // 
            // FrmRadar
            // 
            resources.ApplyResources(this, "$this");
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Pink;
            Controls.Add(labUrl);
            Controls.Add(gbStatusView);
            Controls.Add(gbMainCtrl);
            Controls.Add(txbUrl);
            Controls.Add(ssBottomLab);
            Controls.Add(msMenu);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FrmRadar";
            FormClosing += FrmRadar_FormClosing;
            Load += FrmRadar_Load;
            gbStatusView.ResumeLayout(false);
            gbMainCtrl.ResumeLayout(false);
            ssBottomLab.ResumeLayout(false);
            ssBottomLab.PerformLayout();
            msMenu.ResumeLayout(false);
            msMenu.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label labUrl;
        private System.Windows.Forms.Label labDate;
        private System.Windows.Forms.Label labStatusView;
        private System.Windows.Forms.GroupBox gbStatusView;
        private System.Windows.Forms.Button btnMatch;
        private System.Windows.Forms.Button btnOddsTotal;
        private System.Windows.Forms.Button btnResetBuffer;
        private System.Windows.Forms.Button btnUpdate;
        private System.Windows.Forms.Button btnFollow;
        private System.Windows.Forms.Button btnLogin;
        private System.Windows.Forms.GroupBox gbMainCtrl;
        private System.Windows.Forms.Timer tmrTick;
        private System.Windows.Forms.ToolStripStatusLabel labServerStatus;
        private System.Windows.Forms.ToolStripStatusLabel labStatus;
        private System.Windows.Forms.TextBox txbUrl;
        private System.Windows.Forms.ToolStripStatusLabel labStatusText;
        private System.Windows.Forms.ToolStripStatusLabel labSecondText;
        private System.Windows.Forms.StatusStrip ssBottomLab;
        private System.Windows.Forms.ToolStripStatusLabel labSecond;
        private System.Windows.Forms.ToolStripMenuItem tsmiMinimized;
        private System.Windows.Forms.ToolStripMenuItem tsmiHideFrm;
        private System.Windows.Forms.ToolStripMenuItem tsmiSuffix;
        private System.Windows.Forms.ToolStripMenuItem tsmiNameReplace;
        private System.Windows.Forms.ToolStripMenuItem tsmiExclude;
        private System.Windows.Forms.ToolStripMenuItem tsmiTransfer;
        private System.Windows.Forms.ToolStripMenuItem tsmiProxy;
        private System.Windows.Forms.ToolStripMenuItem tsmiUser;
        private System.Windows.Forms.ToolStripMenuItem tsmiRemind;
        private System.Windows.Forms.ToolStripMenuItem miFunction;
        private System.Windows.Forms.ToolStripMenuItem tsmiConfig;
        private System.Windows.Forms.MenuStrip msMenu;
        private System.Windows.Forms.ToolStripMenuItem tsmiFilterMode;
        private ToolStripMenuItem tsmiLeagueReplace;
    }
}