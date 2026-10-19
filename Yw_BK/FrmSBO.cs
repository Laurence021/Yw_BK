#region 功能修改描述與時間

/*
    描述：來源視窗
    日期：2022-04-14
*/

#endregion

using Gp.MonitorService;
using System.Net;
using System.Reflection;
using Yw_BK.main;
using Yw_BK.model;
using YwCore;
using YwCore.form;
using YwCore.model.interfaces;

namespace Yw_BK
{
    public partial class FrmSBO : Form, IChangeFormDesign
    {
        //Timer計算更新秒數使用
        private int _notUpdateCount = 0;
        /// <summary>
        /// 委派控制視窗顏色
        /// </summary>
        /// <param name="color"></param>
        public delegate void ChangeFormBackColor(Color color);

        #region 子頁面

        //參數設置頁
        private FrmConfig _frmConfig = null;
        //代理頁面
        private FrmProxyV2 _frmProxy = null;
        //主流程
        private Process _process = null;
        //聯盟預設後綴頁面
        private FrmSuffix _frmSuffix = null;
        //GP轉換頁面
        private FrmTransfer _frmTransfer = null;
        //設定賽事頁
        private FrmMatch _frmMatch = null;
        //訊息提醒設置頁面
        private FrmRemind _frmRemind = null;
        //排除關閉賽事頁面
        private FrmExclude _frmExclude = null;
        //來源隊伍名稱替換設置頁
        private FrmTeamNameReplace _frmNameReplace = null;
        //排除聯盟頁
        private FrmFilterLeague _frmFilterLeague = null;
        //來源聯盟名稱替換設置頁
        private FrmLeagueNameReplaceV2 _frmLeagueReplace = null;

        #endregion

        /// <summary>
        ///建構子
        /// </summary>
        public FrmSBO()
        {
            //綁定主視窗文字變動事件
            this.TextChanged += FrmSBO_TextChanged;

            InitializeComponent();
            //設定版本號
            labDate.Text = Config.version;

            //設定來源對應網址
            switch (Config.source)
            {
                case Source.SBO:
                    txbUrl.Text = Properties.Settings.Default.UrlSBO;
                    break;
            }

            //設定來源網址
            Config.url = txbUrl.Text;
            //設定變更畫面行為
            Config.ChangeFormDesign = this;
            //記錄開啟
            LogFile.SetLog(LogMode.Control, Text + " 狀態:開始執行, YwCore: " + YwCore.Common.version);
        }

        /// <summary>
        /// 載入視窗
        /// </summary>
        private void FrmSBO_Load(object sender, EventArgs e)
        {
            //子頁面建構
            _frmConfig = new FrmConfig();
            _frmProxy = new FrmProxyV2(Config.Proxy) { Icon = this.Icon };
            _process = new Process();
            _frmSuffix = new FrmSuffix();
            _frmTransfer = new FrmTransfer();
            _frmRemind = new FrmRemind();
            _frmExclude = new FrmExclude();
            _frmNameReplace = new FrmTeamNameReplace(Config.GetReplaceTeamService, ConfigFilePath.TeamReplacePath);
            _frmFilterLeague = new FrmFilterLeague(1, Common.GetApplicationName(Common.source, Entrance.None), Config.FilterLeagueHashSet);
            _frmFilterLeague.FilterChange += (obj, args) => Config.ConfigChange();
            _frmLeagueReplace = new FrmLeagueNameReplaceV2(Config.GetReplaceLeagueService, ConfigFilePath.LeagueReplacePath);
        }

