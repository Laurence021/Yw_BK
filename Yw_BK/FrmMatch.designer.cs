namespace Yw_BK
{
    partial class FrmMatch
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmMatch));
            gbMatchView = new GroupBox();
            tvMatch = new TreeView();
            menuGameInfo = new ContextMenuStrip(components);
            menuItemGameInfo = new ToolStripMenuItem();
            menuCopyGid = new ToolStripMenuItem();
            menuAddExclude = new ToolStripMenuItem();
            ToolStripMenuItem = new ToolStripMenuItem();
            miCopySourceId = new ToolStripMenuItem();
            miCopyTeam = new ToolStripMenuItem();
            menuPlay = new ContextMenuStrip(components);
            menuItemPlay = new ToolStripMenuItem();
            menuLeague = new ContextMenuStrip(components);
            menuItemClearTest = new ToolStripMenuItem();
            toolStripSeparator2 = new ToolStripSeparator();
            miBlockGameLineLeague = new ToolStripMenuItem();
            miBlockQ2League = new ToolStripMenuItem();
            miBlockQ4League = new ToolStripMenuItem();
            miBlock2HalfLeague = new ToolStripMenuItem();
            miUniqueControlLeague = new ToolStripMenuItem();
            miCopyLeague = new ToolStripMenuItem();
            menuGpdm = new ContextMenuStrip(components);
            menuItemClearTest1 = new ToolStripMenuItem();
            menuRoot = new ContextMenuStrip(components);
            menuItemClearTest2 = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            miBlockGameLineRoot = new ToolStripMenuItem();
            miBlockQ2Root = new ToolStripMenuItem();
            miBlockQ4Root = new ToolStripMenuItem();
            miBlock2HalfRoot = new ToolStripMenuItem();
            gbMatchView.SuspendLayout();
            menuGameInfo.SuspendLayout();
            menuPlay.SuspendLayout();
            menuLeague.SuspendLayout();
            menuGpdm.SuspendLayout();
            menuRoot.SuspendLayout();
            SuspendLayout();
            // 
            // gbMatchView
            // 
            resources.ApplyResources(gbMatchView, "gbMatchView");
            gbMatchView.Controls.Add(tvMatch);
            gbMatchView.Name = "gbMatchView";
            gbMatchView.TabStop = false;
            // 
            // tvMatch
            // 
            resources.ApplyResources(tvMatch, "tvMatch");
            tvMatch.BackColor = SystemColors.ActiveBorder;
            tvMatch.BorderStyle = BorderStyle.FixedSingle;
            tvMatch.CheckBoxes = true;
            tvMatch.FullRowSelect = true;
            tvMatch.HideSelection = false;
            tvMatch.Name = "tvMatch";
            tvMatch.TabStop = false;
            tvMatch.AfterCheck += TvMatch_AfterCheck;
            tvMatch.MouseClick += TvMatch_MouseClick;
            // 
            // menuGameInfo
            // 
            resources.ApplyResources(menuGameInfo, "menuGameInfo");
            menuGameInfo.Items.AddRange(new ToolStripItem[] { menuItemGameInfo, menuCopyGid, menuAddExclude, ToolStripMenuItem, miCopySourceId, miCopyTeam });
            menuGameInfo.Name = "contextMenuStrip1";
            // 
            // menuItemGameInfo
            // 
            resources.ApplyResources(menuItemGameInfo, "menuItemGameInfo");
            menuItemGameInfo.Name = "menuItemGameInfo";
            // 
            // menuCopyGid
            // 
            resources.ApplyResources(menuCopyGid, "menuCopyGid");
            menuCopyGid.Name = "menuCopyGid";
            menuCopyGid.Click += MenuCopyGid_Click;
            // 
            // menuAddExclude
            // 
            resources.ApplyResources(menuAddExclude, "menuAddExclude");
            menuAddExclude.Name = "menuAddExclude";
            menuAddExclude.Click += MenuAddExclude_Click;
            // 
            // ToolStripMenuItem
            // 
            resources.ApplyResources(ToolStripMenuItem, "ToolStripMenuItem");
            ToolStripMenuItem.Name = "ToolStripMenuItem";
            ToolStripMenuItem.Click += MenuItemClearTest_Click;
            // 
            // miCopySourceId
            // 
            resources.ApplyResources(miCopySourceId, "miCopySourceId");
            miCopySourceId.Name = "miCopySourceId";
            miCopySourceId.Click += MiCopySourceId_Click;
            // 
            // miCopyTeam
            // 
            resources.ApplyResources(miCopyTeam, "miCopyTeam");
            miCopyTeam.Name = "miCopyTeam";
            miCopyTeam.Click += MiCopyTeam_Click;
            // 
            // menuPlay
            // 
            resources.ApplyResources(menuPlay, "menuPlay");
            menuPlay.Items.AddRange(new ToolStripItem[] { menuItemPlay });
            menuPlay.Name = "menuPlay";
            // 
            // menuItemPlay
            // 
            resources.ApplyResources(menuItemPlay, "menuItemPlay");
            menuItemPlay.Name = "menuItemPlay";
            menuItemPlay.Click += MenuItemPlay_Click;
            // 
            // menuLeague
            // 
            resources.ApplyResources(menuLeague, "menuLeague");
            menuLeague.Items.AddRange(new ToolStripItem[] { menuItemClearTest, toolStripSeparator2, miBlockGameLineLeague, miBlockQ2League, miBlockQ4League, miBlock2HalfLeague, miUniqueControlLeague, miCopyLeague });
            menuLeague.Name = "menuLeague";
            menuLeague.Opening += MenuBlock_Opening;
            // 
            // menuItemClearTest
            // 
            resources.ApplyResources(menuItemClearTest, "menuItemClearTest");
            menuItemClearTest.Name = "menuItemClearTest";
            menuItemClearTest.Click += MenuItemClearTest_Click;
            // 
            // toolStripSeparator2
            // 
            resources.ApplyResources(toolStripSeparator2, "toolStripSeparator2");
            toolStripSeparator2.Name = "toolStripSeparator2";
            // 
            // miBlockGameLineLeague
            // 
            resources.ApplyResources(miBlockGameLineLeague, "miBlockGameLineLeague");
            miBlockGameLineLeague.CheckOnClick = true;
            miBlockGameLineLeague.Name = "miBlockGameLineLeague";
            miBlockGameLineLeague.CheckedChanged += MiBlockSameGameLine_CheckedChanged;
            // 
            // miBlockQ2League
            // 
            resources.ApplyResources(miBlockQ2League, "miBlockQ2League");
            miBlockQ2League.CheckOnClick = true;
            miBlockQ2League.Name = "miBlockQ2League";
            miBlockQ2League.CheckedChanged += MiBlockQ2_CheckedChanged;
            // 
            // miBlockQ4League
            // 
            resources.ApplyResources(miBlockQ4League, "miBlockQ4League");
            miBlockQ4League.CheckOnClick = true;
            miBlockQ4League.Name = "miBlockQ4League";
            miBlockQ4League.CheckedChanged += MiBlockQ4_CheckedChanged;
            // 
            // miBlock2HalfLeague
            // 
            resources.ApplyResources(miBlock2HalfLeague, "miBlock2HalfLeague");
            miBlock2HalfLeague.CheckOnClick = true;
            miBlock2HalfLeague.Name = "miBlock2HalfLeague";
            miBlock2HalfLeague.CheckedChanged += MiBlock2Half_CheckedChanged;
            // 
            // miUniqueControlLeague
            // 
            resources.ApplyResources(miUniqueControlLeague, "miUniqueControlLeague");
            miUniqueControlLeague.CheckOnClick = true;
            miUniqueControlLeague.Name = "miUniqueControlLeague";
            miUniqueControlLeague.CheckedChanged += MiUniqueControlLeague_CheckedChanged;
            // 
            // miCopyLeague
            // 
            resources.ApplyResources(miCopyLeague, "miCopyLeague");
            miCopyLeague.Name = "miCopyLeague";
            miCopyLeague.Click += MiCopyLeague_Click;
            // 
            // menuGpdm
            // 
            resources.ApplyResources(menuGpdm, "menuGpdm");
            menuGpdm.Items.AddRange(new ToolStripItem[] { menuItemClearTest1 });
            menuGpdm.Name = "menuGpdm";
            // 
            // menuItemClearTest1
            // 
            resources.ApplyResources(menuItemClearTest1, "menuItemClearTest1");
            menuItemClearTest1.Name = "menuItemClearTest1";
            menuItemClearTest1.Click += MenuItemClearTest_Click;
            // 
            // menuRoot
            // 
            resources.ApplyResources(menuRoot, "menuRoot");
            menuRoot.Items.AddRange(new ToolStripItem[] { menuItemClearTest2, toolStripSeparator1, miBlockGameLineRoot, miBlockQ2Root, miBlockQ4Root, miBlock2HalfRoot });
            menuRoot.Name = "menuRoot";
            menuRoot.Opening += MenuBlock_Opening;
            // 
            // menuItemClearTest2
            // 
            resources.ApplyResources(menuItemClearTest2, "menuItemClearTest2");
            menuItemClearTest2.Name = "menuItemClearTest2";
            menuItemClearTest2.Click += MenuItemClearTest_Click;
            // 
            // toolStripSeparator1
            // 
            resources.ApplyResources(toolStripSeparator1, "toolStripSeparator1");
            toolStripSeparator1.Name = "toolStripSeparator1";
            // 
            // miBlockGameLineRoot
            // 
            resources.ApplyResources(miBlockGameLineRoot, "miBlockGameLineRoot");
            miBlockGameLineRoot.CheckOnClick = true;
            miBlockGameLineRoot.Name = "miBlockGameLineRoot";
            miBlockGameLineRoot.CheckedChanged += MiBlockSameGameLine_CheckedChanged;
            // 
            // miBlockQ2Root
            // 
            resources.ApplyResources(miBlockQ2Root, "miBlockQ2Root");
            miBlockQ2Root.CheckOnClick = true;
            miBlockQ2Root.Name = "miBlockQ2Root";
            miBlockQ2Root.CheckedChanged += MiBlockQ2_CheckedChanged;
            // 
            // miBlockQ4Root
            // 
            resources.ApplyResources(miBlockQ4Root, "miBlockQ4Root");
            miBlockQ4Root.CheckOnClick = true;
            miBlockQ4Root.Name = "miBlockQ4Root";
            miBlockQ4Root.CheckedChanged += MiBlockQ4_CheckedChanged;
            // 
            // miBlock2HalfRoot
            // 
            resources.ApplyResources(miBlock2HalfRoot, "miBlock2HalfRoot");
            miBlock2HalfRoot.CheckOnClick = true;
            miBlock2HalfRoot.Name = "miBlock2HalfRoot";
            miBlock2HalfRoot.CheckedChanged += MiBlock2Half_CheckedChanged;
            // 
            // FrmMatch
            // 
            resources.ApplyResources(this, "$this");
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(gbMatchView);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "FrmMatch";
            FormClosing += FrmMatch_FormClosing;
            Load += MatchForm_Load;
            gbMatchView.ResumeLayout(false);
            menuGameInfo.ResumeLayout(false);
            menuPlay.ResumeLayout(false);
            menuLeague.ResumeLayout(false);
            menuGpdm.ResumeLayout(false);
            menuRoot.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.GroupBox gbMatchView;
        private System.Windows.Forms.TreeView tvMatch;
        private System.Windows.Forms.ContextMenuStrip menuGameInfo;
        private System.Windows.Forms.ToolStripMenuItem menuItemGameInfo;
        private System.Windows.Forms.ContextMenuStrip menuPlay;
        private System.Windows.Forms.ToolStripMenuItem menuCopyGid;
        private System.Windows.Forms.ToolStripMenuItem menuAddExclude;
        private System.Windows.Forms.ContextMenuStrip menuLeague;
        private System.Windows.Forms.ToolStripMenuItem menuItemClearTest;
        private System.Windows.Forms.ContextMenuStrip menuGpdm;
        private System.Windows.Forms.ToolStripMenuItem menuItemClearTest1;
        private System.Windows.Forms.ContextMenuStrip menuRoot;
        private System.Windows.Forms.ToolStripMenuItem menuItemClearTest2;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem menuItemPlay;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripMenuItem miBlockGameLineLeague;
        private ToolStripMenuItem miBlockQ2League;
        private ToolStripMenuItem miBlockQ4League;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem miBlockGameLineRoot;
        private ToolStripMenuItem miBlockQ2Root;
        private ToolStripMenuItem miBlockQ4Root;
        private ToolStripMenuItem miBlock2HalfRoot;
        private ToolStripMenuItem miBlock2HalfLeague;
        private ToolStripMenuItem miUniqueControlLeague;
        private ToolStripMenuItem miCopyLeague;
        private ToolStripMenuItem miCopySourceId;
        private ToolStripMenuItem miCopyTeam;
    }
}