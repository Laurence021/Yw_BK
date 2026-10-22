#region 功能修改描述與時間

/*
    描述：提醒設置頁面
    日期：2019-10-29

    描述：調整對應參數設置選勾判斷處理 (搭配YwCore[1.0.3.66])
    日期：2021-12-09

    描述：提示訊息添加平台資訊
    日期：2022-03-18

    描述：移除MAR來源
    日期：2022-07-06

    描述：添加ZD脫勾關小格選勾 (搭配YwCore[1.0.5.89])
    日期：2023-06-20

    描述：MT訊息變色新需求調整(配合YwCore[1.1.7.32])
    日期：2024-11-29
*/

#endregion

using System.Text.RegularExpressions;
using Yw_BK.model;
using YwCore;

namespace Yw_BK
{
    public partial class FrmRemind : Form
    {
        //對應開頭名稱
        private string _name;

        /// <summary>
        /// 建構子
        /// </summary>
        public FrmRemind()
        {
            InitializeComponent();

            //設定AP名稱
            _name = YwCore.Common.GetApplicationName();
            //設定參數
            GetConfiguration();
            SetConfiguration();
            //主動觸發事件變動
            this.OnTextChanged(null);
        }

        //Load事件
        private void FrmRemind_Load(object sender, EventArgs e)
        {
            btnSaveConfig.Enabled = false;
            SetDefaultText();
        }

        //關閉Form事件
        private void FrmRemind_FormClosing(object sender, FormClosingEventArgs e)
        {
            LogFile.SetLog(LogMode.Control, "提醒設置 狀態:關閉");
        }

        //視窗名稱變動事件
        private void FrmRemind_TextChanged(object sender, EventArgs e)
        {
            this.TextChanged -= FrmRemind_TextChanged;
            this.Text += $"({_name})";
            this.TextChanged += FrmRemind_TextChanged;
        }

        //CheckBox變化事件
        private void CkbCheckedChanged(object sender, EventArgs e)
        {
            btnSaveConfig.Enabled = true;

            if (sender is CheckBox checkBox && checkBox.Parent is GroupBox gbCtls)
            {
                if (gbCtls == gbMain)
                {
                    //是否需要被勾選
                    bool isAllCheck = true;

                    foreach (Control ctl in gbCtls.Controls)
                    {
                        if (ctl is CheckBox cbCtl && cbCtl != ckbLinked && cbCtl.Enabled)
                        {
                            if (cbCtl.Checked)
                            {
                                isAllCheck &= true;
                            }
                            else
                            {
                                isAllCheck &= false;
                                break;
                            }
                        }
                    }

                    ckbLinked.CheckedChanged -= CkbLinked_CheckedChanged;
                    ckbLinked.Checked = isAllCheck;
                    ckbLinked.CheckedChanged += CkbLinked_CheckedChanged;
                }
            }
        }

        //TextBox變化事件
        private void TxbTextChanged(object sender, EventArgs e)
        {
            TextBox textBox = ((TextBox)sender);

            if (textBox.Name == txbPort.Name)
            {
                //PORT只能輸入數字
                textBox.Text = Regex.Replace(textBox.Text, "[^0-9]", "");
            }
            else if (textBox.Name == txbIP.Name)
            {
                //IP不用空白字元
                textBox.Text = Regex.Replace(textBox.Text, "\\s", "");
            }
            else
            {
                textBox.Text = Regex.Replace(textBox.Text, "[^0-9.]", "");
            }

            btnSaveConfig.Enabled = true;
        }

        //連動事件
        private void CkbLinked_CheckedChanged(object sender, EventArgs e)
        {
            bool check = ckbLinked.Checked;

            foreach (Control ctl in gbMain.Controls)
            {
                if (ctl is CheckBox && ctl != sender)
                {
                    CheckBox ckb = ctl as CheckBox;
                    ckb.Checked = ckb.Enabled && ckb.Visible ? check : false;
                }
            }

            btnSaveConfig.Enabled = true;
        }

        //設置保存
        private void BtnSaveConfig_Click(object sender, EventArgs e)
        {
            LogFile.SetLog(LogMode.Control, "提醒設置版面 - 設置保存 狀態:點擊");

            //預設超時秒數
            SetDefaultText();
            //設定數值至Configuration
            SetConfiguration();
            //紀錄狀態至Settings
            SetStatusToSettings();

            LogFile.SetLog(LogMode.Control, "提醒設置版面 - 設置保存 狀態:設置成功 " + LogFile.GetText(this));

            btnSaveConfig.Enabled = false;
        }

