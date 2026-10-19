#region 功能修改描述與時間

/*
    描述：SB解析器
    日期：2020-06-30

    描述：調整夢幻配對從聯盟名移至後綴
    日期：2021-05-03

    描述：調整轉換比分避免為負值
    日期：2021-05-12
                        
    描述：調整ZD開賽時間有查詢過才使用
    日期：2021-06-29

    描述：添加NBA總冠軍賽後綴正則
    日期：2021-07-06

    描述：單節比分解析添加防護
    日期：2021-08-13

    描述：調整賽事狀態解析
    日期：2021-09-23

    描述：NBA以外聯盟不處理後綴，皆當主名稱
    日期：2021-11-23

    描述：排除虛擬賽事
    日期：2022-03-17

    描述：來源聯盟名稱-前後須有空白才認定為玩法，不解析
    日期：2022-11-25
                        
    描述：調整NBA隊伍主客位置(上客下主)
    日期：2023-03-16
*/

#endregion

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Yw_BK.model;
using YwCore;

namespace Yw_BK
{
    /// <summary>
    /// SB解析器
    /// </summary>
    public static class ParserSB
    {
        //正規式(匹配2個空格以上)
        private static Regex regex = new Regex(@"[ ]{2,}", RegexOptions.None);
        //新訂閱指令含水
        private static int[] betTypeIncluding = new int[] { 461, 462, 463, 464, 651, 652 };

