namespace Yw_BK
{
    partial class FrmJZ
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmJZ));
            this.msMenu = new System.Windows.Forms.MenuStrip();
            this.tsmiConfig = new System.Windows.Forms.ToolStripMenuItem();
            this.miFunction = new System.Windows.Forms.ToolStripMenuItem();
            this.tsmiRemind = new System.Windows.Forms.ToolStripMenuItem();
            this.miSimTestBtn = new System.Windows.Forms.ToolStripMenuItem();
            this.tsmiUser = new System.Windows.Forms.ToolStripMenuItem();
            this.tsmiProxy = new System.Windows.Forms.ToolStripMenuItem();
            this.tsmiTransfer = new System.Windows.Forms.ToolStripMenuItem();
            this.tsmiExclude = new System.Windows.Forms.ToolStripMenuItem();
            this.tsmiNameReplace = new System.Windows.Forms.ToolStripMenuItem();
            this.tsmiApiSwitch = new System.Windows.Forms.ToolStripMenuItem();
            this.tsmiHideFrm = new System.Windows.Forms.ToolStripMenuItem();
            this.tsmiMinimized = new System.Windows.Forms.ToolStripMenuItem();
            this.ssBottomLab = new System.Windows.Forms.StatusStrip();
            this.labSecondText = new System.Windows.Forms.ToolStripStatusLabel();
            this.labSecond = new System.Windows.Forms.ToolStripStatusLabel();
            this.labStatusText = new System.Windows.Forms.ToolStripStatusLabel();
            this.labStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.labServerStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.tmrTick = new System.Windows.Forms.Timer(this.components);
            this.gbMainCtrl = new System.Windows.Forms.GroupBox();
            this.btnMatch = new System.Windows.Forms.Button();
            this.btnOddsTotal = new System.Windows.Forms.Button();
            this.btnResetBuffer = new System.Windows.Forms.Button();
            this.btnUpdate = new System.Windows.Forms.Button();
            this.btnFollow = new System.Windows.Forms.Button();
            this.btnLogin = new System.Windows.Forms.Button();
            this.gbStatusView = new System.Windows.Forms.GroupBox();
            this.labDate = new System.Windows.Forms.Label();
            this.labStatusView = new System.Windows.Forms.Label();
            this.txbUrl = new System.Windows.Forms.TextBox();
            this.labUrl = new System.Windows.Forms.Label();
            this.msMenu.SuspendLayout();
            this.ssBottomLab.SuspendLayout();
            this.gbMainCtrl.SuspendLayout();
            this.gbStatusView.SuspendLayout();
            this.SuspendLayout();
            // 
            // msMenu
            // 
            this.msMenu.BackColor = System.Drawing.Color.Pink;
            this.msMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsmiConfig,
            this.miFunction,
            this.tsmiHideFrm,
            this.tsmiMinimized});
            resources.ApplyResources(this.msMenu, "msMenu");
            this.msMenu.Name = "msMenu";
            // 
            // tsmiConfig
            // 
            this.tsmiConfig.Name = "tsmiConfig";
            resources.ApplyResources(this.tsmiConfig, "tsmiConfig");
            this.tsmiConfig.Click += new System.EventHandler(this.ToolStripMenuItem_Click);
            // 
            // miFunction
            // 
            this.miFunction.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsmiRemind,
            this.miSimTestBtn,
            this.tsmiUser,
            this.tsmiProxy,
            this.tsmiTransfer,
            this.tsmiExclude,
            this.tsmiNameReplace,
            this.tsmiApiSwitch});
            this.miFunction.Name = "miFunction";
            resources.ApplyResources(this.miFunction, "miFunction");
            // 
            // tsmiRemind
            // 
            this.tsmiRemind.Name = "tsmiRemind";
            resources.ApplyResources(this.tsmiRemind, "tsmiRemind");
            this.tsmiRemind.Click += new System.EventHandler(this.ToolStripMenuItem_Click);
            // 
            // miSimTestBtn
            // 
            this.miSimTestBtn.Name = "miSimTestBtn";
            resources.ApplyResources(this.miSimTestBtn, "miSimTestBtn");
            // 
            // tsmiUser
            // 
            this.tsmiUser.Name = "tsmiUser";
            resources.ApplyResources(this.tsmiUser, "tsmiUser");
            this.tsmiUser.Click += new System.EventHandler(this.ToolStripMenuItem_Click);
            // 
            // tsmiProxy
            // 
            this.tsmiProxy.Name = "tsmiProxy";
            resources.ApplyResources(this.tsmiProxy, "tsmiProxy");
            this.tsmiProxy.Click += new System.EventHandler(this.ToolStripMenuItem_Click);
            // 
            // tsmiTransfer
            // 
            this.tsmiTransfer.Name = "tsmiTransfer";
            resources.ApplyResources(this.tsmiTransfer, "tsmiTransfer");
            this.tsmiTransfer.Click += new System.EventHandler(this.ToolStripMenuItem_Click);
            // 
            // tsmiExclude
            // 
            this.tsmiExclude.Name = "tsmiExclude";
            resources.ApplyResources(this.tsmiExclude, "tsmiExclude");
            this.tsmiExclude.Click += new System.EventHandler(this.ToolStripMenuItem_Click);
            // 
            // tsmiNameReplace
            // 
            this.tsmiNameReplace.Name = "tsmiNameReplace";
            resources.ApplyResources(this.tsmiNameReplace, "tsmiNameReplace");
            this.tsmiNameReplace.Click += new System.EventHandler(this.ToolStripMenuItem_Click);
            // 
            // tsmiApiSwitch
            // 
            this.tsmiApiSwitch.Name = "tsmiApiSwitch";
            resources.ApplyResources(this.tsmiApiSwitch, "tsmiApiSwitch");
            this.tsmiApiSwitch.Click += new System.EventHandler(this.ToolStripMenuItem_Click);
            // 
            // tsmiHideFrm
            // 
            this.tsmiHideFrm.Name = "tsmiHideFrm";
            resources.ApplyResources(this.tsmiHideFrm, "tsmiHideFrm");
            this.tsmiHideFrm.Click += new System.EventHandler(this.TsmiHideFrm_Click);
            // 
            // tsmiMinimized
            // 
            this.tsmiMinimized.Name = "tsmiMinimized";
            resources.ApplyResources(this.tsmiMinimized, "tsmiMinimized");
            this.tsmiMinimized.Click += new System.EventHandler(this.TsmiMinimized_Click);
            // 
            // ssBottomLab
            // 
            this.ssBottomLab.BackColor = System.Drawing.Color.Pink;
            this.ssBottomLab.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.labSecondText,
            this.labSecond,
            this.labStatusText,
            this.labStatus,
            this.labServerStatus});
            resources.ApplyResources(this.ssBottomLab, "ssBottomLab");
            this.ssBottomLab.Name = "ssBottomLab";
            this.ssBottomLab.RenderMode = System.Windows.Forms.ToolStripRenderMode.Professional;
            this.ssBottomLab.ShowItemToolTips = true;
            this.ssBottomLab.SizingGrip = false;
            // 
            // labSecondText
            // 
            resources.ApplyResources(this.labSecondText, "labSecondText");
            this.labSecondText.Name = "labSecondText";
            // 
            // labSecond
            // 
            resources.ApplyResources(this.labSecond, "labSecond");
            this.labSecond.ForeColor = System.Drawing.Color.Red;
            this.labSecond.Name = "labSecond";
            this.labSecond.Spring = true;
            this.labSecond.Tag = "None";
            // 
            // labStatusText
            // 
            resources.ApplyResources(this.labStatusText, "labStatusText");
            this.labStatusText.Name = "labStatusText";
            // 
            // labStatus
            // 
            resources.ApplyResources(this.labStatus, "labStatus");
            this.labStatus.ForeColor = System.Drawing.Color.Red;
            this.labStatus.Name = "labStatus";
            this.labStatus.Spring = true;
            this.labStatus.Tag = "NotStarted";
            this.labStatus.TextChanged += new System.EventHandler(this.LabStatus_TextChanged);
            // 
            // labServerStatus
            // 
            resources.ApplyResources(this.labServerStatus, "labServerStatus");
            this.labServerStatus.Name = "labServerStatus";
            // 
            // tmrTick
            // 
            this.tmrTick.Enabled = true;
            this.tmrTick.Interval = 1000;
            this.tmrTick.Tick += new System.EventHandler(this.TmrTick_Tick);
            // 
            // gbMainCtrl
            // 
            this.gbMainCtrl.Controls.Add(this.btnMatch);
            this.gbMainCtrl.Controls.Add(this.btnOddsTotal);
            this.gbMainCtrl.Controls.Add(this.btnResetBuffer);
            this.gbMainCtrl.Controls.Add(this.btnUpdate);
            this.gbMainCtrl.Controls.Add(this.btnFollow);
            this.gbMainCtrl.Controls.Add(this.btnLogin);
            resources.ApplyResources(this.gbMainCtrl, "gbMainCtrl");
            this.gbMainCtrl.Name = "gbMainCtrl";
            this.gbMainCtrl.TabStop = false;
            // 
            // btnMatch
            // 
            resources.ApplyResources(this.btnMatch, "btnMatch");
            this.btnMatch.Name = "btnMatch";
            this.btnMatch.Tag = "";
            this.btnMatch.UseVisualStyleBackColor = true;
            this.btnMatch.Click += new System.EventHandler(this.BtnMatch_Click);
            // 
            // btnOddsTotal
            // 
            resources.ApplyResources(this.btnOddsTotal, "btnOddsTotal");
            this.btnOddsTotal.Name = "btnOddsTotal";
            this.btnOddsTotal.UseVisualStyleBackColor = true;
            this.btnOddsTotal.Click += new System.EventHandler(this.BtnOddsTotal_Click);
            // 
            // btnResetBuffer
            // 
            resources.ApplyResources(this.btnResetBuffer, "btnResetBuffer");
            this.btnResetBuffer.Name = "btnResetBuffer";
            this.btnResetBuffer.UseVisualStyleBackColor = true;
            this.btnResetBuffer.Click += new System.EventHandler(this.BtnResetBuffer_Click);
            // 
            // btnUpdate
            // 
            resources.ApplyResources(this.btnUpdate, "btnUpdate");
            this.btnUpdate.Name = "btnUpdate";
            this.btnUpdate.UseVisualStyleBackColor = true;
            this.btnUpdate.Click += new System.EventHandler(this.BtnUpdate_Click);
            // 
            // btnFollow
            // 
            resources.ApplyResources(this.btnFollow, "btnFollow");
            this.btnFollow.Name = "btnFollow";
            this.btnFollow.Tag = "Start";
            this.btnFollow.UseVisualStyleBackColor = true;
            this.btnFollow.Click += new System.EventHandler(this.BtnFollow_Click);
            // 
            // btnLogin
            // 
            resources.ApplyResources(this.btnLogin, "btnLogin");
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.UseVisualStyleBackColor = true;
            this.btnLogin.Click += new System.EventHandler(this.BtnLogin_Click);
            // 
            // gbStatusView
            // 
            this.gbStatusView.Controls.Add(this.labDate);
            this.gbStatusView.Controls.Add(this.labStatusView);
            resources.ApplyResources(this.gbStatusView, "gbStatusView");
            this.gbStatusView.Name = "gbStatusView";
            this.gbStatusView.TabStop = false;
            // 
            // labDate
            // 
            resources.ApplyResources(this.labDate, "labDate");
            this.labDate.ForeColor = System.Drawing.SystemColors.ButtonShadow;
            this.labDate.Name = "labDate";
            // 
            // labStatusView
            // 
            this.labStatusView.Image = global::Yw_BK.Properties.Resources.Img_BK;
            resources.ApplyResources(this.labStatusView, "labStatusView");
            this.labStatusView.Name = "labStatusView";
            // 
            // txbUrl
            // 
            resources.ApplyResources(this.txbUrl, "txbUrl");
            this.txbUrl.Name = "txbUrl";
            this.txbUrl.TabStop = false;
            this.txbUrl.Validated += new System.EventHandler(this.TxbUrl_Validated);
            // 
            // labUrl
            // 
            resources.ApplyResources(this.labUrl, "labUrl");
            this.labUrl.Name = "labUrl";
            // 
            // FrmJZ
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Pink;
            this.Controls.Add(this.txbUrl);
            this.Controls.Add(this.labUrl);
            this.Controls.Add(this.gbStatusView);
            this.Controls.Add(this.gbMainCtrl);
            this.Controls.Add(this.ssBottomLab);
            this.Controls.Add(this.msMenu);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FrmJZ";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FrmJZ_FormClosing);
            this.Load += new System.EventHandler(this.FrmJZ_Load);
            this.msMenu.ResumeLayout(false);
            this.msMenu.PerformLayout();
            this.ssBottomLab.ResumeLayout(false);
            this.ssBottomLab.PerformLayout();
            this.gbMainCtrl.ResumeLayout(false);
            this.gbStatusView.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip msMenu;
        private System.Windows.Forms.ToolStripMenuItem tsmiConfig;
        private System.Windows.Forms.ToolStripMenuItem miFunction;
        private System.Windows.Forms.ToolStripMenuItem tsmiRemind;
        private System.Windows.Forms.ToolStripMenuItem miSimTestBtn;
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
        private System.Windows.Forms.ToolStripMenuItem tsmiTransfer;
        private System.Windows.Forms.Button btnMatch;
        private System.Windows.Forms.ToolStripMenuItem tsmiExclude;
        private System.Windows.Forms.ToolStripMenuItem tsmiNameReplace;
        private System.Windows.Forms.ToolStripStatusLabel labServerStatus;
        private System.Windows.Forms.Label labDate;
        private System.Windows.Forms.ToolStripMenuItem tsmiApiSwitch;
    }
}