        //設置TextBox預設值
        private void SetDefaultText()
        {
            #region 預設超時秒數

            if (string.IsNullOrEmpty(txbDs.Text)) txbDs.Text = "60";

            if (string.IsNullOrEmpty(txbZd.Text)) txbZd.Text = "60";

            if (string.IsNullOrEmpty(txbZp.Text)) txbZp.Text = "60";

            if (string.IsNullOrEmpty(txbDj.Text)) txbDj.Text = "60";

            if (string.IsNullOrEmpty(txbXb.Text)) txbXb.Text = "60";

            if (string.IsNullOrEmpty(txbZdClose.Text)) txbZdClose.Text = "60";

            if (string.IsNullOrEmpty(txbDsTimeOut.Text)) txbDsTimeOut.Text = "60";

            if (string.IsNullOrEmpty(txbZdTimeOut.Text)) txbZdTimeOut.Text = "60";

            #endregion
        }

        // 取得控件狀態
        private void GetConfiguration()
        {
            string configStatus = string.Empty;

            #region 各來源遮蔽

            if (Config.source == Source.XB)
            {
                ckbDj.Enabled = false;
                labDj.Enabled = false;
                txbDj.Enabled = false;

                ckbXb.Enabled = false;
                labXb.Enabled = false;
                txbXb.Enabled = false;
            }
            else if (Config.source == Source.SB)
            {
                ckbDj.Enabled = false;
                labDj.Enabled = false;
                txbDj.Enabled = false;

                ckbXb.Enabled = false;
                labXb.Enabled = false;
                txbXb.Enabled = false;
            }
            else if (Config.source == Source.JZ)
            {
                ckbZp.Enabled = false;
                labZp.Enabled = false;
                txbZp.Enabled = false;

                ckbDj.Enabled = false;
                labDj.Enabled = false;
                txbDj.Enabled = false;

                ckbXb.Enabled = false;
                labXb.Enabled = false;
                txbXb.Enabled = false;

                ckbZdClose.Enabled = false;
                labZdClose.Enabled = false;
                txbZdClose.Enabled = false;
            }
            else if (Config.source == Source.BET365)
            {
                ckbZp.Enabled = false;
                labZp.Enabled = false;
                txbZp.Enabled = false;

                ckbDj.Enabled = false;
                labDj.Enabled = false;
                txbDj.Enabled = false;

                ckbXb.Enabled = false;
                labXb.Enabled = false;
                txbXb.Enabled = false;

                ckbAbnormal.Enabled = true;
                ckbAbnormal.Visible = true;

                ckbDsTimeOut.Enabled = false;
                txbDsTimeOut.Enabled = false;

                if (Config.gpType == GpType.Zd)
                {
                    ckbDs.Enabled = false;
                    labDs.Enabled = false;
                    txbDs.Enabled = false;
                }
                else
                {
                    ckbZd.Enabled = false;
                    labZd.Enabled = false;
                    txbZd.Enabled = false;
                    ckbZdClose.Enabled = false;
                    labZdClose.Enabled = false;
                    txbZdClose.Enabled = false;
                }
            }
            else if (Config.source == Source.SBO)
            {
                ckbZp.Enabled = false;
                labZp.Enabled = false;
                txbZp.Enabled = false;

                ckbDj.Enabled = false;
                labDj.Enabled = false;
                txbDj.Enabled = false;

                ckbXb.Enabled = false;
                labXb.Enabled = false;
                txbXb.Enabled = false;
            }
            else if (Config.source == Source.MyStake)
            {
                ckbDs.Enabled = false;
                labDs.Enabled = false;
                txbDs.Enabled = false;

                ckbZp.Enabled = false;
                labZp.Enabled = false;
                txbZp.Enabled = false;

                ckbDj.Enabled = false;
                labDj.Enabled = false;
                txbDj.Enabled = false;

                ckbXb.Enabled = false;
                labXb.Enabled = false;
                txbXb.Enabled = false;

                ckbDsTimeOut.Enabled = false;
                txbDsTimeOut.Enabled = false;
            }
            else if (Config.source == Source.PIN && (Config.entrance == Entrance.LEO || Config.entrance == Entrance.Asia))
            {
                ckbDj.Enabled = false;
                labDj.Enabled = false;
                txbDj.Enabled = false;

                ckbXb.Enabled = false;
                labXb.Enabled = false;
                txbXb.Enabled = false;
            }
            else if (Config.source == Source.Radar)
            {
                ckbZp.Enabled = false;
                labZp.Enabled = false;
                txbZp.Enabled = false;

                ckbDj.Enabled = false;
                labDj.Enabled = false;
                txbDj.Enabled = false;

                ckbXb.Enabled = false;
                labXb.Enabled = false;
                txbXb.Enabled = false;

                ckbAbnormal.Enabled = true;
                ckbAbnormal.Visible = true;
            }

            #endregion

            //讀取設定
            configStatus = GetConfigStatus();

            if (!string.IsNullOrEmpty(configStatus))
            {
                //解析Settings紀錄狀態
                string[] status = configStatus.Split('^');

                try
                {
                    if (status.Length != 21)
                    {
                        return;
                    }

                    #region CheckBox狀態

                    ckbDs.Checked = status[0] == "1" && ckbDs.Enabled ? true : false;
                    ckbZd.Checked = status[1] == "1" && ckbZd.Enabled ? true : false;
                    ckbZp.Checked = status[2] == "1" && ckbZp.Enabled ? true : false;
                    ckbDj.Checked = status[3] == "1" && ckbDj.Enabled ? true : false;
                    ckbXb.Checked = status[4] == "1" && ckbXb.Enabled ? true : false;
                    ckbAbnormal.Checked = status[5] == "1" && ckbAbnormal.Enabled ? true : false;
                    ckbZdClose.Checked = status[6] == "1" && ckbZdClose.Enabled ? true : false;
                    ckbDsTimeOut.Checked = status[7] == "1" && ckbDsTimeOut.Enabled ? true : false;
                    ckbZdTimeOut.Checked = status[8] == "1" && ckbZdTimeOut.Enabled ? true : false;
                    ckbSendMsg.Checked = status[9] == "1" && ckbSendMsg.Enabled ? true : false;

                    #endregion

                    #region TextBox狀態

                    txbDs.Text = string.IsNullOrEmpty(status[10]) ? "60" : status[10];
                    txbZd.Text = string.IsNullOrEmpty(status[11]) ? "60" : status[11];
                    txbZp.Text = string.IsNullOrEmpty(status[12]) ? "60" : status[12];
                    txbDj.Text = string.IsNullOrEmpty(status[13]) ? "60" : status[13];
                    txbXb.Text = string.IsNullOrEmpty(status[14]) ? "60" : status[14];
                    txbZdClose.Text = string.IsNullOrEmpty(status[15]) ? "60" : status[15];
                    txbDsTimeOut.Text = string.IsNullOrEmpty(status[16]) ? "60" : status[16];
                    txbZdTimeOut.Text = string.IsNullOrEmpty(status[17]) ? "60" : status[17];
                    txbIP.Text = status[18];
                    txbPort.Text = status[19];

                    #endregion

                }
                catch (Exception ex)
                {
                    LogFile.SetLog(LogMode.Error, ex.Message);
                }
            }
        }