        /// <summary>
        /// SB解析
        /// </summary>
        /// <param name="dataInfo">數據</param>
        public static void Parser(DataInfo dataInfo, CommonSB commonSB)
        {
            try
            {
                //暫存賽事字典
                ConcurrentDictionary<string, GameInfo> tmpGameDic = new ConcurrentDictionary<string, GameInfo>();
                //GP類型
                if (!Enum.TryParse(dataInfo.StrType, out GpType gpType)) { return; }
                //訂閱類型
                SubscribeType sType = gpType == GpType.Zd ? SubscribeType.Zd : gpType == GpType.Ds ? SubscribeType.Ds : SubscribeType.Zp;
                //取得SB推送資料物件
                List<MatchInfo> matchList = commonSB.FormatDataSB.DataToModel(dataInfo.Data, sType, out bool isComplete);
                //無數據跳過
                if (matchList == null) { return; }
                //刪除賽事列表
                List<decimal> delList = new List<decimal>();

                foreach (MatchInfo matchInfo in matchList)
                {
                    GameInfo gameInfo = new GameInfo(matchInfo.MatchId.ToString());

                    #region 賽事資訊

                    //GP類型
                    GpType tmpGpType = dataInfo.StrType == GpType.Zd.ToString() ? GpType.Zd : (matchInfo.MarketId == "T" || matchInfo.MarketId == "ET") ? GpType.Ds : GpType.Zp;
                    //類型不同則跳過
                    if (tmpGpType != gpType) { continue; }

                    gameInfo.GpType = tmpGpType;
                    //賽事類型
                    byte gType = (byte)(gpType == GpType.Zd ? GType.BkZd : GType.BkDs);
                    gameInfo.GType = gType;
                    //是否為NBA
                    bool isNBA = false;
                    //聯盟名稱 刪除前綴*, 連續空格替換單空格
                    string league = regex.Replace(matchInfo.LeagueNameCn, " ").TrimStart('*').Trim();

                    // 2022/3/16提出虛擬賽事不跟，2022/10/05提出沙巴篮球台不跟
                    if (league.Contains("虚拟") ||
                        league.Contains("沙巴篮球台"))
                    { continue; }

                    //固定對應玩法
                    int fixedSpecials = 0;
                    //固定setType值
                    byte fixedSetType = 0;
                    //是否為明星賽特殊玩法
                    bool isAllStarSpecial = false;

                    #region 聯盟後綴處理

                    //NBA需要處理後綴，其他籃球直接抓整串來源的"聯盟名稱"
                    //使用英文判斷是否為NBA(比照TSGP)
                    if (matchInfo.LeagueNameEn.Contains("NBA") && !matchInfo.LeagueNameEn.Contains("WNBA") && !Config.CheckNbaExcludeName(matchInfo.LeagueNameEn))
                    {
                        isNBA = true;

                        // 2025.2.17 全明星赛 - 扣篮大赛 All Star Game - Slam Dunk Contest 全明星赛 - 三分球大赛 All Star Game - Three-Point Contest 全明星赛 - 技巧挑战赛 All Star Game - Skills Challenge
                        if ((league.Contains("全明星赛 - 扣篮大赛") || matchInfo.LeagueNameEn.IndexOf("All Star Game - Slam Dunk Contest", StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (league.Contains("全明星赛 - 三分球大赛") || matchInfo.LeagueNameEn.IndexOf("All Star Game - Three-Point Contest", StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (league.Contains("全明星赛 - 技巧挑战赛") || matchInfo.LeagueNameEn.IndexOf("All Star Game - Skills Challenge", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            isAllStarSpecial = true;
                        }

                        // 2025.2.17 新秀挑战赛設為聯盟
                        if (league.Contains("全明星赛 - 新秀挑战赛") || matchInfo.LeagueNameEn.IndexOf("All Star Game - Rising Stars Challenge", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            league = league.Replace("全明星赛 - 新秀挑战赛", "全明星赛 新秀挑战赛").Trim();
                        }

                        if (league.Contains(" - "))
                        {
                            var tmp = league.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                            string split = string.Join("-", tmp.Skip(1));

                            // 2024.12.24新增需求抓取 "全場-第1個三分球"
                            if (split.StartsWith("最先得三分球之球队") || matchInfo.LeagueNameEn.IndexOf("First 3-Point Shot Scored", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                fixedSpecials = 261;
                                fixedSetType = 1;
                            }
                            //"全場-三分球總數", "全場-籃板球總數", "全場-搶斷總數"
                            else if (split.StartsWith("三分球总进球数") || (matchInfo.LeagueNameEn.IndexOf("Most 3-Point Shots Scored", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                matchInfo.LeagueNameEn.IndexOf("(Player)", StringComparison.OrdinalIgnoreCase) < 0))
                            {
                                fixedSpecials = 13;
                                fixedSetType = 1;
                            }
                            else if (split.StartsWith("最多篮板") || (matchInfo.LeagueNameEn.IndexOf("Most Rebounds", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                matchInfo.LeagueNameEn.IndexOf("(Player)", StringComparison.OrdinalIgnoreCase) < 0))
                            {
                                fixedSpecials = 32;
                                fixedSetType = 1;
                            }
                            else if (split.StartsWith("最多抢断") || (matchInfo.LeagueNameEn.IndexOf("Most Steals", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                matchInfo.LeagueNameEn.IndexOf("(Player)", StringComparison.OrdinalIgnoreCase) < 0))
                            {
                                fixedSpecials = 33;
                                fixedSetType = 1;
                            }

                            if (fixedSpecials > 0)
                            {
                                league = tmp.First();
                            }
                        }
                    }

                    #endregion

                    //設置主客標記
                    gameInfo.Home = isNBA ? 2 : 1;

                    //匹配是否符合替換條件
                    KeyValuePair<string, string> replacePair = YwCore.Common.leagueReplaceDic.FirstOrDefault(a => league.Contains(a.Key));

                    if (!string.IsNullOrEmpty(replacePair.Key))
                    {
                        gameInfo.League.Name = league.Replace(replacePair.Key, replacePair.Value).Trim();
                    }
                    else
                    {
                        gameInfo.League.Name = league;
                    }

                    if (Config.FilterLeagueHashSet.Any(filter => gameInfo.League.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        continue;
                    }

                    //設定預設後綴
                    if (FrmSuffix.suffixList.TryGetValue(gameInfo.League.Name, out int suffixId) && suffixId != 0)
                    {
                        gameInfo.Suffix.Name = WebName.suffixDic[suffixId].Cn;
                        gameInfo.Suffix.FullName = WebName.suffixDic[suffixId].GetFullName();
                        gameInfo.Suffix.Id = suffixId;
                    }

                    //隊伍名稱 刪除前綴*, 連續空格替換單空格
                    string teamA = regex.Replace(matchInfo.HteamNameCn, " ").TrimStart('*').Trim();
                    string teamB = regex.Replace(matchInfo.AteamNameCn, " ").TrimStart('*').Trim();
                    ConvertTools.ExchangeValue(ref teamA, ref teamB, isNBA);
                    gameInfo.TeamA.Name = teamA;
                    gameInfo.TeamB.Name = teamB;

                    //開賽時間
                    gameInfo.SetGameDate(new DateTime(1970, 1, 1).AddSeconds(long.Parse(matchInfo.ShowTimeDt)).AddHours(8));

                    //防止兩隊名稱一樣
                    if (gameInfo.TeamA.Name.Equals(gameInfo.TeamB.Name))
                    {
                        continue;
                    }

                    //ZP用只抓明日賽事(12點前抓帳務日期明天 12點後抓帳務日期後天)
                    if (Config.isTomorrow && gpType == GpType.Zp)
                    {
                        int hour = DateTime.Now.Hour;
                        DateTime zpTDate = hour >= 12 ? DateTime.Now.Date.AddDays(2) : DateTime.Now.Date.AddDays(1);

                        if (gameInfo.PaymentDate > zpTDate)
                        {
                            delList.Add(matchInfo.MatchId);
                            continue;
                        }
                    }

                    //是否為3x3籃球
                    bool is3x3 = Config.Is3x3(gameInfo.League.Name);

                    #endregion

                    #region ZD狀態、比分處理、剩餘時間

                    if (tmpGpType == GpType.Zd)
                    {
                        //賽事進行狀態
                        int status = 0;

                        switch (matchInfo.LivePeriod)
                        {
                            case "0":
                                status = matchInfo.CsStatus.Equals("1") ? 6 : 5;
                                break;
                            case "1":
                                status = (league.Contains("NCAA") && !league.Contains("女子")) ? 11 : 1;
                                break;
                            case "2":
                                status = (league.Contains("NCAA") && !league.Contains("女子")) ? 12 : 2;
                                break;
                            case "3":
                                status = 3;
                                break;
                            case "4":
                                status = 4;
                                break;
                            case "99":
                                status = 13;
                                break;
                            default:
                                status = 5;
                                break;
                        }

                        gameInfo.Status = is3x3 ? 1 : status;

                        if (byte.TryParse(matchInfo.LiveHomeScore.ToString(), out byte scoreA) &&
                            byte.TryParse(matchInfo.LiveAwayScore.ToString(), out byte scoreB))
                        {
                            ConvertTools.ExchangeValue(ref scoreA, ref scoreB, isNBA);
                            gameInfo.Score.SetScore(true, scoreA, scoreB);
                        }

                        if (matchInfo.Live_Score != null)
                        {
                            if (league.Contains("NCAA") && !league.Contains("女子"))
                            {
                                gameInfo.ScoreHalf1.SetScore(ParserScore(matchInfo.Live_Score.h1q, matchInfo.Live_Score.a1q, out byte score1A, out byte score1B), score1A, score1B);
                                gameInfo.ScoreHalf2.SetScore(ParserScore(matchInfo.Live_Score.h2q, matchInfo.Live_Score.a2q, out byte score2A, out byte score2B), score2A, score2B);

                            }
                            else
                            {
                                gameInfo.Score1.SetScore(ParserScore(matchInfo.Live_Score.h1q, matchInfo.Live_Score.a1q, out byte score1A, out byte score1B, isNBA), score1A, score1B);
                                gameInfo.Score2.SetScore(ParserScore(matchInfo.Live_Score.h2q, matchInfo.Live_Score.a2q, out byte score2A, out byte score2B, isNBA), score2A, score2B);
                                gameInfo.Score3.SetScore(ParserScore(matchInfo.Live_Score.h3q, matchInfo.Live_Score.a3q, out byte score3A, out byte score3B, isNBA), score3A, score3B);
                                gameInfo.Score4.SetScore(ParserScore(matchInfo.Live_Score.h4q, matchInfo.Live_Score.a4q, out byte score4A, out byte score4B, isNBA), score4A, score4B);

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

                        DateTime datetime = new DateTime(1970, 1, 1).AddSeconds(long.Parse(matchInfo.LiveTimer)).AddHours(8);
                        decimal inplayTimer = 0;

                        if (!string.IsNullOrEmpty(matchInfo.SessionTime) && !string.IsNullOrEmpty(matchInfo.PausePeriod)
                            && decimal.TryParse((DateTime.Now - datetime).TotalSeconds.ToString("F0"), out decimal ts)
                            && decimal.TryParse(matchInfo.PausePeriod, out decimal pausePeriod)
                            && decimal.TryParse(matchInfo.SessionTime, out decimal sessionTime))
                        {
                            //賽制時間(秒)
                            sessionTime = sessionTime * 60;

                            //來源暫停倒數
                            if (pausePeriod < 0)
                            {
                                inplayTimer = sessionTime + pausePeriod;
                            }
                            //來源正在倒數
                            else if (pausePeriod == 0)
                            {
                                inplayTimer = sessionTime - ts;
                            }
                            //中場休息
                            else if (pausePeriod > 0)
                            {
                                inplayTimer = sessionTime;
                            }

                            gameInfo.LastSec = (int)(inplayTimer > 0 ? inplayTimer : 0);//防止來源為負值
                        }
                    }

                    #endregion

                    #region 添加至字典或取得對應賽事

                    string key = gameInfo.GetGameDictKey();

                    if (tmpGameDic.TryGetValue(key, out GameInfo tmpGame))
                    {
                        gameInfo = tmpGame;
                    }
                    else
                    {
                        tmpGameDic.TryAdd(key, gameInfo);
                    }

                    #endregion

                    #region 解析玩法

                    //全場上半的RF OU BetType
                    int[] ftHtRfOu = new int[] { 1, 3, 7, 8 };

                    //玩法排序(DS先排betTypey再排sort  ZD先排betType再排PL差距)
                    var oddsSort = gpType == GpType.Zd ? matchInfo.OddsDic.OrderBy(a => a.Value.Bettype).ThenBy(b => Math.Abs(ChangeRatio(b.Value.Odds1a) - ChangeRatio(b.Value.Odds2a))).ThenBy(c => c.Value.Sort) :
                        matchInfo.OddsDic.OrderBy(a => a.Value.Bettype).ThenBy(b =>
                        {
                            //DS 的全上RF OU A盤需要抓最平水
                            if (ftHtRfOu.Contains(b.Value.Bettype))
                            {
                                return Math.Abs(ChangeRatio(b.Value.Odds1a) - ChangeRatio(b.Value.Odds2a));
                            }
                            else
                            {
                                return b.Value.Sort;
                            }
                        });

                    HashSet<decimal> sourceHaveRf = new HashSet<decimal>();

                    //額外儲存Ds需要抓鴛鴦盤使用 Key: Tuple(specials, 玩法type)/ Values: List<PlateInfo>
                    Dictionary<Tuple<int, int>, List<PlateInfo>> cdDsSameCountPlateInfos = new Dictionary<Tuple<int, int>, List<PlateInfo>>();

                    for (int i = 0; i < oddsSort.Count(); i++)
                    {
                        OddsInfo oddsInfo = oddsSort.ElementAt(i).Value;

                        int specials = 0;
                        int tmpSetType = 0;

                        #region 區分場次(全場或上半)

                        if (fixedSpecials == 0)
                        {
                            switch (oddsInfo.Bettype)
                            {
                                case 7:
                                case 8:
                                case 12:
                                case 21:
                                    //上半
                                    specials = 1;
                                    break;
                                case 609:
                                case 610:
                                case 611:
                                case 612:
                                    if (!GetStatus(oddsInfo.Resourceid, out specials)) { continue; }
                                    break;
                                case 401:
                                case 461:
                                    //全場主隊總得分
                                    specials = Config.TeamTotalIsChangeSpecials(14, isNBA);
                                    break;
                                case 402:
                                case 462:
                                    //全場客隊總得分
                                    specials = Config.TeamTotalIsChangeSpecials(15, isNBA);
                                    break;
                                case 403:
                                case 463:
                                    //上半主隊總得分
                                    specials = Config.TeamTotalIsChangeSpecials(16, isNBA);
                                    break;
                                case 404:
                                case 464:
                                    //上半客隊總得分
                                    specials = Config.TeamTotalIsChangeSpecials(17, isNBA);
                                    break;
                                case 615:
                                case 651:
                                    //單節主隊總得分
                                    if (!GetStatus(isNBA ? false : true, oddsInfo.Resourceid, out specials)) { continue; }
                                    break;
                                case 616:
                                case 652:
                                    //單節客隊總得分
                                    if (!GetStatus(isNBA ? true : false, oddsInfo.Resourceid, out specials)) { continue; }
                                    break;
                                case 606:
                                    //下半首先獲得N分
                                    if (int.TryParse(oddsInfo.Resourceid, out int value))
                                    {
                                        specials = 102;
                                        tmpSetType = value;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                    break;
                                case 607:
                                    //上半首先獲得N分
                                    if (int.TryParse(oddsInfo.Resourceid, out value))
                                    {
                                        specials = 103;
                                        tmpSetType = value;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                    break;
                                case 613:
                                    //單節首先獲得N分
                                    if (!GetStatusFirst(oddsInfo.Resourceid, out specials, out tmpSetType)) { continue; }
                                    break;
                                case 604:
                                    //搶首
                                    if (isNBA) { specials = 60; }
                                    else { continue; }
                                    break;
                                case 605:
                                    //搶尾
                                    if (isNBA) { specials = 70; }
                                    else { continue; }
                                    break;
                                default:
                                    specials = 0;
                                    break;
                            }
                        }
                        else
                        {
                            specials = fixedSpecials;
                            tmpSetType = fixedSetType;
                        }

                        #endregion

                        switch (oddsInfo.Bettype)
                        {
                            case 1:
                            case 7:
                            case 604:
                            case 605:
                            case 606:
                            case 607:
                            case 609:
                            case 613:
                                #region RF

                                if (Config.SetKindType(gpType, specials, 1, out int kTypeRf))
                                {
                                    byte setType = tmpSetType == 0 ? gameInfo.GetSetType(kTypeRf) : (byte)tmpSetType;
                                    //生成gpdm並新增對應字典
                                    decimal gpdm = specials * 100 + setType;
                                    if (oddsInfo.GetOpenStatus()) { sourceHaveRf.Add(gpdm); }

                                    //PK解析
                                    decimal.TryParse(oddsInfo.Hdp1, out decimal hdp1);
                                    decimal.TryParse(oddsInfo.Hdp2, out decimal hdp2);
                                    //RT[1:A隊讓 0:B隊讓]
                                    int strong = hdp2 > 0m ? 0 : 1;
                                    if (isNBA) { strong = strong == 1 ? 0 : 1; }
                                    //PK,PL
                                    decimal plate = hdp2 > 0m ? hdp2 : hdp1;

                                    //Ds全場上半額外儲存，額外解析
                                    if (gpType != GpType.Zd && ftHtRfOu.Contains(oddsInfo.Bettype))
                                    {
                                        decimal tmpPk = strong == 1 ? plate : -1 * plate;
                                        if (!isNBA) { tmpPk = -1 * tmpPk; }
                                        //加入到字典內
                                        Tuple<int, int> tmpKey = new Tuple<int, int>(specials, 1);
                                        cdDsSameCountPlateInfos.TryAdd(tmpKey, new List<PlateInfo>());
                                        cdDsSameCountPlateInfos[tmpKey].Add(new PlateInfo(tmpPk, decimal.Floor(ChangeRatio(oddsInfo.Odds1a) * 100m) / 100m, decimal.Floor(ChangeRatio(oddsInfo.Odds2a) * 100m) / 100m));

                                        continue;
                                    }

                                    //ZD限定setType只能有一個(ZD只抓輸贏或平手最接近平水的一盤)
                                    if (gpType != GpType.Zd || (setType == 1 && (plate != Math.Truncate(plate) || plate == 0)))
                                    {
                                        //生成物件
                                        KInfo kInfo = new KInfo(gpType.ToString(), 0, kTypeRf, setType);
                                        kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                                        BInfo bInfoA = new BInfo(Config.GetBType(kTypeRf, setType, 1));
                                        BInfo bInfoB = new BInfo(Config.GetBType(kTypeRf, setType, 2));

                                        //開放才解析
                                        if (oddsInfo.GetOpenStatus())
                                        {
                                            decimal odds_A = decimal.Floor(ChangeRatio(oddsInfo.Odds1a) * 100m) / 100m;
                                            decimal odds_B = decimal.Floor(ChangeRatio(oddsInfo.Odds2a) * 100m) / 100m;
                                            ConvertTools.ExchangeValue(ref odds_A, ref odds_B, isNBA);

                                            //設置數據
                                            kInfo.SetLine(plate, strong);
                                            bInfoA.SetOdds(odds_A);
                                            bInfoB.SetOdds(odds_B);
                                            kInfo.SetIsOpen();
                                        }

                                        //存放SB來源賽事編號
                                        kInfo.SB_MatchId = matchInfo.MatchId;
                                        //添加字典
                                        gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                        //放進字典
                                        kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                        kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                        gameInfo.ObjDic[gpdm].TryAdd(kTypeRf, kInfo);
                                    }
                                }

                                #endregion
                                break;
                            case 3:
                            case 8:
                            case 610:
                                #region OU

                                if (Config.SetKindType(gpType, specials, 2, out int kTypeOu))
                                {
                                    byte setType = gameInfo.GetSetType(kTypeOu);
                                    //PK解析
                                    decimal.TryParse(oddsInfo.Hdp1, out decimal plate);

                                    //Ds全場上半額外儲存，額外解析
                                    if (gpType != GpType.Zd && ftHtRfOu.Contains(oddsInfo.Bettype))
                                    {
                                        //加入到字典內
                                        Tuple<int, int> tmpKey = new Tuple<int, int>(specials, 2);
                                        cdDsSameCountPlateInfos.TryAdd(tmpKey, new List<PlateInfo>());
                                        cdDsSameCountPlateInfos[tmpKey].Add(new PlateInfo(plate, decimal.Floor(ChangeRatio(oddsInfo.Odds1a) * 100m) / 100m, decimal.Floor(ChangeRatio(oddsInfo.Odds2a) * 100m) / 100m));

                                        continue;
                                    }

                                    //ZD限定setType只能有一個(ZD只抓輸贏最接近平水的一盤)
                                    if (gpType != GpType.Zd || (setType == 1 && plate != Math.Truncate(plate)))
                                    {
                                        //生成物件
                                        KInfo kInfo = new KInfo(gpType.ToString(), 0, kTypeOu, setType);
                                        kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                                        BInfo bInfoO = new BInfo(Config.GetBType(kTypeOu, setType, 1));
                                        BInfo bInfoU = new BInfo(Config.GetBType(kTypeOu, setType, 2));

                                        //開放才解析
                                        if (oddsInfo.GetOpenStatus())
                                        {
                                            //PK,PL
                                            decimal odds_O = decimal.Floor(ChangeRatio(oddsInfo.Odds1a) * 100m) / 100m;
                                            decimal odds_U = decimal.Floor(ChangeRatio(oddsInfo.Odds2a) * 100m) / 100m;
                                            //設置數據
                                            kInfo.SetLine(plate);
                                            bInfoO.SetOdds(odds_O);
                                            bInfoU.SetOdds(odds_U);
                                            kInfo.SetIsOpen();
                                        }

                                        //存放SB來源賽事編號
                                        kInfo.SB_MatchId = matchInfo.MatchId;
                                        //生成gpdm並新增對應字典
                                        decimal gpdm = specials * 100 + setType;
                                        //添加字典
                                        gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                        //放進字典
                                        kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                        kInfo.BInfoDic.TryAdd(bInfoU.BType, bInfoU);
                                        gameInfo.ObjDic[gpdm].TryAdd(kTypeOu, kInfo);
                                    }
                                }

                                #endregion
                                break;
                            case 20:
                            case 21:
                            case 612:
                                #region DY

                                //三分球,籃板球,搶斷玩法(specials == 261,32,33)、 全明星賽特殊玩法 需DY轉RF
                                int tmpType = (gameInfo.GpType == GpType.Zd || new[] { 261, 32, 33 }.Contains(fixedSpecials) || isAllStarSpecial) ? 1 : 3;

                                if (Config.SetKindType(gpType, specials, tmpType, out int kTypeDy))
                                {
                                    byte setType = 1;

                                    //生成物件
                                    KInfo kInfo = new KInfo(gpType.ToString(), 0, kTypeDy, setType);
                                    BInfo bInfoA = new BInfo(Config.GetBType(kTypeDy, setType, 1));
                                    BInfo bInfoB = new BInfo(Config.GetBType(kTypeDy, setType, 2));
                                    //生成gpdm並新增對應字典
                                    decimal gpdm = specials * 100 + setType;

                                    //開放才解析
                                    if (oddsInfo.GetOpenStatus())
                                    {
                                        //PL
                                        decimal odds_A = decimal.Floor(ChangeRatio(oddsInfo.Odds1a) * 100m) / 100m;
                                        decimal odds_B = decimal.Floor(ChangeRatio(oddsInfo.Odds2a) * 100m) / 100m;
                                        ConvertTools.ExchangeValue(ref odds_A, ref odds_B, isNBA);
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
                                                    //存放SB來源賽事編號
                                                    kInfo.SB_MatchId = matchInfo.MatchId;
                                                    gameInfo.ObjDic[gpdm][kTypeDy] = kInfo;
                                                }
                                            }
                                            else if (Config.isZdOnlyPK && sourceHaveRf.Contains(gpdm))
                                            {
                                                kInfo.IsSourceFollow = 1;
                                                //更新kInfo
                                                kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                                kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                                //存放SB來源賽事編號
                                                kInfo.SB_MatchId = matchInfo.MatchId;
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
                                                //存放SB來源賽事編號
                                                kInfo.SB_MatchId = matchInfo.MatchId;
                                                gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                                gameInfo.ObjDic[gpdm][kTypeDy] = kInfo;
                                            }
                                        }
                                    }

                                    if (gameInfo.GpType != GpType.Zd)
                                    {
                                        //存放SB來源賽事編號
                                        kInfo.SB_MatchId = matchInfo.MatchId;
                                        //添加字典
                                        gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                        //放進字典
                                        kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                        kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                        gameInfo.ObjDic[gpdm].TryAdd(kTypeDy, kInfo);
                                    }
                                }

                                #endregion
                                break;
                            case 2:
                            case 12:
                            case 611:
                                #region OE

                                if (Config.SetKindType(gpType, specials, 4, out int kTypeOe) && !isAllStarSpecial)
                                {
                                    byte setType = gameInfo.GetSetType(kTypeOe);

                                    //限定setType只能有一個
                                    if (setType == 1)
                                    {
                                        //生成物件
                                        KInfo kInfo = new KInfo(gpType.ToString(), 0, kTypeOe, setType);
                                        BInfo bInfoO = new BInfo(Config.GetBType(kTypeOe, setType, 1));
                                        BInfo bInfoE = new BInfo(Config.GetBType(kTypeOe, setType, 2));
                                        decimal odds_A;
                                        decimal odds_B;

                                        //手動設置OE (排除 ZD 全場/上下半/單節 主客隊總得分 oe設置)
                                        if ((gameInfo.GpType != GpType.Zd && Config.isOpenDsZpOe) || (gameInfo.GpType == GpType.Zd && Config.isOpenZdOe && (specials < 14 || specials > 29)))
                                        {
                                            if (gameInfo.GpType == GpType.Zd && !is3x3)
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
                                        //開放才解析
                                        else if (oddsInfo.GetOpenStatus() && !(gameInfo.GpType == GpType.Zd && is3x3))
                                        {
                                            odds_A = decimal.Floor(ChangeRatio(oddsInfo.Odds1a) * 100m) / 100m;
                                            odds_B = decimal.Floor(ChangeRatio(oddsInfo.Odds2a) * 100m) / 100m;
                                            bInfoO.SetOdds(odds_A);
                                            bInfoE.SetOdds(odds_B);
                                            kInfo.SetIsOpen();
                                        }

                                        //存放SB來源賽事編號
                                        kInfo.SB_MatchId = matchInfo.MatchId;
                                        //生成gpdm並新增對應字典
                                        decimal gpdm = specials * 100 + setType;
                                        //添加字典
                                        gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                        //放進字典
                                        kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                        kInfo.BInfoDic.TryAdd(bInfoE.BType, bInfoE);
                                        gameInfo.ObjDic[gpdm].TryAdd(kTypeOe, kInfo);
                                    }
                                }

                                #endregion
                                break;
                            case 401:
                            case 461:
                            case 402:
                            case 462:
                            case 403:
                            case 463:
                            case 404:
                            case 464:
                            case 615:
                            case 651:
                            case 616:
                            case 652:
                                #region OU

                                if (Config.SetKindType(gpType, specials, 2, out int kTypeTotalOu))
                                {
                                    byte setType = gameInfo.GetSetType(kTypeTotalOu);
                                    //限定setType只能有一個
                                    if (setType == 1)
                                    {
                                        //生成物件
                                        KInfo kInfo = new KInfo(gpType.ToString(), 0, kTypeTotalOu, setType);
                                        kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                                        BInfo bInfoO = new BInfo(Config.GetBType(kTypeTotalOu, setType, 1));
                                        BInfo bInfoU = new BInfo(Config.GetBType(kTypeTotalOu, setType, 2));
                                        //判斷是否扣水
                                        decimal dif = betTypeIncluding.Contains(oddsInfo.Bettype) ? 1 : 0;

                                        //開放才解析
                                        if (oddsInfo.GetOpenStatus())
                                        {
                                            //PK解析
                                            decimal.TryParse(oddsInfo.Hdp1, out decimal plate);
                                            //PK,PL
                                            decimal odds_O = decimal.Floor(ChangeRatio(oddsInfo.Odds1a) * 100m) / 100m - dif;
                                            decimal odds_U = decimal.Floor(ChangeRatio(oddsInfo.Odds2a) * 100m) / 100m - dif;
                                            //設置數據
                                            kInfo.SetLine(plate);
                                            bInfoO.SetOdds(odds_O);
                                            bInfoU.SetOdds(odds_U);
                                            kInfo.SetIsOpen();
                                        }

                                        //存放SB來源賽事編號
                                        kInfo.SB_MatchId = matchInfo.MatchId;
                                        //生成gpdm並新增對應字典
                                        decimal gpdm = specials * 100 + setType;
                                        //添加字典
                                        gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                        //放進字典
                                        kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                        kInfo.BInfoDic.TryAdd(bInfoU.BType, bInfoU);
                                        gameInfo.ObjDic[gpdm].TryAdd(kTypeTotalOu, kInfo);
                                    }
                                }

                                #endregion

                                #region OE

                                if (Config.SetKindType(gpType, specials, 4, out int kTypeTotalOe))
                                {
                                    byte setType = gameInfo.GetSetType(kTypeTotalOe);

                                    //限定setType只能有一個
                                    if (setType == 1)
                                    {
                                        //生成物件
                                        KInfo kInfo = new KInfo(gpType.ToString(), 0, kTypeTotalOe, setType);
                                        BInfo bInfoO = new BInfo(Config.GetBType(kTypeTotalOe, setType, 1));
                                        BInfo bInfoE = new BInfo(Config.GetBType(kTypeTotalOe, setType, 2));
                                        decimal odds_A;
                                        decimal odds_B;

                                        //手動設置OE (排除 ZD 全場/上下半/單節 主客隊總得分 oe設置)
                                        if (gameInfo.GpType != GpType.Zd)
                                        {
                                            if (Config.isOpenDsZpOe)
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

                                            }
                                            //如未設置固定給0.97
                                            else
                                            {
                                                bInfoO.SetOdds(0.97m);
                                                bInfoE.SetOdds(0.97m);
                                                kInfo.SetIsOpen();
                                            }
                                        }

                                        kInfo.IsManual = true;

                                        //存放SB來源賽事編號
                                        kInfo.SB_MatchId = matchInfo.MatchId;
                                        //生成gpdm並新增對應字典
                                        decimal gpdm = specials * 100 + setType;
                                        //添加字典
                                        gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                        //放進字典
                                        kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                        kInfo.BInfoDic.TryAdd(bInfoE.BType, bInfoE);
                                        gameInfo.ObjDic[gpdm].TryAdd(kTypeTotalOe, kInfo);
                                    }
                                }

                                #endregion
                                break;
                        }
                    }

                    //DS 全場 上半額外處理(包刮處理鴛鴦盤)
                    if (gpType != GpType.Zd && fixedSpecials == 0)
                    {
                        foreach (KeyValuePair<Tuple<int, int>, List<PlateInfo>> kvp in cdDsSameCountPlateInfos)
                        {
                            DsSameCount(kvp.Key.Item1, kvp.Key.Item2, kvp.Value, gameInfo, gpType, matchInfo.MatchId, isNBA);
                        }
                    }

                    #endregion

                    #region 補上玩法缺少的項目

                    foreach (KeyValuePair<decimal, GpdmInfo> tmp in gameInfo.ObjDic.OrderBy(e => e.Key))
                    {
                        decimal gpdm = tmp.Key;
                        GpdmInfo gpdmDic = tmp.Value;

                        if (gpdmDic.Where(a => a.Value.SB_MatchId == matchInfo.MatchId).Any())
                        {
                            //取得specials值
                            int specials = GetSpecials(gpdm);

                            //RF
                            if (Config.SetKindType(gpType, specials, 1, out int kTypeRF) && !gpdmDic.ContainsKey(kTypeRF))
                            {
                                byte setType = gameInfo.GetSetType(kTypeRF);
                                //生成物件
                                KInfo kInfo = new KInfo(gpType.ToString(), 0, kTypeRF, setType);
                                BInfo bInfoA = new BInfo(Config.GetBType(kTypeRF, setType, 1));
                                BInfo bInfoB = new BInfo(Config.GetBType(kTypeRF, setType, 2));
                                //放進字典
                                kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                gameInfo.ObjDic[gpdm].TryAdd(kTypeRF, kInfo);
                            }

                            //OU
                            if (Config.SetKindType(gpType, specials, 2, out int kTypeOU) && !gpdmDic.ContainsKey(kTypeOU))
                            {
                                byte setType = gameInfo.GetSetType(kTypeOU);
                                //生成物件
                                KInfo kInfo = new KInfo(gpType.ToString(), 0, kTypeOU, setType);
                                BInfo bInfoO = new BInfo(Config.GetBType(kTypeOU, setType, 1));
                                BInfo bInfoU = new BInfo(Config.GetBType(kTypeOU, setType, 2));
                                //放進字典
                                kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                kInfo.BInfoDic.TryAdd(bInfoU.BType, bInfoU);
                                gameInfo.ObjDic[gpdm].TryAdd(kTypeOU, kInfo);
                            }

                            //DY
                            if (Config.SetKindType(gpType, specials, 3, out int kTypeDy) && !gpdmDic.ContainsKey(kTypeDy))
                            {
                                byte setType = gameInfo.GetSetType(kTypeDy);

                                //限定setType只能有一個
                                if (setType == 1)
                                {
                                    //生成物件
                                    KInfo kInfo = new KInfo(gpType.ToString(), 0, kTypeDy, setType);
                                    BInfo bInfoA = new BInfo(Config.GetBType(kTypeDy, setType, 1));
                                    BInfo bInfoB = new BInfo(Config.GetBType(kTypeDy, setType, 2));
                                    //放進字典
                                    kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                    kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                    gameInfo.ObjDic[gpdm].TryAdd(kTypeDy, kInfo);
                                }
                            }

                            //OE
                            if (Config.SetKindType(gpType, specials, 4, out int kTypeOE) && !gpdmDic.ContainsKey(kTypeOE))
                            {
                                byte setType = gameInfo.GetSetType(kTypeOE);

                                //限定setType只能有一個
                                if (setType == 1)
                                {
                                    //生成物件
                                    KInfo kInfo = new KInfo(gpType.ToString(), 0, kTypeOE, setType);
                                    BInfo bInfoO = new BInfo(Config.GetBType(kTypeOE, setType, 1));
                                    BInfo bInfoE = new BInfo(Config.GetBType(kTypeOE, setType, 2));
                                    decimal odds_A;
                                    decimal odds_B;

                                    //手動設置OE (排除 ZD 全場/上下半/單節 主客隊總得分 oe設置)
                                    if ((gameInfo.GpType != GpType.Zd && Config.isOpenDsZpOe) || (gameInfo.GpType == GpType.Zd && Config.isOpenZdOe && (specials < 14 || specials > 29)))
                                    {
                                        if (gameInfo.GpType == GpType.Zd && !is3x3)
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
                                    gameInfo.ObjDic[gpdm].TryAdd(kTypeOE, kInfo);
                                }
                            }

                        }
                    }

                    #endregion

                    //賽事刪除
                    if (matchInfo.IsRemove)
                    {
                        delList.Add(matchInfo.MatchId);
                    }
                    else
                    {
                        gameInfo.CheckPlAndSet();
                    }
                }

                foreach (KeyValuePair<string, GameInfo> kvp in tmpGameDic)
                {
                    //因應來源名稱替換功能，需要抓出修改前的賽事進行關閉
                    foreach (GameInfo game in Config.gameDic.Values.Where(a => a.GetSourceGameDictKey() == kvp.Value.GetSourceGameDictKey() && a.GetGameDictKey() != kvp.Value.GetGameDictKey()))
                    {
                        game.DataId = -1;
                    }

                    //存在才處理
                    if (Config.gameDic.TryGetValue(kvp.Key, out GameInfo getGame))
                    {
                        getGame.Compare(kvp.Value, gpType);

                        //註記刪除不存在的玩法
                        var delKinfo = getGame.ObjDic.Where(a => !kvp.Value.ObjDic.Keys.Contains(a.Key)).SelectMany(a => a.Value);

                        foreach (KeyValuePair<int, KInfo> kInfo in delKinfo)
                        {
                            kInfo.Value.DataId = -1;
                        }
                    }
                    //不存在直接添加
                    else
                    {
                        Config.gameDic.TryAdd(kvp.Key, kvp.Value.DeepClone());
                    }
                }

                //首包解析，進行賽事比對
                if (isComplete)
                {
                    //對應賽事列表
                    var gameList = (from g in Config.gameDic
                                    where g.Value.GpType == gpType
                                    select g);
                    //查詢不存在的game  重新訂閱前剛好賽事移除，導至首包無該場賽事
                    var t = gameList.Where(a => !tmpGameDic.Keys.Contains(a.Key)).Select(a => a.Value);

                    foreach (GameInfo game in t)
                    {
                        game.DataId = -1;
                    }
                }

                //刪除賽事列表
                if (delList.Count > 0)
                {
                    //對應賽事列表
                    IEnumerable<GameInfo> gameList = Config.gameDic.Values.Where(a => a.GpType == gpType);

                    foreach (GameInfo game in gameList)
                    {
                        foreach (GpdmInfo playDic in game.ObjDic.Values)
                        {
                            foreach (KInfo kInfo in playDic.Values)
                            {
                                //是否存在刪除列表
                                if (delList.Contains(kInfo.SB_MatchId))
                                {
                                    kInfo.DataId = -1;
                                }
                            }
                        }

                        //是否為玩法全部刪除
                        bool isAllDel = !game.ObjDic.Values.Any(a => a.Values.Any(b => b.DataId != -1));
                        //全部刪除則game也刪除
                        if (isAllDel)
                        {
                            game.DataId = -1;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, dataInfo.StrType + Environment.NewLine + ex.ToString() + Environment.NewLine + "來源資料:" + YwCore.Common.regexJson.Replace(dataInfo.Data, "$1"));
            }
        }

        /// <summary>
        /// MY盤轉HK盤[足球首頁只給MY盤]
        /// </summary>
        /// <param name="ratioMY">MY盤數值</param>
        /// <returns>HK盤數值</returns>
        private static decimal ChangeRatio(decimal ratioMY)
        {
            return ratioMY < 0 ? Math.Floor((-1 / ratioMY) * 100) / 100 : ratioMY;
        }

        /// <summary>
        /// 從已知Gpdm倒推取得Specials值
        /// </summary>
        /// <param name="gpdm">賽事Gpdm</param>
        /// <returns>返回Specials</returns>
        public static int GetSpecials(decimal gpdm)
        {
            int specials = -1;
            int.TryParse(decimal.Truncate(gpdm / 100m).ToString(), out specials);
            return specials;
        }

        /// <summary>
        /// 從來源resourceid推Specials值
        /// </summary>
        /// <param name="resourceid">來源場次</param>
        /// <param name="tmpSpecials">返回Specials</param>
        /// <returns>有無拿到對應場次</returns>
        public static bool GetStatus(string resourceid, out int tmpSpecials)
        {
            tmpSpecials = 0;

            if (string.IsNullOrEmpty(resourceid)) { return false; }

            switch (resourceid)
            {
                case "01":
                    tmpSpecials = 3;
                    break;
                case "02":
                    tmpSpecials = 4;
                    break;
                case "03":
                    tmpSpecials = 5;
                    break;
                case "04":
                    tmpSpecials = 6;
                    break;
                default:
                    return false;
            }

            return true;
        }

        /// <summary>
        /// (首先獲得N分)從來源resourceid推Specials值
        /// </summary>
        /// <param name="resourceid">來源場次</param>
        /// <param name="tmpSpecials">返回Specials</param>
        /// <returns>有無拿到對應場次</returns>
        public static bool GetStatusFirst(string resourceid, out int tmpSpecials, out int tmpSetType)
        {
            tmpSpecials = 0;
            tmpSetType = 0;

            if (string.IsNullOrEmpty(resourceid)) { return false; }

            if (int.TryParse(resourceid, out int value))
            {
                tmpSetType = value % 100;

                switch (value / 100)
                {
                    case 1://來源"第1節-首先獲得N分"解析成"全場-首先獲得N分"
                        tmpSpecials = 101;
                        break;
                    case 2:
                        tmpSpecials = 105;
                        break;
                    case 3:
                        tmpSpecials = 106;
                        break;
                    case 4:
                        tmpSpecials = 107;
                        break;
                    default:
                        return false;
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// (單隊總入球)從來源resourceid推Specials值
        /// </summary>
        /// <param name="isHomeTeam">是否為主隊玩法</param>
        /// <param name="resourceid">來源場次</param>
        /// <param name="tmpSpecials">返回Specials</param>
        /// <returns>有無拿到對應場次</returns>
        public static bool GetStatus(bool isHomeTeam, string resourceid, out int tmpSpecials)
        {
            tmpSpecials = 0;

            if (string.IsNullOrEmpty(resourceid)) { return false; }

            switch (resourceid)
            {
                case "01":
                    tmpSpecials = isHomeTeam ? 20 : 21;
                    break;
                case "02":
                    tmpSpecials = isHomeTeam ? 22 : 23;
                    break;
                case "03":
                    tmpSpecials = isHomeTeam ? 24 : 25;
                    break;
                case "04":
                    tmpSpecials = isHomeTeam ? 26 : 27;
                    break;
                default:
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 轉換比分(轉型態)
        /// </summary>
        /// <param name="a">A隊分數資料</param>
        /// <param name="b">B隊分數資料</param>
        /// <param name="scoreA">A隊分數</param>
        /// <param name="scoreB">B隊分數</param>
        /// <param name="isChange">是否為NBA進行交換比分</param>
        private static bool ParserScore(object a, object b, out byte scoreA, out byte scoreB, bool isChange = false)
        {
            scoreA = scoreB = 0;
            bool isOpen = byte.TryParse(a?.ToString(), out scoreA) && byte.TryParse(b?.ToString(), out scoreB);

            if (scoreA < 0) { scoreA = 0; }
            if (scoreB < 0) { scoreB = 0; }

            ConvertTools.ExchangeValue(ref scoreA, ref scoreB, isChange);

            return isOpen;
        }

        /// <summary>
        /// NBA DS 全場上半鴛鴦盤處理
        /// </summary>
        /// <param name="specials">玩法specials 全場0 上半1</param>
        /// <param name="type">rf: 1  ou:2</param>
        /// <param name="list">來源pkpl</param>
        /// <param name="gameInfo">GameInfo</param>
        /// <param name="gpType">GP類型</param>
        /// <param name="matchId">標記SB_matchId</param>
        /// <param name="isNBA">NBA</param>
        private static void DsSameCount(int specials, int type, List<PlateInfo> list, GameInfo gameInfo, GpType gpType, decimal matchId, bool isNBA)
        {
            #region 整理成成PlateInfo

            PlateInfo plateInfo_a = list.FirstOrDefault();
            list = list.OrderBy(x => x.Plate).ToList();
            //主盤位置
            int index_a = list.IndexOf(plateInfo_a);

            #endregion

            //要抓取鴛鴦盤的數量 (上半需要勾上選勾才判斷)
            int sameCount = (specials == 1 && !Config.isSbDjSame) || Config.sameCount == 1 ? 1 : Config.sameCount == 2 ? 3 : 5;
            //存放抓取鴛鴦盤(存放BCDE盤)
            PlateInfo[] plateInfos = new PlateInfo[4];
            //判斷是否全好全壞s
            bool isAllBacdGood = index_a == 0 || index_a == list.Count - 1;
            //存放全好全壞固定方向抓法
            bool[] isMinus = new bool[2];

            //整理排序鴛鴦盤要抓的鴛鴦盤
            for (int i = 1; i < sameCount; i++)
            {
                Config.ArrangedNbaDsFtHtSameCountOdds(list, plateInfo_a, type, i, sameCount, isAllBacdGood, ref plateInfos, ref isMinus[0]);
            }

            //排除為null，遞補邏輯
            plateInfos = plateInfos.Where(x => x != null).ToArray();

            //給pkpl (A盤RF OU 一起給)
            for (int i = 0; i < sameCount; i++)
            {
                PlateInfo plateInfo = i == 0 ? plateInfo_a : plateInfos.Length > i - 1 ? plateInfos[i - 1] : null;

                if (Config.SetKindType(gameInfo.GpType, specials, type, out int kType))
                {
                    byte setType = gameInfo.GetSetType(kType);
                    //生成物件
                    KInfo kInfo = new KInfo(gpType.ToString(), 0, kType, setType);
                    kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;

                    BInfo bInfoA = new BInfo(Config.GetBType(kType, setType, 1));
                    BInfo bInfoB = new BInfo(Config.GetBType(kType, setType, 2));

                    if (plateInfo != null)
                    {
                        //PK,PL
                        decimal plate = Math.Abs(plateInfo.Plate);
                        decimal odds_A = decimal.Floor(plateInfo.Ratio_H * 100m) / 100m;
                        decimal odds_B = decimal.Floor(plateInfo.Ratio_A * 100m) / 100m;

                        //RF額外給RT
                        if (type == 1)
                        {
                            //RT[1:A隊讓 0:B隊讓]
                            int strong = plateInfo.Plate < 0 ? 0 : 1;
                            if (!isNBA) { strong = strong == 1 ? 0 : 1; }
                            ConvertTools.ExchangeValue(ref odds_A, ref odds_B, isNBA);

                            //設置數據
                            kInfo.SetLine(plate, strong);
                            bInfoA.SetOdds(odds_A);
                            bInfoB.SetOdds(odds_B);
                        }
                        else
                        {
                            kInfo.SetLine(plate);
                            bInfoA.SetOdds(odds_A);
                            bInfoB.SetOdds(odds_B);
                        }

                        kInfo.SetIsOpen();
                    }

                    //存放SB來源賽事編號
                    kInfo.SB_MatchId = matchId;
                    //放進字典
                    kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                    kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                    //生成gpdm並新增對應字典
                    decimal gpdm = specials * 100 + setType;
                    gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                    gameInfo.ObjDic[gpdm].TryAdd(kType, kInfo);
                }
            }
        }
    }
}
