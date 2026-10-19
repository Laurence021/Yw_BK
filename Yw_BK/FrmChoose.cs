#region 功能修改描述與時間

/*
    描述：選擇來源視窗
    建立時間：2019-01-07
*/

#endregion

using System.Text.RegularExpressions;
using YwCore;

namespace Yw_BK
{
    public partial class FrmChoose : Form
    {
        //顯示畫面
        private static FrmChoose _msgBox;
        //選擇結果
        private static int _result;
        //選擇結果
        private static string _resultName;

        /// <summary>
        /// 建構子
        /// </summary>
        public FrmChoose()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 顯示視窗
        /// </summary>
        /// <param name="frmText">視窗名稱</param>
        /// <returns>點擊的結果</returns>
        public static int Show(string frmText)
        {
            _msgBox = new FrmChoose();
            _msgBox.Text = frmText;
            _msgBox.ShowDialog();
            return _result;
        }

        /// <summary>
        /// 顯示視窗
        /// </summary>
        /// <param name="frmText">視窗名稱</param>
        /// <returns>點擊的結果</returns>
        public static Source Show(string frmText, Source source)
        {
            string strSource = source == Source.BET365 ? "365ZD" : source.GetDescriptionText();

            _msgBox = new FrmChoose();
            _msgBox.Text = frmText;

            Regex exclude = new Regex(@"\(.*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            foreach (Control ctl in _msgBox.Controls)
            {
                if (ctl.Name == "gbSelectSource")
                {
                    foreach (Control gbCtl in ctl.Controls)
                    {
                        if (gbCtl is Button btnCtl)
                        {
                            string text = btnCtl.Text;
                            Match excludeMatch = exclude.Match(text);

                            if (excludeMatch.Success)
                            {
                                text = text.Replace(excludeMatch.Value, "").Trim();
                            }

                            if (text == strSource)
                            {
                                btnCtl.Text = "All";
                                btnCtl.BackColor = Color.IndianRed;
                            }
                        }
                    }
                }
            }

            _msgBox.ShowDialog();

            if (_resultName == "365ZD") { _resultName = "BET365"; }
            Match resultMatch = exclude.Match(_resultName);

            if (resultMatch.Success)
            {
                _resultName = _resultName.Replace(resultMatch.Value, "").Trim();
            }

            Source resultSource = Extension.ParseByDescription<Source>(_resultName);

            return resultSource;
        }

        //按鈕觸發事件
        private void Button_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            _result = Convert.ToInt32(btn.Tag);
            _resultName = btn.Text;
            _msgBox.Close();
        }
    }
}
