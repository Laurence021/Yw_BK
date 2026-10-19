#region 功能修改描述與時間

/*
    描述：XB來源內容解析
    日期：2019-05-16

    描述：修正NCAA來源卡住未換節判斷
    日期：2023-01-06

    描述：外頁解析排除"球員數據"、調整比分狀態解析
    日期：2023-02-02
                        
    描述：調整NBA隊伍主客位置(上客下主)
    日期：2023-03-16
                        
    描述：添加NBA DS玩法(全上鴛鴦盤、優先得分、罰球、籃板、助攻)
    日期：2025-02-20
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
    /// XB解析
    /// </summary>
    public class ParserXB
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
        /// 內外頁關聯ID     key:gId , value:內頁場次id列表
        /// </summary>
        public static ConcurrentDictionary<GpType, ConcurrentDictionary<decimal, HashSet<decimal>>> dicParentId = new ConcurrentDictionary<GpType, ConcurrentDictionary<decimal, HashSet<decimal>>>();
        /// <summary>
        /// 匹配賽制
        /// </summary>
        private static Regex gameFormatReg = new Regex(@"([0-9])x([0-9]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        /// <summary>
        /// 當前聯盟字典
        /// </summary>
        public static ConcurrentDictionary<GpType, List<string>> DictLeague = new ConcurrentDictionary<GpType, List<string>>();
        /// <summary>
        /// 存放來源優先、最後玩法的T，對應解析的specials的字典
        /// </summary>
        private static Dictionary<int, int[]> dictFirstLastSourceT = new Dictionary<int, int[]>()
        {
            //優先獲得三分球、最後獲得三分球
            { 13, new int[]{261,1911,1912,1913,1914} },
            //優先獲得罰球、最後獲得罰球
            { 35, new int[]{161,1915,1916,1917,1918} },
            //優先獲得籃板、最後獲得籃板(目前尚未抓)
            { 32, new int[]{} },
            //優先獲得助攻、最後獲得助攻(目前尚未抓)
            { 31, new int[]{} }
        };

        /// <summary>
        /// 解析目前需要聯盟
        /// </summary>
        /// <param name="html"></param>
        public static void ParserLeague(string html, GpType gpType)
        {
            HashSet<decimal> tmpLis = new HashSet<decimal>();
            JObject obj = JObject.Parse(html);

            if (obj.TryGetValue("Value", out JToken datas))
            {
                //搜尋對應球類
                JToken jToken = datas.FirstOrDefault(a => a.Value<string>("I")?.ToString() == "3" && a.Value<string>("MS") == null)?.SelectToken("L");

                if (jToken != null)
                {
                    for (int i = 0; i < jToken.Count(); i++)
                    {
                        JToken jTokenSc = jToken[i]?["SC"];
                        string li = "";
                        string l = "";

                        if (jTokenSc != null)
                        {
                            for (int j = 0; j < jTokenSc.Count(); j++)
                            {
                                li = jTokenSc[j].Value<string>("LI");
                                l = jTokenSc[j].Value<string>("L");

                                if (decimal.TryParse(li, out decimal value) && !Config.FilterLeagueHashSet.Any(filter => l.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                                {
                                    tmpLis.Add(value);
                                }
                            }
                        }
                        else
                        {
                            li = jToken[i].Value<string>("LI");
                            l = jToken[i].Value<string>("L");

                            if (decimal.TryParse(li, out decimal value) && !Config.FilterLeagueHashSet.Any(filter => l.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                tmpLis.Add(value);
                            }
                        }
                    }
                }
            }

            //每200個分組(比照來源)
            List<string> leagueIds = tmpLis.OrderBy(a => a).Select((s, index) => new { Value = s, Index = index / 200 })
                           .GroupBy(item => item.Index).Select(group => string.Join(",", group.Select(g => g.Value))).ToList();

            DictLeague[gpType] = leagueIds;

            if (gpType == GpType.Ds && Config.entrance == Entrance.XB)
            {
                DictLeague[GpType.Zp] = leagueIds;
            }
        }

        /// <summary>
        /// 1-X-BET 第一層解析 新增主賽事
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
                //Json
                JObject obj = JObject.Parse(html);

                if (!obj.TryGetValue("Value", out JToken datas))
                {
                    return;
                }

                for (int i = 0; i < datas.Count(); i++)
                {
                    if (datas[i].ToString().Contains("Winner") || datas[i].ToString().Contains("Results of the championships"))
                    {
                        continue;
                    }

                    //數據類別化
                    XBDataFormat bc = JsonConvert.DeserializeObject<XBDataFormat>(datas[i].ToString());

                    //防呆 判斷E是否為NULL，排除電競賽事(2K)、外頁球員數據與三分球不抓
                    if (bc == null || bc.E == null || string.IsNullOrEmpty(bc.L) || bc.L.Contains("2K") ||
                        string.IsNullOrEmpty(bc.O1) || string.IsNullOrEmpty(bc.O2) ||
                        bc.TG == "球员数据" || bc.TG == "三分地带进球得分" ||
                        (bc.SC != null && !string.IsNullOrEmpty(bc.SC.CPS) && bc.SC.CPS == "比赛结束"))
                    {
                        continue;
                    }

                    #region 計算Ds與Zp的分界

                    if (gpType != GpType.Zd)
                    {
                        DateTime gameDate = Convert.ToDateTime("1970/01/01 8:00:00").AddSeconds(bc.S);
                        DateTime dsDate = DateTime.Now.Hour >= 12 ? DateTime.Now.Date.AddDays(1).AddHours(12) : DateTime.Now.Date.AddHours(12);

                        if (gpType == GpType.Ds && gameDate >= dsDate)
                        {
                            continue;
                        }
                        else if (gpType == GpType.Zp && gameDate < dsDate)
                        {
                            continue;
                        }
                    }

                    #endregion

                    //設定暫存聯盟
                    string league = bc.L.Trim();               

                    decimal sourceMatchId = (bc.MG == null) ? bc.I : decimal.Parse(bc.MG.ToString());
                    //設定賽事ID
                    GameInfo gameInfo = new GameInfo(sourceMatchId.ToString(), sendId);
                    //設定GP類型
                    gameInfo.GpType = gpType;
                    //賽事類型
                    byte gType = (byte)(gpType == GpType.Zd ? GType.BkZd : GType.BkDs);
                    gameInfo.GType = gType;
                    //設定聯盟
                    gameInfo.League.Name = league;

                    if(Config.ExcludeAssingLeague(gameInfo.League.Name))
                    {
                        continue;
                    }

                    //是否為NBA
                    bool isNBA = false;
                    //預設賽制4X10
                    string gameFormat = "4x10";

                    if (bc.CN == "美国")
                    {
                        //NCAA預設賽制 2X20
                        if ((bc.L.Contains("NCAA") || bc.LE.Contains("NCAA")) && !(bc.L.Contains("WNCAA") || bc.LE.Contains("WNCAA")))
                        {
                            gameFormat = "2x20";
                        }
                        //NBA預設賽制 4X12
                        else if ((bc.L.Contains("NBA") || bc.LE.Contains("NBA")) && !(bc.L.Contains("WNBA") || bc.LE.Contains("WNBA")))
                        {
                            gameFormat = "4x12";

                            if (!Config.CheckNbaExcludeName(gameInfo.League.Name))
                            {
                                isNBA = true;
                            }
                        }
                    }

                    //設定隊伍名稱
                    string teamA = bc.O1.Replace("'", "’").Trim();
                    string teamB = bc.O2.Replace("'", "’").Trim();
                    ConvertTools.ExchangeValue(ref teamA, ref teamB, isNBA);
                    gameInfo.TeamA.Name = teamA;
                    gameInfo.TeamB.Name = teamB;
                    //防止兩隊名稱一樣
                    if (gameInfo.TeamA.Name.Equals(gameInfo.TeamB.Name)) { continue; }

                    //匹配來源賽制時間
                    if (bc.MIS != null)
                    {
                        foreach (MIS mIS in bc.MIS.Where(a => !string.IsNullOrEmpty(a.V)))
                        {
                            Match m = gameFormatReg.Match(mIS.V.Replace("х", "x"));

                            if (m.Success)
                            {
                                gameFormat = m.Value;

                                //賽制時間"2x10"抓至聯盟名 (需求調整從後綴改抓至聯盟名)
                                if (gameFormat == "2x10")
                                {
                                    gameInfo.League.Name = $"{gameInfo.League.Name} {gameFormat}";
                                }
                            }
                        }
                    }

                    //設定預設後綴
                    if (FrmSuffix.suffixList.TryGetValue(gameInfo.League.Name, out int suffixId) && suffixId != 0)
                    {
                        gameInfo.Suffix.Name = WebName.suffixDic[suffixId].Cn;
                        gameInfo.Suffix.FullName = WebName.suffixDic[suffixId].GetFullName();
                        gameInfo.Suffix.Id = suffixId;
                    }

                    #region 設定比賽、帳務時間 與 ZD比分

                    if (bc.S != 0)
                    {
                        //設定比賽時間
                        DateTime simpleDate = Convert.ToDateTime("1970/01/01 8:00:00").AddSeconds(bc.S);

                        //23:59則加一分鐘
                        if (simpleDate.Hour == 23 && simpleDate.Minute == 59)
                        {
                            simpleDate = simpleDate.AddMinutes(1);
                        }

                        gameInfo.SetGameDate(Convert.ToDateTime(simpleDate.ToString("yyyy-MM-dd HH:mm")));

                        //設定Zd比分
                        if (gpType == GpType.Zd)
                        {
                            //賽事狀態 預設ZD中
                            int status = 5;

                            //bc.SC.I 會標記賽前 Pre-game betting 則時間狀態不解析 ("The match has not started" 沒有進行秒數)
                            if (bc.SC != null && (string.IsNullOrEmpty(bc.SC.I) || bc.SC.I != "Pre-game betting"))
                            {
                                //總比分
                                if (bc.SC.FS != null &&
                                    byte.TryParse(bc.SC.FS.S1.ToString(), out byte totalScoreA) &&
                                    byte.TryParse(bc.SC.FS.S2.ToString(), out byte totalScoreB))
                                {
                                    ConvertTools.ExchangeValue(ref totalScoreA, ref totalScoreB, isNBA);
                                    gameInfo.Score.SetScore(true, totalScoreA, totalScoreB);

                                    //有總分在取各節比分，且各比分須為byte型態

                                    //目前僅NCAA，其餘2X10走一般單節格式
                                    if (gameFormat.StartsWith("2x20"))
                                    {
                                        var ps11 = bc.SC.PS.FirstOrDefault(a => a.Key == 11).Value;
                                        var ps12 = bc.SC.PS.FirstOrDefault(a => a.Key == 12).Value;

                                        if (byte.TryParse(ps11.S1.ToString(), out byte scoreHalf1A) &&
                                            byte.TryParse(ps11.S2.ToString(), out byte scoreHalf1B) &&
                                            byte.TryParse(ps12.S1.ToString(), out byte scoreHalf2A) &&
                                            byte.TryParse(ps12.S2.ToString(), out byte scoreHalf2B))
                                        {
                                            gameInfo.Score1.SetScore(true, 0, 0);
                                            gameInfo.Score2.SetScore(true, 0, 0);
                                            gameInfo.Score3.SetScore(true, 0, 0);
                                            gameInfo.Score4.SetScore(true, 0, 0);
                                            gameInfo.ScoreHalf1.SetScore(true, scoreHalf1A, scoreHalf1B);
                                            gameInfo.ScoreHalf2.SetScore(true, scoreHalf2A, scoreHalf2B);
                                        }
                                    }
                                    else
                                    {
                                        var ps1 = bc.SC.PS.FirstOrDefault(a => a.Key == 1).Value;
                                        var ps2 = bc.SC.PS.FirstOrDefault(a => a.Key == 2).Value;
                                        var ps3 = bc.SC.PS.FirstOrDefault(a => a.Key == 3).Value;
                                        var ps4 = bc.SC.PS.FirstOrDefault(a => a.Key == 4).Value;

                                        if (byte.TryParse(ps1.S1.ToString(), out byte score1A) &&
                                            byte.TryParse(ps1.S2.ToString(), out byte score1B) &&
                                            byte.TryParse(ps2.S1.ToString(), out byte score2A) &&
                                            byte.TryParse(ps2.S2.ToString(), out byte score2B) &&
                                            byte.TryParse(ps3.S1.ToString(), out byte score3A) &&
                                            byte.TryParse(ps3.S2.ToString(), out byte score3B) &&
                                            byte.TryParse(ps4.S1.ToString(), out byte score4A) &&
                                            byte.TryParse(ps4.S2.ToString(), out byte score4B) &&
                                            byte.TryParse((score1A + score2A).ToString(), out byte scoreHalf1A) &&
                                            byte.TryParse((score1B + score2B).ToString(), out byte scoreHalf1B) &&
                                            byte.TryParse((score3A + score4A).ToString(), out byte scoreHalf2A) &&
                                            byte.TryParse((score3B + score4B).ToString(), out byte scoreHalf2B))
                                        {
                                            ConvertTools.ExchangeValue(ref score1A, ref score1B, isNBA);
                                            ConvertTools.ExchangeValue(ref score2A, ref score2B, isNBA);
                                            ConvertTools.ExchangeValue(ref score3A, ref score3B, isNBA);
                                            ConvertTools.ExchangeValue(ref score4A, ref score4B, isNBA);
                                            ConvertTools.ExchangeValue(ref scoreHalf1A, ref scoreHalf1B, isNBA);
                                            ConvertTools.ExchangeValue(ref scoreHalf2A, ref scoreHalf2B, isNBA);

                                            gameInfo.Score1.SetScore(true, score1A, score1B);
                                            gameInfo.Score2.SetScore(true, score2A, score2B);
                                            gameInfo.Score3.SetScore(true, score3A, score3B);
                                            gameInfo.Score4.SetScore(true, score4A, score4B);
                                            gameInfo.ScoreHalf1.SetScore(true, scoreHalf1A, scoreHalf1B);
                                            gameInfo.ScoreHalf2.SetScore(true, scoreHalf2A, scoreHalf2B);
                                        }
                                    }
                                }

                                //進行狀態
                                switch (bc.SC.CP)
                                {
                                    default: status = 5; break;
                                    case 1: status = 1; break;
                                    case 2: status = 2; break;
                                    case 3: status = 3; break;
                                    case 4: status = 4; break;
                                    case 11: status = 11; break;
                                    case 12: status = 12; break;
                                    case 5:
                                    case 6:
                                        status = 13;
                                        break;
                                }

                                string[] tmpSplit = gameFormat.Split('x');
                                //節次、單節時間(分)
                                if (int.TryParse(tmpSplit[0], out int quarter) &&
                                    byte.TryParse(tmpSplit[1], out byte minute))
                                {
                                    //設置來源賽制
                                    gameInfo.QuarterTimeFromSource = minute;

                                    //單節剩餘時間
                                    if (bc.SC.TD == -1)
                                    {
                                        gameInfo.LastSec = bc.SC.TS;
                                    }
                                    //進行時間
                                    else
                                    {
                                        gameInfo.RunTimeFromSource = bc.SC.TS;

                                        //來源卡住未換節防護
                                        if ((status < 4 && quarter == 4) || (status == 11 && quarter == 2))
                                        {
                                            //依賽制區分單節秒數
                                            int quarterSec = minute * 60;

                                            //判斷來源進行總秒數是否超出對應單節秒數 (因應需求達到總時長之後第10秒才觸發, 因此加上9秒)
                                            if (bc.SC.TS > (quarterSec * (status % 10) + 9))
                                            {
                                                gameInfo.IsOverDjTime = true;
                                            }
                                        }
                                    }
                                }
                            }

                            //設置狀態
                            gameInfo.Status = Config.Is3x3(gameInfo.League.Name) ? 1 : status;
                        }
                    }
                    //解析失敗跳過
                    else
                    {
                        continue;
                    }

                    #endregion

                    gameInfo.Home = isNBA ? 2 : 1;

                    string key = gameInfo.GetGameDictKey();

                    //只保留一筆同帳務隊伍比賽，賽事頁面不勾GP則不請求內頁
                    if (tmpGameDic.TryAdd(key, gameInfo) && !gameInfo.IsFilter())
                    {
                        tmpMoreHashSet.Add(sourceMatchId);
                    }

                    //蒐集來源全場gid
                    tmpMainDic.TryAdd(sourceMatchId, key);
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
                                    //移除來源已關閉賽事內頁相關ID
                                    if (dicParentId.TryGetValue(gpType, out ConcurrentDictionary<decimal, HashSet<decimal>> tmpDicParentId) &&
                                        tmpDicParentId.TryRemove(tmpkvp.Key, out HashSet<decimal> tmpSceneId))
                                    {
                                        getGame.MoreDataDic.TryRemove(tmpkvp.Key, out long[] x);

                                        foreach (decimal tmpId in tmpSceneId)
                                        {
                                            getGame.MoreDataDic.TryRemove(tmpId, out x);
                                        }
                                    }
                                    //ZD走原邏輯
                                    else if (gpType == GpType.Zd)
                                    {
                                        getGame.MoreDataDic.TryRemove(tmpkvp.Key, out long[] x);
                                    }
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
                else if (tmpMainDic.Count == 0)
                {
                    //未解析出gId對應賽是字典緩存, 清空關聯字典
                    dicParentId.TryAdd(gpType, new ConcurrentDictionary<decimal, HashSet<decimal>>());
                    dicParentId[gpType] = new ConcurrentDictionary<decimal, HashSet<decimal>>();
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
                throw new Exception("XB-" + gpType.ToString().ToUpper() + "來源內容解析失敗");
            }
        }

        /// <summary>
        /// 1-X-BET 第一層解析 新增主賽事
        /// </summary>
        /// <param name="html">來源內容</param>
        /// <param name="threadInfo">執行緒資訊</param>
        /// <param name="sendId">非同步要求流水號</param>
        /// <returns>新增、更新的資料ID串列</returns>
        public static void Parser(ConcurrentBag<string> htmlBag, long sendId, GpType gpType)
        {
            try
            {
                //暫存賽事字典
                ConcurrentDictionary<string, GameInfo> tmpGameDic = new ConcurrentDictionary<string, GameInfo>();
                //暫存來源gid關聯字典
                ConcurrentDictionary<decimal, string> tmpMainDic = new ConcurrentDictionary<decimal, string>();
                //暫存內頁HashSet
                HashSet<decimal> tmpMoreHashSet = new HashSet<decimal>();

                foreach (string html in htmlBag)
                {
                    //Json
                    JObject obj = JObject.Parse(html);

                    if (obj.TryGetValue("Value", out JToken datas))
                    {
                        //搜尋對應球類
                        JToken jToken = datas.FirstOrDefault(a => a.Value<string>("I")?.ToString() == "3" && a.Value<string>("MS") == null)?.SelectToken("L");

                        if (jToken != null)
                        {
                            for (int i = 0; i < jToken.Count(); i++)
                            {
                                JToken jTokenLeague = jToken[i];

                                if (jTokenLeague == null) { continue; }

                                JToken jTokenSc = jTokenLeague["SC"];
                                List<Tuple<JToken, string>> gamesList = new List<Tuple<JToken, string>>();

                                if (jTokenSc != null)
                                {
                                    for (int j = 0; j < jTokenSc.Count(); j++)
                                    {
                                        string league = jTokenSc[j]["L"].ToString();
                                        JToken jTokenGame = jTokenSc[j]["G"];

                                        if (jTokenGame != null)
                                        {
                                            gamesList.Add(new Tuple<JToken, string>(jTokenGame, league));
                                        }
                                    }
                                }
                                else
                                {
                                    JToken jTokenGame = jTokenLeague["G"];
                                    string league = jTokenLeague["L"].ToString();

                                    if (jTokenGame != null)
                                    {
                                        gamesList.Add(new Tuple<JToken, string>(jTokenGame, league));
                                    }
                                }

                                foreach (Tuple<JToken, string> game in gamesList)
                                {
                                    for (int j = 0; j < game.Item1.Count(); j++)
                                    {
                                        //數據類別化
                                        XBDataFormat bc = JsonConvert.DeserializeObject<XBDataFormat>(game.Item1[j].ToString());

                                        //防呆 判斷E是否為NULL，排除電競賽事(2K)、外頁球員數據與三分球不抓、排除Home Away(有兩個隊伍)
                                        if (bc == null || string.IsNullOrEmpty(game.Item2) || game.Item2.Contains("2K") ||
                                            string.IsNullOrEmpty(bc.O1) || string.IsNullOrEmpty(bc.O2) ||
                                            (bc.O1.Contains("/") && bc.O2.Contains("/")) ||
                                            bc.TG == "球员数据" || bc.TG == "三分地带进球得分" ||
                                            (bc.SC != null && !string.IsNullOrEmpty(bc.SC.CPS) && bc.SC.CPS == "比赛结束"))
                                        {
                                            continue;
                                        }

                                        #region 計算Ds與Zp的分界

                                        if (gpType != GpType.Zd)
                                        {
                                            DateTime gameDate = Convert.ToDateTime("1970/01/01 8:00:00").AddSeconds(bc.S);
                                            DateTime dsDate = DateTime.Now.Hour >= 12 ? DateTime.Now.Date.AddDays(1).AddHours(12) : DateTime.Now.Date.AddHours(12);

                                            if (gpType == GpType.Ds && gameDate >= dsDate)
                                            {
                                                continue;
                                            }
                                            else if (gpType == GpType.Zp && gameDate < dsDate)
                                            {
                                                continue;
                                            }
                                        }

                                        #endregion

                                        //設定暫存聯盟
                                        string league = game.Item2.Trim();
                          
                                        decimal sourceMatchId = (bc.MG == null) ? bc.I : decimal.Parse(bc.MG.ToString());
                                        //設定賽事ID
                                        GameInfo gameInfo = new GameInfo(sourceMatchId.ToString(), sendId);
                                        //設定GP類型
                                        gameInfo.GpType = gpType;
                                        //賽事類型
                                        byte gType = (byte)(gpType == GpType.Zd ? GType.BkZd : GType.BkDs);
                                        gameInfo.GType = gType;
                                        //設定聯盟
                                        gameInfo.League.Name = league;

                                        if (Config.ExcludeAssingLeague(gameInfo.League.Name)) { continue; }
                                        //是否為NBA
                                        bool isNBA = false;
                                        //預設賽制4X10
                                        string gameFormat = "4x10";

                                        if (bc.CE == "United States")
                                        {
                                            //NCAA預設賽制 2X20
                                            if ((game.Item2.Contains("NCAA") || bc.LE.Contains("NCAA")) && !(game.Item2.Contains("WNCAA") || bc.LE.Contains("WNCAA")))
                                            {
                                                gameFormat = "2x20";
                                            }
                                            //NBA預設賽制 4X12
                                            else if ((game.Item2.Contains("NBA") || bc.LE.Contains("NBA")) && !(game.Item2.Contains("WNBA") || bc.LE.Contains("WNBA")))
                                            {
                                                gameFormat = "4x12";

                                                if (!Config.CheckNbaExcludeName(gameInfo.League.Name))
                                                {
                                                    isNBA = true;
                                                }
                                            }
                                        }

                                        //設定隊伍名稱
                                        string teamA = bc.O1.Replace("'", "’").Trim();
                                        string teamB = bc.O2.Replace("'", "’").Trim();
                                        ConvertTools.ExchangeValue(ref teamA, ref teamB, isNBA);
                                        gameInfo.TeamA.Name = teamA;
                                        gameInfo.TeamB.Name = teamB;
                                        //防止兩隊名稱一樣
                                        if (gameInfo.TeamA.Name.Equals(gameInfo.TeamB.Name)) { continue; }

                                        //匹配來源賽制時間
                                        if (bc.MIS != null)
                                        {
                                            foreach (MIS mIS in bc.MIS.Where(a => !string.IsNullOrEmpty(a.V)))
                                            {
                                                Match m = gameFormatReg.Match(mIS.V.Replace("х", "x"));

                                                if (m.Success)
                                                {
                                                    gameFormat = m.Value;

                                                    //賽制時間"2x10"抓至聯盟名 (需求調整從後綴改抓至聯盟名)
                                                    if (gameFormat == "2x10")
                                                    {
                                                        gameInfo.League.Name = $"{gameInfo.League.Name} {gameFormat}";
                                                    }
                                                }
                                            }
                                        }

                                        //設定預設後綴
                                        if (FrmSuffix.suffixList.TryGetValue(gameInfo.League.Name, out int suffixId) && suffixId != 0)
                                        {
                                            gameInfo.Suffix.Name = WebName.suffixDic[suffixId].Cn;
                                            gameInfo.Suffix.FullName = WebName.suffixDic[suffixId].GetFullName();
                                            gameInfo.Suffix.Id = suffixId;
                                        }

                                        #region 設定比賽、帳務時間 與 ZD比分

                                        if (bc.S != 0)
                                        {
                                            //設定比賽時間
                                            DateTime simpleDate = Convert.ToDateTime("1970/01/01 8:00:00").AddSeconds(bc.S);

                                            //23:59則加一分鐘
                                            if (simpleDate.Hour == 23 && simpleDate.Minute == 59)
                                            {
                                                simpleDate = simpleDate.AddMinutes(1);
                                            }

                                            gameInfo.SetGameDate(Convert.ToDateTime(simpleDate.ToString("yyyy-MM-dd HH:mm")));

                                            //設定Zd比分
                                            if (gpType == GpType.Zd)
                                            {
                                                //賽事狀態 預設ZD中
                                                int status = 5;

                                                //bc.SC.I 會標記賽前 Pre-game betting 則時間狀態不解析 ("The match has not started" 沒有進行秒數)
                                                if (bc.SC != null && (string.IsNullOrEmpty(bc.SC.I) || bc.SC.I != "Pre-game betting"))
                                                {
                                                    //總比分
                                                    if (bc.SC.FS != null &&
                                                        byte.TryParse(bc.SC.FS.S1.ToString(), out byte totalScoreA) &&
                                                        byte.TryParse(bc.SC.FS.S2.ToString(), out byte totalScoreB))
                                                    {
                                                        ConvertTools.ExchangeValue(ref totalScoreA, ref totalScoreB, isNBA);
                                                        gameInfo.Score.SetScore(true, totalScoreA, totalScoreB);

                                                        //有總分在取各節比分，且各比分須為byte型態

                                                        //目前僅NCAA，其餘2X10走一般單節格式
                                                        if (gameFormat.StartsWith("2x20"))
                                                        {
                                                            var ps11 = bc.SC.PS.FirstOrDefault(a => a.Key == 11).Value;
                                                            var ps12 = bc.SC.PS.FirstOrDefault(a => a.Key == 12).Value;

                                                            if (byte.TryParse(ps11.S1.ToString(), out byte scoreHalf1A) &&
                                                                byte.TryParse(ps11.S2.ToString(), out byte scoreHalf1B) &&
                                                                byte.TryParse(ps12.S1.ToString(), out byte scoreHalf2A) &&
                                                                byte.TryParse(ps12.S2.ToString(), out byte scoreHalf2B))
                                                            {
                                                                gameInfo.Score1.SetScore(true, 0, 0);
                                                                gameInfo.Score2.SetScore(true, 0, 0);
                                                                gameInfo.Score3.SetScore(true, 0, 0);
                                                                gameInfo.Score4.SetScore(true, 0, 0);
                                                                gameInfo.ScoreHalf1.SetScore(true, scoreHalf1A, scoreHalf1B);
                                                                gameInfo.ScoreHalf2.SetScore(true, scoreHalf2A, scoreHalf2B);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            var ps1 = bc.SC.PS.FirstOrDefault(a => a.Key == 1).Value;
                                                            var ps2 = bc.SC.PS.FirstOrDefault(a => a.Key == 2).Value;
                                                            var ps3 = bc.SC.PS.FirstOrDefault(a => a.Key == 3).Value;
                                                            var ps4 = bc.SC.PS.FirstOrDefault(a => a.Key == 4).Value;

                                                            if (byte.TryParse(ps1.S1.ToString(), out byte score1A) &&
                                                                byte.TryParse(ps1.S2.ToString(), out byte score1B) &&
                                                                byte.TryParse(ps2.S1.ToString(), out byte score2A) &&
                                                                byte.TryParse(ps2.S2.ToString(), out byte score2B) &&
                                                                byte.TryParse(ps3.S1.ToString(), out byte score3A) &&
                                                                byte.TryParse(ps3.S2.ToString(), out byte score3B) &&
                                                                byte.TryParse(ps4.S1.ToString(), out byte score4A) &&
                                                                byte.TryParse(ps4.S2.ToString(), out byte score4B) &&
                                                                byte.TryParse((score1A + score2A).ToString(), out byte scoreHalf1A) &&
                                                                byte.TryParse((score1B + score2B).ToString(), out byte scoreHalf1B) &&
                                                                byte.TryParse((score3A + score4A).ToString(), out byte scoreHalf2A) &&
                                                                byte.TryParse((score3B + score4B).ToString(), out byte scoreHalf2B))
                                                            {
                                                                ConvertTools.ExchangeValue(ref score1A, ref score1B, isNBA);
                                                                ConvertTools.ExchangeValue(ref score2A, ref score2B, isNBA);
                                                                ConvertTools.ExchangeValue(ref score3A, ref score3B, isNBA);
                                                                ConvertTools.ExchangeValue(ref score4A, ref score4B, isNBA);
                                                                ConvertTools.ExchangeValue(ref scoreHalf1A, ref scoreHalf1B, isNBA);
                                                                ConvertTools.ExchangeValue(ref scoreHalf2A, ref scoreHalf2B, isNBA);

                                                                gameInfo.Score1.SetScore(true, score1A, score1B);
                                                                gameInfo.Score2.SetScore(true, score2A, score2B);
                                                                gameInfo.Score3.SetScore(true, score3A, score3B);
                                                                gameInfo.Score4.SetScore(true, score4A, score4B);
                                                                gameInfo.ScoreHalf1.SetScore(true, scoreHalf1A, scoreHalf1B);
                                                                gameInfo.ScoreHalf2.SetScore(true, scoreHalf2A, scoreHalf2B);
                                                            }
                                                        }
                                                    }

                                                    //進行狀態
                                                    switch (bc.SC.CP)
                                                    {
                                                        default: status = 5; break;
                                                        case 1: status = 1; break;
                                                        case 2: status = 2; break;
                                                        case 3: status = 3; break;
                                                        case 4: status = 4; break;
                                                        case 11: status = 11; break;
                                                        case 12: status = 12; break;
                                                        case 5:
                                                        case 6:
                                                            status = 13;
                                                            break;
                                                    }

                                                    string[] tmpSplit = gameFormat.Split('x');
                                                    //節次、單節時間(分)
                                                    if (int.TryParse(tmpSplit[0], out int quarter) &&
                                                        byte.TryParse(tmpSplit[1], out byte minute))
                                                    {
                                                        //設置來源賽制
                                                        gameInfo.QuarterTimeFromSource = minute;

                                                        //單節剩餘時間
                                                        if (bc.SC.TD == -1)
                                                        {
                                                            gameInfo.LastSec = bc.SC.TS;
                                                        }
                                                        //進行時間
                                                        else
                                                        {
                                                            gameInfo.RunTimeFromSource = bc.SC.TS;

                                                            //來源卡住未換節防護
                                                            if ((status < 4 && quarter == 4) || (status == 11 && quarter == 2))
                                                            {
                                                                //依賽制區分單節秒數
                                                                int quarterSec = minute * 60;

                                                                //判斷來源進行總秒數是否超出對應單節秒數 (因應需求達到總時長之後第10秒才觸發, 因此加上9秒)
                                                                if (bc.SC.TS > (quarterSec * (status % 10) + 9))
                                                                {
                                                                    gameInfo.IsOverDjTime = true;
                                                                }
                                                            }
                                                        }
                                                    }
                                                }

                                                //設置狀態
                                                gameInfo.Status = Config.Is3x3(gameInfo.League.Name) ? 1 : status;
                                            }
                                        }
                                        //解析失敗跳過
                                        else
                                        {
                                            continue;
                                        }

                                        #endregion

                                        gameInfo.Home = isNBA ? 2 : 1;

                                        string key = gameInfo.GetGameDictKey();

                                        //只保留一筆同帳務隊伍比賽，賽事頁面不勾GP則不請求內頁
                                        if (tmpGameDic.TryAdd(key, gameInfo) && !gameInfo.IsFilter())
                                        {
                                            tmpMoreHashSet.Add(sourceMatchId);
                                        }

                                        //蒐集來源全場gid
                                        tmpMainDic.TryAdd(sourceMatchId, key);
                                    }
                                }
                            }
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
                                    //移除來源已關閉賽事內頁相關ID
                                    if (dicParentId.TryGetValue(gpType, out ConcurrentDictionary<decimal, HashSet<decimal>> tmpDicParentId) &&
                                        tmpDicParentId.TryRemove(tmpkvp.Key, out HashSet<decimal> tmpSceneId))
                                    {
                                        getGame.MoreDataDic.TryRemove(tmpkvp.Key, out long[] x);

                                        foreach (decimal tmpId in tmpSceneId)
                                        {
                                            getGame.MoreDataDic.TryRemove(tmpId, out x);
                                        }
                                    }
                                    //ZD走原邏輯
                                    else if (gpType == GpType.Zd)
                                    {
                                        getGame.MoreDataDic.TryRemove(tmpkvp.Key, out long[] x);
                                    }
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
                else if (tmpMainDic.Count == 0)
                {
                    //未解析出gId對應賽是字典緩存, 清空關聯字典
                    dicParentId.TryAdd(gpType, new ConcurrentDictionary<decimal, HashSet<decimal>>());
                    dicParentId[gpType] = new ConcurrentDictionary<decimal, HashSet<decimal>>();
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
                throw new Exception("XB-" + gpType.ToString().ToUpper() + "來源內容解析失敗");
            }
        }

        /// <summary>
        /// XB內頁解析
        /// </summary>
        /// <param name="data">數據</param>
        /// <param name="sendId">發送流水號</param>
        /// <param name="gpType">GP類型</param>
        /// <param name="sourceMatchId">來源賽事Id</param>
        public static void MoreParser(string data, long sendId, GpType gpType, decimal sourceMatchId, string gameKey)
        {
            try
            {
                //數據轉jobject
                JObject obj = JObject.Parse(data);
                //數據類別化
                XBDataFormat bc = JsonConvert.DeserializeObject<XBDataFormat>(obj["Value"].ToString());
                //暫存內頁節次佇列
                HashSet<decimal> tmpSceneHashset = new HashSet<decimal>();

                //賽事不存在跳過
                if (!Config.gameDic.TryGetValue(gameKey, out GameInfo outGameInfo)) { return; }

                GpType tmpGpType = gpType & (GpType.Ds | GpType.Zd | GpType.Zp);

                //轉bc失敗，移除HashSet相對應mainGpdm
                if (bc == null)
                {
                    if (moreDic.TryGetValue(tmpGpType, out HashSet<decimal> tmp))
                    {
                        tmp.Remove(sourceMatchId);
                    }

                    return;
                }

                //複製出賽事基本訊息
                GameInfo gameInfo = outGameInfo.CloneBase();

                #region 主盤

                bool isNBA = (bc.L.Contains("NBA") || bc.LE.Contains("NBA")) &&
                            !(bc.L.Contains("WNBA") || bc.LE.Contains("WNBA")) &&
                             !string.IsNullOrEmpty(bc.CN) && bc.CN.Equals("美国") &&
                            !Config.CheckNbaExcludeName(gameInfo.League.Name);

                if (bc.GE != null)
                {
                    int specials = bc.P != null ? GetSpecial(bc.P) : 0;
                    //是否需解析玩法
                    bool isParser = true;

                    //DS賽事額外判斷三分球總數、罰牌得分、籃板、助攻
                    if (tmpGpType != GpType.Zd && !string.IsNullOrEmpty(bc.TG))
                    {
                        switch (bc.TG)
                        {
                            //三分球總數
                            case "三分地带进球得分": specials = 13; break;
                            //首先獲得罰球, 最後獲得罰球
                            case "罚球得分": specials = 35; break;
                            //球隊籃板
                            case "篮板": specials = 32; break;
                            //球隊助攻
                            case "助攻": specials = 31; break;
                            default: isParser = false; break;
                        }
                    }

                    //三分球總數, NBA以外的聯盟不解析
                    //XB來源2х10不抓單節玩法
                    if ((specials == 13 && gameInfo.GType == (byte)GType.BkZd && !isNBA) ||
                        ((gameInfo.League.Name.Contains("2x10") || gameInfo.Suffix.Name.Contains("2x10")) && specials >= 3))
                    {
                        isParser = false;
                    }

                    if (isParser)
                    {
                        List<Es> tmpEs = new List<Es>();

                        // 收集主盤所有玩法
                        foreach (GEs gEs in bc.GE)
                        {
                            for (int x = 0; x < gEs.E.Length; x++)
                            {
                                foreach (Es es in gEs.E[x])
                                {
                                    //鎖定的玩法不判斷
                                    if (es.B) { continue; }

                                    tmpEs.Add(es);
                                }
                            }
                        }

                        SetMainMatch(sendId, gpType, sourceMatchId, gameInfo, specials, tmpEs, isNBA);

                        if (isNBA || tmpGpType == GpType.Zd)
                        {
                            //總得分
                            int totalSpecials = specials * 2 + 14;
                            SetTotalMatch(sendId, gpType, sourceMatchId, gameInfo, Config.TeamTotalIsChangeSpecials(totalSpecials, isNBA), tmpEs, true);
                            SetTotalMatch(sendId, gpType, sourceMatchId, gameInfo, Config.TeamTotalIsChangeSpecials(totalSpecials + 1, isNBA), tmpEs, false);
                        }

                        if (isNBA)
                        {
                            if (!dictFirstLastSourceT.ContainsKey(specials))
                            {
                                if (specials == 0)
                                {
                                    //搶首
                                    SetSpecialMatch(sendId, gpType, sourceMatchId, gameInfo, 60, tmpEs, 1, 1052, 1053, isNBA);
                                    //搶尾
                                    SetSpecialMatch(sendId, gpType, sourceMatchId, gameInfo, 70, tmpEs, 1, 1942, 1943, isNBA);
                                }

                                //優先獲得N分，NBA DS只抓全場、第二節 (全場101 第二節105)
                                if (specials == 0 || specials == 4)
                                {
                                    var firstNScores = tmpEs.Where(x => x.T == 11895);
                                    int specialsFirstNScore = specials + 101;

                                    foreach (var firstNScore in firstNScores)
                                    {
                                        SetSpecialMatch(sendId, gpType, sourceMatchId, gameInfo, specialsFirstNScore, tmpEs, (byte)firstNScore.P, 11895, 11896, isNBA, true);
                                    }
                                }

                                //第二、四節優先得分隊伍
                                if (specials == 4 || specials == 6)
                                {
                                    int tmpSepcials = specials == 4 ? 112 : 113;
                                    //優先獲得
                                    SetSpecialMatch(sendId, gpType, sourceMatchId, gameInfo, tmpSepcials, tmpEs, 1, 1052, 1053, isNBA);
                                }
                            }
                            //優先獲得和最後獲得玩法(三分球、罰球)
                            else
                            {
                                int[] tmpSourceT = dictFirstLastSourceT[specials];

                                if (tmpSourceT.Length > 4)
                                {
                                    int threeSpecials = tmpSourceT[0];
                                    //優先獲得
                                    SetSpecialMatch(sendId, gpType, sourceMatchId, gameInfo, threeSpecials, tmpEs, 1, tmpSourceT[1], tmpSourceT[2], isNBA);
                                    //最後獲得
                                    SetSpecialMatch(sendId, gpType, sourceMatchId, gameInfo, threeSpecials, tmpEs, 9, tmpSourceT[3], tmpSourceT[4], isNBA);
                                }
                            }
                        }
                    }
                }

                #endregion

                //內頁無玩法則離開
                if (bc.SG != null)
                {
                    for (int i = 0; i < bc.SG.Length; i++)
                    {
                        if (SetLastNum(bc.SG[i], out int specials))
                        {
                            //三分球總數, NBA以外的聯盟不解析
                            if (specials == 13 && gameInfo.GType == (byte)GType.BkZd && !isNBA) { continue; }
                            //XB來源2х10不抓單節玩法
                            if ((gameInfo.League.Name.Contains("2x10") || gameInfo.Suffix.Name.Contains("2x10")) && specials >= 3) { continue; }
                            //罰球得分,籃板,助攻, 除DS NBA以外不解析
                            if ((specials == 35 || specials == 32 || specials == 31) && !(isNBA && gameInfo.GType == (byte)GType.BkDs)) { continue; }

                            //DS/ZP蒐集賽事節次ID
                            if (gameInfo.GType != (byte)GType.BkZd && specials > 0)
                            {
                                //其他籃球(非NBA), 不蒐集2,4節ID, 排除不請求
                                if (!isNBA && (specials == 4 || specials == 6)) { continue; }
                                //蒐集賽事節次ID
                                if (decimal.TryParse(bc.SG[i].CI.ToString(), out decimal sceneId))
                                {
                                    tmpSceneHashset.Add(sceneId);
                                }
                            }

                            if (bc.SG[i].GE != null)
                            {
                                List<Es> tmpEs = new List<Es>();

                                //收集各節次所有玩法
                                foreach (GEs gEs in bc.SG[i].GE)
                                {
                                    for (int x = 0; x < gEs.E.Length; x++)
                                    {
                                        foreach (Es es in gEs.E[x])
                                        {
                                            //鎖定的玩法不判斷
                                            if (es.B) { continue; }

                                            tmpEs.Add(es);
                                        }
                                    }
                                }

                                //有開啟才解析
                                if ((gpType == GpType.DsMore && Config.isOpenDs) ||
                                    (gpType == GpType.ZdMore && Config.isOpenZd) ||
                                    (gpType == GpType.ZpMore && Config.isOpenZp))
                                {
                                    SetMainMatch(sendId, gpType, sourceMatchId, gameInfo, specials, tmpEs, isNBA);

                                    if (!dictFirstLastSourceT.ContainsKey(specials))
                                    {
                                        //總得分
                                        int totalSpecials = specials * 2 + 14;
                                        SetTotalMatch(sendId, gpType, sourceMatchId, gameInfo, Config.TeamTotalIsChangeSpecials(totalSpecials, isNBA), tmpEs, true);
                                        SetTotalMatch(sendId, gpType, sourceMatchId, gameInfo, Config.TeamTotalIsChangeSpecials(totalSpecials + 1, isNBA), tmpEs, false);
                                    }
                                    //優先獲得和最後獲得玩法
                                    else if (isNBA && (dictFirstLastSourceT.ContainsKey(specials)))
                                    {
                                        int[] tmpSourceT = dictFirstLastSourceT[specials];

                                        if (tmpSourceT.Length > 4)
                                        {
                                            int threeSpecials = tmpSourceT[0];
                                            //優先獲得
                                            SetSpecialMatch(sendId, gpType, sourceMatchId, gameInfo, threeSpecials, tmpEs, 1, tmpSourceT[1], tmpSourceT[2], isNBA);
                                            //最後獲得
                                            SetSpecialMatch(sendId, gpType, sourceMatchId, gameInfo, threeSpecials, tmpEs, 9, tmpSourceT[3], tmpSourceT[4], isNBA);
                                        }

                                    }
                                }
                            }
                        }
                    }
                }

                gameInfo.CheckPlAndSet();

                //緩存比對處理
                if (Config.gameDic.TryGetValue(gameInfo.GetGameDictKey(), out GameInfo getGame))
                {
                    getGame.Compare(gameInfo, gpType);
                }

                //DS/ZP場次ID更新緩存
                if (tmpGpType != GpType.Zd && tmpSceneHashset.Count > 0)
                {
                    //更新緩存
                    if (dicParentId.TryGetValue(tmpGpType, out ConcurrentDictionary<decimal, HashSet<decimal>> tmpDicParentId))
                    {
                        //存內頁玩法數量字典
                        tmpDicParentId.AddOrUpdate(sourceMatchId, tmpSceneHashset, (k, v) => { return tmpSceneHashset; });
                    }
                    else
                    {
                        dicParentId.TryAdd(tmpGpType, new ConcurrentDictionary<decimal, HashSet<decimal>>());
                        dicParentId[tmpGpType].TryAdd(sourceMatchId, tmpSceneHashset);
                    }
                }
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, $"{gpType}, id:{sourceMatchId}{Environment.NewLine}{ex.ToString()}");
            }
        }

        /// <summary>
        /// 全場、上半、下半、第N節
        /// </summary>
        /// <param name="sendId">發送流水號</param>
        /// <param name="gpType">執行序gpType</param>
        /// <param name="sourceMatchId">來源賽事Id</param>
        /// <param name="gameInfo">賽事資訊</param>
        /// <param name="specials">特殊玩法分類</param>
        /// <param name="tmpEs">存放玩法Es字典</param>
        /// <param name="isNBA">是否為NBA</param>
        private static void SetMainMatch(long sendId, GpType gpType, decimal sourceMatchId, GameInfo gameInfo, int specials, List<Es> tmpEs, bool isNBA)
        {
            //判斷是否抓鴛鴦盤
            bool isSameCount = false;

            if ((gpType & GpType.Zd) == 0 && Config.sameCount > 1)
            {
                //全場
                if (specials == 0)
                {
                    isSameCount = true;
                }
                //上半
                else if (specials == 1)
                {
                    isSameCount = Config.isSbDjSame;
                }
            }

            //是否為3x3籃球
            bool is3x3 = Config.Is3x3(gameInfo.League.Name);
            decimal[] plData = SetMatch(tmpEs, gameInfo.GpType, specials, isSameCount, out bool sourceHaveRf, out Dictionary<string, List<PlateInfo>> allEsDic);
            //是否隨隊伍交換
            bool isChangeTeam = isNBA;

            //RF
            if (Config.SetKindType(gameInfo.GpType, specials, 1, out int kTypeRF))
            {
                byte setType = gameInfo.GetSetType(kTypeRF);
                //生成物件
                KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeRF, setType, sourceMatchId);
                kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                BInfo bInfoA = new BInfo(Config.GetBType(kTypeRF, setType, 1));
                BInfo bInfoB = new BInfo(Config.GetBType(kTypeRF, setType, 2));

                if (plData[2] != 0m && plData[3] != 0m)
                {
                    //RT[1:A隊讓 0:B隊讓]
                    int strong = Convert.ToInt16(plData[0]);
                    if (isChangeTeam) { strong = strong == 1 ? 0 : 1; }
                    //PK,PL
                    decimal plate = Convert.ToDecimal(plData[1]);
                    decimal odds_A = Convert.ToDecimal(plData[2]);
                    decimal odds_B = Convert.ToDecimal(plData[3]);
                    ConvertTools.ExchangeValue(ref odds_A, ref odds_B, isChangeTeam);
                    //設置數據
                    kInfo.SetLine(plate, strong);
                    bInfoA.SetOdds(odds_A);
                    bInfoB.SetOdds(odds_B);
                    kInfo.SetIsOpen();
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

                if (plData[5] != 0m && plData[6] != 0m)
                {
                    //PK,PL
                    decimal plate = Convert.ToDecimal(plData[4]);
                    decimal odds_O = Convert.ToDecimal(plData[5]);
                    decimal odds_U = Convert.ToDecimal(plData[6]);
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

            int type = gameInfo.GpType == GpType.Zd ? 1 : 3;

            //1*2
            if (Config.SetKindType(gameInfo.GpType, specials, type, out int kTypeDy))
            {
                byte setType = 1;

                //生成物件
                KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeDy, setType, sourceMatchId);
                BInfo bInfoA = new BInfo(Config.GetBType(kTypeDy, setType, 1));
                BInfo bInfoB = new BInfo(Config.GetBType(kTypeDy, setType, 2));
                //生成gpdm並新增對應字典
                decimal gpdm = specials * 100 + setType;

                //沒有三項才解析PL
                if (plData[7] != 0m && plData[8] != 0m)
                {
                    //PL
                    decimal odds_A = Convert.ToDecimal(plData[7]);
                    decimal odds_B = Convert.ToDecimal(plData[8]);
                    ConvertTools.ExchangeValue(ref odds_A, ref odds_B, isChangeTeam);
                    //設置數據
                    bInfoA.SetOdds(odds_A);
                    bInfoB.SetOdds(odds_B);
                    kInfo.SetIsOpen();

                    if (gameInfo.GpType == GpType.Zd)
                    {
                        //ZD參數設置"勾只跟PK"，DY最平水需要抓DY
                        //"勾只跟PK"來源【全場】最平水RF PK為0.5，此時抓取獨贏
                        if (Config.isZdOnlyPK &&
                            gameInfo.ObjDic.TryGetValue(gpdm, out GpdmInfo gpdmInfo) &&
                            gpdmInfo.TryGetValue(kTypeDy, out KInfo kInfoRf) && kInfoRf.IsOpen)
                        {
                            if (kInfoRf.BInfoDic.TryGetValue(bInfoA.BType, out BInfo tmpBIfoA) &&
                            kInfoRf.BInfoDic.TryGetValue(bInfoB.BType, out BInfo tmpBIfoB) &&
                            Math.Abs(tmpBIfoA.Odds - tmpBIfoB.Odds) > Math.Abs(odds_A - odds_B) ||
                            (specials == 0 && kInfoRf.Line == 500))
                            {
                                kInfo.IsSourceFollow = 1;
                                //更新kInfo
                                kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                //來源【全場】PK為0.5轉DY，若DY低水低於0.74則關盤
                                kInfo.SetIsOpen(!(specials == 0 && kInfoRf.Line == 500 && Math.Min(odds_A, odds_B) < 0.74m));
                                gameInfo.ObjDic[gpdm][kTypeDy] = kInfo;
                            }
                        }
                        else if (Config.isZdOnlyPK && sourceHaveRf)
                        {
                            kInfo.IsSourceFollow = 1;
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

                if (gameInfo.GpType != GpType.Zd)
                {
                    //放進字典
                    kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                    kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                    gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                    gameInfo.ObjDic[gpdm].TryAdd(kTypeDy, kInfo);
                }
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

                    //手動設置OE 
                    if (!is3x3 && (gameInfo.GpType != GpType.Zd && Config.isOpenDsZpOe) || (gameInfo.GpType == GpType.Zd && Config.isOpenZdOe))
                    {
                        if (gameInfo.GpType == GpType.Zd)
                        {
                            odds_O = odds_E = decimal.Floor((Config.zdOERatio) * 100m) / 100m;
                        }
                        else
                        {
                            odds_O = odds_E = decimal.Floor((Config.dsOERatio) * 100m) / 100m;
                        }

                        kInfo.IsManual = true;
                    }
                    //籃板和助攻不抓OE
                    else if (!(gameInfo.GpType == GpType.Zd && is3x3) && (specials != 31 && specials != 32))
                    {
                        if (plData[9] != 0 && plData[10] != 0)
                        {
                            odds_O = plData[9];
                            odds_E = plData[10];
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

            //鴛鴦盤抓取
            if (isSameCount)
            {
                HandleSameCount(gameInfo, allEsDic, specials, gpType, sendId, sourceMatchId, isNBA);
            }
        }

        /// <summary>
        /// 總得分
        /// </summary>
        /// <param name="sendId">發送流水號</param>
        /// <param name="gpType">執行序gpType</param>
        /// <param name="sourceMatchId">來源賽事Id</param>
        /// <param name="gameInfo">賽事資訊</param>
        /// <param name="specials">特殊玩法分類</param>
        /// <param name="tmpEs">存放玩法Es字典</param>
        private static void SetTotalMatch(long sendId, GpType gpType, decimal sourceMatchId, GameInfo gameInfo, int specials, List<Es> tmpEs, bool isTeamA)
        {
            decimal[] plData = SetTotalMatchOdds(tmpEs, isTeamA, gpType);

            //來源有才解析
            if (plData.Any(a => a > 0))
            {
                //OU
                if (Config.SetKindType(gameInfo.GpType, specials, 2, out int kTypeOU))
                {
                    byte setType = gameInfo.GetSetType(kTypeOU);
                    //生成物件
                    KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOU, setType, sourceMatchId);
                    kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                    BInfo bInfoO = new BInfo(Config.GetBType(kTypeOU, setType, 1));
                    BInfo bInfoU = new BInfo(Config.GetBType(kTypeOU, setType, 2));

                    if (plData[1] != 0m && plData[2] != 0m)
                    {
                        //PK,PL
                        decimal plate = Convert.ToDecimal(plData[0]);
                        decimal odds_O = Convert.ToDecimal(plData[1]);
                        decimal odds_U = Convert.ToDecimal(plData[2]);
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
                        decimal odds_A;
                        decimal odds_B;

                        //手動設置OE (排除 ZD 全場/上下半/單節 主客隊總得分 oe設置)
                        if ((gameInfo.GpType != GpType.Zd && Config.isOpenDsZpOe) || (gameInfo.GpType == GpType.Zd && Config.isOpenZdOe))
                        {
                            if (gameInfo.GpType == GpType.Zd)
                            {
                                odds_A = odds_B = decimal.Floor((Config.zdOERatio) * 100m) / 100m;
                            }
                            else
                            {
                                odds_A = odds_B = decimal.Floor((Config.dsOERatio) * 100m) / 100m;
                            }

                            //設置數據
                            bInfoO.SetOdds(odds_A);
                            bInfoE.SetOdds(odds_B);
                            kInfo.SetIsOpen(odds_A > 0 && odds_B > 0);

                            kInfo.IsManual = true;
                        }

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

        /// <summary>
        /// 全場、上半、下半、第N節
        /// </summary>
        /// <param name="sendId">發送流水號</param>
        /// <param name="gpType">執行序gpType</param>
        /// <param name="sourceMatchId">來源賽事Id</param>
        /// <param name="gameInfo">賽事資訊</param>
        /// <param name="specials">特殊玩法分類</param>
        /// <param name="tmpEs">存放玩法Es字典</param>
        /// <param name="leftTNum">來源左邊PL代號</param>
        /// <param name="rightTNum">來源右邊PL代號</param>
        /// <param name="isNBA">是否為NBA</param>
        /// <param name="isNBADsFirstNScore">是否為NBA DS 首先獲得N分</param>
        private static void SetSpecialMatch(long sendId, GpType gpType, decimal sourceMatchId, GameInfo gameInfo, int specials, List<Es> tmpEs, byte setType, int leftTNum, int rightTNum, bool isNBA, bool isNBADsFirstNScore = false)
        {
            decimal[] plData = isNBADsFirstNScore ? SetFirstNScoresOdds(tmpEs, leftTNum, rightTNum, setType, isNBA) : SetSpecialMatchOdds(tmpEs, leftTNum, rightTNum);
            //是否隨隊伍交換
            bool isChangeTeam = isNBA;

            //來源有才解析
            if (plData.Any(a => a > 0))
            {
                //RF
                if (Config.SetKindType(gameInfo.GpType, specials, 1, out int kTypeRF))
                {
                    //生成物件
                    KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeRF, setType, sourceMatchId);
                    kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                    BInfo bInfoA = new BInfo(Config.GetBType(kTypeRF, setType, 1));
                    BInfo bInfoB = new BInfo(Config.GetBType(kTypeRF, setType, 2));

                    if (plData[2] != 0m && plData[3] != 0m)
                    {
                        //RT[1:A隊讓 0:B隊讓]
                        int strong = Convert.ToInt16(plData[0]);
                        if (isChangeTeam) { strong = strong == 1 ? 0 : 1; }
                        //PK,PL
                        decimal plate = Convert.ToDecimal(plData[1]);
                        decimal odds_A = Convert.ToDecimal(plData[2]);
                        decimal odds_B = Convert.ToDecimal(plData[3]);
                        ConvertTools.ExchangeValue(ref odds_A, ref odds_B, isChangeTeam);
                        //設置數據                       
                        kInfo.SetLine(plate, strong);
                        bInfoA.SetOdds(odds_A);
                        bInfoB.SetOdds(odds_B);
                        kInfo.SetIsOpen();
                    }

                    //放進字典
                    kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                    kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                    //生成gpdm並新增對應字典
                    decimal gpdm = specials * 100 + setType;
                    gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                    gameInfo.ObjDic[gpdm].TryAdd(kTypeRF, kInfo);
                }
            }
        }

        /// <summary>
        /// 設定玩法
        /// </summary>
        /// <param name="es">玩法矩陣</param>
        /// <param name="gpType">GP類型</param>
        /// <param name="specials">特殊玩法分類(判斷全場用)</param>
        /// <param name="isSameCount">是否抓鴛鴦盤</param>
        /// <param name="sourceHaveRf">回傳來源是否有RF</param>
        /// <param name="allEsDic">蒐集來源所有玩法(鴛鴦盤使用)</param>
        /// <returns></returns>
        private static decimal[] SetMatch(List<Es> es, GpType gpType, int specials, bool isSameCount, out bool sourceHaveRf, out Dictionary<string, List<PlateInfo>> allEsDic)
        {
            //設定資料初始化{RT, RF_PK, RF_H_PL, RF_A_PL, OU_PK, OU_O_PL, OU_U_PL, 1*2_H_PL, 1*2_A_PL, OE_O_PL, OE_E_PL, 1*2_D_PL}
            decimal[] newData = new decimal[12];
            allEsDic = new Dictionary<string, List<PlateInfo>>();

            #region RF

            var leftRfEs = es.Where(a => a.T == 7);
            var rightRfEs = es.Where(a => a.T == 8);
            //A、B PL交集
            var joinRfEs = leftRfEs.Join(rightRfEs, (a) => a.P, (b) => -b.P, (a, b) => new { Plate = a.P, PlA = a.CV, PlB = b.CV, Difference = Math.Abs(a.CV - b.CV) });
            sourceHaveRf = joinRfEs.Any();

            //全場RF不抓0.5(勾上【只跟PK】需判斷0.5是否最平水)
            if (specials == 0 && !(gpType == GpType.Zd && Config.isZdOnlyPK))
            {
                joinRfEs = joinRfEs.Where(a => Math.Abs(a.Plate) != 0.5m);
            }

            //DS上半單節RF PK  = 0，需額外抓到DY
            if (gpType != GpType.Zd && (specials == 1 || (specials >= 3 && specials <= 6)))
            {
                var DsHtDjEs = joinRfEs.FirstOrDefault(a => a.Plate == 0m);
                joinRfEs = joinRfEs.Where(a => a.Plate != 0m);

                if (DsHtDjEs != null)
                {
                    newData[7] = decimal.Floor(DsHtDjEs.PlA * 100m) / 100m;
                    newData[8] = decimal.Floor(DsHtDjEs.PlB * 100m) / 100m;
                }
            }

            //ZD抓最靠近平水的輸盤或平手盤，補充：全場ZD不抓取0輸盤。
            //DS抓最靠近平水的PK
            var filterRfEs = gpType == GpType.Zd ?
                joinRfEs.Where(a => a.Plate == 0 || Math.Truncate(a.Plate) != a.Plate).OrderBy(a => a.Difference).FirstOrDefault() :
                joinRfEs.OrderBy(a => a.Difference).FirstOrDefault();

            if (filterRfEs != null)
            {
                newData[0] = filterRfEs.Plate <= 0 ? 1 : 0;
                newData[1] = Math.Abs(filterRfEs.Plate);
                newData[2] = decimal.Floor(filterRfEs.PlA * 100m) / 100m;
                newData[3] = decimal.Floor(filterRfEs.PlB * 100m) / 100m;
            }

            #endregion

            #region OU

            var leftOuEs = es.Where(a => a.T == 9);
            var rightOuEs = es.Where(a => a.T == 10);
            //A、B PL交集
            var joinOuEs = leftOuEs.Join(rightOuEs, (a) => a.P, (b) => b.P, (a, b) => new { Plate = a.P, PlA = a.CV, PlB = b.CV, Difference = Math.Abs(a.CV - b.CV) });
            //ZD抓最靠近平水的輸盤
            //DS抓最靠近平水
            var filterOuEs = gpType == GpType.Zd ?
                joinOuEs.Where(a => Math.Truncate(a.Plate) != a.Plate).OrderBy(a => a.Difference).FirstOrDefault() :
                joinOuEs.OrderBy(a => a.Difference).FirstOrDefault();

            if (filterOuEs != null)
            {
                newData[4] = Math.Abs(filterOuEs.Plate);
                newData[5] = decimal.Floor(filterOuEs.PlA * 100m) / 100m;
                newData[6] = decimal.Floor(filterOuEs.PlB * 100m) / 100m;
            }

            #endregion

            #region ML

            var leftMlEs = es.Where(a => a.T == 1 || a.T == 401);
            var rightMlEs = es.Where(a => a.T == 3 || a.T == 402);
            var havaDraw = es.Any(a => a.T == 2 && a.CV != 0);
            var joinMlEs = leftMlEs.Join(rightMlEs, (a) => a.P, (b) => b.P, (a, b) => new { PlA = a.CV, PlB = b.CV }).FirstOrDefault();

            if (joinMlEs != null && !havaDraw && newData[7] == 0 && newData[8] == 0)
            {
                newData[7] = decimal.Floor(joinMlEs.PlA * 100m) / 100m;
                newData[8] = decimal.Floor(joinMlEs.PlB * 100m) / 100m;
            }

            #endregion

            #region OE

            var leftOeEs = es.Where(a => a.T == 183);
            var rightOeEs = es.Where(a => a.T == 182);
            var joinOeEs = leftOeEs.Join(rightOeEs, (a) => a.P, (b) => b.P, (a, b) => new { PlA = a.CV, PlB = b.CV }).FirstOrDefault();

            if (joinOeEs != null)
            {
                newData[9] = decimal.Floor(joinOeEs.PlA * 100m) / 100m;
                newData[10] = decimal.Floor(joinOeEs.PlB * 100m) / 100m;
            }

            #endregion

            //鴛鴦盤整理
            if (isSameCount)
            {
                if (joinRfEs.Any())
                {
                    List<PlateInfo> tmp = new List<PlateInfo>();

                    foreach (var rfEs in joinRfEs)
                    {
                        tmp.Add(new PlateInfo(rfEs.Plate, rfEs.PlA, rfEs.PlB));
                    }

                    allEsDic.Add("rf", tmp);
                }

                if (joinOuEs.Any())
                {
                    List<PlateInfo> tmp = new List<PlateInfo>();

                    foreach (var ouEs in joinOuEs)
                    {
                        tmp.Add(new PlateInfo(ouEs.Plate, ouEs.PlA, ouEs.PlB));
                    }

                    allEsDic.Add("ou", tmp);
                }
            }

            return newData;
        }

        /// <summary>
        /// 設定玩法(總得分)
        /// </summary>
        /// <param name="es">玩法矩陣</param>
        /// <param name="isTeamA">單隊玩法是否為A隊</param>
        /// <param name="gpType">GP類型</param>
        /// <returns></returns>
        private static decimal[] SetTotalMatchOdds(List<Es> es, bool isTeamA, GpType gpType)
        {
            //設定資料初始化{主OU_PK, 主OU_A_PL, 主OU_B_PL }
            decimal[] newData = new decimal[3];
            int leftT = isTeamA ? 11 : 13;
            int rightT = isTeamA ? 12 : 14;

            #region OU

            var leftOuEs = es.Where(a => a.T == leftT);
            var rightOuEs = es.Where(a => a.T == rightT);
            //A、B PL交集
            var joinOuEs = leftOuEs.Join(rightOuEs, (a) => a.P, (b) => b.P, (a, b) => new { Plate = a.P, PlA = a.CV, PlB = b.CV, Difference = Math.Abs(a.CV - b.CV) });
            //ZD抓最靠近平水的輸盤
            //DS抓最靠近平水
            var filterOuEs = gpType == GpType.Zd ?
                joinOuEs.Where(a => Math.Truncate(a.Plate) != a.Plate).OrderBy(a => a.Difference).FirstOrDefault() :
                joinOuEs.OrderBy(a => a.Difference).FirstOrDefault();

            if (filterOuEs != null)
            {
                newData[0] = Math.Abs(filterOuEs.Plate);
                newData[1] = decimal.Floor(filterOuEs.PlA * 100m) / 100m;
                newData[2] = decimal.Floor(filterOuEs.PlB * 100m) / 100m;
            }

            #endregion

            return newData;
        }

        /// <summary>
        /// 設定優先獲得N分玩法整理
        /// </summary>
        /// <param name="es">玩法矩陣</param>
        /// <param name="leftTNum">來源左邊PL代號</param>
        /// <param name="rightTNum">來源右邊PL代號</param
        /// <param name="setType">抓取第N分玩法</param>
        /// <param name="isNBA">是否為NBA</param>
        /// <returns></returns>
        private static decimal[] SetFirstNScoresOdds(List<Es> es, int leftTNum, int rightTNum, byte setType, bool isNBA)
        {
            //設定資料初始化{RT, RF_PK, RF_H_PL, RF_A_PL}
            decimal[] newData = new decimal[4];

            #region RF

            var leftRfEs = es.FirstOrDefault(a => a.T == leftTNum && a.P == setType);
            var rightRfEs = es.FirstOrDefault(a => a.T == rightTNum && a.P == setType);

            if (leftRfEs != null && rightRfEs != null)
            {
                newData[0] = isNBA ? 1 : 0;  //因為是NBA後續交換隊伍，會改成給0
                newData[1] = 0;
                newData[2] = decimal.Floor(leftRfEs.CV * 100m) / 100m;
                newData[3] = decimal.Floor(rightRfEs.CV * 100m) / 100m;
            }

            #endregion

            return newData;
        }

        /// <summary>
        /// 設定玩法(特殊玩法)
        /// </summary>
        /// <param name="es">玩法矩陣</param>
        /// <param name="leftTNum">來源左邊PL代號</param>
        /// <param name="rightTNum">來源右邊PL代號</param>
        /// <returns></returns>
        private static decimal[] SetSpecialMatchOdds(List<Es> es, int leftTNum, int rightTNum)
        {
            //設定資料初始化{RT, RF_PK, RF_H_PL, RF_A_PL}
            decimal[] newData = new decimal[4];

            #region RF

            var leftRfEs = es.Where(a => a.T == leftTNum);
            var rightRfEs = es.Where(a => a.T == rightTNum);
            //A、B PL交集
            var joinRfEs = leftRfEs.Join(rightRfEs, (a) => a.P, (b) => -b.P, (a, b) => new { Plate = a.P, PlA = a.CV, PlB = b.CV, Difference = Math.Abs(a.CV - b.CV) });
            //抓最靠近平水的PK
            var filterRfEs = joinRfEs.OrderBy(a => a.Difference).FirstOrDefault();

            if (filterRfEs != null)
            {
                newData[0] = filterRfEs.Plate <= 0 ? 1 : 0;
                newData[1] = Math.Abs(filterRfEs.Plate);
                newData[2] = decimal.Floor(filterRfEs.PlA * 100m) / 100m;
                newData[3] = decimal.Floor(filterRfEs.PlB * 100m) / 100m;
            }

            #endregion

            return newData;
        }

        /// <summary>
        /// 設定Special
        /// </summary>
        /// <param name="sg">來源SG格式</param>
        /// <param name="specials">輸出對應Specials</param>
        /// <returns></returns>
        private static bool SetLastNum(SGs sg, out int specials)
        {
            specials = -1;

            if (sg.P == null)
            {
                if (sg.V != null) { specials = 0; }
                if (!string.IsNullOrEmpty(sg.TG) && sg.TG.Equals("三分地带进球得分")) { specials = 13; }
                if (!string.IsNullOrEmpty(sg.TG) && sg.TG.Equals("罚球得分")) { specials = 161; }
                if (!string.IsNullOrEmpty(sg.TG) && sg.TG.Equals("篮板")) { specials = 32; }
                if (!string.IsNullOrEmpty(sg.TG) && sg.TG.Equals("助攻")) { specials = 31; }
            }
            else
            {
                //排除不抓玩法
                if (!string.IsNullOrEmpty(sg.TG))
                {
                    specials = -1;
                }
                else
                {
                    specials = GetSpecial(sg.P);
                }
            }

            return specials > 0;
        }

        /// <summary>
        /// 取得Special
        /// </summary>
        /// <param name="scene">節次</param>
        /// <param name="playName">特殊玩法名稱</param>
        /// <returns></returns>
        private static int GetSpecial(int? scene)
        {
            switch (scene)
            {
                case 1: return 3;
                case 2: return 4;
                case 3: return 5;
                case 4: return 6;
                case 11: return 1;
                case 12: return 2;
            }

            return -1;
        }

        /// <summary>
        /// 處理鴛鴦盤資料
        /// </summary>
        /// <param name="gameInfo">賽事資訊</param>
        /// <param name="allEsDic">來源odds</param>
        /// <param name="specials">玩法specials</param>
        /// <param name="gpType">GpType</param>
        /// <param name="sendId">SendId</param>
        /// <param name="sourceMatchId">SourceMatchId</param>
        /// <param name="isNBA">NBA</param>
        private static void HandleSameCount(GameInfo gameInfo, Dictionary<string, List<PlateInfo>> allEsDic, int specials, GpType gpType, long sendId, decimal sourceMatchId, bool isNBA)
        {
            //初始化
            List<PlateInfo> rfList = new List<PlateInfo>();
            List<PlateInfo> ouList = new List<PlateInfo>();
            PlateInfo rf_a = null;
            PlateInfo ou_a = null;
            int index_a_rf = -1;
            int index_a_ou = -1;

            if (allEsDic.ContainsKey("rf"))
            {
                rfList = allEsDic["rf"].OrderBy(x => x.Plate).ToList();
                rf_a = rfList.OrderBy(x => Math.Abs(x.Ratio_A - x.Ratio_H)).FirstOrDefault();
                index_a_rf = rf_a != null ? rfList.IndexOf(rf_a) : -1;
            }

            if (allEsDic.ContainsKey("ou"))
            {
                ouList = allEsDic["ou"].OrderBy(x => x.Plate).ToList();
                ou_a = ouList.OrderBy(x => Math.Abs(x.Ratio_A - x.Ratio_H)).FirstOrDefault();
                index_a_ou = ou_a != null ? ouList.IndexOf(ou_a) : -1;
            }

            int sameCount = Config.sameCount == 2 ? 3 : 5;
            //存放抓取鴛鴦盤(存放BCDE盤)
            PlateInfo[] plateInfos_rf = new PlateInfo[4];
            PlateInfo[] plateInfos_ou = new PlateInfo[4];
            //判斷是否全好全壞
            bool isAllBacdGood_rf = index_a_rf == 0 || index_a_rf == rfList.Count - 1;
            bool isAllBacdGood_ou = index_a_ou == 0 || index_a_ou == ouList.Count - 1;
            //存放全好全壞固定方向抓法
            bool[] isMinus = new bool[2];

            //整理排序鴛鴦盤要抓的鴛鴦盤
            for (int i = 1; i < sameCount; i++)
            {
                //rf
                Config.ArrangedNbaDsFtHtSameCountOdds(rfList, rf_a, 1, i, sameCount, isAllBacdGood_rf, ref plateInfos_rf, ref isMinus[0]);
                //ou
                Config.ArrangedNbaDsFtHtSameCountOdds(ouList, ou_a, 2, i, sameCount, isAllBacdGood_ou, ref plateInfos_ou, ref isMinus[1]);
            }

            //排除為null，遞補邏輯
            plateInfos_rf = plateInfos_rf.Where(x => x != null).ToArray();
            plateInfos_ou = plateInfos_ou.Where(x => x != null).ToArray();

            //給pkpl
            for (int i = 1; i < sameCount; i++)
            {
                PlateInfo rfPlateInfo = plateInfos_rf.Length > i - 1 ? plateInfos_rf[i - 1] : null;
                PlateInfo ouPlateInfo = plateInfos_ou.Length > i - 1 ? plateInfos_ou[i - 1] : null;

                //rf
                if (Config.SetKindType(gameInfo.GpType, specials, 1, out int rfkType))
                {
                    byte setType = gameInfo.GetSetType(rfkType);
                    //生成物件
                    KInfo kInfo = new KInfo(gpType.ToString(), sendId, rfkType, setType, sourceMatchId);
                    kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;

                    BInfo bInfoA = new BInfo(Config.GetBType(rfkType, setType, 1));
                    BInfo bInfoB = new BInfo(Config.GetBType(rfkType, setType, 2));

                    if (rfPlateInfo != null)
                    {
                        //PK,PL
                        decimal plate = Math.Abs(rfPlateInfo.Plate);
                        decimal odds_A = decimal.Floor(rfPlateInfo.Ratio_H * 100m) / 100m;
                        decimal odds_B = decimal.Floor(rfPlateInfo.Ratio_A * 100m) / 100m;
                        ConvertTools.ExchangeValue(ref odds_A, ref odds_B, isNBA);
                        //RT[1:A隊讓 0:B隊讓]
                        int strong = rfPlateInfo.Plate < 0 ? 1 : 0;
                        if (isNBA) { strong = strong == 1 ? 0 : 1; }

                        //設置數據
                        kInfo.SetLine(plate, strong);
                        bInfoA.SetOdds(odds_A);
                        bInfoB.SetOdds(odds_B);
                        kInfo.SetIsOpen();
                    }

                    //放進字典
                    kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                    kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                    //生成gpdm並新增對應字典
                    decimal gpdm = specials * 100 + setType;
                    gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                    gameInfo.ObjDic[gpdm].TryAdd(rfkType, kInfo);
                }

                //ou
                if (Config.SetKindType(gameInfo.GpType, specials, 2, out int oukType))
                {
                    byte setType = gameInfo.GetSetType(oukType);
                    //生成物件
                    KInfo kInfo = new KInfo(gpType.ToString(), sendId, oukType, setType, sourceMatchId);
                    kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;

                    BInfo bInfoA = new BInfo(Config.GetBType(oukType, setType, 1));
                    BInfo bInfoB = new BInfo(Config.GetBType(oukType, setType, 2));

                    if (ouPlateInfo != null)
                    {
                        //PK,PL
                        decimal plate = Math.Abs(ouPlateInfo.Plate);
                        decimal odds_A = decimal.Floor(ouPlateInfo.Ratio_H * 100m) / 100m;
                        decimal odds_B = decimal.Floor(ouPlateInfo.Ratio_A * 100m) / 100m;

                        kInfo.SetLine(plate);
                        bInfoA.SetOdds(odds_A);
                        bInfoB.SetOdds(odds_B);
                        kInfo.SetIsOpen();
                    }

                    //放進字典
                    kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                    kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                    //生成gpdm並新增對應字典
                    decimal gpdm = specials * 100 + setType;
                    gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                    gameInfo.ObjDic[gpdm].TryAdd(oukType, kInfo);
                }
            }
        }
    }
}
