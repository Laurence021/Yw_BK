#region 功能修改描述與時間

/*
    描述：設置頁面
    日期：2019-01-07

    描述：添加是否跟奧運選勾(JZ用)
    日期：2021-05-13

    描述：ZD關閉歸零控件添加解鎖功能
    日期：2021-05-31

    描述：移除MAR來源
    日期：2022-07-06

    描述：PIN Dj/Xb/Ac間隔秒數依據Ds/Zp連動
    日期：2023-11-08
*/

#endregion

using Gp.MonitorService;
using System.Net;
using Yw_BK.model;
using YwCore;
using YwCore.source;

namespace Yw_BK
{
    public partial class FrmConfig : Form
    {
        //對應開頭名稱
        private string _name;
        //檔案路徑
        private string _filePath = new DirectoryInfo(Application.StartupPath).Parent.Parent.Parent.FullName + "\\Info\\ConfigStatus.txt";
        //記錄控件狀態新舊字典
        private Dictionary<string, string> oldTextDic = new Dictionary<string, string>();
        private Dictionary<string, string> newTextDic = new Dictionary<string, string>();
        //鴛鴦盤清單對應轉換
        private string[] sameCountStringArray = new string[] { "全部(All)", "1 盤(1 Lines)", "3 盤(3 Lines)", "5 盤(5 Lines)" };

        /// <summary>
        /// 建構子
        /// </summary>
        /// <param name="loginEnabled">登入按鈕的控制項是否能使用</param>
        public FrmConfig(bool loginEnabled = false)
        {
            this.TextChanged += FrmConfig_TextChanged;
            InitializeComponent();

            cbbLanguage.DisplayMember = "Key";
            cbbLanguage.ValueMember = "Value";
            cbbLanguage.DataSource = new BindingSource(YwCore.Common.langDic, null);
            cbbLanguage.SelectedIndexChanged += CbbLanguage_SelectedIndexChanged;

            if (Config.source == Source.BET365 && Config.gpType != GpType.None)
            {
                _name = Config.sport.ToString() + Config.source.ToString() + Config.gpType.ToString("g");
            }
            else
            {
                _name = Config.sport.ToString() + Config.source.ToString();
            }

            SetFormControl();
            SetConfig();

            btnSave.Enabled = false;
            BtnLock_Click(null, null);

            SetConvertMLEnabled();
            //按下登入後，鎖定特定控制項
            SetControlEnabled(loginEnabled);
        }

        //視窗載入事件
        private void FrmConfig_Load(object sender, EventArgs e)
        {
            cbbLanguage.SelectedValue = YwCore.Common.langDic.ContainsValue(Thread.CurrentThread.CurrentUICulture.Name) ? Thread.CurrentThread.CurrentUICulture.Name : cbbLanguage.SelectedValue;
            CbbLanguage_SelectedIndexChanged(sender, e);

            cbbSameCount.SelectedIndexChanged += ControlChanged;
            oldTextDic.Clear();
            LogFile.AddCtrlDic(this, oldTextDic);
            LogFile.SetLog(LogMode.Control, "參數設置版面開啟 : " + LogFile.GetText(this));
        }

        //控件變化觸發事件
        private void ControlChanged(object sender, EventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.Name == "ckbIsZdOnlyPk")
            {
                SetConvertMLEnabled();
            }

            btnSave.Enabled = true;
        }

