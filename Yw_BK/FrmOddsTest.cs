#region 功能修改描述與時間

/*
    描述：PL模擬測試頁面
    建立時間：2020-01-21
*/

#endregion

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yw_BK.model;
using System.Text.RegularExpressions;
using YwCore;

namespace Yw_BK
{
    public partial class FrmOddsTest : Form
    {
        //欲修改的Kinfo
        private KInfo kInfo;
        //畫面顯示文字
        private string gameText;

        /// <summary>
        /// 建構子
        /// </summary>
        /// <param name="gameText"></param>
        /// <param name="kInfo"></param>
        public FrmOddsTest(string gameText, KInfo kInfo)
        {
            InitializeComponent();

            this.kInfo = kInfo;
            this.gameText = gameText;
        }

        /// <summary>
        /// 視窗載入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FrmOddsTest_Load(object sender, EventArgs e)
        {
            ckbTest.Checked = kInfo.IsTest;
            ckbOpen.Checked = kInfo.IsOpen;
            ckbOpen.Enabled = kInfo.IsTest;

            #region 視窗、預設控制項位置調整

            if (kInfo.BInfoDic.Count <= 5)
            {
                this.Size = new Size(500, 240);
                btnSave.Location = new Point(400, 170);
                ckbTest.Location = new Point(405, 145);
            }
            else if (kInfo.BInfoDic.Count <= 10)
            {
                this.Height = 240;
                btnSave.Location = new Point(btnSave.Location.X, 170);
                ckbTest.Location = new Point(btnSave.Location.X + 5, 145);
            }

            ckbOpen.Location = new Point(this.Width / 2 - ckbOpen.Width / 2, 170 + (btnSave.Height - ckbOpen.Height) / 2);

            #endregion

            //標題資訊
            Label lblGameInfo = new Label();
            lblGameInfo.Text = this.gameText;
            lblGameInfo.Width = this.Width;
            lblGameInfo.Height = lblGameInfo.PreferredHeight + 20;
            lblGameInfo.TextAlign = ContentAlignment.TopCenter;
            this.Controls.Add(lblGameInfo);

            int x = 0;
            int y = lblGameInfo.Bottom;

            #region PK相關控制項

            //僅RF、OU有PK值可設定
            if ((kInfo.PlayTyp & (1 | 2)) == kInfo.PlayTyp && !Config.IsErrorLine(1m, kInfo.KType))
            {
                Label lblPk = new Label();
                lblPk.Text = $"{YwCore.Properties.Resources.Line}:";
                lblPk.Location = new Point((this.Width - 2 * (50 + 10) - 10) / 2 - 10, y + 10);
                lblPk.Width = 50;
                lblPk.TextAlign = ContentAlignment.MiddleRight;
                this.Controls.Add(lblPk);

                if (kInfo.PlayTyp == 1)
                {
                    x = (this.Width - 3 * (50 + 10) - 10) / 2 - 10;
                    lblPk.Location = new Point(x, lblPk.Top);

                    //RT選單
                    ComboBox cbRt = new ComboBox();
                    cbRt.Name = "cbRt";
                    cbRt.Location = new Point(lblPk.Right + 10, lblPk.Top + Math.Abs(cbRt.Height - lblPk.Height) / 2);
                    cbRt.DropDownStyle = ComboBoxStyle.DropDownList;
                    cbRt.Width = 50;
                    Dictionary<int, string> dc = new Dictionary<int, string>();
                    dc.Add(1, "A -");
                    dc.Add(0, "B -");
                    cbRt.DataSource = new BindingSource(dc, null);
                    cbRt.ValueMember = "Key";
                    cbRt.DisplayMember = "Value";
                    cbRt.Enabled = kInfo.IsTest;
                    this.Controls.Add(cbRt);
                    cbRt.SelectedValue = kInfo.LineAt;
                    x = cbRt.Right;
                }
                else
                {
                    x = lblPk.Right;
                }

                //PK設定欄位
                TextBox tbPk = new TextBox();
                tbPk.Name = "tbPk";
                tbPk.Width = 50;
                tbPk.TextAlign = HorizontalAlignment.Right;
                tbPk.Location = new Point(x + 10, lblPk.Top + Math.Abs(tbPk.Height - lblPk.Height) / 2);
                tbPk.Enabled = kInfo.IsTest;
                tbPk.Text = kInfo.GetPkValue().ToString();
                tbPk.Validating += Number_Validating;
                this.Controls.Add(tbPk);

                y = tbPk.Bottom;
            }

            #endregion

            #region PL相關控制項

            var bInfoList = kInfo.BInfoDic.Values.OrderBy(a => a.BType).ToList();
            int spacingX = bInfoList.Count > 3 ? 15 : 20;

            if (bInfoList.Count > 10)
            {
                x = (this.Width - 10 * (60 + spacingX) - spacingX) / 2 - spacingX / 2;
            }
            else
            {
                x = (this.Width - bInfoList.Count * (60 + spacingX) - spacingX) / 2 - spacingX / 2;
            }

            for (int i = 1; i <= bInfoList.Count; i++)
            {
                BInfo bInfo = bInfoList[i - 1];
                Label lblPl = new Label();
                char[] c = bInfo.BType.ToString().Remove(0, bInfo.BType.ToString().Length - 2).ToCharArray();

                switch (kInfo.PlayTyp)
                {
                    case 1:
                        lblPl.Text = (c[1] == '1' ? "A" : "B");
                        break;
                    case 2:
                        lblPl.Text = (c[1] == '1' ? YwCore.Properties.Resources.Over : YwCore.Properties.Resources.Under);
                        break;
                    case 4:
                        lblPl.Text = (c[1] == '1' ? "A" : c[1] == '2' ? "B" : YwCore.Properties.Resources.Draw);
                        break;
                    case 8:
                        lblPl.Text = (c[1] == '1' ? YwCore.Properties.Resources.Odd : YwCore.Properties.Resources.Even);
                        break;
                    case 16:

                        if (c.ToString() == "99")
                        {
                            lblPl.Text = "Other";
                        }
                        else
                        {
                            lblPl.Text = string.Join(":", c);
                        }

                        break;
                    case 32:

                        if (c.ToString() == "79")
                        {
                            lblPl.Text = "7+";
                        }
                        else
                        {
                            lblPl.Text = string.Join("-", c);
                        }
                        break;
                    case 64:
                        lblPl.Text = string.Join("/", c).Replace("1", "A").Replace("2", "B").Replace("3", YwCore.Properties.Resources.Draw);
                        break;
                }

                lblPl.Location = new Point(x + spacingX, y);
                lblPl.Width = 60;
                lblPl.TextAlign = ContentAlignment.MiddleCenter;
                this.Controls.Add(lblPl);

                //PL設定欄位
                TextBox tbPl = new TextBox();
                tbPl.Location = new Point(lblPl.Left, lblPl.Bottom);
                tbPl.Width = 50;
                tbPl.Left += (lblPl.Width - tbPl.Width) / 2;
                tbPl.TextAlign = HorizontalAlignment.Right;
                tbPl.Name = "tbPl" + bInfoList[i - 1].BType;
                tbPl.Enabled = kInfo.IsTest;
                tbPl.Text = bInfo.Odds.ToString();
                tbPl.Validating += Number_Validating;
                this.Controls.Add(tbPl);
                x = lblPl.Right;

                if (i % 10 == 0)
                {
                    y = tbPl.Bottom + 5;
                    x = (this.Width - 10 * (60 + spacingX) - spacingX) / 2 - spacingX / 2;
                }
            }

            #endregion
        }

