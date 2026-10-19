#region 功能修改描述與時間

/*
    描述：MyStake來源內容格式
    日期：2022-09-23
*/

#endregion

using System;

namespace Yw_BK.source
{
    /// <summary>
    /// MyStake來源JSON格式
    /// </summary>
    public class MyStakeDataFormat
    {
        /// <summary>
        /// 賽事資訊
        /// </summary>
        public Game[] Games { get; set; }
        /// <summary>
        /// 國家資訊
        /// </summary>
        public Region[] Regions { get; set; }
        /// <summary>
        /// 聯盟資訊
        /// </summary>
        public Championat[] Championats { get; set; }
        /// <summary>
        /// 隊伍資訊
        /// </summary>
        public Team[] Teams { get; set; }
    }

    public class Game
    {
        /// <summary>
        /// 內頁請求賽事ID
        /// </summary>
        public long ID { get; set; }
        /// <summary>
        /// 球類ID
        /// </summary>
        public int Sport { get; set; }
        /// <summary>
        /// 國家ID
        /// </summary>
        public int Region { get; set; }
        /// <summary>
        /// 聯盟ID
        /// </summary>
        public int Champ { get; set; }

        private DateTime _startTime;
        /// <summary>
        /// 開賽時間
        /// </summary>
        public DateTime StartTime { get => _startTime; set => _startTime = value.AddHours(8); }
        /// <summary>
        /// 隊伍A ID
        /// </summary>
        public int Team1 { get; set; }
        /// <summary>
        /// 隊伍B ID
        /// </summary>
        public int Team2 { get; set; }
        /// <summary>
        /// 赛事狀態(未開賽：1、暫停：24、1Q：15、2Q：16...類推)
        /// </summary>
        public int MatchStatusID { get; set; }
        /// <summary>
        /// 進行時間(有給賽制資訊[Plng]會跌加前幾節時間，未給則為本節進行時間)
        /// </summary>
        public string MatchTime { get; set; }
        /// <summary>
        /// 現在比分
        /// </summary>
        public string Score { get; set; }
        public bool LiveBetStatus { get; set; }
        //賽制資訊
        public string Plng { get; set; }
    }

    public class Region
    {
        public long ID { get; set; }
        public string Name { get; set; }
        public int SportID { get; set; }
    }

    public class Championat
    {
        public long ID { get; set; }
        public string Name { get; set; }
        public int RegionID { get; set; }
    }

    public class Team
    {
        public long ID { get; set; }
        public string Name { get; set; }
    }



    public class MyStakeInnerDataFormat
    {
        public Gmk[] Gmk { get; set; }
        public mk[] mk { get; set; }
        public MyStakeMatch Match { get; set; }
    }

    public class Gmk
    {
        public long Id { get; set; }
        /// <summary>
        /// 玩法ID
        /// </summary>
        public int Mid { get; set; }
        /// <summary>
        /// PL
        /// </summary>
        public decimal V { get; set; }
        /// <summary>
        /// 1:A隊 2:B隊
        /// </summary>
        public int Posn { get; set; }
        /// <summary>
        /// PK
        /// </summary>
        public decimal H { get; set; }
        /// <summary>
        /// 是否顯示
        /// </summary>
        public bool Visible { get; set; }
        /// <summary>
        /// RT
        /// </summary>
        public bool Hisminus { get; set; }
        /// <summary>
        /// 第N節
        /// </summary>
        public int P1 { get; set; }
        /// <summary>
        /// 非來源屬性，有些聯盟會給Hisminus當RT，有些直接在H給正負
        /// 所以統一在此變數轉
        /// </summary>
        public decimal Pk { get => Hisminus ? -H : H; }
    }

    public class MyStakeMatch
    {
        /// <summary>
        /// 狀態為True 時間倒數，null 時間正數
        /// </summary>
        public string Active { get; set; }
        /// <summary>
        /// 赛事狀態(未開賽：1、暫停：24、1Q：15、2Q：16...類推)
        /// </summary>
        public int EventStatus { get; set; }
        /// <summary>
        /// 進行時間(Active狀態為True則該欄位會是剩餘時間)
        /// </summary>
        public string MatchTimeExtended { get; set; }
        /// <summary>
        /// 剩餘時間
        /// </summary>
        public string RemainingTime { get; set; }
        /// <summary>
        /// 總分
        /// </summary>
        public string Score { get; set; }
        /// <summary>
        /// 細節比分
        /// </summary>
        public string SetScores { get; set; }
        /// <summary>
        /// 賽制資訊
        /// </summary>
        public string Plng { get; set; }
        /// <summary>
        /// 體育項目
        /// </summary>
        public int Sport { get; set; }
    }

    public class mk
    {
        public int Sport { get; set; }
    }
}