        // 控件狀態設置
        private void SetConfiguration()
        {
            int port = 0;
            bool enabled = ckbSendMsg.Checked && !string.IsNullOrEmpty(txbIP.Text) && int.TryParse(txbPort.Text, out port);
            YwCore.Common.SendMessage.SetSetting(enabled, txbIP.Text, port);
            DateTime dateTime = DateTime.Now;

            int sec = ConvertTextToSec(txbZdTimeOut.Text, 60);
            string msg = $"{Config.gpName}籃球{GpType.Zd.ToString("g")}對庫超時";
            string key = "ZdUpdate";
            IRemind remindInfo = new ConditionalRemindInfo(key, $"{msg}未更新", $"{msg}已恢復", dateTime, sec, () => Config.isOpenZd && ckbZdTimeOut.Checked, () => Config.gameDic.Values.Any(a => a.GType == (byte)GType.BkZd && a.ObjDic.Values.Any(b => b.IsMain)));
            SetRemindTask(key, remindInfo);

            sec = ConvertTextToSec(txbDsTimeOut.Text, 60);
            msg = $"{Config.gpName}籃球{GpType.Ds.ToString("g")}對庫超時";
            key = "DsUpdate";
            remindInfo = new ConditionalRemindInfo(key, $"{msg}未更新", $"{msg}已恢復", dateTime, sec, () => (Config.isOpenDs || Config.isOpenZp) && ckbDsTimeOut.Checked, () => Config.gameDic.Values.Any(a => a.GType == (byte)GType.BkDs && a.ObjDic.Values.Any(b => b.IsMain)));
            SetRemindTask(key, remindInfo);

            sec = ConvertTextToSec(txbDs.Text, 60);
            msg = $"{Config.gpName}籃球{GpType.Ds.ToString("g")}超時{sec}秒";
            key = GpType.Ds.ToString("g");
            remindInfo = new ConditionalRemindInfo(key, $"{msg}未獲取", $"{msg}已恢復", dateTime, sec, () => SetEnabled(key, ckbDs.Checked), () => Config.gameDic.Values.Any(a => a.GType == (byte)GType.BkDs && a.ObjDic.Values.Any(b => b.IsMain)));
            SetRemindTask(key, remindInfo);

            sec = ConvertTextToSec(txbZdClose.Text, 60);
            msg = $"{Config.gpName}籃球{GpType.Zd.ToString("g")}超時{sec}秒";
            key = "ZdClose";
            remindInfo = new ConditionalRemindInfo(key, $"{msg}未獲取(數據脫勾關小格)", $"{msg}已恢復(數據脫勾關小格)", dateTime, sec, () => Config.isOpenZd && ckbZdClose.Checked, () => Config.gameDic.Values.Any(a => a.GType == (byte)GType.BkZd && a.ObjDic.Values.Any(b => b.IsMain)));
            SetRemindTask(key, remindInfo);

            sec = ConvertTextToSec(txbZd.Text, 60);
            msg = $"{Config.gpName}籃球{GpType.Zd.ToString("g")}超時{sec}秒";
            key = GpType.Zd.ToString("g");
            remindInfo = new ConditionalRemindInfo(key, $"{msg}未獲取", $"{msg}已恢復", dateTime, sec, () => SetEnabled(key, ckbZd.Checked), () => Config.gameDic.Values.Any(a => a.GType == (byte)GType.BkZd && a.ObjDic.Values.Any(b => b.IsMain)), remindInfo);
            SetRemindTask(key, remindInfo);

            sec = ConvertTextToSec(txbZp.Text, 60);
            msg = $"{Config.gpName}籃球{GpType.Zp.ToString("g")}超時{sec}秒";
            key = GpType.Zp.ToString("g");
            remindInfo = new ConditionalRemindInfo(key, $"{msg}未獲取", $"{msg}已恢復", dateTime, sec, () => SetEnabled(key, ckbZp.Checked), () => Config.gameDic.Values.Any(a => a.GType == (byte)GType.BkDs && a.ObjDic.Values.Any(b => b.IsMain)));
            SetRemindTask(key, remindInfo);

            sec = ConvertTextToSec(txbDj.Text, 60);
            msg = $"{Config.gpName}籃球{GpType.Dj.ToString("g")}超時{sec}秒";
            key = GpType.Dj.ToString("g");
            remindInfo = new ConditionalRemindInfo(key, $"{msg}未獲取", $"{msg}已恢復", dateTime, sec, () => SetEnabled(key, ckbDj.Checked), () => Config.gameDic.Values.Any(a => a.ObjDic.Values.Any(b => b.IsMain)));
            SetRemindTask(key, remindInfo);

            sec = ConvertTextToSec(txbXb.Text, 60);
            msg = $"{Config.gpName}籃球{GpType.Xb.ToString("g")}超時{sec}秒";
            key = GpType.Xb.ToString("g");
            remindInfo = new ConditionalRemindInfo(key, $"{msg}未獲取", $"{msg}已恢復", dateTime, sec, () => SetEnabled(key, ckbXb.Checked), () => Config.gameDic.Values.Any(a => a.ObjDic.Values.Any(b => b.IsMain)));
            SetRemindTask(key, remindInfo);

            Config.AbnormalHandleEnabled = ckbAbnormal.Checked;
        }