        /// <summary>
        /// 設置URL
        /// </summary>
        private void TxbUrl_Validated(object sender, EventArgs e)
        {
            txbUrl.Text = txbUrl.Text.Trim('/', ' ') + '/';
            Config.url = txbUrl.Text;
            Properties.Settings.Default.UrlSBO = txbUrl.Text;
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// toolStripMenuItem事件觸發(開啟對應子視窗)
        /// </summary>
        private void ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //轉換型態
            ToolStripMenuItem item = (ToolStripMenuItem)sender;

            //對應視窗
            switch (item.Name)
            {
                case "tsmiConfig":

                    if (_frmConfig.Visible)
                    {
                        _frmConfig.BringToFront();
                    }
                    else
                    {
                        _frmConfig = new FrmConfig();
                        _frmConfig.Show();
                    }

                    break;
                case "tsmiProxy":

                    if (_frmProxy.Visible)
                    {
                        _frmProxy.BringToFront();
                    }
                    else
                    {
                        _frmProxy = new FrmProxyV2(Config.Proxy) { Icon = this.Icon };
                        _frmProxy.Show();
                        LogFile.SetLog(LogMode.Control, item.Text + " 狀態:開啟");
                    }

                    break;
                case "tsmiSuffix":

                    if (_frmSuffix.Visible)
                    {
                        _frmSuffix.BringToFront();
                    }
                    else
                    {
                        _frmSuffix = new FrmSuffix();
                        _frmSuffix.Show();
                        LogFile.SetLog(LogMode.Control, item.Text + " 狀態:開啟");
                    }

                    break;
                case "tsmiTransfer":

                    if (_frmTransfer.Visible)
                    {
                        _frmTransfer.BringToFront();
                    }
                    else
                    {
                        Source transferSource = Config.ChooseTransferSource();
                        _frmTransfer = new FrmTransfer(transferSource);
                        _frmTransfer.Show();
                        _frmTransfer.FormClosed += (a, b) => { Config.isTransfer = false; };
                        LogFile.SetLog(LogMode.Control, item.Text + " 狀態:開啟");
                    }

                    Config.isTransfer = true;
                    break;
                case "tsmiRemind":

                    if (_frmRemind.Visible)
                    {
                        _frmRemind.BringToFront();
                    }
                    else
                    {
                        _frmRemind = new FrmRemind();
                        _frmRemind.Show();
                        LogFile.SetLog(LogMode.Control, item.Text + " 狀態:開啟");
                    }

                    break;
                case "tsmiExclude":
                    if (_frmExclude.Visible)
                    {
                        _frmExclude.BringToFront();
                    }
                    else
                    {
                        _frmExclude = new FrmExclude();
                        _frmExclude.Show();
                        LogFile.SetLog(LogMode.Control, item.Text + " 狀態:開啟");
                    }

                    break;
                case "tsmiNameReplace":
                    if (_frmNameReplace.Visible)
                    {
                        _frmNameReplace.BringToFront();
                    }
                    else
                    {
                        _frmNameReplace = new FrmTeamNameReplace(Config.GetReplaceTeamService, ConfigFilePath.TeamReplacePath);
                        _frmNameReplace.Show();
                        LogFile.SetLog(LogMode.Control, item.Text + " 狀態:開啟");
                    }
                    break;
                case "tsmiFilterMode":
                    if (_frmFilterLeague.Visible)
                    {
                        _frmFilterLeague.BringToFront();
                    }
                    else
                    {
                        _frmFilterLeague = new FrmFilterLeague(1, Common.GetApplicationName(Common.source, Entrance.None), Config.FilterLeagueHashSet);
                        _frmFilterLeague.FilterChange += (obj, args) => Config.ConfigChange();
                        _frmFilterLeague.Show();
                        LogFile.SetLog(LogMode.Control, item.Text + " 狀態:開啟");
                    }
                    break;
                case "tsmiLeagueReplace":
                    if (_frmLeagueReplace.Visible)
                    {
                        _frmLeagueReplace.BringToFront();
                    }
                    else
                    {
                        _frmLeagueReplace = new FrmLeagueNameReplaceV2(Config.GetReplaceLeagueService, ConfigFilePath.LeagueReplacePath);
                        _frmLeagueReplace.Show();
                        LogFile.SetLog(LogMode.Control, item.Text + " 狀態:開啟");
                    }
                    break;
            }
        }

        /// <summary>
        /// 登入
        /// </summary>
        private void BtnLogin_Click(object sender, EventArgs e)
        {
            LogFile.SetLog(LogMode.Control, "登入 狀態:點擊");

            if (string.IsNullOrEmpty(txbUrl.Text) || !Uri.TryCreate(txbUrl.Text, UriKind.Absolute, out Uri uri))
            {
                MessageBox.Show(YwCore.Properties.Resources.ErrorUrl, YwCore.Properties.Resources.Remind);
                return;
            }

            btnLogin.Enabled = false;
            btnFollow.Enabled = true;
            btnUpdate.Enabled = true;
            btnMatch.Enabled = true;
            btnResetBuffer.Enabled = true;

            //啟動主流程
            _process.Start();
        }

