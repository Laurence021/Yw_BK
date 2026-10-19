namespace Yw_BK
{
    partial class Frm365
    {
        /// <summary>
        /// 設計工具所需的變數。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清除任何使用中的資源。
        /// </summary>
        /// <param name="disposing">如果應該處置受控資源則為 true，否則為 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 設計工具產生的程式碼

        /// <summary>
        /// 此為設計工具支援所需的方法 - 請勿使用程式碼編輯器修改
        /// 這個方法的內容。
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Frm365));
            msMenu = new MenuStrip();
            tsmiConfig = new ToolStripMenuItem();
            miFunction = new ToolStripMenuItem();
            tsmiRemind = new ToolStripMenuItem();
            tsmiUser = new ToolStripMenuItem();
            tsmiProxy = new ToolStripMenuItem();
            tsmiSuffix = new ToolStripMenuItem();
            tsmiTransfer = new ToolStripMenuItem();
            tsmiExclude = new ToolStripMenuItem();
            tsmiNameReplace = new ToolStripMenuItem();
            tsmiFilterMode = new ToolStripMenuItem();
            tsmiLeagueReplace = new ToolStripMenuItem();
            tsmiHideFrm = new ToolStripMenuItem();
            tsmiMinimized = new ToolStripMenuItem();
            ssBottomLab = new StatusStrip();
            labSecondText = new ToolStripStatusLabel();
            labSecond = new ToolStripStatusLabel();
            labStatusText = new ToolStripStatusLabel();
            labStatus = new ToolStripStatusLabel();
            labServerStatus = new ToolStripStatusLabel();
            tmrTick = new System.Windows.Forms.Timer(components);
            gbMainCtrl = new GroupBox();
            btnMatch = new Button();
            btnOddsTotal = new Button();
            btnResetBuffer = new Button();
            btnUpdate = new Button();
            btnFollow = new Button();
            btnLogin = new Button();
            gbStatusView = new GroupBox();
            labDate = new Label();
            labStatusView = new Label();
            txbUrl = new TextBox();
            labUrl = new Label();
            msMenu.SuspendLayout();
            ssBottomLab.SuspendLayout();
            gbMainCtrl.SuspendLayout();
            gbStatusView.SuspendLayout();
            SuspendLayout();
            // 
            // msMenu
            // 
            msMenu.BackColor = Color.Pink;
            msMenu.Items.AddRange(new ToolStripItem[] { tsmiConfig, miFunction, tsmiHideFrm, tsmiMinimized });
            resources.ApplyResources(msMenu, "msMenu");
            msMenu.Name = "msMenu";
            // 
            // tsmiConfig
            // 
            tsmiConfig.Name = "tsmiConfig";
            resources.ApplyResources(tsmiConfig, "tsmiConfig");
            tsmiConfig.Click += ToolStripMenuItem_Click;
            // 
            // miFunction
            // 
            miFunction.DropDownItems.AddRange(new ToolStripItem[] { tsmiRemind, tsmiUser, tsmiProxy, tsmiSuffix, tsmiTransfer, tsmiExclude, tsmiFilterMode, tsmiLeagueReplace, tsmiNameReplace });
            miFunction.Name = "miFunction";
            resources.ApplyResources(miFunction, "miFunction");
            // 
            // tsmiRemind
            // 
            tsmiRemind.Name = "tsmiRemind";
            resources.ApplyResources(tsmiRemind, "tsmiRemind");
            tsmiRemind.Click += ToolStripMenuItem_Click;
            // 
            // tsmiUser
            // 
            tsmiUser.Name = "tsmiUser";
            resources.ApplyResources(tsmiUser, "tsmiUser");
            tsmiUser.Click += ToolStripMenuItem_Click;
            // 
            // tsmiProxy
            // 
            tsmiProxy.Name = "tsmiProxy";
            resources.ApplyResources(tsmiProxy, "tsmiProxy");
            tsmiProxy.Click += ToolStripMenuItem_Click;
            // 
            // tsmiSuffix
            // 
            tsmiSuffix.Name = "tsmiSuffix";
            resources.ApplyResources(tsmiSuffix, "tsmiSuffix");
            tsmiSuffix.Click += ToolStripMenuItem_Click;
            // 
            // tsmiTransfer
            // 
            tsmiTransfer.Name = "tsmiTransfer";
            resources.ApplyResources(tsmiTransfer, "tsmiTransfer");
            tsmiTransfer.Click += ToolStripMenuItem_Click;
            // 
            // tsmiExclude
            // 
            tsmiExclude.Name = "tsmiExclude";
            resources.ApplyResources(tsmiExclude, "tsmiExclude");
            tsmiExclude.Click += ToolStripMenuItem_Click;
            // 
            // tsmiNameReplace
            // 
            tsmiNameReplace.Name = "tsmiNameReplace";
            resources.ApplyResources(tsmiNameReplace, "tsmiNameReplace");
            tsmiNameReplace.Click += ToolStripMenuItem_Click;
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
            // tsmiHideFrm
            // 
            tsmiHideFrm.Name = "tsmiHideFrm";
            resources.ApplyResources(tsmiHideFrm, "tsmiHideFrm");
            tsmiHideFrm.Click += TsmiHideFrm_Click;
            // 
            // tsmiMinimized
            // 
            tsmiMinimized.Name = "tsmiMinimized";
            resources.ApplyResources(tsmiMinimized, "tsmiMinimized");
            tsmiMinimized.Click += TsmiMinimized_Click;
            // 
            // ssBottomLab
            // 
            ssBottomLab.BackColor = Color.Pink;
            ssBottomLab.Items.AddRange(new ToolStripItem[] { labSecondText, labSecond, labStatusText, labStatus, labServerStatus });
            resources.ApplyResources(ssBottomLab, "ssBottomLab");
            ssBottomLab.Name = "ssBottomLab";
            ssBottomLab.RenderMode = ToolStripRenderMode.Professional;
            ssBottomLab.ShowItemToolTips = true;
            ssBottomLab.SizingGrip = false;
            // 
            // labSecondText
            // 
            resources.ApplyResources(labSecondText, "labSecondText");
            labSecondText.Name = "labSecondText";
            // 
            // labSecond
            // 
            resources.ApplyResources(labSecond, "labSecond");
            labSecond.ForeColor = Color.Red;
            labSecond.Name = "labSecond";
            labSecond.Spring = true;
            // 
            // labStatusText
            // 
            resources.ApplyResources(labStatusText, "labStatusText");
            labStatusText.Name = "labStatusText";
            // 
            // labStatus
            // 
            resources.ApplyResources(labStatus, "labStatus");
            labStatus.ForeColor = Color.Red;
            labStatus.Name = "labStatus";
            labStatus.Spring = true;
            labStatus.TextChanged += LabStatus_TextChanged;
            // 
            // labServerStatus
            // 
            resources.ApplyResources(labServerStatus, "labServerStatus");
            labServerStatus.Name = "labServerStatus";
            // 
            // tmrTick
            // 
            tmrTick.Enabled = true;
            tmrTick.Interval = 1000;
            tmrTick.Tick += TmrTick_Tick;
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
            btnFollow.Click += BtnFollow_Click;
            // 
            // btnLogin
            // 
            resources.ApplyResources(btnLogin, "btnLogin");
            btnLogin.Name = "btnLogin";
            btnLogin.UseVisualStyleBackColor = true;
            btnLogin.Click += BtnLogin_Click;
            // 
            // gbStatusView
            // 
            gbStatusView.Controls.Add(labDate);
            gbStatusView.Controls.Add(labStatusView);
            resources.ApplyResources(gbStatusView, "gbStatusView");
            gbStatusView.Name = "gbStatusView";
            gbStatusView.TabStop = false;
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
            // txbUrl
            // 
            resources.ApplyResources(txbUrl, "txbUrl");
            txbUrl.Name = "txbUrl";
            txbUrl.TabStop = false;
            txbUrl.Validated += TxbUrl_Validated;
            // 
            // labUrl
            // 
            resources.ApplyResources(labUrl, "labUrl");
            labUrl.Name = "labUrl";
            // 
            // Frm365
            // 
            resources.ApplyResources(this, "$this");
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Pink;
            Controls.Add(txbUrl);
            Controls.Add(labUrl);
            Controls.Add(gbStatusView);
            Controls.Add(gbMainCtrl);
            Controls.Add(ssBottomLab);
            Controls.Add(msMenu);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "Frm365";
            FormClosing += Frm365_FormClosing;
            Load += Frm365_Load;
            msMenu.ResumeLayout(false);
            msMenu.PerformLayout();
            ssBottomLab.ResumeLayout(false);
            ssBottomLab.PerformLayout();
            gbMainCtrl.ResumeLayout(false);
            gbStatusView.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.MenuStrip msMenu;
        private System.Windows.Forms.ToolStripMenuItem tsmiConfig;
        private System.Windows.Forms.ToolStripMenuItem miFunction;
        private System.Windows.Forms.ToolStripMenuItem tsmiRemind;
        private System.Windows.Forms.ToolStripMenuItem tsmiUser;
        private System.Windows.Forms.ToolStripMenuItem tsmiProxy;
        private System.Windows.Forms.ToolStripMenuItem tsmiHideFrm;
        private System.Windows.Forms.ToolStripMenuItem tsmiMinimized;
        private System.Windows.Forms.StatusStrip ssBottomLab;
        private System.Windows.Forms.ToolStripStatusLabel labSecondText;
        private System.Windows.Forms.ToolStripStatusLabel labSecond;
        private System.Windows.Forms.ToolStripStatusLabel labStatusText;
        private System.Windows.Forms.ToolStripStatusLabel labStatus;
        private System.Windows.Forms.Timer tmrTick;
        private System.Windows.Forms.GroupBox gbMainCtrl;
        private System.Windows.Forms.Button btnResetBuffer;
        private System.Windows.Forms.Button btnUpdate;
        private System.Windows.Forms.Button btnFollow;
        private System.Windows.Forms.Button btnLogin;
        private System.Windows.Forms.GroupBox gbStatusView;
        private System.Windows.Forms.Label labStatusView;
        private System.Windows.Forms.TextBox txbUrl;
        private System.Windows.Forms.Label labUrl;
        private System.Windows.Forms.Button btnOddsTotal;
        private System.Windows.Forms.ToolStripMenuItem tsmiSuffix;
        private System.Windows.Forms.ToolStripMenuItem tsmiTransfer;
        private System.Windows.Forms.Button btnMatch;
        private System.Windows.Forms.ToolStripMenuItem tsmiExclude;
        private System.Windows.Forms.ToolStripMenuItem tsmiNameReplace;
        private System.Windows.Forms.ToolStripStatusLabel labServerStatus;
        private System.Windows.Forms.Label labDate;
        private System.Windows.Forms.ToolStripMenuItem tsmiFilterMode;
        private ToolStripMenuItem tsmiLeagueReplace;
    }
}

