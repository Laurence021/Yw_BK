#region 功能修改描述與時間

/*
    描述：XB來源內容格式
    日期：2019-05-16

    描述：修正來源細節比分的key
    日期：2023-02-02
*/

#endregion

using System.Collections.Generic;
using System.Linq;

namespace Yw_BK.source
{
    //// <summary>
    /// XB來源JSON格式
    /// </summary>
    public class XBDataFormat
    {
        /// <summary>
        /// PL陣列
        /// </summary>
        public List<Es> E { set; get; }
        /// <summary>
        /// GameId 
        /// </summary>
        public decimal I { set; get; }
        /// <summary>
        /// League
        /// </summary>
        public string L { set; get; }
        /// <summary>
        /// League(英文語系)
        /// </summary>
        public string LE { set; get; }
        /// <summary>
        /// LeagueId
        /// </summary>
        public string LI { set; get; }
        /// <summary>
        /// 主隊名稱
        /// </summary>
        public string O1 { set; get; }
        /// <summary>
        /// 主隊ID
        /// </summary>
        public int O1I { set; get; }
        /// <summary>
        /// 客隊名稱
        /// </summary>
        public string O2 { set; get; }
        /// <summary>
        /// 客隊ID
        /// </summary>
        public int O2I { set; get; }
        /// <summary>
        /// 比賽倒數時間
        /// </summary>
        public int B { set; get; }
        /// <summary>
        /// 全場比賽開始時間 比賽時間 = S + 1970/01/01 8:00:00
        /// </summary>
        public int S { set; get; }
        /// <summary>
        /// 加時賽、半場、節陣列
        /// </summary>
        public SGs[] SG { set; get; }
        /// <summary>
        /// 比分陣列
        /// </summary>
        public SCs SC { set; get; }
        /// <summary>
        /// 加時賽的主賽事ID
        /// </summary>
        public int? MG { set; get; }
        /// <summary>
        /// 加時賽
        /// </summary>
        public string V { set; get; }
        /// <summary>
        /// 國家
        /// </summary>
        public string CN { set; get; }
        /// <summary>
        /// 國家(英文)
        /// </summary>
        public string CE { set; get; }
        /// <summary>
        /// PL陣列
        /// </summary>
        public GEs[] GE { set; get; }
        /// <summary>
        /// 內頁玩法數量
        /// </summary>
        public int EC { set; get; }
        /// <summary>
        /// 比分版顯示訊息
        /// </summary>
        public MIS[] MIS { set; get; }
        /// <summary>
        /// 半場[11、12]、節[1、2、3、4] 
        /// </summary>
        public int? P { set; get; }
        /// <summary>
        /// 其他資訊(三分地带进球得分....等)
        /// </summary>
        public string TG { set; get; }
    }

    /// <summary>
    /// PL陣列
    /// </summary>
    public class Es
    {
        /// <summary>
        /// 是否鎖定(true:鎖定 / 沒有顯示:不鎖定)
        /// </summary>
        public bool B { set; get; }
        /// <summary>
        /// 最接中間盤
        /// </summary>
        public decimal CE { set; get; }
        /// <summary>
        /// PL變數
        /// </summary>
        public decimal CV { set; get; }
        /// <summary>
        /// PK變數
        /// </summary>
        public decimal P { set; get; }
        /// <summary>
        /// 位置
        /// 1:主1*2 2:客1*2 7:主RF 8:客RF 9:OU_O 10:OU_U 11:主總分大 12:主總分小 13:客總分大 14:客總分小 182:OE_E 183:OE_O 401:主1*2(加時賽) 402:客1*2(加時賽)
        /// </summary>
        public int T { set; get; }
    }

    /// <summary>
    /// PL陣列
    /// </summary>
    public class GEs
    {
        /// <summary>
        /// PL陣列
        /// </summary>
        public Es[][] E { set; get; }
    }

    /// <summary>
    /// 加時賽、半場、節陣列
    /// </summary>
    public class SGs
    {
        /// <summary>
        /// DS內頁節次ID
        /// </summary>
        public decimal CI { set; get; }
        /// <summary>
        /// 半場[11、12]、節[1、2、3、4] 
        /// </summary>
        public int? P { set; get; }
        /// <summary>
        /// 加時賽
        /// </summary>
        public string V { set; get; }
        /// <summary>
        /// 特殊玩法名稱
        /// </summary>
        public string TG { set; get; }
        /// <summary>
        /// PL陣列
        /// </summary>
        public GEs[] GE { set; get; }
    }

    /// <summary>
    /// 比分陣列
    /// </summary>
    public class SCs
    {
        /// <summary>
        /// CurrPeriod  1:"节 1", 2:"节 2", 3:"节 3", 4:"节 4", 5:"1 加时赛", 6:"2 加时赛", 11:"半场 1", 12:"半场 2"
        /// </summary>
        public int CP { set; get; }
        /// <summary>
        /// CurrPeriodStr 比賽節次
        /// </summary>
        public string CPS { set; get; }
        /// <summary>
        /// FullScore 比分 S1-主隊 S2-客隊 
        /// </summary>
        public FSs FS { set; get; }
        /// <summary>
        /// PeriodScores 各節比分 S1-主隊 S2-客隊 
        /// </summary>
        public List<KeyValuePair<int, PSs>> PS
        {
            set
            {
                _PS = value;

                int[] indexArray = new int[] { 1, 2, 3, 4, 11, 12 };

                foreach (int index in indexArray)
                {
                    if (_PS.Any(a => a.Key == index) == false)
                    {
                        _PS.Add(new KeyValuePair<int, PSs>(index, new PSs { S1 = 0, S2 = 0 }));
                    }
                }
            }
            get { return _PS; }
        }

        private List<KeyValuePair<int, PSs>> _PS;
        /// <summary>
        /// Info 判斷是否為賽前Pre-game betting
        /// </summary>
        public string I { set; get; }
        /// <summary>
        /// TimeDirection  -1:倒數  0:上數
        /// </summary>
        public int TD { get; set; }
        /// <summary>
        /// TimeRun
        /// </summary>
        public int TR { get; set; }
        /// <summary>
        /// TimeSec 比賽時間(秒數)
        /// </summary>
        public int TS { set; get; }
    }

    /// <summary>
    /// 比分 S1-主隊 S2-客隊 
    /// </summary>
    public class FSs
    {
        /// <summary>
        /// 主隊比分
        /// </summary>
        public int S1 { set; get; }
        /// <summary>
        /// 客隊比分
        /// </summary>
        public int S2 { set; get; }
    }

    /// <summary>
    /// 比分 S1-主隊 S2-客隊 
    /// </summary>
    public class PSs
    {
        /// <summary>
        /// 主隊比分
        /// </summary>
        public int S1 { set; get; }
        /// <summary>
        /// 客隊比分
        /// </summary>
        public int S2 { set; get; }
    }

    /// <summary>
    /// 比分版顯示訊息
    /// </summary>
    public class MIS
    {
        /// <summary>
        /// 顯示訊息位置編號(暫無使用)
        /// </summary>
        public int K { set; get; }
        /// <summary>
        /// 顯示訊息
        /// </summary>
        public string V { set; get; }
    }
}