        /// <summary>
        /// 視窗關閉前執行
        /// </summary>
        private void FrmSBO_FormClosing(object sender, FormClosingEventArgs e)
        {
            FrmCustomClose frmCustom = new FrmCustomClose();
            DialogResult result = frmCustom.Show(Config.isFollow);

            if (result == DialogResult.No)
            {
                e.Cancel = true;
            }
            else if (result == DialogResult.Retry)
            {
                Config.RestartApplication();
                // 記錄重啟行為
                LogFile.SetLog(LogMode.Control, Text + " 狀態:重新啟動, YwCore: " + Common.version);
            }
            else
            {
                //記錄關閉
                LogFile.SetLog(LogMode.Control, Text + " 狀態:結束執行, YwCore: " + Common.version);
            }
        }

        /// <summary>
        /// 隱藏子頁
        /// </summary>
        private void TsmiHideFrm_Click(object sender, EventArgs e)
        {
            LogFile.SetLog(LogMode.Control, "隱藏子頁 狀態:點擊");
            Type type = this.GetType();
            var fields = type.GetRuntimeFields().Where(a => a.FieldType.IsSubclassOf(typeof(Form)));

            foreach (var field in fields)
            {
                var form = field.GetValue(this);
                ((Form)form)?.Hide();
            }
        }

        /// <summary>
        /// 最小化
        /// </summary>
        private void TsmiMinimized_Click(object sender, EventArgs e)
        {
            LogFile.SetLog(LogMode.Control, "最小化 狀態:點擊");
            WindowState = FormWindowState.Minimized;
            Type type = this.GetType();
            var fields = type.GetRuntimeFields().Where(a => a.FieldType.IsSubclassOf(typeof(Form)));

            foreach (var field in fields)
            {
                var form = field.GetValue(this);

                if (form != null)
                {
                    ((Form)form).WindowState = FormWindowState.Minimized;
                }
            }
        }

        /// <summary>
        /// 計時器
        /// </summary>
        private void TmrTick_Tick(object sender, EventArgs e)
        {
            bool isUpdate = false;
            labStatusView.Text = Display.GetShowMsg(out isUpdate);

            if (Common.MonitorChannel.State != Grpc.Core.ConnectivityState.Ready)
            {
                labStatusView.Text += YwCore.Properties.Resources.Sync_Alert_ServiceDisconnected + Environment.NewLine;
            }

            if (Config.isFollow)
            {
                //累加次數
                _notUpdateCount++;

                //數據有更新則清除秒數
                if (isUpdate) { _notUpdateCount = 0; }

                labSecond.Text = _notUpdateCount.ToString();

            }
        }