        //數字驗證
        private void Number_Validating(object sender, System.ComponentModel.CancelEventArgs e)
        {
            TextBox textBox = sender as TextBox;

            if (!Regex.IsMatch(textBox.Text, @"^(([0-9]+|0)\.([0-9]{1,3})$)|^([0-9]+|0)$"))
            {
                e.Cancel = true;
                textBox.SelectAll();
            }
        }

        //儲存
        private void BtnSave_Click(object sender, EventArgs e)
        {
            KInfo oldKinfo = kInfo.DeepClone();
            decimal line = kInfo.GetPkValue();
            int lineAt = kInfo.LineAt;
            kInfo.IsTest = ckbTest.Checked;

            if (ckbTest.Checked)
            {
                foreach (Control control in this.Controls)
                {
                    if (control.Name == "cbRt")
                    {
                        lineAt = (int)((ComboBox)control).SelectedValue;
                    }
                    else if (control.Name == "tbPk")
                    {
                        line = Convert.ToDecimal(((TextBox)control).Text);
                    }
                    else if (control.Name.Contains("tbPl"))
                    {
                        int bType = Convert.ToInt32(control.Name.Replace("tbPl", ""));
                        kInfo.BInfoDic[bType].SetOddsTest(Convert.ToDecimal(((TextBox)control).Text));
                    }
                    else if (control.Name.Contains("ckbOpen"))
                    {
                        kInfo.SetIsOpenTest(((CheckBox)control).Checked);
                    }
                }

                kInfo.SetLineTest(line, lineAt);
                kInfo.CompareTest(oldKinfo);
            }
            else
            {
                kInfo.IsUpdate = true;
            }

            //日誌記錄操作
            LogFile.SetLog(LogMode.Control, $"點擊儲存 : 測試" + (ckbTest.Checked ? "開啟" : "關閉"));

            this.Close();
        }

        //模擬測試勾選
        private void CkbTest_CheckedChanged(object sender, EventArgs e)
        {
            foreach (Control control in this.Controls)
            {
                if (control.Name.Contains("tb") || control.Name.Contains("cb") || control.Name == "ckbOpen")
                {
                    control.Enabled = ckbTest.Checked;
                }
            }
        }
    }
}