        //設置保存
        private async void BtnSave_Click(object sender, EventArgs e)
        {
            this.Enabled = false;

            try
            {
                RegisterType registerType = RegisterType.None;

                if (ckbDs.Checked)
                {
                    registerType |= RegisterType.Ds;
                }

                if (ckbZd.Checked)
                {
                    registerType |= RegisterType.Zd;
                }

                if (ckbZp.Checked)
                {
                    registerType |= RegisterType.Zp;
                }

                //GP已開始登入流程
                if (Config.IsLogin && ((Config.isOpenZd || Config.isOpenDs || Config.isOpenZp) != (registerType != RegisterType.None)))
                {
                    if (FrmUser.users.Values.Count > 0)
                    {
                        bool isUse = registerType != RegisterType.None;
                        var result = await Config.AccountMonitor.RegisterAsync(Config.source, Config.entrance, FrmUser.users.Select(a => a.Key), isUse);

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
                }

                if (Config.isFollow)
                {
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
            }
            finally
            {
                this.Enabled = true;
            }

            LogFile.SetLog(LogMode.Control, "參數設置版面 - 設置保存 狀態:點擊");
            //設置全域變數
            SetConfig();
            //控件狀態儲存至檔案
            SetStatusToFile();
            newTextDic.Clear();
            LogFile.AddCtrlDic(this, newTextDic);
            //控件狀態紀錄
            LogFile.SetLog(LogMode.Control, "參數設置版面 - 設置保存 狀態:設置成功 " + LogFile.GetChangeText(newTextDic, ref oldTextDic));
            //觸發Config異動事件
            Config.ConfigChange();

            SetLockStatus(false);
            btnSave.Enabled = false;
        }

        //設定視窗控件
        private void SetFormControl()
        {
            try
            {
                //參數狀態
                string configStatus = GetFileConfig();

                if (Config.source == Source.PIN)
                {
                    gbPIN.Enabled = true;

                    //"全部"選項不加入
                    for (int i = 1; i < sameCountStringArray.Length; i++)
                    {
                        cbbSameCount.Items.Add(sameCountStringArray[i]);
                    }

                    cbbSameCount.SelectedIndex = 1;
                }
                else if (Config.source == Source.BET365)
                {
                    gbHtXbDj.Enabled = true;
                    ckbZp.Enabled = false;
                    labZpSec.Enabled = txbZpSec.Enabled = false;

                    switch (Config.gpType)
                    {
                        case GpType.Ds:
                            gbZd.Enabled = false;
                            ckbClearZero.Enabled = false;
                            break;
                        case GpType.Zd:
                            gbDs.Enabled = false;
                            ckbOTConvertML.Visible = true;
                            ckbNBAConvertML.Visible = true;
                            break;
                    }
                }
                else if (Config.source == Source.JZ)
                {
                    ckbZp.Enabled = false;
                    txbZpSec.Enabled = false;
                    ckbIsTrackOlympics.Visible = true;
                }
                else if (Config.source == Source.SBO)
                {
                    ckbZp.Enabled = false;
                    txbZpSec.Enabled = false;
                    gbPIN.Enabled = true;
                    ckbDj.Enabled = false;
                    ckbXb.Enabled = false;
                    ckbAc.Enabled = false;
                    ckbSbDjSame.Enabled = false;
                    txbDjSec.Enabled = false;
                    txbXbSec.Enabled = false;
                    txbAcSec.Enabled = false;
                    cbbSameCount.Items.AddRange(new string[] { "1 盤(1 Lines)", "3 盤(3 Lines)" });
                    cbbSameCount.SelectedIndex = 1;
                }
                else if (Config.source == Source.MyStake)
                {
                    gbDs.Enabled = false;
                }
                else if (Config.source == Source.Radar)
                {
                    //gbDs.Enabled = false;
                    ckbZp.Enabled = false;
                    txbZpSec.Enabled = false;

                    gbPIN.Enabled = true;
                    ckbDj.Enabled = false;
                    ckbXb.Enabled = false;
                    ckbAc.Enabled = false;
                    txbDjSec.Enabled = false;
                    txbXbSec.Enabled = false;
                    txbAcSec.Enabled = false;

                    //"全部"選項不加入
                    for (int i = 1; i < sameCountStringArray.Length; i++)
                    {
                        cbbSameCount.Items.Add(sameCountStringArray[i]);
                    }

                    cbbSameCount.SelectedIndex = 1;
                }
                else if (Config.source == Source.XB)
                {
                    gbPIN.Enabled = true;
                    ckbDj.Enabled = false;
                    ckbXb.Enabled = false;
                    ckbAc.Enabled = false;
                    txbDjSec.Enabled = false;
                    txbXbSec.Enabled = false;
                    txbAcSec.Enabled = false;

                    //"全部"選項不加入
                    for (int i = 1; i < sameCountStringArray.Length; i++)
                    {
                        cbbSameCount.Items.Add(sameCountStringArray[i]);
                    }

                    cbbSameCount.SelectedIndex = 1;
                }
                else if (Config.source == Source.SB)
                {
                    gbPIN.Enabled = true;
                    ckbDj.Enabled = false;
                    ckbXb.Enabled = false;
                    ckbAc.Enabled = false;
                    txbDjSec.Enabled = false;
                    txbXbSec.Enabled = false;
                    txbAcSec.Enabled = false;

                    //"全部"選項不加入
                    for (int i = 1; i < sameCountStringArray.Length; i++)
                    {
                        cbbSameCount.Items.Add(sameCountStringArray[i]);
                    }

                    cbbSameCount.SelectedIndex = 1;
                }

                //字串不為空值才處理
                if (!string.IsNullOrEmpty(configStatus))
                {
                    //切割狀態
                    string[] status = configStatus.Split('^');
                    //陣列索引
                    int index = 0;
                    //長度不符則跳過
                    if (status.Length != 48) { return; }

                    #region CheckBox狀態

                    //Ds
                    ckbDs.Checked = status[index++] == "1" ? true : false;
                    ckbDsOE.Checked = status[index++] == "1" ? true : false;

                    //Zd
                    ckbZd.Checked = status[index++] == "1" ? true : false;
                    ckbZdOE.Checked = status[index++] == "1" ? true : false;
                    ckbAutoCreateGame.Checked = status[index++] == "1" ? true : false;

                    //Zp
                    ckbZpTomorrow.Checked = status[index++] == "1" ? true : false;
                    ckbZp.Checked = status[index++] == "1" ? true : false;

                    //Dj
                    ckbDj.Checked = status[index++] == "1" ? true : false;

                    //Xb
                    ckbXb.Checked = status[index++] == "1" ? true : false;

                    //Ac
                    ckbAc.Checked = status[index++] == "1" ? true : false;

                    //設置
                    ckbIsProxy.Checked = status[index++] == "1" ? true : false;
                    ckbSbDjSame.Checked = status[index++] == "1" ? true : false;
                    ckbClearZero.Checked = status[index++] == "1" ? true : false;
                    //只跟PK
                    ckbIsDsOnlyPk.Checked = status[index++] == "1" ? true : false;
                    ckbIsZdOnlyPk.Checked = status[index++] == "1" ? true : false;
                    //是否跟奧運
                    ckbIsTrackOlympics.Checked = status[index++] == "1" ? true : false;

                    //是否轉換正盤
                    ckbOTConvertML.Checked = status[index++] == "1" ? true : false;
                    ckbNBAConvertML.Checked = status[index++] == "1" ? true : false;

                    #endregion

                    #region TextBox狀態

                    //Ds
                    txbDsSec.Text = status[index++];
                    txbDsOERatio.Text = status[index++];
                    txbDsRf.Text = status[index++];
                    txbDsOu.Text = status[index++];
                    txbDsOe.Text = status[index++];
                    txbDsDy.Text = status[index++];

                    //Zd
                    txbZdSec.Text = status[index++];
                    txbZdOERatio.Text = status[index++];
                    txbZdRf.Text = status[index++];
                    txbZdOu.Text = status[index++];
                    txbZdOe.Text = status[index++];
                    txbZdDy.Text = status[index++];

                    //Zp
                    txbZpSec.Text = status[index++];

                    //PIN設置
                    txbDjSec.Text = status[index++];
                    txbXbSec.Text = status[index++];
                    txbAcSec.Text = status[index++];

                    //365設置
                    txbHtRf.Text = status[index++];
                    txbHtOu.Text = status[index++];
                    txbHtOe.Text = status[index++];
                    txbHtDy.Text = status[index++];
                    txbXbRf.Text = status[index++];
                    txbXbOu.Text = status[index++];
                    txbXbOe.Text = status[index++];
                    txbXbDy.Text = status[index++];
                    txbDjRf.Text = status[index++];
                    txbDjOu.Text = status[index++];
                    txbDjOe.Text = status[index++];
                    txbDjDy.Text = status[index++];

                    #endregion

                    #region ComboBox狀態

                    int selectedIndex = status[index] == "0" ? 0 : status[index] == "1" ? 1 : status[index] == "2" ? 2 : status[index] == "3" ? 3 : 2;
                    //索引符合範圍內才設置
                    if (cbbSameCount.Items.Count > selectedIndex) { cbbSameCount.SelectedIndex = selectedIndex; }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
        }

        //設定全域變數
        private void SetConfig()
        {
            #region CheckBox

            if (Config.source == Source.BET365)
            {
                //Ds
                Config.isOpenDs = ckbDs.Checked && Config.gpType == GpType.Ds;
                Config.isOpenDsZpOe = ckbDsOE.Checked;

                //Zd
                Config.isOpenZd = ckbZd.Checked && Config.gpType == GpType.Zd;
                Config.isOpenZdOe = ckbZdOE.Checked;

                //Zp
                Config.isOpenZp = ckbZp.Checked;
            }
            else
            {
                //Ds
                Config.isOpenDs = ckbDs.Checked;
                Config.isOpenDsZpOe = ckbDsOE.Checked;

                //Zd
                Config.isOpenZd = ckbZd.Checked;
                Config.isOpenZdOe = ckbZdOE.Checked;

                //Zp
                Config.isTomorrow = ckbZpTomorrow.Checked;
                Config.isOpenZp = ckbZp.Checked;

                //Dj
                Config.isOpenDj = ckbDj.Checked;

                //Xb
                Config.isOpenXb = ckbXb.Checked;

                //Ac
                Config.isOpenAc = ckbAc.Checked;
            }

            if (ckbAutoCreateGame.Enabled)
            {
                Config.AutoCreateZdGame = ckbAutoCreateGame.Checked;

            }

            //設置
            Config.isProxy = ckbIsProxy.Checked;
            Config.isSbDjSame = ckbSbDjSame.Checked;
            Config.isOddsClearZero = ckbClearZero.Checked;
            Config.isDsOnlyPK = ckbIsDsOnlyPk.Checked;
            Config.isZdOnlyPK = ckbIsZdOnlyPk.Checked;
            Config.isTrackOlympics = ckbIsTrackOlympics.Checked;
            Config.isOTConvertML = ckbOTConvertML.Checked;
            Config.isNBAConvertML = ckbNBAConvertML.Checked;

            #endregion

            #region TextBox

            decimal value;
            int temp;
            //DS限制最低秒數5S
            int dsLimitSec = 5000;
            //ZP限制最低秒數10S
            int zpLimitSec = 10000;

            switch (Config.source)
            {
                //365DS秒數不可低於20S
                case Source.BET365:
                    dsLimitSec = 20000;
                    break;
                //JZ不受限制
                case Source.JZ:
                case Source.Radar:
                    dsLimitSec = 0;
                    break;
                case Source.XB:
                    dsLimitSec = 30000;
                    zpLimitSec = 30000;
                    break;
            }

            int tmpDsSec = int.TryParse(txbDsSec.Text, out temp) ? (temp == 0 ? 5000 : temp * 1000) : 5000;
            int tmpZpSec = int.TryParse(txbZpSec.Text, out temp) ? (temp == 0 ? 10000 : temp * 1000) : 10000;

            //Ds
            Config.dsSec = tmpDsSec < dsLimitSec ? dsLimitSec : tmpDsSec;
            txbDsSec.Text = txbDsSec.Enabled == true ? (Config.dsSec / 1000).ToString() : txbDsSec.Text;
            Config.dsOERatio = decimal.TryParse(txbDsOERatio.Text, out value) ? value : 0m;
            Config.gapDsRf = decimal.TryParse(txbDsRf.Text, out value) ? value : 0m;
            Config.gapDsOu = decimal.TryParse(txbDsOu.Text, out value) ? value : 0m;
            Config.gapDsOe = decimal.TryParse(txbDsOe.Text, out value) ? value : 0m;
            Config.gapDsDy = decimal.TryParse(txbDsDy.Text, out value) ? value : 0m;

            //Zd
            Config.zdSec = int.TryParse(txbZdSec.Text, out temp) ? (temp == 0 ? 2000 : temp * 1000) : 2000;
            Config.zdOERatio = decimal.TryParse(txbZdOERatio.Text, out value) ? value : 0m;
            Config.gapZdRf = decimal.TryParse(txbZdRf.Text, out value) ? value : 0m;
            Config.gapZdOu = decimal.TryParse(txbZdOu.Text, out value) ? value : 0m;
            Config.gapZdOe = decimal.TryParse(txbZdOe.Text, out value) ? value : 0m;
            Config.gapZdDy = decimal.TryParse(txbZdDy.Text, out value) ? value : 0m;

            //Zp
            Config.zpSec = tmpZpSec < zpLimitSec ? zpLimitSec : tmpZpSec;
            txbZpSec.Text = txbZpSec.Enabled == true ? (Config.zpSec / 1000).ToString() : txbZpSec.Text;

            //PIN設置

            //判斷開關取最小間隔
            List<int> list = new List<int>();
            if (Config.isOpenDs) list.Add(Config.dsSec);
            if (Config.isOpenZp) list.Add(Config.zpSec);
            int otherSec = list.Count == 0 ? Config.dsSec : list.Min();

            Config.djSec = otherSec;
            Config.xbSec = otherSec;
            Config.acSec = otherSec;
            txbDjSec.Text = txbDjSec.Enabled == true ? (Config.djSec / 1000).ToString() : txbDjSec.Text;
            txbXbSec.Text = txbXbSec.Enabled == true ? (Config.xbSec / 1000).ToString() : txbXbSec.Text;
            txbAcSec.Text = txbAcSec.Enabled == true ? (Config.acSec / 1000).ToString() : txbAcSec.Text;

            //365設置
            Config.gapHtRf = decimal.TryParse(txbHtRf.Text, out value) ? value : 0m;
            Config.gapHtOu = decimal.TryParse(txbHtOu.Text, out value) ? value : 0m;
            Config.gapHtOe = decimal.TryParse(txbHtOe.Text, out value) ? value : 0m;
            Config.gapHtDy = decimal.TryParse(txbHtDy.Text, out value) ? value : 0m;
            Config.gapXbRf = decimal.TryParse(txbXbRf.Text, out value) ? value : 0m;
            Config.gapXbOu = decimal.TryParse(txbXbOu.Text, out value) ? value : 0m;
            Config.gapXbOe = decimal.TryParse(txbXbOe.Text, out value) ? value : 0m;
            Config.gapXbDy = decimal.TryParse(txbXbDy.Text, out value) ? value : 0m;
            Config.gapDjRf = decimal.TryParse(txbDjRf.Text, out value) ? value : 0m;
            Config.gapDjOu = decimal.TryParse(txbDjOu.Text, out value) ? value : 0m;
            Config.gapDjOe = decimal.TryParse(txbDjOe.Text, out value) ? value : 0m;
            Config.gapDjDy = decimal.TryParse(txbDjDy.Text, out value) ? value : 0m;

            #endregion

            #region ComboBox狀態

            Config.sameCount = Array.IndexOf(sameCountStringArray, cbbSameCount.Text);

            #endregion
        }

        //儲存控件狀態至檔案
        private void SetStatusToFile()
        {
            string configStatus = string.Empty;

            #region CheckBox

            //Ds
            configStatus += (ckbDs.Checked ? 1 : 0) + "^";
            configStatus += (ckbDsOE.Checked ? 1 : 0) + "^";

            //Zd
            configStatus += (ckbZd.Checked ? 1 : 0) + "^";
            configStatus += (ckbZdOE.Checked ? 1 : 0) + "^";
            configStatus += (ckbAutoCreateGame.Checked ? 1 : 0) + "^";

            //Zp
            configStatus += (ckbZpTomorrow.Checked ? 1 : 0) + "^";
            configStatus += (ckbZp.Checked ? 1 : 0) + "^";

            //Dj
            configStatus += (ckbDj.Checked ? 1 : 0) + "^";

            //Xb
            configStatus += (ckbXb.Checked ? 1 : 0) + "^";

            //Ac
            configStatus += (ckbAc.Checked ? 1 : 0) + "^";

            //設置
            configStatus += (ckbIsProxy.Checked ? 1 : 0) + "^";
            configStatus += (ckbSbDjSame.Checked ? 1 : 0) + "^";
            configStatus += (ckbClearZero.Checked ? 1 : 0) + "^";
            configStatus += (ckbIsDsOnlyPk.Checked ? 1 : 0) + "^";
            configStatus += (ckbIsZdOnlyPk.Checked ? 1 : 0) + "^";
            configStatus += (ckbIsTrackOlympics.Checked ? 1 : 0) + "^";
            configStatus += (ckbOTConvertML.Checked ? 1 : 0) + "^";
            configStatus += (ckbNBAConvertML.Checked ? 1 : 0) + "^";
            #endregion

            #region TextBox

            //Ds
            configStatus += txbDsSec.Text + "^";
            configStatus += txbDsOERatio.Text + "^";
            configStatus += txbDsRf.Text + "^";
            configStatus += txbDsOu.Text + "^";
            configStatus += txbDsOe.Text + "^";
            configStatus += txbDsDy.Text + "^";

            //Zd
            configStatus += txbZdSec.Text + "^";
            configStatus += txbZdOERatio.Text + "^";
            configStatus += txbZdRf.Text + "^";
            configStatus += txbZdOu.Text + "^";
            configStatus += txbZdOe.Text + "^";
            configStatus += txbZdDy.Text + "^";

            //Zp
            configStatus += txbZpSec.Text + "^";

            //PIN設置
            configStatus += txbDjSec.Text + "^";
            configStatus += txbXbSec.Text + "^";
            configStatus += txbAcSec.Text + "^";

            //365設置
            configStatus += txbHtRf.Text + "^";
            configStatus += txbHtOu.Text + "^";
            configStatus += txbHtOe.Text + "^";
            configStatus += txbHtDy.Text + "^";
            configStatus += txbXbRf.Text + "^";
            configStatus += txbXbOu.Text + "^";
            configStatus += txbXbOe.Text + "^";
            configStatus += txbXbDy.Text + "^";
            configStatus += txbDjRf.Text + "^";
            configStatus += txbDjOu.Text + "^";
            configStatus += txbDjOe.Text + "^";
            configStatus += txbDjDy.Text + "^";

            #endregion

            #region ComboBox

            configStatus += cbbSameCount.SelectedIndex + "^";

            #endregion

            SaveFileConfig(configStatus);
        }

        //取得檔案的參數設置
        private string GetFileConfig()
        {
            string configStatus = "";
            FileInfo fileInfo = new FileInfo(_filePath);

            //檔案是否存在
            if (fileInfo.Exists)
            {
                FileStream stream = fileInfo.OpenRead();
                StreamReader reader = new StreamReader(stream);
                string mark = $"{_name},";

                while (!reader.EndOfStream)
                {
                    string data = reader.ReadLine();

                    if (data.StartsWith(mark))
                    {
                        configStatus = data.Replace(mark, "");
                        break;
                    }
                }

                stream.Close();
                reader.Close();
            }

            return configStatus;
        }

        //保存參數設置
        private void SaveFileConfig(string configStatus)
        {
            FileInfo fileInfo = new FileInfo(_filePath);
            StreamWriter writer;
            string mark = $"{_name},";

            //判斷檔案是否存在
            if (!fileInfo.Exists)
            {
                fileInfo.Directory.Create();
                FileStream stream;
                stream = fileInfo.Create();
                writer = new StreamWriter(stream);
                writer.WriteLine(mark + configStatus);
                writer.Close();
                stream.Close();
            }
            else
            {
                FileStream stream = fileInfo.OpenRead();
                StreamReader reader = new StreamReader(stream);
                string content = string.Empty;
                bool isSaved = false;

                while (!reader.EndOfStream)
                {
                    string data = reader.ReadLine();

                    if (data.StartsWith(mark))
                    {
                        data = mark + configStatus;
                        isSaved = true;
                    }

                    content += data + Environment.NewLine;
                }

                if (!isSaved)
                {
                    content += mark + configStatus + Environment.NewLine;
                }

                stream.Close();
                reader.Close();

                File.WriteAllText(_filePath, content);
            }
        }

        //鎖定/解鎖
        private void BtnLock_Click(object sender, EventArgs e)
        {
            switch (btnLock.Tag)
            {
                case "0":
                    SetLockStatus(true);
                    break;
                case "1":
                    SetLockStatus(false);
                    break;
            }
        }

        //設置 鎖定/解鎖狀態
        private void SetLockStatus(bool value)
        {
            btnLock.Tag = value ? "1" : "0";
            BtnLock_TextChanged(null, null);
            txbDsRf.Enabled = value;
            txbDsOu.Enabled = value;
            txbDsOe.Enabled = value;
            txbDsDy.Enabled = value;

            txbZdRf.Enabled = value;
            txbZdOu.Enabled = value;
            txbZdOe.Enabled = value;
            txbZdDy.Enabled = value;

            txbHtRf.Enabled = value;
            txbHtOu.Enabled = value;
            txbHtOe.Enabled = value;
            txbHtDy.Enabled = value;

            txbXbRf.Enabled = value;
            txbXbOu.Enabled = value;
            txbXbOe.Enabled = value;
            txbXbDy.Enabled = value;

            txbDjRf.Enabled = value;
            txbDjOu.Enabled = value;
            txbDjOe.Enabled = value;
            txbDjDy.Enabled = value;

            if (Config.gpType != GpType.Ds)
            {
                ckbClearZero.Enabled = value;
            }
        }

        //語系切換事件
        private void CbbLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            YwCore.Properties.Resources.Culture = Properties.Resources.Culture = Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(cbbLanguage.SelectedValue.ToString());

            foreach (Form form in Application.OpenForms)
            {
                ChangeUI.ChangeUILanguage(form);
            }

            string lang = Common.langDic.FirstOrDefault(x => x.Value == cbbLanguage.SelectedValue.ToString()).Key;
            Config.ChooseSet[3] = lang;
        }

        //鎖定按鈕文字異動事件
        private void BtnLock_TextChanged(object sender, EventArgs e)
        {
            btnLock.TextChanged -= BtnLock_TextChanged;

            switch (btnLock.Tag)
            {
                case "0":
                    btnLock.Text = Properties.Resources.SettingUnlock;
                    break;
                case "1":
                    btnLock.Text = Properties.Resources.SettingLock;
                    break;
            }

            btnLock.TextChanged += BtnLock_TextChanged;
        }

        //視窗名稱變動事件
        private void FrmConfig_TextChanged(object sender, EventArgs e)
        {
            this.TextChanged -= FrmConfig_TextChanged;
            this.Text += $"({YwCore.Common.GetApplicationName()})";
            this.TextChanged += FrmConfig_TextChanged;
        }

        /// <summary>
        /// 設定是否跟奧運開關
        /// </summary>
        /// <param name="enabled">控制項是否能使用</param>
        public void SetControlEnabled(bool enabled = true)
        {
            ckbIsTrackOlympics.Enabled = enabled;
        }

        /// <summary>
        /// 設定是否可開啟轉換正盤
        /// </summary>
        public void SetConvertMLEnabled()
        {
            if (ckbIsZdOnlyPk.Checked)
            {
                ckbOTConvertML.Enabled = true;
                ckbNBAConvertML.Enabled = true;
            }
            else
            {
                ckbOTConvertML.Checked = false;
                ckbNBAConvertML.Checked = false;
                ckbOTConvertML.Enabled = false;
                ckbNBAConvertML.Enabled = false;
            }
        }
    }
}
