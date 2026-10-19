#region 功能修改描述與時間

/*
    描述：PIN內容格式
    建立時間：2019-01-09
*/

#endregion

using System;

namespace Yw_BK.source
{
    /// <summary>
    /// PIN來源JSON格式
    /// </summary>
    class PINDataFormat
    {
        /// <summary>
        /// Jason格式
        /// </summary>
        public class MatchDetail
        {
            public string DisplayName { get; set; }
            public string PeriodDesc { get; set; }
            public Gameline[] GameLines { get; set; }
        }

        /// <summary>
        /// 賽事
        /// </summary>
        public class Gameline
        {
            /// <summary>
            /// 賽事編號
            /// </summary>
            public decimal EvId { get; set; }
            /// <summary>
            /// 賽事編號尾數 (鴛鴦盤判斷)
            /// </summary>
            public decimal Lvl { get; set; }
            /// <summary>
            /// 是否有Zd
            /// </summary>
            public string DispDate { get; set; }
            /// <summary>
            /// 時間
            /// </summary>
            public DateTime Date { get; set; }
            /// <summary>
            /// 主隊
            /// </summary>
            public TmH TmH { get; set; }
            /// <summary>
            /// 客隊
            /// </summary>
            public TmA TmA { get; set; }
            /// <summary>
            /// 1*2 1、2，5、6
            /// </summary>
            public string[] WagerMap { get; set; }
            /// <summary>
            /// RF
            /// </summary>
            public string[] SpWagerMap { get; set; }
            /// <summary>
            /// OU
            /// </summary>
            public string[] TotWagerMap { get; set; }
        }

        /// <summary>
        /// 主隊
        /// </summary>
        public class TmH
        {
            public string Txt { get; set; }
            public string Pd { get; set; }
        }

        /// <summary>
        /// 客隊
        /// </summary>
        public class TmA
        {
            public string Txt { get; set; }
            public string Pd { get; set; }
        }
    }
}