        //來源選擇控件狀態儲存
        private void SetStatusToSettings()
        {
            string configStatus = string.Empty;

            #region CheckBox狀態

            configStatus += (ckbDs.Checked ? "1" : "0") + "^";
            configStatus += (ckbZd.Checked ? "1" : "0") + "^";
            configStatus += (ckbZp.Checked ? "1" : "0") + "^";
            configStatus += (ckbDj.Checked ? "1" : "0") + "^";
            configStatus += (ckbXb.Checked ? "1" : "0") + "^";
            configStatus += (ckbAbnormal.Checked ? "1" : "0") + "^";
            configStatus += (ckbZdClose.Checked ? "1" : "0") + "^";

            configStatus += (ckbDsTimeOut.Checked ? "1" : "0") + "^";
            configStatus += (ckbZdTimeOut.Checked ? "1" : "0") + "^";
            configStatus += (ckbSendMsg.Checked ? "1" : "0") + "^";

            #endregion

            #region TextBox狀態

            configStatus += txbDs.Text + "^";
            configStatus += txbZd.Text + "^";
            configStatus += txbZp.Text + "^";
            configStatus += txbDj.Text + "^";
            configStatus += txbXb.Text + "^";
            configStatus += txbZdClose.Text + "^";

            configStatus += txbDsTimeOut.Text + "^";
            configStatus += txbZdTimeOut.Text + "^";
            configStatus += txbIP.Text + "^";
            configStatus += txbPort.Text + "^";

            #endregion

            //儲存狀態
            SaveConfigStatus(configStatus);
        }