        /// <summary>
        ///  啟動/停止
        /// </summary>
        private async void BtnFollow_Click(object sender, EventArgs e)
        {
            try
            {
                btnFollow.Enabled = false;

                if (Config.isFollow)
                {
                    if (MessageBox.Show(YwCore.Properties.Resources.ConfirmStopGP, $"{YwCore.Properties.Resources.Remind} {Common.GetApplicationName()}", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                    {
                        return;
                    }

                    await Config.RegisterMonitor.RegisterAsync(new RegisterApRequest() { Gp = new RegisterGP() { GpType = RegisterType.None, HostName = Dns.GetHostName(), ProcessId = Environment.ProcessId, Sport = (int)Config.sport, SourceId = (int)Config.source } });
                }
                else
                {
                    RegisterType registerType = RegisterType.None;

                    if (Config.isOpenDs)
                    {
                        registerType |= RegisterType.Ds;
                    }

                    if (Config.isOpenZd)
                    {
                        registerType |= RegisterType.Zd;
                    }

                    if (Config.isOpenZp)
                    {
                        registerType |= RegisterType.Zp;
                    }

                    var result = await Config.RegisterMonitor.RegisterAsync(new RegisterApRequest() { Gp = new RegisterGP() { GpType = registerType, HostName = Dns.GetHostName(), ProcessId = Environment.ProcessId, Sport = (int)Config.sport, SourceId = (int)Config.source } });

                    if (result.IsConnect)
                    {
                        if (!string.IsNullOrEmpty(result.Message))
                        {
                            MessageBox.Show(result.Message, YwCore.Properties.Resources.Remind);
                            return;
                        }
                    }
                    else
                    {
                        if (MessageBox.Show(result.Message, $"{YwCore.Properties.Resources.Remind} {Common.GetApplicationName()}", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                        {
                            return;
                        }
                    }
                }

                Common.ChangeBackColor((Control)sender);
                Config.isFollow = !Config.isFollow;

                if (Config.isFollow)
                {
                    LogFile.SetLog(LogMode.Control, "啟動GP 狀態:啟動中");
                    btnFollow.Text = YwCore.Properties.Resources.Stop;
                    labStatus.Text = YwCore.Properties.Resources.Started;
                    txbUrl.Enabled = false;
                }
                else
                {
                    LogFile.SetLog(LogMode.Control, "啟動GP 狀態:未啟動");
                    btnFollow.Text = YwCore.Properties.Resources.Start;
                    labStatus.Text = YwCore.Properties.Resources.Stopped;
                    txbUrl.Enabled = true;
                }

                btnOddsTotal.Enabled = Config.isFollow;
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
            finally
            {
                btnFollow.Enabled = true;
            }
        }

        /// <summary>
        /// 強制更新數據
        /// </summary>
        private void BtnUpdate_Click(object sender, EventArgs e)
        {
            try
            {
                LogFile.SetLog(LogMode.Control, "強制更新數據 狀態:點擊");
                _process.UpdateAllData();

                //防止短時間內重複點擊
                Control control = (Control)sender;
                control.Enabled = false;
                Task.Run(async () => { await Task.Delay(5000); YwCore.Common.SetControlEnabled(control, true); });
            }
            catch (Exception ex)
            {
                Display.SetDictInfo(Display.Type.Error, ex.Message);
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
        }

        /// <summary>
        /// 刷新副名稱緩存
        /// </summary>
        private void BtnResetBuffer_Click(object sender, EventArgs e)
        {
            LogFile.SetLog(LogMode.Control, "刷新副名稱緩存 狀態:點擊");
            YwCore.Common.ChangeBackColor((Control)sender);
            Task task = Task.Run(() => { SetField.RefreshWebNameBuf((Control)sender); });
        }

        /// <summary>
        /// 加總讀取
        /// </summary>
        private void BtnOddsTotal_Click(object sender, EventArgs e)
        {
            try
            {
                LogFile.SetLog(LogMode.Control, "差距值讀取 狀態:點擊");
                Config.CollectKindModeToQueue();
                Config.CollectOddsToQueue();

                //防止短時間內重複點擊
                Control control = (Control)sender;
                control.Enabled = false;
                Task.Run(async () => { await Task.Delay(1500); YwCore.Common.SetControlEnabled(control, true); });
            }
            catch (Exception ex)
            {
                Display.SetDictInfo(Display.Type.Error, ex.Message);
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
        }

        /// <summary>
        /// 視窗文字變動事件
        /// </summary>
        private void FrmSBO_TextChanged(object sender, EventArgs e)
        {
            this.TextChanged -= FrmSBO_TextChanged;
            this.Text += " " + Config.version;
            this.TextChanged += FrmSBO_TextChanged;
        }

        /// <summary>
        /// 啟動文字變更事件
        /// </summary>
        private void BtnFollow_TextChanged(object sender, EventArgs e)
        {
            btnFollow.TextChanged -= BtnFollow_TextChanged;

            if (Config.isFollow)
            {
                btnFollow.Text = YwCore.Properties.Resources.Stop;
            }
            else
            {
                btnFollow.Text = YwCore.Properties.Resources.Start;
            }

            btnFollow.TextChanged += BtnFollow_TextChanged;
        }

        /// <summary>
        /// 狀態文字變動事件
        /// </summary>
        private void LabStatus_TextChanged(object sender, EventArgs e)
        {
            labStatus.TextChanged -= BtnFollow_TextChanged;

            if (Config.isFollow)
            {
                labStatus.Text = YwCore.Properties.Resources.Started;
            }
            else
            {
                labStatus.Text = YwCore.Properties.Resources.Stopped;
            }

            labStatus.TextChanged += BtnFollow_TextChanged;
        }

        /// <summary>
        /// 賽事按鈕
        /// </summary>
        private void BtnMatch_Click(object sender, EventArgs e)
        {
            if (_frmMatch == null || !_frmMatch.Visible)
            {
                YwCore.Common.ChangeBackColor((Control)sender);
                _frmMatch = new FrmMatch(() => { YwCore.Common.ChangeBackColor((Control)sender); });
                _frmMatch.Show();
            }
            else
            {
                _frmMatch.Close();
            }
        }

        /// <summary>
        /// 變更畫面顏色
        /// </summary>
        /// <param name="color"></param>
        public void ChangeFormColor(Color color)
        {
            //是否為其他線程呼叫
            if (this.InvokeRequired)
            {
                if (BackColor != color)
                {
                    this.Invoke(new ChangeFormBackColor(ChangeFormColor), new object[] { color });
                }
            }
            else
            {
                BackColor = msMenu.BackColor = ssBottomLab.BackColor = color;
            }
        }
    }
}
