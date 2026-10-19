#region 功能修改描述與時間

/*
    描述：選擇平台視窗
    日期：2020-06-30

    描述：HG來源添加入口設置
    日期：2021-06-10
*/

#endregion

using System;
using System.Windows.Forms;
using YwCore;

namespace Yw_BK
{
    public partial class FrmEntrance : Form
    {
        //顯示畫面
        private static FrmEntrance _msgBox;
        //選擇結果
        private static int _result;

        /// <summary>
        /// 建構子
        /// </summary>
        public FrmEntrance()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 顯示視窗
        /// </summary>
        /// <param name="source">來源</param>
        /// <param name="frmName">視窗前綴名稱</param>>
        /// <returns>點擊的結果</returns>
        public static int Show(Source source, string frmName)
        {
            _msgBox = new FrmEntrance();
            _msgBox.Text = frmName;

            switch (source)
            {
                case Source.SB:
                    _msgBox.btnS1.Text = "CU";
                    _msgBox.btnS2.Text = "Dafa";
                    _msgBox.btnS3.Text = "Fun88";
                    _msgBox.btnS4.Text = "免登入版";
                    _msgBox.btnS3.Enabled = false;
                    _msgBox.btnS5.Visible = false;
                    break;
                case Source.PIN:
                    //_msgBox.btnS1.Text = "PIN";
                   // _msgBox.btnS2.Text = "LEO";
                    _msgBox.btnS3.Text = "PIN(Asia)";
                    _msgBox.btnS1.Visible = false;  //移除PIN(PIN)來源
                    _msgBox.btnS2.Visible = false;  //隱藏PIN(LEO)來源
                    _msgBox.btnS4.Visible = false;
                    _msgBox.btnS5.Visible = false;
                    break;
                case Source.XB:
                    _msgBox.btnS1.Text = "XB";
                    _msgBox.btnS2.Text = "XB(New)";
                    _msgBox.btnS3.Visible = false;
                    _msgBox.btnS4.Visible = false;
                    _msgBox.btnS4.Visible = false;
                    _msgBox.btnS5.Visible = false;
                    break;
                default:
                    return 0;
            }

            _msgBox.ShowDialog();
            return _result;
        }

        //按鈕觸發事件
        private void Button_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            _result = Convert.ToInt32(btn.Tag);
            _msgBox.Close();
        }
    }
}