        //設置文字轉數值
        private int ConvertTextToSec(string text, int defaultSec)
        {
            if (int.TryParse(text, out int result))
            {
                return result;
            }
            else
            {
                return defaultSec;
            }
        }

        /// <summary>
        /// 設置添加提醒任務
        /// </summary>
        /// <param name="taskName">任務名稱</param>
        /// <param name="remind">提醒物件</param>
        private void SetRemindTask(string taskName, IRemind remind)
        {
            Config.remindInfos.AddOrUpdate(taskName, remind, (k, v) =>
            {
                remind.SetFlag(v.GetSendFlag());
                remind.SetLastTime(v.GetLastTime());
                return remind;
            });
        }

        #region IO存取

        //儲存檔案
        private void SaveConfigStatus(string configStatus)
        {
            DirectoryInfo dir = new DirectoryInfo(Application.StartupPath).Parent.Parent.Parent;
            string configFile = dir.FullName + "\\Info\\RemindConfig.txt";
            FileInfo fileInfo = new FileInfo(configFile);
            StreamWriter writer;
            string mark = $"{_name},";

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
                File.WriteAllText(configFile, content);
            }
        }

        #endregion

        //取得紀錄數據
        private string GetConfigStatus()
        {
            DirectoryInfo dir = new DirectoryInfo(Application.StartupPath).Parent.Parent.Parent;
            string configFile = dir.FullName + "\\Info\\RemindConfig.txt";
            FileInfo fileInfo = new FileInfo(configFile);
            string configStatus = "";

            if (fileInfo.Exists)
            {
                FileStream stream = fileInfo.OpenRead();
                StreamReader reader = new StreamReader(stream);
                string content = string.Empty;
                string mark = $"{_name},";

                while (!reader.EndOfStream)
                {
                    string data = reader.ReadLine();

                    if (data.Contains(mark))
                    {
                        configStatus = data.Replace(mark, "");
                    }

                    content += data + Environment.NewLine;
                }

                stream.Close();
                reader.Close();
            }

            return configStatus;
        }

        /// <summary>
        /// 設定提醒開關狀態
        /// </summary>
        /// <param name="taskName">任務名稱</param>
        /// <param name="enable">控件開關</param>
        public bool SetEnabled(string taskName, bool enable)
        {
            //是否勾上
            bool isChecked = enable;

            #region 判斷對應參數設置開關是否勾上

            if (Enum.TryParse(taskName, out GpType gpType))
            {
                switch (gpType)
                {
                    case GpType.Ds:
                        isChecked = Config.isOpenDs && isChecked;
                        break;
                    case GpType.Zd:
                        isChecked = Config.isOpenZd && isChecked;
                        break;
                    case GpType.Zp:
                        isChecked = Config.isOpenZp && isChecked;
                        break;
                    case GpType.Dj:
                        isChecked = Config.isOpenDj && isChecked;
                        break;
                    case GpType.Xb:
                        isChecked = Config.isOpenXb && isChecked;
                        break;
                }
            }

            #endregion

            return isChecked;
        }

        /// <summary>
        /// 設定提醒開關狀態
        /// </summary>
        /// <param name="taskName">任務名稱</param>
        public int SetEnabled(string taskName)
        {
            //開關種類 1:一般超時提醒  2:超時未獲取進行關閉
            int enabledSwitch = 0;

            #region 判斷對應參數設置開關是否勾上

            if (Enum.TryParse(taskName, out GpType gpType))
            {
                switch (gpType)
                {
                    case GpType.Zd:

                        if (Config.isOpenZd)
                        {
                            if (ckbZd.Checked)
                            {
                                enabledSwitch |= RemindMode.Alert.GetHashCode();
                            }

                            if (ckbZdClose.Checked)
                            {
                                enabledSwitch |= RemindMode.Close.GetHashCode();
                            }
                        }

                        break;
                }
            }

            #endregion

            return enabledSwitch;
        }
    }
}
