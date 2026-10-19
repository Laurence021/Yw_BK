#region 功能修改描述與時間

/*
    描述：MyStake來源內容解析
    日期：2022-09-23

    描述：添加NBA解析
    日期：2023-02-24
                        
    描述：調整NBA隊伍主客位置(上客下主)
    日期：2023-03-16
                        
    描述：添加抓取三對三籃球ZD
    日期：2023-06-15

    描述：比分時間狀態資訊統一由內頁獲取
    日期：2023-06-27

    描述：調整剩餘時間判斷方式(避免單節未開打時計算錯誤)
    日期：2024-12-23
*/

#endregion

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Yw_BK.model;
using YwCore;

namespace Yw_BK.source
{
    /// <summary>
    /// MyStake解析
    /// </summary>
    public class ParserMyStake
    {
        /// <summary>
        /// 來源gId對應賽事字典key    例: key:11223344  value: gType League TeamA TeamB
        /// </summary>
        public static ConcurrentDictionary<GpType, ConcurrentDictionary<decimal, string>> dictMain = new ConcurrentDictionary<GpType, ConcurrentDictionary<decimal, string>>();
        /// <summary>
        /// 有內頁的來源賽事id集合    key:類型 , value:賽事id列表
        /// </summary>
        public static ConcurrentDictionary<GpType, HashSet<decimal>> moreDic = new ConcurrentDictionary<GpType, HashSet<decimal>>();

        /// <summary>
        /// MyStake 第一層解析 新增主賽事
        /// </summary>
        /// <param name="html">來源內容</param>
        /// <param name="threadInfo">執行緒資訊</param>
        /// <param name="sendId">非同步要求流水號</param>
        /// <returns>新增、更新的資料ID串列</returns>
        public static void Parser(string html, long sendId, GpType gpType)
        {
            try
            {
                //暫存賽事字典
                ConcurrentDictionary<string, GameInfo> tmpGameDic = new ConcurrentDictionary<string, GameInfo>();
                //暫存來源gid關聯字典
                ConcurrentDictionary<decimal, string> tmpMainDic = new ConcurrentDictionary<decimal, string>();
                //暫存內頁HashSet
                HashSet<decimal> tmpMoreHashSet = new HashSet<decimal>();
                string sData = ConvertTools.GzipDecompress(html.Trim('"'));

                if (!string.IsNullOrEmpty(sData))
                {
                    //數據類別化
                    MyStakeDataFormat dataFormat = (MyStakeDataFormat)JsonConvert.DeserializeObject(sData, typeof(MyStakeDataFormat));
                    //Sport 2 是籃球 84 是3X3籃球
                    var games = dataFormat?.Games?.Where(a => a.Sport == 2 || a.Sport == 84);

                    if (games != null)
                    {
                        foreach (Game game in games)
                        {
                            GameInfo gameInfo = new GameInfo(game.ID.ToString(), sendId);
                            //設定GP類型
                            gameInfo.GpType = gpType;
                            //賽事類型
                            byte gType = (byte)(gpType == GpType.Zd ? GType.BkZd : GType.BkDs);
                            gameInfo.GType = gType;
                            string region = dataFormat?.Regions?.FirstOrDefault(a => a.SportID == game.Sport && a.ID == game.Region)?.Name;
                            string league = dataFormat?.Championats?.FirstOrDefault(a => a.RegionID == game.Region && a.ID == game.Champ)?.Name;
                            string teamA = dataFormat?.Teams?.FirstOrDefault(a => a.ID == game.Team1)?.Name;
                            string teamB = dataFormat?.Teams?.FirstOrDefault(a => a.ID == game.Team2)?.Name;

                            //無聯盟名稱或隊伍名稱則不往下解析
                            if (string.IsNullOrEmpty(region) || string.IsNullOrEmpty(league) || string.IsNullOrEmpty(teamA) || string.IsNullOrEmpty(teamB) ||
                                teamA.Equals(teamB))
                            {
                                continue;
                            }

                            //是否為NBA
                            bool isNBA = league.Contains("NBA") && !league.Contains("WNBA") && !Config.CheckNbaExcludeName(league) && game.Region == 14;
                            //設定聯盟
                            gameInfo.League.Name = $"{region.Trim()} {league.Trim()}";

                            //排除聯盟
                            if (Config.FilterLeagueHashSet.Any(filter => gameInfo.League.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                continue;
                            }

                            //設定預設後綴
                            if (FrmSuffix.suffixList.TryGetValue(gameInfo.League.Name, out int suffixId) && suffixId != 0)
                            {
                                gameInfo.Suffix.Name = WebName.suffixDic[suffixId].En;
                                gameInfo.Suffix.FullName = WebName.suffixDic[suffixId].GetFullName();
                                gameInfo.Suffix.Id = suffixId;
                            }

                            //設定隊伍名稱
                            ConvertTools.ExchangeValue(ref teamA, ref teamB, isNBA);
                            gameInfo.TeamA.Name = teamA.Trim();
                            gameInfo.TeamB.Name = teamB.Trim();

                            #region 設定比賽、帳務時間(比分狀態由內頁解析)

                            gameInfo.SetGameDate(game.StartTime);

                            #endregion

                            gameInfo.Home = isNBA ? 2 : 1;

                            string key = gameInfo.GetGameDictKey();

                            //只保留一筆同帳務隊伍比賽
                            if (tmpGameDic.TryAdd(key, gameInfo))
                            {
                                tmpMoreHashSet.Add(game.ID);
                            }

                            //蒐集來源全場gid
                            tmpMainDic.TryAdd(game.ID, key);
                        }
                    }
                }
                //緩存比對處理
                if (tmpGameDic.Count > 0)
                {
                    foreach (KeyValuePair<string, GameInfo> kvp in tmpGameDic)
                    {
                        //存在才處理
                        if (Config.gameDic.TryGetValue(kvp.Key, out GameInfo getGame))
                        {
                            getGame.Compare(kvp.Value, gpType);

                            //判斷內頁關聯ID是否還存在, 不存在則移除走關閉流程
                            foreach (KeyValuePair<decimal, long[]> tmpkvp in getGame.MoreDataDic)
                            {
                                if (!tmpMainDic.ContainsKey(tmpkvp.Key))
                                {
                                    getGame.MoreDataDic.TryRemove(tmpkvp.Key, out long[] x);
                                }
                            }
                        }
                        //不存在直接添加
                        else
                        {
                            Config.gameDic.TryAdd(kvp.Key, kvp.Value.DeepClone());
                        }
                    }
                }

                #region 更新緩存

                //不存在GP類型則添加
                moreDic.TryAdd(gpType, new HashSet<decimal>());
                //不存在GP類型則添加
                dictMain.TryAdd(gpType, new ConcurrentDictionary<decimal, string>());
                //更新緩存
                moreDic[gpType] = tmpMoreHashSet;

                dictMain[gpType] = tmpMainDic;

                #endregion
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, gpType.ToString() + Environment.NewLine + ex.ToString());
                throw new Exception("MyStake-" + gpType.ToString().ToUpper() + "來源內容解析失敗");
            }
        }

        /// <summary>
        /// MyStake內頁解析
        /// </summary>
        /// <param name="data">數據</param>
        /// <param name="sendId">發送流水號</param>
        /// <param name="gpType">GP類型</param>
        /// <param name="sourceMatchId">來源賽事Id</param>
        public static void MoreParser(string data, long sendId, GpType gpType, decimal sourceMatchId, string gameKey)
        {
            try
            {
                //暫存賽事字典
                ConcurrentDictionary<string, GameInfo> dictGame = new ConcurrentDictionary<string, GameInfo>();
                //賽事不存在跳過
                if (!Config.gameDic.TryGetValue(gameKey, out GameInfo outGameInfo)) { return; }

                string sData = ConvertTools.GzipDecompress(data.Trim('"'));
                if (string.IsNullOrEmpty(sData)) { return; }

                //複製出賽事基本訊息
                GameInfo gameInfo = outGameInfo.CloneBase();
                //數據類別化
                MyStakeInnerDataFormat dataFormat = (MyStakeInnerDataFormat)JsonConvert.DeserializeObject(sData, typeof(MyStakeInnerDataFormat));
                if (dataFormat == null) { return; }

                //是否為NBA
                bool isNBA = gameInfo.League.Name.StartsWith("USA NBA") && !Config.CheckNbaExcludeName(gameInfo.League.Name);
                //是否為三對三籃球
                bool is3x3 = dataFormat.mk?.Where(a => a.Sport == 84)?.FirstOrDefault() != null
                    || dataFormat.Match?.Sport == 84;

                #region 時間狀態、比分解析區塊

                if ((gpType & GpType.Zd) > 0)
                {
                    //賽事狀態 預設ZD中
                    int status = 5;

                    if (dataFormat.Match != null)
                    {
                        #region 比分

                        string[] totalScore = dataFormat.Match.Score?.Split(':');

                        //解析總分
                        if (totalScore != null && totalScore.Length == 2 &&
                            byte.TryParse(totalScore[0], out byte totalScoreA) &&
                            byte.TryParse(totalScore[1], out byte totalScoreB))
                        {
                            ConvertTools.ExchangeValue(ref totalScoreA, ref totalScoreB, isNBA);
                            gameInfo.Score.SetScore(true, totalScoreA, totalScoreB);

                            //有總分在取各節比分，且各比分須為byte型態
                            if (is3x3 == false && dataFormat.Match.SetScores != null && dataFormat.Match.SetScores != "null")
                            {
                                JArray scores = JArray.Parse(dataFormat.Match.SetScores);

                                if (gameInfo.League.Name.Contains("NCAA") && !gameInfo.League.Name.Contains("Women"))
                                {
                                    for (int i = 0; i < scores.Count; i++)
                                    {
                                        string[] score = scores[i].ToString().Split(':');

                                        if (score.Length == 2)
                                        {
                                            switch (i)
                                            {
                                                case 0:
                                                    gameInfo.ScoreHalf1.SetScore(byte.TryParse(score[0], out byte sca) & byte.TryParse(score[1], out byte scb), sca, scb);
                                                    break;
                                                case 1:
                                                    gameInfo.ScoreHalf2.SetScore(byte.TryParse(score[0], out sca) & byte.TryParse(score[1], out scb), sca, scb);
                                                    break;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    for (int i = 0; i < scores.Count; i++)
                                    {
                                        string[] score = scores[i].ToString().Split(':');

                                        if (score.Length == 2)
                                        {
                                            ConvertTools.ExchangeValue(ref score[0], ref score[1], isNBA);

                                            switch (i)
                                            {
                                                case 0:
                                                    gameInfo.Score1.SetScore(byte.TryParse(score[0], out byte sca) & byte.TryParse(score[1], out byte scb), sca, scb);
                                                    break;
                                                case 1:
                                                    gameInfo.Score2.SetScore(byte.TryParse(score[0], out sca) & byte.TryParse(score[1], out scb), sca, scb);
                                                    break;
                                                case 2:
                                                    gameInfo.Score3.SetScore(byte.TryParse(score[0], out sca) & byte.TryParse(score[1], out scb), sca, scb);
                                                    break;
                                                case 3:
                                                    gameInfo.Score4.SetScore(byte.TryParse(score[0], out sca) & byte.TryParse(score[1], out scb), sca, scb);
                                                    break;
                                            }
                                        }
                                    }

                                    if (byte.TryParse((gameInfo.Score1.GetScoreA() + gameInfo.Score2.GetScoreA()).ToString(), out byte scoreHalf1A) &&
                                        byte.TryParse((gameInfo.Score1.GetScoreB() + gameInfo.Score2.GetScoreB()).ToString(), out byte scoreHalf1B) &&
                                        byte.TryParse((gameInfo.Score3.GetScoreA() + gameInfo.Score4.GetScoreA()).ToString(), out byte scoreHalf2A) &&
                                        byte.TryParse((gameInfo.Score3.GetScoreB() + gameInfo.Score4.GetScoreB()).ToString(), out byte scoreHalf2B))
                                    {
                                        gameInfo.ScoreHalf1.SetScore(true, scoreHalf1A, scoreHalf1B);
                                        gameInfo.ScoreHalf2.SetScore(true, scoreHalf2A, scoreHalf2B);
                                    }
                                }
                            }
                        }

                        #endregion

                        #region 狀態 & 剩餘/進行時間

                        //狀態解析
                        switch (dataFormat.Match.EventStatus)
                        {
                            case 15:    //"ITEM1Q"
                            case 25:    //"PAUSE1"
                            case 218:   //"First break"
                                status = 1;
                                break;
                            case 16:    //"ITEM2Q"
                            case 26:    //"PAUSE2"
                                status = 2;
                                break;
                            case 17:    //"ITEM3Q"
                            case 27:    //"PAUSE3"
                            case 220:   //"Third break"
                                status = 3;
                                break;
                            case 18:    //"ITEM4Q"
                            case 28:    //"PAUSE4"
                                status = 4;
                                break;
                            case 24:    //"PAUSED"     NCAA 2X賽制使用
                            case 219:   //"Second break"   一般4X賽制使用
                                status = 6;
                                break;
                            case 3:     //"ITEM1P"  NCAA 2X賽制使用
                            case 216:   //"1st period"    
                                status = 11;
                                break;
                            case 4:     //"ITEM2P"  NCAA 2X賽制使用
                            case 217:   //"2nd period"
                                status = 12;
                                break;
                            case 31:    //"OT"
                                status = 13;
                                break;
                        }

                        //進行時間(Active狀態為True則該欄位會是剩餘時間)
                        string matchTimeExtended = dataFormat.Match.MatchTimeExtended;
                        //累計剩餘時間
                        string remainingTime = dataFormat.Match.RemainingTime;
                        //單節賽制時間
                        string gameMinute = dataFormat.Match.Plng;
                        //時間計數狀態
                        string active = dataFormat.Match.Active;
                        int minute = 0;
                        int seconds = 0;
                        int lastSec = 0;

                        if ((status != 5 && status != 6 && status != 13) || is3x3)
                        {
                            //有剩餘時間則直接抓
                            if (!string.IsNullOrEmpty(remainingTime))
                            {
                                string[] time = remainingTime.Split(':');

                                if (time.Length > 1 && int.TryParse(time[0], out minute) &&
                                                       int.TryParse(time[1], out seconds))
                                {
                                    if (int.TryParse(gameMinute, out int plng))
                                    {
                                        //計算剩餘節次未開打總剩餘時間秒數
                                        int remainQuarterSeconds = 0;

                                        //總節次為4節
                                        if (status < 10)
                                        {
                                            remainQuarterSeconds = ((4 - status) * plng) * 60;
                                        }
                                        //總節次為2節
                                        else
                                        {
                                            remainQuarterSeconds = ((2 - (status % 10)) * plng) * 60;
                                        }

                                        lastSec = minute * 60 + seconds - remainQuarterSeconds;
                                    }
                                    else
                                    {
                                        lastSec = minute * 60 + seconds;
                                    }
                                }
                            }
                            //無剩餘時間抓進行時間
                            else if (!string.IsNullOrEmpty(matchTimeExtended))
                            {
                                string[] time = matchTimeExtended.Split(':');

                                if (time.Length > 1 && int.TryParse(time[0], out minute) &&
                                                       int.TryParse(time[1], out seconds))
                                {
                                    //無計數狀態，則為進行時間，需換算為剩餘時間
                                    if (string.IsNullOrEmpty(active) && int.TryParse(gameMinute, out int plng))
                                    {
                                        //取當前節次進行時間
                                        minute = minute % plng;
                                        lastSec = plng * 60 - (minute * 60 + seconds);
                                    }
                                    else
                                    {
                                        lastSec = minute * 60 + seconds;
                                    }
                                }
                            }
                        }

                        //設置剩餘時間
                        gameInfo.LastSec = lastSec < 0 ? 0 : lastSec;

                        #endregion
                    }

                    //設置狀態
                    gameInfo.Status = is3x3 ? 1 : status;
                }

                #endregion

                #region 全場、上半、第N節、下半

                int allSenceCount = is3x3 ? 0 : 6;

                for (int i = 0; i <= allSenceCount; i++)
                {
                    List<Gmk> playRfA = new List<Gmk>();
                    List<Gmk> playRfB = new List<Gmk>();
                    List<Gmk> playOUA = new List<Gmk>();
                    List<Gmk> playOUB = new List<Gmk>();
                    Gmk playDyA = new Gmk();
                    Gmk playDyB = new Gmk();
                    Gmk playOeA = new Gmk();
                    Gmk playOeB = new Gmk();
                    int specials = i;

                    switch (i)
                    {
                        //全場
                        case 0:
                            if (is3x3)
                            {
                                playRfA = dataFormat.Gmk?.Where(a => a.Mid == 19446 && a.Posn == 1)?.ToList();
                                playRfB = dataFormat.Gmk?.Where(a => a.Mid == 19446 && a.Posn == 2)?.ToList();
                                playOUA = dataFormat.Gmk?.Where(a => a.Mid == 19447 && a.Posn == 1)?.ToList();
                                playOUB = dataFormat.Gmk?.Where(a => a.Mid == 19447 && a.Posn == 2)?.ToList();
                                playDyA = dataFormat.Gmk?.Where(a => a.Mid == 19444 && a.Posn == 1)?.FirstOrDefault();
                                playDyB = dataFormat.Gmk?.Where(a => a.Mid == 19444 && a.Posn == 2)?.FirstOrDefault();
                                playOeA = dataFormat.Gmk?.Where(a => a.Mid == 19450 && a.Posn == 1)?.FirstOrDefault();
                                playOeB = dataFormat.Gmk?.Where(a => a.Mid == 19450 && a.Posn == 2)?.FirstOrDefault();
                            }
                            else
                            {
                                playRfA = dataFormat.Gmk?.Where(a => a.Mid == 770 && a.Posn == 1)?.ToList();
                                playRfB = dataFormat.Gmk?.Where(a => a.Mid == 770 && a.Posn == 2)?.ToList();
                                playOUA = dataFormat.Gmk?.Where(a => a.Mid == 768 && a.Posn == 1)?.ToList();
                                playOUB = dataFormat.Gmk?.Where(a => a.Mid == 768 && a.Posn == 2)?.ToList();
                                playDyA = dataFormat.Gmk?.Where(a => a.Mid == 767 && a.Posn == 1)?.FirstOrDefault();
                                playDyB = dataFormat.Gmk?.Where(a => a.Mid == 767 && a.Posn == 2)?.FirstOrDefault();
                                playOeA = dataFormat.Gmk?.Where(a => a.Mid == 772 && a.Posn == 1)?.FirstOrDefault();
                                playOeB = dataFormat.Gmk?.Where(a => a.Mid == 772 && a.Posn == 2)?.FirstOrDefault();
                            }

                            break;
                        //上半
                        case 1:
                            playRfA = dataFormat.Gmk?.Where(a => a.Mid == 781 && a.Posn == 1)?.ToList();
                            playRfB = dataFormat.Gmk?.Where(a => a.Mid == 781 && a.Posn == 2)?.ToList();
                            playOUA = dataFormat.Gmk?.Where(a => a.Mid == 782 && a.Posn == 1)?.ToList();
                            playOUB = dataFormat.Gmk?.Where(a => a.Mid == 782 && a.Posn == 2)?.ToList();
                            playDyA = dataFormat.Gmk?.Where(a => a.Mid == 780 && a.Posn == 1)?.FirstOrDefault();
                            playDyB = dataFormat.Gmk?.Where(a => a.Mid == 780 && a.Posn == 2)?.FirstOrDefault();
                            playOeA = dataFormat.Gmk?.Where(a => a.Mid == 783 && a.Posn == 1)?.FirstOrDefault();
                            playOeB = dataFormat.Gmk?.Where(a => a.Mid == 783 && a.Posn == 2)?.FirstOrDefault();

                            break;
                        //下半
                        case 2:
                            playRfA = dataFormat.Gmk?.Where(a => a.Mid == 822 && a.Posn == 1)?.ToList();
                            playRfB = dataFormat.Gmk?.Where(a => a.Mid == 822 && a.Posn == 2)?.ToList();
                            playOUA = dataFormat.Gmk?.Where(a => a.Mid == 823 && a.Posn == 1)?.ToList();
                            playOUB = dataFormat.Gmk?.Where(a => a.Mid == 823 && a.Posn == 2)?.ToList();
                            playDyA = dataFormat.Gmk?.Where(a => a.Mid == 821 && a.Posn == 1)?.FirstOrDefault();
                            playDyB = dataFormat.Gmk?.Where(a => a.Mid == 821 && a.Posn == 2)?.FirstOrDefault();
                            playOeA = dataFormat.Gmk?.Where(a => a.Mid == 824 && a.Posn == 1)?.FirstOrDefault();
                            playOeB = dataFormat.Gmk?.Where(a => a.Mid == 824 && a.Posn == 2)?.FirstOrDefault();
                            break;
                        //第一節
                        case 3:
                            playRfA = dataFormat.Gmk?.Where(a => ((a.Mid == 17971 && a.P1 == 1) || a.Mid == 787) && a.Posn == 1)?.ToList();
                            playRfB = dataFormat.Gmk?.Where(a => ((a.Mid == 17971 && a.P1 == 1) || a.Mid == 787) && a.Posn == 2)?.ToList();
                            playOUA = dataFormat.Gmk?.Where(a => ((a.Mid == 17965 && a.P1 == 1) || a.Mid == 786) && a.Posn == 1)?.ToList();
                            playOUB = dataFormat.Gmk?.Where(a => ((a.Mid == 17965 && a.P1 == 1) || a.Mid == 786) && a.Posn == 2)?.ToList();
                            playDyA = dataFormat.Gmk?.Where(a => ((a.Mid == 17970 && a.P1 == 1) || a.Mid == 784) && a.Posn == 1)?.FirstOrDefault();
                            playDyB = dataFormat.Gmk?.Where(a => ((a.Mid == 17970 && a.P1 == 1) || a.Mid == 784) && a.Posn == 2)?.FirstOrDefault();
                            playOeA = dataFormat.Gmk?.Where(a => ((a.Mid == 17972 && a.P1 == 1) || a.Mid == 783) && a.Posn == 1)?.FirstOrDefault();
                            playOeB = dataFormat.Gmk?.Where(a => ((a.Mid == 17972 && a.P1 == 1) || a.Mid == 783) && a.Posn == 2)?.FirstOrDefault();
                            break;
                        //第二節
                        case 4:
                            playRfA = dataFormat.Gmk?.Where(a => ((a.Mid == 17971 && a.P1 == 2) || a.Mid == 795) && a.Posn == 1)?.ToList();
                            playRfB = dataFormat.Gmk?.Where(a => ((a.Mid == 17971 && a.P1 == 2) || a.Mid == 795) && a.Posn == 2)?.ToList();
                            playOUA = dataFormat.Gmk?.Where(a => ((a.Mid == 17965 && a.P1 == 2) || a.Mid == 794) && a.Posn == 1)?.ToList();
                            playOUB = dataFormat.Gmk?.Where(a => ((a.Mid == 17965 && a.P1 == 2) || a.Mid == 794) && a.Posn == 2)?.ToList();
                            playDyA = dataFormat.Gmk?.Where(a => ((a.Mid == 17970 && a.P1 == 2) || a.Mid == 792) && a.Posn == 1)?.FirstOrDefault();
                            playDyB = dataFormat.Gmk?.Where(a => ((a.Mid == 17970 && a.P1 == 2) || a.Mid == 792) && a.Posn == 2)?.FirstOrDefault();
                            playOeA = dataFormat.Gmk?.Where(a => ((a.Mid == 17972 && a.P1 == 2) || a.Mid == 796) && a.Posn == 1)?.FirstOrDefault();
                            playOeB = dataFormat.Gmk?.Where(a => ((a.Mid == 17972 && a.P1 == 2) || a.Mid == 796) && a.Posn == 2)?.FirstOrDefault();
                            break;
                        //第三節
                        case 5:
                            playRfA = dataFormat.Gmk?.Where(a => ((a.Mid == 17971 && a.P1 == 3) || a.Mid == 803) && a.Posn == 1)?.ToList();
                            playRfB = dataFormat.Gmk?.Where(a => ((a.Mid == 17971 && a.P1 == 3) || a.Mid == 803) && a.Posn == 2)?.ToList();
                            playOUA = dataFormat.Gmk?.Where(a => ((a.Mid == 17965 && a.P1 == 3) || a.Mid == 802) && a.Posn == 1)?.ToList();
                            playOUB = dataFormat.Gmk?.Where(a => ((a.Mid == 17965 && a.P1 == 3) || a.Mid == 802) && a.Posn == 2)?.ToList();
                            playDyA = dataFormat.Gmk?.Where(a => ((a.Mid == 17970 && a.P1 == 3) || a.Mid == 800) && a.Posn == 1)?.FirstOrDefault();
                            playDyB = dataFormat.Gmk?.Where(a => ((a.Mid == 17970 && a.P1 == 3) || a.Mid == 800) && a.Posn == 2)?.FirstOrDefault();
                            playOeA = dataFormat.Gmk?.Where(a => ((a.Mid == 17972 && a.P1 == 3) || a.Mid == 804) && a.Posn == 1)?.FirstOrDefault();
                            playOeB = dataFormat.Gmk?.Where(a => ((a.Mid == 17972 && a.P1 == 3) || a.Mid == 804) && a.Posn == 2)?.FirstOrDefault();
                            break;
                        //第四節
                        case 6:
                            playRfA = dataFormat.Gmk?.Where(a => ((a.Mid == 17971 && a.P1 == 4) || a.Mid == 811) && a.Posn == 1)?.ToList();
                            playRfB = dataFormat.Gmk?.Where(a => ((a.Mid == 17971 && a.P1 == 4) || a.Mid == 811) && a.Posn == 2)?.ToList();
                            playOUA = dataFormat.Gmk?.Where(a => ((a.Mid == 17965 && a.P1 == 4) || a.Mid == 810) && a.Posn == 1)?.ToList();
                            playOUB = dataFormat.Gmk?.Where(a => ((a.Mid == 17965 && a.P1 == 4) || a.Mid == 810) && a.Posn == 2)?.ToList();
                            playDyA = dataFormat.Gmk?.Where(a => ((a.Mid == 17970 && a.P1 == 4) || a.Mid == 808) && a.Posn == 1)?.FirstOrDefault();
                            playDyB = dataFormat.Gmk?.Where(a => ((a.Mid == 17970 && a.P1 == 4) || a.Mid == 808) && a.Posn == 2)?.FirstOrDefault();
                            playOeA = dataFormat.Gmk?.Where(a => ((a.Mid == 17972 && a.P1 == 4) || a.Mid == 812) && a.Posn == 1)?.FirstOrDefault();
                            playOeB = dataFormat.Gmk?.Where(a => ((a.Mid == 17972 && a.P1 == 4) || a.Mid == 812) && a.Posn == 2)?.FirstOrDefault();
                            break;
                    }

                    //玩法資訊關聯集合
                    var betRfGroup = ConvertTools.IsInnerJoin(playRfA, playRfB) ? playRfA.Join(playRfB, a => a.Pk, b => -b.Pk, (A, B) => new { Pk = A.Pk, A.Visible, PlA = A.V - 1, PlB = B.V - 1, Diff = Math.Abs(A.V - B.V) }) : null;
                    var betOuGroup = ConvertTools.IsInnerJoin(playOUA, playOUB) ? playOUA.Join(playOUB, a => a.H, b => b.H, (A, B) => new { Pk = A.H, A.Visible, PlA = A.V - 1, PlB = B.V - 1, Diff = Math.Abs(A.V - B.V) }) : null;
                    var betDy = playDyA != null && playDyB != null ? new { Pk = 0, playDyA.Visible, PlA = playDyA.V - 1, PlB = playDyB.V - 1, Diff = Math.Abs(playDyA.V - playDyB.V) } : null;
                    var betOe = playOeA != null && playOeB != null ? new { Pk = 0, playOeA.Visible, PlA = playOeA.V - 1, PlB = playOeB.V - 1, Diff = Math.Abs(playOeA.V - playOeB.V) } : null;

                    if ((betRfGroup != null && betRfGroup.Any()) || (betOuGroup != null && betOuGroup.Any()) || betDy != null || betOe != null)
                    {
                        #region 全場、上半、單節、下半、單節

                        //RF
                        if (Config.SetKindType(gameInfo.GpType, specials, 1, out int kTypeRF))
                        {
                            byte setType = gameInfo.GetSetType(kTypeRF);
                            //生成物件
                            KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeRF, setType, sourceMatchId);
                            kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                            BInfo bInfoA = new BInfo(Config.GetBType(kTypeRF, setType, 1));
                            BInfo bInfoB = new BInfo(Config.GetBType(kTypeRF, setType, 2));

                            if (betRfGroup != null && betRfGroup.Any())
                            {
                                //全場RF不抓0.5(勾上【只跟PK】需判斷0.5是否最平水)
                                var rf = !Config.isZdOnlyPK && specials == 0 ? betRfGroup.Where(a => a.Visible && Math.Abs(a.Pk) != 0.5m && a.Pk != Math.Truncate(a.Pk)).OrderBy(a => a.Diff).FirstOrDefault() :
                                                                               betRfGroup.Where(a => a.Visible && a.Pk != Math.Truncate(a.Pk)).OrderBy(a => a.Diff).FirstOrDefault();
                                if (rf != null)
                                {
                                    //RT[1:A隊讓 0:B隊讓]
                                    int strong = rf.Pk < 0 ? 1 : 0;
                                    if (isNBA) { strong = strong == 1 ? 0 : 1; }
                                    //PK,PL
                                    decimal plate = Math.Abs(rf.Pk);
                                    decimal odds_A = rf.PlA;
                                    decimal odds_B = rf.PlB;
                                    ConvertTools.ExchangeValue(ref odds_A, ref odds_B, isNBA);

                                    //設置數據
                                    kInfo.SetLine(plate, strong);
                                    bInfoA.SetOdds(odds_A);
                                    bInfoB.SetOdds(odds_B);
                                    kInfo.SetIsOpen();
                                }
                            }

                            //放進字典
                            kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                            kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                            //生成gpdm並新增對應字典
                            decimal gpdm = specials * 100 + setType;
                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                            gameInfo.ObjDic[gpdm].TryAdd(kTypeRF, kInfo);
                        }

                        //OU
                        if (Config.SetKindType(gameInfo.GpType, specials, 2, out int kTypeOU))
                        {
                            byte setType = gameInfo.GetSetType(kTypeOU);
                            //生成物件
                            KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOU, setType, sourceMatchId);
                            kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                            BInfo bInfoO = new BInfo(Config.GetBType(kTypeOU, setType, 1));
                            BInfo bInfoU = new BInfo(Config.GetBType(kTypeOU, setType, 2));
                            if (betOuGroup != null && betOuGroup.Any())
                            {
                                var ou = betOuGroup.Where(a => a.Visible && a.Pk != Math.Truncate(a.Pk)).OrderBy(a => a.Diff).FirstOrDefault();

                                if (ou != null)
                                {
                                    //PK,PL
                                    decimal plate = Math.Abs(ou.Pk);
                                    decimal odds_O = ou.PlA;
                                    decimal odds_U = ou.PlB;
                                    //設置數據
                                    kInfo.SetLine(plate);
                                    bInfoO.SetOdds(odds_O);
                                    bInfoU.SetOdds(odds_U);
                                    kInfo.SetIsOpen();
                                }
                            }
                            //放進字典
                            kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                            kInfo.BInfoDic.TryAdd(bInfoU.BType, bInfoU);
                            //生成gpdm並新增對應字典
                            decimal gpdm = specials * 100 + setType;
                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                            gameInfo.ObjDic[gpdm].TryAdd(kTypeOU, kInfo);
                        }

                        //OE
                        if (Config.SetKindType(gameInfo.GpType, specials, 4, out int kTypeOE))
                        {
                            byte setType = gameInfo.GetSetType(kTypeOE);

                            //限定setType只能有一個
                            if (setType == 1)
                            {
                                //生成物件
                                KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOE, setType, sourceMatchId);
                                BInfo bInfoO = new BInfo(Config.GetBType(kTypeOE, setType, 1));
                                BInfo bInfoE = new BInfo(Config.GetBType(kTypeOE, setType, 2));
                                decimal odds_O = 0m;
                                decimal odds_E = 0m;

                                if (!is3x3)
                                {
                                    //手動設置OE
                                    if (Config.isOpenZdOe)
                                    {
                                        odds_O = odds_E = decimal.Floor((Config.zdOERatio) * 100m) / 100m;
                                        kInfo.IsManual = true;
                                    }
                                    else if (betOe != null && betOe.Visible)
                                    {
                                        odds_O = betOe.PlA;
                                        odds_E = betOe.PlB;
                                    }
                                }

                                //設置數據
                                bInfoO.SetOdds(odds_O);
                                bInfoE.SetOdds(odds_E);
                                kInfo.SetIsOpen(odds_O > 0 && odds_E > 0);

                                //放進字典
                                kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                kInfo.BInfoDic.TryAdd(bInfoE.BType, bInfoE);
                                //生成gpdm並新增對應字典
                                decimal gpdm = specials * 100 + setType;
                                gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                gameInfo.ObjDic[gpdm].TryAdd(kTypeOE, kInfo);
                            }
                        }

                        //來源DY
                        if (Config.SetKindType(gameInfo.GpType, specials, 1, out int kTypeDy))
                        {
                            byte setType = 1;
                            //生成物件
                            KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeDy, setType, sourceMatchId);
                            kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                            BInfo bInfoA = new BInfo(Config.GetBType(kTypeDy, setType, 1));
                            BInfo bInfoB = new BInfo(Config.GetBType(kTypeDy, setType, 2));

                            if (betDy != null)
                            {
                                if (betDy.Visible)
                                {
                                    //RT[1:A隊讓 0:B隊讓]
                                    int strong = 1;
                                    if (isNBA) { strong = strong == 1 ? 0 : 1; }
                                    //PK,PL
                                    decimal plate = 0;
                                    decimal odds_A = betDy.PlA;
                                    decimal odds_B = betDy.PlB;
                                    ConvertTools.ExchangeValue(ref odds_A, ref odds_B, isNBA);

                                    //設置數據
                                    kInfo.SetLine(plate, strong);
                                    bInfoA.SetOdds(odds_A);
                                    bInfoB.SetOdds(odds_B);
                                    kInfo.SetIsOpen();

                                    decimal gpdm = specials * 100 + setType;

                                    //ZD參數設置"勾只跟PK"，DY最平水需要抓DY
                                    //"勾只跟PK"來源【全場】最平水RF PK為0.5，此時抓取獨贏
                                    if (Config.isZdOnlyPK &&
                                        gameInfo.ObjDic.TryGetValue(gpdm, out GpdmInfo gpdmInfo) &&
                                        gpdmInfo.TryGetValue(kTypeDy, out KInfo kInfoRf) && kInfoRf.IsOpen)
                                    {
                                        if (kInfoRf.BInfoDic.TryGetValue(bInfoA.BType, out BInfo tmpBIfoA) &&
                                        kInfoRf.BInfoDic.TryGetValue(bInfoB.BType, out BInfo tmpBIfoB) &&
                                        Math.Abs(tmpBIfoA.Odds - tmpBIfoB.Odds) > betDy.Diff ||
                                        (specials == 0 && kInfoRf.Line == 500))
                                        {
                                            //更新kInfo
                                            kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                            kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                            //來源【全場】PK為0.5轉DY，若DY低水低於0.74則關盤
                                            kInfo.SetIsOpen(!(specials == 0 && kInfoRf.Line == 500 && Math.Min(odds_A, odds_B) < 0.74m));
                                            gameInfo.ObjDic[gpdm][kTypeDy] = kInfo;
                                        }
                                    }
                                    else if (Config.isZdOnlyPK && betRfGroup != null && betRfGroup.Any(a => a.Visible))
                                    {
                                        //更新kInfo
                                        kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                        kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                        gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                        gameInfo.ObjDic[gpdm][kTypeDy] = kInfo;
                                    }
                                    //ZD參數設置"未勾只跟PK"，無RF需抓DY
                                    else if (!Config.isZdOnlyPK &&
                                            (!gameInfo.ObjDic.Keys.Contains(gpdm) ||
                                            (gameInfo.ObjDic.TryGetValue(gpdm, out GpdmInfo _gpdmInfo) &&
                                            (!_gpdmInfo.Keys.Contains(kTypeDy) || (_gpdmInfo.TryGetValue(kTypeDy, out KInfo _kInfoRf) && !_kInfoRf.IsOpen)))))
                                    {
                                        //更新kInfo
                                        kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                        kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                        gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                        gameInfo.ObjDic[gpdm][kTypeDy] = kInfo;
                                    }
                                }
                            }
                        }

                        #endregion
                    }
                    else if (specials == 0)
                    {
                        //都沒PKPL，需建全場賽事空殼。(2022/10/05 需求)
                        //RF
                        if (Config.SetKindType(gameInfo.GpType, specials, 1, out int kTypeRF))
                        {
                            byte setType = gameInfo.GetSetType(kTypeRF);
                            //生成物件
                            KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeRF, setType, sourceMatchId);
                            kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                            BInfo bInfoA = new BInfo(Config.GetBType(kTypeRF, setType, 1));
                            BInfo bInfoB = new BInfo(Config.GetBType(kTypeRF, setType, 2));
                            //放進字典
                            kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                            kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                            //生成gpdm並新增對應字典
                            decimal gpdm = specials * 100 + setType;
                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                            gameInfo.ObjDic[gpdm].TryAdd(kTypeRF, kInfo);
                        }

                        //OU
                        if (Config.SetKindType(gameInfo.GpType, specials, 2, out int kTypeOU))
                        {
                            byte setType = gameInfo.GetSetType(kTypeOU);
                            //生成物件
                            KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOU, setType, sourceMatchId);
                            kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                            BInfo bInfoO = new BInfo(Config.GetBType(kTypeOU, setType, 1));
                            BInfo bInfoU = new BInfo(Config.GetBType(kTypeOU, setType, 2));
                            //放進字典
                            kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                            kInfo.BInfoDic.TryAdd(bInfoU.BType, bInfoU);
                            //生成gpdm並新增對應字典
                            decimal gpdm = specials * 100 + setType;
                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                            gameInfo.ObjDic[gpdm].TryAdd(kTypeOU, kInfo);
                        }

                        //OE
                        if (Config.SetKindType(gameInfo.GpType, specials, 4, out int kTypeOE))
                        {
                            byte setType = gameInfo.GetSetType(kTypeOE);

                            //限定setType只能有一個
                            if (setType == 1)
                            {
                                //生成物件
                                KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOE, setType, sourceMatchId);
                                BInfo bInfoO = new BInfo(Config.GetBType(kTypeOE, setType, 1));
                                BInfo bInfoE = new BInfo(Config.GetBType(kTypeOE, setType, 2));
                                //放進字典
                                kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                kInfo.BInfoDic.TryAdd(bInfoE.BType, bInfoE);
                                //生成gpdm並新增對應字典
                                decimal gpdm = specials * 100 + setType;
                                gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                gameInfo.ObjDic[gpdm].TryAdd(kTypeOE, kInfo);
                            }
                        }
                    }
                }

                #endregion

                #region 全場、第N節總得分

                for (int i = 0; i <= 9; i++)
                {
                    List<Gmk> playOUA = new List<Gmk>();
                    List<Gmk> playOUB = new List<Gmk>();
                    int specials = 0;

                    switch (i)
                    {
                        //主隊總得分
                        case 0:
                            specials = Config.TeamTotalIsChangeSpecials(14, isNBA);
                            playOUA = dataFormat.Gmk?.Where(a => a.Mid == 816 && a.Posn == 1)?.ToList();
                            playOUB = dataFormat.Gmk?.Where(a => a.Mid == 816 && a.Posn == 2)?.ToList();
                            break;
                        //客隊總得分
                        case 1:
                            specials = Config.TeamTotalIsChangeSpecials(15, isNBA);
                            playOUA = dataFormat.Gmk?.Where(a => a.Mid == 817 && a.Posn == 1)?.ToList();
                            playOUB = dataFormat.Gmk?.Where(a => a.Mid == 817 && a.Posn == 2)?.ToList();
                            break;
                        //第1節主隊總得分
                        case 2:
                            specials = Config.TeamTotalIsChangeSpecials(20, isNBA);
                            playOUA = dataFormat.Gmk?.Where(a => ((a.Mid == 17975 && a.P1 == 1) || a.Mid == 6753) && a.Posn == 1)?.ToList();
                            playOUB = dataFormat.Gmk?.Where(a => ((a.Mid == 17975 && a.P1 == 1) || a.Mid == 6753) && a.Posn == 2)?.ToList();
                            break;
                        //第1節客隊總得分
                        case 3:
                            specials = Config.TeamTotalIsChangeSpecials(21, isNBA);
                            playOUA = dataFormat.Gmk?.Where(a => ((a.Mid == 17976 && a.P1 == 1) || a.Mid == 6754) && a.Posn == 1)?.ToList();
                            playOUB = dataFormat.Gmk?.Where(a => ((a.Mid == 17976 && a.P1 == 1) || a.Mid == 6754) && a.Posn == 2)?.ToList();
                            break;
                        //第2節主隊總得分
                        case 4:
                            specials = Config.TeamTotalIsChangeSpecials(22, isNBA);
                            playOUA = dataFormat.Gmk?.Where(a => ((a.Mid == 17975 && a.P1 == 2) || a.Mid == 6755) && a.Posn == 1)?.ToList();
                            playOUB = dataFormat.Gmk?.Where(a => ((a.Mid == 17975 && a.P1 == 2) || a.Mid == 6755) && a.Posn == 2)?.ToList();
                            break;
                        //第2節客隊總得分
                        case 5:
                            specials = Config.TeamTotalIsChangeSpecials(23, isNBA);
                            playOUA = dataFormat.Gmk?.Where(a => ((a.Mid == 17976 && a.P1 == 2) || a.Mid == 6756) && a.Posn == 1)?.ToList();
                            playOUB = dataFormat.Gmk?.Where(a => ((a.Mid == 17976 && a.P1 == 2) || a.Mid == 6756) && a.Posn == 2)?.ToList();
                            break;
                        //第3節主隊總得分
                        case 6:
                            specials = Config.TeamTotalIsChangeSpecials(24, isNBA);
                            playOUA = dataFormat.Gmk?.Where(a => ((a.Mid == 17975 && a.P1 == 3) || a.Mid == 6757) && a.Posn == 1)?.ToList();
                            playOUB = dataFormat.Gmk?.Where(a => ((a.Mid == 17975 && a.P1 == 3) || a.Mid == 6757) && a.Posn == 2)?.ToList();
                            break;
                        //第3節客隊總得分
                        case 7:
                            specials = Config.TeamTotalIsChangeSpecials(25, isNBA);
                            playOUA = dataFormat.Gmk?.Where(a => ((a.Mid == 17976 && a.P1 == 3) || a.Mid == 6758) && a.Posn == 1)?.ToList();
                            playOUB = dataFormat.Gmk?.Where(a => ((a.Mid == 17976 && a.P1 == 3) || a.Mid == 6758) && a.Posn == 2)?.ToList();
                            break;
                        //第4節主隊總得分
                        case 8:
                            specials = Config.TeamTotalIsChangeSpecials(26, isNBA);
                            playOUA = dataFormat.Gmk?.Where(a => ((a.Mid == 17975 && a.P1 == 4) || a.Mid == 6759) && a.Posn == 1)?.ToList();
                            playOUB = dataFormat.Gmk?.Where(a => ((a.Mid == 17975 && a.P1 == 4) || a.Mid == 6759) && a.Posn == 2)?.ToList();
                            break;
                        //第4節客隊總得分
                        case 9:
                            specials = Config.TeamTotalIsChangeSpecials(27, isNBA);
                            playOUA = dataFormat.Gmk?.Where(a => ((a.Mid == 17976 && a.P1 == 4) || a.Mid == 6760) && a.Posn == 1)?.ToList();
                            playOUB = dataFormat.Gmk?.Where(a => ((a.Mid == 17976 && a.P1 == 4) || a.Mid == 6760) && a.Posn == 2)?.ToList();
                            break;
                    }

                    //玩法資訊關聯集合
                    var betOuGroup = ConvertTools.IsInnerJoin(playOUA, playOUB) ? playOUA.Join(playOUB, a => a.H, b => b.H, (A, B) => new { Pk = A.H, A.Visible, PlA = A.V - 1, PlB = B.V - 1, Diff = Math.Abs(A.V - B.V) }) : null;

                    #region 全場、單節總入球

                    if ((betOuGroup != null && betOuGroup.Any()))
                    {
                        var ou = betOuGroup.Where(a => a.Visible && a.Pk != Math.Truncate(a.Pk)).OrderBy(a => a.Diff).FirstOrDefault();

                        //OU
                        if (Config.SetKindType(gameInfo.GpType, specials, 2, out int kTypeOU))
                        {
                            byte setType = gameInfo.GetSetType(kTypeOU);
                            //生成物件
                            KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOU, setType, sourceMatchId);
                            kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                            BInfo bInfoO = new BInfo(Config.GetBType(kTypeOU, setType, 1));
                            BInfo bInfoU = new BInfo(Config.GetBType(kTypeOU, setType, 2));

                            if (ou != null)
                            {
                                //PK,PL
                                decimal plate = Math.Abs(ou.Pk);
                                decimal odds_O = ou.PlA;
                                decimal odds_U = ou.PlB;
                                //設置數據
                                kInfo.SetLine(plate);
                                bInfoO.SetOdds(odds_O);
                                bInfoU.SetOdds(odds_U);
                                kInfo.SetIsOpen();
                            }

                            //放進字典
                            kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                            kInfo.BInfoDic.TryAdd(bInfoU.BType, bInfoU);
                            //生成gpdm並新增對應字典
                            decimal gpdm = specials * 100 + setType;
                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                            gameInfo.ObjDic[gpdm].TryAdd(kTypeOU, kInfo);
                        }
                    }

                    #endregion
                }

                #endregion

                gameInfo.CheckPlAndSet();

                //緩存比對處理
                if (Config.gameDic.TryGetValue(gameInfo.GetGameDictKey(), out GameInfo getGame))
                {
                    getGame.Compare(gameInfo, gpType);
                }
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, $"{gpType}, id: {sourceMatchId}{Environment.NewLine}{ex.ToString()}");
            }
        }
    }
}
